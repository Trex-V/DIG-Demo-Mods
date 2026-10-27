# Extra Crafts 1.0.0

A BepInEx 6 Unity Mono plugin for DIG that adds `x10` and `x25` batch-crafting buttons for furnace and saw recipes. The existing `craft` and `x5` buttons remain available.

See [ABOUT.md](./ABOUT.md) for version, tested game build, author, and live-toggle support. Keep `DigFurnaceQueue.ABOUT.md` beside the plugin DLL so Mod Manager can display these details.

The game's original six-job queue limit is unchanged. The `x5`, `x10`, and `x25` buttons share a compact row beneath the full-width `craft` button, with small gaps between buttons. Each batch button queues one job for the selected number of crafts, and is disabled when the recipe is locked, the queue is full, or the required materials are unavailable.

## Install

1. Close DIG.
2. Extract `release\Extra Crafts 1.0.0.zip` into the DIG game folder, or copy the built `DigFurnaceQueue.dll` and `DigFurnaceQueue.ABOUT.md` into `BepInEx\plugins`.
3. Launch DIG and check `BepInEx\LogOutput.txt` for the plugin startup message.

BepInEx 6 Unity Mono x64 must already be installed. Batch crafting changes the number of crafts in a single queue job, not the queue capacity.

To remove the mod, delete `BepInEx\plugins\DigFurnaceQueue.dll`.

## Build from source

Requires the .NET SDK and the game's managed assemblies. Set `DIG_GAME_DIR` to your game installation path:

```powershell
$env:DIG_GAME_DIR = Read-Host "Path to your DIG installation"
dotnet build .\DigFurnaceQueue.csproj -c Release
```

The plugin DLL is written to `bin\Release\netstandard2.1\DigFurnaceQueue.dll`.
