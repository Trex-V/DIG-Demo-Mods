# DIG Mods

A collection of source projects for BepInEx mods for **DIG**. Each mod is a
separate plugin and can be installed and used independently.

## Mods

| Mod | What it does |
| --- | --- |
| [Auto Tool](./DigAutoTool/README.md) | Automatically equips the required tool before mining or chopping. Trees use the axe; cliffs and ore nodes use the pickaxe; ordinary blocks use the tool specified by the block. Blueprint and gold-bag nodes are left unchanged. |
| [Extra Crafts](./DigFurnaceQueue/README.md) | Adds `x10` and `x25` batch-crafting buttons for furnace and saw recipes, alongside the game's existing `craft` and `x5` options. The original queue limit still applies. |
| [Mod Manager](./DigModManager/README.md) | Adds a main-menu mod list with plugin version and metadata. Offers best-effort live enable/disable for supported plugins; restarting the game may be required for a complete unload or reload. |
| [Extended Stats](./ExtendedStats/README.md) | Adds a pause-menu statistics viewer for blocks broken and item quantities broken, picked up, crafted, placed, and sold. Supports local-player and world totals, sorting, and saved statistics. |
| [No Popup](./NoPopup/README.md) | Blocks the game's browser links to Google Forms (`docs.google.com/forms/...` and `forms.gle`) without blocking unrelated URLs. |
| [Order Tracker](./OrderTracker/README.md) | Shows accepted orders and their crate progress in an on-screen panel, including item counts and ready-to-ship crates. The panel can be minimized, and its toggle and scroll bindings can be configured in Controls. |

## Compatibility

The projects target **.NET Standard 2.1** and are intended for the Windows x64
version of DIG running Unity Mono with **BepInEx 6 Unity Mono x64**. Consult
each mod's README for its requirements and tested loader version.

## Build from source

Building requires the .NET SDK and a local DIG installation. The game itself
is not included in this repository; its managed assemblies are referenced from
`Dig_Data\Managed`.

In PowerShell, set `DIG_GAME_DIR` to the folder containing `Dig.exe`, then
build the project you want:

```powershell
$env:DIG_GAME_DIR = Read-Host "Path to your DIG installation"
dotnet build .\DigAutoTool\DigAutoTool.csproj -c Release
dotnet build .\DigFurnaceQueue\DigFurnaceQueue.csproj -c Release
dotnet build .\DigModManager\DigModManager.csproj -c Release
dotnet build .\ExtendedStats\ExtendedStats.csproj -c Release
dotnet build .\NoPopup\NoPopup.csproj -c Release
dotnet build .\OrderTracker\OrderTracker.csproj -c Release
```

Run only the build command for the mod you want. Each plugin DLL is written to
that project's `bin\Release\netstandard2.1` directory. Generated `bin`, `obj`,
and release-package directories are excluded by `.gitignore`.

## Install

Follow the individual mod README linked above. Installation differs by mod:
some include a Windows installer script, while others are installed by copying
their DLL (and, where applicable, their `.ABOUT.md` sidecar) into
`BepInEx\plugins` in the game folder. Close the game before installing or
removing plugins.

## Repository notes

- Game assemblies and BepInEx runtime files are not distributed here; obtain
  them through the game and BepInEx's official channels.
- Mod-specific installation, removal, configuration, and feature details are
  documented in each project's README.
- This repository is licensed under the MIT License; see [LICENSE](./LICENSE).
