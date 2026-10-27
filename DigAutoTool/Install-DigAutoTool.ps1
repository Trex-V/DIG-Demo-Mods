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
    $gameExe = Join-Path $gameDir 'Dig.exe'
    $pluginSource = Join-Path $gameDir 'BepInEx\plugins\DigAutoTool.dll'
    $aboutSource = Join-Path $gameDir 'BepInEx\plugins\DigAutoTool.ABOUT.md'

    if (-not (Test-Path -LiteralPath $gameExe -PathType Leaf)) {
        throw 'Dig.exe was not found beside the installer. Extract the mod package into the game root folder.'
    }

    if (-not (Test-Path -LiteralPath $pluginSource -PathType Leaf)) {
        throw 'BepInEx\plugins\DigAutoTool.dll is missing. Re-extract the complete mod package into the game root folder.'
    }

    if (-not (Test-Path -LiteralPath $aboutSource -PathType Leaf)) {
        throw 'BepInEx\plugins\DigAutoTool.ABOUT.md is missing. Re-extract the complete mod package into the game root folder.'
    }

    if (Get-Process -Name Dig -ErrorAction SilentlyContinue) {
        throw 'DIG is running. Close the game and run the installer again.'
    }

    $loaderMarkers = @(
        (Join-Path $gameDir 'winhttp.dll'),
        (Join-Path $gameDir '.doorstop_version'),
        (Join-Path $gameDir 'doorstop_config.ini'),
        (Join-Path $gameDir 'BepInEx\core\BepInEx.Unity.Mono.dll')
    )
    $presentMarkers = @($loaderMarkers | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
    $hasLoader = $presentMarkers.Count -eq $loaderMarkers.Count

    if ($hasLoader) {
        Write-Host 'BepInEx Unity Mono loader is already installed; leaving it unchanged.'
    }
    else {
        if ($presentMarkers.Count -gt 0) {
            throw 'A partial or different Doorstop/BepInEx installation was found. The installer will not overwrite it. Resolve that installation first, then rerun.'
        }

        $url = 'https://builds.bepinex.dev/projects/bepinex_be/791/BepInEx-Unity.Mono-win-x64-6.0.0-be.791%2Bb782da4.zip'
        $expectedHash = '5DCE6C73F760933957F477839A6E28E5D359F196A5CEF4A5C90F5A7A73FCE82C'
        $tempRoot = Join-Path $env:TEMP ('DigAutoTool-Setup-' + [Guid]::NewGuid().ToString('N'))
        $archive = Join-Path $tempRoot 'BepInEx.zip'
        $staging = Join-Path $tempRoot 'Extracted'

        try {
            New-Item -ItemType Directory -Path $staging -Force | Out-Null
            Write-Host 'Downloading the official BepInEx 6 Unity Mono x64 loader...'
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing

            $actualHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
            if ($actualHash -ne $expectedHash) {
                throw "BepInEx download checksum mismatch. Expected $expectedHash but got $actualHash."
            }

            Expand-Archive -LiteralPath $archive -DestinationPath $staging -Force

            $stagedCore = Join-Path $staging 'BepInEx\core\BepInEx.Unity.Mono.dll'
            $stagedDoorstop = Join-Path $staging 'winhttp.dll'
            if (-not (Test-Path -LiteralPath $stagedCore -PathType Leaf) -or
                -not (Test-Path -LiteralPath $stagedDoorstop -PathType Leaf)) {
                throw 'The downloaded archive did not contain the expected BepInEx Unity Mono loader files.'
            }

            $stagedBepInEx = Join-Path $staging 'BepInEx'
            $gameBepInEx = Join-Path $gameDir 'BepInEx'
            New-Item -ItemType Directory -Path $gameBepInEx -Force | Out-Null
            Copy-Item -Path (Join-Path $stagedBepInEx '*') -Destination $gameBepInEx -Recurse -Force

            foreach ($name in @('winhttp.dll', '.doorstop_version', 'doorstop_config.ini')) {
                $source = Join-Path $staging $name
                if (Test-Path -LiteralPath $source -PathType Leaf) {
                    Copy-Item -LiteralPath $source -Destination (Join-Path $gameDir $name) -Force
                }
            }
        }
        finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force
            }
        }
    }

    $installedPlugin = Join-Path $gameDir 'BepInEx\plugins\DigAutoTool.dll'
    if (-not (Test-Path -LiteralPath $installedPlugin -PathType Leaf)) {
        throw 'The DigAutoTool plugin DLL is missing from the game BepInEx plugins folder.'
    }

    $installedAbout = Join-Path $gameDir 'BepInEx\plugins\DigAutoTool.ABOUT.md'
    Copy-Item -LiteralPath $aboutSource -Destination $installedAbout -Force
    if (-not (Test-Path -LiteralPath $installedAbout -PathType Leaf)) {
        throw 'The DigAutoTool mod information file could not be installed.'
    }

    Write-Host ''
    Write-Host 'Auto Tool 1.0.0 is installed.' -ForegroundColor Green
    Write-Host 'Launch DIG to load it. Check BepInEx\LogOutput.txt if you need to verify startup.'
}
catch {
    Write-Host ''
    Write-Host "Installation failed: $($_.Exception.Message)" -ForegroundColor Red
    Pause-Installer
    exit 1
}

Pause-Installer
exit 0
