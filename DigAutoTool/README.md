# Auto Tool 1.0.0

A BepInEx 6 Unity Mono plugin for DIG that selects the required tool immediately before a world-block, cliff-mining, or tree-chopping request. Trees select the axe, cliffs and ore nodes select the pickaxe, and ordinary blocks use their required tool. The selected tool remains equipped afterward.

See [ABOUT.md](./ABOUT.md) for version, tested game build, author, and live-toggle support. The installer also installs `DigAutoTool.ABOUT.md` beside the plugin DLL so Mod Manager can display these details.

For ordinary blocks, the plugin reads the required tool from the game's block definition. Ore nodes and cliffs select the pickaxe. Blueprint and gold-bag nodes are left alone.

## Quick install

1. Close DIG.
2. Extract the mod ZIP into the game root folder (the folder containing `Dig.exe`).
3. Double-click `Install-DigAutoTool.bat` and approve the administrator prompt.
4. Launch DIG. The installer downloads and verifies the official BepInEx 6 Unity Mono x64 loader if it is not already installed, then confirms the plugin is present.

The installer requires Windows PowerShell and an internet connection if BepInEx is not already installed. It verifies the downloaded loader's SHA-256 checksum and will not overwrite a partial or unfamiliar loader setup.

The tested loader is [BepInEx 6.0.0-be.791 for Windows x64](https://builds.bepinex.dev/projects/bepinex_be/791/BepInEx-Unity.Mono-win-x64-6.0.0-be.791%2Bb782da4.zip). See the [official Mono Unity installation guide](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_mono.html).

To remove the mod, delete `BepInEx\plugins\DigAutoTool.dll`. This does not remove BepInEx.

## Build from source

Requires the .NET SDK and a copy of the game's managed assemblies. The project references `Dig_Data\Managed\DigDeliver.Runtime.dll`; set `DIG_GAME_DIR` to your game installation path:

```powershell
$env:DIG_GAME_DIR = Read-Host "Path to your DIG installation"
dotnet build .\DigAutoTool.csproj -c Release
```

The plugin DLL is written to `bin\Release\netstandard2.1\DigAutoTool.dll`.
