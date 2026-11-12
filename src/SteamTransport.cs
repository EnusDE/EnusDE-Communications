// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Steamworks;
namespace EnusDE.Communications
{
    // Separate Steam messages channel; no game packet schemas or broadcast hooks.
    internal sealed class SteamTransport:IDisposable
    {
        const int Channel=189731,MaxBytes=8192;
        readonly IntPtr[] buffer=new IntPtr[16];
        readonly Func<ulong,bool> allowed;
        readonly Action<ulong,string> receive;
        readonly HashSet<ulong> peers=new HashSet<ulong>();
        readonly Dictionary<ulong,string> failures=new Dictionary<ulong,string>();
        readonly Action<string> report;
        readonly PerformanceTrace performance;
        Callback<SteamNetworkingMessagesSessionRequest_t> session;
        Callback<SteamNetworkingMessagesSessionFailed_t> failed;
        public SteamTransport(Func<ulong,bool> authorize,Action<ulong,string> onReceive,Action<string> log,PerformanceTrace trace=null)
        {
            allowed=authorize;receive=onReceive;report=log;performance=trace;
            session=Callback<SteamNetworkingMessagesSessionRequest_t>.Create(request=>{var identity=request.m_identityRemote;ulong peer=identity.GetSteamID64();if(allowed(peer)){if(SteamNetworkingMessages.AcceptSessionWithUser(ref identity))peers.Add(peer);else Report(peer,"Steam chat session could not be accepted.");}});
            failed=Callback<SteamNetworkingMessagesSessionFailed_t>.Create(error=>{ulong peer=error.m_info.m_identityRemote.GetSteamID64();if(peers.Contains(peer)||allowed(peer))Report(peer,"Steam chat session failed: "+error.m_info.m_eEndReason+" / "+error.m_info.m_szEndDebug);});
        }
        void Report(ulong peer,string message){string previous;if(!failures.TryGetValue(peer,out previous)||previous!=message){failures[peer]=message;report?.Invoke(message);}}
        internal string Describe(ulong peer)
        {
            if(peer==0)return "host identity not available";
            try
            {
                var identity=new SteamNetworkingIdentity();identity.SetSteamID64(peer);SteamNetConnectionInfo_t info;SteamNetConnectionRealTimeStatus_t status;
                var state=SteamNetworkingMessages.GetSessionConnectionInfo(ref identity,out info,out status);
                return state+"; end reason: "+info.m_eEndReason+" / "+info.m_szEndDebug;
            }
            catch(Exception error){return "session information unavailable: "+error.Message;}
        }
        public bool Send(ulong peer,string text,bool reconnect=false)
        {
            using var measurement=performance?.Measure(PerformanceStage.Send)??default;
            if(peer==0||!allowed(peer))return false;
            byte[] data=Encoding.UTF8.GetBytes(text);if(data.Length>MaxBytes)return false;
            var identity=new SteamNetworkingIdentity();identity.SetSteamID64(peer);var handle=GCHandle.Alloc(data,GCHandleType.Pinned);
            try
            {
                // Only a new handshake may acknowledge/restart a broken session. Chat text is never resent.
                int flags=Constants.k_nSteamNetworkingSend_Reliable|(reconnect?Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession:0);
                var result=SteamNetworkingMessages.SendMessageToUser(ref identity,handle.AddrOfPinnedObject(),(uint)data.Length,flags,Channel);
                if(result==EResult.k_EResultOK){performance?.Sent(data.Length);peers.Add(peer);failures.Remove(peer);return true;}
                Report(peer,"Steam chat send failed: "+result);return false;
            }
            finally{handle.Free();}
        }
        public void Poll()
        {
            using var measurement=performance?.Measure(PerformanceStage.Poll)??default;
            int count=SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel,buffer,buffer.Length);
            for(int i=0;i<count;i++)
            {
                IntPtr ptr=buffer[i];
                try
                {
                    var message=SteamNetworkingMessage_t.FromIntPtr(ptr);ulong sender=message.m_identityPeer.GetSteamID64();
                    if(message.m_cbSize<=0||message.m_cbSize>MaxBytes||!allowed(sender))continue;
                    performance?.Received(message.m_cbSize);var data=new byte[message.m_cbSize];Marshal.Copy(message.m_pData,data,0,data.Length);receive(sender,new UTF8Encoding(false,true).GetString(data));
                }
                catch(DecoderFallbackException){}
                finally{SteamNetworkingMessage_t.Release(ptr);buffer[i]=IntPtr.Zero;}
            }
        }
        public void Dispose()
        {
            session?.Dispose();session=null;failed?.Dispose();failed=null;
            foreach(var peer in peers){try{var identity=new SteamNetworkingIdentity();identity.SetSteamID64(peer);SteamNetworkingMessages.CloseChannelWithUser(ref identity,Channel);}catch{}}
            peers.Clear();failures.Clear();
        }
    }
}
