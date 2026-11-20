// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Linq;
using EnusDE.Communications;
using Steamworks;
using UnityEngine;
static class VoiceRuntimeTests
{
    static int passed;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Test(string name,Action action){action();passed++;Console.WriteLine("PASS "+name);}
    sealed class Setup:IDisposable
    {
        internal CommsRuntime Runtime=new CommsRuntime();internal VoiceRuntime Voice;
        internal Guid A=Guid.NewGuid(),B=Guid.NewGuid();
        internal Setup(bool host=true)
        {
            SteamNetworkingMessages.Reset();GameAdapter.Ready=GameAdapter.MultiplayerActive=true;GameAdapter.IsHost=host;GameAdapter.Local=host?1ul:2ul;GameAdapter.Host=1;
            Time.realtimeSinceStartupAsDouble+=10;Application.isFocused=true;GameMenus.IsOpen=false;
            Microphone.Starts=Microphone.Ends=0;Microphone.Recording=false;Microphone.devices=new[]{"Test microphone"};AudioSource.Plays=AudioSource.Stops=0;
            Runtime.Entry.Settings.VoiceBinding="<Keyboard>/v";
            Runtime.View.People["1"]=new Person{Id="1"};Runtime.View.People["2"]=new Person{Id="2",X=10};Runtime.View.People["3"]=new Person{Id="3",X=20};
            Voice=new VoiceRuntime(Runtime);
        }
        internal void Enable(){Runtime.Entry.Settings.Host.VoiceAllowed=true;Runtime.Entry.Settings.VoiceEnabled=true;Voice.Tick();}
        internal void Queue(ulong sender,VoicePacket packet,long age=0){SteamNetworkingMessages.Queue(sender,VoiceWire.Write(packet),189732,age);}
        internal VoicePacket Hello(ulong sender,Guid consent)=>new VoicePacket{Kind=1,Sender=sender,Consent=consent};
        internal VoicePacket Audio(ulong sender,Guid consent,uint seq=1)=>new VoicePacket{Kind=3,Sender=sender,Consent=consent,Sequence=seq,Stream=123,Audio=VoiceAudio.Encode(new float[640],1)};
        internal VoicePacket[] Sent=>SteamNetworkingMessages.SentMessages.Select(p=>VoiceWire.Read(p.Item3)).ToArray();
        internal void Join(){Queue(2,Hello(2,A));Queue(3,Hello(3,B));Voice.Tick();SteamNetworkingMessages.SentMessages.Clear();}
        public void Dispose(){Voice.Dispose();Check(SteamNetworkingMessages.Messages.Count==0,"Unreleased received Steam messages");}
    }
    internal static void Run()
    {
        Test("codec preserves speech-band waveform and bounds decoded samples",()=>{
            var pcm=Enumerable.Range(0,640).Select(i=>(float)(.4*Math.Sin(2*Math.PI*440*i/16000+1))).ToArray();
            byte[] compressed=VoiceAudio.Encode(pcm,1);float[] decoded=VoiceAudio.Decode(compressed);
            double error=pcm.Select((v,i)=>Math.Pow(v-decoded[i],2)).Average();
            Check(compressed.Length==324&&decoded.Length==640&&error<.001&&decoded.All(v=>v>=-1&&v<=1),"Codec waveform or size regression");
            Check(Math.Abs(decoded[0]-pcm[0])<.01,"Loud block starts near silence");
        });
        Test("wire rejects truncation, oversized frames and incompatible protocol",()=>{
            for(int n=0;n<500;n++)Check(VoiceWire.Read(new byte[n])==null,"Accepted invalid envelope");
            var p=new VoicePacket{Kind=1,Consent=Guid.NewGuid(),Sender=2};var data=VoiceWire.Write(p);Check(VoiceWire.Read(data)!=null,"Valid heartbeat rejected");data[2]=99;Check(VoiceWire.Read(data)==null,"Accepted future protocol");
        });
        Test("voice distance rejects invalid positions and fades to the boundary",()=>{
            var a=new Person();var b=new Person{X=20};Check(Math.Abs(VoiceWire.Gain(a,b,40)-20f/38)<.001,"Distance fade");b.X=40;Check(VoiceWire.Gain(a,b,40)==0,"Boundary leak");b.X=float.NaN;Check(VoiceWire.Gain(a,b,40)==0,"Invalid coordinate leak");
            Check(VoiceWire.Newer(0,uint.MaxValue)&&!VoiceWire.Newer(4,5),"Sequence wrap/reordering");
        });
        Test("jitter buffer bounds backlog, clears and outputs silence on underrun",()=>{
            var b=new VoiceBuffer();
            for(int i=1;i<=100;i++)b.Push(Enumerable.Repeat(i/100f,640).ToArray());
            var output=new float[6400];b.Read(output,1,16000,1);
            Check(output[0]==.96f&&output[3199]==1&&output[3200]==0,"Overflow stalled or played old speech");
            b.Clear();b.Read(output,1,16000,1);Check(output.All(v=>v==0),"Audio survived clear");
        });
        Test("paced stereo output survives one lost packet and uneven mixer callbacks",()=>{
            var b=new VoiceBuffer();var frame=Enumerable.Repeat(.5f,640).ToArray();
            b.Push(frame);b.Push(frame);b.Push(frame);
            for(int packet=0;packet<40;packet++)
            {
                // Four differently sized callbacks consume 40 ms of 48 kHz output.
                foreach(int frames in new[]{512,448,384,576})
                {
                    var stereo=new float[frames*2];
                    b.Read(stereo,2,48000,.5f);
                    Check(stereo.All(v=>v>0&&v<=.25f),"A dropout interrupted the speech stream");
                    for(int i=0;i<stereo.Length;i+=2)Check(stereo[i]==stereo[i+1],"Interleaved channels drifted");
                }
                if(packet==10)continue;
                b.Push(frame,missingFrame:packet==11);
            }
        });
        Test("default-off runtime creates neither microphone nor network traffic",()=>{using(var f=new Setup()){f.Voice.Tick();Check(Microphone.Starts==0&&SteamNetworkingMessages.Sends==0,"Default-off has side effects");}});
        Test("host relays only opted-in nearby peers without personal microphone access",()=>{using(var f=new Setup()){
            f.Runtime.Entry.Settings.Host.VoiceAllowed=true;f.Voice.Tick();f.Join();f.Queue(2,f.Audio(2,f.A));f.Voice.Tick();
            Check(f.Sent.Count(p=>p.Kind==3)==1&&SteamNetworkingMessages.SentMessages.Single().Item1==3&&Microphone.Starts==0&&AudioSource.Plays==0,"Relay participation leak");
        }});
        Test("host excludes peers beyond hearing range and rejects sender impersonation",()=>{using(var f=new Setup()){
            f.Enable();f.Join();f.Runtime.View.People["3"].X=100;f.Queue(2,f.Audio(2,f.A));f.Queue(2,f.Audio(3,f.A,2));f.Voice.Tick();
            Check(!f.Sent.Any(p=>p.Kind==3)&&AudioSource.Plays==1,"Distance or sender identity bypass");
        }});
        Test("voice rejects strangers, stale consent and speech after opt-out",()=>{using(var f=new Setup()){
            f.Enable();f.Join();f.Queue(99,f.Audio(99,f.A));f.Queue(2,f.Audio(2,Guid.NewGuid()));f.Queue(2,new VoicePacket{Kind=4,Sender=2,Consent=f.A});f.Queue(2,f.Audio(2,f.A));f.Voice.Tick();
            Check(!f.Sent.Any(p=>p.Kind==3)&&AudioSource.Plays==0,"Consent bypass");
        }});
        Test("voice drops old and duplicate frames and limits sender packet rate",()=>{using(var f=new Setup()){
            f.Enable();f.Join();f.Queue(2,f.Audio(2,f.A),300000);f.Queue(2,f.Audio(2,f.A));f.Queue(2,f.Audio(2,f.A));f.Voice.Tick();Check(f.Sent.Count(p=>p.Kind==3)==1,"Stale/duplicate frame forwarded");
            SteamNetworkingMessages.SentMessages.Clear();for(uint i=2;i<32;i++)f.Queue(2,f.Audio(2,f.A,i));f.Voice.Tick();Check(f.Sent.Count(p=>p.Kind==3)<=28,"Rate limit exceeded");
        }});
        Test("expired peer opt-in stops forwarding even if audio continues",()=>{using(var f=new Setup()){
            f.Enable();f.Join();Time.realtimeSinceStartupAsDouble+=4;f.Queue(2,f.Audio(2,f.A));f.Voice.Tick();Check(!f.Sent.Any(p=>p.Kind==3),"Expired peer forwarded");
        }});
        Test("client requires a fresh matching host grant before microphone capture",()=>{using(var f=new Setup(false)){
            f.Runtime.Entry.Settings.VoiceEnabled=true;f.Runtime.Entry.VoiceHeld=true;f.Voice.Tick();Check(Microphone.Starts==0,"Captured before host consent");
            Guid token=f.Sent.Single(p=>p.Kind==1).Consent;f.Queue(1,new VoicePacket{Kind=2,Sender=1,Consent=Guid.NewGuid(),Range=40});f.Voice.Tick();Check(Microphone.Starts==0,"Accepted wrong token");
            f.Queue(1,new VoicePacket{Kind=2,Sender=1,Consent=token,Range=40});f.Voice.Tick();Check(Microphone.Starts==1,"Grant did not activate capture");
            Microphone.Position=1280;f.Voice.Tick();Check(f.Sent.Count(p=>p.Kind==3)==2,"Captured audio did not reach host");
            f.Runtime.Entry.VoiceHeld=false;f.Voice.Tick();Check(Microphone.Ends==1&&!Microphone.Recording,"Push-to-talk release retained microphone");
        }});
        Test("host grant timeout stops microphone and rejects untrusted grants",()=>{using(var f=new Setup(false)){
            f.Runtime.Entry.Settings.VoiceEnabled=true;f.Voice.Tick();Guid token=f.Sent.Single().Consent;
            f.Queue(1,new VoicePacket{Kind=2,Sender=1,Consent=token,Range=40});f.Runtime.Entry.VoiceHeld=true;f.Voice.Tick();Check(Microphone.Recording,"Not recording");
            Time.realtimeSinceStartupAsDouble+=4;f.Voice.Tick();Check(!Microphone.Recording,"Capture survived lost grant");
        }});
        Test("host revocation immediately stops microphone and notifies participants",()=>{using(var f=new Setup()){
            f.Enable();f.Join();f.Runtime.Entry.VoiceHeld=true;f.Voice.Tick();Check(Microphone.Recording,"Not recording");f.Runtime.Entry.Settings.Host.VoiceAllowed=false;f.Voice.SettingsChanged();
            Check(!Microphone.Recording&&f.Sent.Count(p=>p.Kind==2&&p.Range==0)==2,"Host revocation failed");
        }});
        Test("mute and personal opt-out release playback buffers immediately",()=>{using(var f=new Setup()){
            f.Enable();f.Join();f.Queue(2,f.Audio(2,f.A));f.Voice.Tick();Check(AudioSource.Plays==1,"No playback");f.Runtime.Entry.Settings.VoiceMuted=new[]{"2"};f.Voice.SettingsChanged();Check(AudioSource.Stops==1,"Mute did not stop audio");
            f.Queue(2,f.Audio(2,f.A,2));f.Voice.Tick();Check(AudioSource.Plays==1,"Muted peer restarted playback");
            f.Runtime.Entry.Settings.VoiceEnabled=false;f.Voice.SettingsChanged();f.Queue(2,f.Audio(2,f.A,3));f.Voice.Tick();Check(AudioSource.Plays==1,"Personal opt-out played audio");
        }});
        Test("menus, focus loss, role testing and disconnect stop microphone",()=>{using(var f=new Setup()){
            f.Enable();f.Runtime.Entry.VoiceHeld=true;f.Voice.Tick();Check(Microphone.Recording,"Capture unavailable");Application.isFocused=false;f.Voice.Tick();Check(!Microphone.Recording,"Background capture");
            Application.isFocused=true;GameMenus.IsOpen=true;f.Voice.Tick();Check(!Microphone.Recording,"Menu capture");GameMenus.IsOpen=false;f.Runtime.Entry.RoleTestActive=true;f.Voice.Tick();Check(!Microphone.Recording,"Role-test capture");
            f.Runtime.Entry.RoleTestActive=false;f.Voice.Tick();f.Runtime.Client.Connected=false;f.Voice.Tick();Check(!Microphone.Recording,"Disconnected capture");
        }});
        Test("missing microphone, conflicting key and UI capture never start transmission",()=>{using(var f=new Setup()){
            f.Enable();f.Runtime.Entry.VoiceHeld=true;f.Runtime.Entry.VoiceBindingConflict=true;f.Voice.Tick();Check(Microphone.Starts==0,"Conflicting key captured audio");
            f.Runtime.Entry.VoiceBindingConflict=false;f.Runtime.UI.Captures=true;f.Voice.Tick();Check(Microphone.Starts==0,"Chat typing captured audio");
            f.Runtime.UI.Captures=false;f.Runtime.Entry.SettingsOpen=true;f.Voice.Tick();Check(Microphone.Starts==0,"Preferences captured audio");
            f.Runtime.Entry.SettingsOpen=false;f.Runtime.Entry.Settings.VoiceMicrophone="Removed microphone";f.Voice.SettingsChanged();f.Voice.Tick();Check(Microphone.Starts==0&&f.Voice.Indicator==MicrophoneIndicator.Unavailable,"Missing device silently substituted");
            f.Runtime.Entry.Settings.VoiceMicrophone="";f.Voice.SettingsChanged();f.Runtime.Entry.VoiceHeld=false;f.Voice.Tick();f.Runtime.Entry.VoiceHeld=true;f.Voice.Tick();Check(Microphone.Recording,"Microphone did not recover");
            f.Runtime.DiagnosticSuspended=true;f.Voice.Tick();Check(!Microphone.Recording,"Diagnostic pause retained microphone");
        }});
        Test("microphone indicator distinguishes disabled, unbound, idle and live PTT",()=>{using(var f=new Setup()){
            f.Voice.Tick();Check(f.Voice.Indicator==MicrophoneIndicator.Idle,"Opted-out microphone did not show idle");
            f.Runtime.Entry.Settings.VoiceBinding="";f.Enable();
            Check(f.Voice.Indicator==MicrophoneIndicator.Unavailable&&Microphone.Starts==0,"Unbound PTT appeared usable");
            f.Runtime.Entry.Settings.VoiceBinding="<Keyboard>/v";f.Voice.Tick();
            Check(f.Voice.Indicator==MicrophoneIndicator.Idle,"Ready PTT appeared live");
            f.Runtime.Entry.VoiceHeld=true;f.Voice.Tick();
            Check(f.Voice.Indicator==MicrophoneIndicator.Live&&Microphone.Recording,"Recording did not appear live");
            f.Runtime.Entry.VoiceHeld=false;f.Voice.Tick();
            Check(f.Voice.Indicator==MicrophoneIndicator.Idle&&!Microphone.Recording,"Released PTT kept live indicator");
        }});
        Test("open microphone requires both opt-ins and pauses outside normal gameplay",()=>{using(var f=new Setup()){
            f.Runtime.Entry.Settings.VoiceOpenMic=true;
            f.Runtime.Entry.Settings.VoiceBinding="";
            f.Runtime.Entry.Settings.VoiceEnabled=true;f.Voice.Tick();
            Check(f.Voice.Indicator==MicrophoneIndicator.Unavailable&&Microphone.Starts==0,"Open mic bypassed host permission");
            f.Runtime.Entry.SettingsOpen=true;f.Enable();
            Check(Microphone.Starts==0,"Open mic started behind settings");
            f.Runtime.Entry.SettingsOpen=false;f.Voice.Tick();
            Check(f.Voice.Indicator==MicrophoneIndicator.Live&&Microphone.Recording,"Open mic depended on a key");
            f.Runtime.UI.Captures=true;f.Voice.Tick();
            Check(f.Voice.Indicator==MicrophoneIndicator.Idle&&!Microphone.Recording,"Open mic stayed on while typing");
            f.Runtime.UI.Captures=false;Application.isFocused=false;f.Voice.Tick();
            Check(!Microphone.Recording,"Open mic resumed in background");
            Application.isFocused=true;f.Voice.Tick();Check(Microphone.Recording,"Open mic did not resume after focus");
            f.Runtime.Entry.Settings.VoiceEnabled=false;f.Voice.Tick();
            Check(!Microphone.Recording&&f.Voice.Indicator==MicrophoneIndicator.Idle,"Open mic survived local opt-out");
        }});
        Test("audio callback failure still releases every polled native message",()=>{
            SteamNetworkingMessages.Reset();var packet=new VoicePacket{Kind=1,Consent=Guid.NewGuid(),Sender=2};
            SteamNetworkingMessages.Queue(2,VoiceWire.Write(packet),189732);SteamNetworkingMessages.Queue(2,VoiceWire.Write(packet),189732);
            using(var t=new VoiceTransport(id=>true,(id,bytes)=>{throw new InvalidOperationException("test callback");}))
            {bool threw=false;try{t.Poll();}catch(InvalidOperationException){threw=true;}Check(threw&&SteamNetworkingMessages.Messages.Count==0&&SteamNetworkingMessages.Released==2,"Native message leak on callback error");}
        });
        Console.WriteLine("RESULT "+passed+" voice audio/network/runtime tests passed");
    }
}
