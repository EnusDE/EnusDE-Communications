// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace EnusDE.Communications
{
    public sealed class MessageParts
    {
        public string Id,TrainId,TrainLabel,Origin,Destination,Task,Detail;
        public bool ToDispatcher;
    }
    public sealed class MessageTemplate
    {
        public readonly string Id,English,Channels;
        public readonly bool Origin,Destination,DispatcherOnly,NeedsTrain,NeedsTask;
        internal MessageTemplate(string id,string english,bool origin=false,bool destination=false,bool dispatcher=false,string channels="train,dispatch",bool needsTrain=true,bool task=false)
        {Id=id;English=english;Origin=origin;Destination=destination;DispatcherOnly=dispatcher;Channels=channels;NeedsTrain=needsTrain;NeedsTask=task;}
        public bool Allows(string channel)=>Channels.Split(',').Contains(channel,StringComparer.Ordinal);
    }
    public static class MessageTemplates
    {
        public static readonly MessageTemplate[] All={
            new MessageTemplate("general_hello","Hello everyone!",channels:"global",needsTrain:false),
            new MessageTemplate("general_help","I need help.",channels:"global",needsTrain:false),
            new MessageTemplate("general_help_at","I need help at {1}.",true,channels:"global",needsTrain:false),
            new MessageTemplate("general_available","I am available to help.",channels:"global",needsTrain:false),
            new MessageTemplate("general_thanks","Thank you for your help!",channels:"global",needsTrain:false),
            new MessageTemplate("crew_help","Crew assistance is needed at {1}.",true,channels:"crew",needsTrain:false),
            new MessageTemplate("crew_available","I can take on a task at {1}.",true,channels:"crew",needsTrain:false),
            new MessageTemplate("crew_take_task","I will handle {3} at {1}.",true,channels:"crew",needsTrain:false,task:true),
            new MessageTemplate("crew_assign","Locomotive {0}: please handle {3} at {1}.",true,channels:"crew",task:true),
            new MessageTemplate("movement_request","Locomotive {0} requests permission to travel from {1} to {2}.",true,true),
            new MessageTemplate("ready","Locomotive {0} is ready to depart from {1} towards {2}.",true,true),
            new MessageTemplate("arrived","Locomotive {0} has arrived at {1}.",true),
            new MessageTemplate("shunting_request","Locomotive {0} requests permission to shunt at {1}.",true),
            new MessageTemplate("shunting_done","Locomotive {0} has completed shunting at {1}.",true),
            new MessageTemplate("understood","Locomotive {0}: understood."),
            new MessageTemplate("hold","Locomotive {0}: please wait at {1}.",true,false,true),
            new MessageTemplate("proceed","Locomotive {0} may proceed from {1} to {2}.",true,true,true)
        };
        public static readonly Dictionary<string,string> Tasks=new Dictionary<string,string>(StringComparer.Ordinal){["pickup"]="collecting cars",["deliver"]="delivering cars",["assemble"]="assembling the train",["shunt"]="shunting",["custom"]=""};
        public static MessageTemplate Find(string id)=>All.FirstOrDefault(t=>String.Equals(t.Id,id,StringComparison.Ordinal));
        public static bool ValidPlace(string text)=>!String.IsNullOrWhiteSpace(text)&&text.Length<=80&&!text.Any(c=>Char.IsControl(c)||c=='\u2028'||c=='\u2029');
        public static bool TryNormalize(MessageParts input,WorkingTrain train,bool dispatcher,string channel,out MessageParts result)
        {
            result=null;var type=Find(input?.Id);
            if(type==null||!type.Allows(channel)||type.NeedsTrain&&(train==null||input.TrainId!=train.Id)||type.DispatcherOnly&&(!dispatcher||channel!="dispatch"||input.ToDispatcher))return false;
            if(type.Origin&&!ValidPlace(input.Origin)||type.Destination&&!ValidPlace(input.Destination))return false;
            if(type.NeedsTask&&(input.Task==null||!Tasks.ContainsKey(input.Task)||input.Task=="custom"&&!ValidPlace(input.Detail)))return false;
            string label=type.NeedsTrain?(String.IsNullOrEmpty(train.Label)?train.Id:train.Label):"";
            label=new String(label.Select(c=>Char.IsControl(c)?' ':c).Take(160).ToArray());
            result=new MessageParts{Id=type.Id,TrainId=type.NeedsTrain?train.Id:null,TrainLabel=type.NeedsTrain?label:null,Origin=type.Origin?input.Origin.Trim():null,Destination=type.Destination?input.Destination.Trim():null,ToDispatcher=input.ToDispatcher,Task=type.NeedsTask?input.Task:null,Detail=type.NeedsTask&&input.Task=="custom"?input.Detail.Trim():null};return true;
        }
        public static string Render(MessageParts parts,Func<string,string> localize=null)
        {
            var type=Find(parts?.Id);if(type==null)return null;
            string key="template.text."+type.Id,format=localize?.Invoke(key);
            if(String.IsNullOrEmpty(format)||format==key)format=type.English;
            string task=parts.Detail??"";
            if(parts.Task!=null&&parts.Task!="custom"&&Tasks.TryGetValue(parts.Task,out var english)){string taskKey="template.task."+parts.Task;task=localize?.Invoke(taskKey);if(String.IsNullOrEmpty(task)||task==taskKey)task=english;}
            try{return String.Format(CultureInfo.InvariantCulture,format,parts.TrainLabel??parts.TrainId,parts.Origin,parts.Destination,task);}
            catch(FormatException){return String.Format(CultureInfo.InvariantCulture,type.English,parts.TrainLabel??parts.TrainId,parts.Origin,parts.Destination,task);}
        }
        public static string[] Places(IEnumerable<string> source,string query="")=>source.Where(ValidPlace).Select(s=>s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(s=>String.IsNullOrWhiteSpace(query)||s.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0).OrderBy(s=>s,StringComparer.OrdinalIgnoreCase).Take(500).ToArray();
    }
}
