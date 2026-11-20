# 0.6.8 — unpublished test build

## English

This source build targets Railroader 0.6.3. The port uses `Game.HomeDirectory.UserPaths` and updates the exact game module check, manifest dependency and metadata tests for that build. It does not support the previous 0.6.2 game build.

Adds optional proximity voice through the multiplayer host. Host permission and personal participation start off on every world load. Native preferences provide microphone selection, an initially unbound push-to-talk key, playback volume, microphone gain, host hearing distance and per-player mute controls. The host can relay while personally opted out. An on-screen status indicates readiness and microphone activity.

Voice uses a separate Steam channel and protocol, independently decodable compressed audio blocks, expiring opt-ins, bounded queues and distance filtering. It does not record audio to disk. Railroader 0.6.3 remains required; chat protocol 4 and optional mod participation remain unchanged. This first voice implementation does not include radio, directional panning or wall/vehicle occlusion.

Compiled against the supplied Railroader 0.6.3 references and checked with automated regression and voice boundary tests. Actual microphone/audio quality, visual UI, Steam connectivity, multiplayer behavior and performance comparison still need in-game testing. Not published or endorsed by Valve, Giraffe Lab or Nexus Mods. See [VOICE.md](VOICE.md) for setup, test steps and policy references.

## Deutsch

Dieser Quellstand ist auf Railroader 0.6.3 angepasst. Die Portierung verwendet `Game.HomeDirectory.UserPaths` und aktualisiert die genaue Spielmodulprüfung, die Manifest-Abhängigkeit und die Metadatentests. Der vorherige Spielstand 0.6.2 wird damit nicht unterstützt.

Ergänzt optionale Umgebungssprache über den Multiplayer-Host. Host-Freigabe und persönliche Teilnahme sind nach jedem Laden einer Welt aus. Die nativen Optionen bieten Mikrofonwahl, eine zunächst unbelegte Sprechtaste, Sprachlautstärke, Mikrofonverstärkung, Host-Hörweite und individuelle Stummschaltung. Der Host kann weiterleiten, ohne selbst teilzunehmen. Eine Statuszeile zeigt Bereitschaft und Mikrofonaktivität.

Sprache verwendet einen getrennten Steam-Kanal mit eigenem Protokoll, unabhängig dekodierbare komprimierte Audioblöcke, zeitlich begrenzte Freigaben, begrenzte Puffer und Entfernungsfilter. Audio wird nicht auf Datenträger gespeichert. Ziel bleibt Railroader 0.6.3; Textprotokoll 4 und freiwillige Mod-Teilnahme bleiben erhalten. Funk, Richtungsausgabe und Abschirmung durch Wände/Fahrzeuge sind nicht enthalten.

Gegen die bereitgestellten Railroader-0.6.3-Referenzen kompiliert und automatisiert geprüft. Echte Mikrofon-/Tonqualität, sichtbare Bedienung, Steam-Verbindungen, Multiplayer und Leistungsvergleich müssen noch im Spiel geprüft werden. Nicht veröffentlicht und keine Freigabe durch Valve, Giraffe Lab oder Nexus Mods. Einrichtung, Testablauf und Richtlinienquellen: [VOICE.md](VOICE.md).
