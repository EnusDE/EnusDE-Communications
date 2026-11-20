// Copyright (c) 2026 EnusDE. MIT License.
using System;
using EnusDE.Communications;
public static class VoiceSessionTests
{
    public static int Main()
    {
        var defaults = new ModSettings();
        var legacy = Newtonsoft.Json.JsonConvert.DeserializeObject<ModSettings>("{}");
        legacy.Normalize();
        if (defaults.VoiceEnabled || defaults.Host.VoiceAllowed || legacy.VoiceEnabled || legacy.Host.VoiceAllowed)
            throw new Exception("New and legacy settings must disable both permissions.");
        var session = new VoiceSession();
        int stops = 0;
        session.Stopped += () => stops++;
        if (session.Active) throw new Exception("Voice must start disabled.");
        for (int mask = 0; mask < 16; mask++)
        {
            bool active = session.Apply((mask & 1) != 0, (mask & 2) != 0,
                (mask & 4) != 0, (mask & 8) != 0, VoiceSession.ProtocolVersion);
            if (active != (mask == 15)) throw new Exception("Every consent gate is required.");
        }
        session.Reset();
        if (session.Active || stops != 1) throw new Exception("Reset must stop active voice.");
        session.Reset();
        if (stops != 1) throw new Exception("Reset must be idempotent.");
        if (session.Apply(true, true, true, true, 0)) throw new Exception("Reject incompatible peers.");
        session.Apply(true, true, true, true, VoiceSession.ProtocolVersion);
        session.Apply(true, false, true, true, VoiceSession.ProtocolVersion);
        if (session.Active || stops != 2) throw new Exception("Host revocation must stop voice.");
        session.Apply(true, true, true, true, VoiceSession.ProtocolVersion);
        session.Apply(true, true, false, true, VoiceSession.ProtocolVersion);
        if (session.Active || stops != 3) throw new Exception("Local revocation must stop voice.");
        Console.WriteLine("Voice consent gates, protocol rejection, revocation and reset passed.");
        VoiceRuntimeTests.Run();
        return 0;
    }
}
