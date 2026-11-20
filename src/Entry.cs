// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Railroader.Modding;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace EnusDE.Communications
{
    public sealed class Entry:IModEntryPoint,IWorldLifecycle
    {
        const string PatchId="enusde.communications";
        static Entry active;
        static readonly FieldInfo FocusPause=AccessTools.Field(typeof(UI.GameInput),"_focusPause");
        internal ModSettings Settings;internal Localization Text;internal IModLogger Log;internal string DataDirectory;internal HostArchive Archive;
        internal readonly PerformanceTrace Performance=new PerformanceTrace();
        internal CommsRuntime TestRuntime;GameObject testRoot;
        internal CommsRuntime ActiveRuntime=>TestRuntime??Runtime;
        internal bool RoleTestActive=>TestRuntime!=null;
        internal bool PerformanceRecording=>Performance.Recording;
        internal bool CanDiagnosticPause=>!RoleTestActive&&Runtime!=null&&GameAdapter.MultiplayerActive;
        GameObject maintenance;
        internal InputAction Wheel,OpenChat,VoiceTalk;InputActionMap map;CommsBindings bindings;Harmony harmony;GameObject root;internal CommsRuntime Runtime;NativeSettings nativeSettings;string settingsPath;
        internal bool BindingConflict=>!String.IsNullOrEmpty(Settings.ChatBinding)&&String.Equals(Settings.ChatBinding,Settings.WheelBinding,StringComparison.OrdinalIgnoreCase);
        internal bool SettingsOpen=>nativeSettings?.IsOpen==true;
        internal bool WheelPressed=>bindings?.Pressed(Wheel)==true;
        internal bool WheelReleased=>bindings?.Released(Wheel)==true;
        internal bool WheelHeld=>bindings?.Held(Wheel)==true;
        internal bool WheelInputReady=>bindings?.Ready(Wheel)==true;
        internal bool WheelFunctionKey=>bindings?.WheelFunctionKey==true;
        internal bool VoiceHeld=>bindings?.Held(VoiceTalk)==true;
        internal string[] VoiceMicrophones=>Microphone.devices;
        internal string VoiceStatus=>Runtime?.Voice?.Status??Text.Get("voice_unavailable");
        internal Person[] VoicePeople=>Runtime?.View.People.Values.Where(p=>p.Id!=GameAdapter.LocalId).ToArray()??Array.Empty<Person>();
        internal bool VoiceBindingConflict=>!String.IsNullOrEmpty(Settings.VoiceBinding)&&(String.Equals(Settings.VoiceBinding,Settings.WheelBinding,StringComparison.OrdinalIgnoreCase)||String.Equals(Settings.VoiceBinding,Settings.ChatBinding,StringComparison.OrdinalIgnoreCase));
        internal bool ChatPressed=>bindings?.Pressed(OpenChat)==true;
        internal void KeepBindingsEnabled(CommsRuntime source)
        {if(ActiveRuntime!=source)return;if(!SettingsOpen)bindings?.EnsureEnabled();bindings?.Sample(Time.frameCount,Application.isFocused&&!SettingsOpen);}
        public void OnModLoad(IModContext context)
        {
            if(typeof(Game.State.StateManager).Module.ModuleVersionId!=new Guid("aac29a8a-ef90-41c1-b17a-0d75169dc6b8"))throw new NotSupportedException("EnusDE Communications 0.6.8 requires the Railroader 0.6.3 build it was compiled against.");
            active=this;Log=context.Log;DataDirectory=Path.Combine(context.ModDirectory,"Data");Directory.CreateDirectory(DataDirectory);settingsPath=ModSettings.ForHome(Game.HomeDirectory.UserPaths.RailroaderDirectory);
            try{Settings=ModSettings.LoadUser(Game.HomeDirectory.UserPaths.RailroaderDirectory,context.ModDirectory);}catch(Exception error){Settings=new ModSettings();Log.Warning("Using default communication settings: "+error.Message);}
            harmony=new Harmony(PatchId);Text=new Localization(context.ModDirectory,harmony,message=>Log.Warning(message));
            bindings=new CommsBindings(Settings);map=bindings.Map;Wheel=bindings.Wheel;OpenChat=bindings.OpenChat;VoiceTalk=bindings.VoiceTalk;
            harmony.Patch(AccessTools.Method(typeof(UI.GameInput),"Update"),prefix:new HarmonyMethod(typeof(Entry),nameof(BeforeInput)),finalizer:new HarmonyMethod(typeof(Entry),nameof(AfterInput)));
            // Observe both native roles without changing packets, input or return values.
            foreach(string name in new[]{"Network.Steam.SteamClient","Network.Steam.SteamServer"})
            {
                var type=typeof(Game.State.StateManager).Assembly.GetType(name);var nativeUpdate=type==null?null:AccessTools.Method(type,"Update");
                if(nativeUpdate!=null)try{harmony.Patch(nativeUpdate,prefix:new HarmonyMethod(typeof(Entry),nameof(BeforeNativeNetwork)),finalizer:new HarmonyMethod(typeof(Entry),nameof(AfterNativeNetwork)));}catch(Exception error){Log.Warning("Optional native network timing unavailable for "+name+": "+error.Message);}
            }
            harmony.Patch(AccessTools.Method(typeof(UI.Common.EscapeHandler),"HandleEscape"),prefix:new HarmonyMethod(typeof(Entry),nameof(BeforeEscape)));
            harmony.Patch(AccessTools.Method(typeof(Game.State.StateManager),"HandleCharacterMessage",new[]{typeof(Game.Messages.ICharacterMessage),typeof(Game.IPlayer)}),postfix:new HarmonyMethod(typeof(Entry),nameof(AfterNativeConsole)));
            nativeSettings=new NativeSettings(this);nativeSettings.Register();
            Archive=new HostArchive(Path.Combine(DataDirectory,"HostArchive"));maintenance=new GameObject("EnusDE.Communications.ArchiveMaintenance");UnityEngine.Object.DontDestroyOnLoad(maintenance);maintenance.AddComponent<ArchiveMaintenance>().Initialize(this);
            Log.Info("EnusDE Communications 0.6.8 loaded. Settings: Preferences > Mods > EnusDE Communications. No separate menu dependency. User settings: "+settingsPath+". Optional multiplayer participation. Steam channel 189731.");
        }
        public void OnWorldLoad()
        {
            OnWorldUnload();Settings.VoiceEnabled=false;Settings.Host.VoiceAllowed=false;map.Enable();root=new GameObject("EnusDE.Communications.Runtime");UnityEngine.Object.DontDestroyOnLoad(root);
            Runtime=root.AddComponent<CommsRuntime>();try{Runtime.Begin(this);}catch(Exception error){Log.Error("Communications could not start: "+error);OnWorldUnload();}
        }
        public void OnWorldUnload()
        {StopRoleTest();Runtime?.Voice?.Dispose();if(Performance.Recording)ExportPerformance();map?.Disable();if(root!=null)UnityEngine.Object.Destroy(root);root=null;Runtime=null;NativeDock.Clear();GameAdapter.Clear();GameMenus.Clear();}
        public void OnModUnload()
        {OnWorldUnload();if(maintenance!=null)UnityEngine.Object.Destroy(maintenance);maintenance=null;nativeSettings?.Dispose();nativeSettings=null;harmony?.UnpatchAll(PatchId);harmony=null;bindings?.Dispose();bindings=null;map=null;if(active==this)active=null;}
        internal void Save(bool refresh=true)
        {try{Settings.Write(settingsPath);Runtime?.Voice?.SettingsChanged();if(refresh){Runtime?.UI?.ApplySettings();TestRuntime?.UI?.ApplySettings();}}catch(Exception error){Log.Warning("Communication settings could not be saved: "+error.Message);ActiveRuntime?.UI?.Toast(Text.Get("save_failed"));}}
        internal void SetChatBinding(string path)
        {bool enabled=map.enabled;map.Disable();OpenChat.ApplyBindingOverride(0,path);Settings.ChatBinding=path;if(enabled)map.Enable();Save();}
        static void BeforeInput(UI.GameInput __instance,out bool __state)
        {__state=false;bool captured=active?.ActiveRuntime?.UI?.Captures==true,paused=(captured||active?.Performance.Recording==true)&&FocusPause!=null&&(bool)FocusPause.GetValue(__instance);active?.Performance.Input(captured,paused);if(captured&&FocusPause!=null&&!paused){FocusPause.SetValue(__instance,true);__state=true;}}
        static Exception AfterInput(UI.GameInput __instance,bool __state,Exception __exception)
        {if(__state)FocusPause.SetValue(__instance,false);return __exception;}
        static void BeforeNativeNetwork(out PerformanceTrace.Measurement __state)
        {__state=active?.Performance.Measure(PerformanceStage.NativeNetwork)??default;}
        static Exception AfterNativeNetwork(PerformanceTrace.Measurement __state,Exception __exception)
        {__state.Dispose();return __exception;}
        static bool BeforeEscape(){if(active?.SettingsOpen==true)return true;return active?.ActiveRuntime?.UI?.HandleEscape()!=true;}
        static void AfterNativeConsole(Game.Messages.ICharacterMessage __0,Game.IPlayer __1)
        {
            if(__0 is Game.Messages.Say say&&__1!=null)
            {try{active?.Runtime?.ObserveNativeConsole(__1.PlayerId.String,say.text);}catch(Exception error){active?.Log.Warning("Console mod notice unavailable: "+error.Message);}}
        }
        internal void OpenSettings()
        {ActiveRuntime?.UI?.CancelWheel();nativeSettings?.Show();}
        internal void TogglePerformanceCapture()
        {if(Performance.Recording){ExportPerformance();return;}if(RoleTestActive){TestRuntime.UI.Toast(Text.Get("test_stop_before_measure"));return;}if(Runtime==null)return;Performance.Start();Log.Info("60-second chat performance capture started; buffered in memory.");Runtime.UI.Toast(Text.Get("performance_started"));}
        internal void StartRoleTest()
        {
            if(!GameAdapter.IsHost||Runtime==null||RoleTestActive)return;
            if(Runtime.DiagnosticSuspended){Runtime.UI.Toast(Text.Get("test_wait_pause"));return;}
            if(Performance.Recording)ExportPerformance();Runtime.UI.SuspendForRoleTest();Runtime.Voice?.Dispose();
            testRoot=new GameObject("EnusDE.Communications.LocalRoleTest");UnityEngine.Object.DontDestroyOnLoad(testRoot);TestRuntime=testRoot.AddComponent<CommsRuntime>();
            try{TestRuntime.BeginRoleTest(this,Runtime.View,Runtime.Client.State?.Train??GameAdapter.Candidate());nativeSettings.Close();}catch(Exception error){Log.Warning("Local role test could not start: "+error.Message);StopRoleTest();}
        }
        internal void SwitchTestRole(TestChatRole role){if(!GameAdapter.IsHost)return;TestRuntime?.SelectTestRole(role);}
        internal void StopRoleTest(){TestRuntime?.UI?.PrepareDiagnosticPause();if(testRoot!=null)UnityEngine.Object.Destroy(testRoot);testRoot=null;TestRuntime=null;Runtime?.UI?.Dirty();}
        internal void PauseForDiagnosis()
        {if(!CanDiagnosticPause)return;if(!Performance.Recording)TogglePerformanceCapture();Runtime.PauseForDiagnosis();nativeSettings.Close();}
        internal void ExportPerformance()
        {
            Performance.Stop();if(Performance.Count==0)return;
            try
            {
                string directory=Path.Combine(DataDirectory,"Diagnostics");Directory.CreateDirectory(directory);
                string path=Path.Combine(directory,"communications-performance-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".csv");
                using(var writer=new StreamWriter(path,false,new System.Text.UTF8Encoding(false)))Performance.Write(writer);
                Log.Info("Chat performance capture saved ("+Performance.Count+" frames): "+path);Runtime?.UI?.Toast(Text.Get("performance_saved"));
            }
            catch(Exception error){Log.Warning("Chat performance capture could not be saved: "+error.Message);}
        }
    }
    public sealed class ArchiveMaintenance:MonoBehaviour
    {
        Entry entry;float next;bool reported;
        internal void Initialize(Entry owner){entry=owner;}
        void Update()
        {
            if(entry==null||entry.Runtime?.DiagnosticSuspended==true||Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+.5f;
            try{using(entry.Performance.Measure(PerformanceStage.Archive))entry.Archive.Purge();reported=false;}catch(Exception error){if(!reported){entry.Log.Warning("Archive cleanup failed: "+error.Message);reported=true;}}
        }
    }
}
