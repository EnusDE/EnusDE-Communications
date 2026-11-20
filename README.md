# EnusDE Communications

[Deutsche Zusammenfassung](#deutsche-zusammenfassung)

EnusDE Communications adds in-game text chat and railroad operating messages to **Railroader 0.6.3**. It includes global, crew and train channels, two-way dispatcher conversations, reusable message templates, and an F10 shunting wheel. You can configure the chat HUD and message fading in the game's preferences.

**Development status** · 0.6.11 is a test update for HUD visibility and chat positioning, based on 0.6.10. It retains cached optional translator discovery and experimental, default-off proximity voice. Live verification of the positioning changes and the earlier stutter fix is pending.

## Install

1. Close Railroader and use the supplied `EnusDE-Communications-v0.6.11.zip` test package for Railroader 0.6.3.
2. Extract the ZIP and copy the `EnusDE-Communications` folder into your Railroader `Mods` folder. The usual location is `Documents\Railroader\Mods`. Use your actual Railroader home directory if you have redirected Documents or configured a custom location.
3. Enable DLL mods in Railroader's experimental settings if needed. Restart the game and activate **EnusDE Communications**.
4. Load a world and open **Preferences → Mods → EnusDE Communications**. To use the chat in singleplayer, enable **Show chat in singleplayer**.

When updating, replace the existing mod files and keep the mod's `Data` folder and your Railroader `ModSettings` files. Keep one installed copy of the mod.

## Requirements

- Railroader **0.6.3** with native DLL mods enabled.
- Harmony **2.3.6**, included in the install ZIP.
- [EnusDE Translation](https://www.nexusmods.com/railroader/mods/1991) is optional and supplies the selected language. Communications includes its own English and German text and defaults to English. A separate mod menu is unnecessary.
- For this development build, the host and voice participants should use **Communications 0.6.11** (chat protocol 4; separate voice protocol 1). Players who do not install Communications can still join and use the game's normal console.

## Use the chat

- Use the speech-bubble icon to open chat input in multiplayer or toggle the singleplayer HUD. Select **Global**, **Crew** or **Train** to choose a channel.
- Click **+** to compose a template. Templates display in each recipient's supported language. Free text keeps its original wording.
- To send a shunting message, join a train channel, hold **F10**, point at the desired message, and release. Release in the center or outside the wheel to cancel.
- Use **Dispatcher** to open an ongoing conversation. The host can preview driver, dispatcher and administrator views using the local role test.

The host keeps a rolling six-hour log of chat and native multiplayer console messages. Cleanup runs while the game is open and resumes at the next startup. An optional 60-second performance capture saves a local CSV with timing data.

### Position the chat (0.6.11)

Open **Preferences → Mods → EnusDE Communications**. Automatic docking places the chat flush with the left screen edge above the locomotive controls. Enable **Dock on the right instead of the left** to use the right edge instead. To place it freely, enable **Unlock chat position**, then drag the small **::** handle beside the chat's **+** button. Disable the unlock setting to lock the saved position. Reset window positions to restore left automatic docking.

Hiding the game's HUD in photo mode also hides the chat, its toolbar icon and communication windows. They return when the HUD is shown again.

## Proximity voice (0.6.8 test build)

Open **Preferences → Mods → EnusDE Communications**. The host enables **Allow proximity voice for this session**. Each participating player, including the host if they want to speak or listen, separately enables **Participate in proximity voice**, selects a microphone and assigns a push-to-talk key. Both permission switches reset to off on each world load. Hold the key outside menus to speak. The host can relay while personally opted out. Hearing distance defaults to 40 metres and is controlled by the host. Personal voice volume, microphone gain and per-player mute controls are available there too.

Voice travels through the host on a separate Steam channel. Only opted-in participants in range receive it. Audio exists temporarily in memory and is not added to the chat archive or diagnostics. Voice is optional; unmodified players can still join. Headphones are recommended. This first implementation has distance fading, with no radio, directional panning or wall/vehicle occlusion. See [voice setup, limitations and two-PC tests](VOICE.md).

## Source and documentation

- [Build and test from source](BUILDING.md)
- [Local and Cloud workflow](WORKFLOW.md)
- [0.6.11 test release notes / Versionshinweise](RELEASE-NOTES-0.6.11.md)
- [Message templates and dispatcher chat (German)](MESSAGE-BUILDER.md)
- [Host role test (German)](ROLE-TEST.md)
- [Performance capture (German)](PERFORMANCE.md)

Created by EnusDE with AI assistance. The mod source is [MIT licensed](LICENSE). The [third-party notices](THIRD-PARTY-NOTICES.txt) cover the bundled Harmony library and its components.

## Deutsche Zusammenfassung

EnusDE Communications ergänzt **Railroader 0.6.3** um Global-, Mannschafts- und Zugchat, FDL-Gespräche, Nachrichtenbausteine und ein F10-Rangierrad. Kopiere den Ordner `EnusDE-Communications` aus dem Testpaket `EnusDE-Communications-v0.6.11.zip` in deinen Railroader-Mods-Ordner und aktiviere die Mod. Die Einstellungen findest du unter **Spieloptionen → Mods → EnusDE Communications**. Harmony liegt bei. EnusDE Translation ist optional. **Seit 0.6.11 lässt sich der Chat links oder rechts andocken sowie über einen entsperrbaren Griff frei verschieben. Beim Ausblenden des Spiel-HUDs verschwindet auch die Chatoberfläche.** Die neuen Positionen und der Leistungseffekt der Übersetzerkorrektur aus 0.6.10 bleiben im Spiel zu prüfen. Die Umgebungssprache startet nach jedem Weltladen ausgeschaltet. Für künftige lokale Builds **Build-Local.cmd** im Quellordner doppelt anklicken. Die gemeinsame Arbeit mit dem Cloud-Chat ist in [WORKFLOW.md](WORKFLOW.md) erklärt.
