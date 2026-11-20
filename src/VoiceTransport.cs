// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
namespace EnusDE.Communications
{
    // Text transport owns session authorization; voice only owns this separate channel.
    internal sealed class VoiceTransport:IDisposable
    {
        const int Channel=189732;
        readonly IntPtr[] buffer=new IntPtr[32];readonly HashSet<ulong> peers=new HashSet<ulong>();
        readonly Func<ulong,bool> allowed;readonly Action<ulong,byte[]> receive;
        internal VoiceTransport(Func<ulong,bool> authorize,Action<ulong,byte[]> callback){allowed=authorize;receive=callback;}
        internal bool Send(ulong peer,VoicePacket packet)
        {
            if(peer==0||!allowed(peer))return false;
            byte[] data=VoiceWire.Write(packet);if(data.Length>VoiceWire.MaxBytes)return false;
            var identity=new SteamNetworkingIdentity();identity.SetSteamID64(peer);var handle=GCHandle.Alloc(data,GCHandleType.Pinned);
            try
            {
                // Never queue old speech for retransmission. Handshakes are repeated periodically.
                var result=SteamNetworkingMessages.SendMessageToUser(ref identity,handle.AddrOfPinnedObject(),(uint)data.Length,Constants.k_nSteamNetworkingSend_Unreliable|Constants.k_nSteamNetworkingSend_NoNagle,Channel);
                if(result!=EResult.k_EResultOK)return false;peers.Add(peer);return true;
            }
            finally{handle.Free();}
        }
        internal void Poll()
        {
            int count=SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel,buffer,buffer.Length);
            Exception failure=null;
            for(int i=0;i<count;i++)
            {
                IntPtr ptr=buffer[i];
                try
                {
                    var message=SteamNetworkingMessage_t.FromIntPtr(ptr);ulong sender=message.m_identityPeer.GetSteamID64();
                    if(failure!=null||message.m_cbSize<VoiceWire.Header||message.m_cbSize>VoiceWire.MaxBytes||!allowed(sender))continue;
                    if(SteamNetworkingUtils.GetLocalTimestamp().m_SteamNetworkingMicroseconds-message.m_usecTimeReceived.m_SteamNetworkingMicroseconds>200000)continue;
                    var data=new byte[message.m_cbSize];Marshal.Copy(message.m_pData,data,0,data.Length);receive(sender,data);
                }
                catch(Exception error){failure=error;}
                finally{SteamNetworkingMessage_t.Release(ptr);buffer[i]=IntPtr.Zero;}
            }
            if(failure!=null)throw failure;
        }
        public void Dispose()
        {
            foreach(ulong peer in peers)try{var id=new SteamNetworkingIdentity();id.SetSteamID64(peer);SteamNetworkingMessages.CloseChannelWithUser(ref id,Channel);}catch{}
            peers.Clear();
        }
    }
}
