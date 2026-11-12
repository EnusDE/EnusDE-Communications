using System;
using System.Collections.Generic;
using System.Linq;
namespace EnusDE.Communications
{
    public static class ChatVisibility
    {
        public static bool IsVisible(bool multiplayer,bool singleplayerChoice)=>multiplayer||singleplayerChoice;
    }
    // Native console notices work without installing a client DLL. No chat payload is bridged.
    public sealed class ConsoleNoticeQueue
    {
        readonly Dictionary<string,double> pending=new Dictionary<string,double>();
        readonly Dictionary<string,double> nextNotice=new Dictionary<string,double>();
        public void Enqueue(string player,double now)
        {
            if(String.IsNullOrEmpty(player)||pending.ContainsKey(player))return;
            double next;if(nextNotice.TryGetValue(player,out next)&&now<next)return;
            pending[player]=now+2; // Give a newly joined compatible client time to introduce itself.
        }
        public void Flush(double now,Func<string,bool> online,Func<string,bool> compatible,Action<string> send)
        {
            foreach(var id in pending.Where(p=>p.Value<=now).Select(p=>p.Key).ToArray())
            {
                pending.Remove(id);if(!online(id)||compatible(id))continue;
                nextNotice[id]=now+30;send(id);
            }
            foreach(var id in nextNotice.Keys.Where(id=>!online(id)).ToArray())nextNotice.Remove(id);
        }
        public void Clear(){pending.Clear();nextNotice.Clear();}
    }
}
