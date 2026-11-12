// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
namespace EnusDE.Communications
{
    public static class ChatPresentation
    {
        public static double Advance(double clock,double elapsed,bool focused,bool typing)=>focused&&!typing?clock+Math.Max(0,Math.Min(.5,elapsed)):clock;
        public static float Opacity(double received,double clock,float seconds,bool typing)=>typing||seconds==0?1:(float)Math.Max(0,Math.Min(1,seconds-(clock-received)));
        public static int SortingOrder(bool menuOpen,int menuOrder)=>menuOpen?Math.Min(69,menuOrder-1):70;
    }
    public enum ChatItemKind { Message,Conversation,Reply }
    public sealed class ChatItem { public VisibleRecord Row;public ChatItemKind Kind; }
    public static class ChatTimeline
    {
        public static ChatItem[] Compose(ClientSession client,string channel,string target,bool reading,string localId)
        {
            var normal=client.Rows(channel,target);var rows=reading?normal:normal.Skip(Math.Max(0,normal.Count-5));
            return rows.Select(r=>new ChatItem{Row=r,Kind=ChatItemKind.Message})
                .Concat(client.DispatchActivity(localId).Select(r=>new ChatItem{Row=r,Kind=ChatItemKind.Conversation}))
                .Concat(client.StructuredDispatchActivity(localId).Select(r=>new ChatItem{Row=r,Kind=ChatItemKind.Message}))
                .Concat(client.PendingDispatch.Values.Select(r=>new ChatItem{Row=r,Kind=ChatItemKind.Reply}))
                .OrderBy(r=>r.Row.Message.Utc).ThenBy(r=>r.Row.Received).ToArray();
        }
        public static float BottomPadding(float viewportHeight,float rowsHeight)=>Math.Max(0,viewportHeight-rowsHeight);
    }
    public sealed class TrainPage
    {
        public readonly WorkingTrain[] Items;
        public readonly int Index,Pages,Total;
        public bool Previous=>Index>0;
        public bool Next=>Index+1<Pages;
        public TrainPage(IEnumerable<WorkingTrain> trains,string query,int page,int size=4)
        {
            if(size<1)throw new ArgumentOutOfRangeException(nameof(size));
            var filtered=trains.Where(t=>String.IsNullOrWhiteSpace(query)||(t.Label??t.Id).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)
                .OrderBy(t=>t.Label??t.Id,StringComparer.OrdinalIgnoreCase).ThenBy(t=>t.Id,StringComparer.Ordinal).ToArray();
            Total=filtered.Length;Pages=Math.Max(1,(Total+size-1)/size);Index=Math.Max(0,Math.Min(Pages-1,page));Items=filtered.Skip(Index*size).Take(size).ToArray();
        }
    }
}
