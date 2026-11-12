// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace EnusDE.Communications
{
    public sealed partial class CommsUI
    {
        string builderRequest,builderError,draftTemplate="movement_request",draftOrigin="",draftDestination="",draftRecipient="dispatch",draftTask="pickup",draftDetail="",draftAssignedTrain;
        internal void ShowMessageBuilder(string fixedThread=null,string replyTo=null,bool asOperator=false)
        {
            using var measurement=entry.Performance.Measure(PerformanceStage.Rebuild);
            ClosePopup();CancelWheel();
            var state=runtime.Client.State;string token=state?.Token;long version=state?.Version??0;
            string enrolledTrain=state?.Train,trainId=fixedThread!=null&&fixedThread.StartsWith("fdl:",StringComparison.Ordinal)?fixedThread.Substring(4):enrolledTrain;
            VisibleRecord original=null;if(replyTo!=null)runtime.Client.PendingDispatch.TryGetValue(replyTo,out original);
            bool fixedRoute=fixedThread!=null,dispatcher=fixedRoute&&!asOperator&&state?.Dispatcher==true&&(replyTo==null||original?.Message.DispatchRequest==true);
            string selectedChannel=fixedRoute?"dispatch":draftRecipient;
            var definitions=MessageTemplates.All.Where(t=>t.Allows(selectedChannel)&&(dispatcher?t.DispatcherOnly||t.Id=="understood":!t.DispatcherOnly)).OrderBy(t=>dispatcher?(t.Id=="hold"?0:t.Id=="proceed"?1:2):0).ToArray();
            if(!definitions.Any(t=>t.Id==draftTemplate))draftTemplate=definitions[0].Id;
            if(original?.Message.Parts!=null)
            {draftOrigin=original.Message.Parts.Origin??"";draftDestination=original.Message.Parts.Destination??"";}
            string[] places;try{places=GameAdapter.MessagePlaces();}catch{places=Array.Empty<string>();}
            popup=f.Window(root,T("builder_title"),680,720,ClosePopup);var window=popup;popupKind="builder";Center(window);
            f.Text(window,T("builder_recipient"),12,45,96,26,14);
            Action refresh=null;
            string[] recipients={"global","crew","train","dispatch"};var receiverButtons=new Button[4];
            if(fixedRoute)f.Text(window,T(dispatcher?"train":"fdl")+" · "+(replyTo!=null?T("reply"):T("open_conversation")),112,45,556,26,14);
            else for(int i=0;i<4;i++)
            {
                string recipient=recipients[i];receiverButtons[i]=f.Button(window,T(recipient=="dispatch"?"fdl":recipient),112+i*139,40,133,30,()=>{draftRecipient=recipient;ShowMessageBuilder();});
            }
            var trainLabel=f.Text(window,"",12,82,fixedRoute?656:500,26,14);
            Button chooseTrain=null;
            if(!fixedRoute)chooseTrain=f.Button(window,T(selectedChannel=="crew"?"builder_assign_train":"builder_choose_train"),520,78,148,30,()=>
            {
                if(selectedChannel!="crew"){ShowChooseTrain();return;}
                var trains=runtime.View.Trains.Values.Where(t=>(String.IsNullOrEmpty(t.Crew)||t.Crew==runtime.Client.State?.Crew)&&(runtime.Client.State?.Admin==true||String.IsNullOrEmpty(t.Crew)||t.Crew==runtime.Client.State?.ActualCrew)).OrderBy(t=>t.Label??t.Id,StringComparer.OrdinalIgnoreCase).ToArray();
                var names=trains.Select((t,i)=>(i+1)+". "+new String((t.Label??t.Id).Select(c=>Char.IsControl(c)?' ':c).Take(70).ToArray())).ToArray();
                ShowPlacePicker(window,names,name=>{int index=Array.IndexOf(names,name);if(index>=0){draftAssignedTrain=trains[index].Id;refresh?.Invoke();}},"builder_assign_train");
            });
            f.Text(window,T("builder_type"),12,112,656,22,14);var choices=new Button[definitions.Length];
            for(int i=0;i<definitions.Length;i++)
            {
                var definition=definitions[i];choices[i]=f.Button(window,T("template.name."+definition.Id),12+(i%2)*332,136+(i/2)*35,324,30,()=>{draftTemplate=definition.Id;builderError=null;refresh?.Invoke();});
            }
            f.Text(window,T("builder_origin"),12,246,656,22,14);
            var origin=f.Input(window,12,270,580,32,T("builder_place_hint"),null,value=>{draftOrigin=value;builderError=null;refresh?.Invoke();});origin.characterLimit=80;origin.text=draftOrigin;
            var originPick=f.Button(window,"…",600,270,68,32,()=>ShowPlacePicker(window,places,name=>origin.text=name));
            f.Text(window,T("builder_destination"),12,310,656,22,14);
            var destination=f.Input(window,12,334,580,32,T("builder_place_hint"),null,value=>{draftDestination=value;builderError=null;refresh?.Invoke();});destination.characterLimit=80;destination.text=draftDestination;
            var destinationPick=f.Button(window,"…",600,334,68,32,()=>ShowPlacePicker(window,places,name=>destination.text=name));
            f.Text(window,T("builder_task"),12,374,656,22,14);
            Button task=null;task=f.Button(window,"",12,398,220,32,()=>{var keys=MessageTemplates.Tasks.Keys.ToArray();int index=Array.IndexOf(keys,draftTask);draftTask=keys[(index+1)%keys.Length];builderError=null;refresh?.Invoke();});
            var detail=f.Input(window,240,398,428,32,T("builder_custom_task"),null,value=>{draftDetail=value;builderError=null;refresh?.Invoke();});detail.characterLimit=80;detail.text=draftDetail;
            f.Text(window,T("builder_preview"),12,442,656,22,14);
            var area=f.Rect("template preview",window,12,466,656,136);f.Background(area,new Color(.08f,.07f,.05f,.8f));area.gameObject.AddComponent<RectMask2D>();
            var sc=area.gameObject.AddComponent<ScrollRect>();sc.horizontal=false;sc.vertical=true;sc.viewport=area;sc.movementType=ScrollRect.MovementType.Clamped;
            var previewContent=f.Rect("preview text",area,8,5,640,126);sc.content=previewContent;var preview=f.Text(previewContent,"",0,0,640,126,15);preview.overflowMode=TextOverflowModes.Overflow;
            var hint=f.Text(window,T("builder_language_hint"),12,610,656,40,13);hint.color=UiFactory.Muted;
            var error=f.Text(window,"",12,664,450,40,13);error.color=new Color(1,.65f,.45f);
            MessageParts normalized=null;string route=null,targetRoute=null;bool valid=false;
            var send=f.Button(window,T("send"),480,666,188,32,()=>
            {
                refresh?.Invoke();if(!valid||builderRequest!=null)return;
                var request=runtime.Client.Request("send",route,targetRoute,MessageTemplates.Render(normalized),replyTo:replyTo);request.Parts=normalized;request.Version=version;
                builderRequest=request.RequestId;runtime.Send(request);refresh?.Invoke();
            });
            refresh=()=>
            {
                if(popup!=window||window==null)return;
                if(builderRequest!=null&&!runtime.IsPending(builderRequest)){builderRequest=null;builderError=T("not_confirmed");}
                var current=runtime.Client.State;var definition=MessageTemplates.Find(draftTemplate);WorkingTrain train=null;
                if(selectedChannel=="crew")trainId=draftAssignedTrain??enrolledTrain;
                if(trainId!=null)runtime.View.Trains.TryGetValue(trainId,out train);
                route=fixedRoute?"dispatch":draftRecipient;targetRoute=route=="dispatch"?fixedThread??"fdl:"+trainId:route=="crew"?current?.Crew:trainId;
                var parts=new MessageParts{Id=draftTemplate,TrainId=trainId,Origin=draftOrigin,Destination=draftDestination,Task=draftTask,Detail=draftDetail,ToDispatcher=route=="dispatch"&&replyTo==null&&(!fixedRoute||asOperator)};
                bool context=current!=null&&current.Token==token&&current.Version==version&&(fixedRoute||selectedChannel=="crew"||current.Train==enrolledTrain)&&!runtime.RoutePending;
                string problem=!runtime.Client.Connected?T("connecting"):!context?T("error.membership_changed"):definition.NeedsTrain&&train==null?T("wheel_needs_train"):route=="crew"&&String.IsNullOrEmpty(current.Crew)?T("error.no_crew"):
                    replyTo!=null&&!runtime.Client.PendingDispatch.ContainsKey(replyTo)?T("error.reply_unavailable"):null;
                normalized=null;valid=problem==null&&MessageTemplates.TryNormalize(parts,train,current?.Dispatcher==true,route,out normalized);
                if(problem==null&&!valid)problem=T("builder_complete_fields");
                error.text=builderError??problem??"";send.interactable=valid&&builderRequest==null;
                trainLabel.text=definition.NeedsTrain?T("train")+": "+(train?.Label??T("no_train")):T("builder_no_train_needed");
                if(chooseTrain!=null)chooseTrain.gameObject.SetActive(definition.NeedsTrain);
                origin.interactable=originPick.interactable=definition.Origin;destination.interactable=destinationPick.interactable=definition.Destination;
                task.interactable=definition.NeedsTask;task.GetComponentInChildren<TMP_Text>().text=T("template.task."+draftTask);detail.interactable=definition.NeedsTask&&draftTask=="custom";
                for(int i=0;i<choices.Length;i++)choices[i].GetComponent<Image>().color=definitions[i].Id==draftTemplate?UiFactory.WheelSelected:UiFactory.WheelBrown;
                if(!fixedRoute)for(int i=0;i<4;i++)receiverButtons[i].GetComponent<Image>().color=recipients[i]==draftRecipient?UiFactory.WheelSelected:UiFactory.WheelBrown;
                var previewParts=normalized??new MessageParts{Id=draftTemplate,TrainId=trainId,TrainLabel=train?.Label??"—",Origin=String.IsNullOrWhiteSpace(draftOrigin)?"…":draftOrigin,Destination=String.IsNullOrWhiteSpace(draftDestination)?"…":draftDestination,Task=draftTask,Detail=draftDetail};
                string text=MessageTemplates.Render(previewParts,entry.Text.Get);
                if(preview.text!=text){preview.text=text;float height=Math.Max(126,Measure(text,15,640));preview.rectTransform.sizeDelta=new Vector2(640,height);previewContent.sizeDelta=new Vector2(640,height);sc.verticalNormalizedPosition=1;}
            };
            popupRefresh=refresh;refresh();
        }
        void ShowPlacePicker(RectTransform parent,string[] places,Action<string> selected,string titleKey="builder_places")
        {
            var existing=parent.Find("message place picker");if(existing!=null){existing.gameObject.SetActive(false);Destroy(existing.gameObject);}
            RectTransform chooser=null;Action close=()=>{if(chooser!=null){chooser.gameObject.SetActive(false);Destroy(chooser.gameObject);}};
            chooser=f.Window(parent,T(titleKey),640,436,close);chooser.name="message place picker";chooser.anchoredPosition=new Vector2(20,-86);
            string query="";int page=0;var list=f.Rect("place choices",chooser,12,88,616,264);Button previous=null,next=null;TMP_Text pageLabel=null;
            Action rebuild=()=>
            {
                foreach(Transform child in list){child.gameObject.SetActive(false);Destroy(child.gameObject);}
                var matches=MessageTemplates.Places(places,query);int pages=Math.Max(1,(matches.Length+5)/6);page=Math.Max(0,Math.Min(pages-1,page));
                previous.interactable=page>0;next.interactable=page+1<pages;pageLabel.text=T("page")+" "+(page+1)+" / "+pages;
                for(int i=0;i<Math.Min(6,matches.Length-page*6);i++){string name=matches[page*6+i];f.Button(list,name,0,i*42,616,36,()=>{selected(name);close();});}
                if(matches.Length==0)f.Text(list,T(titleKey=="builder_assign_train"?"no_trains_found":"builder_no_places"),0,4,616,120,14);
            };
            f.Input(chooser,12,42,616,32,T("search"),null,value=>{query=value;page=0;rebuild();});
            previous=f.Button(chooser,"‹ "+T("previous"),12,360,136,30,()=>{page--;rebuild();});next=f.Button(chooser,T("next")+" ›",492,360,136,30,()=>{page++;rebuild();});pageLabel=f.Text(chooser,"",160,364,320,26,14);pageLabel.alignment=TextAlignmentOptions.Center;
            f.Text(chooser,T(titleKey=="builder_assign_train"?"builder_assign_hint":"builder_custom_place"),12,400,616,28,13);rebuild();
        }
    }
}
