# 安装失败时在本地自检：远程 zip、pluginmaster、本机残留目录
param(
    [string]$RepoRoot = "$PSScriptRoot\..",
    [string]$InstallDir = "$env:APPDATA\XIVLauncherCN\installedPlugins\HeelsDesignLinker",
    [string]$LogPath = "$env:APPDATA\XIVLauncherCN\dalamud.log"
)

$ErrorActionPreference = "Continue"
$masterPath = Join-Path $RepoRoot "pluginmaster.cn.json"
if (-not (Test-Path $masterPath)) {
    $masterPath = Join-Path $RepoRoot "pluginmaster.json"
}
$localZip = Join-Path $RepoRoot "plugins\HeelsDesignLinker\latest-cn.zip"

Write-Host "=== Heels Design Linker install diagnose ===" -ForegroundColor Cyan

if (Test-Path $masterPath) {
    $master = Get-Content $masterPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $entry = $master[0]
    Write-Host "`n$([System.IO.Path]::GetFileName($masterPath)):"
    Write-Host "  AssemblyVersion: $($entry.AssemblyVersion)"
    Write-Host "  DalamudApiLevel: $($entry.DalamudApiLevel)"
    Write-Host "  CanUnloadAsync:  $($entry.CanUnloadAsync)"
    Write-Host "  LastUpdate:      $($entry.LastUpdate)"
    Write-Host "  Download:        $($entry.DownloadLinkInstall)"
    if ($entry.Name -match 'deprecated') {
        Write-Host "  WARN: deprecated manifest URL — switch custom repo to pluginmaster.cn.json" -ForegroundColor Yellow
    }
    $zipUrl = $entry.DownloadLinkInstall
} else {
    Write-Host "WARN: pluginmaster.json not found at $masterPath" -ForegroundColor Yellow
    $zipUrl = "https://raw.githubusercontent.com/kyodaikokata/HeelsDesignLinker/main/plugins/HeelsDesignLinker/latest-cn.zip"
}

function Test-ZipFile([string]$path, [string]$label) {
    Write-Host "`n--- $label ---"
    if (-not (Test-Path $path)) {
        Write-Host "  MISSING: $path" -ForegroundColor Red
        return $false
    }
    $len = (Get-Item $path).Length
    Write-Host "  Size: $len bytes"
    if ($len -lt 5000) {
        Write-Host "  WARN: zip too small (maybe HTML error page)" -ForegroundColor Yellow
    }
    try {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [System.IO.Compression.ZipFile]::OpenRead($path)
        foreach ($e in $zip.Entries) { Write-Host "  entry: $($e.FullName)" }
        $bad = $zip.Entries | Where-Object { $_.FullName -match '[/\\]' }
        if ($bad) {
            Write-Host "  FAIL: nested paths in zip" -ForegroundColor Red
            $zip.Dispose()
            return $false
        }
        $names = $zip.Entries | ForEach-Object { $_.Name }
        if ($names -notcontains "HeelsDesignLinker.dll" -or $names -notcontains "HeelsDesignLinker.json") {
            Write-Host "  FAIL: missing dll or json" -ForegroundColor Red
            $zip.Dispose()
            return $false
        }
        $manifestEntry = $zip.Entries | Where-Object { $_.Name -eq "HeelsDesignLinker.json" } | Select-Object -First 1
        if ($manifestEntry) {
            $sr = New-Object System.IO.StreamReader($manifestEntry.Open())
            $json = $sr.ReadToEnd(); $sr.Close()
            if ($json -match '"AssemblyVersion"\s*:\s*"([^"]+)"') { Write-Host "  zip AssemblyVersion: $($Matches[1])" }
            if ($json -match '"DalamudApiLevel"\s*:\s*(\d+)') { Write-Host "  zip DalamudApiLevel: $($Matches[1])" }
        }
        $zip.Dispose()
        Write-Host "  OK: zip structure" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "  FAIL: cannot open zip - $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

$localZipOk = Test-ZipFile $localZip "Local plugins/HeelsDesignLinker/latest-cn.zip"
if ($localZipOk -and (Test-Path $masterPath)) {
    $master = Get-Content $masterPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $entry = $master[0]
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $z = [System.IO.Compression.ZipFile]::OpenRead($localZip)
    $me = $z.Entries | Where-Object { $_.Name -eq "HeelsDesignLinker.json" } | Select-Object -First 1
    if ($me) {
        $sr = New-Object System.IO.StreamReader($me.Open()); $zj = $sr.ReadToEnd(); $sr.Close()
        if ($zj -match '"AssemblyVersion"\s*:\s*"([^"]+)"') {
            $zipVer = $Matches[1]
            if ($entry.AssemblyVersion -ne $zipVer) {
                Write-Host "`nFAIL: manifest AssemblyVersion ($($entry.AssemblyVersion)) != zip ($zipVer)" -ForegroundColor Red
                Write-Host "  Dalamud error: Distributed plugin version does not match repo version"
            }
        }
    }
    $z.Dispose()
}

$remoteZip = Join-Path $env:TEMP "HeelsDesignLinker-remote.zip"
$remoteDir = Join-Path $env:TEMP "HeelsDesignLinker-remote-extract"
Remove-Item $remoteZip, $remoteDir -Recurse -Force -ErrorAction SilentlyContinue
try {
    Invoke-WebRequest -Uri $zipUrl -OutFile $remoteZip -UseBasicParsing
    [void](Test-ZipFile $remoteZip "Remote download")
} catch {
    Write-Host "`nRemote download FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  Try VPN / different network / raw.githubusercontent.com URL in pluginmaster"
}

Write-Host "`n--- Installed folder ---"
if (Test-Path $InstallDir) {
    Write-Host "  EXISTS (can block clean reinstall): $InstallDir" -ForegroundColor Yellow
    Get-ChildItem $InstallDir -Recurse | ForEach-Object { Write-Host "    $($_.FullName.Replace($InstallDir, ''))" }
    $nested = Get-ChildItem $InstallDir -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match 'net\d' }
    if ($nested) {
        Write-Host "  FAIL: old bad layout (net10.0-windows subfolder). Delete entire HeelsDesignLinker folder." -ForegroundColor Red
    }
} else {
    Write-Host "  OK: not present (good for fresh install)" -ForegroundColor Green
}

Write-Host "`n--- dalamud.log (last install errors) ---"
if (Test-Path $LogPath) {
    Select-String -Path $LogPath -Pattern 'HeelsDesignLinker|HeelsToggle|PLUGINR|InstallPlugin|install failed|SSL|zip' -CaseSensitive:$false |
        Select-Object -Last 25 |
        ForEach-Object { Write-Host "  $($_.Line)" }
} else {
    Write-Host "  Log not found: $LogPath"
}

Write-Host "`n=== If remote zip OK but game still fails ===" -ForegroundColor Cyan
Write-Host "1. Update XIVLauncher CN + Dalamud to latest (needs API 15 / .NET 10)."
Write-Host "2. Exit game; delete: $InstallDir"
Write-Host "3. /xlsettings: remove custom repo, re-add with ?v=timestamp on pluginmaster URL."
Write-Host "4. Manual install: extract remote zip into installedPlugins\HeelsDesignLinker\ (game closed)."
Write-Host "5. Send the last 25 log lines above to the author if still failing."
