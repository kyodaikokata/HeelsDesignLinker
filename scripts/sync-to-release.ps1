# DEPRECATED: use publish-release.ps1 (build-dual -> KKT-Catalog/scripts/publish-plugin.ps1).
# See Release/KKT-Catalog/REPOSITORY.md. This script remains for legacy HeelsDesignLinker single-repo releases only.
# Sync source, dual zips, and pluginmaster.*.json into the git push folder.
param(
    [string]$SourceRoot = "$PSScriptRoot\..",
    [string]$ReleaseRoot = "E:\work\DalamudProject\Release\HeelsDesignLinker",
    [string]$DistDir = "$PSScriptRoot\..\dist",
    [string]$AssemblyName = "HeelsDesignLinker",
    [string]$PluginFolder = "HeelsDesignLinker",
    [string]$CatalogGitHubRepo = "kyodaikokata/HeelsDesignLinker",
    [switch]$SkipSourceSync,
    [switch]$SkipGlobalZip
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\lib\PublishHelpers.ps1"

$cnZip = Join-Path $DistDir "cn\latest.zip"
$globalZip = Join-Path $DistDir "global\latest.zip"

if (-not (Test-Path $cnZip)) {
    throw "CN zip not found: $cnZip. Run scripts\build-dual.ps1 first."
}

$pluginsDir = Join-Path $ReleaseRoot "plugins\$PluginFolder"
New-Item -ItemType Directory -Force -Path $pluginsDir | Out-Null

Copy-Item -Force $cnZip (Join-Path $pluginsDir "latest-cn.zip")
Write-Host "Copied CN zip -> $pluginsDir\latest-cn.zip"

if ((Test-Path $globalZip) -and -not $SkipGlobalZip) {
    Copy-Item -Force $globalZip (Join-Path $pluginsDir "latest-global.zip")
    Write-Host "Copied Global zip -> $pluginsDir\latest-global.zip"
} else {
    Write-Warning "Global zip not copied (missing dist or -SkipGlobalZip). Only CN zip will be published."
}

if (-not $SkipSourceSync) {
    $srcProj = Join-Path $SourceRoot "HeelsToggle"
    $dstProj = Join-Path $ReleaseRoot "HeelsToggle"
    if (-not (Test-Path $srcProj)) {
        throw "Source project not found: $srcProj"
    }

    Write-Host "Syncing source $srcProj -> $dstProj"
    & robocopy $srcProj $dstProj /MIR /XD bin obj .idea .vs dist /XF *.user /NFL /NDL /NJH /NJS /nc /ns /np
    if ($LASTEXITCODE -ge 8) {
        throw "robocopy failed with exit code $LASTEXITCODE"
    }

    $srcImages = Join-Path $SourceRoot "images"
    $dstImages = Join-Path $ReleaseRoot "images"
    if (Test-Path (Join-Path $srcImages "icon.png")) {
        New-Item -ItemType Directory -Force -Path $dstImages | Out-Null
        Copy-Item -Force (Join-Path $srcImages "icon.png") (Join-Path $dstImages "icon.png")
        Write-Host "Synced plugin icon -> $dstImages\icon.png"
    } else {
        Write-Warning "images/icon.png not found under $SourceRoot. pluginmaster IconUrl may 404 until the icon is committed."
    }
}

$baseRaw = "https://raw.githubusercontent.com/$CatalogGitHubRepo/main"
$zipCnUrl = "$baseRaw/plugins/$PluginFolder/latest-cn.zip"
$zipGlobalUrl = "$baseRaw/plugins/$PluginFolder/latest-global.zip"
$iconUrl = "$baseRaw/images/icon.png"

$masterCn = Join-Path $SourceRoot "pluginmaster.cn.json"
$masterGlobal = Join-Path $SourceRoot "pluginmaster.global.json"
if (-not (Test-Path $masterCn) -or -not (Test-Path $masterGlobal)) {
    throw "pluginmaster.cn.json / pluginmaster.global.json not found under $SourceRoot"
}

Copy-Item -Force $masterCn (Join-Path $ReleaseRoot "pluginmaster.cn.json")
Copy-Item -Force $masterGlobal (Join-Path $ReleaseRoot "pluginmaster.global.json")

$cnEntry = Get-Content (Join-Path $ReleaseRoot "pluginmaster.cn.json") -Raw -Encoding UTF8 | ConvertFrom-Json
$globalEntry = Get-Content (Join-Path $ReleaseRoot "pluginmaster.global.json") -Raw -Encoding UTF8 | ConvertFrom-Json
$cnItem = if ($cnEntry -is [System.Array]) { $cnEntry[0] } else { $cnEntry }
$globalItem = if ($globalEntry -is [System.Array]) { $globalEntry[0] } else { $globalEntry }

$cnVersion = Assert-ValidPluginZip -ZipPath (Join-Path $pluginsDir "latest-cn.zip") -AssemblyName $AssemblyName
if ($cnVersion) {
    $cnItem.AssemblyVersion = $cnVersion
    $globalItem.AssemblyVersion = $cnVersion
}

$cnItem.DownloadLinkInstall = $zipCnUrl
$cnItem.DownloadLinkUpdate = $zipCnUrl
$cnItem.DownloadLinkTesting = $zipCnUrl
$cnItem.IconUrl = $iconUrl

$globalItem.DownloadLinkInstall = $zipGlobalUrl
$globalItem.DownloadLinkUpdate = $zipGlobalUrl
$globalItem.DownloadLinkTesting = $zipGlobalUrl
$globalItem.IconUrl = $iconUrl

Write-PluginMasterArray -Path (Join-Path $ReleaseRoot "pluginmaster.cn.json") -Entry $cnItem
Write-PluginMasterArray -Path (Join-Path $ReleaseRoot "pluginmaster.global.json") -Entry $globalItem

Update-PluginMasterLastUpdate -MasterPath (Join-Path $ReleaseRoot "pluginmaster.cn.json") | Out-Null
Update-PluginMasterLastUpdate -MasterPath (Join-Path $ReleaseRoot "pluginmaster.global.json") | Out-Null

# Legacy pluginmaster.json: keep deprecated label but sync version/links with CN zip (Dalamud rejects mismatches).
$legacyMaster = Join-Path $SourceRoot "pluginmaster.json"
if (Test-Path $legacyMaster) {
    $legacyEntry = Get-Content $legacyMaster -Raw -Encoding UTF8 | ConvertFrom-Json
    $legacyItem = if ($legacyEntry -is [System.Array]) { $legacyEntry[0] } else { $legacyEntry }
    if ($cnVersion) { $legacyItem.AssemblyVersion = $cnVersion }
    $legacyItem.DownloadLinkInstall = $zipCnUrl
    $legacyItem.DownloadLinkUpdate = $zipCnUrl
    $legacyItem.DownloadLinkTesting = $zipCnUrl
    $legacyItem.IconUrl = $iconUrl
    Write-PluginMasterArray -Path (Join-Path $ReleaseRoot "pluginmaster.json") -Entry $legacyItem
    Write-PluginMasterArray -Path $legacyMaster -Entry $legacyItem
    Update-PluginMasterLastUpdate -MasterPath (Join-Path $ReleaseRoot "pluginmaster.json") | Out-Null
    Write-Host "Synced legacy pluginmaster.json (AssemblyVersion = $cnVersion)"
}

$gitignorePath = Join-Path $ReleaseRoot ".gitignore"
$gitignore = @"
## Ignore Visual Studio temporary files, build results, and
## files generated by popular Visual Studio add-ons.

# User-specific files
*.suo
*.user
*.userosscache
*.sln.docstates

# Build results
[Dd]ebug/
[Dd]ebugPublic/
[Rr]elease/
[Rr]eleases/
x64/
x86/
[Bb]in/
[Oo]bj/
[Ll]og/

# Visual Studio cache/options directory
.vs/

# Rider
.idea/

# Visual Studio Code
.vscode/

# NuGet Packages
*.nupkg
**/packages/*
!**/packages/build/

# Build outputs that should be included
!plugins/$PluginFolder/latest-cn.zip
!plugins/$PluginFolder/latest-global.zip

# Dalamud
Dalamud/
*.json.old

# OS files
Thumbs.db
.DS_Store
"@
Set-Content -Path $gitignorePath -Value $gitignore.TrimEnd() -Encoding UTF8

Write-Host ""
Write-Host "Release folder ready: $ReleaseRoot" -ForegroundColor Green
Write-Host "CN install URL:     $baseRaw/pluginmaster.cn.json"
Write-Host "Global install URL: $baseRaw/pluginmaster.global.json"
Write-Host ""
Write-Host "Next:"
Write-Host "  cd `"$ReleaseRoot`""
Write-Host "  git add pluginmaster.json pluginmaster.cn.json pluginmaster.global.json plugins/$PluginFolder/*.zip HeelsToggle/ images/icon.png"
Write-Host "  git commit -m `"Release $cnVersion`""
Write-Host "  git push"
