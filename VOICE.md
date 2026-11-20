# Experimental proximity voice — 0.6.8

[Deutsche Kurzanleitung](#deutsche-kurzanleitung)

This is an unpublished test implementation for Railroader **0.6.3**. Install the same 0.6.8 test build on the host and voice participants. The existing text protocol stays at 4; voice uses its own protocol 1 and Steam messages channel 189732. Players without Communications, or with voice disabled, remain able to join normally.

## Set up and test on two PCs

1. Close Railroader on both PCs before replacing the DLL. Preserve the mod's Data folder and personal ModSettings. Keep only one installed copy. Enable native DLL mods and restart if necessary.
2. Host a multiplayer world. Open **Preferences → Mods → EnusDE Communications** and enable **Allow proximity voice for this session**. The default hearing distance is 40 metres; the host can choose 5–100 metres.
3. On each PC that should speak or listen, enable **Participate in proximity voice**. This is independent of host permission: the host can relay without listening or using their microphone. Both switches reset to off every time a world loads.
4. Use **Select next microphone** to cycle through the system default and detected devices. Grant Railroader microphone access in your OS if required. Assign an unused **Push-to-talk key**; it starts unbound. A collision with the chat or wheel key blocks transmission. Use headphones to avoid feedback.
5. Close menus, stand together and hold the talk key. A HUD line shows voice readiness or transmission. Release to stop microphone capture. Speech from nearby participants fades with distance. This first implementation has no directional panning, radio, wall occlusion or locomotive sound insulation. Playback follows the game's audio output device.
6. Test both directions, moving beyond the host's hearing distance and back. Test a third participant, simultaneous speech and per-player muting. Mute choices persist locally by player ID; microphone choice, gain, volume and key are also personal settings.
7. Disable personal participation during speech: that PC must immediately stop capture and playback. Disable host permission: relay and host capture must stop; clients stop on the denial or when their short host grant expires (up to three seconds if the denial is lost). Text chat must continue.
8. Test a missing/removed microphone, released/rebound key, lost focus, open menus, disconnect/reconnect, world reload and local role test. Menus/focus loss stop transmission. Disconnects, expired grants, world changes and role testing clear the local audio resources.
9. Join with one unmodified player and one participant with voice off. Neither should be required to install/enable voice. Compare game performance in the same save and workflow with voice off and on.

## Implementation and limits

Unity captures the selected microphone only while push-to-talk is held and participation is permitted. Capture requests mono-compatible 16 kHz audio. Devices must support the requested capture format; unsupported or unavailable devices show an error. There is no automatic device substitution for a missing explicitly selected microphone.

Audio uses independently decodable 40 ms IMA ADPCM blocks (640 samples, 324 bytes), implemented in the mod without an added codec dependency. The full application datagram is 368 bytes: about 74 kbit/s per active speaker per route, before Steam/network overhead. The host's upload grows with nearby recipients. This initial codec favors a small independent implementation over modern low-bitrate codecs; live intelligibility, latency and CPU/network costs still need measurement.

The host authenticates senders against the active game roster and compatible chat sessions, tracks expiring explicit voice opt-ins, checks fresh consent tokens, rejects duplicate/out-of-order blocks, limits audio frames per sender and selects recipients using its own world positions. Audio is sent unreliably and is never retransmitted. Receive processing is bounded per update, datagrams older than 200 ms in the local Steam queue are discarded, playback sample queues are bounded to 200 ms, and up to 32 remote playback sources may exist. These are safeguards, not a measured end-to-end latency guarantee.

The host necessarily receives compressed speech before deciding where to relay it. Voice participants should trust their host. Communications does not write audio to disk, archive it, transcribe it, send it to an AI service, or contact an external voice server. This cannot prevent another participant from recording their own audio output. The mod uses the game's existing Steam networking session and closes only its own voice channel.

The cloud build has game reference assemblies, but no running Railroader, Steam login, microphone or output device. Automated boundary tests exercise the real voice logic with test doubles. Live hardware and two-PC verification remain required. No claim of Valve/Nexus endorsement or guaranteed moderation acceptance is made.

## Official policy references

Reviewed on 13 November 2026:

- [Steam Voice documentation](https://partner.steamgames.com/doc/features/voice): Valve documents voice capture and transmission using networking APIs. This is a technical use case, not a mod-specific permission grant.
- [Steam Subscriber Agreement](https://store.steampowered.com/subscriber_agreement/), especially section 2.G: general restrictions and applicable permissions remain relevant. Absence of a voice-specific ban is not a blanket licence.
- [Railroader FAQ](https://docs.railroadergame.com/faq/): the game identifies the Steam Subscriber Agreement as its EULA and restricts redistribution of game assets.
- [Railroader DLL mods](https://docs.railroadergame.com/modding/dll-mods/): native DLL loading and lifecycle are provided; access beyond the small modding contract is unsupported and may break with updates.
- [Nexus File Submission Guidelines](https://help.nexusmods.com/article/28-file-submission-guidelines), Inappropriate Content: internet communication must be crucial to a mod's functionality; Nexus reserves moderation rights. The guidelines ask authors of tools/apps depending on sending/receiving data to contact staff with an explanation and source code. Treat that as relevant to this network feature before a Nexus upload; the author handles publication and any contact. The guidelines also require accurate functionality descriptions and appropriate AI disclosure/tagging.
- [Nexus Terms of Service](https://help.nexusmods.com/article/18-terms-of-service): general site, content and moderation requirements continue to apply.

No external party has been contacted and no package has been uploaded as part of development.

## Deutsche Kurzanleitung

Auf beiden PCs denselben 0.6.8-Teststand installieren und Railroader neu starten. Im Multiplayer unter **Spieloptionen → Mods → EnusDE Communications** gibt der Host die Umgebungssprache frei. Jeder Teilnehmer aktiviert zusätzlich seine eigene Teilnahme, wählt ein Mikrofon und belegt eine freie Sprechtaste. Beide Freigaben stehen nach jedem Laden einer Welt wieder auf aus. Die Host-Freigabe allein schaltet sein eigenes Mikrofon und seinen Empfang nicht ein.

Zum Sprechen außerhalb von Menüs die Sprechtaste halten. Kopfhörer verwenden. Der Host bestimmt die Hörweite (Standard 40 Meter). Lautstärke, Mikrofonverstärkung und Stummschaltung einzelner Spieler sind persönlich einstellbar. Freitextchat und Spieler ohne Mod bleiben unabhängig davon nutzbar. Sprache wird nur vorübergehend im Arbeitsspeicher verarbeitet, über den Host weitergeleitet und nicht gespeichert. Kein Funk, keine Richtungsausgabe und keine Abschirmung durch Wände oder Fahrzeuge.

Zwei-PC-Test: beide Sprechrichtungen, Entfernung, gleichzeitiges Sprechen, Stummschaltung, persönliche Abschaltung, Host-Abschaltung, Menüs, Fokusverlust, fehlendes Mikrofon, Neuverbindung, Weltwechsel und Beitritt ohne Mod prüfen. Bei verlorener Host-Abschaltnachricht endet die Freigabe beim Teilnehmer spätestens nach dem kurzen Drei-Sekunden-Zeitfenster. Die Cloud-Prüfungen ersetzen keinen Hör- oder Spieltest.

Die Nexus-Richtlinien enthalten eine ausdrückliche Regel für notwendige Internetkommunikation und die Bitte um Kontakt mit Erklärung und Quellcode bei datenübertragenden Tools/Apps. Das ist für die spätere Veröffentlichung zu berücksichtigen. Eine garantierte Freigabe durch Valve, Giraffe Lab oder Nexus liegt nicht vor.
