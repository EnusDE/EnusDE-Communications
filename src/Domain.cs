// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
namespace EnusDE.Communications
{
    public static class WireProtocol { public const int Version=4; }
    public sealed class Person
    {
        public string Id, Name, Crew, Rank, SelectedCar;
        public bool Host;
        public float X, Y, Z;
    }
    public sealed class WorkingTrain { public string Id, Label, Crew; }
    public sealed class WorldView
    {
        public string Id;
        public readonly Dictionary<string, Person> People = new Dictionary<string, Person>(StringComparer.Ordinal);
        public readonly Dictionary<string, WorkingTrain> Trains = new Dictionary<string, WorkingTrain>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Crews = new Dictionary<string, string>(StringComparer.Ordinal);
    }
    public sealed class ClientRequest
    {
        public int Protocol = WireProtocol.Version;
        public string Kind, Nonce, Token, RequestId, Channel, Target, Text, Signal, ReplyTo;
        public long Version;
        public MessageParts Parts;
        public bool ToDispatcher;
    }
    public sealed class ChatRecord
    {
        public string Id, SenderId, SenderName, Channel, Target, TargetLabel, Text, Signal, RailroadId, ReplyTo;
        public bool FromDispatcher,DispatchRequest;
        public MessageParts Parts;
        public DateTime Utc;
        public float X, Y, Z;
    }
    public sealed class ServerPacket
    {
        public int Protocol = WireProtocol.Version;
        public string Kind, Nonce, Token, Error, Ack, Crew, ActualCrew, Train, TrainLabel, Thread, ReplyTo, SelectedCar;
        public long Version;
        public bool ClearCrew, ClearTrain, Admin, Dispatcher, CanReply, OperatorConversation;
        public ChatRecord Message;
        public Dictionary<string,bool> PeerChat;
    }
    public sealed class HostPolicy
    {
        public string[] AdminRanks = new string[0];
        public string[] DispatcherRanks = { "Dispatcher" };
        public bool IsAdmin(Person p) { return p != null && (p.Host || (AdminRanks ?? new string[0]).Contains(p.Rank, StringComparer.Ordinal)); }
        public bool IsDispatcher(Person p) { return IsAdmin(p) || (p != null && (DispatcherRanks ?? new string[0]).Contains(p.Rank, StringComparer.Ordinal)); }
        public bool CanTrain(Person p, WorkingTrain t) { return p != null && t != null && (IsAdmin(p) || String.IsNullOrEmpty(t.Crew) || t.Crew == p.Crew); }
    }
    public static class SignalText
    {
        public static readonly Dictionary<string,string> English = new Dictionary<string,string> {
            {"stop","Stop!"},{"forward","Forward"},{"reverse","Reverse"},{"slow","Slow down"},
            {"one","One car length"},{"two","Two car lengths"},{"five","Five car lengths"},
            {"switch","Set the switch"},{"ready","Ready"},{"understood","Understood"},{"whistle","Whistle"},
            {"wait","Please wait"},{"report","Please report"}
        };
    }
    public sealed class HostRouter
    {
        sealed class Member
        {
            public string Nonce, Token, ActualCrew, Crew, CrewOverride, Train, Rank,SelectedCar;
            public bool Admin, Dispatcher;
            public long Version = 1,TrainEpoch,SelectionEpoch,DispatcherEpoch;
            public readonly HashSet<string> Requests = new HashSet<string>();
            public readonly Queue<string> Order = new Queue<string>();
            public DateTime RateStart;
            public DateTime LastSeen;
            public int RateCount;
        }
        readonly Dictionary<string,Member> members = new Dictionary<string,Member>(StringComparer.Ordinal);
        sealed class DispatchThread
        {
            public string Train;
            public readonly List<ChatRecord> History=new List<ChatRecord>();
            public readonly Dictionary<string,PendingDispatch> Pending=new Dictionary<string,PendingDispatch>();
        }
        sealed class PendingDispatch
        {
            public ChatRecord Message;
            public readonly Dictionary<string,string> Recipients=new Dictionary<string,string>();
            public readonly HashSet<string> Answered=new HashSet<string>();
        }
        readonly Dictionary<string,DispatchThread> threads = new Dictionary<string,DispatchThread>(StringComparer.Ordinal);
        IEnumerable<ChatRecord> saved;
        string peerSignature;
        readonly Action<string,ServerPacket> deliver;
        readonly Action<ChatRecord> archive;
        readonly Func<DateTime> utc;
        public HostPolicy Policy;
        public WorldView World { get; private set; } = new WorldView();
        public bool HasCompatibleClient(string id){Member member;return members.TryGetValue(id,out member)&&(utc()-member.LastSeen).TotalSeconds<30;}
        public HostRouter(HostPolicy policy, Action<string,ServerPacket> delivery, Action<ChatRecord> log, Func<DateTime> clock = null,IEnumerable<ChatRecord> history=null)
        { Policy=policy;deliver=delivery;archive=log;utc=clock??(()=>DateTime.UtcNow);saved=history; }
        public void Refresh(WorldView view)
        {
            World=view;
            if(saved!=null&&view.Id!=null)
            {
                foreach(var record in saved.Where(r=>r!=null&&!String.IsNullOrEmpty(r.Id)&&r.Channel=="dispatch"&&r.RailroadId==view.Id&&r.Utc>utc().AddHours(-6)).OrderBy(r=>r.Utc))
                {string train=TrainForThread(record.Target);if(train!=null&&view.Trains.ContainsKey(train))AddHistory(ThreadFor(train),record);}saved=null;
            }
            foreach(var id in members.Keys.ToArray())
            {
                if(!view.People.ContainsKey(id)){members.Remove(id);continue;}
                var m=members[id];var p=view.People[id];
                bool selectionChanged=m.SelectedCar!=p.SelectedCar;if(selectionChanged){m.SelectedCar=p.SelectedCar;m.SelectionEpoch++;}
                bool admin=Policy.IsAdmin(p),dispatcher=Policy.IsDispatcher(p);
                string crew=admin&&!String.IsNullOrEmpty(m.CrewOverride)&&view.Crews.ContainsKey(m.CrewOverride)?m.CrewOverride:p.Crew;
                bool cc=m.Crew!=crew,ct=!String.IsNullOrEmpty(m.Train)&&(!view.Trains.ContainsKey(m.Train)||!Policy.CanTrain(p,view.Trains[m.Train]));
                bool changed=cc||ct||m.ActualCrew!=p.Crew||m.Rank!=p.Rank||m.Admin!=admin||m.Dispatcher!=dispatcher;
                if(m.Dispatcher!=dispatcher)m.DispatcherEpoch++;
                m.Crew=crew;m.ActualCrew=p.Crew;m.Rank=p.Rank;m.Admin=admin;m.Dispatcher=dispatcher;
                if(!admin)m.CrewOverride=null;if(ct){m.Train=null;m.TrainEpoch++;}
                if(changed){m.Version++;State(id,m,cc,ct);}
                else if(selectionChanged)State(id,m);
            }
            foreach(var key in threads.Keys.ToArray())
            {
                var t=threads[key];if(!World.Trains.ContainsKey(t.Train)){threads.Remove(key);continue;}
                t.History.RemoveAll(r=>r.Utc<=utc().AddHours(-6));foreach(var pending in t.Pending.Where(p=>p.Value.Message.Utc<=utc().AddHours(-6)).Select(p=>p.Key).ToArray())t.Pending.Remove(pending);
            }
            string signature=String.Join(";",World.People.Keys.OrderBy(id=>id).Select(id=>id+":"+HasCompatibleClient(id)));
            if(signature!=peerSignature){peerSignature=signature;foreach(var member in members.ToArray())State(member.Key,member.Value);}
        }
        static string ThreadId(string train)=>"fdl:"+train;
        static string TrainForThread(string thread)=>thread!=null&&thread.StartsWith("fdl:",StringComparison.Ordinal)?thread.Substring(4):null;
        DispatchThread ThreadFor(string train){string id=ThreadId(train);DispatchThread t;if(!threads.TryGetValue(id,out t))threads[id]=t=new DispatchThread{Train=train};return t;}
        static void AddHistory(DispatchThread thread,ChatRecord record){thread.History.Add(record);if(thread.History.Count>500){thread.Pending.Remove(thread.History[0].Id);thread.History.RemoveAt(0);}}
        static string Ticket(Member member,string train)=>member.Token+(member.Dispatcher?":d:"+member.DispatcherEpoch:member.Train==train?":t:"+member.TrainEpoch:":s:"+member.SelectionEpoch);
        static bool ValidTicket(Member member,string train,string ticket)=>member.Train==train&&ticket==member.Token+":t:"+member.TrainEpoch||member.SelectedCar==train&&ticket==member.Token+":s:"+member.SelectionEpoch||member.Dispatcher&&ticket==member.Token+":d:"+member.DispatcherEpoch;
        bool ReceivesTrainContact(Person person,Member member,string train)=>Policy.CanTrain(person,World.Trains[train])&&(member.Train==train||member.SelectedCar==train);
        ServerPacket Packet(Member m,string kind){return new ServerPacket{Kind=kind,Nonce=m.Nonce,Token=m.Token,Version=m.Version};}
        void State(string id,Member m,bool clearCrew=false,bool clearTrain=false,string ack=null)
        {
            var r=Packet(m,"state");r.Crew=m.Crew;r.ActualCrew=m.ActualCrew;r.Train=m.Train;
            r.TrainLabel=!String.IsNullOrEmpty(m.Train)&&World.Trains.ContainsKey(m.Train)?World.Trains[m.Train].Label:null;
            r.Admin=m.Admin;r.Dispatcher=m.Dispatcher;r.ClearCrew=clearCrew;r.ClearTrain=clearTrain;r.Ack=ack;r.SelectedCar=m.SelectedCar;r.PeerChat=World.People.Keys.ToDictionary(peer=>peer,peer=>HasCompatibleClient(peer));deliver(id,r);
        }
        void Error(string id,Member m,string error,string ack=null,string replyTo=null){var r=Packet(m,"error");r.Error=error;r.Ack=ack;r.ReplyTo=replyTo;deliver(id,r);}
        static bool Short(string value,int maximum){return value!=null&&value.Length<=maximum;}
        public void Handle(string id,ClientRequest request)
        {
            if(request==null||request.Protocol!=WireProtocol.Version||!World.People.ContainsKey(id)||!Short(request.Nonce,64)||!Short(request.RequestId,64))return;
            Member m;
            if(request.Kind=="hello")
            {
                if(request.Nonce.Length<16)return;
                bool fresh=!members.TryGetValue(id,out m)||m.Nonce!=request.Nonce;
                if(fresh)
                {var p=World.People[id];m=new Member{Nonce=request.Nonce,Token=Guid.NewGuid().ToString("N"),ActualCrew=p.Crew,Crew=p.Crew,Rank=p.Rank,SelectedCar=p.SelectedCar,Admin=Policy.IsAdmin(p),Dispatcher=Policy.IsDispatcher(p)};members[id]=m;}
                m.LastSeen=utc();State(id,m,fresh,fresh,request.RequestId);return;
            }
            if(!members.TryGetValue(id,out m)||m.Nonce!=request.Nonce||m.Token!=request.Token)return;
            m.LastSeen=utc();
            if(request.Version!=m.Version){State(id,m);Error(id,m,"membership_changed",request.RequestId);return;}
            if(m.Requests.Contains(request.RequestId))return;
            m.Requests.Add(request.RequestId);m.Order.Enqueue(request.RequestId);if(m.Order.Count>256)m.Requests.Remove(m.Order.Dequeue());
            var now=utc();if((now-m.RateStart).TotalSeconds>=5){m.RateStart=now;m.RateCount=0;}
            if(++m.RateCount>30){Error(id,m,"rate_limit",request.RequestId);return;}
            var person=World.People[id];
            if(request.Kind=="ping"){State(id,m,false,false,request.RequestId);return;}
            if(request.Kind=="join")
            {
                WorkingTrain train;
                if(!Short(request.Target,160)||!World.Trains.TryGetValue(request.Target,out train)||!Policy.CanTrain(person,train)){Error(id,m,"train_denied",request.RequestId);return;}
                bool changed=m.Train!=train.Id;m.Train=train.Id;if(changed){m.Version++;m.TrainEpoch++;}State(id,m,false,changed,request.RequestId);return;
            }
            if(request.Kind=="leave"){m.Train=null;m.TrainEpoch++;m.Version++;State(id,m,false,true,request.RequestId);return;}
            if(request.Kind=="crew")
            {
                if(!String.IsNullOrEmpty(request.Target)&&(!m.Admin||!World.Crews.ContainsKey(request.Target))){Error(id,m,"crew_denied",request.RequestId);return;}
                string next=String.IsNullOrEmpty(request.Target)?person.Crew:request.Target;bool changed=next!=m.Crew;
                m.CrewOverride=request.Target;m.Crew=next;if(changed)m.Version++;State(id,m,changed,false,request.RequestId);return;
            }
            if(request.Kind=="contact"||request.Kind=="contact_operator")
            {
                bool operatorContact=request.Kind=="contact_operator";
                if(!Short(request.Target,160)||!World.Trains.ContainsKey(request.Target)||(operatorContact?(m.Train!=request.Target||!Policy.CanTrain(person,World.Trains[request.Target])):!m.Dispatcher)){Error(id,m,"dispatcher_denied",request.RequestId);return;}
                string thread=ThreadId(request.Target);var conversation=ThreadFor(request.Target);
                if(!operatorContact)
                {var begin=Packet(m,"history_begin");begin.Thread=thread;begin.TrainLabel=World.Trains[request.Target].Label;deliver(id,begin);
                foreach(var row in conversation.History){var historical=Packet(m,"history");historical.Message=row;deliver(id,historical);}}
                var packet=Packet(m,"contact");packet.Thread=thread;packet.TrainLabel=World.Trains[request.Target].Label;packet.Ack=request.RequestId;packet.OperatorConversation=operatorContact;deliver(id,packet);return;
            }
            if(request.Kind!="send")return;
            bool signal=!String.IsNullOrEmpty(request.Signal);
            bool template=request.Parts!=null,toDispatcher=request.ToDispatcher||template&&request.Parts.ToDispatcher;
            if(template&&signal||toDispatcher&&(request.Channel!="dispatch"||!String.IsNullOrEmpty(request.ReplyTo))){Error(id,m,"template_invalid",request.RequestId);return;}
            if(signal&&!SignalText.English.ContainsKey(request.Signal)){Error(id,m,"signal_unknown",request.RequestId);return;}
            if(!signal&&!template&&(String.IsNullOrWhiteSpace(request.Text)||!Short(request.Text,500))){Error(id,m,"text_invalid",request.RequestId);return;}
            string channel=request.Channel,target=null,label=null;DispatchThread dispatch=null;PendingDispatch replying=null;
            if(signal&&channel!="train"&&!(channel=="dispatch"&&(request.Signal=="wait"||request.Signal=="report"||request.Signal=="understood")))
            {Error(id,m,"signal_train_only",request.RequestId);return;}
            if(channel=="global"){target="global";label="Global";}
            else if(channel=="crew")
            {target=m.Crew;if(String.IsNullOrEmpty(target)){Error(id,m,"no_crew",request.RequestId);return;}label=World.Crews.ContainsKey(target)?World.Crews[target]:target;}
            else if(channel=="train")
            {
                target=m.Train;WorkingTrain t;
                if(String.IsNullOrEmpty(target)||!World.Trains.TryGetValue(target,out t)||!Policy.CanTrain(person,t)||request.Target!=target){Error(id,m,"train_denied",request.RequestId);return;}label=t.Label;
            }
            else if(channel=="dispatch")
            {
                if(toDispatcher)
                {
                    string train=TrainForThread(request.Target);WorkingTrain working;
                    if(String.IsNullOrEmpty(train)||m.Train!=train||!World.Trains.TryGetValue(train,out working)||!Policy.CanTrain(person,working)){Error(id,m,"train_denied",request.RequestId);return;}
                    dispatch=ThreadFor(train);
                }
                if(!Short(request.Target,260)||!threads.TryGetValue(request.Target,out dispatch)||!World.Trains.ContainsKey(dispatch.Train))
                {Error(id,m,"thread_closed",request.RequestId);return;}
                if(!m.Dispatcher&&!ReceivesTrainContact(person,m,dispatch.Train))
                {Error(id,m,"dispatcher_denied",request.RequestId);return;}
                bool wantsReply=!String.IsNullOrEmpty(request.ReplyTo);
                if(signal&&(request.Signal=="wait"||request.Signal=="report")&&!m.Dispatcher)
                {Error(id,m,"dispatcher_denied",request.RequestId);return;}
                if(!toDispatcher&&(!m.Dispatcher||wantsReply))
                {
                    string ticket;
                    if((!m.Dispatcher&&!ReceivesTrainContact(person,m,dispatch.Train))||!Short(request.ReplyTo,64)||!dispatch.Pending.TryGetValue(request.ReplyTo,out replying)||replying.Message.Utc<=now.AddHours(-6)||replying.Answered.Contains(id)||!replying.Recipients.TryGetValue(id,out ticket)||!ValidTicket(m,dispatch.Train,ticket))
                    {Error(id,m,"reply_unavailable",request.RequestId,request.ReplyTo);return;}
                }
                target=request.Target;label=World.Trains[dispatch.Train].Label;
            }
            else{Error(id,m,"channel_invalid",request.RequestId);return;}
            MessageParts parts=null;
            if(template)
            {
                var definition=MessageTemplates.Find(request.Parts.Id);WorkingTrain working=null;
                if(definition==null||!definition.Allows(channel)||definition.DispatcherOnly&&toDispatcher){Error(id,m,"template_invalid",request.RequestId);return;}
                if(definition.NeedsTrain)
                {
                    string train=channel=="dispatch"?dispatch.Train:channel=="crew"?request.Parts.TrainId:m.Train;
                    if(String.IsNullOrEmpty(train)||!World.Trains.TryGetValue(train,out working)||!(channel=="dispatch"&&m.Dispatcher&&!toDispatcher)&&!Policy.CanTrain(person,working)||channel=="crew"&&!String.IsNullOrEmpty(working.Crew)&&working.Crew!=m.Crew)
                    {Error(id,m,"train_denied",request.RequestId);return;}
                }
                if(!MessageTemplates.TryNormalize(request.Parts,working,m.Dispatcher,channel,out parts)){Error(id,m,"template_invalid",request.RequestId);return;}
            }
            var record=new ChatRecord{Id=Guid.NewGuid().ToString("N"),Utc=now,SenderId=id,SenderName=person.Name,Channel=channel,Target=target,TargetLabel=label,
                Text=template?MessageTemplates.Render(parts):signal?SignalText.English[request.Signal]:request.Text,Parts=parts,Signal=signal?request.Signal:null,X=person.X,Y=person.Y,Z=person.Z,RailroadId=World.Id,
                FromDispatcher=channel=="dispatch"&&m.Dispatcher&&!toDispatcher&&(replying==null||replying.Message.DispatchRequest),DispatchRequest=toDispatcher,ReplyTo=replying?.Message.Id};
            try{archive(record);}catch{Error(id,m,"archive_failed",request.RequestId);return;}
            PendingDispatch incoming=null;
            if(dispatch!=null)
            {
                AddHistory(dispatch,record);
                if(replying==null&&(record.FromDispatcher||record.DispatchRequest)){incoming=new PendingDispatch{Message=record};dispatch.Pending[record.Id]=incoming;}
                if(replying!=null)replying.Answered.Add(id);
            }
            foreach(var pair in members.ToArray())
            {
                var recipient=pair.Value;var rp=World.People[pair.Key];
                bool receive=channel=="global"||channel=="crew"&&recipient.Crew==target||channel=="train"&&recipient.Train==target&&Policy.CanTrain(rp,World.Trains[target]);
                if(channel=="dispatch") receive=recipient.Dispatcher||ReceivesTrainContact(rp,recipient,dispatch.Train);
                if(receive)
                {
                    var response=Packet(recipient,"message");response.Message=record;response.Ack=pair.Key==id?request.RequestId:null;
                    response.CanReply=incoming!=null&&pair.Key!=id&&(record.DispatchRequest?recipient.Dispatcher:recipient.Dispatcher||ReceivesTrainContact(rp,recipient,dispatch.Train));if(response.CanReply)incoming.Recipients[pair.Key]=Ticket(recipient,dispatch.Train);
                    deliver(pair.Key,response);
                }
                if(request.Signal=="whistle"&&DistanceSquared(person,rp)<=6400){var response=Packet(recipient,"sound");response.Message=record;deliver(pair.Key,response);}
            }
            if(replying!=null){var done=Packet(m,"reply_done");done.ReplyTo=replying.Message.Id;done.Ack=request.RequestId;deliver(id,done);}
        }
        static float DistanceSquared(Person a,Person b){float x=a.X-b.X,y=a.Y-b.Y,z=a.Z-b.Z;return x*x+y*y+z*z;}
    }
}
