# Build and test EnusDE Communications

You need PowerShell 7, the .NET 8 SDK, and the managed reference assemblies from Railroader **0.6.3**. On Windows, use the game directory that contains `Railroader.exe` and `Railroader_Data`. In Steam, you can find it through the game's **Manage → Browse local files** menu. Compilation and automated tests also run on Linux when the supplied game directory contains `Railroader_Data/Managed`; running the game itself requires the full installation.

The build uses reference assemblies from that installation and the vendored Harmony 2.3.6 library in `dependencies/Harmony`. See [third-party notices](THIRD-PARTY-NOTICES.txt).

Run these commands from the repository root, substituting your own game directory:

```powershell
$game = 'C:\path\to\Railroader'
.\Build.ps1 -GameDirectory $game -Package
.\Test.ps1 -GameDirectory $game
```

The build writes the mod DLL to `build/`. `-Package` adds the install and source ZIPs. Build before running the tests so the suite can inspect the compiled DLL. The regular test runner checks message and permissions rules, localization, UI and input logic, and Steam transport behavior using test doubles. Test outputs stay under `build/`.

For a separate native smoke test on Windows, close Railroader, keep Steam signed in, and run `.\Test-Native.ps1 -GameDirectory $game`. It starts the game with a temporary profile under `build/`. Check its result in `Player.log` in the generated `native-smoke-*` directory. Manual visual checks, microphone/audio checks and a two-player multiplayer session are needed to verify the live UI, voice and networking.
