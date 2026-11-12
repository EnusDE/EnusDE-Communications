using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EnusDE.Communications;
using Steamworks;
static class TransportTests
{
    static int passed;
    static void Check(bool value,string why){if(!value)throw new Exception(why);}
    static void Test(string name,Action test){SteamNetworkingMessages.Reset();test();passed++;Console.WriteLine("PASS "+name);}
    static void Main()
    {
        Test("broken Steam session recovers through hello without replaying chat text",()=>{
            var logs=new List<string>();using(var transport=new SteamTransport(id=>id==42,(id,text)=>{},logs.Add))
            {
                SteamNetworkingMessages.Broken=true;
                Check(transport.Describe(42).Contains("5003")&&transport.Describe(0).Contains("not available"),"Handshake timeout diagnostics omitted the failure stage");
                Check(!transport.Send(42,"private chat")&&!transport.Send(42,"private chat"),"Broken text send unexpectedly succeeded");
                Check(logs.Count==1&&!logs[0].Contains("private chat"),"Repeated errors spammed logs or disclosed chat text");
                Check(transport.Send(42,"hello",true),"New hello cannot recover the failed session");
                Check(Encoding.UTF8.GetString(SteamNetworkingMessages.Sent)=="hello"&&SteamNetworkingMessages.Sends==3,"Old chat text was replayed");
                Check(transport.Send(42,"Grüße 日本語")&&Encoding.UTF8.GetString(SteamNetworkingMessages.Sent)=="Grüße 日本語","UTF-8 transport corrupted text");
                Check((SteamNetworkingMessages.Flags&Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession)==0,"Ordinary messages restart failed sessions");
            }
            Check(SteamNetworkingMessages.Closed==1&&Callback<SteamNetworkingMessagesSessionRequest_t>.Handler==null&&Callback<SteamNetworkingMessagesSessionFailed_t>.Handler==null,"Session callback or owned channel survived disposal");
        });
        Test("Steam boundary rejects strangers and releases invalid incoming messages",()=>{
            int received=0;using(var transport=new SteamTransport(id=>id==42,(id,text)=>received++,message=>{}))
            {
                Check(!transport.Send(9,"forbidden")&&!transport.Send(42,new string('x',8193))&&SteamNetworkingMessages.Sends==0,"Invalid outgoing packet was sent");
                var identity=new SteamNetworkingIdentity();identity.SetSteamID64(9);Callback<SteamNetworkingMessagesSessionRequest_t>.Handler(new SteamNetworkingMessagesSessionRequest_t{m_identityRemote=identity});Check(SteamNetworkingMessages.Accepted==0,"Unknown session accepted");
                identity.SetSteamID64(42);Callback<SteamNetworkingMessagesSessionRequest_t>.Handler(new SteamNetworkingMessagesSessionRequest_t{m_identityRemote=identity});Check(SteamNetworkingMessages.Accepted==1,"Authorized session ignored");
                SteamNetworkingMessages.Queue(9,Encoding.UTF8.GetBytes("forged"));SteamNetworkingMessages.Queue(42,new byte[]{0xff});SteamNetworkingMessages.Queue(42,new byte[8193]);SteamNetworkingMessages.Queue(42,Encoding.UTF8.GetBytes("valid"));transport.Poll();
                Check(received==1&&SteamNetworkingMessages.Released==4&&SteamNetworkingMessages.Messages.Count==0,"Rejected packets reached handler or native memory was leaked");
            }
        });
        Test("Steam session failure exposes bounded diagnostics only for known peers",()=>{
            var logs=new List<string>();using(var transport=new SteamTransport(id=>id==42,(id,text)=>{},logs.Add))
            {
                var identity=new SteamNetworkingIdentity();identity.SetSteamID64(9);var failure=new SteamNetworkingMessagesSessionFailed_t{m_info=new SteamNetConnectionInfo_t{m_identityRemote=identity,m_eEndReason=5003,m_szEndDebug="Timeout"}};
                Callback<SteamNetworkingMessagesSessionFailed_t>.Handler(failure);Check(logs.Count==0,"Unknown session triggered diagnostic");identity.SetSteamID64(42);failure.m_info.m_identityRemote=identity;
                Callback<SteamNetworkingMessagesSessionFailed_t>.Handler(failure);Callback<SteamNetworkingMessagesSessionFailed_t>.Handler(failure);Check(logs.Count==1&&logs[0].Contains("5003")&&logs[0].Contains("Timeout"),"Missing or repeated failure details");
            }
        });
        Test("enabled timing preserves payload delivery, authorization and native message release",()=>{
            long now=0;var trace=new PerformanceTrace(()=>now++,1000);trace.Start();trace.NextFrame(1,false,true,false);string received=null;
            using(var transport=new SteamTransport(id=>id==42,(id,text)=>received=text,message=>{},trace))
            {
                Check(transport.Send(42,"private text")&&!transport.Send(9,"forged"),"Timing changed send authorization");
                SteamNetworkingMessages.Queue(42,Encoding.UTF8.GetBytes("reply text"));transport.Poll();Check(received=="reply text"&&SteamNetworkingMessages.Released==1,"Timing altered receive/release");
            }
            trace.Stop();using(var csv=new System.IO.StringWriter()){trace.Write(csv);string data=csv.ToString();Check(!data.Contains("private text")&&!data.Contains("reply text")&&!data.Contains("forged"),"Packet contents leaked into performance capture");}
        });
        Console.WriteLine("RESULT "+passed+" transport regression tests passed");
    }
}
