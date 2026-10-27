# Mod Manager 1.0.0

A BepInEx 6 Unity Mono plugin for DIG. It adds a stationary **Mod Manager** button below **Add to Wishlist** on the main menu and lists the BepInEx plugins loaded for the current game session. The window uses DIG's UI theme and displays version, last-tested game build, author, and live-toggle support from each plugin's `.ABOUT.md` sidecar.

See [ABOUT.md](./ABOUT.md) for version, tested game build, author, and live-toggle support.

## Live toggles

The manager offers best-effort live enable/disable for loaded Unity plugins. Disabling a plugin disables its Unity component and removes Harmony patches owned by that plugin GUID. Enabling reactivates the component and reapplies Harmony patches in its assembly.

BepInEx does not provide a universal plugin unload/reload API. Mods with custom patch owners, background threads, event subscriptions, or other state outside their Unity component and Harmony patches may not fully stop or resume until the game restarts. The manager itself is always enabled. Plugins that are not Unity plugin components are listed but cannot be toggled live.

## Install

1. Close DIG.
2. Copy `DigModManager.dll` and `DigModManager.ABOUT.md` into `BepInEx\plugins` in the DIG game folder.
3. Launch DIG. Choose **Mod Manager** from the main menu.

Requires BepInEx 6 Unity Mono x64. Remove the mod by deleting `BepInEx\plugins\DigModManager.dll`.

## Build from source

Requires the .NET SDK and the game's managed assemblies. Set `DIG_GAME_DIR` to your game installation path:

```powershell
$env:DIG_GAME_DIR = Read-Host "Path to your DIG installation"
dotnet build .\DigModManager.csproj -c Release
```

The plugin DLL is written to `bin\Release\netstandard2.1\DigModManager.dll`.
