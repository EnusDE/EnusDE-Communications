using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
namespace EnusDE.Communications
{
    internal sealed class CommsBindings:IDisposable
    {
        internal readonly InputActionAsset Asset;
        internal readonly InputActionMap Map;
        internal readonly InputAction Wheel,OpenChat,VoiceTalk;
        sealed class BoundButton {internal string Path;internal ButtonControl[] Controls=Array.Empty<ButtonControl>();internal int RetryFrame,Revision;internal readonly ButtonEdges Edges=new ButtonEdges();}
        readonly Dictionary<InputAction,BoundButton> buttons=new Dictionary<InputAction,BoundButton>();
        int deviceRevision;
        void DevicesChanged(InputDevice device,InputDeviceChange change){deviceRevision++;}
        internal bool Pressed(InputAction action)=>buttons.TryGetValue(action,out var bound)&&bound.Edges.Pressed;
        internal bool Released(InputAction action)=>buttons.TryGetValue(action,out var bound)&&bound.Edges.Released;
        internal bool Held(InputAction action)=>buttons.TryGetValue(action,out var bound)&&bound.Edges.Held;
        internal bool Ready(InputAction action)=>buttons.TryGetValue(action,out var bound)&&bound.Edges.Ready;
        internal bool WheelFunctionKey
        {
            get
            {
                string path=Wheel.bindings[0].effectivePath??"";int slash=path.LastIndexOf('/'),number;
                return path.StartsWith("<Keyboard>",StringComparison.OrdinalIgnoreCase)&&slash>=0&&path.Length>slash+2&&(path[slash+1]=='f'||path[slash+1]=='F')&&Int32.TryParse(path.Substring(slash+2),out number)&&number>=1&&number<=24;
            }
        }
        internal void Sample(int frame,bool focused){Sample(Wheel,frame,focused);Sample(OpenChat,frame,focused);Sample(VoiceTalk,frame,focused);}
        void Sample(InputAction action,int frame,bool focused)
        {
            string path=action.bindings.Count>0?action.bindings[0].effectivePath??"":"";BoundButton bound;
            bool existing=buttons.TryGetValue(action,out bound);if(!existing)buttons[action]=bound=new BoundButton();
            bool changed=existing&&(bound.Path!=path||bound.Revision!=deviceRevision);
            if(bound.Path!=path||bound.Revision!=deviceRevision||bound.Controls.Any(c=>!c.device.added)||bound.Controls.Length==0&&frame>=bound.RetryFrame)
            {
                bound.Path=path;bound.Revision=deviceRevision;bound.RetryFrame=frame+60;bound.Controls=Array.Empty<ButtonControl>();
                if(!String.IsNullOrEmpty(path))try{using(var found=InputSystem.FindControls(path))bound.Controls=found.OfType<ButtonControl>().ToArray();}catch(ArgumentException){}
            }
            bool physical=bound.Controls.Length>0;
            bound.Edges.Sample(frame,physical?bound.Controls.Any(c=>c.isPressed):action.IsPressed(),physical?bound.Controls.Any(c=>c.wasPressedThisFrame):action.WasPressedThisFrame(),physical?bound.Controls.Any(c=>c.wasReleasedThisFrame):action.WasReleasedThisFrame(),focused,changed);
        }
        internal void EnsureEnabled(){if(!Map.enabled)Map.Enable();}
        internal CommsBindings(ModSettings settings)
        {
            Asset=ScriptableObject.CreateInstance<InputActionAsset>();Asset.name="EnusDE Communications";
            Map=new InputActionMap("EnusDE Communications");Asset.AddActionMap(Map);
            Wheel=Map.AddAction("Communication wheel",InputActionType.Button);Wheel.AddBinding(settings.WheelBinding);
            OpenChat=Map.AddAction("Open chat",InputActionType.Button);OpenChat.AddBinding(settings.ChatBinding??"");
            VoiceTalk=Map.AddAction("Push to talk",InputActionType.Button);VoiceTalk.AddBinding(settings.VoiceBinding??"");
            InputSystem.onDeviceChange+=DevicesChanged;
        }
        public void Dispose(){InputSystem.onDeviceChange-=DevicesChanged;if(Asset!=null){Asset.Disable();UnityEngine.Object.Destroy(Asset);}}
    }
}
