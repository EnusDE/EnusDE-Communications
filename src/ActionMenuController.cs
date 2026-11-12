// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Collections.Generic;
namespace EnusDE.Communications
{
    public sealed class MenuContext
    {
        public string Train,Token;public long Version;
        public bool Valid=>!String.IsNullOrEmpty(Train)&&!String.IsNullOrEmpty(Token);
        public bool Matches(MenuContext other)=>other!=null&&Valid&&other.Valid&&Train==other.Train&&Token==other.Token&&Version==other.Version;
    }
    public enum MenuResult {None,Selected,Closed,ContextChanged}
    public sealed class HoldWheelController
    {
        MenuContext context;string[] choices;
        public bool Active {get;private set;}
        public bool HoldToSelect {get;private set;}
        public int Selected {get;private set;}=-1;
        public int Generation {get;private set;}
        public bool Open(MenuContext current,string[] items,bool hold)
        {
            Close();if(current==null||!current.Valid||items==null||items.Length==0)return false;
            context=new MenuContext{Train=current.Train,Token=current.Token,Version=current.Version};choices=(string[])items.Clone();HoldToSelect=hold;Active=true;return true;
        }
        public void Hover(int index){Selected=Active&&index>=0&&index<choices.Length?index:-1;}
        public MenuResult Tick(MenuContext current,bool held,bool released,bool inputReady,out string selection)
        {
            selection=null;if(!Active)return MenuResult.None;
            if(!context.Matches(current)){Close();return MenuResult.ContextChanged;}
            if(!HoldToSelect)return MenuResult.None;
            if(!inputReady){Close();return MenuResult.Closed;}
            if(released)return Finish(out selection);
            if(!held){Close();return MenuResult.Closed;}return MenuResult.None;
        }
        public MenuResult Click(MenuContext current,int index,int generation,out string selection)
        {
            selection=null;if(!Active||generation!=Generation)return MenuResult.None;
            if(!context.Matches(current)){Close();return MenuResult.ContextChanged;}
            Hover(index);return HoldToSelect?MenuResult.None:Finish(out selection);
        }
        MenuResult Finish(out string selection){selection=Selected>=0?choices[Selected]:null;Close();return selection==null?MenuResult.Closed:MenuResult.Selected;}
        public void Close(){Generation++;Active=false;Selected=-1;choices=null;context=null;}
        public static int Sector(double x,double y)
        {if(Double.IsNaN(x)||Double.IsNaN(y))return -1;double radius=Math.Sqrt(x*x+y*y);return radius<55||radius>180?-1:((int)Math.Floor((Math.Atan2(x,y)*180/Math.PI+22.5+360)%360/45))%8;}
    }
    public sealed class ButtonEdges
    {
        bool initialized,down,wasFocused,suppressed;int sampledFrame=Int32.MinValue;
        public bool Pressed {get;private set;}public bool Released {get;private set;}
        public bool Held=>down;
        public bool Ready {get;private set;}
        public void Sample(int frame,bool held,bool pressEvent,bool releaseEvent,bool focused,bool bindingChanged)
        {
            if(frame==sampledFrame)return;sampledFrame=frame;Pressed=Released=false;
            bool reset=bindingChanged||!focused||initialized&&!wasFocused;
            if(reset)suppressed=held;if(!held)suppressed=false;
            Ready=focused&&!reset&&!suppressed;
            if(focused&&!reset&&!suppressed)
            {Pressed=initialized?held&&!down||!held&&pressEvent:pressEvent;Released=initialized&&(down&&!held||releaseEvent);}
            down=held;wasFocused=focused;initialized=true;
        }
    }
}
