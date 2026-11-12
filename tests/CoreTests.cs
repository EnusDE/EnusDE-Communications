// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EnusDE.Communications;
using Newtonsoft.Json;
static class CoreTests
{
    static int passed;
    static string artifacts;
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Test(string name,Action action){action();passed++;Console.WriteLine("PASS "+name);}
    sealed class Network
    {
        public WorldView World=new WorldView{Id="test-railroad"};
        public Dictionary<string,ClientSession> Clients=new Dictionary<string,ClientSession>();
        public List<ChatRecord> Log=new List<ChatRecord>();
        public List<Tuple<string,ServerPacket>> Packets=new List<Tuple<string,ServerPacket>>();
        public HostRouter Host;
        public DateTime Now=new DateTime(2026,11,5,12,0,0,DateTimeKind.Utc);
        public Network()
        {
            World.Crews["A"]="Alpha";World.Crews["B"]="Bravo";
            Add("h",null,"Crew",true);Add("a","A");Add("c","A");Add("b","B");Add("d","B","Dispatcher");Add("g",null,"Passenger");
            World.Trains["open"]=new WorkingTrain{Id="open",Label="Test 1"};
            World.Trains["alpha"]=new WorkingTrain{Id="alpha",Label="Test 10",Crew="A"};
            World.Trains["bravo"]=new WorkingTrain{Id="bravo",Label="Test 20",Crew="B"};
            Host=new HostRouter(new HostPolicy(),(id,p)=>{Packets.Add(Tuple.Create(id,p));Clients[id].Accept(p,42);},r=>Log.Add(r),()=>Now);
            Host.Refresh(World);foreach(string id in World.People.Keys.ToArray()){Clients[id]=new ClientSession();Send(id,"hello");}
        }
        void Add(string id,string crew,string rank="Crew",bool host=false){World.People[id]=new Person{Id=id,Name="Verified "+id,Crew=crew,Rank=rank,Host=host};}
        public void Send(string id,string kind,string channel=null,string target=null,string text=null,string signal=null,string replyTo=null){Host.Handle(id,Clients[id].Request(kind,channel,target,text,signal,replyTo));}
        public int Count(string id,string channel,string target){return Clients[id].Rows(channel,target).Count;}
        public ClientRequest Template(string id,string channel,string target,MessageParts parts,string replyTo=null)
        {var request=Clients[id].Request("send",channel,target,replyTo:replyTo);request.Parts=parts;Host.Handle(id,request);return request;}
        public void Error(string id,string error){Check(Clients[id].Error==error,"Expected "+error+", got "+Clients[id].Error);}
    }
    static void Main(string[] args)
    {
        try
        {
            artifacts=Path.GetFullPath(args[0]);Directory.CreateDirectory(artifacts);
            Test("global / verified identity / literal text",()=>{
                var n=new Network();n.Send("a","send","global",text:"<b>Original</b> Grüße 日本語");
                foreach(string id in n.Clients.Keys)Check(n.Count(id,"global","global")==1,"Global delivery");
                Check(n.Log.Single().SenderId=="a"&&n.Log[0].SenderName=="Verified a","Sender must come from roster");
                Check(n.Log[0].Text=="<b>Original</b> Grüße 日本語","Free text must remain literal");
                var req=n.Clients["a"].Request("send","global",text:"forged");n.Host.Handle("unknown",req);Check(n.Log.Count==1,"Unknown peer accepted");
            });
            Test("crew isolation, automatic change, no history backfill",()=>{
                var n=new Network();n.Send("a","send","crew",text:"Alpha only");Check(n.Count("c","crew","A")==1&&n.Count("b","crew","A")==0,"Crew leak");
                var old=n.Clients["a"].Request("send","crew",text:"stale");n.World.People["a"].Crew="B";n.Host.Refresh(n.World);
                Check(n.Count("a","crew","A")==0&&n.Count("c","crew","A")==1,"Own history purge");
                n.Host.Handle("a",old);n.Error("a","membership_changed");Check(n.Log.Count==1,"Stale message accepted");
                n.Send("b","send","crew",text:"Bravo live");Check(n.Count("a","crew","B")==1,"New crew delivery");
                n.World.People["a"].Crew="A";n.Host.Refresh(n.World);Check(n.Count("a","crew","A")==0&&n.Count("a","crew","B")==0,"Rejoin backfill");
            });
            Test("train access matrix and selected target enforcement",()=>{
                var n=new Network();n.Send("g","join",target:"open");Check(n.Clients["g"].State.Train=="open","Open train");
                n.Send("b","join",target:"alpha");n.Error("b","train_denied");n.Send("a","join",target:"alpha");n.Send("c","join",target:"alpha");
                n.Send("a","send","train","bravo",signal:"stop");n.Error("a","train_denied");
                n.Send("a","send","train","alpha",signal:"stop");Check(n.Count("c","train","alpha")==1&&n.Count("b","train","alpha")==0,"Train delivery");
                n.Send("h","join",target:"bravo");Check(n.Clients["h"].State.Train=="bravo","Host bypass");
                n.World.People["b"].Rank="President";n.Host.Refresh(n.World);Check(!n.Clients["b"].State.Admin,"Rank implicitly granted admin");n.Host.Policy.AdminRanks=new[]{"President"};n.Host.Refresh(n.World);n.Send("b","join",target:"alpha");Check(n.Clients["b"].State.Admin,"Explicit admin bypass");
                n.World.People["b"].Rank="Crew";n.Host.Refresh(n.World);Check(n.Clients["b"].State.Train==null&&!n.Clients["b"].State.Admin,"Revoked access");
            });
            Test("ownership changes revoke membership and queued signals",()=>{
                var n=new Network();n.Send("g","join",target:"open");n.Send("g","send","train","open",text:"one");var old=n.Clients["g"].Request("send","train","open",signal:"forward");
                n.World.Trains["open"].Crew="A";n.Host.Refresh(n.World);Check(n.Clients["g"].State.Train==null&&n.Count("g","train","open")==0,"Ownership purge");
                n.Host.Handle("g",old);Check(n.Log.Count==1,"Old signal leaked");
            });
            Test("radial signals never global or crew; whitelist enforced",()=>{
                var n=new Network();n.Send("a","send","global",signal:"stop");n.Error("a","signal_train_only");n.Send("a","send","crew",signal:"whistle");n.Error("a","signal_train_only");
                n.Send("a","send","train","alpha",signal:"unknown");n.Error("a","signal_unknown");Check(n.Log.Count==0,"Invalid signals archived");
            });
            Test("whistle sound proximity separate from train text",()=>{
                var n=new Network();n.World.People["b"].X=79;n.World.People["g"].X=81;n.Host.Refresh(n.World);int b=0,g=0,a=0;
                n.Clients["b"].PlayWhistle=r=>b++;n.Clients["g"].PlayWhistle=r=>g++;n.Clients["a"].PlayWhistle=r=>a++;
                n.Send("a","join",target:"alpha");n.Send("a","send","train","alpha",signal:"whistle");
                Check(b==1&&g==0&&a==1,"Whistle radius");Check(n.Count("b","train","alpha")==0&&n.Count("g","global","global")==0,"Whistle text leak");
                Check(n.Log.Single().Signal=="whistle","Canonical archive");
            });
            Test("dispatcher directed contact, reply, isolation, separate train",()=>{
                var n=new Network();n.Send("a","join",target:"alpha");n.Send("c","join",target:"alpha");n.Send("d","join",target:"bravo");
                n.Send("d","contact",target:"alpha");string thread=n.Clients["d"].Conversations.Keys.Single();Check(n.Clients["d"].State.Train=="bravo","Dispatcher train changed");
                n.Send("d","send","dispatch",thread,signal:"wait");Check(n.Count("a","dispatch",thread)==1&&n.Count("b","dispatch",thread)==0,"Directed delivery");
                string request=n.Clients["a"].PendingDispatch.Keys.Single();n.Send("a","send","dispatch",thread,text:"Acknowledged",replyTo:request);Check(n.Count("d","dispatch",thread)==2&&n.Count("c","dispatch",thread)==2,"Reply routing");
                Check(n.Clients["a"].PendingDispatch.Count==0,"Answer link remains");n.Send("a","send","dispatch",thread,text:"again",replyTo:request);n.Error("a","reply_unavailable");
                n.Send("a","send","dispatch",thread,signal:"wait");n.Error("a","dispatcher_denied");
                n.Send("b","send","dispatch",thread,text:"intruder");n.Error("b","dispatcher_denied");
                n.Send("a","contact",target:"bravo");n.Error("a","dispatcher_denied");
                n.World.People["d"].Rank="Crew";n.Host.Refresh(n.World);n.Send("d","contact",target:"alpha");n.Error("d","dispatcher_denied");
                n.Send("h","contact",target:"alpha");Check(n.Count("h","dispatch",thread)==2,"New dispatcher cannot see handover history");Check(n.Count("d","dispatch",thread)==0,"Lost dispatcher kept history");
            });
            Test("admin crew inspection and revocation",()=>{
                var n=new Network();n.Send("a","crew",target:"B");n.Error("a","crew_denied");n.Send("h","crew",target:"B");
                n.Send("b","send","crew",text:"Seen by host");Check(n.Count("h","crew","B")==1&&n.World.People["h"].Crew==null,"Admin inspection changes actual crew");
                n.Send("h","crew");Check(n.Count("h","crew","B")==0&&n.Clients["h"].State.Crew==null,"Inspection purge");
            });
            Test("dispatcher handover survives issuer disconnect and host reload",()=>{
                var n=new Network();n.Send("a","join",target:"alpha");n.Send("d","contact",target:"alpha");string thread="fdl:alpha";n.Send("d","send","dispatch",thread,text:"Shared operation");string question=n.Clients["a"].PendingDispatch.Keys.Single();
                n.World.People.Remove("d");n.Host.Refresh(n.World);n.Send("a","send","dispatch",thread,text:"Still received",replyTo:question);Check(n.Count("h","dispatch",thread)==2,"Issuer departure closed conversation");
                n.Clients["h"]=new ClientSession();var restored=new HostRouter(new HostPolicy(),(id,p)=>n.Clients[id].Accept(p,1),r=>n.Log.Add(r),()=>n.Now,n.Log.ToArray());restored.Refresh(n.World);restored.Handle("h",n.Clients["h"].Request("hello"));restored.Handle("h",n.Clients["h"].Request("contact",target:"alpha"));Check(n.Count("h","dispatch",thread)==2,"Host reload lost shared history");
                restored.Handle("h",n.Clients["h"].Request("contact",target:"alpha"));Check(n.Count("h","dispatch",thread)==2,"Repeated history replay duplicated rows");
            });
            Test("dispatcher history has railroad, time and role boundaries",()=>{
                var n=new Network();var saved=new[]{new ChatRecord{Id="current",RailroadId=n.World.Id,Utc=n.Now,Channel="dispatch",Target="fdl:alpha",Text="Current"},new ChatRecord{Id="other",RailroadId="other-world",Utc=n.Now,Channel="dispatch",Target="fdl:alpha",Text="Foreign"},new ChatRecord{Id="old",RailroadId=n.World.Id,Utc=n.Now.AddHours(-6),Channel="dispatch",Target="fdl:alpha",Text="Expired"}};
                var router=new HostRouter(new HostPolicy(),(id,p)=>n.Clients[id].Accept(p,1),r=>{},()=>n.Now,saved);router.Refresh(new WorldView());router.Refresh(n.World);
                n.Clients["d"]=new ClientSession();router.Handle("d",n.Clients["d"].Request("hello"));router.Handle("d",n.Clients["d"].Request("contact",target:"alpha"));Check(n.Count("d","dispatch","fdl:alpha")==1&&n.Clients["d"].Rows("dispatch","fdl:alpha")[0].Message.Id=="current","Archive scope or expiry leaked");
                n.Clients["a"]=new ClientSession();router.Handle("a",n.Clients["a"].Request("hello"));router.Handle("a",n.Clients["a"].Request("join",target:"alpha"));router.Handle("a",n.Clients["a"].Request("contact",target:"alpha"));Check(n.Count("a","dispatch","fdl:alpha")==0&&n.Clients["a"].PendingDispatch.Count==0,"Ordinary player got history backfill");
            });
            Test("one-time reply cannot be forged, reused or carried across a train rejoin",()=>{
                var n=new Network();n.Send("a","join",target:"alpha");n.Send("d","contact",target:"alpha");n.Send("d","send","dispatch","fdl:alpha",text:"Question");string id=n.Clients["a"].PendingDispatch.Keys.Single();
                n.Send("c","join",target:"alpha");n.Send("c","send","dispatch","fdl:alpha",text:"Did not receive",replyTo:id);n.Error("c","reply_unavailable");
                n.Send("a","leave");n.Send("a","join",target:"alpha");n.Send("a","send","dispatch","fdl:alpha",text:"Old membership",replyTo:id);n.Error("a","reply_unavailable");
                n.Send("d","send","dispatch","fdl:alpha",text:"New question");id=n.Clients["a"].PendingDispatch.Keys.Single();n.Send("a","send","dispatch","fdl:alpha",text:"First reply",replyTo:id);n.Send("a","send","dispatch","fdl:alpha",text:"Duplicate reply",replyTo:id);n.Error("a","reply_unavailable");Check(n.Log.Count==3,"Invalid reply archived");
            });
            Test("unavailable and expired replies are removed without recording a second answer",()=>{
                var n=new Network();n.Send("a","join",target:"alpha");n.Send("d","contact",target:"alpha");n.Send("d","send","dispatch","fdl:alpha",text:"Question");string id=n.Clients["a"].PendingDispatch.Keys.Single();
                n.Now=n.Now.AddHours(6);n.Send("a","send","dispatch","fdl:alpha",text:"Too late",replyTo:id);n.Error("a","reply_unavailable");Check(n.Clients["a"].PendingDispatch.Count==0&&n.Log.Count==1,"Expired reply remained active");
            });
            Test("dispatcher can reach selected operators without enabling the train wheel",()=>{
                var n=new Network();n.World.People["a"].SelectedCar="alpha";n.World.People["b"].SelectedCar="alpha";n.Host.Refresh(n.World);n.Send("d","contact",target:"alpha");n.Send("d","send","dispatch","fdl:alpha",text:"Operator contact");
                Check(n.Clients["a"].PendingDispatch.Count==1&&n.Clients["a"].State.Train==null,"Selected operator not reached");Check(n.Clients["b"].PendingDispatch.Count==0,"Foreign crew selection bypass");
                string id=n.Clients["a"].PendingDispatch.Keys.Single();n.Send("a","send","train","alpha",signal:"stop");n.Error("a","train_denied");n.Send("a","send","dispatch","fdl:alpha",text:"Operator reply",replyTo:id);Check(n.Log.Count==2,"Selected operator reply failed");
            });
            Test("switching selected locomotive invalidates old direct replies without clearing enrolled train",()=>{
                var n=new Network();n.World.People["a"].SelectedCar="alpha";n.Host.Refresh(n.World);n.Send("d","contact",target:"alpha");n.Send("d","send","dispatch","fdl:alpha",text:"Question");string id=n.Clients["a"].PendingDispatch.Keys.Single();
                n.World.People["a"].SelectedCar="open";n.Host.Refresh(n.World);Check(n.Clients["a"].PendingDispatch.Count==0,"Old selected-loco request retained");n.World.People["a"].SelectedCar="alpha";n.Host.Refresh(n.World);n.Send("a","send","dispatch","fdl:alpha",text:"Old selection",replyTo:id);n.Error("a","reply_unavailable");
                n.Send("a","join",target:"alpha");n.Send("d","send","dispatch","fdl:alpha",text:"Enrolled question");id=n.Clients["a"].PendingDispatch.Keys.Single();n.World.People["a"].SelectedCar="open";n.Host.Refresh(n.World);Check(n.Clients["a"].State.Train=="alpha"&&n.Clients["a"].PendingDispatch.Count==1,"Camera switch changed enrolled train");n.Send("a","send","dispatch","fdl:alpha",text:"Enrolled reply",replyTo:id);Check(n.Log.Count==3,"Enrolled reply blocked after camera switch");
            });
            Test("host and dispatcher operating a train retain a one-time reply",()=>{
                var n=new Network();n.Send("h","join",target:"alpha");n.World.People["d"].SelectedCar="alpha";n.Host.Policy.AdminRanks=new[]{"Dispatcher"};n.Host.Refresh(n.World);
                n.Send("h","contact",target:"alpha");n.Send("h","send","dispatch","fdl:alpha",text:"Question from host");
                string question=n.Log.Last().Id;Check(n.Clients["h"].PendingDispatch.Count==0,"Sender received a reply-to-self link");Check(n.Clients["d"].PendingDispatch.ContainsKey(question),"Dispatcher operator lost reply link");
                n.Send("d","ping");Check(n.Clients["d"].PendingDispatch.ContainsKey(question),"Heartbeat cleared privileged operator reply");
                n.Send("d","send","dispatch","fdl:alpha",text:"Operator answer",replyTo:question);
                Check(n.Clients["d"].PendingDispatch.Count==0&&!n.Log.Last().FromDispatcher&&n.Log.Last().ReplyTo==question,"Answer became a new dispatcher question");
                n.Send("d","send","dispatch","fdl:alpha",text:"Answer replay",replyTo:question);n.Error("d","reply_unavailable");Check(n.Log.Count==2,"Dispatcher role bypassed single reply");
                n.Send("d","send","dispatch","fdl:alpha",text:"Question to host");Check(n.Clients["h"].PendingDispatch.Count==1,"Host operating train cannot reply");
                question=n.Clients["h"].PendingDispatch.Keys.Single();n.Send("h","send","dispatch","fdl:alpha",text:"Host answer",replyTo:question);Check(n.Log.Last().ReplyTo==question&&!n.Log.Last().FromDispatcher,"Host answer misclassified");
            });
            Test("role changes preserve received operator requests without exposing dispatcher history",()=>{
                var n=new Network();n.Send("a","join",target:"alpha");n.Send("d","contact",target:"alpha");n.Send("d","send","dispatch","fdl:alpha",text:"Question");string question=n.Clients["a"].PendingDispatch.Keys.Single();
                n.World.People["a"].Rank="Dispatcher";n.Host.Refresh(n.World);Check(n.Clients["a"].PendingDispatch.ContainsKey(question),"Granting FDL role erased operator request");
                n.Send("a","contact",target:"bravo");n.World.People["a"].Rank="Crew";n.Host.Refresh(n.World);Check(n.Clients["a"].PendingDispatch.ContainsKey(question)&&n.Clients["a"].Conversations.Count==0,"Revoking FDL role lost request or retained privileged conversations");
                n.Send("a","send","dispatch","fdl:alpha",text:"Still an operator",replyTo:question);Check(n.Log.Count==2,"Operator reply blocked after role revocation");
            });
            Test("FDL activity shows own and operated train conversations without unrelated traffic",()=>{
                var n=new Network();n.Send("h","join",target:"alpha");n.Send("h","contact",target:"alpha");n.Send("h","send","dispatch","fdl:alpha",text:"Own activity");
                Check(n.Clients["h"].DispatchActivity("h").Count()==1&&n.Clients["h"].PendingDispatch.Count==0,"Own conversation missing or self-reply enabled");
                n.Send("d","contact",target:"bravo");n.Send("d","send","dispatch","fdl:bravo",text:"Unrelated traffic");Check(n.Clients["h"].DispatchActivity("h").Count()==1,"Unrelated train flooded the main chat");
                n.Send("d","contact",target:"alpha");n.Send("d","send","dispatch","fdl:alpha",text:"Incoming request");Check(n.Clients["h"].PendingDispatch.Count==2&&n.Clients["h"].DispatchActivity("h").Count()==1,"Incoming request is missing or duplicated as activity");
                Check(!n.Clients["a"].DispatchActivity("a").Any(),"Ordinary operator can open privileged conversations");
            });
            Test("host and FDL outside a train can answer incoming requests once and clear the main card",()=>{
                var n=new Network();n.Send("d","contact",target:"alpha");n.Send("d","send","dispatch","fdl:alpha",text:"Question for duty desk");
                var h=n.Clients["h"];string question=h.PendingDispatch.Keys.Single();Check(h.State.Train==null&&h.PendingDispatch[question].DispatcherTicket,"FDL-only reply ticket missing");
                n.Send("h","send","dispatch","fdl:alpha",text:"Host response",replyTo:question);
                Check(h.PendingDispatch.Count==0&&!h.DispatchActivity("h").Any(),"Answered request or own reply reappeared as a conversation card");
                n.Send("h","send","dispatch","fdl:alpha",text:"Repeated response",replyTo:question);n.Error("h","reply_unavailable");Check(n.Log.Count==2,"Privileged reply was repeated");
            });
            Test("FDL-only answer tickets do not survive revocation and regrant",()=>{
                var n=new Network();n.Send("h","contact",target:"alpha");n.Send("h","send","dispatch","fdl:alpha",text:"Question");string question=n.Clients["d"].PendingDispatch.Keys.Single();
                n.World.People["d"].Rank="Crew";n.Host.Refresh(n.World);Check(n.Clients["d"].PendingDispatch.Count==0,"Lost role retained privileged request");
                n.World.People["d"].Rank="Dispatcher";n.Host.Refresh(n.World);n.Send("d","send","dispatch","fdl:alpha",text:"Old privilege",replyTo:question);n.Error("d","reply_unavailable");Check(n.Log.Count==1,"Old role ticket became valid again");
            });
            Test("main timeline interleaves FDL and channel messages with newest at the bottom",()=>{
                var n=new Network();n.Send("d","contact",target:"alpha");n.Send("a","send","global",text:"Older");n.Now=n.Now.AddSeconds(5);n.Send("d","send","dispatch","fdl:alpha",text:"FDL middle");n.Now=n.Now.AddSeconds(5);n.Send("a","send","global",text:"Newest");
                var items=ChatTimeline.Compose(n.Clients["h"],"global","global",false,"h");Check(items.Select(i=>i.Row.Message.Text).SequenceEqual(new[]{"Older","FDL middle","Newest"}),"New channel message was placed above an old pending FDL request");
                Check(items[1].Kind==ChatItemKind.Reply&&ChatTimeline.BottomPadding(215,100)==115&&ChatTimeline.BottomPadding(215,160)==55&&ChatTimeline.BottomPadding(215,300)==0,"Short timelines do not grow upward from the bottom");
            });
            Test("train paging is bounded, searchable and stable while the roster changes",()=>{
                var trains=Enumerable.Range(1,10).Select(i=>new WorkingTrain{Id=i.ToString(),Label="Engine "+i.ToString("D2")}).Reverse().ToArray();
                var first=new TrainPage(trains,"",0);var last=new TrainPage(trains,"",99);Check(first.Items.Length==4&&first.Items[0].Id=="1"&&!first.Previous&&first.Next&&first.Pages==3,"First page invalid");Check(last.Index==2&&last.Items.Length==2&&last.Previous&&!last.Next,"Last page invalid");
                var searched=new TrainPage(trains,"engine 09",2);Check(searched.Index==0&&searched.Total==1&&searched.Items.Single().Id=="9","Search did not reset/clamp page");Check(new TrainPage(trains.Take(2),"",2).Index==0&&new TrainPage(trains,"missing",0).Items.Length==0,"Shrinking or empty results left an invalid page");
            });
            Test("deduplication, rate limit and text bounds",()=>{
                var n=new Network();var request=n.Clients["a"].Request("send","global",text:"once");n.Host.Handle("a",request);n.Host.Handle("a",request);Check(n.Log.Count==1,"Duplicate sent");
                n.Send("a","send","global",text:new string('a',501));n.Error("a","text_invalid");
                for(int i=0;i<30;i++)n.Send("b","send","global",text:"allowed");n.Send("b","send","global",text:"limited");n.Error("b","rate_limit");Check(n.Log.Count==31,"Rate exceeded");
                n.Now=n.Now.AddSeconds(5);n.Send("b","send","global",text:"resumed");Check(n.Log.Count==32,"Rate did not reset");
            });
            Test("session nonce, token and membership packet guards",()=>{
                var n=new Network();var a=n.Clients["a"];n.Send("a","send","crew",text:"record");var packet=n.Packets.Last(p=>p.Item1=="a"&&p.Item2.Kind=="message").Item2;
                a.Accept(packet,50);Check(n.Count("a","crew","A")==1,"Client duplicate");
                var json=JsonConvert.SerializeObject(packet);var forged=JsonConvert.DeserializeObject<ServerPacket>(json);forged.Message.Id="new";forged.Token="wrong";a.Accept(forged,50);Check(n.Count("a","crew","A")==1,"Token forgery");
                var old=n.Clients["a"].Request("send","global",text:"old");var replacement=new ClientSession();n.Clients["a"]=replacement;n.Send("a","hello");n.Host.Handle("a",old);Check(n.Log.Count==1,"Old session accepted");
                replacement.Accept(packet,99);Check(replacement.History.Count==0,"Old packet accepted");
            });
            Test("repeated hello retains live history; reconnect starts empty",()=>{
                var n=new Network();n.Send("a","send","crew",text:"keep");n.Send("a","hello");Check(n.Count("a","crew","A")==1,"Hello cleared history");
                n.Clients["a"]=new ClientSession();n.Send("a","hello");Check(n.Clients["a"].History.Count==0,"Reconnect backfill");
            });
            Test("client history bounded at 500 and dispatch cleared on leave",()=>{
                var n=new Network();n.Send("a","join",target:"alpha");n.Send("d","contact",target:"alpha");string thread=n.Clients["d"].Conversations.Keys.Single();
                n.Send("d","send","dispatch",thread,text:"contact");n.Send("a","leave");Check(n.Clients["a"].Conversations.Count==0&&n.Count("a","dispatch",thread)==0,"Dispatch lingered");
                for(int i=0;i<510;i++){n.Now=n.Now.AddSeconds(5);n.Send("a","send","global",text:i.ToString());}Check(n.Count("a","global","global")==500,"History cap");
            });
            Test("failed archive prevents unlogged delivery",()=>{
                var n=new Network();var failed=new HostRouter(new HostPolicy(),(id,p)=>n.Clients[id].Accept(p,1),r=>{throw new IOException("disk");});
                n.Clients["a"]=new ClientSession();failed.Refresh(n.World);failed.Handle("a",n.Clients["a"].Request("hello"));failed.Handle("a",n.Clients["a"].Request("send","global",text:"blocked"));
                n.Error("a","archive_failed");Check(n.Count("a","global","global")==0,"Unlogged delivery");
            });
            Test("archive six real hours, boundary, startup and temp cleanup",()=>{
                string dir=Path.Combine(artifacts,"archive-"+Guid.NewGuid().ToString("N"));var now=new DateTime(2026,11,5,12,0,0,DateTimeKind.Utc);var archive=new HostArchive(dir,()=>now);
                archive.Append(new ChatRecord{Id="old",Utc=now.AddHours(-6).AddSeconds(1),Text="visible"});archive.Append(new ChatRecord{Id="new",Utc=now,Text="new"});Check(archive.Read().Count==2,"Early deletion");
                now=now.AddSeconds(1);archive.Purge();Check(archive.Read().Single().Id=="new","Boundary deletion");
                string stale=now.AddHours(-7).Ticks.ToString("D19")+"-crash.comm.json.tmp";File.WriteAllText(Path.Combine(dir,stale),"partial");
                now=now.AddHours(7);archive=new HostArchive(dir,()=>now);Check(Directory.GetFiles(dir).Length==0&&archive.Read().Count==0,"Startup cleanup");
            });
            Test("settings invalid values normalized and atomically replaced",()=>{
                string file=Path.Combine(artifacts,"settings.json");var s=new ModSettings{FadeSeconds=float.NaN,ChatWidth=12,Host=new HostPolicy{AdminRanks=null,DispatcherRanks=null}};s.Write(file);s.FadeSeconds=0;s.Write(file);var read=ModSettings.Read(file);
                Check(read.FadeSeconds==0&&read.ChatWidth==440&&read.Host.AdminRanks.Length==0,"Settings normalization");Check(!File.Exists(file+".tmp"),"Temp settings remained");
            });
            Test("personal settings migration, precedence, colors, hotkey and fade steps",()=>{
                string home=Path.Combine(artifacts,"home-"+Guid.NewGuid().ToString("N")),mod=Path.Combine(home,"Mods","Communications");Directory.CreateDirectory(Path.Combine(mod,"Data"));
                new ModSettings{FontSize=19,ChatBinding="<Keyboard>/enter",BackgroundOpacity=.4f,BackgroundR=.8f}.Write(Path.Combine(mod,"Data","Settings.json"));var loaded=ModSettings.LoadUser(home,mod);
                Check(File.Exists(Path.Combine(home,"ModSettings","enusde-communications.json"))&&loaded.FontSize==19&&loaded.ChatBinding=="<Keyboard>/enter"&&loaded.BackgroundOpacity==.4f&&loaded.BackgroundR==.8f,"Migration lost personal values");
                loaded.FontSize=21;loaded.Write(ModSettings.ForHome(home));Check(ModSettings.LoadUser(home,mod).FontSize==21,"Legacy overrides user settings");Check(File.Exists(Path.Combine(mod,"Data","Settings.json")),"Legacy was removed");
                Check(new ModSettings().ChatBinding=="","Chat hotkey preassigned");foreach(float value in new float[]{5,10,15,20,25,30,35,40,45,50,55,60})Check(ModSettings.NormalizeFade(value)==value,"Fade step changed");Check(ModSettings.NormalizeFade(65)==0&&ModSettings.NormalizeFade(0)==0,"Unlimited fade setting");
            });
            Test("multiplayer visibility overrides the singleplayer choice without changing it",()=>{
                var settings=new ModSettings{ChatVisible=false};Check(!ChatVisibility.IsVisible(false,settings.ChatVisible),"SP hide ignored");Check(ChatVisibility.IsVisible(true,settings.ChatVisible),"MP remained hidden");Check(!settings.ChatVisible,"MP changed the SP preference");Check(!ChatVisibility.IsVisible(false,settings.ChatVisible),"Leaving MP lost the SP choice");settings.ChatVisible=true;Check(ChatVisibility.IsVisible(false,settings.ChatVisible)&&ChatVisibility.IsVisible(true,settings.ChatVisible),"SP enable failed");
            });
            Test("native console notice targets only online peers without a compatible chat session",()=>{
                var n=new Network();n.World.People["plain"]=new Person{Id="plain",Name="No mod",Rank="Crew"};n.Host.Refresh(n.World);var queue=new ConsoleNoticeQueue();var sent=new List<string>();
                queue.Enqueue("plain",0);queue.Enqueue("a",0);queue.Enqueue("unknown",0);queue.Flush(1,id=>n.World.People.ContainsKey(id),n.Host.HasCompatibleClient,sent.Add);Check(sent.Count==0,"No handshake grace");queue.Flush(2,id=>n.World.People.ContainsKey(id),n.Host.HasCompatibleClient,sent.Add);Check(sent.SequenceEqual(new[]{"plain"}),"Notice reached the wrong player");
                Check(n.Clients["h"].State.PeerChat["a"]&&!n.Clients["h"].State.PeerChat["plain"],"Peer compatibility indicator incorrect");
            });
            Test("console notice handshake grace, cooldown, disconnect and reset",()=>{
                var queue=new ConsoleNoticeQueue();bool compatible=false,online=true;int sent=0;queue.Enqueue("p",0);compatible=true;queue.Flush(2,_=>online,_=>compatible,_=>sent++);Check(sent==0,"New compatible client notified");
                compatible=false;queue.Enqueue("p",3);queue.Flush(5,_=>online,_=>compatible,_=>sent++);queue.Enqueue("p",6);queue.Flush(8,_=>online,_=>compatible,_=>sent++);Check(sent==1,"Notice spam");
                queue.Enqueue("p",36);queue.Flush(38,_=>online,_=>compatible,_=>sent++);Check(sent==2,"Cooldown did not expire");queue.Enqueue("p",70);online=false;queue.Flush(72,_=>online,_=>compatible,_=>sent++);Check(sent==2,"Disconnected player notified");
                online=true;queue.Enqueue("p",73);queue.Clear();queue.Flush(80,_=>online,_=>compatible,_=>sent++);Check(sent==2,"Notice survived session reset");
            });
            Test("chat compatibility expires and returns after a valid heartbeat",()=>{
                var n=new Network();Check(n.Host.HasCompatibleClient("a"),"Missing hello capability");n.Now=n.Now.AddSeconds(31);n.Host.Refresh(n.World);Check(!n.Host.HasCompatibleClient("a")&&!n.Clients["h"].State.PeerChat["a"],"Stale compatibility retained");n.Send("a","ping");n.Host.Refresh(n.World);Check(n.Host.HasCompatibleClient("a")&&n.Clients["h"].State.PeerChat["a"],"Heartbeat did not restore compatibility");
            });
            Test("structured train messages use verified locomotive labels and canonical English",()=>{
                var n=new Network();n.Send("a","join",target:"alpha");n.Send("c","join",target:"alpha");
                var request=n.Template("a","train","alpha",new MessageParts{Id="movement_request",TrainId="alpha",TrainLabel="forged",Origin=" Bryson {Depot} ",Destination="Sylva"});
                var record=n.Log.Single();Check(record.Parts!=request.Parts&&record.Parts.TrainLabel=="Test 10"&&record.Parts.Origin=="Bryson {Depot}","Unverified parts retained");
                Check(record.Text=="Locomotive Test 10 requests permission to travel from Bryson {Depot} to Sylva."&&n.Count("c","train","alpha")==1,"Template was not delivered as canonical English plus parts");
            });
            Test("crew-specific templates retain crew isolation and reject the wrong channel",()=>{
                var n=new Network();n.Send("a","join",target:"alpha");var p=new MessageParts{Id="crew_help",Origin="Sylva"};n.Template("a","crew","A",p);
                Check(n.Count("c","crew","A")==1&&n.Count("b","crew","B")==0,"Crew template leaked");n.Template("a","global","global",p);n.Error("a","template_invalid");Check(n.Log.Count==1,"Global quick communication accepted");
            });
            Test("template schema rejects unknown, oversized, control and conflicting values",()=>{
                var n=new Network();n.Send("a","join",target:"alpha");
                foreach(var p in new[]{new MessageParts{Id="unknown",TrainId="alpha"},new MessageParts{Id="arrived",TrainId="alpha",Origin=new string('x',81)},new MessageParts{Id="arrived",TrainId="alpha",Origin="Line\nBreak"},new MessageParts{Id="movement_request",TrainId="alpha",Origin="Bryson"},new MessageParts{Id="hold",TrainId="alpha",Origin="Bryson"}})
                {n.Template("a","train","alpha",p);n.Error("a","template_invalid");}
                var conflict=n.Clients["a"].Request("send","train","alpha",signal:"stop");conflict.Parts=new MessageParts{Id="understood",TrainId="alpha"};n.Host.Handle("a",conflict);n.Error("a","template_invalid");Check(n.Log.Count==0,"Invalid template archived");
            });
            Test("operator initiates FDL request and only dispatchers receive answer tickets",()=>{
                var n=new Network();n.Send("g","join",target:"open");n.Send("a","join",target:"open");
                n.Template("g","dispatch","fdl:open",new MessageParts{Id="movement_request",TrainId="open",Origin="Bryson",Destination="Sylva",ToDispatcher=true});var original=n.Log.Single();
                Check(original.DispatchRequest&&!original.FromDispatcher&&n.Clients["d"].PendingDispatch.ContainsKey(original.Id)&&n.Clients["h"].PendingDispatch.ContainsKey(original.Id),"Dispatcher request did not create tickets");
                Check(!n.Clients["g"].PendingDispatch.ContainsKey(original.Id)&&!n.Clients["a"].PendingDispatch.ContainsKey(original.Id)&&n.Count("b","dispatch","fdl:open")==0,"Operator received an FDL answer ticket or foreign train received message");
                Check(ChatTimeline.Compose(n.Clients["g"],"train","open",false,"g").Any(x=>x.Row.Message.Id==original.Id&&x.Kind==ChatItemKind.Message),"Own request missing from main chat");
                n.Template("d","dispatch","fdl:open",new MessageParts{Id="hold",TrainId="open",Origin="Bryson"},original.Id);
                var answer=n.Log.Last();Check(answer.FromDispatcher&&answer.ReplyTo==original.Id&&!n.Clients["d"].PendingDispatch.ContainsKey(original.Id),"FDL answer did not consume its ticket");
                Check(n.Clients["g"].StructuredDispatchActivity("g").Any(x=>x.Message.Id==answer.Id)&&!n.Clients["g"].PendingDispatch.ContainsKey(answer.Id),"FDL answer not visible or created an unwanted new reply ticket");
                n.Template("d","dispatch","fdl:open",new MessageParts{Id="understood",TrainId="open"},original.Id);n.Error("d","reply_unavailable");Check(n.Log.Count==2,"Reply ticket reused");
            });
            Test("FDL requests require enrollment and exact train/thread identity",()=>{
                var n=new Network();n.Template("g","dispatch","fdl:open",new MessageParts{Id="understood",TrainId="open",ToDispatcher=true});n.Error("g","train_denied");
                n.Send("g","join",target:"open");n.Template("g","dispatch","fdl:alpha",new MessageParts{Id="understood",TrainId="alpha",ToDispatcher=true});n.Error("g","train_denied");
                n.Template("g","dispatch","fdl:open",new MessageParts{Id="understood",TrainId="alpha",ToDispatcher=true});n.Error("g","template_invalid");
                n.Template("g","dispatch","fdl:open",new MessageParts{Id="understood",TrainId="open"});n.Error("g","reply_unavailable");
                n.Template("g","train","open",new MessageParts{Id="understood",TrainId="open",ToDispatcher=true});n.Error("g","template_invalid");Check(n.Log.Count==0,"Unauthorized request accepted");
            });
            Test("host operating a train can request the FDL without being misclassified",()=>{
                var n=new Network();n.Send("h","join",target:"alpha");n.Template("h","dispatch","fdl:alpha",new MessageParts{Id="ready",TrainId="alpha",Origin="Bryson",Destination="Sylva",ToDispatcher=true});
                var record=n.Log.Single();Check(record.DispatchRequest&&!record.FromDispatcher&&n.Clients["d"].PendingDispatch.ContainsKey(record.Id)&&!n.Clients["h"].PendingDispatch.ContainsKey(record.Id),"Host operator classified as an FDL directive");
            });
            Test("dispatcher templates can address a different crew through the FDL channel",()=>{
                var n=new Network();n.Send("a","join",target:"alpha");n.Template("a","dispatch","fdl:alpha",new MessageParts{Id="movement_request",TrainId="alpha",Origin="Bryson",Destination="Sylva",ToDispatcher=true});string request=n.Log.Single().Id;
                Check(!n.Host.Policy.CanTrain(n.World.People["d"],n.World.Trains["alpha"]),"Fixture is not a foreign crew");
                n.Template("d","dispatch","fdl:alpha",new MessageParts{Id="proceed",TrainId="alpha",Origin="Bryson",Destination="Sylva"},request);
                Check(n.Log.Count==2&&n.Log.Last().Parts.Id=="proceed","Foreign-crew FDL answer blocked");
                n.Template("d","crew","B",new MessageParts{Id="proceed",TrainId="alpha",Origin="Bryson",Destination="Sylva"});Check(n.Log.Count==2,"FDL-only template accepted outside FDL");
            });
            Test("stale message-form context cannot follow a changed train enrollment",()=>{
                var n=new Network();n.Send("a","join",target:"alpha");var stale=n.Clients["a"].Request("send","train","alpha");stale.Parts=new MessageParts{Id="understood",TrainId="alpha"};
                n.Send("a","leave");n.Host.Handle("a",stale);n.Error("a","membership_changed");Check(n.Log.Count==0,"Stale builder context sent to another train");
            });
            Test("structured FDL history survives JSON archive and dispatcher handover",()=>{
                var n=new Network();n.Send("a","join",target:"alpha");n.Template("a","dispatch","fdl:alpha",new MessageParts{Id="movement_request",TrainId="alpha",Origin="Bryson",Destination="Sylva",ToDispatcher=true});
                string directory=Path.Combine(artifacts,"template-history-"+Guid.NewGuid().ToString("N"));var archive=new HostArchive(directory,()=>n.Now);archive.Append(n.Log.Single());
                var loaded=archive.Read().Single();Check(loaded.DispatchRequest&&loaded.Parts.TrainId=="alpha"&&loaded.Parts.Destination=="Sylva","Structured archive lost values");
                var client=new ClientSession();var host=new HostRouter(new HostPolicy(),(id,p)=>client.Accept(p,0),r=>{},()=>n.Now,archive.Read());host.Refresh(n.World);host.Handle("d",client.Request("hello"));host.Handle("d",client.Request("contact",target:"alpha"));
                Check(client.Rows("dispatch","fdl:alpha").Single().Message.Parts.Id=="movement_request","Handover lost template history");
            });
            Test("template request deduplication and protocol compatibility remain enforced",()=>{
                var n=new Network();n.Send("a","join",target:"alpha");var request=n.Template("a","train","alpha",new MessageParts{Id="understood",TrainId="alpha"});n.Host.Handle("a",request);Check(n.Log.Count==1,"Structured request replayed");
                var old=n.Clients["a"].Request("send","train","alpha",text:"Old protocol");old.Protocol=3;n.Host.Handle("a",old);Check(n.Log.Count==1,"Protocol 3 accepted new routing semantics");
            });
            Test("location choices are bounded, searchable and preserve proper names",()=>{
                var places=MessageTemplates.Places(new[]{"Sylva"," sylva ","Bryson City","Dillsboro","Bad\nName",""},"syl");Check(places.Length==1&&places[0]=="Sylva","Place filter/deduplication changed names");
                Check(MessageTemplates.Places(Enumerable.Range(0,600).Select(i=>"Place "+i)).Length==500,"Unbounded location list");
                var parts=new MessageParts{Id="movement_request",TrainLabel="TuT 10",Origin="{Depot} 日本語",Destination="Sylva"};Check(MessageTemplates.Render(parts,k=>"Bad {9}").Contains("{Depot} 日本語"),"Bad language format crashed or altered user values");
            });
            Test("global assistance templates require no train and never enable global F10",()=>{
                var n=new Network();n.Template("g","global","global",new MessageParts{Id="general_help",TrainId="forged",TrainLabel="forged"});
                Check(n.Log.Single().Text=="I need help."&&n.Log.Single().Parts.TrainId==null&&n.Clients.Values.All(c=>c.Rows("global","global").Count==1),"General template needed a train or retained fake label");
                n.Send("g","send","global",signal:"stop");n.Error("g","signal_train_only");Check(n.Log.Count==1,"F10-style global signal became possible");
            });
            Test("crew coordination and train work assignment do not change enrollment",()=>{
                var n=new Network();n.Template("a","crew","A",new MessageParts{Id="crew_take_task",Origin="Bryson",Task="pickup"});
                n.Template("a","crew","A",new MessageParts{Id="crew_assign",TrainId="alpha",TrainLabel="fake",Origin="Sylva",Task="deliver"});
                Check(n.Log.Count==2&&n.Log.Last().Parts.TrainLabel=="Test 10"&&n.Clients["a"].State.Train==null&&n.Count("c","crew","A")==2&&n.Count("b","crew","B")==0,"Crew assignment altered membership or leaked");
                n.Template("a","crew","A",new MessageParts{Id="crew_assign",TrainId="bravo",Origin="Sylva",Task="deliver"});n.Error("a","train_denied");
                n.Template("a","crew","A",new MessageParts{Id="crew_take_task",Origin="Sylva",Task="unknown"});n.Error("a","template_invalid");Check(n.Log.Count==2,"Invalid work assignment accepted");
            });
            Test("persistent operator conversation supports repeated free-text questions and FDL replies",()=>{
                var n=new Network();n.Send("g","join",target:"open");n.Send("g","contact_operator",target:"open");
                Check(n.Packets.Last(p=>p.Item1=="g").Item2.OperatorConversation,"Operator conversation not identified");
                for(int i=0;i<2;i++)
                {
                    var question=n.Clients["g"].Request("send","dispatch","fdl:open",text:"May I depart? "+i);question.ToDispatcher=true;n.Host.Handle("g",question);string id=n.Log.Last().Id;
                    Check(n.Clients["d"].PendingDispatch.ContainsKey(id),"FDL did not receive a new question notification");
                    n.Send("d","send","dispatch","fdl:open",text:"Please wait "+i,replyTo:id);
                }
                Check(n.Count("g","dispatch","fdl:open")==4&&n.Log.Count==4&&n.Clients["g"].Conversations.ContainsKey("fdl:open"),"Conversation stopped after one reply");
            });
            Test("operator conversation reopening does not reveal archived messages from before joining",()=>{
                var n=new Network();n.Send("g","join",target:"open");var question=n.Clients["g"].Request("send","dispatch","fdl:open",text:"Earlier question");question.ToDispatcher=true;n.Host.Handle("g",question);
                n.Send("a","join",target:"open");n.Send("a","contact_operator",target:"open");Check(n.Count("a","dispatch","fdl:open")==0,"New operator received historical messages");
                n.Send("a","contact_operator",target:"alpha");n.Error("a","dispatcher_denied");
                n.Send("d","contact",target:"open");Check(n.Count("d","dispatch","fdl:open")==1,"FDL lost the handover history");
            });
            Test("TF answers notify the FDL without requiring locomotive selection",()=>{
                var n=new Network();n.Send("g","join",target:"open");n.Send("d","contact",target:"open");n.Send("d","send","dispatch","fdl:open",text:"Please report");string original=n.Log.Last().Id;
                n.Send("g","send","dispatch","fdl:open",text:"Ready now",replyTo:original);string answer=n.Log.Last().Id;
                Check(n.Clients["d"].State.Train==null&&n.Clients["d"].DispatchActivity("d").Any(r=>r.Message.Id==answer),"FDL answer notification hidden without selected train");
                n.Send("d","contact",target:"open");Check(!n.Clients["d"].DispatchActivity("d").Any(r=>r.Message.Id==answer),"Opening archived replies created a fresh answer notification");
            });
            RoleTestTests.Run(Test,Check);
            ActionMenuTests.Run(Test,Check);
            PresentationTests.Run(Test,Check);
            Test("non-host action-menu choice reaches the enrolled train through the host router",()=>{
                var n=new Network();n.Send("g","join",target:"open");n.Send("h","join",target:"open");
                var menu=new HoldWheelController();var client=n.Clients["g"];
                var context=new MenuContext{Train=client.State.Train,Token=client.State.Token,Version=client.State.Version};
                Check(!client.State.Admin&&!client.State.Dispatcher&&menu.Open(context,new[]{"stop"},true),"Regular guest could not open menu");
                menu.Hover(0);string signal;Check(menu.Tick(context,false,true,true,out signal)==MenuResult.Selected,"Guest choice lost");n.Send("g","send","train",context.Train,signal:signal);
                Check(n.Count("h","train","open")==1&&n.Log.Single().SenderId=="g","Guest signal did not reach the train");
            });
            MetadataTests.Run(args[1],args[2],Test,Check);
            PerformanceTests.Run(Test,Check);
            Console.WriteLine("RESULT "+passed+" tests passed");
        }
        catch(Exception error){Console.Error.WriteLine("FAIL "+error);Environment.ExitCode=1;}
    }
}
