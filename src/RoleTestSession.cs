// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
namespace EnusDE.Communications
{
    public enum TestChatRole { Driver,Dispatcher,Administrator }
    // Local-only participants. Never registered with Steam, the game roster or the host archive.
    public sealed class RoleTestSession
    {
        static readonly string[] Ids={"test:driver","test:dispatcher","test:administrator"};
        readonly Dictionary<string,ClientSession> clients=new Dictionary<string,ClientSession>();
        readonly HostRouter router;
        public readonly WorldView View=new WorldView{Id="local-role-test"};
        public readonly List<ChatRecord> Records=new List<ChatRecord>();
        public TestChatRole Role {get;private set;}
        public string ActiveId=>Ids[(int)Role];
        public ClientSession Client=>clients[ActiveId];
        public Action<string,ServerPacket> Packet;
        public Func<double> MessageClock=()=>0;
        public RoleTestSession(WorldView source,string selectedTrain=null,Func<DateTime> clock=null)
        {
            foreach(var crew in source.Crews)View.Crews[crew.Key]=crew.Value;
            foreach(var train in source.Trains.Values)View.Trains[train.Id]=new WorkingTrain{Id=train.Id,Label=train.Label,Crew=train.Crew};
            if(View.Trains.Count==0)View.Trains["test:locomotive"]=new WorkingTrain{Id="test:locomotive",Label="Test 1"};
            if(selectedTrain==null||!View.Trains.ContainsKey(selectedTrain))selectedTrain=View.Trains.Keys.OrderBy(k=>k,StringComparer.Ordinal).First();
            string crewId=View.Trains[selectedTrain].Crew??"test:crew";if(!View.Crews.ContainsKey(crewId))View.Crews[crewId]="Test crew";
            View.People[Ids[0]]=new Person{Id=Ids[0],Name="[TEST] TF",Rank="Crew",Crew=crewId,SelectedCar=selectedTrain};
            View.People[Ids[1]]=new Person{Id=Ids[1],Name="[TEST] FDL",Rank="Dispatcher",Crew=crewId};
            View.People[Ids[2]]=new Person{Id=Ids[2],Name="[TEST] Admin",Rank="President",Host=true,Crew=crewId};
            foreach(var id in Ids)clients[id]=new ClientSession();
            router=new HostRouter(new HostPolicy(),(id,p)=>{clients[id].Accept(p,MessageClock());Packet?.Invoke(id,p);},r=>{Records.Add(r);if(Records.Count>500)Records.RemoveAt(0);},clock);
            router.Refresh(View);foreach(var id in Ids)router.Handle(id,clients[id].Request("hello"));
            router.Handle(Ids[0],clients[Ids[0]].Request("join",target:selectedTrain));
        }
        public void Select(TestChatRole role){if(!Enum.IsDefined(typeof(TestChatRole),role))throw new ArgumentOutOfRangeException(nameof(role));Role=role;}
        public void Send(ClientRequest request)=>router.Handle(ActiveId,request);
        public void Tick(){router.Refresh(View);foreach(var id in Ids)router.Handle(id,clients[id].Request("ping"));}
    }
}
