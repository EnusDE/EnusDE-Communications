using System;
using System.IO;
using EnusDE.Communications;
using HarmonyLib;
static class LanguageTests
{
    static int passed;static string pack;
    static void Check(bool condition,string reason){if(!condition)throw new Exception(reason);}
    static void Test(string name,Action action)
    {
        AccessTools.AvailableType=null;UnityEngine.Object.Found=new UnityEngine.Object[0];UnityEngine.Object.FindThrows=false;
        action();passed++;Console.WriteLine("PASS "+name);
    }
    static void Main(string[] args)
    {
        try
        {
            pack=Path.Combine(args[0],"EnusDE-Communications");
            Test("standalone English, no translation component or patch",()=>{
                var harmony=new Harmony();var text=new Localization(pack,harmony);Check(text.Get("global")=="Global"&&text.Get("signal.stop")=="Stop!","English fallback");Check(harmony.Patches==0,"Absent translator patched");
                Check(text.Message(new ChatRecord{Text="  <b>Hallo</b>  "})=="  <b>Hallo</b>  ","Free text altered");
            });
            Test("installed but inactive translator stays English",()=>{
                AccessTools.AvailableType=typeof(FakeTranslationBehaviour);var text=new Localization(pack,new Harmony());Check(text.Get("signal.stop")=="Stop!","Inactive translator affected language");
            });
            Test("active translator supplies language; signals use own dictionaries",()=>{
                AccessTools.AvailableType=typeof(FakeTranslationBehaviour);var source=new FakeTranslationBehaviour();UnityEngine.Object.Found=new[]{source};var text=new Localization(pack,new Harmony());
                Check(text.Get("signal.stop")=="Halt!","German not selected");source.settings.language="en";Check(text.Refresh()&&text.Get("signal.stop")=="Stop!","Language switch not detected");
                source.settings.language="unknown";text.Refresh();Check(text.Get("signal.stop")=="Stop!","Missing language fallback");source.settings.language="../../de";text.Refresh();Check(text.Get("signal.stop")=="Stop!","Invalid language fallback");
            });
            Test("translator enabled late, removed and recreated",()=>{
                var text=new Localization(pack,new Harmony());AccessTools.AvailableType=typeof(FakeTranslationBehaviour);var source=new FakeTranslationBehaviour();UnityEngine.Object.Found=new[]{source};text.Refresh();Check(text.Get("signal.stop")=="Halt!","Late detection");
                source.Destroyed=true;UnityEngine.Object.Found=new UnityEngine.Object[0];text.Refresh();Check(text.Get("signal.stop")=="Stop!","Removal fallback");UnityEngine.Object.Found=new[]{new FakeTranslationBehaviour()};text.Refresh();Check(text.Get("signal.stop")=="Halt!","Recreated source");
            });
            Test("optional patch failure does not block language or chat text",()=>{
                AccessTools.AvailableType=typeof(FakeTranslationBehaviour);UnityEngine.Object.Found=new[]{new FakeTranslationBehaviour()};int warnings=0;var harmony=new Harmony{FailPatch=true};var text=new Localization(pack,harmony,_=>warnings++);
                Check(text.Get("signal.stop")=="Halt!","Optional isolation failure blocked language");text.Refresh();Check(warnings==1&&harmony.Patches==1,"Repeated optional patch failure");Check(text.Message(new ChatRecord{Text="Original"})=="Original","Free text changed");
            });
            Test("unavailable or incompatible translator falls back without throwing",()=>{
                AccessTools.AvailableType=typeof(FakeTranslationBehaviour);UnityEngine.Object.FindThrows=true;int warnings=0;var text=new Localization(pack,new Harmony(),_=>warnings++);text.Refresh();Check(text.Get("signal.stop")=="Stop!"&&warnings==1,"Optional component failure");
                UnityEngine.Object.FindThrows=false;AccessTools.AvailableType=typeof(IncompatibleTranslationBehaviour);UnityEngine.Object.Found=new[]{new IncompatibleTranslationBehaviour()};text.Refresh();Check(text.Get("signal.stop")=="Stop!","Missing settings fallback");
            });
            Test("one structured packet renders independently in English and German",()=>{
                var english=new Localization(pack,new Harmony());AccessTools.AvailableType=typeof(FakeTranslationBehaviour);UnityEngine.Object.Found=new[]{new FakeTranslationBehaviour()};var german=new Localization(pack,new Harmony());
                var parts=new MessageParts{Id="movement_request",TrainId="test",TrainLabel="TuT 10",Origin="Bryson City",Destination="Sylva"};var packet=new ChatRecord{Parts=parts,Text=MessageTemplates.Render(parts)};
                Check(english.Message(packet)=="Locomotive TuT 10 requests permission to travel from Bryson City to Sylva.","EN receiver inherited sender language");
                Check(german.Message(packet)=="Lok TuT 10 möchte von Bryson City nach Sylva fahren.","DE template not rendered on receiver");
            });
            Test("received templates follow later language selection and preserve values",()=>{
                AccessTools.AvailableType=typeof(FakeTranslationBehaviour);var source=new FakeTranslationBehaviour();UnityEngine.Object.Found=new[]{source};var text=new Localization(pack,new Harmony());
                var record=new ChatRecord{Parts=new MessageParts{Id="arrived",TrainId="train",TrainLabel="TuT 10",Origin="Depot {A} 日本語"}};
                Check(text.Message(record)=="Lok TuT 10 ist in Depot {A} 日本語 angekommen.","Names/number changed");source.settings.language="en";text.Refresh();Check(text.Message(record)=="Locomotive TuT 10 has arrived at Depot {A} 日本語.","Received history did not switch to EN");
                source.settings.language="fr";text.Refresh();Check(text.Message(record).StartsWith("Locomotive"),"Missing language did not fall back to English");
            });
            Test("all template dictionaries match canonical fallback and have labels",()=>{
                var english=new Localization(pack,new Harmony());AccessTools.AvailableType=typeof(FakeTranslationBehaviour);UnityEngine.Object.Found=new[]{new FakeTranslationBehaviour()};var german=new Localization(pack,new Harmony());
                foreach(var template in MessageTemplates.All)
                {
                    Check(english.Get("template.text."+template.Id)==template.English,"Canonical English differs: "+template.Id);
                    var parts=new MessageParts{Id=template.Id,TrainLabel="TuT 10",Origin="Bryson",Destination="Sylva",Task="pickup"};string rendered=german.Message(new ChatRecord{Parts=parts});
                    Check((!template.NeedsTrain||rendered.Contains("TuT 10"))&&!rendered.Contains("{0}")&&german.Get("template.name."+template.Id)!="template.name."+template.Id,"Missing DE template/name: "+template.Id);
                }
            });
            Test("unknown future templates preserve the archived English fallback",()=>{
                var text=new Localization(pack,new Harmony());var record=new ChatRecord{Text="English fallback",Parts=new MessageParts{Id="future-template"}};
                Check(text.Message(record)=="English fallback","Unknown template did not use the transmitted fallback");
                Check(text.Message(new ChatRecord{Text="Nicht übersetzen: Sylva"})=="Nicht übersetzen: Sylva","Free text was auto-translated");
            });
            Test("crew preset tasks translate at each receiving endpoint",()=>{
                var english=new Localization(pack,new Harmony());AccessTools.AvailableType=typeof(FakeTranslationBehaviour);UnityEngine.Object.Found=new[]{new FakeTranslationBehaviour()};var german=new Localization(pack,new Harmony());
                var record=new ChatRecord{Parts=new MessageParts{Id="crew_assign",TrainLabel="TuT 10",Origin="Sylva",Task="deliver"}};
                Check(english.Message(record)=="Locomotive TuT 10: please handle delivering cars at Sylva.","English task missing");
                Check(german.Message(record)=="Lok TuT 10: bitte das Zustellen von Wagen in Sylva übernehmen.","German task missing");
                record.Parts.Task="custom";record.Parts.Detail="Gleis A prüfen";Check(english.Message(record).Contains("Gleis A prüfen")&&german.Message(record).Contains("Gleis A prüfen"),"Custom task text was changed");
            });
            Test("global information renders without a locomotive or translation dependency",()=>{
                var english=new Localization(pack,new Harmony());Check(english.Message(new ChatRecord{Parts=new MessageParts{Id="general_help"}})=="I need help.","Global fallback depends on train/language mod");
            });
            Console.WriteLine("RESULT "+passed+" language regression tests passed");
        }
        catch(Exception error){Console.Error.WriteLine("FAIL "+error);Environment.ExitCode=1;}
    }
}
