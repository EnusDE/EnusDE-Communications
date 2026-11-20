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
            var bytes=new byte[Bytes];
            int predictor=ToPcm16(pcm[0],gain),index=0;
            int previous=predictor,maxDelta=0;
            for(int i=1;i<8;i++)
            {
                int current=ToPcm16(pcm[i],gain);
                maxDelta=Math.Max(maxDelta,Math.Abs(current-previous));
                previous=current;
            }
            while(index<Steps.Length-1&&Steps[index]<maxDelta/2)index++;
            bytes[0]=(byte)predictor;
            bytes[1]=(byte)(predictor>>8);
            bytes[2]=(byte)index;
            for(int i=0;i<Samples;i++)
            {
                int diff=ToPcm16(pcm[i],gain)-predictor;
                int code=diff<0?8:0;if(diff<0)diff=-diff;int step=Steps[index];
                if(diff>=step){code|=4;diff-=step;}if(diff>=(step>>1)){code|=2;diff-=step>>1;}if(diff>=(step>>2))code|=1;
                Decode(code,ref predictor,ref index);bytes[4+i/2]|=(byte)(code<<((i&1)*4));
            }
            return bytes;
        }
        static int ToPcm16(float sample,float gain)
        {
            if(Single.IsNaN(sample)||Single.IsInfinity(sample))return 0;
            return (int)(Math.Max(-1,Math.Min(1,sample*gain))*32767);
        }
        public static float[] Decode(byte[] bytes)
        {
            if(bytes==null||bytes.Length!=Bytes||bytes[2]>88||bytes[3]!=0)return null;
            int predictor=(short)(bytes[0]|bytes[1]<<8),index=bytes[2];var pcm=new float[Samples];
            for(int i=0;i<Samples;i++)pcm[i]=Decode((bytes[4+i/2]>>((i&1)*4))&15,ref predictor,ref index)/32768f;
            return pcm;
        }
    }
    // Bounded jitter buffer. The mixer consumes samples at the output device's rate.
    public sealed class VoiceBuffer
    {
        const int FrameSamples=VoiceAudio.Samples;
        readonly float[] samples=new float[FrameSamples*5];
        readonly object sync=new object();
        int read,count;
        double phase;
        bool primed;
        float[] previousFrame;

        public void Clear()
        {
            lock(sync)
            {
                read=count=0;
                phase=0;
                primed=false;
                previousFrame=null;
            }
        }

        public void Push(float[] frame,bool missingFrame=false)
        {
            if(frame==null||frame.Length!=FrameSamples)return;
            lock(sync)
            {
                // Conceal one lost packet with a fading copy of the previous block.
                // Skip concealment when playback has already drained or the queue is full.
                if(missingFrame&&primed&&previousFrame!=null&&count+2*FrameSamples<=samples.Length)
                {
                    int write=(read+count)%samples.Length;
                    for(int i=0;i<FrameSamples;i++)
                    {
                        float weight=(1f-(float)i/FrameSamples)*.8f;
                        samples[write]=previousFrame[i]*weight;
                        if(++write==samples.Length)write=0;
                    }
                    count+=FrameSamples;
                }
                int overflow=count+FrameSamples-samples.Length;
                if(overflow>0)
                {
                    Advance(overflow);
                    phase=0;
                }
                int start=(read+count)%samples.Length;
                int first=Math.Min(FrameSamples,samples.Length-start);
                Array.Copy(frame,0,samples,start,first);
                Array.Copy(frame,first,samples,0,FrameSamples-first);
                count+=FrameSamples;
                previousFrame=frame;
                if(count>=FrameSamples*3)primed=true;
            }
        }

        public void Read(float[] output,int channels,int outputRate,float gain)
        {
            Array.Clear(output,0,output.Length);
            if(channels<=0||outputRate<=0)return;
            double step=(double)VoiceAudio.Rate/outputRate;
            lock(sync)
            {
                if(!primed)return;
                for(int offset=0;offset<=output.Length-channels&&count>0;offset+=channels)
                {
                    int next=read+1==samples.Length?0:read+1;
                    float sample=samples[read];
                    if(count>1)sample+=(samples[next]-sample)*(float)phase;
                    sample*=gain;
                    for(int channel=0;channel<channels;channel++)output[offset+channel]=sample;

                    phase+=step;
                    int whole=(int)phase;
                    phase-=whole;
                    if(whole>0)Advance(Math.Min(whole,count));
                }
                if(count==0)
                {
                    phase=0;
                    primed=false;
                    previousFrame=null;
                }
            }
        }

        void Advance(int length)
        {
            read=(read+length)%samples.Length;
            count-=length;
        }
    }
}
