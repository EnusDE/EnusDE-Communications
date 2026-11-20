using System;
using System.Linq;
using EnusDE.Communications;
using UI.Builder;
static class SettingsUiTests
{
    static void Check(bool condition,string reason){if(!condition)throw new Exception(reason);}
    static BuildState Draw(Entry entry,bool host)
    {
        GameAdapter.IsHost=host;
        using(var settings=new NativeSettings(entry))
        {
            entry.OpenSettingsCallback=settings.Show;settings.Register();entry.OpenSettings();
            Check(settings.IsOpen&&UI.PreferencesWindow.PreferencesBuilder.Selection==SharedModsTab.TabId,"Native Preferences > Mods was not selected");
            var state=UI.Windows.WindowManager.Shared.LastState;Check(state.ModsTabs==1&&state.Sections.Contains("EnusDE Communications"),"Native Mods section missing or duplicated");return state;
        }
    }
    static void Main()
    {
        try
        {
            var entry=new Entry();var local=Draw(entry,false);
            Check(local.Sliders.Count==11&&local.Rebinds.Count==3&&local.Toggles.Count==7,"Settings rendering stopped early");Check(!local.Buttons.ContainsKey("archive"),"Host archive visible to client");
            Console.WriteLine("PASS native settings content completes with all sliders and three binding fields");
            local.Buttons["performance_start"]();Check(entry.PerformanceRecording,"Capture cannot be started by a guest");
            var diagnostic=Draw(entry,false);diagnostic.Buttons["performance_pause"]();Check(entry.DiagnosticPauses==1,"Diagnostic pause unavailable");
            diagnostic.Buttons["performance_stop"]();Check(!entry.PerformanceRecording,"Capture cannot be stopped");
            Console.WriteLine("PASS performance capture and diagnostic pause are available in personal native settings");
            Check(local.Interactable.Values.All(v=>v),"Personal switches were created disabled");
            var fade=local.Sliders["fade"];Check(fade.Step==5&&fade.Min==5&&fade.Max==65,"Fade slider contract");fade.Change(60);Check(entry.Settings.FadeSeconds==60&&fade.Caption()=="60 s","Sixty seconds");fade.Change(65);Check(entry.Settings.FadeSeconds==0&&fade.Caption().Contains("∞"),"Unlimited step");
            Console.WriteLine("PASS fade slider five-second steps and final unlimited position");
            Check(entry.Settings.ChatBinding=="","Chat key must start unbound");entry.OpenChat.bindings[0].effectivePath="<Keyboard>/enter";local.Rebinds[entry.OpenChat.name]();Check(entry.Settings.ChatBinding=="<Keyboard>/enter","Rebound key not saved");local.Buttons["clear_chat_binding"]();Check(entry.Settings.ChatBinding=="","Clear binding");
            Console.WriteLine("PASS unbound chat action can be assigned and cleared through settings callbacks");
            local.Sliders["opacity"].Change(100);local.Sliders["red"].Change(1);local.Sliders["green"].Change(0);local.Sliders["blue"].Change(.25f);Check(entry.Settings.BackgroundOpacity==1&&entry.Settings.BackgroundR==1&&entry.Settings.BackgroundG==0&&entry.Settings.BackgroundB==.25f,"Color callbacks");local.Sliders["opacity"].Change(0);Check(entry.Settings.BackgroundOpacity==0,"Transparent background");
            Console.WriteLine("PASS background color and opacity from transparent to opaque");
            var host=Draw(entry,true);Check(host.Buttons.ContainsKey("archive")&&host.Toggles.Count==18&&host.Interactable.Values.All(v=>v),"Missing or disabled host controls");host.Toggles["admin_rank: President"](true);Check(entry.Settings.Host.AdminRanks.Contains("President"),"Host rank switch is not usable");host.Toggles["admin_rank: President"](false);GameAdapter.IsHost=false;host.Toggles["admin_rank: President"](true);Check(entry.Settings.Host.AdminRanks.Length==0,"Stale host settings granted permission");
            Console.WriteLine("PASS host-only archive control and rank mutation guard");
            Check(entry.Wheel.actionMap.asset==entry.Bindings.Asset&&entry.OpenChat.actionMap.asset==entry.Bindings.Asset,"Action asset missing");bool rejected=false;try{UnityEngine.InputSystem.InputActionReference.Create(new UnityEngine.InputSystem.InputActionMap("orphan").AddAction("test",UnityEngine.InputSystem.InputActionType.Button));}catch(InvalidOperationException){rejected=true;}Check(rejected,"Native asset requirement not enforced");
            Console.WriteLine("PASS binding factory registers input actions in an asset accepted by the native rebind control");
            local.Toggles["show_chat_sp"](false);Check(!entry.Settings.ChatVisible,"Singleplayer toggle not saved");local.Toggles["show_chat_sp"](true);Check(entry.Settings.ChatVisible,"Singleplayer toggle not restored");
            local.Toggles["desk_shortcut"](false);Check(!entry.Settings.ShowDeskShortcut,"Desk access cannot be hidden");local.Toggles["desk_shortcut"](true);Check(entry.Settings.ShowDeskShortcut,"Desk access cannot be restored");
            Console.WriteLine("PASS singleplayer visibility can be disabled and enabled through settings");
            Check(entry.Settings.AutoDock&&!entry.Settings.ChatDockRight&&!entry.Settings.ChatUnlocked,"Default is not docked left and locked");
            local.Toggles["chat_dock_right"](true);
            local.Toggles["chat_unlock"](true);
            Check(!entry.Settings.AutoDock&&entry.Settings.ChatUnlocked&&entry.Settings.ChatX==220&&entry.Settings.ChatY==80,"Unlock did not retain the visible position");
            local.Toggles["chat_unlock"](false);
            Check(!entry.Settings.AutoDock&&!entry.Settings.ChatUnlocked&&entry.Settings.ChatX==220,"Lock restored docking or lost the free position");
            local.Toggles["auto_dock"](true);
            Check(entry.Settings.AutoDock&&entry.Settings.ChatDockRight&&!entry.Settings.ChatUnlocked,"Docking lost the selected side");
            local.Buttons["reset_positions"]();
            Check(entry.Settings.AutoDock&&!entry.Settings.ChatDockRight&&!entry.Settings.ChatUnlocked,"Reset did not restore left docking");
            Console.WriteLine("PASS independent chat docking, unlocking, locking and reset");
            var physical=new UnityEngine.InputSystem.Controls.ButtonControl();UnityEngine.InputSystem.InputSystem.Controls[entry.Settings.WheelBinding]=physical;entry.Bindings.Sample(1,true);
            entry.Bindings.Map.Disable();GameAdapter.IsHost=false;physical.isPressed=true;entry.Bindings.Sample(2,true);Check(entry.Bindings.Pressed(entry.Wheel),"Guest cannot read a held key when the action-map press event is missing");entry.Bindings.EnsureEnabled();Check(entry.Bindings.Map.enabled,"Wheel map did not recover from a lifecycle reset");
            physical.isPressed=false;entry.Bindings.Sample(3,true);Check(entry.Bindings.Released(entry.Wheel),"Physical wheel release was lost");
            entry.Wheel.bindings[0].effectivePath="<Keyboard>/f9";entry.Bindings.Sample(4,true);Check(!entry.Bindings.Pressed(entry.Wheel),"Old cached key remained bound after rebind");
            Check(entry.Bindings.WheelFunctionKey,"Function key not recognized as an explicit menu command");entry.Wheel.bindings[0].effectivePath="<Keyboard>/g";Check(!entry.Bindings.WheelFunctionKey,"Letter binding may interrupt chat typing");
            Console.WriteLine("PASS guest wheel input survives action-map resets and observes physical press/release and rebinding");
            using(var several=new CommsBindings(new ModSettings()))
            {
                var first=new UnityEngine.InputSystem.Controls.ButtonControl();var second=new UnityEngine.InputSystem.Controls.ButtonControl();
                UnityEngine.InputSystem.InputSystem.Controls["<Keyboard>/f10"]=first;UnityEngine.InputSystem.InputSystem.ExtraControls["<Keyboard>/f10"]=second;
                several.Sample(1,true);second.isPressed=true;several.Sample(2,true);Check(several.Pressed(several.Wheel),"A second keyboard was ignored");
                UnityEngine.InputSystem.InputSystem.ExtraControls.Clear();UnityEngine.InputSystem.InputSystem.Changed();several.Sample(3,true);first.isPressed=true;several.Sample(4,true);Check(several.Pressed(several.Wheel),"Keyboard reconnect broke F10");
            }
            Console.WriteLine("PASS all matching keyboards are sampled and device changes refresh the binding cache");
            var shared=new NativeSettings(entry);shared.Register();shared.Register();
            var slot=(System.Collections.Generic.List<Action<UIPanelBuilder>>)AppDomain.CurrentDomain.GetData("ashenmare.preferences-mods.v1");
            int otherDraws=0;Action<UIPanelBuilder> other=b=>otherDraws++;slot.Add(other);
            shared.Show();Check(slot.Count==2&&UI.Windows.WindowManager.Shared.LastState.ModsTabs==1&&otherDraws==1,"Shared native Mods tab duplicated or lost another contributor");
            Console.WriteLine("PASS native Mods tab renders Communications and another contributor once");
            shared.Dispose();shared.Dispose();Check(slot.Count==1&&slot[0]==other&&GameMenus.IsOpen&&HarmonyLib.Harmony.Patches==0,"Unloading Communication changed another contributor or closed Preferences");
            Console.WriteLine("PASS unloading removes only the own callback and patch, preserving Preferences and other settings");
            shared.Register();shared.Show();Check(slot.Count==2&&UI.Windows.WindowManager.Shared.LastState.ModsTabs==0,"A second owner added a duplicate Mods tab");
            shared.Dispose();slot.Remove(other);Check(slot.Count==0,"Settings registration leaked");
            Console.WriteLine("PASS existing Mods tab owner wins regardless of registration order");
            var roleHost=Draw(entry,true);Check(roleHost.Buttons.ContainsKey("test_open")&&!local.Buttons.ContainsKey("test_open"),"Role testing is not host-only");roleHost.Buttons["test_open"]();
            var roleActive=Draw(entry,true);Check(!roleActive.Buttons.ContainsKey("archive"),"Live host controls mixed into test-role settings");roleActive.Buttons["test_dispatcher"]();Check(entry.TestRole==TestChatRole.Dispatcher,"Test role switch unavailable");roleActive.Buttons["test_stop"]();Check(!entry.RoleTestActive,"Role test cannot be ended");
            Console.WriteLine("PASS host-only role test starts, switches and exits through native settings");
            var voiceLocal=Draw(entry,false);
            Check(!entry.Settings.VoiceEnabled&&!entry.Settings.Host.VoiceAllowed&&!voiceLocal.Toggles.ContainsKey("voice_host_allow"),"Voice defaults or host boundary");
            voiceLocal.Toggles["voice_enable"](true);Check(entry.Settings.VoiceEnabled&&!entry.Settings.Host.VoiceAllowed,"Local opt-in granted host permission");voiceLocal.Toggles["voice_enable"](false);
            voiceLocal.Buttons["voice_next_microphone"]();Check(entry.Settings.VoiceMicrophone=="Test microphone","Microphone selector did not advance");voiceLocal.Buttons["voice_next_microphone"]();Check(entry.Settings.VoiceMicrophone=="","Microphone selector did not return to default");
            entry.VoiceTalk.bindings[0].effectivePath="<Keyboard>/v";voiceLocal.Rebinds[entry.VoiceTalk.name]();Check(entry.Settings.VoiceBinding=="<Keyboard>/v","Talk binding not saved");
            voiceLocal.Sliders["voice_volume"].Change(.25f);voiceLocal.Sliders["voice_gain"].Change(1.5f);Check(entry.Settings.VoiceVolume==.25f&&entry.Settings.VoiceGain==1.5f,"Voice sliders not applied");
            Console.WriteLine("PASS voice opt-in, microphone cycling, push-to-talk and personal audio settings");
            var voiceHost=Draw(entry,true);voiceHost.Toggles["voice_host_allow"](true);voiceHost.Sliders["voice_range"].Change(60);Check(entry.Settings.Host.VoiceAllowed&&entry.Settings.VoiceRange==60,"Host voice settings not applied");
            GameAdapter.IsHost=false;voiceHost.Toggles["voice_host_allow"](false);voiceHost.Sliders["voice_range"].Change(10);Check(entry.Settings.Host.VoiceAllowed&&entry.Settings.VoiceRange==60,"Stale guest callback changed host voice policy");
            Console.WriteLine("PASS host voice permission and hearing distance reject stale guest callbacks");
            Console.WriteLine("RESULT 17 settings UI regression tests passed");
        }
        catch(Exception error){Console.Error.WriteLine("FAIL "+error);Environment.ExitCode=1;}
    }
}
