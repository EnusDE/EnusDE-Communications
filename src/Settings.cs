// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.IO;
using Newtonsoft.Json;
namespace EnusDE.Communications
{
    public sealed class ModSettings
    {
        public float FadeSeconds=15,FontSize=15,WhistleVolume=0.6f;
        public float ChatX=16,ChatY=174,ChatWidth=500,ChatHeight=215;
        public float DispatchX=1080,DispatchY=140,ArchiveX=650,ArchiveY=160;
        public float BackgroundOpacity=0,BackgroundR=.1f,BackgroundG=.11f,BackgroundB=.085f;
        public bool ChatVisible=true,AutoDock=true,ShowTimestamps=true,ShowDeskShortcut=true;
        public string WheelBinding="<Keyboard>/f10";
        public string ChatBinding="";
        public HostPolicy Host=new HostPolicy();
        public void Normalize()
        {
            FadeSeconds=NormalizeFade(FadeSeconds);FontSize=Bound(FontSize,15,12,24);WhistleVolume=Bound(WhistleVolume,.6f,0,1);
            ChatX=Bound(ChatX,16,0,10000);ChatY=Bound(ChatY,174,0,10000);ChatWidth=Bound(ChatWidth,440,440,800);ChatHeight=Bound(ChatHeight,215,120,600);
            DispatchX=Bound(DispatchX,1080,0,10000);DispatchY=Bound(DispatchY,140,0,10000);ArchiveX=Bound(ArchiveX,650,0,10000);ArchiveY=Bound(ArchiveY,160,0,10000);
            BackgroundOpacity=Bound(BackgroundOpacity,0,0,1);BackgroundR=Bound(BackgroundR,.1f,0,1);BackgroundG=Bound(BackgroundG,.11f,0,1);BackgroundB=Bound(BackgroundB,.085f,0,1);
            if(String.IsNullOrWhiteSpace(WheelBinding)||WheelBinding.Length>160)WheelBinding="<Keyboard>/f10";
            if(ChatBinding==null||ChatBinding.Length>160)ChatBinding="";
            if(Host==null)Host=new HostPolicy();
            if(Host.AdminRanks==null)Host.AdminRanks=new string[0];if(Host.DispatcherRanks==null)Host.DispatcherRanks=new[]{"Dispatcher"};
        }
        public static float NormalizeFade(float seconds){if(Single.IsNaN(seconds)||Single.IsInfinity(seconds))return 15;if(seconds<=0||seconds>60)return 0;return Math.Max(5,Math.Min(60,(float)Math.Round(seconds/5)*5));}
        public static string ForHome(string home)
        {if(String.IsNullOrWhiteSpace(home)||!Path.IsPathRooted(home))throw new IOException("Absolute Railroader home required.");return Path.Combine(Path.GetFullPath(home),"ModSettings","enusde-communications.json");}
        public static ModSettings LoadUser(string home,string modDirectory)
        {
            string file=ForHome(home);if(File.Exists(file))return Read(file);
            string previous=Path.Combine(modDirectory,"Data","Settings.json");var result=Read(previous);result.Write(file);return result;
        }
        static float Bound(float v,float fallback,float min,float max){return Single.IsNaN(v)||Single.IsInfinity(v)?fallback:Math.Max(min,Math.Min(max,v));}
        public static ModSettings Read(string path){var result=File.Exists(path)?JsonConvert.DeserializeObject<ModSettings>(File.ReadAllText(path)):new ModSettings();result=result??new ModSettings();result.Normalize();return result;}
        public void Write(string path)
        {
            Normalize();Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));string temporary=path+".tmp";File.WriteAllText(temporary,JsonConvert.SerializeObject(this,Formatting.Indented));
            if(File.Exists(path)){try{File.Replace(temporary,path,null);}catch(NotSupportedException){File.Copy(temporary,path,true);File.Delete(temporary);}}
            else File.Move(temporary,path);
        }
    }
}
