using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using EnusDE.Communications;
using Newtonsoft.Json;
static class MetadataTests
{
    public static void Run(string game,string project,Action<string,Action> test,Action<bool,string> check)
    {
        string managed=Path.Combine(game,"Railroader_Data","Managed");
        using(var file=File.OpenRead(Path.Combine(managed,"Railroader.dll")))
        using(var pe=new PEReader(file))
        {
            var md=pe.GetMetadataReader();
            Func<TypeDefinitionHandle,string> full=null;
            full=h=>{var t=md.GetTypeDefinition(h);return t.GetDeclaringType().IsNil?md.GetString(t.Namespace)+"."+md.GetString(t.Name):full(t.GetDeclaringType())+"+"+md.GetString(t.Name);};
            Action<string,string,bool> member=(type,name,field)=>{
                var handle=md.TypeDefinitions.Single(h=>full(h)==type);var definition=md.GetTypeDefinition(handle);
                bool found=field?definition.GetFields().Any(h=>md.GetString(md.GetFieldDefinition(h).Name)==name):definition.GetMethods().Any(h=>md.GetString(md.GetMethodDefinition(h).Name)==name);
                check(found,"Missing native member "+type+"."+name);
            };
            test("actual 0.6.3 game build and private integration members",()=>{
                check(md.GetGuid(md.GetModuleDefinition().Mvid)==new Guid("aac29a8a-ef90-41c1-b17a-0d75169dc6b8"),"Unexpected game build");
                member("UI.GameInput","_focusPause",true);member("UI.GameInput","Update",false);member("UI.Common.EscapeHandler","HandleEscape",false);
                member("Game.HostManager","_clients",true);member("Game.HostManager","AccessLevelForPlayerId",false);member("Game.HostManager+Client","Status",true);member("Game.HostManager+Client","get_PlayerId",false);
                member("Network.Client.ClientManager","_client",true);member("Network.Client.GameClient","get_ConnectionInfo",false);
                member("Network.Steam.SteamClient","Update",false);
                member("Network.Steam.SteamServer","Update",false);
                member("Helpers.WorldTransformer","GameToWorld",false);
                member("Game.HomeDirectory.UserPaths","get_RailroaderDirectory",false);member("Game.State.PlayerPropertiesManager","_object",true);
                member("UI.EngineControls.LocomotiveControlsUIAdapter","nameLabel",true);member("UI.EngineControls.LocomotiveControlsUIAdapter","get_Locomotive",false);member("UI.TopRightArea","clockText",true);
                member("Game.State.StateManager","HandleCharacterMessage",false);member("Game.HostManager","SendTo",false);member("Network.Multiplayer","get_Mode",false);
                member("UI.Windows.WindowManager","RegisterCustomWindow",false);member("UI.Windows.CustomWindowHandle","Create",false);member("Track.Signals.CTCPanelButton","Activate",false);
                member("UI.PreferencesWindow.PreferencesBuilder","BuildTabs",false);member("UI.PreferencesWindow.PreferencesBuilder","SelectedTab",true);
                member("UI.Windows.WindowManager","get_TopmostShownWindow",false);member("UI.Windows.BuilderWindowBase","Show",false);
                member("UI.Builder.UIBuilderAssets","CreateButton",false);member("UI.Builder.UIBuilderAssets","uiSprite",true);
                member("Model.Ops.Timetable.TimetableController","get_Shared",false);member("Model.Ops.Timetable.TimetableController","branches",true);
                member("Model.Ops.Timetable.TimetableBranch","stations",true);member("Model.Ops.Timetable.TimetableStation","name",true);
                var builder=md.TypeDefinitions.Select(h=>md.GetTypeDefinition(h)).Single(t=>md.GetString(t.Name)=="UIPanelBuilder");
                var slider=builder.GetMethods().Select(h=>md.GetMethodDefinition(h)).Single(m=>md.GetString(m.Name)=="AddSliderQuantized");
                var parameters=slider.GetParameters().Select(h=>md.GetParameter(h)).Where(p=>p.SequenceNumber>0).Select(p=>md.GetString(p.Name)).ToArray();
                check(parameters.Skip(3).SequenceEqual(new[]{"increment","minValue","maxValue","editingEndedAction"}),"Quantized slider contract changed");
                var toggle=builder.GetMethods().Select(h=>md.GetMethodDefinition(h)).Single(m=>md.GetString(m.Name)=="AddFieldToggle");
                check(toggle.GetParameters().Select(h=>md.GetParameter(h)).Any(p=>p.SequenceNumber==4&&md.GetString(p.Name)=="interactable"),"Toggle interactivity parameter contract changed");
            });
        }
        test("compiled mod entry point and assembly references",()=>{
            using(var file=File.OpenRead(Path.Combine(project,"build","EnusDE.Communications.dll")))using(var pe=new PEReader(file))
            {
                var md=pe.GetMetadataReader();var entry=md.TypeDefinitions.Select(h=>md.GetTypeDefinition(h)).Single(t=>md.GetString(t.Name)=="Entry");
                var contracts=entry.GetInterfaceImplementations().Select(h=>md.GetInterfaceImplementation(h).Interface).Select(h=>md.GetString(md.GetTypeReference((TypeReferenceHandle)h).Name)).ToArray();
                check(contracts.Contains("IModEntryPoint")&&contracts.Contains("IWorldLifecycle"),"Missing native entry contracts");
                var refs=md.AssemblyReferences.Select(h=>md.GetString(md.GetAssemblyReference(h).Name)).ToArray();
                foreach(string name in refs){check(name!="EnusDE.GermanTranslation","Hard assembly reference to translator");check(name!="System.Private.CoreLib"&&name!="System.Runtime","Accidentally compiled for .NET Core");check(name=="0Harmony"||name=="EnusDE.ModMenu"||File.Exists(Path.Combine(managed,name+".dll")),"Unresolved game reference "+name);}
                check(!refs.Contains("EnusDE.ModMenu"),"External menu reference must be removed");
                check(!md.TypeReferences.Any(h=>md.GetString(md.GetTypeReference(h).Namespace)=="EnusDE.ModMenu"),"External menu types remain");
            }
        });
        test("standalone manifest has no translator dependency",()=>{
            var manifest=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(project,"EnusDE-Communications","Info.json")));
            var required=manifest["dependencies"]["required"].Select(x=>(string)x).ToArray();
            check(required.SequenceEqual(new[]{"rr/game ==0.6.3"}),"Only the game may be a required dependency");
            check(manifest["dependencies"]["optional"]==null,"Translator must not be a dependency");
            check((string)manifest["scope"]=="optional","Players without the mod must be allowed to join");
        });
        test("chat buttons own their geometry instead of cloning self-sizing native prefabs",()=>{
            using(var file=File.OpenRead(Path.Combine(project,"build","EnusDE.Communications.dll")))using(var pe=new PEReader(file))
            {
                var md=pe.GetMetadataReader();
                check(!md.MemberReferences.Any(h=>md.GetString(md.GetMemberReference(h).Name)=="CreateButton"),"Native button factory can override fixed chat/icon geometry");
                var factory=md.TypeDefinitions.Select(h=>md.GetTypeDefinition(h)).Single(t=>md.GetString(t.Name)=="UiFactory");
                check(factory.GetMethods().Any(h=>md.GetString(md.GetMethodDefinition(h).Name)=="Button"),"Owned button factory missing");
            }
        });
        test("EN/DE language dictionaries and fixed signal coverage",()=>{
            var english=JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(Path.Combine(project,"EnusDE-Communications","Languages","en.json")));
            var german=JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(Path.Combine(project,"EnusDE-Communications","Languages","de.json")));
            check(english.Keys.OrderBy(x=>x).SequenceEqual(german.Keys.OrderBy(x=>x)),"EN/DE keys differ");
            foreach(string key in SignalText.English.Keys)check(english.ContainsKey("signal."+key),"Missing signal "+key);
            foreach(string path in Directory.GetFiles(Path.Combine(project,"src"),"*.cs"))foreach(Match match in Regex.Matches(File.ReadAllText(path),"(?:T|Text\\.Get)\\(\"([^\"]+)\""))
            {string key=match.Groups[1].Value;if(!key.EndsWith("."))check(english.ContainsKey(key),"Missing UI text "+key);}
            check(english.Values.All(v=>!String.IsNullOrWhiteSpace(v))&&german.Values.All(v=>!String.IsNullOrWhiteSpace(v)),"Empty translation");
        });
    }
}
