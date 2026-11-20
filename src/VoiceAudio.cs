// Copyright (c) 2026 EnusDE. MIT License.
using System;
namespace EnusDE.Communications
{
    // Independent 40 ms IMA ADPCM blocks: losing a datagram cannot corrupt later audio.
    public static class VoiceAudio
    {
        public const int Rate=16000, Samples=640, Bytes=324;
        static readonly int[] Steps={7,8,9,10,11,12,13,14,16,17,19,21,23,25,28,31,34,37,41,45,50,55,60,66,73,80,88,97,107,118,130,143,157,173,190,209,230,253,279,307,337,371,408,449,494,544,598,658,724,796,876,963,1060,1166,1282,1411,1552,1707,1878,2066,2272,2499,2749,3024,3327,3660,4026,4428,4871,5358,5894,6484,7132,7845,8630,9493,10442,11487,12635,13899,15289,16818,18500,20350,22385,24623,27086,29794,32767};
        static readonly int[] Adjust={-1,-1,-1,-1,2,4,6,8};
        static int Clamp(int v,int min,int max)=>Math.Max(min,Math.Min(max,v));
        static int Decode(int code,ref int predictor,ref int index)
        {
            int step=Steps[index],delta=step>>3;
            if((code&1)!=0)delta+=step>>2;if((code&2)!=0)delta+=step>>1;if((code&4)!=0)delta+=step;
            predictor=Clamp(predictor+((code&8)!=0?-delta:delta),-32768,32767);
            index=Clamp(index+Adjust[code&7],0,88);return predictor;
        }
        public static byte[] Encode(float[] pcm,float gain)
        {
            if(pcm==null||pcm.Length!=Samples||Single.IsNaN(gain)||Single.IsInfinity(gain))throw new ArgumentException("Invalid voice samples.");
            var bytes=new byte[Bytes];int predictor=0,index=0;
            for(int i=0;i<Samples;i++)
            {
                float sample=pcm[i];if(Single.IsNaN(sample)||Single.IsInfinity(sample))sample=0;
                int value=(int)(Math.Max(-1,Math.Min(1,sample*gain))*32767),diff=value-predictor;
                int code=diff<0?8:0;if(diff<0)diff=-diff;int step=Steps[index];
                if(diff>=step){code|=4;diff-=step;}if(diff>=(step>>1)){code|=2;diff-=step>>1;}if(diff>=(step>>2))code|=1;
                Decode(code,ref predictor,ref index);bytes[4+i/2]|=(byte)(code<<((i&1)*4));
            }
            return bytes;
        }
        public static float[] Decode(byte[] bytes)
        {
            if(bytes==null||bytes.Length!=Bytes||bytes[2]>88||bytes[3]!=0)return null;
            int predictor=(short)(bytes[0]|bytes[1]<<8),index=bytes[2];var pcm=new float[Samples];
            for(int i=0;i<Samples;i++)pcm[i]=Decode((bytes[4+i/2]>>((i&1)*4))&15,ref predictor,ref index)/32768f;
            return pcm;
        }
    }
    // Bounded jitter buffer. Audio thread only touches this lock-protected sample ring.
    public sealed class VoiceBuffer
    {
        readonly float[] samples=new float[VoiceAudio.Samples*5];readonly object sync=new object();
        int read,count;bool primed;
        public void Clear(){lock(sync){read=count=0;primed=false;}}
        public void Push(float[] frame)
        {
            if(frame==null||frame.Length!=VoiceAudio.Samples)return;
            lock(sync)
            {
                if(count+frame.Length>samples.Length){read=count=0;primed=false;}
                for(int i=0;i<frame.Length;i++)samples[(read+count+i)%samples.Length]=frame[i];
                count+=frame.Length;if(count>=VoiceAudio.Samples*2)primed=true;
            }
        }
        public void Read(float[] output)
        {
            lock(sync)
            {
                Array.Clear(output,0,output.Length);if(!primed)return;
                int n=Math.Min(count,output.Length);
                for(int i=0;i<n;i++)output[i]=samples[(read+i)%samples.Length];
                read=(read+n)%samples.Length;count-=n;if(count==0)primed=false;
            }
        }
    }
}
