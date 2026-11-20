// Test-only native builder boundary. Parameter names/order match the checked game API.
using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public struct Vector2Int {public Vector2Int(int x,int y){}}
    public class Object {public string name;public static void Destroy(Object value){}}
    public class ScriptableObject:Object {public static T CreateInstance<T>() where T:ScriptableObject,new()=>new T();}
}
namespace UnityEngine.InputSystem
{
    public enum InputActionType {Button}
    public class InputDevice {public bool added=true;}
    public enum InputDeviceChange {Added,Removed}
    public class InputControl {public InputDevice device=new InputDevice();}
    public sealed class ControlList:List<InputControl>,IDisposable {public void Dispose(){}}
    public static class InputSystem
    {
        public static readonly Dictionary<string,InputControl> Controls=new Dictionary<string,InputControl>();public static readonly Dictionary<string,InputControl> ExtraControls=new Dictionary<string,InputControl>();
        public static event Action<InputDevice,InputDeviceChange> onDeviceChange;
        public static void Changed(){onDeviceChange?.Invoke(new InputDevice(),InputDeviceChange.Added);}
        public static ControlList FindControls(string path){var result=new ControlList();if(Controls.TryGetValue(path,out var control))result.Add(control);if(ExtraControls.TryGetValue(path,out control))result.Add(control);return result;}
    }
    public sealed class InputBinding {public string effectivePath;}
    public sealed class InputAction
    {
        public string name;public InputActionMap actionMap;public readonly List<InputBinding> bindings=new List<InputBinding>();
        public void AddBinding(string path){bindings.Add(new InputBinding{effectivePath=path});}
        public bool WasPressedThisFrame()=>false;public bool WasReleasedThisFrame()=>false;public bool IsPressed()=>false;
    }
    public sealed class InputActionMap
    {
        public InputActionAsset asset;public bool enabled;public InputActionMap(string name){}public void Enable(){enabled=true;}public void Disable(){enabled=false;}
        public InputAction AddAction(string name,InputActionType type)=>new InputAction{name=name,actionMap=this};
    }
    public sealed class InputActionAsset:UnityEngine.ScriptableObject
    {
        public void AddActionMap(InputActionMap map){map.asset=this;}
        public void Disable(){}
    }
    public sealed class InputActionReference
    {
        public static InputActionReference Create(InputAction action)
        {if(action.actionMap?.asset==null)throw new InvalidOperationException("Action must belong to an InputActionAsset");return new InputActionReference();}
    }
}
namespace UnityEngine.InputSystem.Controls
{
    public sealed class ButtonControl:UnityEngine.InputSystem.InputControl {public bool wasPressedThisFrame,wasReleasedThisFrame,isPressed;}
}
namespace UI.Builder
{
    public enum ExpandableStyle { SectionHeader }
    public sealed class UIState<T>{public T Value;public UIState(T value){Value=value;}}
    public sealed class Slider
    {
        public Func<float> Value;public Func<string> Caption;public Action<float> Change;public float Step,Min,Max;
    }
    public sealed class BuildState
    {
        public readonly List<string> Sections=new List<string>();public int ModsTabs;public readonly Dictionary<string,Slider> Sliders=new Dictionary<string,Slider>();
        public readonly Dictionary<string,Action> Buttons=new Dictionary<string,Action>();
        public readonly Dictionary<string,Action<bool>> Toggles=new Dictionary<string,Action<bool>>();
        public readonly Dictionary<string,Action> Rebinds=new Dictionary<string,Action>();
        public readonly Dictionary<string,bool> Interactable=new Dictionary<string,bool>();
    }
    public struct UIPanelBuilder
    {
        public BuildState State;
        public float FieldLabelWidth;
        public void AddExpandable(string label,UIState<bool> state,Action<UIPanelBuilder> content,ExpandableStyle style){State.Sections.Add(label);content(this);}
        public void AddLabel(string text){}
        public void Spacer(float height){}
        public void SetNeedsRebuild(){}
        public void VScrollView(Action<UIPanelBuilder> content){content(this);}
        public void AddField(string name,Slider slider){State.Sliders.Add(name,slider);}
        public void AddButton(string name,Action action){State.Buttons.Add(name,action);}
        public void AddFieldToggle(string name,Func<bool> read,Action<bool> changed,bool interactable){read();State.Toggles.Add(name,changed);State.Interactable.Add(name,interactable);}
        public Slider AddSliderQuantized(Func<float> valueClosure,Func<string> textValueClosure,Action<float> valueChangedAction,float increment,float minValue,float maxValue,Action<float> editingEndedAction)
        {
            if(increment<=0)throw new ArgumentException("increment must be positive");
            if(minValue>maxValue||valueClosure()<minValue||valueClosure()>maxValue)throw new ArgumentException("Invalid slider range");
            textValueClosure();return new Slider{Value=valueClosure,Caption=textValueClosure,Change=valueChangedAction,Step=increment,Min=minValue,Max=maxValue};
        }
        public Slider AddSlider(Func<float> valueClosure,Func<string> textValueClosure,Action<float> valueChangedAction,float minValue,float maxValue,bool wholeNumbers,Action<float> editingEndedAction)
        {return AddSliderQuantized(valueClosure,textValueClosure,valueChangedAction,wholeNumbers?1:.001f,minValue,maxValue,editingEndedAction);}
        public void AddInputBindingControl(UnityEngine.InputSystem.InputAction inputAction,bool conflict,Action onRebind)
        {if(inputAction.bindings.Count==0)throw new ArgumentException("Missing empty binding");UnityEngine.InputSystem.InputActionReference.Create(inputAction);State.Rebinds.Add(inputAction.name,onRebind);}
    }
}
namespace EnusDE.Communications
{
    public sealed class TestText {public string Get(string key)=>key;}
    public sealed class TestLog {public void Warning(string text){throw new Exception(text);}}
    public sealed class TestUi
    {
        readonly ModSettings settings;
        public TestUi(ModSettings settings){this.settings=settings;}
        public void ShowArchive(){}
        public void Toast(string text){}
        public void RememberChatPosition(){settings.ChatX=220;settings.ChatY=80;}
    }
    public sealed class TestRuntime {public TestUi UI;}
    public sealed class Entry
    {
        public ModSettings Settings=new ModSettings();public TestText Text=new TestText();public TestLog Log=new TestLog();public TestRuntime Runtime=new TestRuntime();
        internal CommsBindings Bindings;public UnityEngine.InputSystem.InputAction Wheel=>Bindings.Wheel;public UnityEngine.InputSystem.InputAction OpenChat=>Bindings.OpenChat;public bool BindingConflict=>Settings.ChatBinding==Settings.WheelBinding;
        public UnityEngine.InputSystem.InputAction VoiceTalk=>Bindings.VoiceTalk;
        public bool VoiceBindingConflict=>false;
        public string[] VoiceMicrophones=>new[]{"Test microphone"};
        public string VoiceStatus=>"voice_off";
        public Person[] VoicePeople=>Array.Empty<Person>();
        public Entry(){Bindings=new CommsBindings(Settings);Runtime.UI=new TestUi(Settings);}
        public Action OpenSettingsCallback;public void OpenSettings(){OpenSettingsCallback?.Invoke();}
        public void Save(bool refresh=true){Settings.Normalize();}
        public void SetChatBinding(string path){Settings.ChatBinding=path;OpenChat.bindings[0].effectivePath=path;Save();}
        public bool PerformanceRecording,CanDiagnosticPause=true;public int DiagnosticPauses;
        public void TogglePerformanceCapture(){PerformanceRecording=!PerformanceRecording;}
        public void PauseForDiagnosis(){DiagnosticPauses++;}
        public bool RoleTestActive;public TestChatRole TestRole;
        public void StartRoleTest(){RoleTestActive=true;}
        public void SwitchTestRole(TestChatRole role){TestRole=role;}
        public void StopRoleTest(){RoleTestActive=false;}
    }
    public static class GameAdapter {public static bool IsHost;}
}
