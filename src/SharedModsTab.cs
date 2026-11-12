// Copyright (c) 2026 EnusDE. MIT License.
// Uses the existing in-process Mods tab protocol, also used by the translation mod.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UI.Builder;
namespace EnusDE.Communications
{
    internal static class SharedModsTab
    {
        internal const string TabId="ashenmare.mods";
        const string SlotKey="ashenmare.preferences-mods.v1",PatchId="enusde.communications.preferences";
        static Action<UIPanelBuilder> mine;static Action<string> warning;static Harmony harmony;static bool reported;
        static List<Action<UIPanelBuilder>> Slot(bool create)
        {
            var domain=AppDomain.CurrentDomain;
            lock(domain)
            {
                var existing=domain.GetData(SlotKey);var result=existing as List<Action<UIPanelBuilder>>;
                if(existing!=null&&result==null)throw new InvalidOperationException("Incompatible Mods tab registry.");
                if(result==null&&create){result=new List<Action<UIPanelBuilder>>();domain.SetData(SlotKey,result);}return result;
            }
        }
        internal static void Register(Action<UIPanelBuilder> draw,Action<string> log)
        {
            if(draw==null)throw new ArgumentNullException(nameof(draw));Remove();warning=log;reported=false;
            var type=typeof(UIPanelBuilder).Assembly.GetType("UI.PreferencesWindow.PreferencesBuilder",true);
            var target=type.GetMethod("BuildTabs",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance,null,new[]{typeof(UITabbedPanelBuilder)},null);
            if(target==null)throw new MissingMethodException(type.FullName,"BuildTabs");
            var slot=Slot(true);mine=draw;lock(slot)slot.Add(draw);
            try{harmony=new Harmony(PatchId);harmony.Patch(target,postfix:new HarmonyMethod(typeof(SharedModsTab).GetMethod(nameof(BuildTabsPostfix),BindingFlags.NonPublic|BindingFlags.Static)));}
            catch{Remove();throw;}
        }
        internal static void Remove()
        {
            var slot=Slot(false);if(slot!=null&&mine!=null)lock(slot)slot.Remove(mine);mine=null;
            harmony?.UnpatchAll(PatchId);harmony=null;warning=null;
        }
        static void BuildTabsPostfix(UITabbedPanelBuilder __0)
        {
            try{var slot=Slot(false);if(mine==null||slot==null)return;lock(slot)if(slot.Count==0||slot[0]!=mine)return;__0.AddTab("Mods",TabId,DrawTab);}
            catch(Exception error){Report(error);}
        }
        static void DrawTab(UIPanelBuilder builder)
        {
            builder.VScrollView(content=>{
                var slot=Slot(false);if(slot==null)return;Action<UIPanelBuilder>[] contributors;lock(slot)contributors=slot.ToArray();
                foreach(var draw in contributors)try{draw(content);}catch(Exception error){Report(error);}
            });
        }
        static void Report(Exception error){if(reported)return;reported=true;warning?.Invoke("Could not display mod settings: "+error.Message);}
    }
}
