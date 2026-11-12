using System;
using System.Linq;
using EnusDE.Communications;
static class RoleTestTests
{
    static WorldView Source()
    {var v=new WorldView{Id="real-save"};v.Crews["A"]="Crew A";v.Trains["a"]=new WorkingTrain{Id="a",Label="TuT 10",Crew="A"};v.People["real-player"]=new Person{Id="real-player",Name="Real host",Host=true};return v;}
    public static void Run(Action<string,Action> test,Action<bool,string> check)
    {
        test("local host role test gives TF, FDL and admin distinct permissions",()=>{
            var lab=new RoleTestSession(Source(),"a");check(!lab.Client.State.Admin&&!lab.Client.State.Dispatcher&&lab.Client.State.Train=="a","TF inherited host rights");
            lab.Send(lab.Client.Request("contact",target:"a"));check(lab.Client.Error=="dispatcher_denied","TF performed an FDL operation");
            lab.Select(TestChatRole.Dispatcher);check(lab.Client.State.Dispatcher&&!lab.Client.State.Admin,"FDL rights incorrect");
            lab.Select(TestChatRole.Administrator);check(lab.Client.State.Admin&&lab.Client.State.Dispatcher,"Admin rights incorrect");
        });
        test("local role test can send, inspect, acknowledge and continue both sides",()=>{
            var lab=new RoleTestSession(Source(),"a");var driver=lab.Client;var question=driver.Request("send","dispatch","fdl:a",text:"May I depart?");question.ToDispatcher=true;lab.Send(question);
            lab.Select(TestChatRole.Dispatcher);string request=lab.Client.PendingDispatch.Keys.Single();lab.Send(lab.Client.Request("send","dispatch","fdl:a",signal:"wait",replyTo:request));check(lab.Client.PendingDispatch.Count==0,"FDL answer did not consume notification");
            lab.Send(lab.Client.Request("send","dispatch","fdl:a",text:"Please confirm"));
            lab.Select(TestChatRole.Driver);string note=lab.Client.PendingDispatch.Keys.Single();lab.Send(lab.Client.Request("send","dispatch","fdl:a",signal:"understood",replyTo:note));
            check(lab.Client.PendingDispatch.Count==0&&lab.Client==driver,"TF acknowledgment or role history lost");
            var next=lab.Client.Request("send","dispatch","fdl:a",text:"May I proceed now?");next.ToDispatcher=true;lab.Send(next);lab.Select(TestChatRole.Dispatcher);check(lab.Client.PendingDispatch.Count==1&&lab.Records.Count==5,"Conversation was not repeatable");
        });
        test("local role test copies the world and keeps synthetic messages separate",()=>{
            var source=Source();var lab=new RoleTestSession(source,"a");lab.View.Trains["a"].Label="Changed locally";lab.Send(lab.Client.Request("send","global",text:"Local message"));
            check(source.Trains["a"].Label=="TuT 10"&&source.People.Count==1&&!lab.View.People.ContainsKey("real-player"),"Test changed/copied live player identities");
            check(lab.Records.Single().RailroadId=="local-role-test"&&lab.Records.Single().SenderId.StartsWith("test:"),"Test record resembled a live identity/save");
        });
        test("local role message clocks and crew delivery survive switching views",()=>{
            var lab=new RoleTestSession(Source(),"a");lab.MessageClock=()=>42;var request=lab.Client.Request("send","crew","A");request.Parts=new MessageParts{Id="crew_take_task",Origin="Bryson",Task="shunt"};lab.Send(request);
            lab.Select(TestChatRole.Dispatcher);check(lab.Client.Rows("crew","A").Single().Received==42,"Local role messages faded as if received at startup");
            var empty=new RoleTestSession(new WorldView());check(empty.Client.State.Train!=null,"Empty-world test has no usable fixture train");
        });
    }
}
