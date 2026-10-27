# Extended Stats 1.0.0

Extended Stats saves item and block statistics in `BepInEx\config\ExtendedStats.dat`. Open the pause menu and select **Stats** to see a scrollable table with the total number of blocks broken and per-item counts for:

- **Broken**: world blocks broken, grouped under the item they drop when applicable.
- **Picked up**: item quantities added by collecting drops or picking up placed lamps.
- **Crafted**: output quantities completed by crafting recipes and assembly sites.
- **Placed**: assembly components placed and lamps set down.
- **Sold**: item quantities shipped in packed orders.

Use the rounded **Player** and **World** buttons at the top-left, fitted within the menu, to switch between actions performed by your local player and combined totals for all players. The buttons use the same shape as **Close**, with beige and brown coloring and 15-point labels. The viewer opens on **Player**. Crafting and assembly completion are attributed to the player who requested or started them; shipped orders are attributed to the player who started the shipment. The table includes registered item types even when all of their counts are zero and refreshes while open. Each action header shows its total count beneath the label. It starts sorted by **Broken**, highest first. Click any column header to sort by that column; click the active header again to reverse the order. The arrow shows the current direction. The item column sorts alphabetically. Use the **Close** button at the top-right or press **Escape** to close the viewer. Existing statistics are preserved as world totals; earlier data did not record player attribution, so it cannot be split into player totals retroactively.

## Requirements

- Windows x64 DIG Demo using Unity Mono.
- BepInEx 6 Unity Mono x64.

## Install

1. Close DIG.
2. Extract this package into the game root folder, next to `Dig.exe`.
3. Run `Install-ExtendedStats.bat` and approve the administrator prompt.
4. Launch DIG and open the pause menu.

To remove the mod, delete `BepInEx\plugins\ExtendedStats.dll`. The saved statistics file remains until you delete `BepInEx\config\ExtendedStats.dat`.

## Build from source

Requires the .NET SDK and the game's managed assemblies. Set `DIG_GAME_DIR` to your game installation path:

```powershell
$env:DIG_GAME_DIR = Read-Host "Path to your DIG installation"
dotnet build .\ExtendedStats.csproj -c Release
```
