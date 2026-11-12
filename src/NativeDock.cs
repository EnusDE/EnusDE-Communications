using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace EnusDE.Communications
{
    internal static class NativeDock
    {
        const BindingFlags Fields=BindingFlags.NonPublic|BindingFlags.Instance;
        static readonly FieldInfo EngineName=typeof(UI.EngineControls.LocomotiveControlsUIAdapter).GetField("nameLabel",Fields);
        static readonly FieldInfo Clock=typeof(UI.TopRightArea).GetField("clockText",Fields);
        static TMP_Text engineName;static CanvasGroup[] engineGroups;static RectTransform toolbar;
        static readonly Vector3[] corners=new Vector3[4];static float nextEngineScan;
        internal static void Clear(){engineName=null;engineGroups=null;toolbar=null;nextEngineScan=0;}
        internal static float ChatBottom(RectTransform canvas)
        {
            if(String.IsNullOrEmpty(GameAdapter.Candidate()))return 8;
            if((engineName==null||!engineName.gameObject.activeInHierarchy)&&Time.realtimeSinceStartup>=nextEngineScan)
            {
                nextEngineScan=Time.realtimeSinceStartup+1;
                foreach(var panel in Resources.FindObjectsOfTypeAll<UI.EngineControls.LocomotiveControlsUIAdapter>())
                {var name=EngineName?.GetValue(panel) as TMP_Text;if(panel.gameObject.activeInHierarchy&&name!=null&&name.gameObject.activeInHierarchy){engineName=name;engineGroups=name.GetComponentsInParent<CanvasGroup>();break;}}
            }
            if(engineName==null||!engineName.gameObject.activeInHierarchy||engineGroups!=null&&engineGroups.Any(g=>g!=null&&g.alpha<.01f))return 8;
            engineName.rectTransform.GetWorldCorners(corners);Vector2 point;
            Camera camera=engineName.canvas!=null&&engineName.canvas.renderMode!=RenderMode.ScreenSpaceOverlay?engineName.canvas.worldCamera:null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas,RectTransformUtility.WorldToScreenPoint(camera,corners[1]),null,out point);
            return Math.Max(8,point.y+canvas.rect.height*canvas.pivot.y+10);
        }
        internal static RectTransform Toolbar()
        {
            if(toolbar!=null&&toolbar.gameObject.activeInHierarchy)return toolbar;
            foreach(var area in Resources.FindObjectsOfTypeAll<UI.TopRightArea>())
            {
                if(!area.gameObject.activeInHierarchy)continue;
                var clock=Clock?.GetValue(area) as TMP_Text;if(clock==null)continue;
                toolbar=null;
                foreach(var layout in clock.GetComponentsInParent<HorizontalLayoutGroup>())
                {var rect=layout.transform as RectTransform;if(rect!=null&&rect.rect.height<100&&rect.rect.width>=250)toolbar=rect;}
                if(toolbar!=null)return toolbar;
            }
            return null;
        }
    }
}
