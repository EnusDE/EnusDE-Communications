// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Game;
using Game.State;
using Model;
using Network;
using Network.Client;
namespace EnusDE.Communications
{
    internal static class GameAdapter
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static readonly FieldInfo Clients=typeof(HostManager).GetField("_clients",Flags);
        static readonly Type ClientType=typeof(HostManager).GetNestedType("Client",BindingFlags.Public|BindingFlags.NonPublic);
        static readonly FieldInfo Status=ClientType?.GetField("Status",Flags);
        static readonly PropertyInfo ClientId=ClientType?.GetProperty("PlayerId",Flags);
        static readonly MethodInfo Rank=typeof(HostManager).GetMethod("AccessLevelForPlayerId",Flags);
        static readonly FieldInfo NativeClient=typeof(ClientManager).GetField("_client",Flags);
        static readonly PropertyInfo Connection=typeof(GameClient).GetProperty("ConnectionInfo",Flags);
        static readonly FieldInfo PlayerObject=typeof(PlayerPropertiesManager).GetField("_object",Flags);
        static string identitySource,railroadId;
        internal static void Clear(){identitySource=null;railroadId=null;}
        public static string LocalId => PlayersManager.PlayerId.String;
        public static bool Ready => StateManager.Shared!=null&&StateManager.Shared.IsSessionReady&&!StateManager.IsUnloading;
        public static bool IsHost => Ready&&StateManager.IsHost;
        public static bool MultiplayerActive => Ready&&Multiplayer.Mode!=ConnectionMode.Singleplayer;
        public static WorldView Snapshot()
        {
            var result=new WorldView();if(!Ready)return result;
            string railroad=StateManager.Shared.SaveManager?.RailroadPath;
            if(String.IsNullOrEmpty(railroad))railroad=StateManager.Shared.CurrentMapIdentifier+"|"+StateManager.Shared.RailroadMark+"|"+StateManager.Shared.RailroadName;
            if(railroadId==null||identitySource!=railroad){using(var hash=SHA256.Create())railroadId=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(railroad))).Replace("-","");identitySource=railroad;}
            result.Id=railroadId;
            var manager=StateManager.Shared.PlayersManager;if(manager==null)return result;
            HashSet<string> active=null;
            if(IsHost&&HostManager.Shared!=null&&Clients!=null)
            {
                active=new HashSet<string>(StringComparer.Ordinal){LocalId};
                var entries=Clients.GetValue(HostManager.Shared) as IDictionary;
                if(entries!=null)foreach(DictionaryEntry pair in entries)
                {
                    if(Status==null||ClientId==null)throw new NotSupportedException("Host roster changed.");
                    if(Status.GetValue(pair.Value).ToString()=="Active")active.Add(((PlayerId)ClientId.GetValue(pair.Value)).String);
                }
            }
            foreach(var crew in manager.TrainCrews)result.Crews[crew.Id]=crew.Name;
            foreach(var player in manager.AllPlayers)
            {
                string id=player.PlayerId.String;if(String.IsNullOrEmpty(id)||active!=null&&!active.Contains(id))continue;
                Game.AccessControl.AccessLevel level;
                if(IsHost&&HostManager.Shared!=null&&Rank!=null)level=(Game.AccessControl.AccessLevel)Rank.Invoke(HostManager.Shared,new object[]{player.PlayerId});
                else if(!manager.TryGetAccessLevel(player.PlayerId,out level))level=id==LocalId?StateManager.AccessLevel:Game.AccessControl.AccessLevel.Undetermined;
                if(level==Game.AccessControl.AccessLevel.Banned)continue;
                var pos=player.GamePosition;
                string selected=null;
                try
                {
                    var properties=PlayerPropertiesManager.Shared;
                    if(properties!=null&&PlayerObject!=null)
                    {var values=PlayerObject.GetValue(properties) as KeyValue.Runtime.KeyValueObject;if(values!=null){var selectedValue=values[id]["selectedCarId"];if(!selectedValue.IsNull)selected=selectedValue.StringValue;}}
                }
                catch{selected=null;} // Presence information is optional; chat must remain available.
                result.People[id]=new Person{Id=id,Name=player.Name,Crew=manager.TrainCrewIdFor(player.PlayerId),Rank=level.ToString(),Host=IsHost&&id==LocalId,X=pos.x,Y=pos.y,Z=pos.z,SelectedCar=selected};
            }
            var trains=TrainController.Shared;
            if(trains!=null)foreach(var car in trains.Cars)
                if(car!=null&&car.IsLocomotive&&!String.IsNullOrEmpty(car.id))result.Trains[car.id]=new WorkingTrain{Id=car.id,Label=car.DisplayName??car.id,Crew=car.trainCrewId};
            return result;
        }
        public static string Candidate()
        {
            var car=TrainController.Shared?.SelectedLocomotive;
            return car!=null?car.id:null;
        }
        // Only read when opening the message menu; no additional recurring world scans.
        internal static string[] MessagePlaces()
        {
            var timetable=Model.Ops.Timetable.TimetableController.Shared;
            if(timetable==null||timetable.branches==null)return Array.Empty<string>();
            return MessageTemplates.Places(timetable.branches.Where(b=>b!=null&&b.stations!=null).SelectMany(b=>b.stations).Where(s=>s!=null).Select(s=>s.name));
        }
        public static ulong HostSteamId()
        {
            if(IsHost){ulong id;return UInt64.TryParse(LocalId,out id)?id:0;}
            if(Multiplayer.Client==null||NativeClient==null||Connection==null)return 0;
            var client=NativeClient.GetValue(Multiplayer.Client);if(client==null)return 0;
            return ((ConnectionInfo)Connection.GetValue(client)).GameServerId.m_SteamID;
        }
    }
}
