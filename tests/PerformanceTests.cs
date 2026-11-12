using System;
using System.Globalization;
using System.IO;
using System.Linq;
using EnusDE.Communications;
static class PerformanceTests
{
    static string[][] Rows(PerformanceTrace trace)
    {using(var writer=new StringWriter()){trace.Write(writer);return writer.ToString().Trim().Split('\n').Select(s=>s.TrimEnd('\r').Split(';')).ToArray();}}
    public static void Run(Action<string,Action> test,Action<bool,string> check)
    {
        test("performance capture is inactive until explicitly started",()=>{
            int reads=0;var trace=new PerformanceTrace(()=>{reads++;return 0;},1000);
            using(trace.Measure(PerformanceStage.Poll)){}trace.NextFrame(1,false,true,false);trace.Input(true,true);trace.Sent(12);trace.Received(13);
            check(reads==0&&trace.Count==0&&Rows(trace).Length==1,"Inactive capture performed sampling");
        });
        test("frame gaps and measured work are stored separately for host and guest",()=>{
            long now=0;var trace=new PerformanceTrace(()=>now,1000);trace.Start();trace.NextFrame(1,false,true,false);
            using(trace.Measure(PerformanceStage.Snapshot)){now=7;}now=500;trace.NextFrame(2,true,true,true);trace.Input(true,false);trace.Sent(100);trace.Received(80);trace.Stop();
            var rows=Rows(trace);check(rows.Length==3&&rows[1][7]=="7.000"&&rows[2][2]=="500.000","Gap/work mixed up");
            check(rows[1][3]=="client"&&rows[2][3]=="host"&&rows[2][5]=="1"&&rows[2][16]=="1","Role/suspension/input missing");
            check(rows[2][18]=="1"&&rows[2][19]=="100"&&rows[2][20]=="1"&&rows[2][21]=="80","Traffic counts wrong");
        });
        test("capture automatically stops once at the duration limit",()=>{
            long now=0;var trace=new PerformanceTrace(()=>now,1000);trace.Start(1);trace.NextFrame(1,false,true,false);now=999;trace.NextFrame(2,false,true,false);now=1000;
            check(trace.NextFrame(3,false,true,false)&&!trace.Recording&&trace.Count==2&&!trace.NextFrame(4,false,true,false),"Time limit not bounded");
        });
        test("capture has a strict frame storage limit even with a stationary clock",()=>{
            var trace=new PerformanceTrace(()=>0,1000);trace.Start();for(int i=0;i<PerformanceTrace.MaxFrames;i++)check(!trace.NextFrame(i,false,true,false),"Early frame limit");
            check(trace.NextFrame(PerformanceTrace.MaxFrames,false,true,false)&&trace.Count==PerformanceTrace.MaxFrames&&!trace.Recording,"Unbounded sample storage");
        });
        test("old timing scopes cannot contaminate a restarted capture",()=>{
            long now=0;var trace=new PerformanceTrace(()=>now,1000);trace.Start();trace.NextFrame(1,false,true,false);var old=trace.Measure(PerformanceStage.Poll);
            trace.Stop();trace.Start();trace.NextFrame(2,false,true,false);now=200;old.Dispose();trace.Stop();check(Rows(trace)[1][9]=="0.000","Stale scope wrote into a new capture");
        });
        test("CSV is culture independent and has only numeric operational fields",()=>{
            var old=CultureInfo.CurrentCulture;try{
                CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("de-DE");long now=0;var trace=new PerformanceTrace(()=>now,1000);trace.Start();trace.NextFrame(1,false,true,false);
                using(trace.Measure(PerformanceStage.UI)){now=15;}trace.NextFrame(1,false,true,false);trace.Stop();var rows=Rows(trace);
                check(rows.Length==2&&rows[0].Length==26&&rows[1].Length==26&&rows[1][12]=="15.000"&&rows[1][25].EndsWith("Z"),"CSV shape/culture wrong");
                check(!rows[0].Any(s=>s.Contains("name")||s.Contains("text")||s.Contains("identity")),"Capture contains identity/chat columns");
            }finally{CultureInfo.CurrentCulture=old;}
        });
        test("active captures cannot write files mid-frame",()=>{
            var trace=new PerformanceTrace(()=>0,1000);trace.Start();bool rejected=false;try{Rows(trace);}catch(InvalidOperationException){rejected=true;}
            check(rejected&&trace.Recording,"Export mutated a running capture");trace.Stop();
        });
    }
}
