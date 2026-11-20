# EnusDE Communications

[Deutsche Zusammenfassung](#deutsche-zusammenfassung)

EnusDE Communications adds in-game text chat and railroad operating messages to **Railroader 0.6.2**. It includes global, crew and train channels, two-way dispatcher conversations, reusable message templates, and an F10 shunting wheel. You can configure the chat HUD and message fading in the game's preferences.

**Development status** · 0.6.8 adds experimental, default-off proximity voice. It is an unpublished test build. Compilation and automated checks do not establish live audio quality or multiplayer reliability. The published 0.6.7 pre-release remains available.

## Install

1. Close Railroader and download `EnusDE-Communications-v0.6.7.zip` from [Releases](https://github.com/EnusDE/EnusDE-Communications/releases).
2. Extract the ZIP and copy the `EnusDE-Communications` folder into your Railroader `Mods` folder. The usual location is `Documents\Railroader\Mods`. Use your actual Railroader home directory if you have redirected Documents or configured a custom location.
3. Enable DLL mods in Railroader's experimental settings if needed. Restart the game and activate **EnusDE Communications**.
4. Load a world and open **Preferences → Mods → EnusDE Communications**. To use the chat in singleplayer, enable **Show chat in singleplayer**.

When updating, replace the existing mod files and keep the mod's `Data` folder and your Railroader `ModSettings` files. Keep one installed copy of the mod.

## Requirements

- Railroader **0.6.2** with native DLL mods enabled.
- Harmony **2.3.6**, included in the install ZIP.
- [EnusDE Translation](https://www.nexusmods.com/railroader/mods/1991) is optional and supplies the selected language. Communications includes its own English and German text and defaults to English. A separate mod menu is unnecessary.
- For this development build, the host and voice participants should use **Communications 0.6.8** (chat protocol 4; separate voice protocol 1). Players who do not install Communications can still join and use the game's normal console.

## Use the chat

- Use the speech-bubble icon to open chat input in multiplayer or toggle the singleplayer HUD. Select **Global**, **Crew** or **Train** to choose a channel.
- Click **+** to compose a template. Templates display in each recipient's supported language. Free text keeps its original wording.
- To send a shunting message, join a train channel, hold **F10**, point at the desired message, and release. Release in the center or outside the wheel to cancel.
- Use **Dispatcher** to open an ongoing conversation. The host can preview driver, dispatcher and administrator views using the local role test.

The host keeps a rolling six-hour log of chat and native multiplayer console messages. Cleanup runs while the game is open and resumes at the next startup. An optional 60-second performance capture saves a local CSV with timing data.

## Proximity voice (0.6.8 test build)

Click the microphone beside **Aa** in the chat to open **Proximity voice**. The host enables **Allow proximity voice for this session**. Each participant enables **Participate in proximity voice**, selects a microphone, and chooses **Push-to-talk** or **Open microphone**. Both permission switches reset to off on each world load. The icon is green during capture, amber when idle or off, and crossed out when unavailable. An open microphone pauses in menus, while entering text, and when the game loses focus. The host sets the hearing distance, which defaults to 40 metres. The same window has personal gain, volume and per-player mute controls.

Voice travels through the host on a separate Steam channel. Only opted-in participants in range receive it. Audio exists temporarily in memory and is not added to the chat archive or diagnostics. Voice is optional; unmodified players can still join. Headphones are recommended. This first implementation has distance fading, with no radio, directional panning or wall/vehicle occlusion. See [voice setup, limitations and two-PC tests](VOICE.md).

## Source and documentation

- [Build and test from source](BUILDING.md)
- [0.6.8 development release notes / Versionshinweise](RELEASE-NOTES-0.6.8.md)
- [Message templates and dispatcher chat (German)](MESSAGE-BUILDER.md)
- [Host role test (German)](ROLE-TEST.md)
- [Performance capture (German)](PERFORMANCE.md)

Created by EnusDE with AI assistance. The mod source is [MIT licensed](LICENSE). The [third-party notices](THIRD-PARTY-NOTICES.txt) cover the bundled Harmony library and its components.

## Deutsche Zusammenfassung

EnusDE Communications ergänzt **Railroader 0.6.2** um Global-, Mannschafts- und Zugchat, FDL-Gespräche, Nachrichtenbausteine und ein F10-Rangierrad. Das Installations-ZIP liegt unter [Releases](https://github.com/EnusDE/EnusDE-Communications/releases). Kopiere den enthaltenen Ordner `EnusDE-Communications` in deinen Railroader-Mods-Ordner und aktiviere die Mod. Die Einstellungen findest du unter **Spieloptionen → Mods → EnusDE Communications**. Harmony liegt bei. EnusDE Translation ist optional. **0.6.8 ist ein unveröffentlichter Entwicklungsstand.** Das Mikrofon neben **Aa** im Chat öffnet die Sprachoptionen. Dort wählen Teilnehmer zwischen Sprechtaste und offenem Mikrofon. Host und Teilnehmer geben die Umgebungssprache nach jedem Weltladen separat frei. [Anleitung und Zwei-PC-Test](VOICE.md)
