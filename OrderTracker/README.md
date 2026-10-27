# Order Tracker 1.0.0

Order Tracker is a BepInEx 6 Unity Mono plugin for the Windows x64 DIG Demo. It shows every accepted order and its dock crate in a compact panel on the right side of the screen. The expanded tracker is 20% narrower and 20% taller than the original 430-by-460 UI size, with the top edge held in place. The top bar stays anchored in the same position when the tracker is expanded or minimized. The tracker body has no large backdrop, while its order cards remain readable. The tracker stays behind game menus and the crate checklist.

The tracker updates as items are deposited. Item rows include the game's item icon, order progress, the backpack item count in parentheses, and the chest count below the item name. Fully supplied item lines disappear. Multiple active crates are shown independently in a scrollable list, and a completed crate stays visible as “Ready to ship” until it is shipped. Unfinished crates appear first, with ready-to-ship crates grouped at the bottom. The scrollbar appears only when the list is taller than its visible area; scroll with it or the configurable mouse-button bindings. The tracker renders above the crate checklist and below open game menus, and both its body and minimize-button layer are hidden while a crate window is open so the crate window remains unobstructed. Its transparent scroll viewport uses rectangular clipping so order cards remain visible without requiring a visible backdrop. The header opacity matches the order cards when expanded and is half that opacity when minimized. Use the transparent `-` button in the header to collapse the panel; the `+` button restores it. The button remains clickable above open menus.

When a crate becomes ready to ship, the tracker scrolls down just enough to reveal the next unfinished crate if it is hidden below the list.

Press `T` to toggle the tracker by default. Change its binding in **Settings > Controls > Order Tracker toggle**. **Order Tracker scroll up** defaults to mouse button 5; **Order Tracker scroll down** defaults to mouse button 4. Change either binding in **Settings > Controls**. The tracker toggle prompt updates to show its assigned key, and all selected bindings are saved as key paths so they remain stable between game launches. Tracker input actions are enabled on the first Unity update so the defaults are active when the game starts.

## Requirements

- Windows x64 DIG Demo using Unity Mono.
- BepInEx 6 Unity Mono x64. The tested build is [BepInEx 6.0.0-be.791](https://builds.bepinex.dev/projects/bepinex_be/791/BepInEx-Unity.Mono-win-x64-6.0.0-be.791%2Bb782da4.zip).

## Install

1. Close the game.
2. Extract this package into the game root folder, next to `Dig.exe`.
3. Run `Install-OrderTracker.bat` and approve the administrator prompt.
4. Launch the game and accept an order. The tracker appears while the game session is active.

The installer downloads and SHA-256 verifies the official loader if BepInEx is not already installed. It will not overwrite a partial or unfamiliar loader installation.

To remove Order Tracker, delete `BepInEx\plugins\OrderTracker.dll`. This does not remove BepInEx.

## Build from source

Requires the .NET SDK and the game's managed assemblies. Set `DIG_GAME_DIR` to your game installation path:

```powershell
$env:DIG_GAME_DIR = Read-Host "Path to your DIG installation"
dotnet build .\OrderTracker.csproj -c Release
```

The plugin DLL is written to `bin\Release\netstandard2.1\OrderTracker.dll`.
