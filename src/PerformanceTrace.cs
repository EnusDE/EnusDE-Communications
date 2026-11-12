// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
namespace EnusDE.Communications
{
    internal enum PerformanceStage { Update,Snapshot,Language,Poll,Send,Receive,UI,Rebuild,NativeNetwork,Archive,Count }
    // Explicit, bounded capture. No disk access, message text or peer identities during sampling.
    internal sealed class PerformanceTrace
    {
        internal const int MaxFrames=12000;
        sealed class Frame
        {
            internal int Number,Gc0,Gc1,Gc2,Sent,SentBytes,Received,ReceivedBytes;
            internal double Elapsed,Gap;
            internal bool Host,Connected,Suspended,Captured,NativePaused;
            internal readonly double[] Work=new double[(int)PerformanceStage.Count];
        }
        readonly Func<long> ticks;
        readonly double milliseconds;
        Frame[] frames;
        int count,generation,lastGc0,lastGc1,lastGc2;
        long started,previous;
        DateTime startedUtc;
        double duration;
        internal bool Recording {get;private set;}
        internal int Count=>count;
        internal PerformanceTrace(Func<long> clock=null,long frequency=0)
        {ticks=clock??Stopwatch.GetTimestamp;milliseconds=1000.0/(frequency>0?frequency:Stopwatch.Frequency);}
        internal void Start(double seconds=60)
        {
            // Allocate once on explicit start; reuse frame storage during the capture.
            if(frames==null){frames=new Frame[MaxFrames];for(int i=0;i<frames.Length;i++)frames[i]=new Frame();}
            count=0;generation++;duration=Math.Max(1,Math.Min(60,seconds))*1000;
            startedUtc=DateTime.UtcNow;started=previous=ticks();lastGc0=Collections(0);lastGc1=Collections(1);lastGc2=Collections(2);Recording=true;
        }
        // Returns true once, when a running capture has reached its time/size limit.
        internal bool NextFrame(int number,bool host,bool connected,bool suspended)
        {
            if(!Recording)return false;
            long now=ticks();if((now-started)*milliseconds>=duration||count>=MaxFrames){Stop();return true;}
            if(count>0&&frames[count-1].Number==number)return false;
            var f=frames[count++];f.Number=number;f.Elapsed=(now-started)*milliseconds;f.Gap=count==1?0:(now-previous)*milliseconds;previous=now;
            f.Host=host;f.Connected=connected;f.Suspended=suspended;f.Captured=f.NativePaused=false;Array.Clear(f.Work,0,f.Work.Length);
            f.Sent=f.SentBytes=f.Received=f.ReceivedBytes=0;
            int g0=Collections(0),g1=Collections(1),g2=Collections(2);f.Gc0=g0-lastGc0;f.Gc1=g1-lastGc1;f.Gc2=g2-lastGc2;lastGc0=g0;lastGc1=g1;lastGc2=g2;
            return false;
        }
        internal void Stop(){Recording=false;generation++;}
        internal Measurement Measure(PerformanceStage stage)=>Recording&&count>0?new Measurement(this,stage,ticks(),generation,count-1):default;
        internal readonly struct Measurement:IDisposable
        {
            readonly PerformanceTrace owner;readonly PerformanceStage stage;readonly long start;readonly int version,index;
            internal Measurement(PerformanceTrace trace,PerformanceStage part,long timestamp,int epoch,int frame){owner=trace;stage=part;start=timestamp;version=epoch;index=frame;}
            public void Dispose(){if(owner!=null&&owner.Recording&&owner.generation==version)owner.frames[index].Work[(int)stage]+=Math.Max(0,(owner.ticks()-start)*owner.milliseconds);}
        }
        internal void Input(bool captured,bool nativePaused){if(Recording&&count>0){frames[count-1].Captured|=captured;frames[count-1].NativePaused|=nativePaused;}}
        internal void Sent(int bytes){if(Recording&&count>0){frames[count-1].Sent++;frames[count-1].SentBytes+=Math.Max(0,bytes);}}
        internal void Received(int bytes){if(Recording&&count>0){frames[count-1].Received++;frames[count-1].ReceivedBytes+=Math.Max(0,bytes);}}
        internal void Write(TextWriter output)
        {
            if(Recording)throw new InvalidOperationException("Stop the performance capture before exporting.");
            output.WriteLine("frame;elapsed_ms;frame_gap_ms;role;chat_connected;chat_suspended;update_ms;snapshot_ms;language_ms;chat_poll_ms;chat_send_ms;chat_receive_ms;ui_ms;ui_rebuild_ms;native_network_ms;archive_ms;input_captured;native_focus_pause;sent_packets;sent_bytes;received_packets;received_bytes;gc0;gc1;gc2;utc_iso");
            var line=new StringBuilder(320);
            for(int i=0;i<count;i++)
            {
                var f=frames[i];line.Clear();line.Append(f.Number).Append(';').Append(N(f.Elapsed)).Append(';').Append(N(f.Gap)).Append(';').Append(f.Host?"host":"client").Append(';').Append(f.Connected?1:0).Append(';').Append(f.Suspended?1:0);
                foreach(double work in f.Work)line.Append(';').Append(N(work));
                line.Append(';').Append(f.Captured?1:0).Append(';').Append(f.NativePaused?1:0).Append(';').Append(f.Sent).Append(';').Append(f.SentBytes).Append(';').Append(f.Received).Append(';').Append(f.ReceivedBytes).Append(';').Append(f.Gc0).Append(';').Append(f.Gc1).Append(';').Append(f.Gc2).Append(';').Append(startedUtc.AddMilliseconds(f.Elapsed).ToString("o",CultureInfo.InvariantCulture));output.WriteLine(line);
            }
        }
        static string N(double value)=>value.ToString("0.000",CultureInfo.InvariantCulture);
        static int Collections(int generation)=>generation<=GC.MaxGeneration?GC.CollectionCount(generation):0;
    }
}
