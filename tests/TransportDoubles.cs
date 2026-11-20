// Test-only Steam boundary. No fake types are shipped in the mod.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace Steamworks
{
    public enum EResult { k_EResultOK=1,k_EResultNoConnection=3 }
    public enum ESteamNetworkingConnectionState { None,Connected,ProblemDetectedLocally }
    public struct SteamNetConnectionRealTimeStatus_t {}
    public static class Constants { public const int k_nSteamNetworkingSend_Unreliable=0,k_nSteamNetworkingSend_NoNagle=1,k_nSteamNetworkingSend_Reliable=8,k_nSteamNetworkingSend_AutoRestartBrokenSession=32; }
    public struct SteamNetworkingMicroseconds {public long m_SteamNetworkingMicroseconds;}
    public static class SteamNetworkingUtils {public static long Now=1000000;public static SteamNetworkingMicroseconds GetLocalTimestamp()=>new SteamNetworkingMicroseconds{m_SteamNetworkingMicroseconds=Now};}
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
        public SteamNetworkingMicroseconds m_usecTimeReceived;
        public SteamNetworkingIdentity m_identityPeer;public IntPtr m_pData;public int m_cbSize;
        public static SteamNetworkingMessage_t FromIntPtr(IntPtr ptr)=>SteamNetworkingMessages.Messages[ptr];
        public static void Release(IntPtr ptr){Marshal.FreeHGlobal(SteamNetworkingMessages.Messages[ptr].m_pData);SteamNetworkingMessages.Messages.Remove(ptr);SteamNetworkingMessages.Released++;}
    }
    public static class SteamNetworkingMessages
    {
        public static bool Broken;public static int Sends,Flags,Accepted,Closed,Released,Channel;
        public static byte[] Sent;
        public static readonly List<Tuple<ulong,int,byte[]>> SentMessages=new List<Tuple<ulong,int,byte[]>>();
        static readonly Dictionary<IntPtr,int> channels=new Dictionary<IntPtr,int>();
        public static readonly Dictionary<IntPtr,SteamNetworkingMessage_t> Messages=new Dictionary<IntPtr,SteamNetworkingMessage_t>();
        static readonly Queue<IntPtr> queued=new Queue<IntPtr>();static int sequence;
        public static void Reset(){Broken=false;Sends=Flags=Accepted=Closed=Released=0;Sent=null;SentMessages.Clear();}
        public static bool AcceptSessionWithUser(ref SteamNetworkingIdentity identity){Accepted++;return true;}
        public static ESteamNetworkingConnectionState GetSessionConnectionInfo(ref SteamNetworkingIdentity identity,out SteamNetConnectionInfo_t info,out SteamNetConnectionRealTimeStatus_t status)
        {info=new SteamNetConnectionInfo_t{m_eEndReason=Broken?5003:0,m_szEndDebug=Broken?"Timeout":""};status=new SteamNetConnectionRealTimeStatus_t();return Broken?ESteamNetworkingConnectionState.ProblemDetectedLocally:ESteamNetworkingConnectionState.Connected;}
        public static bool CloseChannelWithUser(ref SteamNetworkingIdentity identity,int channel){Closed++;Channel=channel;return true;}
        public static EResult SendMessageToUser(ref SteamNetworkingIdentity identity,IntPtr data,uint count,int flags,int channel)
        {
            Sends++;Flags=flags;Channel=channel;
            if((flags&Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession)!=0)Broken=false;
            if(Broken)return EResult.k_EResultNoConnection;
            Sent=new byte[count];Marshal.Copy(data,Sent,0,(int)count);SentMessages.Add(Tuple.Create(identity.GetSteamID64(),channel,Sent));return EResult.k_EResultOK;
        }
        public static int ReceiveMessagesOnChannel(int channel,IntPtr[] buffer,int maximum)
        {int count=0,total=queued.Count;for(int i=0;i<total;i++){var ptr=queued.Dequeue();if(count<maximum&&channels[ptr]==channel){buffer[count++]=ptr;channels.Remove(ptr);}else queued.Enqueue(ptr);}return count;}
        public static void Queue(ulong sender,byte[] data,int channel=189731,long age=0)
        {
            var identity=new SteamNetworkingIdentity();identity.SetSteamID64(sender);var payload=Marshal.AllocHGlobal(data.Length);Marshal.Copy(data,0,payload,data.Length);
            var ptr=new IntPtr(++sequence);Messages[ptr]=new SteamNetworkingMessage_t{m_usecTimeReceived=new SteamNetworkingMicroseconds{m_SteamNetworkingMicroseconds=SteamNetworkingUtils.Now-age},m_identityPeer=identity,m_pData=payload,m_cbSize=data.Length};channels[ptr]=channel;queued.Enqueue(ptr);
        }
    }
}
