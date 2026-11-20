// Test-only audio/game boundary; never included in the mod DLL.
using System;
using System.Collections.Generic;
using EnusDE.Communications;
namespace UnityEngine
{
    public class Object {public static void Destroy(Object o){}}
    public class MonoBehaviour:Object {}
    public static class AudioSettings
    {
        public static int outputSampleRate=48000;
        public static event Action<bool> OnAudioConfigurationChanged;
        public static void Change(){OnAudioConfigurationChanged?.Invoke(true);}
    }
    public class Transform {public void SetParent(Transform t,bool keep){}}
    public class GameObject:Object
    {
        public readonly Transform transform=new Transform();public GameObject(string name){}
        public T AddComponent<T>() where T:new()=>new T();
    }
    public class AudioClip:Object
    {
        public int frequency=16000,channels=1,samples=32000;
        public static AudioClip Create(string n,int count,int c,int rate,bool stream,Action<float[]> read)=>new AudioClip();
        public bool GetData(float[] data,int offset){for(int i=0;i<data.Length;i++)data[i]=(float)(.3*Math.Sin((offset+i)*.1));return true;}
    }
    public class AudioSource
    {
        public float spatialBlend,volume;public bool loop,playOnAwake;public AudioClip clip;
        public static int Plays,Stops;public void Play(){Plays++;}public void Stop(){Stops++;}
    }
    public static class Microphone
    {
        public static string[] devices={"Test microphone"};public static int Starts,Ends,Position;public static bool Recording;
        public static AudioClip Start(string name,bool loop,int length,int frequency){Starts++;Recording=true;Position=0;return new AudioClip();}
        public static int GetPosition(string device)=>Position;
        public static bool IsRecording(string device)=>Recording;
        public static void End(string device){Ends++;Recording=false;}
    }
    public static class Time {public static double realtimeSinceStartupAsDouble=10;}
    public static class Application {public static bool isFocused=true;}
}
namespace EnusDE.Communications
{
    internal sealed class VoiceTestText {internal string Get(string key)=>key;}
    internal sealed class VoiceTestLog {internal void Warning(string text){throw new Exception(text);}}
    internal sealed class Entry
    {
        internal ModSettings Settings=new ModSettings();internal VoiceTestText Text=new VoiceTestText();internal VoiceTestLog Log=new VoiceTestLog();
        internal bool VoiceHeld,VoiceBindingConflict,SettingsOpen,RoleTestActive;
    }
    internal sealed class VoiceTestClient {internal bool Connected=true;}
    internal sealed class VoiceTestUi
    {
        internal bool Captures;
        internal bool MicrophoneInputBlocked=>Captures;
    }
    internal sealed class CommsRuntime
    {
        internal Entry Entry=new Entry();internal WorldView View=new WorldView();internal VoiceTestClient Client=new VoiceTestClient();
        internal VoiceTestUi UI=new VoiceTestUi();internal bool DiagnosticSuspended;
        internal UnityEngine.Transform transform=new UnityEngine.Transform();
        internal string LocalId=>GameAdapter.Local.ToString();
        internal bool VoiceCompatible(ulong id)=>View.People.ContainsKey(id.ToString());
    }
    internal static class GameAdapter
    {
        internal static bool Ready=true,MultiplayerActive=true,IsHost=true;internal static ulong Local=1,Host=1;
        internal static ulong HostSteamId()=>Host;
    }
    internal static class GameMenus {internal static bool IsOpen;}
}
