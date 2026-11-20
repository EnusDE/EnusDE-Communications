// Copyright (c) 2026 EnusDE. MIT License.
using System;
namespace EnusDE.Communications
{
    internal enum MicrophoneIndicator { Unavailable, Idle, Live }

    // Control boundary only. No microphone, playback, transport or recording is started here.
    // VoiceRuntime evaluates this gate before local capture and playback. Host relay consent is per peer.
    public sealed class VoiceSession
    {
        public const int ProtocolVersion = 1;
        public bool Active { get; private set; }
        public event Action Stopped;

        public bool Apply(bool connected, bool hostAllowsVoice, bool localOptIn,
            bool peerOptIn, int peerProtocol)
        {
            bool next = connected && hostAllowsVoice && localOptIn && peerOptIn
                && peerProtocol == ProtocolVersion;
            bool stop = Active && !next;
            Active = next;
            if (stop) Stopped?.Invoke(); // Adapter must discard pending audio and release devices.
            return Active;
        }

        // Call on disconnect, host/world change, or disposal. Reconnection requires fresh consent.
        public void Reset() { Apply(false, false, false, false, 0); }
    }
}
