// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UI.Windows;
namespace EnusDE.Communications
{
    public sealed partial class CommsUI:MonoBehaviour
    {
        CommsRuntime runtime;Entry entry;UiFactory f;GameObject canvasObject,toolbarCanvasObject;Canvas canvas,toolbarCanvas;GraphicRaycaster raycaster,toolbarRaycaster;RectTransform root,toolbarRoot,chat,viewport,content,inputBox,top,popup,dispatch,deskAccess;
        readonly HoldWheelController actionMenu=new HoldWheelController();RectTransform wheel;Button[] wheelButtons;TMP_Text wheelLabel;int wheelOpenedFrame;
        ScrollRect scroll,dispatchScroll;RectTransform dispatchContent,nativeToolbar,topSlot;Image chatHit;TMP_InputField input,dispatchInput;TMP_Text target,status,dispatchTitle;Button[] channelButtons;Button fdlButton,replyButton,dispatchSend,topButton;
        readonly List<Tuple<CanvasGroup,VisibleRecord>> visibleRows=new List<Tuple<CanvasGroup,VisibleRecord>>();
        readonly Dictionary<string,int> unread=new Dictionary<string,int>();
        Track.Signals.CTCPanelButton[] deskButtons;float nextDeskScan,nextDeskCheck;
        Image[] channelMarkers;string timelineSignature,dispatchSignature;
        readonly Vector3[] topCorners=new Vector3[4];
        string channel="train",thread,replyMessageId,pendingReplyRequest,popupKind,lastCandidate;bool dirty=true,layoutDirty,historyFocus,drawMessages=true,cursorOwned;CursorLockMode previousLock;bool previousVisible;
        float toastUntil,nextDock,nextHud,nextPopupRefresh,toolbarExtra;Action popupRefresh;Vector2 lastResolution;double previousTime;
        public bool Dragging;
        bool dispatchAsOperator;
        Button dispatchAcknowledge;
        RectTransform roleTestBar;TMP_Text roleTestLabel;
        internal double FadeClock {get;private set;}
        internal float Scale=>canvas!=null?canvas.scaleFactor:1;
        internal bool WheelOpen=>actionMenu?.Active==true;
        internal bool ChatShown=>runtime.IsRoleTest||ChatVisibility.IsVisible(GameAdapter.MultiplayerActive,drawMessages);
        internal bool Captures=>!runtime.DiagnosticSuspended&&(Dragging||WheelOpen||popup!=null||historyFocus||OwnTextFocused());
        string T(string key)=>entry.Text.Get(key);
        internal void Initialize(CommsRuntime owner)
        {
            runtime=owner;entry=owner.Entry;
            TMP_FontAsset font=TMP_Settings.defaultFontAsset;
            if(font==null)font=Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(x=>x!=null);
            if(font==null)throw new InvalidOperationException("No game font available.");f=new UiFactory(font){Owner=this};
            canvasObject=new GameObject("EnusDE.Communications.Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(CommsOwnedText));canvasObject.layer=5;
            canvas=canvasObject.GetComponent<Canvas>();raycaster=canvasObject.GetComponent<GraphicRaycaster>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=70;root=canvasObject.GetComponent<RectTransform>();
            var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=.5f;
            BuildChat();BuildTop();deskAccess=(RectTransform)f.Button(root,T("desk_communications"),(1920-230)/2,48,230,34,()=>ShowDispatchList()).transform;deskAccess.gameObject.SetActive(false);previousTime=Time.realtimeSinceStartupAsDouble;
        }
        void BuildChat()
        {
            var s=entry.Settings;chat=f.Rect("chat",root,s.ChatX,0,s.ChatWidth,s.ChatHeight+56);chat.anchorMin=chat.anchorMax=Vector2.zero;chat.pivot=Vector2.zero;chat.anchoredPosition=new Vector2(s.ChatX,s.ChatY);
            f.Background(chat,new Color(s.BackgroundR,s.BackgroundG,s.BackgroundB,s.BackgroundOpacity)).raycastTarget=false;
            target=f.Text(chat,T("no_train"),8,s.ChatHeight+2,s.ChatWidth-106,20,13);
            var quick=f.Button(chat,T("wheel_short"),s.ChatWidth-85,s.ChatHeight+2,56,20,TryWheel,true);
            quick.gameObject.AddComponent<UI.Tooltips.UITooltipProvider>().DynamicTooltipInfo=()=>new TooltipInfo(T("wheel_title"),T("wheel_hint"));
            var handle=f.Button(chat,"+",s.ChatWidth-25,s.ChatHeight+2,25,20,()=>{draftRecipient=channel;ShowMessageBuilder();},true);
            handle.gameObject.AddComponent<UI.Tooltips.UITooltipProvider>().DynamicTooltipInfo=()=>new TooltipInfo(T("builder_title"),T("builder_open_hint"));
            var drag=handle.gameObject.AddComponent<CommsDrag>();drag.Target=chat;drag.Owner=this;drag.Done=SaveChatPosition;
            viewport=f.Rect("messages",chat,6,0,s.ChatWidth-12,s.ChatHeight);chatHit=f.Background(viewport,Color.clear);viewport.gameObject.AddComponent<RectMask2D>();
            scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=25;
            content=f.Rect("rows",viewport,0,0,s.ChatWidth-12,s.ChatHeight);scroll.content=content;
            var bar=f.Rect("channels",chat,0,s.ChatHeight+24,s.ChatWidth,30);
            channelButtons=new Button[3];channelMarkers=new Image[3];string[] channels={"global","crew","train"};
            for(int i=0;i<3;i++){string selected=channels[i];channelButtons[i]=f.Button(bar,T(selected),i*75,0,72,28,()=>SelectChannel(selected),true);channelMarkers[i]=f.Background(f.Rect("selected channel",bar,i*75+4,26,64,2),UiFactory.ChannelColor(selected));channelMarkers[i].raycastTarget=false;}
            f.Button(bar,"...",228,0,30,28,()=>{if(channel=="crew"&&runtime.Client.State?.Admin==true)ShowCrewView();else ShowChooseTrain();},true);
            f.Button(bar,T("settings"),261,0,73,28,()=>entry.OpenSettings(),true);
            fdlButton=f.Button(bar,"FDL",337,0,45,28,()=>ShowDispatchList(),true);
            replyButton=f.Button(bar,T("reply"),337,0,65,28,()=>ShowDispatchList(),true);
            replyButton.gameObject.SetActive(false);
            inputBox=f.Rect("compose",chat,0,s.ChatHeight+56,s.ChatWidth,34);
            input=f.Input(inputBox,0,0,s.ChatWidth-70,32,T("write"),Submit);
            f.Button(inputBox,T("send"),s.ChatWidth-65,0,65,32,()=>Submit(input.text));inputBox.gameObject.SetActive(false);
            f.Button(bar,"Aa",s.ChatWidth-28,0,28,28,()=>FocusInput(),true);
        }
        void BuildTop()
        {
            if(!runtime.IsRoleTest)BuildToolbarIcon();else BuildRoleTestBar();drawMessages=entry.Settings.ChatVisible;status=f.Text(root,"",16,48,650,50,15);
        }
        void BuildToolbarIcon()
        {
            if(toolbarRoot==null)
            {
                toolbarCanvasObject=new GameObject("EnusDE.Communications.ToolbarCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(CommsOwnedText));toolbarCanvasObject.layer=5;
                toolbarCanvas=toolbarCanvasObject.GetComponent<Canvas>();toolbarCanvas.renderMode=RenderMode.ScreenSpaceOverlay;toolbarCanvas.sortingOrder=70;
                toolbarRaycaster=toolbarCanvasObject.GetComponent<GraphicRaycaster>();toolbarRoot=toolbarCanvasObject.GetComponent<RectTransform>();
                var scale=toolbarCanvasObject.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scale.matchWidthOrHeight=.5f;
            }
            top=f.Rect("communication toolbar",toolbarRoot,0,0,36,32);
            var group=top.gameObject.AddComponent<CanvasGroup>();group.alpha=1;group.interactable=true;group.blocksRaycasts=true;group.ignoreParentGroups=true;
            topButton=f.Button(top,"",0,0,36,32,()=>{entry.Log.Info("Chat toolbar clicked; multiplayer="+GameAdapter.MultiplayerActive+".");if(GameAdapter.MultiplayerActive){FocusInput();return;}drawMessages=!drawMessages;entry.Settings.ChatVisible=drawMessages;entry.Save(false);dirty=true;},true);
            f.Fill((RectTransform)topButton.transform);f.Fill(topButton.GetComponentInChildren<TMP_Text>().rectTransform,3);
            topButton.name="communication icon";var icon=f.Rect("speech bubble",topButton.transform,3,2,30,28).gameObject.AddComponent<ChatIcon>();icon.raycastTarget=true;icon.color=UiFactory.Ink;f.Fill(icon.rectTransform,3);topButton.targetGraphic=icon;
            top.gameObject.AddComponent<ToolbarWindowFilter>();
            topButton.gameObject.AddComponent<UI.Tooltips.UITooltipProvider>().DynamicTooltipInfo=()=>new TooltipInfo(T("communications"),T("chat_icon_hint"));
            top.gameObject.SetActive(false);
        }
        void SaveChatPosition(){entry.Settings.ChatX=chat.anchoredPosition.x;entry.Settings.ChatY=chat.anchoredPosition.y;entry.Settings.AutoDock=false;entry.Save(false);}
        internal void WindowMoved(RectTransform window)
        {
            if(window==dispatch||window==popup&&popupKind=="dispatch"){entry.Settings.DispatchX=window.anchoredPosition.x;entry.Settings.DispatchY=-window.anchoredPosition.y;}
            else if(window==popup&&popupKind=="archive"){entry.Settings.ArchiveX=window.anchoredPosition.x;entry.Settings.ArchiveY=-window.anchoredPosition.y;}
            else return;entry.Save(false);
        }
        internal void Dirty(){dirty=true;}
        internal void ApplySettings(){layoutDirty=true;}
        void RebuildLayout()
        {
            using var measurement=entry.Performance.Measure(PerformanceStage.Rebuild);
            string draft=input?.text,activeThread=thread,activeReply=replyMessageId,dispatchDraft=dispatchInput?.text;bool compose=inputBox!=null&&inputBox.gameObject.activeSelf,reading=historyFocus,operatorThread=dispatchAsOperator;
            layoutDirty=false;CancelWheel();ClosePopup();CloseDispatch();DetachTop();
            foreach(var old in new[]{chat,top,status?.rectTransform})if(old!=null){old.gameObject.SetActive(false);Destroy(old.gameObject);}
            visibleRows.Clear();timelineSignature=null;BuildChat();BuildTop();input.text=draft??"";SetCompose(compose);historyFocus=reading;if(reading)input.ActivateInputField();Clamp(chat);dirty=true;nextHud=nextDock=0;
            if(activeReply!=null)ShowReply(activeReply);else if(activeThread!=null&&(operatorThread||runtime.Client.State?.Dispatcher==true))ShowConversation(activeThread,operatorThread);
            if(dispatchInput!=null)dispatchInput.text=dispatchDraft??"";
        }
        internal void ResetSession(){CancelWheel();historyFocus=false;unread.Clear();CloseDispatch();ClosePopup();dirty=true;}
        string Target()=>channel=="global"?"global":channel=="crew"?runtime.Client.State?.Crew:runtime.Client.State?.Train;
        void SelectChannel(string value)
        {
            channel=value;unread[value]=0;historyFocus=false;drawMessages=true;input.text="";input.DeactivateInputField();SetCompose(false);dirty=true;
            if(value=="train"&&String.IsNullOrEmpty(runtime.Client.State?.Train))ShowChooseTrain();
        }
        void SetCompose(bool shown){inputBox.gameObject.SetActive(shown);chat.sizeDelta=new Vector2(entry.Settings.ChatWidth,entry.Settings.ChatHeight+56+(shown?36:0));}
        void FocusInput(){historyFocus=true;drawMessages=true;SetCompose(true);input.ActivateInputField();dirty=true;}
        void Submit(string value)
        {
            if(!String.IsNullOrWhiteSpace(value))runtime.Say(channel,Target(),value);
            input.text="";input.DeactivateInputField();if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);historyFocus=false;SetCompose(false);dirty=true;
        }
        bool OwnTextFocused()
        {var selected=EventSystem.current?.currentSelectedGameObject;return selected!=null&&selected.activeInHierarchy&&selected.GetComponent<TMP_InputField>()?.isFocused==true&&selected.GetComponentInParent<CommsOwnedText>()!=null;}
        bool OtherTextFocused()
        {var selected=EventSystem.current?.currentSelectedGameObject;return selected!=null&&selected.activeInHierarchy&&selected.GetComponent<TMP_InputField>()?.isFocused==true;}
        internal void Toast(string text){if(status==null)return;status.text=text;toastUntil=Time.realtimeSinceStartup+6;}
        void Rebuild()
        {
            using var measurement=entry.Performance.Measure(PerformanceStage.Rebuild);
            dirty=false;chat.gameObject.SetActive(ChatShown);chatHit.raycastTarget=historyFocus;scroll.enabled=historyFocus;
            var state=runtime.Client.State;string name=state?.TrainLabel;
            target.text=T("radial_target")+" "+(String.IsNullOrEmpty(name)?T("no_train"):name)+(runtime.RoutePending?" · "+T("connecting"):"");
            string[] labels={"global","crew","train"};
            for(int i=0;i<3;i++)
            {
                string key=labels[i];int count;unread.TryGetValue(key,out count);var caption=channelButtons[i].GetComponentInChildren<TMP_Text>();caption.text=T(key)+(count>0?" ("+count+")":"");
                var tint=UiFactory.ChannelColor(key);caption.color=tint;caption.fontStyle=channel==key?FontStyles.Bold:FontStyles.Normal;channelMarkers[i].gameObject.SetActive(channel==key);
                channelButtons[i].GetComponent<Image>().color=channel==key?new Color(tint.r,tint.g,tint.b,.13f):Color.clear;
            }
            input.textComponent.color=UiFactory.ChannelColor(channel);
            fdlButton.gameObject.SetActive(true);fdlButton.GetComponentInChildren<TMP_Text>().text="FDL"+(runtime.Client.PendingDispatch.Count>0?" ("+runtime.Client.PendingDispatch.Count+")":"");replyButton.gameObject.SetActive(false);
            var items=ChatTimeline.Compose(runtime.Client,channel,Target(),historyFocus,runtime.LocalId);
            string signature=channel+":"+Target()+":"+historyFocus+":"+String.Join(";",items.Select(i=>i.Row.Message.Id+":"+i.Kind));
            if(signature!=timelineSignature){timelineSignature=signature;RebuildRows(items);}
            if(dispatch!=null&&(replyMessageId!=null?!runtime.Client.PendingDispatch.ContainsKey(replyMessageId):dispatchAsOperator?thread!="fdl:"+state?.Train:state?.Dispatcher!=true))CloseDispatch();
            if(dispatch!=null)RefreshDispatch();
            if(!runtime.Client.Connected)Toast(T(runtime.HostUnavailable?"host_unavailable":"connecting"));else if(runtime.Client.Error!=null)Toast(T("error."+runtime.Client.Error));
        }
        void RebuildRows(ChatItem[] items)
        {
            foreach(Transform child in content){child.gameObject.SetActive(false);Destroy(child.gameObject);}visibleRows.Clear();
            float y=0,width=entry.Settings.ChatWidth-12;bool atBottom=scroll.verticalNormalizedPosition<.02f;var placed=new List<RectTransform>();
            foreach(var item in items)
            {
                var row=item.Row;var record=row.Message;RectTransform rect;
                if(item.Kind==ChatItemKind.Message)
                {
                    string text=entry.Text.Message(record);float h=Math.Max(23,Measure(text,entry.Settings.FontSize,width));
                    rect=f.Rect("message",content,0,y,width,h+20);var group=rect.gameObject.AddComponent<CanvasGroup>();
                    f.Text(rect,"["+T(record.Channel=="dispatch"?"fdl":record.Channel)+"] "+record.SenderName+(entry.Settings.ShowTimestamps?" · "+record.Utc.ToLocalTime().ToString("HH:mm"):""),0,0,width,18,12).color=UiFactory.ChannelColor(record.Channel);
                    var label=f.Text(rect,text,0,18,width,h,entry.Settings.FontSize);label.overflowMode=TextOverflowModes.Overflow;label.color=UiFactory.ChannelColor(record.Channel);
                    visibleRows.Add(Tuple.Create(group,row));y+=h+24;
                }
                else
                {
                    string id=record.Id,train=record.Target.Substring(4);bool reply=item.Kind==ChatItemKind.Reply;
                    string text="FDL "+(record.FromDispatcher?"→ ":"← ")+record.TargetLabel+(record.SenderId==runtime.LocalId?" · "+T("own_message"):"")+"\n"+entry.Text.Message(record)+" · "+T(reply?"reply":"open_conversation")+" ›";
                    float h=Math.Max(48,Measure(text,entry.Settings.FontSize,width-12)+10);
                    var button=f.Button(content,text,0,y,width,h,()=>{if(reply)ShowReply(id);else runtime.Send(runtime.Client.Request("contact",target:train));},true);rect=(RectTransform)button.transform;
                    var caption=button.GetComponentInChildren<TMP_Text>();caption.fontSize=entry.Settings.FontSize;caption.alignment=TextAlignmentOptions.TopLeft;caption.color=UiFactory.ChannelColor("dispatch");
                    visibleRows.Add(Tuple.Create(rect.gameObject.AddComponent<CanvasGroup>(),row));y+=h+4;
                }
                placed.Add(rect);
            }
            float padding=ChatTimeline.BottomPadding(entry.Settings.ChatHeight,y);foreach(var row in placed)row.anchoredPosition+=new Vector2(0,-padding);
            content.sizeDelta=new Vector2(width,Math.Max(y,entry.Settings.ChatHeight));if(!historyFocus||atBottom)scroll.verticalNormalizedPosition=0;
        }
        float Measure(string text,float size,float width)
        {
            // Uses the game's font metrics, including non-Latin fallback glyphs.
            var temp=target;float old=temp.fontSize;temp.fontSize=size;float height=temp.GetPreferredValues(text,width,10000).y;temp.fontSize=old;return height;
        }
        void LateUpdate()
        {
            if(canvasObject==null||!runtime.Active)return;
            if(entry.RoleTestActive&&!runtime.IsRoleTest||runtime.DiagnosticSuspended){canvas.enabled=false;if(toolbarCanvas!=null)toolbarCanvas.enabled=false;raycaster.enabled=false;if(toolbarRaycaster!=null)toolbarRaycaster.enabled=false;return;}
            using var measurement=entry.Performance.Measure(PerformanceStage.UI);
            // Native windows cover only their own area. Free chat controls remain interactive.
            bool editing=entry.SettingsOpen;canvas.enabled=true;raycaster.enabled=true;canvas.sortingOrder=GameMenus.ChatOrder;
            if(editing)CancelWheel();
            bool reading=input!=null&&input.isFocused;
            if(historyFocus!=reading){historyFocus=reading;dirty=true;}
            if(layoutDirty)RebuildLayout();
            if(chat.gameObject.activeSelf!=ChatShown)dirty=true;
            double now=Time.realtimeSinceStartupAsDouble,elapsed=Math.Min(.5,Math.Max(0,now-previousTime));previousTime=now;
            FadeClock=ChatPresentation.Advance(FadeClock,elapsed,Application.isFocused,historyFocus);
            // A state/ping update must not replace a pressed button before PointerUp.
            bool pressed=Mouse.current!=null&&Mouse.current.leftButton.isPressed;
            if(dirty&&!pressed&&!Dragging)Rebuild();
            foreach(var row in visibleRows){float alpha=ChatPresentation.Opacity(row.Item2.Received,FadeClock,entry.Settings.FadeSeconds,historyFocus);if(Math.Abs(row.Item1.alpha-alpha)>.002f)row.Item1.alpha=alpha;row.Item1.blocksRaycasts=alpha>.01f;}
            if(Time.realtimeSinceStartup>toastUntil)status.text="";
            var size=root.rect.size;if(size!=lastResolution){lastResolution=size;Clamp(chat);if(dispatch!=null)Clamp(dispatch);if(popup!=null)Clamp(popup);}
            if(!runtime.IsRoleTest){if(Time.realtimeSinceStartup>=nextDock){DockTop();nextDock=Time.realtimeSinceStartup+2;}AlignTopToSlot();}
            if(roleTestLabel!=null)roleTestLabel.text=T("test_active")+" · "+T(runtime.RoleTest.Role==TestChatRole.Driver?"test_driver":runtime.RoleTest.Role==TestChatRole.Dispatcher?"test_dispatcher":"test_admin");
            string candidate=GameAdapter.Candidate();if(Time.realtimeSinceStartup>=nextHud||candidate!=lastCandidate){if(ChatShown&&entry.Settings.AutoDock&&!Dragging&&!pressed){chat.anchoredPosition=new Vector2(entry.Settings.ChatX,NativeDock.ChatBottom(root));Clamp(chat);}lastCandidate=candidate;nextHud=Time.realtimeSinceStartup+.2f;}
            if(!runtime.IsRoleTest&&Time.realtimeSinceStartup>=nextDeskCheck){UpdateDeskAccess();nextDeskCheck=Time.realtimeSinceStartup+.5f;}
            if(popupRefresh!=null&&!pressed&&!Dragging&&Time.realtimeSinceStartup>=nextPopupRefresh){popupRefresh();nextPopupRefresh=Time.realtimeSinceStartup+1;}
            if(pendingReplyRequest!=null&&!runtime.IsPending(pendingReplyRequest)){pendingReplyRequest=null;if(dispatchInput!=null)dispatchInput.interactable=true;if(dispatchSend!=null)dispatchSend.interactable=true;}
            if(!entry.BindingConflict&&entry.ChatPressed&&!BlockWheelContext()&&dispatch==null)FocusInput();
            // If bindings collide, train communication keeps its key; only chat input is blocked.
            if(!entry.SettingsOpen&&entry.WheelPressed)OpenWheel(true);
            if(WheelOpen)
            {
                if(BlockWheelContext()||runtime.RoutePending){CancelWheel();return;}
                Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
                int sector=-1;
                if(Mouse.current!=null&&Time.frameCount>wheelOpenedFrame)
                {Vector2 point;RectTransformUtility.ScreenPointToLocalPointInRectangle(wheel,Mouse.current.position.ReadValue(),null,out point);sector=HoldWheelController.Sector(point.x-175,point.y+175);}
                actionMenu.Hover(sector);HighlightWheel();
                string selected;var result=actionMenu.Tick(MenuState(),entry.WheelHeld,entry.WheelReleased,entry.WheelInputReady,out selected);FinishWheel(result,selected);
            }
        }
        internal void PrepareDiagnosticPause()
        {
            CancelWheel();ClosePopup();CloseDispatch();Dragging=false;historyFocus=false;
            input.DeactivateInputField();SetCompose(false);
            var selected=EventSystem.current?.currentSelectedGameObject;
            if(selected!=null&&selected.GetComponentInParent<CommsOwnedText>()!=null)EventSystem.current.SetSelectedGameObject(null);
            dirty=true;
        }
        internal void SuspendForRoleTest()
        {
            CancelWheel();Dragging=false;historyFocus=false;input.DeactivateInputField();dispatchInput?.DeactivateInputField();
            var selected=EventSystem.current?.currentSelectedGameObject;
            if(selected!=null&&selected.GetComponentInParent<CommsOwnedText>()!=null)EventSystem.current.SetSelectedGameObject(null);
        }
        internal bool HandleEscape()
        {
            if(WheelOpen){CancelWheel();return true;}if(popup!=null){ClosePopup();return true;}
            if(OwnTextFocused()||historyFocus){input.DeactivateInputField();if(dispatchInput!=null)dispatchInput.DeactivateInputField();if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);historyFocus=false;SetCompose(false);dirty=true;return true;}
            if(dispatch!=null){CloseDispatch();return true;}return false;
        }
        bool BlockWheelContext()
        {
            if(popup!=null||entry.SettingsOpen||OtherTextFocused()||PauseMenu.IsPaused||!Application.isFocused)return true;
            if(UI.ContextMenu.ContextMenu.IsShown)return true;
            return Resources.FindObjectsOfTypeAll<UI.PreferencesWindow.PreferencesWindow>().Any(p=>p.gameObject.activeInHierarchy&&p.GetComponentInParent<Window>()?.IsShown==true);
        }
        static readonly string[] WheelSignals={"stop","forward","reverse","slow","distance","whistle","switch","understood"};
        void TryWheel()=>OpenWheel(false);
        void OpenWheel(bool hold)
        {
            if(WheelOpen){CancelWheel();return;}
            // F10 is an explicit menu command, even if our chat field retained focus.
            // Draft text is preserved; ordinary letter bindings still respect text entry.
            if(entry.WheelFunctionKey&&OwnTextFocused()&&popup==null&&!entry.SettingsOpen)
            {input.DeactivateInputField();dispatchInput?.DeactivateInputField();if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);historyFocus=false;SetCompose(false);}
            if(BlockWheelContext()){Toast(T("wheel_input_blocked"));entry.Log.Info("Train menu request blocked by a focused input, another game window, pause or missing application focus.");return;}
            if(runtime.RoutePending){Toast(T("connecting"));return;}
            if(!runtime.Client.Connected){Toast(T(runtime.HostUnavailable?"host_unavailable":"connecting"));return;}
            if(String.IsNullOrEmpty(runtime.Client.State?.Train)){Toast(T("wheel_needs_train"));entry.Log.Info("Train menu request requires explicit train-channel enrollment; opening selection.");ShowChooseTrain();return;}
            historyFocus=false;input.DeactivateInputField();SetCompose(false);
            BuildWheel(hold);
        }
        MenuContext MenuState()=>new MenuContext{Train=runtime.Client.State?.Train,Token=runtime.Client.State?.Token,Version=runtime.Client.State?.Version??0};
        void BuildWheel(bool hold)
        {
            if(!actionMenu.Open(MenuState(),WheelSignals,hold))return;
            previousLock=Cursor.lockState;previousVisible=Cursor.visible;cursorOwned=true;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            wheel=f.Rect("communication wheel",root,(root.rect.width-350)/2,(root.rect.height-350)/2,350,350);wheelOpenedFrame=Time.frameCount;
            wheelButtons=new Button[8];int generation=actionMenu.Generation;
            for(int i=0;i<8;i++)
            {
                int index=i;double angle=i*Math.PI/4;float x=175+(float)Math.Sin(angle)*130-45,y=175-(float)Math.Cos(angle)*130-25;
                var button=f.Button(wheel,T("signal."+WheelSignals[i]),x,y,90,50,()=>{string choice;var result=actionMenu.Click(MenuState(),index,generation,out choice);FinishWheel(result,choice);});wheelButtons[i]=button;
                var outline=button.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.61f,.55f,.39f,.9f);outline.effectDistance=new Vector2(1,-1);outline.useGraphicAlpha=false;
                var colors=button.colors;colors.normalColor=colors.highlightedColor=colors.selectedColor=Color.white;colors.pressedColor=new Color(.8f,.75f,.6f);button.colors=colors;
            }
            var center=f.Rect("train target",wheel,114,146,122,60);f.Background(center,UiFactory.Panel).raycastTarget=false;
            wheelLabel=f.Text(center,runtime.Client.State.TrainLabel,4,5,114,50,14);wheelLabel.alignment=TextAlignmentOptions.Center;HighlightWheel();
            entry.Log.Info("Communication wheel opened; hold="+hold+"; host="+GameAdapter.IsHost+".");
        }
        void HighlightWheel()
        {
            if(wheelButtons==null)return;
            for(int i=0;i<wheelButtons.Length;i++){bool active=i==actionMenu.Selected;var button=wheelButtons[i];button.GetComponent<Image>().color=active?UiFactory.WheelSelected:UiFactory.WheelBrown;button.GetComponentInChildren<TMP_Text>().color=active?UiFactory.WheelGold:UiFactory.Ink;}
            wheelLabel.text=actionMenu.Selected>=0?T("signal."+WheelSignals[actionMenu.Selected]):runtime.Client.State?.TrainLabel;
        }
        void FinishWheel(MenuResult result,string selected)
        {
            if(result==MenuResult.None)return;CancelWheel();
            if(result==MenuResult.Selected)SendSignal(selected);else if(result==MenuResult.ContextChanged)Toast(T("error.membership_changed"));
        }
        void SendSignal(string signal)
        {
            if(runtime.RoutePending||String.IsNullOrEmpty(runtime.Client.State?.Train)){Toast(T("wheel_needs_train"));return;}
            if(signal=="distance"){ShowDistances();return;}
            runtime.Say("train",runtime.Client.State.Train,null,signal);
        }
        void ShowDistances()
        {
            if(String.IsNullOrEmpty(runtime.Client.State?.Train))return;
            var context=MenuState();ClosePopup();popup=f.Window(root,T("signal.distance"),400,190,ClosePopup);Center(popup);
            f.Text(popup,runtime.Client.State.TrainLabel,12,36,376,26,14);
            string[] signals={"one","two","five"};for(int i=0;i<signals.Length;i++){string signal=signals[i];f.Button(popup,T("signal."+signal),12,68+i*34,376,30,()=>{ClosePopup();if(context.Matches(MenuState()))SendSignal(signal);else Toast(T("error.membership_changed"));});}
        }
        internal void CancelWheel()
        {
            actionMenu?.Close();
            if(wheel!=null){wheel.gameObject.SetActive(false);Destroy(wheel.gameObject);}wheel=null;wheelButtons=null;wheelLabel=null;
            if(cursorOwned){cursorOwned=false;if(!PauseMenu.IsPaused&&!OtherTextFocused()&&!UI.ContextMenu.ContextMenu.IsShown){Cursor.lockState=previousLock;Cursor.visible=previousVisible;}}
        }
        internal void ShowChooseTrain(bool contact=false)
        {
            ClosePopup();CancelWheel();popup=f.Window(root,T(contact?"contact_train":"choose_train"),540,440,ClosePopup);Center(popup);
            popupKind=contact?"dispatch":"train";if(contact)PlaceDispatch(popup);
            string query="";int page=0;RectTransform area=f.Rect("train list",popup,12,118,516,238);f.Background(area,Color.clear);area.gameObject.AddComponent<RectMask2D>();var list=f.Rect("train rows",area,0,0,516,238);
            string lastSignature=null;Button previous=null,next=null;TMP_Text pageLabel=null;
            Action rebuild=null;rebuild=()=>
            {
                if(list==null)return;
                var paging=new TrainPage(runtime.View.Trains.Values,query,page);page=paging.Index;var trains=paging.Items;
                if(previous!=null){previous.interactable=paging.Previous;next.interactable=paging.Next;pageLabel.text=T("page")+" "+(page+1)+" / "+paging.Pages+" · "+paging.Total;}
                string signature=query+"|"+page+"|"+runtime.Client.State?.Train+"|"+runtime.Client.State?.ActualCrew+"|"+runtime.Client.State?.Admin+"|"+runtime.Client.State?.Dispatcher+"|"+String.Join(";",trains.Select(t=>t.Id+":"+t.Label+":"+t.Crew+":"+SelectedPlayers(t.Id)));
                if(signature==lastSignature)return;lastSignature=signature;
                foreach(Transform child in list){child.gameObject.SetActive(false);Destroy(child.gameObject);}float y=0;
                foreach(var train in trains)
                {
                    var picked=train;string crew=String.IsNullOrEmpty(train.Crew)?T("open_train"):runtime.View.Crews.ContainsKey(train.Crew)?runtime.View.Crews[train.Crew]:train.Crew;
                    bool allowed=contact?runtime.Client.State?.Dispatcher==true:runtime.Client.State?.Admin==true||String.IsNullOrEmpty(train.Crew)||train.Crew==runtime.Client.State?.ActualCrew;
                    string players=SelectedPlayers(train.Id),selected=runtime.Client.State?.Train==train.Id?" · "+T("connected_train"):"";
                    string label=train.Label+" · "+crew+selected+(String.IsNullOrEmpty(players)?"":"\n[P] "+T("active_players")+": "+players);
                    var button=f.Button(list,label,0,y,516,54,()=>{runtime.Send(runtime.Client.Request(contact?"contact":"join",target:picked.Id));ClosePopup();});button.interactable=allowed;button.GetComponentInChildren<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;y+=58;
                }
                if(trains.Length==0)f.Text(list,T("no_trains_found"),4,8,508,36,14);
            };
            previous=f.Button(popup,"‹ "+T("previous"),12,362,126,30,()=>{page--;rebuild();});
            next=f.Button(popup,T("next")+" ›",402,362,126,30,()=>{page++;rebuild();});pageLabel=f.Text(popup,"",148,367,244,24,13);pageLabel.alignment=TextAlignmentOptions.Center;
            f.Input(popup,12,76,516,32,T("search"),null,value=>{query=value;page=0;rebuild();});
            string candidate=GameAdapter.Candidate();WorkingTrain current;
            if(!contact&&candidate!=null&&runtime.View.Trains.TryGetValue(candidate,out current))f.Button(popup,T("connect_selected")+": "+current.Label,12,36,516,32,()=>{runtime.Send(runtime.Client.Request("join",target:candidate));ClosePopup();});
            else f.Text(popup,T(contact?"fdl_hint":"select_engine_hint"),12,36,516,36,13);
            rebuild();popupRefresh=rebuild;
            if(!contact)f.Button(popup,T("leave_train"),12,400,516,30,()=>{runtime.Send(runtime.Client.Request("leave"));ClosePopup();});
            else if(runtime.Client.State?.Train!=null)f.Button(popup,T("operator_contact"),12,400,516,30,OpenOperatorConversation);
            else f.Text(popup,T("fdl_history_hint"),12,400,516,30,13).color=UiFactory.Muted;
        }
        string SelectedPlayers(string train)=>String.Join(", ",runtime.View.People.Values.Where(p=>p.SelectedCar==train).Select(p=>{bool compatible;return p.Name+(runtime.Client.State?.PeerChat!=null&&runtime.Client.State.PeerChat.TryGetValue(p.Id,out compatible)&&!compatible?" ["+T("needs_chat")+"]":"");}).OrderBy(x=>x));
        void PlaceDispatch(RectTransform window){window.anchoredPosition=new Vector2(entry.Settings.DispatchX,-entry.Settings.DispatchY);ClampTop(window);}
        void ShowCrewView()
        {
            if(runtime.Client.State?.Admin!=true)return;ClosePopup();popup=f.Window(root,T("crew"),460,430,ClosePopup);Center(popup);
            f.Button(popup,T("follow_crew"),12,38,436,32,()=>{runtime.Send(runtime.Client.Request("crew",target:null));ClosePopup();});
            var area=f.Rect("crews",popup,12,80,436,336);f.Background(area,Color.clear);area.gameObject.AddComponent<RectMask2D>();var sc=area.gameObject.AddComponent<ScrollRect>();sc.horizontal=false;sc.vertical=true;sc.viewport=area;var rows=f.Rect("crew rows",area,0,0,436,336);sc.content=rows;float y=0;
            foreach(var crew in runtime.View.Crews.OrderBy(pair=>pair.Value)){string id=crew.Key;f.Button(rows,crew.Value,0,y,436,32,()=>{runtime.Send(runtime.Client.Request("crew",target:id));ClosePopup();});y+=36;}rows.sizeDelta=new Vector2(436,Math.Max(336,y));sc.verticalNormalizedPosition=1;
        }
        internal void ShowDispatchList()
        {
            bool dispatcher=runtime.Client.State?.Dispatcher==true;
            if(!dispatcher){OpenOperatorConversation();return;}
            if(dispatcher&&runtime.Client.PendingDispatch.Count==0){CloseDispatch();ShowChooseTrain(true);return;}
            if(!dispatcher&&runtime.Client.PendingDispatch.Count==1){ShowReply(runtime.Client.PendingDispatch.Keys.First());return;}
            if(runtime.Client.PendingDispatch.Count==0)return;
            ClosePopup();popup=f.Window(root,T("reply"),490,420,ClosePopup);popupKind="dispatch";PlaceDispatch(popup);
            if(dispatcher)f.Button(popup,T("contact_train"),12,38,466,30,()=>{ClosePopup();CloseDispatch();ShowChooseTrain(true);});
            var area=f.Rect("requests",popup,12,dispatcher?76:42,466,dispatcher?332:366);f.Background(area,Color.clear);area.gameObject.AddComponent<RectMask2D>();var sc=area.gameObject.AddComponent<ScrollRect>();sc.horizontal=false;sc.vertical=true;sc.viewport=area;var rows=f.Rect("request rows",area,0,0,466,366);sc.content=rows;float y=0;
            foreach(var notification in runtime.Client.PendingDispatch.Values.OrderBy(r=>r.Message.Utc)){var message=notification.Message;string id=message.Id;f.Button(rows,message.TargetLabel+": "+entry.Text.Message(message),0,y,466,48,()=>{ClosePopup();ShowReply(id);});y+=52;}rows.sizeDelta=new Vector2(466,Math.Max(366,y));sc.verticalNormalizedPosition=1;
        }
        void OpenOperatorConversation()
        {
            string train=runtime.Client.State?.Train;if(String.IsNullOrEmpty(train)){Toast(T("wheel_needs_train"));ShowChooseTrain();return;}
            ClosePopup();runtime.Send(runtime.Client.Request("contact_operator",target:train));
        }
        void ShowConversation(string selected,bool asOperator=false)
        {
            if(selected==null||(asOperator?selected!="fdl:"+runtime.Client.State?.Train:runtime.Client.State?.Dispatcher!=true))return;CloseDispatch();thread=selected;dispatchAsOperator=asOperator;
            dispatch=f.Window(root,T(asOperator?"operator_contact":"fdl"),410,366,CloseDispatch);PlaceDispatch(dispatch);
            f.Button(dispatch,"< "+T("back"),12,38,84,25,()=>{CloseDispatch();if(!asOperator)ShowChooseTrain(true);});dispatchTitle=f.Text(dispatch,"",102,38,296,24,14);
            var area=f.Rect("dispatch viewport",dispatch,12,68,386,168);f.Background(area,Color.clear);area.gameObject.AddComponent<RectMask2D>();dispatchScroll=area.gameObject.AddComponent<ScrollRect>();dispatchScroll.horizontal=false;dispatchScroll.vertical=true;dispatchScroll.viewport=area;dispatchScroll.movementType=ScrollRect.MovementType.Clamped;
            dispatchContent=f.Rect("dispatch rows",area,0,0,386,168);dispatchScroll.content=dispatchContent;
            if(!asOperator){f.Button(dispatch,T("signal.wait"),12,244,120,30,()=>SendDispatchSignal("wait"));f.Button(dispatch,T("signal.understood"),138,244,130,30,()=>SendDispatchSignal("understood"));f.Button(dispatch,T("signal.report"),274,244,124,30,()=>SendDispatchSignal("report"));}
            else
            {
                dispatchAcknowledge=f.Button(dispatch,T("acknowledge"),12,244,150,30,()=>{var pending=ConversationPending();if(pending!=null)runtime.Send(runtime.Client.Request("send","dispatch",thread,signal:"understood",replyTo:pending.Message.Id));});
                f.Text(dispatch,T("operator_persistent_hint"),170,244,228,34,12);
            }
            dispatchInput=f.Input(dispatch,12,284,304,34,T("write"),value=>DispatchSubmit(value));dispatchSend=f.Button(dispatch,T("send"),322,284,76,34,()=>DispatchSubmit(dispatchInput.text));
            f.Button(dispatch,"+ "+T("builder_short"),12,326,386,28,()=>ShowMessageBuilder(thread,ConversationPending()?.Message.Id,asOperator));
            RefreshDispatch();
        }
        void ShowReply(string id)
        {
            VisibleRecord notification;if(!runtime.Client.PendingDispatch.TryGetValue(id,out notification))return;
            ClosePopup();CloseDispatch();replyMessageId=id;thread=notification.Message.Target;historyFocus=false;
            dispatch=f.Window(root,T(notification.Message.DispatchRequest?"builder_reply_title":"reply_to_fdl"),460,338,CloseDispatch);PlaceDispatch(dispatch);
            f.Button(dispatch,"< "+T("back"),12,38,90,26,()=>CloseDispatch());
            f.Text(dispatch,"FDL "+(notification.Message.DispatchRequest?"← ":"→ ")+notification.Message.TargetLabel,110,40,338,24,14);
            var originalArea=f.Rect("original request",dispatch,12,78,436,120);f.Background(originalArea,Color.clear);originalArea.gameObject.AddComponent<RectMask2D>();var originalScroll=originalArea.gameObject.AddComponent<ScrollRect>();originalScroll.horizontal=false;originalScroll.vertical=true;originalScroll.viewport=originalArea;originalScroll.movementType=ScrollRect.MovementType.Clamped;
            string original=notification.Message.SenderName+": "+entry.Text.Message(notification.Message);float originalHeight=Math.Max(120,Measure(original,15,436));var originalContent=f.Rect("original text",originalArea,0,0,436,originalHeight);originalScroll.content=originalContent;f.Text(originalContent,original,0,0,436,originalHeight,15).overflowMode=TextOverflowModes.Overflow;originalScroll.verticalNormalizedPosition=1;
            dispatchInput=f.Input(dispatch,12,210,336,34,T("write"),DispatchSubmit);dispatchSend=f.Button(dispatch,T("send"),354,210,94,34,()=>DispatchSubmit(dispatchInput.text));
            f.Text(dispatch,T("reply_once"),12,254,436,34,12);dirty=true;
            f.Button(dispatch,"+ "+T("builder_short"),12,294,436,30,()=>ShowMessageBuilder(thread,id,!notification.Message.DispatchRequest));
        }
        void DispatchSubmit(string value)
        {
            if(String.IsNullOrWhiteSpace(value)||pendingReplyRequest!=null)return;
            if(replyMessageId!=null)
            {
                var request=runtime.Client.Request("send","dispatch",thread,value,replyTo:replyMessageId);pendingReplyRequest=request.RequestId;dispatchInput.interactable=false;dispatchSend.interactable=false;runtime.Send(request);
            }
            else{var pending=ConversationPending();var request=runtime.Client.Request("send","dispatch",thread,value,replyTo:pending?.Message.Id);request.ToDispatcher=dispatchAsOperator&&pending==null;runtime.Send(request);dispatchInput.text="";}
            if(dispatchInput!=null)dispatchInput.DeactivateInputField();if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);
        }
        void RefreshDispatch()
        {
            if(dispatch==null||thread==null||replyMessageId!=null)return;string label;runtime.Client.Conversations.TryGetValue(thread,out label);dispatchTitle.text=label;
            if(dispatchAcknowledge!=null)dispatchAcknowledge.interactable=ConversationPending()!=null;
            string signature=thread+":"+String.Join(";",runtime.Client.Rows("dispatch",thread).Select(r=>r.Message.Id));if(signature==dispatchSignature)return;dispatchSignature=signature;
            foreach(Transform child in dispatchContent)Destroy(child.gameObject);float y=0;bool atBottom=dispatchScroll.verticalNormalizedPosition<.02f;
            foreach(var row in runtime.Client.Rows("dispatch",thread))
            {string text=row.Message.SenderName+": "+entry.Text.Message(row.Message);float height=Math.Max(30,Measure(text,14,386));f.Text(dispatchContent,text,0,y,386,height,14);y+=height+8;}
            dispatchContent.sizeDelta=new Vector2(386,Math.Max(168,y));if(atBottom)dispatchScroll.verticalNormalizedPosition=0;
        }
        void SendDispatchSignal(string signal){var pending=ConversationPending();runtime.Send(runtime.Client.Request("send","dispatch",thread,signal:signal,replyTo:pending?.Message.Id));}
        VisibleRecord ConversationPending()=>runtime.Client.PendingDispatch.Values.Where(r=>r.Message.Target==thread&&(!dispatchAsOperator||r.Message.FromDispatcher)).OrderByDescending(r=>r.Message.Utc).FirstOrDefault();
        void CloseDispatch(){if(dispatchInput!=null)dispatchInput.DeactivateInputField();if(dispatch!=null){dispatch.gameObject.SetActive(false);Destroy(dispatch.gameObject);}dispatch=null;dispatchInput=null;dispatchSend=null;dispatchAcknowledge=null;thread=null;replyMessageId=null;pendingReplyRequest=null;dispatchSignature=null;dispatchAsOperator=false;}
        void ClosePopup(){popupRefresh=null;popupKind=null;builderRequest=null;builderError=null;if(popup!=null){popup.gameObject.SetActive(false);Destroy(popup.gameObject);}popup=null;}
        internal void ShowArchive(int page=0)
        {
            if(!GameAdapter.IsHost||runtime.Archive==null)return;ClosePopup();popup=f.Window(root,T("archive")+" · 6h",820,500,ClosePopup);Center(popup);
            popupKind="archive";popup.anchoredPosition=new Vector2(entry.Settings.ArchiveX,-entry.Settings.ArchiveY);ClampTop(popup);
            var rows=runtime.Archive.Read();int start=Math.Max(0,page)*6;float y=40;
            foreach(var row in rows.Skip(start).Take(6))
            {var selected=row;f.Text(popup,row.Utc.ToLocalTime().ToString("HH:mm:ss")+" · "+row.SenderName+" ["+row.SenderId+"] · "+row.Channel+" / "+row.TargetLabel+"\n"+entry.Text.Message(row),12,y,738,64,13);f.Button(popup,">",758,y,46,42,()=>ShowArchiveRecord(selected,page));y+=68;}
            if(page>0)f.Button(popup,"<",12,458,60,30,()=>ShowArchive(page-1));if(start+6<rows.Count)f.Button(popup,">",744,458,60,30,()=>ShowArchive(page+1));
        }
        void ShowArchiveRecord(ChatRecord row,int page)
        {
            if(!GameAdapter.IsHost)return;ClosePopup();popup=f.Window(root,T("archive"),760,430,ClosePopup);Center(popup);
            popupKind="archive";popup.anchoredPosition=new Vector2(entry.Settings.ArchiveX,-entry.Settings.ArchiveY);ClampTop(popup);
            f.Text(popup,row.Utc.ToUniversalTime().ToString("u")+"\n"+row.SenderName+" ["+row.SenderId+"]\n"+row.Channel+" / "+row.TargetLabel+"\nID: "+row.Id,12,40,736,105,14);
            var full=f.Text(popup,entry.Text.Message(row),12,152,736,220,15);full.overflowMode=TextOverflowModes.Overflow;
            f.Button(popup,"<",12,386,70,30,()=>ShowArchive(page));
        }
        internal void OnPacket(ServerPacket packet)
        {
            if(builderRequest!=null&&packet.Ack==builderRequest)
            {
                builderRequest=null;
                if(packet.Kind=="error"){builderError=T("error."+packet.Error);popupRefresh?.Invoke();}
                else{ClosePopup();Toast(T("builder_sent"));}
            }
            if(packet.Kind=="contact")ShowConversation(packet.Thread,packet.OperatorConversation);
            if(packet.Kind=="message"&&packet.Message!=null){string key=packet.Message.Channel;int count;unread.TryGetValue(key,out count);if(key!=channel||!historyFocus)unread[key]=count+1;if(key=="dispatch"){Toast(T("fdl")+": "+packet.Message.TargetLabel);if(packet.CanReply)drawMessages=true;}}
            if(packet.Kind=="reply_done"||packet.Kind=="error"&&packet.ReplyTo!=null)
            {if(replyMessageId==packet.ReplyTo)CloseDispatch();historyFocus=false;drawMessages=entry.Settings.ChatVisible;}
            if(packet.ClearTrain||packet.ClearCrew){CancelWheel();if(packet.ClearTrain&&channel=="train"||packet.ClearCrew&&channel=="crew")input.text="";}
            dirty=true;
        }
        internal void Clamp(RectTransform r)
        {if(r.pivot.y>.5f){ClampTop(r);return;}r.anchoredPosition=new Vector2(Mathf.Clamp(r.anchoredPosition.x,0,Math.Max(0,root.rect.width-r.rect.width)),Mathf.Clamp(r.anchoredPosition.y,0,Math.Max(0,root.rect.height-r.rect.height)));}
        void ClampTop(RectTransform r){r.anchoredPosition=new Vector2(Mathf.Clamp(r.anchoredPosition.x,0,Math.Max(0,root.rect.width-r.rect.width)),-Mathf.Clamp(-r.anchoredPosition.y,0,Math.Max(0,root.rect.height-r.rect.height)));}
        void Center(RectTransform r){r.anchoredPosition=new Vector2((root.rect.width-r.rect.width)/2,-(root.rect.height-r.rect.height)/2);ClampTop(r);}
        void DockTop()
        {
            if(top==null)BuildToolbarIcon();var bar=NativeDock.Toolbar();
            if(bar==null){DetachTop();top.gameObject.SetActive(false);return;}
            if(nativeToolbar==bar&&topSlot!=null&&topSlot.parent==bar){top.gameObject.SetActive(true);return;}
            DetachTop();nativeToolbar=bar;topSlot=f.Rect("communication slot",bar,0,0,36,32);topSlot.SetAsFirstSibling();
            var layout=topSlot.gameObject.AddComponent<LayoutElement>();layout.preferredWidth=36;layout.minWidth=36;layout.preferredHeight=32;layout.flexibleWidth=0;
            top.gameObject.SetActive(true);
            var fitter=bar.GetComponent<ContentSizeFitter>();if(fitter==null||fitter.horizontalFit==ContentSizeFitter.FitMode.Unconstrained){toolbarExtra=36;bar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,bar.rect.width+toolbarExtra);}
            LayoutRebuilder.ForceRebuildLayoutImmediate(bar);
            AlignTopToSlot();entry.Log.Info("Chat toolbar attached with its own canvas and raycaster; interactable="+topButton.IsInteractable()+".");
        }
        void AlignTopToSlot()
        {
            if(top==null||topSlot==null||toolbarRoot==null)return;
            toolbarCanvas.enabled=true;toolbarRaycaster.enabled=true;topButton.interactable=true;
            topSlot.GetWorldCorners(topCorners);var nativeCanvas=topSlot.GetComponentInParent<Canvas>();Camera camera=nativeCanvas!=null&&nativeCanvas.renderMode!=RenderMode.ScreenSpaceOverlay?nativeCanvas.worldCamera:null;
            Vector2 left,right;RectTransformUtility.ScreenPointToLocalPointInRectangle(toolbarRoot,RectTransformUtility.WorldToScreenPoint(camera,topCorners[1]),null,out left);RectTransformUtility.ScreenPointToLocalPointInRectangle(toolbarRoot,RectTransformUtility.WorldToScreenPoint(camera,topCorners[3]),null,out right);
            var position=new Vector2(left.x+toolbarRoot.rect.width*toolbarRoot.pivot.x,left.y-toolbarRoot.rect.height*(1-toolbarRoot.pivot.y));var size=new Vector2(Math.Max(1,right.x-left.x),Math.Max(1,left.y-right.y));
            if((top.anchoredPosition-position).sqrMagnitude>.01f)top.anchoredPosition=position;if((top.sizeDelta-size).sqrMagnitude>.01f)top.sizeDelta=size;
        }
        void UpdateDeskAccess()
        {
            if(deskAccess==null)return;bool near=false;var camera=Camera.main;
            if(entry.Settings.ShowDeskShortcut&&runtime.Client.State?.Dispatcher==true&&camera!=null&&!entry.SettingsOpen)
            {
                if(deskButtons==null||Time.realtimeSinceStartup>=nextDeskScan){deskButtons=UnityEngine.Object.FindObjectsByType<Track.Signals.CTCPanelButton>(FindObjectsSortMode.None);nextDeskScan=Time.realtimeSinceStartup+5;}
                foreach(var button in deskButtons)
                {
                    if(button==null||!button.gameObject.activeInHierarchy||(button.transform.position-camera.transform.position).sqrMagnitude>64)continue;
                    var point=camera.WorldToViewportPoint(button.transform.position);if(point.z>0&&point.x>=0&&point.x<=1&&point.y>=0&&point.y<=1){near=true;break;}
                }
            }
            deskAccess.anchoredPosition=new Vector2((root.rect.width-230)/2,-48);if(deskAccess.gameObject.activeSelf!=near)deskAccess.gameObject.SetActive(near);
        }
        void DetachTop(){if(top!=null&&topSlot!=null&&top.parent==topSlot){top.SetParent(root,false);top.gameObject.SetActive(false);}if(topSlot!=null){topSlot.gameObject.SetActive(false);Destroy(topSlot.gameObject);}topSlot=null;if(nativeToolbar!=null&&toolbarExtra>0)nativeToolbar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Math.Max(0,nativeToolbar.rect.width-toolbarExtra));nativeToolbar=null;toolbarExtra=0;}
        void OnDestroy(){CancelWheel();DetachTop();if(top!=null)Destroy(top.gameObject);if(toolbarCanvasObject!=null)Destroy(toolbarCanvasObject);if(canvasObject!=null)Destroy(canvasObject);if(f?.Material!=null)Destroy(f.Material);}
    }
}
