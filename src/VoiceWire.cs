// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.IO;
namespace EnusDE.Communications
{
    public sealed class VoicePacket
    {
        public byte Kind; // 1 opt-in heartbeat, 2 host grant/denial, 3 audio, 4 leave
        public Guid Consent;
        public ulong Sender,Stream;
        public uint Sequence;
        public float Range;
        public byte[] Audio;
    }
    public static class VoiceWire
    {
        public const int Header=44, MaxBytes=Header+VoiceAudio.Bytes;
        public static byte[] Write(VoicePacket packet)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write((byte)'E');writer.Write((byte)'V');writer.Write((byte)VoiceSession.ProtocolVersion);writer.Write(packet.Kind);
                writer.Write(packet.Consent.ToByteArray());writer.Write(packet.Sender);writer.Write(packet.Sequence);writer.Write(packet.Range);writer.Write(packet.Stream);
                if(packet.Audio!=null)writer.Write(packet.Audio);return stream.ToArray();
            }
        }
        public static VoicePacket Read(byte[] data)
        {
            if(data==null||data.Length<Header||data.Length>MaxBytes||data[0]!='E'||data[1]!='V'||data[2]!=VoiceSession.ProtocolVersion||data[3]<1||data[3]>4)return null;
            bool audio=data[3]==3;if(data.Length!=(audio?MaxBytes:Header))return null;
            using(var reader=new BinaryReader(new MemoryStream(data)))
            {
                reader.ReadBytes(4);var p=new VoicePacket{Kind=data[3],Consent=new Guid(reader.ReadBytes(16)),Sender=reader.ReadUInt64(),Sequence=reader.ReadUInt32(),Range=reader.ReadSingle(),Stream=reader.ReadUInt64()};
                if(p.Consent==Guid.Empty||p.Sender==0||Single.IsNaN(p.Range)||Single.IsInfinity(p.Range)||p.Range<0||p.Range>100)return null;
                if(audio){p.Audio=reader.ReadBytes(VoiceAudio.Bytes);if(p.Stream==0||p.Audio[2]>88||p.Audio[3]!=0)return null;}return p;
            }
        }
        public static bool Newer(uint next,uint previous)=>next!=previous&&unchecked(next-previous)<0x80000000u;
        public static float Gain(Person speaker,Person listener,float range)
        {
            if(speaker==null||listener==null||range<=0||Single.IsNaN(range)||Single.IsInfinity(range))return 0;
            double x=(double)speaker.X-listener.X,y=(double)speaker.Y-listener.Y,z=(double)speaker.Z-listener.Z;
            double distance=Math.Sqrt(x*x+y*y+z*z);if(Double.IsNaN(distance)||Double.IsInfinity(distance)||distance>=range)return 0;
            return (float)Math.Min(1,(range-distance)/Math.Max(1,range-2));
        }
    }
}
