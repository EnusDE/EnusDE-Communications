using System;
using EnusDE.Communications;
static class ActionMenuTests
{
    static MenuContext State(string train="alpha",string token="session",long version=1)=>new MenuContext{Train=train,Token=token,Version=version};
    static readonly string[] Items={"stop","forward","reverse","slow","distance","whistle","switch","understood"};
    public static void Run(Action<string,Action> test,Action<bool,string> check)
    {
        test("physical F10 edges survive missing events and repeated map activation",()=>{
            var edges=new ButtonEdges();edges.Sample(1,false,false,false,true,false);edges.Sample(2,true,false,false,true,false);check(edges.Pressed&&edges.Held&&edges.Ready,"Physical press lost without action event");
            edges.Sample(3,true,true,false,true,false);check(!edges.Pressed,"Held key repeated after action activation");edges.Sample(4,false,false,false,true,false);check(edges.Released&&!edges.Held,"Release lost without action event");
            edges.Sample(5,false,true,true,true,false);check(edges.Pressed&&edges.Released,"Very short tap lost between polls");edges.Sample(5,false,false,false,true,false);check(edges.Pressed,"Second reader consumed a frame's press");
        });
        test("focus return and rebinding cannot send an old held selection",()=>{
            var edges=new ButtonEdges();edges.Sample(1,false,false,false,true,false);edges.Sample(2,true,true,false,false,false);edges.Sample(3,true,true,false,true,false);check(!edges.Pressed&&!edges.Ready,"Focus return opened the menu");
            edges.Sample(4,false,false,true,true,false);edges.Sample(5,true,true,false,true,true);check(!edges.Pressed&&!edges.Ready,"Rebinding opened the menu");edges.Sample(6,true,true,false,true,false);check(!edges.Pressed,"Held rebound key opened a frame later");
            edges.Sample(7,false,false,true,true,false);edges.Sample(8,true,false,false,true,false);check(edges.Pressed&&edges.Ready,"Fresh press after rebinding failed");
        });
        test("held wheel stays open and sends the highlighted signal once on release",()=>{
            var wheel=new HoldWheelController();check(wheel.Open(State(),Items,true),"Wheel did not open");wheel.Hover(1);string key;
            check(wheel.Tick(State(),true,false,true,out key)==MenuResult.None&&wheel.Active&&key==null,"Holding sent a signal");
            check(wheel.Tick(State(),false,true,true,out key)==MenuResult.Selected&&key=="forward"&&!wheel.Active,"Release did not send and close");
            check(wheel.Tick(State(),false,true,true,out key)==MenuResult.None&&key==null,"Release sent twice");
        });
        test("wheel center, outside, lost input and explicit cancel close without a message",()=>{
            var wheel=new HoldWheelController();string key;
            foreach(int index in new[]{-1,99}){wheel.Open(State(),Items,true);wheel.Hover(index);check(wheel.Tick(State(),false,true,true,out key)==MenuResult.Closed&&key==null,"Unselected release sent");}
            wheel.Open(State(),Items,true);wheel.Hover(0);check(wheel.Tick(State(),false,true,false,out key)==MenuResult.Closed&&key==null,"Device reset sent the selected signal");
            wheel.Open(State(),Items,true);wheel.Hover(0);wheel.Close();check(wheel.Tick(State(),false,true,true,out key)==MenuResult.None,"Cancelled choice sent");
        });
        test("wheel refuses a changed train, session or permission version",()=>{
            foreach(var changed in new[]{State("bravo"),State(token:"new"),State(version:2)})
            {var wheel=new HoldWheelController();wheel.Open(State(),Items,true);wheel.Hover(0);string key;check(wheel.Tick(changed,false,true,true,out key)==MenuResult.ContextChanged&&key==null,"Selection survived a route change");}
            check(!new HoldWheelController().Open(State(train:null),Items,true),"Wheel opened without enrollment");
        });
        test("mouse fallback clicks send once while held-mode clicks wait for release",()=>{
            var wheel=new HoldWheelController();wheel.Open(State(),Items,false);string key;
            check(wheel.Click(State(),5,wheel.Generation,out key)==MenuResult.Selected&&key=="whistle"&&!wheel.Active,"Mouse-only fallback failed");
            wheel.Open(State(),Items,true);check(wheel.Click(State(),6,wheel.Generation,out key)==MenuResult.None&&wheel.Active,"Held-mode click sent early");
            check(wheel.Tick(State(),false,true,true,out key)==MenuResult.Selected&&key=="switch","Held-mode selection lost");
        });
        test("radial positions match the original eight choices and old callbacks stay invalid",()=>{
            check(HoldWheelController.Sector(0,130)==0&&HoldWheelController.Sector(130,0)==2&&HoldWheelController.Sector(0,-130)==4&&HoldWheelController.Sector(-130,0)==6,"Cardinal layout changed");
            check(HoldWheelController.Sector(0,0)==-1&&HoldWheelController.Sector(200,0)==-1&&HoldWheelController.Sector(Double.NaN,0)==-1,"Abort zones do not reject invalid positions");
            var wheel=new HoldWheelController();wheel.Open(State(),Items,false);int old=wheel.Generation;wheel.Close();wheel.Open(State("bravo"),Items,false);string key;
            check(wheel.Click(State("bravo"),0,old,out key)==MenuResult.None&&key==null,"Old button clicked a new train");
        });
    }
}
