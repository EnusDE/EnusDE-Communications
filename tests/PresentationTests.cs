using System;
using EnusDE.Communications;
static class PresentationTests
{
    internal static void Run(Action<string,Action> test,Action<bool,string> check)
    {
        test("messages expire independently and only active chat typing restores faded text",()=>{
            check(ChatPresentation.Opacity(0,11,10,false)==0&&ChatPresentation.Opacity(7,11,10,false)==1,"Message ages are not independent");
            check(ChatPresentation.Opacity(0,9.5,10,false)==.5f,"Last-second fade changed");
            check(ChatPresentation.Opacity(0,11,10,true)==1,"Typing must reveal history");
            check(ChatPresentation.Opacity(0,11,10,false)==0,"History remained visible after leaving input");
        });
        test("chat fade continues behind menus and mouse hover has no reveal or timer effect",()=>{
            double clock=0;for(int frame=0;frame<24;frame++)clock=ChatPresentation.Advance(clock,.5,true,false);
            check(clock==12&&ChatPresentation.Opacity(0,clock,10,false)==0,"Idle chat did not expire while reading a menu or moving the mouse");
        });
        test("typing pauses fade age while unlimited messages stay visible",()=>{
            check(ChatPresentation.Advance(10,.5,true,true)==10,"Typing advanced fade clock");
            check(ChatPresentation.Advance(10,.5,false,false)==10,"Unfocused application advanced fade clock");
            check(ChatPresentation.Advance(10,-1,true,false)==10,"Clock moved backwards");
            check(ChatPresentation.Opacity(0,10000,0,false)==1,"Unlimited display expired");
        });
        test("visible chat sorts behind native menus and restores its normal layer after closing",()=>{
            foreach(int native in new[]{-10,0,50,100})check(ChatPresentation.SortingOrder(true,native)<native,"Chat covers native menu layer "+native);
            check(ChatPresentation.SortingOrder(false,0)==70,"Normal chat layer not restored");
        });
    }
}
