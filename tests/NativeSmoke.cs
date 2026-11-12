// Test-only companion. Never included in the playable mod.
using System;
using System.Collections;
using System.Reflection;
using System.Linq;
using EnusDE.Communications;
using Railroader.Modding;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
public sealed class NativeSmoke:IModEntryPoint
{
    GameObject root;
    public void OnModLoad(IModContext context){root=new GameObject("Communications smoke test");UnityEngine.Object.DontDestroyOnLoad(root);root.AddComponent<NativeSmokeRunner>().Log=context.Log;}
    public void OnModUnload(){if(root!=null)UnityEngine.Object.Destroy(root);}
}
public sealed class NativeSmokeRunner:MonoBehaviour
{
    internal IModLogger Log;
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static object Field(object target,string name)=>target.GetType().GetField(name,Flags).GetValue(target);
    static object Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Flags).Invoke(target,args);
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(6);
        try
        {
            var entry=(Entry)typeof(Entry).GetField("active",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            if(entry==null)throw new Exception("Communication entry point was not loaded.");
            if(typeof(Entry).Assembly.GetReferencedAssemblies().Any(a=>a.Name=="EnusDE.ModMenu"))throw new Exception("External menu dependency remains.");
            var wheelReference=InputActionReference.Create((InputAction)Field(entry,"Wheel"));var chatReference=InputActionReference.Create((InputAction)Field(entry,"OpenChat"));UnityEngine.Object.Destroy(wheelReference);UnityEngine.Object.Destroy(chatReference);
            // Main menu: exercise the lifecycle before StateManager is ready.
            entry.OnWorldLoad();yield return null;
            var runtime=Field(entry,"Runtime");if(runtime==null)throw new Exception("Runtime not created.");
            var ui=Field(runtime,"UI");if(ui==null)throw new Exception("Native chat construction failed.");
            Invoke(ui,"DockTop");var toolbar=(RectTransform)Field(ui,"top");var ownerCanvas=(Canvas)Field(ui,"toolbarCanvas");var slot=(RectTransform)Field(ui,"topSlot");
            if(toolbar.GetComponentInParent<Canvas>()!=ownerCanvas||ownerCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>()?.enabled!=true)throw new Exception("Chat icon has no independent active raycaster.");
            if(!toolbar.GetComponentInChildren<ChatIcon>(true).raycastTarget)throw new Exception("Speech bubble is not a pointer target.");
            if(toolbar.GetComponentInChildren<UnityEngine.UI.ContentSizeFitter>()!=null)throw new Exception("Toolbar icon contains a self-sizing prefab.");
            if(toolbar.GetComponentInChildren<ChatIcon>(true)==null)throw new Exception("Missing native-style speech bubble icon.");
            var client=(ClientSession)Field(runtime,"Client");var state=new ServerPacket{Kind="state",Nonce=client.Nonce,Token="smoke",Version=1,Train="alpha",TrainLabel="Test 10",Crew="A",ActualCrew="A",Admin=true,Dispatcher=true};
            client.Accept(state,1);
            var view=(WorldView)Field(runtime,"View");view.Trains["alpha"]=new WorkingTrain{Id="alpha",Label="Test 10",Crew="A"};view.Crews["A"]="Alpha";
            Invoke(ui,"Rebuild");Invoke(ui,"ShowChooseTrain",false);Invoke(ui,"ClosePopup");Invoke(ui,"ShowCrewView");Invoke(ui,"ClosePopup");
            state=new ServerPacket{Kind="state",Nonce=client.Nonce,Token="smoke",Version=2,Train="alpha",TrainLabel="Test 10",Crew="A",ActualCrew="A",Admin=false,Dispatcher=false};client.Accept(state,1);
            Invoke(ui,"ShowMessageBuilder",null,null,false);var builder=(RectTransform)Field(ui,"popup");
            var placeInputs=builder.GetComponentsInChildren<TMP_InputField>();if(placeInputs.Length!=3)throw new Exception("Message builder lacks origin/destination/task fields.");
            placeInputs[0].text="Bryson City";placeInputs[1].text="Sylva";((Action)Field(ui,"popupRefresh"))();
            var preview=builder.Find("template preview/preview text").GetComponentInChildren<TMP_Text>().text;
            if(!preview.Contains("Test 10")||!preview.Contains("Bryson City")||!preview.Contains("Sylva"))throw new Exception("Message preview did not fill train/places.");
            Invoke(ui,"ShowPlacePicker",builder,new[]{"Bryson City","Sylva"},new Action<string>(_=>{}),"builder_places");
            if(builder.Find("message place picker")==null)throw new Exception("Location selection did not open.");Invoke(ui,"ClosePopup");
            Invoke(ui,"BuildWheel",true);var buttons=(UnityEngine.UI.Button[])Field(ui,"wheelButtons");if(buttons.Length!=8)throw new Exception("Original eight-button wheel missing.");
            var brown=buttons[0].GetComponent<UnityEngine.UI.Image>().color;if(!(brown.r>brown.g&&brown.g>brown.b))throw new Exception("Wheel is not brown.");
            var control=(HoldWheelController)Field(ui,"actionMenu");control.Hover(0);string picked;if(control.Tick(new MenuContext{Train=state.Train,Token=state.Token,Version=state.Version},false,true,true,out picked)!=MenuResult.Selected||picked!="stop")throw new Exception("Guest wheel did not select on release.");Invoke(ui,"CancelWheel");
            Invoke(ui,"ShowDistances");Invoke(ui,"ClosePopup");
            state=new ServerPacket{Kind="state",Nonce=client.Nonce,Token="smoke",Version=3,Train="alpha",TrainLabel="Test 10",Crew="A",ActualCrew="A",Admin=true,Dispatcher=true};client.Accept(state,1);
            client.Conversations["fdl:alpha"]="Test 10";Invoke(ui,"ShowConversation","fdl:alpha",false);Invoke(ui,"CloseDispatch");
            // Exercise the actual transparent card, raycaster and reply popup with an FDL-capable operator.
            var request=new ChatRecord{Id="smoke-question",Channel="dispatch",Target="fdl:alpha",TargetLabel="Test 10",SenderId="other-dispatcher",SenderName="Dispatcher",FromDispatcher=true,Text="Please confirm",Utc=DateTime.UtcNow};
            client.Accept(new ServerPacket{Kind="message",Nonce=client.Nonce,Token=state.Token,Version=state.Version,Message=request,CanReply=true},1);Invoke(ui,"Rebuild");
            Canvas.ForceUpdateCanvases();
            var content=(RectTransform)Field(ui,"content");var card=content.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name.Contains("Please confirm"));
            float oldY=((RectTransform)card.transform).anchoredPosition.y;
            client.Accept(new ServerPacket{Kind="message",Nonce=client.Nonce,Token=state.Token,Version=state.Version,Message=new ChatRecord{Id="smoke-newest",Channel="train",Target="alpha",SenderName="Operator",Text="Newest below",Utc=request.Utc.AddSeconds(1)}},2);Invoke(ui,"Rebuild");Canvas.ForceUpdateCanvases();
            card=content.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name.Contains("Please confirm"));if(((RectTransform)card.transform).anchoredPosition.y<=oldY)throw new Exception("New messages did not push old rows upward.");
            var channelButtons=(UnityEngine.UI.Button[])Field(ui,"channelButtons");if(channelButtons[0].GetComponentInChildren<TMP_Text>().color==channelButtons[1].GetComponentInChildren<TMP_Text>().color||channelButtons[1].GetComponentInChildren<TMP_Text>().color==channelButtons[2].GetComponentInChildren<TMP_Text>().color)throw new Exception("Channels do not have distinct colors.");
            for(int index=0;index<channelButtons.Length;index++)
            {var rect=(RectTransform)channelButtons[index].transform;if(Math.Abs(rect.rect.width-72)>.1||Math.Abs(rect.anchoredPosition.x-index*75)>.1||channelButtons[index].GetComponent<UnityEngine.UI.ContentSizeFitter>()!=null)throw new Exception("Chat channels were resized or overlap.");}
            if(card.GetComponent<UnityEngine.UI.Image>().canvasRenderer.cullTransparentMesh)throw new Exception("Transparent reply card is culled.");
            var center=RectTransformUtility.WorldToScreenPoint(null,((RectTransform)card.transform).TransformPoint(((RectTransform)card.transform).rect.center));
            var events=UnityEngine.EventSystems.EventSystem.current;if(events==null)throw new Exception("Native EventSystem missing.");
            var pointer=new UnityEngine.EventSystems.PointerEventData(events){position=center};var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();events.RaycastAll(pointer,hits);
            if(hits.Count==0||hits[0].gameObject!=card.gameObject)throw new Exception("Transparent reply card is not the top raycast hit.");
            UnityEngine.EventSystems.ExecuteEvents.Execute(card.gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            if(Field(ui,"dispatch")==null||((string)Field(ui,"replyMessageId"))!=request.Id)throw new Exception("Clicking reply card did not open its window.");
            client.Accept(new ServerPacket{Kind="reply_done",Nonce=client.Nonce,Token=state.Token,Version=state.Version,ReplyTo=request.Id},1);Invoke(ui,"Rebuild");if(Field(ui,"dispatch")!=null)throw new Exception("Confirmed reply window stayed open.");
            if(UnityEngine.Object.FindObjectsByType<CommsOwnedText>(FindObjectsSortMode.None).Length<1)throw new Exception("Expected owned UI markers.");
            if(((RectTransform)Field(ui,"chat")).GetComponentsInChildren<TMP_InputField>(true).Length<1)throw new Exception("Missing native input field.");
            Invoke(entry,"OpenSettings");yield return new WaitForSecondsRealtime(2);
            var localization=Field(entry,"Text");string height=(string)Invoke(localization,"Get","chat_height");
            var preferences=UI.Windows.WindowManager.Shared.GetWindow<UI.PreferencesWindow.PreferencesWindow>();
            var settings=preferences.GetComponent<UI.Windows.Window>();if(settings==null||!settings.IsShown)throw new Exception("Native Preferences window not used.");
            var selected=typeof(UI.PreferencesWindow.PreferencesWindow).Assembly.GetType("UI.PreferencesWindow.PreferencesBuilder").GetField("SelectedTab",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null) as UI.Builder.UIState<string>;
            if(selected?.Value!="ashenmare.mods")throw new Exception("Native Mods tab not selected.");
            var toggles=settings.GetComponentsInChildren<UnityEngine.UI.Toggle>(true);if(toggles.Length<4||toggles.Any(t=>!t.interactable))throw new Exception("Personal settings switches are missing or disabled.");
            var labels=UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Where(t=>t.gameObject.activeInHierarchy).Select(t=>t.text??"").ToArray();
            if(!labels.Any(t=>t.Contains(height))||!labels.Any(t=>t.Contains("enusde-communications.json")))throw new Exception("Native settings did not render through the final local option.");
            Log.Info("NATIVE-SMOKE PASS: entry without external menu, Harmony hooks, chat icon, bottom-up timeline, channel colors, brown hold wheel, FDL cards, message builder/preview/place picker, train/crew/distance popups, Preferences Mods section.");
            entry.OnWorldUnload();yield return null;
            if(UnityEngine.Object.FindObjectsByType<CommsOwnedText>(FindObjectsSortMode.None).Length!=0)throw new Exception("Canvas survived world unload.");
            Log.Info("NATIVE-SMOKE PASS: world unload removed owned canvas.");
        }
        finally { Application.Quit(); }
    }
}
