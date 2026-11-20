// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
namespace EnusDE.Communications
{
    internal sealed class VoiceRuntime:IDisposable
    {
        sealed class Peer
        {
            internal Guid Consent;internal double Seen,Window,Hello;internal int Frames;
            internal ulong Stream;internal uint Sequence;internal bool Sequenced;
        }
        sealed class Speaker
        {
            internal GameObject Root;
            internal AudioSource Source;
            internal VoicePlayback Playback;
            internal ulong Stream;internal uint Sequence;internal bool Sequenced;internal double Seen;
        }
        readonly VoiceSession session=new VoiceSession();
        readonly CommsRuntime runtime;Entry Entry=>runtime.Entry;ModSettings Settings=>Entry.Settings;
        readonly Dictionary<ulong,Peer> peers=new Dictionary<ulong,Peer>();
        readonly Dictionary<ulong,Speaker> speakers=new Dictionary<ulong,Speaker>();
        VoiceTransport transport;Guid consent=Guid.NewGuid();ulong stream=BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(),0);
        ulong hostId,local;
        bool wasHost,micFailed,microphoneAvailable,captureRequested;
        double nextHello,grantUntil,retryAt,micStarted,nextDeviceCheck;
        float range;
        AudioClip microphone;string device;int read;readonly float[] mono=new float[VoiceAudio.Samples];float[] captured;
        uint sequence;string status="voice_off";
        internal string Status=>Entry.Text.Get(status);
        internal MicrophoneIndicator Indicator=>status switch
        {
            "voice_transmitting"=>MicrophoneIndicator.Live,
            "voice_off" or "voice_relay" or "voice_ready"
                or "voice_paused" or "voice_starting_microphone"=>MicrophoneIndicator.Idle,
            _=>MicrophoneIndicator.Unavailable
        };
        internal VoiceRuntime(CommsRuntime owner){runtime=owner;session.Stopped+=()=>{StopCapture();ClearSpeakers();};}
        internal void SettingsChanged()
        {
            StopCapture();ClearSpeakers();micFailed=false;captureRequested=false;retryAt=0;nextDeviceCheck=0;
            if(!Settings.VoiceEnabled&&(!GameAdapter.IsHost||!Settings.Host.VoiceAllowed))Shutdown();
            else if(!Settings.VoiceEnabled) // Host may relay without taking part personally.
            {consent=Guid.NewGuid();}
            if(GameAdapter.IsHost&&!Settings.Host.VoiceAllowed)Shutdown();
            nextHello=0;
        }
        bool Allowed(ulong id)
        {
            if(!GameAdapter.Ready||!GameAdapter.MultiplayerActive||id==0)return false;
            if(!GameAdapter.IsHost)return id==GameAdapter.HostSteamId();
            return runtime.View.People.ContainsKey(id.ToString(CultureInfo.InvariantCulture))&&runtime.VoiceCompatible(id);
        }
        Person Person(ulong id){Person p;return runtime.View.People.TryGetValue(id.ToString(CultureInfo.InvariantCulture),out p)?p:null;}
        bool Muted(ulong id)=>Settings.VoiceMuted.Contains(id.ToString(CultureInfo.InvariantCulture));
        double Now=>Time.realtimeSinceStartupAsDouble;
        bool Participating=>session.Apply(GameAdapter.Ready&&GameAdapter.MultiplayerActive&&runtime.Client.Connected,GameAdapter.IsHost?Settings.Host.VoiceAllowed:grantUntil>Now,Settings.VoiceEnabled,true,VoiceSession.ProtocolVersion);
        internal void Tick()
        {
            if(Now<retryAt)return;
            try{Update();}
            catch(Exception error){Shutdown();status="voice_error";retryAt=Now+5;Entry.Log.Warning("Optional voice stopped: "+error.Message);}
        }
        void Update()
        {
            if(!GameAdapter.Ready||!GameAdapter.MultiplayerActive||runtime.DiagnosticSuspended||Entry.RoleTestActive||!runtime.Client.Connected)
            {Shutdown();status="voice_unavailable";return;}
            bool isHost=GameAdapter.IsHost;
            bool enabled=isHost?Settings.Host.VoiceAllowed:Settings.VoiceEnabled;
            if(!enabled){Shutdown();status=isHost&&Settings.VoiceEnabled?"voice_host_disabled":"voice_off";return;}
            ulong host=GameAdapter.HostSteamId(),id;
            if(!UInt64.TryParse(runtime.LocalId,out id)||host==0){Shutdown();status="voice_unavailable";return;}
            if(hostId!=host||local!=id||wasHost!=isHost){Shutdown();hostId=host;local=id;wasHost=isHost;}
            if(transport==null)transport=new VoiceTransport(Allowed,Receive);
            if(isHost)
            {
                range=Settings.VoiceRange;
                foreach(var peer in peers.Keys.Where(p=>!Allowed(p)||Now-peers[p].Seen>3).ToArray())peers.Remove(peer);
            }
            else if(Now>=nextHello)
            {transport.Send(hostId,new VoicePacket{Kind=1,Consent=consent,Sender=local});nextHello=Now+1;}
            transport.Poll();
            bool participating=Participating;
            if(!participating)
            {
                StopCapture();ClearSpeakers();captureRequested=false;
                status=isHost?"voice_relay":"voice_waiting";
                return;
            }
            UpdateSpeakers();
            UpdateCapture();
        }

