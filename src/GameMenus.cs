// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Reflection;
using UnityEngine;
using UI.Windows;
namespace EnusDE.Communications
{
    internal static class GameMenus
    {
        static readonly PropertyInfo Topmost=typeof(WindowManager).GetProperty("TopmostShownWindow",BindingFlags.Instance|BindingFlags.NonPublic);
        static int frame=-1,order=70;static bool open;
        static readonly System.Collections.Generic.List<Window> windows=new System.Collections.Generic.List<Window>();
        static float nextWindowScan;
        internal static bool IsOpen {get{Refresh();return open;}}
        internal static int ChatOrder {get{Refresh();return order;}}
        internal static void Clear(){frame=-1;open=false;order=70;windows.Clear();nextWindowScan=0;}
        internal static bool CoversPoint(Vector2 point)
        {
            var manager=WindowManager.Shared;if(manager==null)return false;
            if(Time.unscaledTime>=nextWindowScan){manager.GetComponentsInChildren<Window>(false,windows);nextWindowScan=Time.unscaledTime+.1f;}
            foreach(var window in windows)
            {
                if(window==null||!window.IsShown||!window.gameObject.activeInHierarchy)continue;
                var rect=window.transform as RectTransform;if(rect==null)continue;
                var canvas=window.GetComponentInParent<Canvas>();var camera=canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
                if(RectTransformUtility.RectangleContainsScreenPoint(rect,point,camera))return true;
            }
            return false;
        }
        static void Refresh()
        {
            if(frame==Time.frameCount)return;frame=Time.frameCount;
            var manager=WindowManager.Shared;var window=manager!=null?Topmost?.GetValue(manager) as Window:null;
            open=window!=null&&window.IsShown||PauseMenu.IsPaused||UI.ContextMenu.ContextMenu.IsShown;
            var canvas=window!=null?window.GetComponentInParent<Canvas>():null;
            order=ChatPresentation.SortingOrder(open,canvas!=null?canvas.sortingOrder:0);
        }
    }
    public sealed class ToolbarWindowFilter:MonoBehaviour,ICanvasRaycastFilter
    {
        public bool IsRaycastLocationValid(Vector2 point,Camera camera)=>!GameMenus.CoversPoint(point);
    }
}
