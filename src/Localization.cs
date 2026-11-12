// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
namespace EnusDE.Communications
{
    public sealed class CommsOwnedText:MonoBehaviour { }
    internal sealed class Localization
    {
        readonly string folder;
        readonly Harmony harmony;
        readonly Action<string> warning;
        readonly HashSet<MethodInfo> guarded=new HashSet<MethodInfo>();
        Dictionary<string,string> english=new Dictionary<string,string>(),localized=new Dictionary<string,string>();
        object behaviour;FieldInfo settingsField,languageField;
        bool reported;
        string current;
        public Localization(string path,Harmony harmony,Action<string> log=null)
        {
            this.harmony=harmony;warning=log;folder=Path.Combine(path,"Languages");english=Read("en");Refresh();
        }
        void FindLanguageSource()
        {
            if(behaviour is UnityEngine.Object existing&&existing!=null)return;
            behaviour=null;settingsField=null;languageField=null;
            var type=AccessTools.TypeByName("EnusDE.GermanUi.GermanUiBehaviour");
            if(type!=null)
            {
                settingsField=AccessTools.Field(type,"settings");
                var eligible=AccessTools.Method(type,"Eligible",new[]{typeof(Component)});
                // Optional isolation only: this mod translates its own fixed texts.
                // An incompatible translator must never prevent chat from starting.
                if(eligible!=null&&guarded.Add(eligible))
                {try{harmony.Patch(eligible,prefix:new HarmonyMethod(typeof(Localization),nameof(ProtectOwnText)));}catch(Exception error){Report(error);}}
                var objects=UnityEngine.Object.FindObjectsByType(type,FindObjectsSortMode.None);if(objects.Length>0)behaviour=objects[0];
            }
        }
        void Report(Exception error){if(!reported){reported=true;warning?.Invoke("Optional translation integration unavailable: "+error.Message);}}
        static bool ProtectOwnText(Component text,ref bool __result)
        {if(text!=null&&text.GetComponentInParent<CommsOwnedText>()!=null){__result=false;return false;}return true;}
        Dictionary<string,string> Read(string code)
        {
            string path=Path.Combine(folder,code+".json");return File.Exists(path)?JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(path))??new Dictionary<string,string>():new Dictionary<string,string>();
        }
        public bool Refresh()
        {
            string language="en";
            try
            {
                FindLanguageSource();
                if(behaviour is UnityEngine.Object obj&&obj!=null&&settingsField!=null)
                {var settings=settingsField.GetValue(behaviour);if(settings!=null){languageField=languageField??AccessTools.Field(settings.GetType(),"language");language=languageField?.GetValue(settings) as string??"en";}}
            }
            catch(Exception error){behaviour=null;Report(error);}
            language=language.ToLowerInvariant();if(!Regex.IsMatch(language,"^[a-z0-9_-]{2,24}$"))language="en";
            if(current==language)return false;current=language;localized=Read(language);return true;
        }
        public string Get(string key){string text;return localized.TryGetValue(key,out text)?text:english.TryGetValue(key,out text)?text:key;}
        public string Message(ChatRecord message){if(message.Parts!=null)return MessageTemplates.Render(message.Parts,Get)??message.Text;return String.IsNullOrEmpty(message.Signal)?message.Text:Get("signal."+message.Signal);}
    }
}
