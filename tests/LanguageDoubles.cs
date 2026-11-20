// Test-only stand-ins for the Unity/Harmony boundary. Not compiled into the mod.
using System;
using System.Reflection;
using System.Reflection.Emit;
namespace UnityEngine
{
    public enum FindObjectsSortMode { None }
    public class Object
    {
        public static Object[] Found=new Object[0];public static bool FindThrows;public bool Destroyed;
        public static Object[] FindObjectsByType(Type type,FindObjectsSortMode mode){if(FindThrows)throw new InvalidOperationException("Optional component unavailable");return Found;}
        public static bool operator ==(Object a,Object b){return (ReferenceEquals(a,null)||a.Destroyed)==(ReferenceEquals(b,null)||b.Destroyed);}
        public static bool operator !=(Object a,Object b)=>!(a==b);
        public override bool Equals(object other)=>ReferenceEquals(this,other);
        public override int GetHashCode()=>base.GetHashCode();
    }
    public class Component:Object {public object Parent;public T GetComponentInParent<T>() where T:class=>Parent as T;}
    public class MonoBehaviour:Component { }
}
namespace HarmonyLib
{
    public static class AccessTools
    {
        static Type availableType;
        public static int BroadLookups;
        public static Type AvailableType
        {
            get=>availableType;
            set
            {
                availableType=value;
                // Simulate the assembly-load notification accompanying a newly available mod.
                AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("LanguageTest"+Guid.NewGuid().ToString("N")),AssemblyBuilderAccess.Run);
            }
        }
        public static Type TypeByName(string name){BroadLookups++;return AvailableType;}
        public static FieldInfo Field(Type type,string name)=>type.GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static);
        public static MethodInfo Method(Type type,string name,Type[] args)=>type.GetMethod(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static,null,args,null);
    }
    public class HarmonyMethod {public HarmonyMethod(Type type,string name) {}}
    public class Harmony
    {
        public bool FailPatch;public int Patches;
        public void Patch(MethodInfo method,HarmonyMethod prefix){Patches++;if(FailPatch)throw new InvalidOperationException("Optional translator changed");}
    }
}
public sealed class LanguageTestAssembly:Assembly
{
    public int ExactLookups;
    public int FullEnumerations;
    public override Type GetType(string name,bool throwOnError,bool ignoreCase)
    {
        ExactLookups++;
        if(name!="EnusDE.GermanUi.GermanUiBehaviour")throw new Exception("Unexpected type lookup");
        return HarmonyLib.AccessTools.AvailableType;
    }
    public override Type[] GetTypes()
    {
        FullEnumerations++;
        throw new Exception("Optional language discovery must not enumerate all types");
    }
}
public sealed class FakeLanguageSettings {public string language="de";}
public sealed class FakeTranslationBehaviour:UnityEngine.MonoBehaviour
{
    public FakeLanguageSettings settings=new FakeLanguageSettings();
    public bool Eligible(UnityEngine.Component text)=>true;
}
public sealed class IncompatibleTranslationBehaviour:UnityEngine.MonoBehaviour { }
