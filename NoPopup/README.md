# No Popup 1.0.0

No Popup is a BepInEx 6 Unity Mono plugin for the Windows x64 DIG Demo. It blocks Unity browser launches to Google Forms (`docs.google.com/forms/...` and `forms.gle`) while leaving unrelated URLs unchanged.

## Requirements

- Windows x64 DIG Demo using Unity Mono.
- BepInEx 6 Unity Mono x64.

## Install

1. Close DIG.
2. Extract this package into the game root folder, next to `Dig.exe`.
3. Run `Install-NoPopup.bat` and approve the administrator prompt.
4. Launch DIG. No Popup logs when it blocks a Google Forms link.

To remove the mod, delete `BepInEx\plugins\NoPopup.dll`.

## Build from source

Requires the .NET SDK. Set `DIG_GAME_DIR` to your game installation path:

```powershell
$env:DIG_GAME_DIR = Read-Host "Path to your DIG installation"
dotnet build .\NoPopup.csproj -c Release
```

The plugin DLL is written to `bin\Release\netstandard2.1\NoPopup.dll`.
