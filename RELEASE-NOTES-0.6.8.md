# 0.6.8 — unpublished test build

## English

Adds optional proximity voice through the multiplayer host. Host permission and personal participation start off on every world load. A microphone icon beside the chat shows capture status and opens a dedicated game window. It offers push-to-talk and optional open microphone, device selection, playback volume, microphone gain, host hearing distance and per-player mute controls.

Voice uses a separate Steam channel and protocol, independently decodable compressed audio blocks, expiring opt-ins, bounded queues and distance filtering. It does not record audio to disk. Railroader 0.6.2 remains required; chat protocol 4 and optional mod participation remain unchanged. This first voice implementation does not include radio, directional panning or wall/vehicle occlusion.

Compiled against the supplied Railroader 0.6.2 references and checked with automated regression and voice boundary tests. Actual microphone/audio quality, visual UI, Steam connectivity, multiplayer behavior and performance comparison still need in-game testing. Not published or endorsed by Valve, Giraffe Lab or Nexus Mods. See [VOICE.md](VOICE.md) for setup, test steps and policy references.

## Deutsch

Ergänzt optionale Umgebungssprache über den Multiplayer-Host. Host-Freigabe und persönliche Teilnahme sind nach jedem Laden einer Welt aus. Ein Mikrofon-Symbol am Chat zeigt den Aufnahmestatus und öffnet ein eigenes Fenster. Dort stehen Sprechtaste, offenes Mikrofon, Gerätewahl, Sprachlautstärke, Mikrofonverstärkung, Host-Hörweite und individuelle Stummschaltung zur Verfügung.

Sprache verwendet einen getrennten Steam-Kanal mit eigenem Protokoll, unabhängig dekodierbare komprimierte Audioblöcke, zeitlich begrenzte Freigaben, begrenzte Puffer und Entfernungsfilter. Audio wird nicht auf Datenträger gespeichert. Ziel bleibt Railroader 0.6.2; Textprotokoll 4 und freiwillige Mod-Teilnahme bleiben erhalten. Funk, Richtungsausgabe und Abschirmung durch Wände/Fahrzeuge sind nicht enthalten.

Gegen die bereitgestellten Railroader-0.6.2-Referenzen kompiliert und automatisiert geprüft. Echte Mikrofon-/Tonqualität, sichtbare Bedienung, Steam-Verbindungen, Multiplayer und Leistungsvergleich müssen noch im Spiel geprüft werden. Nicht veröffentlicht und keine Freigabe durch Valve, Giraffe Lab oder Nexus Mods. Einrichtung, Testablauf und Richtlinienquellen: [VOICE.md](VOICE.md).
