// Test-only boundary: actual NativeSettings and SharedModsTab are compiled into these tests.
using System;
using System.Collections.Generic;
using System.Reflection;
using UI.Builder;
namespace HarmonyLib
{
    public sealed class HarmonyMethod {public HarmonyMethod(MethodInfo method){}}
    public sealed class Harmony
    {
        public static int Patches;public Harmony(string id){}
        public void Patch(MethodInfo method,HarmonyMethod postfix){Patches++;}
        public void UnpatchAll(string id){Patches--;}
    }
}
namespace UI.Builder
{
    public struct UITabbedPanelBuilder
    {
        public BuildState State;
        public UITabbedPanelBuilder AddTab(string title,string id,Action<UIPanelBuilder> content)
        {if(id==EnusDE.Communications.SharedModsTab.TabId){State.ModsTabs++;content(new UIPanelBuilder{State=State});}return this;}
    }
}
namespace UI.PreferencesWindow
{
    internal static class PreferencesBuilder
    {
        static readonly UIState<string> SelectedTab=new UIState<string>("gfx");
        static void BuildTabs(UITabbedPanelBuilder b){}
        internal static string Selection=>SelectedTab.Value;
    }
    public sealed class PreferencesWindow
    {
        public readonly UI.Windows.Window Window=new UI.Windows.Window();
        public T GetComponent<T>() where T:class=>Window as T;
        public void Show()
        {
            Window.IsShown=true;var manager=UI.Windows.WindowManager.Shared;manager.LastState=new BuildState();
            typeof(EnusDE.Communications.SharedModsTab).GetMethod("BuildTabsPostfix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{new UITabbedPanelBuilder{State=manager.LastState}});
        }
    }
}
namespace UI.Windows
{
    public sealed class Window
    {
        public enum Position { LowerLeft }
        public bool IsShown;
        public void CloseWindow(){IsShown=false;}
    }
    public sealed class WindowConfig
    {
        public static WindowConfig Fixed(UnityEngine.Vector2Int size,Window.Position position)=>new WindowConfig();
    }
    public sealed class CustomWindowDefinition
    {
        public string Identifier,Title;
        public WindowConfig Config;
        public Action<UIPanelBuilder> BuilderAction;
    }
    public sealed class WindowManager
    {
        public static WindowManager Shared=new WindowManager();public BuildState LastState;
        public BuildState LastCustomState;
        public readonly Dictionary<string,CustomWindowDefinition> CustomDefinitions=new Dictionary<string,CustomWindowDefinition>();
        public readonly HashSet<string> OpenCustom=new HashSet<string>();
        public readonly UI.PreferencesWindow.PreferencesWindow Preferences=new UI.PreferencesWindow.PreferencesWindow();
        public T GetWindow<T>() where T:class=>Preferences as T;
        public void RegisterCustomWindow(CustomWindowDefinition definition)=>CustomDefinitions.Add(definition.Identifier,definition);
    }
    public sealed class CustomWindowHandle
    {
        readonly string id;
        CustomWindowHandle(string id){this.id=id;}
        public static bool Exists(string id)=>WindowManager.Shared.CustomDefinitions.ContainsKey(id);
        public static CustomWindowHandle Create(string id)=>new CustomWindowHandle(id);
        public bool IsOpen()=>WindowManager.Shared.OpenCustom.Contains(id);
        public void Close()=>WindowManager.Shared.OpenCustom.Remove(id);
        public void Show()
        {
            var manager=WindowManager.Shared;
            manager.OpenCustom.Add(id);
            manager.LastCustomState=new BuildState();
            manager.CustomDefinitions[id].BuilderAction(new UIPanelBuilder{State=manager.LastCustomState});
        }
        public void SetTitle(string title){}
    }
}
namespace EnusDE.Communications
{
    internal static class GameMenus
    {
        internal static bool IsOpen=>UI.Windows.WindowManager.Shared.Preferences.Window.IsShown||UI.Windows.WindowManager.Shared.OpenCustom.Count!=0;
    }
}
