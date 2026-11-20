// Copyright (c) 2026 EnusDE. MIT License.
using UnityEngine;

namespace EnusDE.Communications
{
    public sealed class VoicePlayback : MonoBehaviour
    {
        internal readonly VoiceBuffer Buffer=new VoiceBuffer();
        internal volatile float Gain;
        volatile int outputRate=VoiceAudio.Rate;

        void OnEnable()
        {
            UpdateOutputRate(false);
            AudioSettings.OnAudioConfigurationChanged+=UpdateOutputRate;
        }

        void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged-=UpdateOutputRate;
            Buffer.Clear();
        }

        void UpdateOutputRate(bool deviceChanged)
        {
            int rate=AudioSettings.outputSampleRate;
            if(rate>0)outputRate=rate;
        }

        // Unity calls this on the audio thread. The first filter on a source
        // without a clip supplies the PCM directly to the audio mixer.
        void OnAudioFilterRead(float[] data,int channels)
        {
            Buffer.Read(data,channels,outputRate,Gain);
        }
    }
}
