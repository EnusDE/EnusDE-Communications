// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Linq;
using UI.Builder;
using System.Reflection;
using UI.PreferencesWindow;
using UI.Windows;
namespace EnusDE.Communications
{
    internal sealed class NativeSettings:IDisposable
    {
        readonly Entry entry;bool registered;
        readonly UIState<bool> expanded=new UIState<bool>(false);
        string T(string key)=>entry.Text.Get(key);
        internal NativeSettings(Entry owner){entry=owner;}
        internal void Register()
        {
            if(registered)return;
            SharedModsTab.Register(b=>b.AddExpandable("EnusDE Communications",expanded,Content,ExpandableStyle.SectionHeader),message=>entry.Log.Warning(message));
            registered=true;
        }
        internal bool IsOpen=>GameMenus.IsOpen;
        internal void Close(){WindowManager.Shared?.GetWindow<PreferencesWindow>()?.GetComponent<Window>()?.CloseWindow();}
        public void Dispose(){if(!registered)return;SharedModsTab.Remove();registered=false;}
        internal void Show()
        {
            try
            {
                var manager=WindowManager.Shared;if(manager==null){entry.Runtime?.UI?.Toast(T("settings_unavailable"));return;}
                var selected=typeof(PreferencesWindow).Assembly.GetType("UI.PreferencesWindow.PreferencesBuilder",true).GetField("SelectedTab",BindingFlags.Static|BindingFlags.NonPublic)?.GetValue(null) as UIState<string>;
                if(selected==null)throw new MissingFieldException("PreferencesBuilder.SelectedTab");
                selected.Value=SharedModsTab.TabId;expanded.Value=true;manager.GetWindow<PreferencesWindow>().Show();
            }
            catch(Exception error){entry.Log.Warning("Could not open Preferences > Mods: "+error.Message);entry.Runtime?.UI?.Toast(T("settings_unavailable"));}
        }
        void Content(UIPanelBuilder b)
        {
            b.FieldLabelWidth=28;
            b.AddLabel(T("settings_display"));
            b.AddFieldToggle(T("show_chat_sp"),()=>entry.Settings.ChatVisible,value=>{entry.Settings.ChatVisible=value;entry.Save();},interactable:true);
            b.AddLabel(T("mp_always_visible"));
            b.FieldLabelWidth=170;
            b.AddField(T("fade"),b.AddSliderQuantized(()=>entry.Settings.FadeSeconds==0?65:entry.Settings.FadeSeconds,()=>entry.Settings.FadeSeconds==0?"∞ · "+T("never"):entry.Settings.FadeSeconds.ToString("0")+" s",value=>{entry.Settings.FadeSeconds=value>60?0:value;entry.Save();},increment:5,minValue:5,maxValue:65,editingEndedAction:null));
            b.AddLabel(T("fade_hint"));
            b.AddField(T("font_size"),b.AddSliderQuantized(()=>entry.Settings.FontSize,()=>entry.Settings.FontSize.ToString("0"),value=>{entry.Settings.FontSize=value;entry.Save();},increment:1,minValue:12,maxValue:24,editingEndedAction:null));
            b.AddField(T("chat_width"),b.AddSliderQuantized(()=>entry.Settings.ChatWidth,()=>entry.Settings.ChatWidth.ToString("0"),value=>{entry.Settings.ChatWidth=value;entry.Save();},increment:20,minValue:440,maxValue:800,editingEndedAction:null));
            b.AddField(T("chat_height"),b.AddSliderQuantized(()=>entry.Settings.ChatHeight,()=>entry.Settings.ChatHeight.ToString("0"),value=>{entry.Settings.ChatHeight=value;entry.Save();},increment:10,minValue:120,maxValue:600,editingEndedAction:null));
            b.FieldLabelWidth=28;
            b.AddFieldToggle(T("auto_dock"),()=>entry.Settings.AutoDock,value=>{
                if(!value&&entry.Settings.AutoDock)entry.Runtime?.UI?.RememberChatPosition();
                entry.Settings.AutoDock=value;
                entry.Settings.ChatUnlocked=false;
                entry.Save();
                b.SetNeedsRebuild();
            },interactable:true);
            b.AddFieldToggle(T("chat_dock_right"),()=>entry.Settings.ChatDockRight,value=>{
                entry.Settings.ChatDockRight=value;
                entry.Settings.AutoDock=true;
                entry.Settings.ChatUnlocked=false;
                entry.Save();
                b.SetNeedsRebuild();
            },interactable:true);
            b.AddFieldToggle(T("chat_unlock"),()=>entry.Settings.ChatUnlocked,value=>{
                if(value&&entry.Settings.AutoDock)entry.Runtime?.UI?.RememberChatPosition();
                if(value)entry.Settings.AutoDock=false;
                entry.Settings.ChatUnlocked=value;
                entry.Save();
                b.SetNeedsRebuild();
            },interactable:true);
            b.AddFieldToggle(T("timestamps"),()=>entry.Settings.ShowTimestamps,value=>{entry.Settings.ShowTimestamps=value;entry.Save();},interactable:true);
            b.AddFieldToggle(T("desk_shortcut"),()=>entry.Settings.ShowDeskShortcut,value=>{entry.Settings.ShowDeskShortcut=value;entry.Save(false);},interactable:true);
            b.Spacer(6);b.AddLabel(T("background"));
            b.FieldLabelWidth=170;
            b.AddField(T("opacity"),b.AddSliderQuantized(()=>entry.Settings.BackgroundOpacity*100,()=>((int)(entry.Settings.BackgroundOpacity*100))+"%",value=>{entry.Settings.BackgroundOpacity=value/100;entry.Save();},increment:5,minValue:0,maxValue:100,editingEndedAction:null));
            b.AddField(T("red"),b.AddSlider(()=>entry.Settings.BackgroundR,()=>((int)(entry.Settings.BackgroundR*255)).ToString(),value=>{entry.Settings.BackgroundR=value;entry.Save();},0,1,false,null));
            b.AddField(T("green"),b.AddSlider(()=>entry.Settings.BackgroundG,()=>((int)(entry.Settings.BackgroundG*255)).ToString(),value=>{entry.Settings.BackgroundG=value;entry.Save();},0,1,false,null));
            b.AddField(T("blue"),b.AddSlider(()=>entry.Settings.BackgroundB,()=>((int)(entry.Settings.BackgroundB*255)).ToString(),value=>{entry.Settings.BackgroundB=value;entry.Save();},0,1,false,null));
            b.AddButton(T("reset_positions"),()=>{entry.Settings.ChatX=16;entry.Settings.ChatY=174;entry.Settings.AutoDock=true;entry.Settings.ChatDockRight=false;entry.Settings.ChatUnlocked=false;entry.Settings.DispatchX=1080;entry.Settings.DispatchY=140;entry.Settings.ArchiveX=650;entry.Settings.ArchiveY=160;entry.Save();b.SetNeedsRebuild();});
            b.Spacer(8);b.AddLabel(T("settings_controls"));b.AddLabel(T("bind_prompt"));b.AddLabel(T("wheel_binding"));b.AddInputBindingControl(entry.Wheel,entry.BindingConflict,()=>{entry.Settings.WheelBinding=entry.Wheel.bindings[0].effectivePath;entry.Save();b.SetNeedsRebuild();});
            b.AddLabel(T("chat_binding"));b.AddInputBindingControl(entry.OpenChat,entry.BindingConflict,()=>{entry.Settings.ChatBinding=entry.OpenChat.bindings[0].effectivePath??"";entry.Save();b.SetNeedsRebuild();});
            b.AddButton(T("clear_chat_binding"),()=>{entry.SetChatBinding("");b.SetNeedsRebuild();});
            b.AddLabel(T("binding_conflict"));
            b.AddLabel(T("wheel_hint"));b.AddField(T("whistle_volume"),b.AddSlider(()=>entry.Settings.WhistleVolume,()=>((int)(entry.Settings.WhistleVolume*100))+"%",value=>{entry.Settings.WhistleVolume=value;entry.Save();},0,1,false,null));
            b.AddLabel(T("language_hint"));
            b.AddLabel(T("settings_location")+": ModSettings/enusde-communications.json");
            b.Spacer(8);b.AddLabel(T("voice_title"));b.AddLabel(T("voice_hint"));
            b.FieldLabelWidth=28;
            b.AddFieldToggle(T("voice_enable"),()=>entry.Settings.VoiceEnabled,value=>{entry.Settings.VoiceEnabled=value;entry.Save();b.SetNeedsRebuild();},interactable:true);
            b.AddLabel(entry.VoiceStatus);
            b.AddLabel(T("voice_microphone")+": "+(String.IsNullOrEmpty(entry.Settings.VoiceMicrophone)?T("voice_default_device"):entry.Settings.VoiceMicrophone));
            b.AddButton(T("voice_next_microphone"),()=>{
                string[] devices=new[]{""}.Concat(entry.VoiceMicrophones).Distinct().ToArray();
                int index=Array.IndexOf(devices,entry.Settings.VoiceMicrophone);
                entry.Settings.VoiceMicrophone=devices[(index+1)%devices.Length];entry.Save();b.SetNeedsRebuild();
            });
            b.AddLabel(T("voice_binding"));b.AddInputBindingControl(entry.VoiceTalk,entry.VoiceBindingConflict,()=>{entry.Settings.VoiceBinding=entry.VoiceTalk.bindings[0].effectivePath??"";entry.Save();b.SetNeedsRebuild();});
            b.FieldLabelWidth=170;
            b.AddField(T("voice_volume"),b.AddSlider(()=>entry.Settings.VoiceVolume,()=>((int)(entry.Settings.VoiceVolume*100))+"%",value=>{entry.Settings.VoiceVolume=value;entry.Save();},0,1,false,null));
            b.AddField(T("voice_gain"),b.AddSlider(()=>entry.Settings.VoiceGain,()=>((int)(entry.Settings.VoiceGain*100))+"%",value=>{entry.Settings.VoiceGain=value;entry.Save();},0,2,false,null));
            b.FieldLabelWidth=28;
            foreach(var person in entry.VoicePeople)
            {
                string id=person.Id;
                b.AddFieldToggle(T("voice_mute")+": "+person.Name+" ("+id+")",()=>entry.Settings.VoiceMuted.Contains(id),value=>{entry.Settings.VoiceMuted=Set(entry.Settings.VoiceMuted,id,value);entry.Save();},interactable:true);
            }
            b.Spacer(8);b.AddLabel(T("performance_title"));b.AddLabel(T("performance_hint"));
            b.AddButton(T(entry.PerformanceRecording?"performance_stop":"performance_start"),()=>{entry.TogglePerformanceCapture();b.SetNeedsRebuild();});
            if(entry.CanDiagnosticPause)b.AddButton(T("performance_pause"),()=>entry.PauseForDiagnosis());
            if(!GameAdapter.IsHost)return;
            b.Spacer(8);b.AddLabel(T("voice_host_title"));b.FieldLabelWidth=28;
            b.AddFieldToggle(T("voice_host_allow"),()=>entry.Settings.Host.VoiceAllowed,value=>{if(!GameAdapter.IsHost)return;entry.Settings.Host.VoiceAllowed=value;entry.Save();b.SetNeedsRebuild();},interactable:true);
            b.FieldLabelWidth=170;
            b.AddField(T("voice_range"),b.AddSliderQuantized(()=>entry.Settings.VoiceRange,()=>entry.Settings.VoiceRange.ToString("0")+" m",value=>{if(!GameAdapter.IsHost)return;entry.Settings.VoiceRange=value;entry.Save();},increment:5,minValue:5,maxValue:100,editingEndedAction:null));

            b.Spacer(8);b.AddLabel(T("test_title"));b.AddLabel(T("test_hint"));
            if(!entry.RoleTestActive)b.AddButton(T("test_open"),entry.StartRoleTest);
            else
            {
                b.AddButton(T("test_driver"),()=>{entry.SwitchTestRole(TestChatRole.Driver);Close();});
                b.AddButton(T("test_dispatcher"),()=>{entry.SwitchTestRole(TestChatRole.Dispatcher);Close();});
                b.AddButton(T("test_admin"),()=>{entry.SwitchTestRole(TestChatRole.Administrator);Close();});
                b.AddButton(T("test_stop"),()=>{entry.StopRoleTest();Close();});return;
            }
            b.Spacer(8);b.AddLabel(T("host_permissions"));
            b.FieldLabelWidth=28;
            foreach(string rank in new[]{"Crew","Dispatcher","Trainmaster","Officer","President"})
            {
                string selected=rank;
                b.AddFieldToggle(T("admin_rank")+": "+rank,()=>entry.Settings.Host.AdminRanks.Contains(selected),value=>{if(!GameAdapter.IsHost)return;entry.Settings.Host.AdminRanks=Set(entry.Settings.Host.AdminRanks,selected,value);entry.Save();},interactable:true);
                b.AddFieldToggle(T("dispatcher_rank")+": "+rank,()=>entry.Settings.Host.DispatcherRanks.Contains(selected),value=>{if(!GameAdapter.IsHost)return;entry.Settings.Host.DispatcherRanks=Set(entry.Settings.Host.DispatcherRanks,selected,value);entry.Save();},interactable:true);
            }
            b.AddLabel(T("archive_hint"));b.AddButton(T("archive"),()=>{Close();entry.Runtime?.UI?.ShowArchive();});
        }
        static string[] Set(string[] values,string item,bool add){return add?values.Concat(new[]{item}).Distinct().ToArray():values.Where(value=>value!=item).ToArray();}
    }
}
