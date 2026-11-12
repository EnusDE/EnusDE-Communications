// Copyright (c) 2026 EnusDE. MIT License.
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UI.Builder;
namespace EnusDE.Communications
{
    public sealed class CommsDrag:MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        internal RectTransform Target;internal CommsUI Owner;internal Action Done;
        public void OnBeginDrag(PointerEventData e){Owner.Dragging=true;}
        public void OnDrag(PointerEventData e){Target.anchoredPosition+=e.delta/Owner.Scale;Owner.Clamp(Target);}
        public void OnEndDrag(PointerEventData e){Owner.Dragging=false;Done?.Invoke();}
    }
    internal sealed class UiFactory
    {
        internal CommsUI Owner;
        internal readonly TMP_FontAsset Font;internal readonly Material Material;
        internal static readonly Color Ink=new Color(.85f,.81f,.68f),Muted=new Color(.68f,.67f,.56f),Panel=new Color(.12f,.105f,.08f,.97f);
        internal static readonly Color WheelBrown=new Color(.19f,.165f,.12f,.98f),WheelSelected=new Color(.37f,.31f,.20f,.99f),WheelGold=new Color(.95f,.85f,.61f);
        internal static Color ChannelColor(string channel)=>channel=="global"?new Color(.57f,.79f,1):channel=="crew"?new Color(.63f,.9f,.64f):channel=="train"?new Color(1,.77f,.43f):new Color(.9f,.73f,1);
        internal UiFactory(TMP_FontAsset font)
        {
            Font=font;Material=new Material(font.material);Material.SetFloat(ShaderUtilities.ID_OutlineWidth,.16f);Material.SetColor(ShaderUtilities.ID_OutlineColor,Color.black);
        }
        internal RectTransform Rect(string name,Transform parent,float x,float y,float width,float height)
        {
            var go=new GameObject(name,typeof(RectTransform));go.layer=5;var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);return r;
        }
        internal void Fill(RectTransform r,float inset=0){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(inset,inset);r.offsetMax=new Vector2(-inset,-inset);}
        internal TMP_Text Text(Transform parent,string text,float x,float y,float width,float height,float size=15)
        {
            var r=Rect("text",parent,x,y,width,height);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=Font;t.fontSharedMaterial=Material;t.fontSize=size;t.color=Ink;t.richText=false;t.text=text;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Ellipsis;return t;
        }
        internal Image Background(RectTransform r,Color color){var i=r.gameObject.AddComponent<Image>();i.color=color;return i;}
        void NativeFrame(Image image)
        {var sprite=UIBuilderAssets.Shared?.uiSprite;if(sprite!=null){image.sprite=sprite;image.type=Image.Type.Sliced;}}
        internal Button Button(Transform parent,string text,float x,float y,float width,float height,Action click,bool transparent=false)
        {
            // Own the geometry: native button prefabs contain self-sizing layout components.
            // Borrow only the frame/colours for dialogs; chat controls keep their flat appearance.
            var r=Rect("button "+text,parent,x,y,width,height);var image=Background(r,transparent?Color.clear:WheelBrown);
            if(!transparent)NativeFrame(image);
            var button=r.gameObject.AddComponent<Button>();var label=Text(r,text,5,3,width-10,height-6,14);
            var colours=button.colors;colours.normalColor=Color.white;colours.highlightedColor=new Color(.8f,.76f,.6f);colours.pressedColor=new Color(.6f,.57f,.44f);button.colors=colours;
            button.onClick.AddListener(()=>click());
            image.raycastTarget=true;image.canvasRenderer.cullTransparentMesh=false;
            button.targetGraphic=image;label.raycastTarget=false;label.alignment=TextAlignmentOptions.Center;return button;
        }
        internal TMP_InputField Input(Transform parent,float x,float y,float width,float height,string hint,Action<string> submit=null,Action<string> change=null)
        {
            var r=Rect("input",parent,x,y,width,height);var image=Background(r,new Color(.09f,.08f,.06f,.97f));NativeFrame(image);var field=r.gameObject.AddComponent<TMP_InputField>();field.targetGraphic=image;
            var viewport=Rect("viewport",r,6,3,width-12,height-6);viewport.gameObject.AddComponent<RectMask2D>();
            var text=Text(viewport,"",0,0,width-12,height-6,15);Fill(text.rectTransform);field.textViewport=viewport;field.textComponent=(TextMeshProUGUI)text;
            var placeholder=Text(viewport,hint,0,0,width-12,height-6,14);placeholder.color=Muted;field.placeholder=placeholder;field.characterLimit=500;field.richText=false;field.lineType=TMP_InputField.LineType.SingleLine;
            if(submit!=null)field.onSubmit.AddListener(value=>submit(value));if(change!=null)field.onValueChanged.AddListener(value=>change(value));return field;
        }
        internal RectTransform Window(Transform parent,string title,float width,float height,Action close)
        {
            var r=Rect(title,parent,0,0,width,height);NativeFrame(Background(r,Panel));
            var border=r.gameObject.AddComponent<Outline>();border.effectColor=new Color(.61f,.55f,.39f,.9f);border.effectDistance=new Vector2(1,-1);border.useGraphicAlpha=false;
            var titleStrip=Rect("title strip",r,1,1,width-2,31);Background(titleStrip,new Color(.32f,.30f,.24f));
            var header=Rect("drag window",r,0,0,width-42,34);Background(header,Color.clear);Text(header,title,12,7,width-60,25,17);
            if(Owner!=null){var drag=header.gameObject.AddComponent<CommsDrag>();drag.Target=r;drag.Owner=Owner;drag.Done=()=>Owner.WindowMoved(r);}
            Button(r,"X",width-38,4,30,28,close);return r;
        }
    }
}
