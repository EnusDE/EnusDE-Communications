param(
    [string]$GameDirectory,
    [switch]$NoOpen
)

$ErrorActionPreference = 'Stop'
$configPath = Join-Path $PSScriptRoot '.local\build.json'

function Test-GameDirectory([string]$Directory) {
    if ([string]::IsNullOrWhiteSpace($Directory)) { return $false }
    return (Test-Path -LiteralPath (Join-Path $Directory 'Railroader.exe') -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $Directory 'Railroader_Data\Managed\Railroader.dll') -PathType Leaf)
}

function Find-SteamGame {
    $steamPaths = @(
        (Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
        (Join-Path ${env:ProgramFiles(x86)} 'Steam')
    ) | Where-Object { $_ } | Select-Object -Unique
    $libraries = @($steamPaths)
    foreach ($steamPath in $steamPaths) {
        $libraryFile = Join-Path $steamPath 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $libraryFile -PathType Leaf) {
            $text = Get-Content -LiteralPath $libraryFile -Raw
            foreach ($match in [regex]::Matches($text, '"path"\s+"([^"]+)"')) {
                $libraries += $match.Groups[1].Value.Replace('\\', '\')
            }
        }
    }
    foreach ($library in ($libraries | Select-Object -Unique)) {
        $candidate = Join-Path $library 'steamapps\common\Railroader'
        if (Test-GameDirectory $candidate) { return $candidate }
    }
}

try {
    if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'Install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0 and run this file again.'
    }
    if (!$GameDirectory -and (Test-Path -LiteralPath $configPath -PathType Leaf)) {
        try { $GameDirectory = (Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json).GameDirectory }
        catch { Write-Host 'Choose your Railroader installation again.' }
    }
    if (!$GameDirectory) { $GameDirectory = Find-SteamGame }
    if (!(Test-GameDirectory $GameDirectory)) {
        Add-Type -AssemblyName System.Windows.Forms
        $picker = New-Object System.Windows.Forms.FolderBrowserDialog
        $picker.Description = 'Select the Railroader installation containing Railroader.exe.'
        $picker.ShowNewFolderButton = $false
        try {
            if ($picker.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
                throw 'Build cancelled.'
            }
            $GameDirectory = $picker.SelectedPath
        } finally {
            $picker.Dispose()
        }
    }
    if (!(Test-GameDirectory $GameDirectory)) { throw 'The selected folder is not a Railroader installation.' }

    $GameDirectory = (Resolve-Path -LiteralPath $GameDirectory).Path
    $null = New-Item -ItemType Directory -Path (Split-Path $configPath -Parent) -Force
    @{GameDirectory = $GameDirectory} | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
    Write-Host "Building with the installed game at $GameDirectory"
    & (Join-Path $PSScriptRoot 'Build.ps1') -GameDirectory $GameDirectory -Package
    & (Join-Path $PSScriptRoot 'Test.ps1') -GameDirectory $GameDirectory

    $manifestPath = Join-Path $PSScriptRoot 'EnusDE-Communications\Info.json'
    $version = (Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json).version
    $zip = Join-Path $PSScriptRoot ("build\EnusDE-Communications-v$version.zip")
    Write-Host "Build and automated checks passed. Install package: $zip"
    if (!$NoOpen) { Start-Process explorer.exe -ArgumentList ('/select,"' + $zip + '"') }
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
