# Local and Cloud development

GitHub is the shared source for the local **Railroader - Chat** chat and **Railroader - Chat (Cloud)**. Keep work on a GitHub branch so it can be continued on either computer. A chat's uncommitted files become available elsewhere after they are committed and pushed.

## Build on your Windows PC

1. Open this repository in GitHub Desktop. Click **Fetch origin**, select the branch with the changes you want to test, and click **Pull origin** when available.
2. Select **Repository → Show in Explorer**.
3. Double-click **Build-Local.cmd**. It finds Railroader through Steam or asks you to select the folder containing `Railroader.exe`.
4. The script builds against that installation's DLLs and runs the automated checks. On success, Explorer selects the install ZIP under `build/`.
5. Close Railroader and extract the ZIP into your Railroader `Mods` directory. Keep the existing `Data` directory and your `ModSettings` files. Test the update in-game.

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) once. The selected game directory is remembered in the ignored `.local/build.json` file. Compilation, game references and test outputs stay on your PC. The build checks this source version against Railroader 0.6.3. Following a game update, the local chat can inspect the new DLLs and adapt the source if the APIs or build identity changed.

## Continue in Cloud

Before switching to Cloud, commit and push your changes in GitHub Desktop. Tell the Cloud chat which repository branch to continue. Have the Cloud chat commit and push its source changes when you want to pick them up on your PC. Back at your PC, fetch the branch in GitHub Desktop and run **Build-Local.cmd**.

The branch `source/communications-0.6.8-rr0.6.3` preserves the source that produced the tested 0.6.8 DLL. `work/communications-voice-improvements` preserves the subsequent audio and microphone-window work. The targeted translation-discovery fix and local build helper are on `fix/optional-language-lookup`. Keep the voice work separate until it is integrated with the current Railroader target and tested on two PCs.

## Deutsch

Beide Chats verwenden die Quelltexte auf GitHub. Vor dem Wechsel Änderungen committen und pushen. Auf dem PC in GitHub Desktop **Fetch origin**, den passenden Branch und gegebenenfalls **Pull origin** wählen. Dann **Build-Local.cmd** doppelt anklicken. Das geprüfte Installations-ZIP erscheint im Explorer. Die Spiel-DLLs und der gemerkte Installationspfad bleiben lokal. Nach einem Spielupdate kann der lokale Chat die neuen DLLs direkt prüfen und erforderliche Quellcodeanpassungen vornehmen.
