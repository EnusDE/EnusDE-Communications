// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;
namespace EnusDE.Communications
{
    [DefaultExecutionOrder(-20000)]
    public sealed class CommsRuntime:MonoBehaviour
    {
        internal Entry Entry;
        internal ClientSession Client;
        internal WorldView View=new WorldView();
        internal CommsUI UI;
        internal HostArchive Archive;
        internal RoleTestSession RoleTest;
        internal bool IsRoleTest=>RoleTest!=null;
        HostRouter host;
        SteamTransport transport;
        internal VoiceRuntime Voice;
        internal bool VoiceCompatible(ulong id)=>host?.HasCompatibleClient(id.ToString(CultureInfo.InvariantCulture))==true;
        float nextRefresh,nextHello,nextPing;
        double lastResponse;
        readonly Dictionary<string,double> pending=new Dictionary<string,double>();
        readonly HashSet<string> routeChanges=new HashSet<string>();
        readonly ConsoleNoticeQueue consoleNotices=new ConsoleNoticeQueue();
        bool reportedTransport,reportedConsoleArchive,hostUnavailableReported;
        bool transportAttempted;
        double startedAt;
        AudioClip whistle;
        double diagnosticPauseUntil;
        internal bool DiagnosticSuspended=>Time.realtimeSinceStartupAsDouble<diagnosticPauseUntil;
        static readonly JsonSerializerSettings JsonSettings=new JsonSerializerSettings{TypeNameHandling=TypeNameHandling.None,MaxDepth=12,DateTimeZoneHandling=DateTimeZoneHandling.Utc};
        internal bool RoutePending=>routeChanges.Count>0;
        internal bool IsPending(string request)=>request!=null&&pending.ContainsKey(request);
        internal string LocalId=>IsRoleTest?RoleTest.ActiveId:GameAdapter.LocalId;
        internal bool Active=>IsRoleTest?Entry.Runtime?.Active==true:GameAdapter.Ready;
        internal bool HostUnavailable=>Active&&!GameAdapter.IsHost&&!Client.Connected&&Time.realtimeSinceStartupAsDouble-startedAt>12;
        internal void Begin(Entry entry)
        {
            Entry=entry;Client=new ClientSession();WireClient();startedAt=lastResponse=Time.realtimeSinceStartupAsDouble;
            View=Snapshot();
            EnsureHost();
            UI=gameObject.AddComponent<CommsUI>();UI.Initialize(this);CreateWhistle();Voice=new VoiceRuntime(this);
        }
        internal void BeginRoleTest(Entry entry,WorldView source,string train)
        {
            Entry=entry;RoleTest=new RoleTestSession(source,train);View=RoleTest.View;Client=RoleTest.Client;WireClient();
            UI=gameObject.AddComponent<CommsUI>();UI.Initialize(this);
            RoleTest.MessageClock=()=>UI.FadeClock;
            RoleTest.Packet=(id,packet)=>{if(id==RoleTest.ActiveId)UI.OnPacket(packet);};
        }
        internal void SelectTestRole(TestChatRole role)
        {if(!IsRoleTest)return;RoleTest.Select(role);Client=RoleTest.Client;pending.Clear();routeChanges.Clear();WireClient();UI.ResetSession();UI.Dirty();}
        internal void ObserveNativeConsole(string playerId,string text)
        {
            if(DiagnosticSuspended||!Active||!GameAdapter.IsHost||!GameAdapter.MultiplayerActive||String.IsNullOrWhiteSpace(text))return;
            EnsureHost();View=Snapshot();host.Refresh(View);Person player;if(!View.People.TryGetValue(playerId,out player))return;
            try
            {
                AppendArchive(new ChatRecord{Id=Guid.NewGuid().ToString("N"),Utc=DateTime.UtcNow,SenderId=player.Id,SenderName=player.Name,Channel="console",Target="native-console",TargetLabel=Entry.Text.Get("native_console"),Text=text.Length>512?text.Substring(0,512):text,RailroadId=View.Id,X=player.X,Y=player.Y,Z=player.Z});
            }
            catch(Exception error){if(!reportedConsoleArchive){reportedConsoleArchive=true;Entry.Log.Warning("Native console message could not be archived: "+error.Message);}}
            if(playerId!=LocalId)consoleNotices.Enqueue(playerId,Time.realtimeSinceStartupAsDouble);
        }
        void SendConsoleNotice(string playerId)
        {
            Person person;if(!GameAdapter.IsHost||!GameAdapter.MultiplayerActive||!View.People.TryGetValue(playerId,out person)||host.HasCompatibleClient(playerId))return;
            string name=(person.Name??playerId).Replace('\r',' ').Replace('\n',' ');if(name.Length>80)name=name.Substring(0,80);
            string message=String.Format(Entry.Text.Get("console_mod_notice"),name,"EnusDE Communications");
            var alert=new Network.Messages.Alert(Network.Messages.AlertStyle.Console,Network.Messages.AlertLevel.Info,global::Console.ConsoleEscape(message),Game.TimeWeather.Now.TotalSeconds);
            try{Game.HostManager.Shared.SendTo(new Game.PlayerId(playerId),alert);}catch(Exception error){Entry.Log.Warning("Optional console notice could not be delivered: "+error.Message);}
        }
        void EnsureHost()
        {
            if(host!=null||!GameAdapter.IsHost)return;
            View=Snapshot();
            Archive=Entry.Archive;host=new HostRouter(Entry.Settings.Host,Deliver,AppendArchive,history:Archive.Read());host.Refresh(View);
        }
        void WireClient()
        {
            Client.Changed=()=>UI?.Dirty();Client.PlayWhistle=PlayWhistle;
            Client.Acknowledged=id=>{if(id!=null){pending.Remove(id);routeChanges.Remove(id);}};
            Client.Rejected=id=>{if(id!=null){pending.Remove(id);routeChanges.Remove(id);}};
        }
        bool Allowed(ulong id)
        {
            if(!Active)return false;
            if(GameAdapter.IsHost)return View.People.ContainsKey(id.ToString(CultureInfo.InvariantCulture));
            return id!=0&&id==GameAdapter.HostSteamId();
        }
        void Receive(ulong sender,string text)
        {
            if(!Active)return;
            using var measurement=Entry.Performance.Measure(PerformanceStage.Receive);
            try
            {
                if(GameAdapter.IsHost)
                {
                    EnsureHost();View=Snapshot();host.Refresh(View);string id=sender.ToString(CultureInfo.InvariantCulture);
                    if(View.People.ContainsKey(id))
                    {
                        var request=JsonConvert.DeserializeObject<ClientRequest>(text,JsonSettings);bool connected=host.HasCompatibleClient(id);host.Handle(id,request);
                        if(!connected&&host.HasCompatibleClient(id))Entry.Log.Info("Remote chat handshake accepted (protocol "+WireProtocol.Version+").");
                        else if(request?.Kind=="hello"&&request.Protocol!=WireProtocol.Version)Entry.Log.Warning("Incompatible remote chat protocol: "+request.Protocol+"; local protocol: "+WireProtocol.Version+".");
                    }
                }
                else if(sender==GameAdapter.HostSteamId())ApplyPacket(JsonConvert.DeserializeObject<ServerPacket>(text,JsonSettings));
            }
            catch(JsonException){}
        }
        void Deliver(string id,ServerPacket packet)
        {
            if(id==LocalId){ApplyPacket(packet);return;}
            ulong peer;if(UInt64.TryParse(id,out peer))transport?.Send(peer,JsonConvert.SerializeObject(packet,JsonSettings));
        }
        void ApplyPacket(ServerPacket packet)
        {
            if(packet==null||packet.Nonce!=Client.Nonce||packet.Protocol!=WireProtocol.Version)return;
            if(Client.State!=null&&(packet.Token!=Client.State.Token||packet.Version<Client.State.Version))return;
            bool connected=Client.Connected;lastResponse=Time.realtimeSinceStartupAsDouble;Client.Accept(packet,UI?.FadeClock??0);
            if(!connected&&Client.Connected&&!GameAdapter.IsHost){hostUnavailableReported=false;Entry.Log.Info("Chat connection to host established (protocol "+WireProtocol.Version+").");}
            if(Client.State!=null&&packet.Token==Client.State.Token&&packet.Version==Client.State.Version)UI?.OnPacket(packet);
        }
        internal void Send(ClientRequest request)
        {
            if(!Active||DiagnosticSuspended)return;
            if(IsRoleTest){if(request.Kind!="ping"&&request.Kind!="hello")pending[request.RequestId]=Time.realtimeSinceStartupAsDouble;RoleTest.Send(request);return;}
            if(request.Kind!="hello"&&request.Kind!="ping"&&!Client.Connected){UI?.Toast(Entry.Text.Get(HostUnavailable?"host_unavailable":"connecting"));return;}
            if(request.Kind!="hello"&&request.Kind!="ping")pending[request.RequestId]=Time.realtimeSinceStartupAsDouble;
            if(request.Kind=="join"||request.Kind=="leave"||request.Kind=="crew"){routeChanges.Add(request.RequestId);UI?.CancelWheel();}
            if(GameAdapter.IsHost){EnsureHost();View=Snapshot();host.Refresh(View);host.Handle(LocalId,request);}
            else if(transport==null||!transport.Send(GameAdapter.HostSteamId(),JsonConvert.SerializeObject(request,JsonSettings),request.Kind=="hello"))
            {pending.Remove(request.RequestId);routeChanges.Remove(request.RequestId);if(request.Kind!="hello"&&request.Kind!="ping")UI?.Toast(Entry.Text.Get("send_failed"));}
        }
        internal void Say(string channel,string target,string text,string signal=null,string replyTo=null)
        {
            if(!Client.Connected){UI.Toast(Entry.Text.Get(HostUnavailable?"host_unavailable":"connecting"));return;}
            Send(Client.Request("send",channel,target,text,signal,replyTo));
        }
        void Update()
        {
            if(!Active){Voice?.Dispose();return;}
            if(IsRoleTest){Entry.KeepBindingsEnabled(this);if(Time.realtimeSinceStartup>=nextPing){RoleTest.Tick();nextPing=Time.realtimeSinceStartup+4;}return;}
            if(Entry.Performance.NextFrame(Time.frameCount,GameAdapter.IsHost,Client.Connected,DiagnosticSuspended))Entry.ExportPerformance();
            if(DiagnosticSuspended){Voice?.Dispose();return;}
            using var measurement=Entry.Performance.Measure(PerformanceStage.Update);
            if(diagnosticPauseUntil>0){diagnosticPauseUntil=0;UI.Toast(Entry.Text.Get("performance_resumed"));Entry.Log.Info("Diagnostic chat pause ended; normal processing resumed.");}
            try
            {
                Entry.KeepBindingsEnabled(this);
                float now=Time.realtimeSinceStartup;
                EnsureHost();
                bool multiplayer=GameAdapter.MultiplayerActive;
                if(!multiplayer&&transportAttempted){transport?.Dispose();transport=null;transportAttempted=false;}
                if(multiplayer&&!transportAttempted)
                {transportAttempted=true;try{transport=new SteamTransport(Allowed,Receive,message=>Entry.Log.Warning(message),Entry.Performance);}catch(Exception error){Entry.Log.Warning("Steam communications unavailable: "+error.Message);}}
                if(!multiplayer&&Client.Connected&&!UI.ChatShown&&!Entry.SettingsOpen)
                {lastResponse=Time.realtimeSinceStartupAsDouble;if(now>=nextRefresh){RefreshLanguage();nextRefresh=now+2;}return;}
                if(now>=nextRefresh){View=Snapshot();if(host!=null){host.Policy=Entry.Settings.Host;host.Refresh(View);}RefreshLanguage();nextRefresh=now+(multiplayer ? .5f : 1f);}
                if(multiplayer)transport?.Poll();
                Voice?.Tick();
                if(!Client.Connected&&now>=nextHello){Send(Client.Request("hello"));nextHello=now+(HostUnavailable?10:2);}
                if(Client.Connected&&now>=nextPing){Send(Client.Request("ping"));nextPing=now+4;}
                if(Client.Connected&&Time.realtimeSinceStartupAsDouble-lastResponse>18)
                {Client=new ClientSession();pending.Clear();routeChanges.Clear();WireClient();UI.ResetSession();nextHello=0;lastResponse=Time.realtimeSinceStartupAsDouble;}
                if(HostUnavailable&&!hostUnavailableReported)
                {hostUnavailableReported=true;UI.Dirty();Entry.Log.Warning("No host chat handshake after 12 seconds. Native multiplayer is "+GameAdapter.MultiplayerActive+"; host identity resolved: "+(GameAdapter.HostSteamId()!=0)+"; transport initialized: "+(transport!=null)+"; Steam chat state: "+transport?.Describe(GameAdapter.HostSteamId())+". Chat hello retries continue; user messages are not resent.");global::Console.Log(global::Console.ConsoleEscape(Entry.Text.Get("host_unavailable")));}
                if(GameAdapter.IsHost&&GameAdapter.MultiplayerActive)consoleNotices.Flush(Time.realtimeSinceStartupAsDouble,id=>View.People.ContainsKey(id),id=>host.HasCompatibleClient(id),SendConsoleNotice);else consoleNotices.Clear();
                foreach(var id in pending.Where(pair=>Time.realtimeSinceStartupAsDouble-pair.Value>10).Select(pair=>pair.Key).ToArray())
                {pending.Remove(id);routeChanges.Remove(id);UI.Toast(Entry.Text.Get("not_confirmed"));}
            }
            catch(Exception error){if(!reportedTransport){reportedTransport=true;Entry.Log.Warning("Communication update failed: "+error.Message);}UI?.Toast(Entry.Text.Get("connection_error"));}
        }
        WorldView Snapshot(){using(Entry.Performance.Measure(PerformanceStage.Snapshot))return GameAdapter.Snapshot();}
        void AppendArchive(ChatRecord record){using(Entry.Performance.Measure(PerformanceStage.Archive))Archive.Append(record);}
        void RefreshLanguage(){using(Entry.Performance.Measure(PerformanceStage.Language))if(Entry.Text.Refresh())UI.ApplySettings();}
        internal void PauseForDiagnosis()
        {
            if(!GameAdapter.MultiplayerActive)return;
            UI.PrepareDiagnosticPause();diagnosticPauseUntil=Time.realtimeSinceStartupAsDouble+10;
            Entry.Log.Info("Diagnostic chat pause: local chat UI, polling and updates suspended for 10 seconds; native multiplayer remains untouched.");
        }
        void CreateWhistle()
        {
            const int rate=44100;var data=new float[(int)(rate*.7f)];var random=new System.Random(7391);double phase=0;
            for(int i=0;i<data.Length;i++){double t=(double)i/rate;phase+=2*Math.PI*(2700+140*Math.Sin(2*Math.PI*32*t))/rate;double env=Math.Min(1,t/.03)*Math.Min(1,(.7-t)/.08);data[i]=(float)(env*.23*(Math.Sin(phase)+.12*Math.Sin(2*phase)+(random.NextDouble()-.5)*.08));}
            whistle=AudioClip.Create("EnusDE personal trill whistle",data.Length,1,rate,false);whistle.SetData(data,0);
        }
        void PlayWhistle(ChatRecord record)
        {
            if(whistle==null||Entry.Settings.WhistleVolume<=0)return;
            Person local;if(!View.People.TryGetValue(LocalId,out local))return;
            var position=new Vector3(record.X,record.Y,record.Z);if(Vector3.Distance(position,new Vector3(local.X,local.Y,local.Z))>80)return;
            var sound=new GameObject("EnusDE whistle");sound.transform.position=Helpers.WorldTransformer.GameToWorld(position);var source=sound.AddComponent<AudioSource>();source.clip=whistle;source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=4;source.maxDistance=80;source.volume=Entry.Settings.WhistleVolume;source.Play();Destroy(sound,1.2f);
        }
        void OnDestroy(){Voice?.Dispose();Voice=null;transport?.Dispose();transport=null;if(whistle!=null)Destroy(whistle);}
    }
}
