// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
namespace EnusDE.Communications
{
    public sealed class VisibleRecord { public ChatRecord Message;public double Received;public bool DispatcherTicket,Historical; }
    public sealed class ClientSession
    {
        public readonly string Nonce=Guid.NewGuid().ToString("N");
        public ServerPacket State { get; private set; }
        public bool Connected => State!=null;
        public string Error {get;private set;}
        public readonly Dictionary<string,List<VisibleRecord>> History=new Dictionary<string,List<VisibleRecord>>();
        public readonly Dictionary<string,string> Conversations=new Dictionary<string,string>();
        public readonly Dictionary<string,VisibleRecord> PendingDispatch=new Dictionary<string,VisibleRecord>();
        readonly HashSet<string> seen=new HashSet<string>();readonly Queue<string> seenOrder=new Queue<string>();
        readonly HashSet<string> answeredDispatch=new HashSet<string>();readonly Queue<string> answeredOrder=new Queue<string>();
        public Action Changed;
        public Action<ChatRecord> PlayWhistle;
        public Action<string> Acknowledged;
        public Action<string> Rejected;
        public ClientRequest Request(string kind,string channel=null,string target=null,string text=null,string signal=null,string replyTo=null)
        {return new ClientRequest{Kind=kind,Nonce=Nonce,Token=State?.Token,Version=State?.Version??0,RequestId=Guid.NewGuid().ToString("N"),Channel=channel,Target=target,Text=text,Signal=signal,ReplyTo=replyTo};}
        public void Accept(ServerPacket packet,double realtime)
        {
            if(packet==null||packet.Protocol!=WireProtocol.Version||packet.Nonce!=Nonce)return;
            if(State!=null&&packet.Token==State.Token&&packet.Version<State.Version)return;
            if(packet.Kind=="state")
            {
                if(State!=null&&packet.Token!=State.Token)return;
                bool fresh=State==null||State.Token!=packet.Token;
                if(fresh){History.Clear();Conversations.Clear();PendingDispatch.Clear();seen.Clear();seenOrder.Clear();answeredDispatch.Clear();answeredOrder.Clear();}
                if(fresh||packet.ClearCrew||State?.Crew!=packet.Crew)Clear("crew:");
                if(fresh||packet.ClearTrain||State?.Train!=packet.Train){Clear("train:");foreach(var id in PendingDispatch.Where(p=>!p.Value.DispatcherTicket).Select(p=>p.Key).ToArray())PendingDispatch.Remove(id);if(!packet.Dispatcher){Clear("dispatch:");Conversations.Clear();}}
                if(State!=null&&State.Dispatcher&&!packet.Dispatcher){Clear("dispatch:");Conversations.Clear();}
                foreach(var id in PendingDispatch.Where(p=>p.Value.DispatcherTicket?!packet.Dispatcher:p.Value.Message.Target!="fdl:"+packet.Train&&p.Value.Message.Target!="fdl:"+packet.SelectedCar).Select(p=>p.Key).ToArray())PendingDispatch.Remove(id);
                State=packet;Error=null;Acknowledged?.Invoke(packet.Ack);Changed?.Invoke();return;
            }
            if(State==null||packet.Token!=State.Token||packet.Version!=State.Version)return;
            if(packet.Kind=="error"){Error=packet.Error;if(packet.Error=="reply_unavailable"&&packet.ReplyTo!=null)PendingDispatch.Remove(packet.ReplyTo);Rejected?.Invoke(packet.Ack);Changed?.Invoke();return;}
            if(packet.Kind=="contact"){Conversations[packet.Thread]=packet.TrainLabel;Acknowledged?.Invoke(packet.Ack);Changed?.Invoke();return;}
            if(packet.Kind=="history_begin"&&State.Dispatcher){History.Remove("dispatch:"+packet.Thread);Conversations[packet.Thread]=packet.TrainLabel;Changed?.Invoke();return;}
            if(packet.Kind=="reply_done"){PendingDispatch.Remove(packet.ReplyTo);if(packet.ReplyTo!=null&&answeredDispatch.Add(packet.ReplyTo)){answeredOrder.Enqueue(packet.ReplyTo);if(answeredOrder.Count>4096)answeredDispatch.Remove(answeredOrder.Dequeue());}Acknowledged?.Invoke(packet.Ack);Changed?.Invoke();return;}
            var message=packet.Message;if(message==null)return;
            if(packet.Kind=="sound")
            {if(Remember("sound:"+message.Id)&&message.Signal=="whistle")PlayWhistle?.Invoke(message);return;}
            bool historical=packet.Kind=="history";
            if(historical){if(!State.Dispatcher)return;}
            else if(packet.Kind!="message"||!Remember("message:"+message.Id))return;
            if(message.Channel=="crew"&&message.Target!=State.Crew||message.Channel=="train"&&message.Target!=State.Train)return;
            if(message.Channel!="global"&&message.Channel!="crew"&&message.Channel!="train"&&message.Channel!="dispatch")return;
            string key=message.Channel+":"+message.Target;List<VisibleRecord> rows;
            if(!History.TryGetValue(key,out rows))History[key]=rows=new List<VisibleRecord>();
            if(rows.All(row=>row.Message.Id!=message.Id))rows.Add(new VisibleRecord{Message=message,Received=realtime,Historical=historical});if(rows.Count>500)rows.RemoveAt(0);
            if(message.Channel=="dispatch")Conversations[message.Target]=message.TargetLabel;
            if(!historical&&(message.FromDispatcher||message.DispatchRequest)&&packet.CanReply)PendingDispatch[message.Id]=new VisibleRecord{Message=message,Received=realtime,DispatcherTicket=State.Dispatcher};
            Error=null;Acknowledged?.Invoke(packet.Ack);Changed?.Invoke();
        }
        bool Remember(string id){if(!seen.Add(id))return false;seenOrder.Enqueue(id);if(seenOrder.Count>4096)seen.Remove(seenOrder.Dequeue());return true;}
        void Clear(string prefix){foreach(var key in History.Keys.Where(key=>key.StartsWith(prefix,StringComparison.Ordinal)).ToArray())History.Remove(key);}
        public List<VisibleRecord> Rows(string channel,string target)
        {List<VisibleRecord> rows;return History.TryGetValue(channel+":"+target,out rows)?rows:new List<VisibleRecord>();}
        public IEnumerable<VisibleRecord> DispatchActivity(string localId)
        {
            if(State?.Dispatcher!=true)return Enumerable.Empty<VisibleRecord>();
            return History.Where(p=>p.Key.StartsWith("dispatch:",StringComparison.Ordinal)).SelectMany(p=>p.Value)
                .Where(r=>!PendingDispatch.ContainsKey(r.Message.Id)&&!answeredDispatch.Contains(r.Message.Id)&&!(r.Message.ReplyTo!=null&&r.Message.SenderId==localId)&&(r.Message.SenderId==localId||r.Message.Target=="fdl:"+State.Train||r.Message.Target=="fdl:"+State.SelectedCar||!r.Historical&&!r.Message.FromDispatcher&&r.Message.SenderId!=localId&&r.Message.ReplyTo!=null))
                .OrderByDescending(r=>r.Message.Utc).Take(5).Reverse();
        }
        public IEnumerable<VisibleRecord> StructuredDispatchActivity(string localId)
        {
            if(State==null||State.Dispatcher)return Enumerable.Empty<VisibleRecord>();
            var rows=History.Where(p=>p.Key.StartsWith("dispatch:",StringComparison.Ordinal)).SelectMany(p=>p.Value).ToArray();
            var requests=new HashSet<string>(rows.Where(r=>r.Message.DispatchRequest).Select(r=>r.Message.Id));
            return rows.Where(r=>!PendingDispatch.ContainsKey(r.Message.Id)&&!answeredDispatch.Contains(r.Message.Id)
                &&(r.Message.Parts!=null||r.Message.DispatchRequest||r.Message.ReplyTo!=null&&requests.Contains(r.Message.ReplyTo))
                &&(r.Message.SenderId==localId||r.Message.Target=="fdl:"+State.Train||r.Message.Target=="fdl:"+State.SelectedCar))
                .OrderByDescending(r=>r.Message.Utc).Take(5).Reverse();
        }
    }
}