        void UpdateCapture()
        {
            if(Now>=nextDeviceCheck)
            {
                var devices=Microphone.devices;
                bool available=devices.Length>0&&(String.IsNullOrEmpty(Settings.VoiceMicrophone)||devices.Contains(Settings.VoiceMicrophone));
                if(available&&!microphoneAvailable)micFailed=false;
                microphoneAvailable=available;
                nextDeviceCheck=Now+1;
            }
            bool openMic=Settings.VoiceOpenMic;
            bool bindingReady=!String.IsNullOrEmpty(Settings.VoiceBinding)&&!Entry.VoiceBindingConflict;
            bool canRecord=Application.isFocused&&!Entry.SettingsOpen&&!GameMenus.IsOpen&&runtime.UI?.MicrophoneInputBlocked!=true;
            bool requested=microphoneAvailable&&canRecord&&(openMic||bindingReady&&Entry.VoiceHeld);
            if(!requested)
            {
                captureRequested=false;
                StopCapture();
                status=!microphoneAvailable||micFailed?"voice_microphone_error":
                    !openMic&&!bindingReady?"voice_binding_required":
                    !canRecord?"voice_paused":"voice_ready";
                return;
            }
            if(!captureRequested)micFailed=false;
            captureRequested=true;
            if(micFailed){status="voice_microphone_error";return;}
            Capture();
        }
        void Receive(ulong sender,byte[] bytes)
        {
            var packet=VoiceWire.Read(bytes);if(packet==null)return;
            if(GameAdapter.IsHost)
            {
                if(!Settings.Host.VoiceAllowed||packet.Sender!=sender)return;
                Peer peer;
                if(packet.Kind==1)
                {
                    if(peers.TryGetValue(sender,out peer)&&Now-peer.Hello<.5)return;
                    if(peer==null||peer.Consent!=packet.Consent)peers[sender]=peer=new Peer{Consent=packet.Consent};
                    peer.Seen=peer.Hello=Now;
                    transport.Send(sender,new VoicePacket{Kind=2,Consent=packet.Consent,Sender=local,Range=Settings.VoiceRange});return;
                }
                if(!peers.TryGetValue(sender,out peer)||peer.Consent!=packet.Consent)return;
                if(packet.Kind==4){peers.Remove(sender);RemoveSpeaker(sender);return;}
                if(packet.Kind!=3||Now-peer.Seen>3)return;
                if(Now-peer.Window>=1){peer.Window=Now;peer.Frames=0;}if(++peer.Frames>30)return;
                if(peer.Sequenced&&peer.Stream==packet.Stream&&!VoiceWire.Newer(packet.Sequence,peer.Sequence))return;
                peer.Sequence=packet.Sequence;peer.Stream=packet.Stream;peer.Sequenced=true;
                Relay(sender,packet);
            }
            else
            {
                if(sender!=hostId||packet.Consent!=consent||!Settings.VoiceEnabled)return;
                if(packet.Kind==2)
                {
                    if(packet.Sender!=hostId)return;
                    range=packet.Range;grantUntil=range>0?Now+3:0;
                    if(range==0){StopCapture();ClearSpeakers();status="voice_host_disabled";}return;
                }
                if(packet.Kind==3&&Participating)Play(packet);
            }
        }
        void Relay(ulong sender,VoicePacket packet)
        {
            Person source=Person(sender);if(source==null)return;
            if(sender!=local&&Settings.VoiceEnabled&&VoiceWire.Gain(source,Person(local),Settings.VoiceRange)>0)Play(packet);
            foreach(var pair in peers)
            {
                if(pair.Key==sender||Now-pair.Value.Seen>3||!Allowed(pair.Key)||VoiceWire.Gain(source,Person(pair.Key),Settings.VoiceRange)<=0)continue;
                packet.Consent=pair.Value.Consent;packet.Range=Settings.VoiceRange;transport.Send(pair.Key,packet);
            }
        }
        void Capture()
        {
            if(microphone==null)
            {
                device=String.IsNullOrEmpty(Settings.VoiceMicrophone)?null:Settings.VoiceMicrophone;
                microphone=Microphone.Start(device,true,2,VoiceAudio.Rate);read=0;micStarted=Now;
                if(microphone==null||microphone.frequency!=VoiceAudio.Rate){StopCapture();micFailed=true;status="voice_microphone_error";return;}
                captured=new float[VoiceAudio.Samples*microphone.channels];
            }
            int position=Microphone.GetPosition(device);
            bool recording=Microphone.IsRecording(device);
            if(position<0||Now-micStarted>2&&!recording){StopCapture();micFailed=true;status="voice_microphone_error";return;}
            if(!recording){status="voice_starting_microphone";return;}
            status="voice_transmitting";
            int available=(position-read+microphone.samples)%microphone.samples;
            if(available>VoiceAudio.Samples*5){read=position;return;} // Drop stale microphone audio after a stall.
            for(int sent=0;available>=VoiceAudio.Samples&&sent<2;sent++)
            {
                if(!microphone.GetData(captured,read))break;
                for(int i=0;i<mono.Length;i++){float sum=0;for(int ch=0;ch<microphone.channels;ch++)sum+=captured[i*microphone.channels+ch];mono[i]=sum/microphone.channels;}
                read=(read+VoiceAudio.Samples)%microphone.samples;available-=VoiceAudio.Samples;
                var packet=new VoicePacket{Kind=3,Consent=consent,Sender=local,Sequence=++sequence,Stream=stream,Range=range,Audio=VoiceAudio.Encode(mono,Settings.VoiceGain)};
                if(GameAdapter.IsHost)Relay(local,packet);else transport.Send(hostId,packet);
            }
        }
        void Play(VoicePacket packet)
        {
            if(!Participating||packet.Sender==local||Muted(packet.Sender)||VoiceWire.Gain(Person(packet.Sender),Person(local),range)<=0)return;
            Speaker speaker;
            if(!speakers.TryGetValue(packet.Sender,out speaker))
            {
                if(speakers.Count>=32)return;
                speaker=new Speaker();speaker.Root=new GameObject("EnusDE proximity voice");speaker.Root.transform.SetParent(runtime.transform,false);
                speaker.Source=speaker.Root.AddComponent<AudioSource>();speaker.Source.spatialBlend=0;speaker.Source.loop=true;speaker.Source.playOnAwake=false;
                speaker.Playback=speaker.Root.AddComponent<VoicePlayback>();
                speaker.Playback.Gain=VoiceWire.Gain(Person(packet.Sender),Person(local),range)*Settings.VoiceVolume;
                speaker.Source.Play();
                speakers[packet.Sender]=speaker;
            }
            if(speaker.Sequenced&&speaker.Stream==packet.Stream&&!VoiceWire.Newer(packet.Sequence,speaker.Sequence))return;
            uint sequenceGap=speaker.Sequenced&&speaker.Stream==packet.Stream?unchecked(packet.Sequence-speaker.Sequence):0;
            if(sequenceGap==0||sequenceGap>5)speaker.Playback.Buffer.Clear();
            speaker.Stream=packet.Stream;speaker.Sequence=packet.Sequence;speaker.Sequenced=true;speaker.Seen=Now;
            speaker.Playback.Buffer.Push(VoiceAudio.Decode(packet.Audio),missingFrame:sequenceGap==2);
        }
        void UpdateSpeakers()
        {
            foreach(ulong id in speakers.Keys.ToArray())
            {
                float gain=VoiceWire.Gain(Person(id),Person(local),range);
                if(Muted(id)||gain<=0||Now-speakers[id].Seen>1){RemoveSpeaker(id);continue;}
                speakers[id].Playback.Gain=gain*Settings.VoiceVolume;
            }
        }
        void RemoveSpeaker(ulong id)
        {
            Speaker speaker;if(!speakers.TryGetValue(id,out speaker))return;
            speaker.Playback.Gain=0;
            speaker.Playback.Buffer.Clear();
            speaker.Source.Stop();
            UnityEngine.Object.Destroy(speaker.Root);speakers.Remove(id);
        }
        void ClearSpeakers(){foreach(ulong id in speakers.Keys.ToArray())RemoveSpeaker(id);}
        void StopCapture()
        {
            if(microphone==null)return;
            var clip=microphone;microphone=null;captured=null;read=0;
            try{Microphone.End(device);}finally{UnityEngine.Object.Destroy(clip);}
        }
        void Shutdown()
        {
            captureRequested=false;
            micFailed=false;
            nextDeviceCheck=0;
            if(transport==null&&microphone==null&&speakers.Count==0&&peers.Count==0&&!session.Active)return;
            StopCapture();ClearSpeakers();
            if(transport!=null)
            {
                try
                {
                    if(wasHost)foreach(var pair in peers)transport.Send(pair.Key,new VoicePacket{Kind=2,Consent=pair.Value.Consent,Sender=local,Range=0});
                    else if(local!=0&&hostId!=0)transport.Send(hostId,new VoicePacket{Kind=4,Consent=consent,Sender=local});
                }
                finally{transport.Dispose();transport=null;}
            }
            session.Reset();peers.Clear();grantUntil=0;nextHello=0;consent=Guid.NewGuid();stream=BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(),0);sequence=0;
        }
        public void Dispose(){Shutdown();status="voice_unavailable";}
    }
}
