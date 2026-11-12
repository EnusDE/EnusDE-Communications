// Test-only Steam boundary. No fake types are shipped in the mod.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace Steamworks
{
    public enum EResult { k_EResultOK=1,k_EResultNoConnection=3 }
    public enum ESteamNetworkingConnectionState { None,Connected,ProblemDetectedLocally }
    public struct SteamNetConnectionRealTimeStatus_t {}
    public static class Constants { public const int k_nSteamNetworkingSend_Reliable=8,k_nSteamNetworkingSend_AutoRestartBrokenSession=32; }
    public struct SteamNetworkingIdentity { ulong id;public void SetSteamID64(ulong value){id=value;}public ulong GetSteamID64()=>id; }
    public struct SteamNetConnectionInfo_t { public SteamNetworkingIdentity m_identityRemote;public int m_eEndReason;public string m_szEndDebug; }
    public struct SteamNetworkingMessagesSessionRequest_t { public SteamNetworkingIdentity m_identityRemote; }
    public struct SteamNetworkingMessagesSessionFailed_t { public SteamNetConnectionInfo_t m_info; }
    public sealed class Callback<T>:IDisposable
    {
        public static Action<T> Handler;
        Action<T> callback;
        public static Callback<T> Create(Action<T> action){Handler+=action;return new Callback<T>{callback=action};}
        public void Dispose(){Handler-=callback;}
    }
    public struct SteamNetworkingMessage_t
    {
        public SteamNetworkingIdentity m_identityPeer;public IntPtr m_pData;public int m_cbSize;
        public static SteamNetworkingMessage_t FromIntPtr(IntPtr ptr)=>SteamNetworkingMessages.Messages[ptr];
        public static void Release(IntPtr ptr){Marshal.FreeHGlobal(SteamNetworkingMessages.Messages[ptr].m_pData);SteamNetworkingMessages.Messages.Remove(ptr);SteamNetworkingMessages.Released++;}
    }
    public static class SteamNetworkingMessages
    {
        public static bool Broken;public static int Sends,Flags,Accepted,Closed,Released,Channel;
        public static byte[] Sent;
        public static readonly Dictionary<IntPtr,SteamNetworkingMessage_t> Messages=new Dictionary<IntPtr,SteamNetworkingMessage_t>();
        static readonly Queue<IntPtr> queued=new Queue<IntPtr>();static int sequence;
        public static void Reset(){Broken=false;Sends=Flags=Accepted=Closed=Released=0;Sent=null;}
        public static bool AcceptSessionWithUser(ref SteamNetworkingIdentity identity){Accepted++;return true;}
        public static ESteamNetworkingConnectionState GetSessionConnectionInfo(ref SteamNetworkingIdentity identity,out SteamNetConnectionInfo_t info,out SteamNetConnectionRealTimeStatus_t status)
        {info=new SteamNetConnectionInfo_t{m_eEndReason=Broken?5003:0,m_szEndDebug=Broken?"Timeout":""};status=new SteamNetConnectionRealTimeStatus_t();return Broken?ESteamNetworkingConnectionState.ProblemDetectedLocally:ESteamNetworkingConnectionState.Connected;}
        public static bool CloseChannelWithUser(ref SteamNetworkingIdentity identity,int channel){Closed++;Channel=channel;return true;}
        public static EResult SendMessageToUser(ref SteamNetworkingIdentity identity,IntPtr data,uint count,int flags,int channel)
        {
            Sends++;Flags=flags;Channel=channel;
            if((flags&Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession)!=0)Broken=false;
            if(Broken)return EResult.k_EResultNoConnection;
            Sent=new byte[count];Marshal.Copy(data,Sent,0,(int)count);return EResult.k_EResultOK;
        }
        public static int ReceiveMessagesOnChannel(int channel,IntPtr[] buffer,int maximum)
        {int count=0;while(count<maximum&&queued.Count>0)buffer[count++]=queued.Dequeue();return count;}
        public static void Queue(ulong sender,byte[] data)
        {
            var identity=new SteamNetworkingIdentity();identity.SetSteamID64(sender);var payload=Marshal.AllocHGlobal(data.Length);Marshal.Copy(data,0,payload,data.Length);
            var ptr=new IntPtr(++sequence);Messages[ptr]=new SteamNetworkingMessage_t{m_identityPeer=identity,m_pData=payload,m_cbSize=data.Length};queued.Enqueue(ptr);
        }
    }
}
