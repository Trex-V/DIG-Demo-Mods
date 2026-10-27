param(
    [switch]$Elevated
)

$ErrorActionPreference = 'Stop'

function Pause-Installer {
    Write-Host ''
    Read-Host 'Press Enter to close this window' | Out-Null
}

if (-not $Elevated) {
    try {
        $powerShellPath = (Get-Process -Id $PID).Path
        $arguments = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -Elevated' -f $PSCommandPath
        $process = Start-Process -FilePath $powerShellPath -ArgumentList $arguments -Verb RunAs -Wait -PassThru
        exit $process.ExitCode
    }
    catch {
        Write-Host "Could not start the installer with administrator privileges: $($_.Exception.Message)" -ForegroundColor Red
        Pause-Installer
        exit 1
    }
}

try {
    $gameDir = Split-Path -Parent $PSCommandPath
    if (-not (Test-Path -LiteralPath (Join-Path $gameDir 'Dig.exe') -PathType Leaf)) {
        throw 'Dig.exe was not found beside the installer. Extract this package into the game root folder.'
    }
    if (Get-Process -Name Dig -ErrorAction SilentlyContinue) {
        throw 'DIG is running. Close the game and run the installer again.'
    }

    $loaderPath = Join-Path $gameDir 'BepInEx\core\BepInEx.Unity.Mono.dll'
    if (-not (Test-Path -LiteralPath $loaderPath -PathType Leaf)) {
        throw 'BepInEx 6 Unity Mono was not found. Install the loader before installing Extended Stats.'
    }

    $pluginSource = Join-Path $gameDir 'BepInEx\plugins\ExtendedStats.dll'
    if (-not (Test-Path -LiteralPath $pluginSource -PathType Leaf)) {
        throw 'BepInEx\plugins\ExtendedStats.dll is missing. Re-extract the full package into the game root folder.'
    }

    Write-Host ''
    Write-Host 'Extended Stats 1.0.0 is installed.' -ForegroundColor Green
    Write-Host 'Launch DIG and open the pause menu to view item statistics.'
}
catch {
    Write-Host ''
    Write-Host "Installation failed: $($_.Exception.Message)" -ForegroundColor Red
    Pause-Installer
    exit 1
}

Pause-Installer
exit 0
