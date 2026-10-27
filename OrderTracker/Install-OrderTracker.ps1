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
        throw 'Dig.exe was not found beside the installer. Extract the package into the game root folder.'
    }
    if (Get-Process -Name Dig -ErrorAction SilentlyContinue) {
        throw 'DIG is running. Close the game and run the installer again.'
    }

    $pluginSource = Join-Path $gameDir 'BepInEx\plugins\OrderTracker.dll'
    if (-not (Test-Path -LiteralPath $pluginSource -PathType Leaf)) {
        throw 'BepInEx\plugins\OrderTracker.dll is missing. Re-extract the complete package into the game root folder.'
    }

    $loaderMarkers = @(
        (Join-Path $gameDir 'winhttp.dll'),
        (Join-Path $gameDir '.doorstop_version'),
        (Join-Path $gameDir 'doorstop_config.ini'),
        (Join-Path $gameDir 'BepInEx\core\BepInEx.Unity.Mono.dll')
    )
    $presentMarkers = @($loaderMarkers | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
    if ($presentMarkers.Count -eq $loaderMarkers.Count) {
        Write-Host 'BepInEx Unity Mono loader is already installed; leaving it unchanged.'
    }
    elseif ($presentMarkers.Count -gt 0) {
        throw 'A partial or different Doorstop/BepInEx installation was found. The installer will not overwrite it.'
    }
    else {
        $url = 'https://builds.bepinex.dev/projects/bepinex_be/791/BepInEx-Unity.Mono-win-x64-6.0.0-be.791%2Bb782da4.zip'
        $expectedHash = '5DCE6C73F760933957F477839A6E28E5D359F196A5CEF4A5C90F5A7A73FCE82C'
        $tempRoot = Join-Path $env:TEMP ('OrderTracker-Setup-' + [Guid]::NewGuid().ToString('N'))
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
            if (-not (Test-Path -LiteralPath (Join-Path $staging 'BepInEx\core\BepInEx.Unity.Mono.dll') -PathType Leaf) -or
                -not (Test-Path -LiteralPath (Join-Path $staging 'winhttp.dll') -PathType Leaf)) {
                throw 'The downloaded archive did not contain the expected BepInEx Unity Mono loader files.'
            }

            $gameBepInEx = Join-Path $gameDir 'BepInEx'
            New-Item -ItemType Directory -Path $gameBepInEx -Force | Out-Null
            Copy-Item -Path (Join-Path $staging 'BepInEx\*') -Destination $gameBepInEx -Recurse -Force
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

    Write-Host ''
    Write-Host 'Order Tracker is installed.' -ForegroundColor Green
    Write-Host 'Launch DIG and accept an order to show the tracker.'
    Write-Host 'Check BepInEx\LogOutput.txt if you need to verify startup.'
}
catch {
    Write-Host ''
    Write-Host "Installation failed: $($_.Exception.Message)" -ForegroundColor Red
    Pause-Installer
    exit 1
}

Pause-Installer
exit 0
