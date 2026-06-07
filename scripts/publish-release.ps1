# Full release pipeline: build-dual (WIP) -> KKT-Catalog publish-plugin.ps1
param(
    [string]$InternalName = "HeelsDesignLinker",
    [string]$ProjectDir = "$PSScriptRoot\..\HeelsToggle",
    [string]$SourceRoot = "$PSScriptRoot\..",
    [string]$DistDir = "$PSScriptRoot\..\dist",
    [string]$CatalogRoot,
    [switch]$SkipGlobal,
    [switch]$SkipSourceSync,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

if (-not $CatalogRoot) {
    if ($env:KKT_CATALOG_ROOT) {
        $CatalogRoot = $env:KKT_CATALOG_ROOT
    } else {
        $defaultCatalog = "E:\work\DalamudProject\Release\KKT-Catalog"
        $catalogJson = Join-Path $defaultCatalog "catalog.json"
        if (Test-Path $catalogJson) {
            $cfg = Get-Content $catalogJson -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($cfg.catalogRoot -and (Test-Path $cfg.catalogRoot)) {
                $CatalogRoot = $cfg.catalogRoot
            } else {
                $CatalogRoot = $defaultCatalog
            }
        } else {
            $CatalogRoot = $defaultCatalog
        }
    }
}

$buildArgs = @{
    ProjectDir = $ProjectDir
    DistDir    = $DistDir
}
if ($SkipGlobal) { $buildArgs.SkipGlobal = $true }

& "$PSScriptRoot\build-dual.ps1" @buildArgs

$publishArgs = @{
    InternalName = $InternalName
    CatalogRoot  = $CatalogRoot
    DistDir      = $DistDir
}
if ($SkipGlobal) { $publishArgs.SkipGlobal = $true }
if ($WhatIf) { $publishArgs.WhatIf = $true }

$publishScript = Join-Path $CatalogRoot "scripts\publish-plugin.ps1"
if (-not (Test-Path $publishScript)) {
    throw "KKT-Catalog publish script not found: $publishScript. See Release/KKT-Catalog/REPOSITORY.md."
}

& $publishScript @publishArgs

if ($SkipSourceSync) {
    Write-Host "SkipSourceSync: source repo not updated." -ForegroundColor Yellow
    return
}

$syncScript = Join-Path $CatalogRoot "scripts\sync-source-repo.ps1"
if (-not (Test-Path $syncScript)) {
    throw "Source sync script not found: $syncScript"
}

$syncArgs = @{
    InternalName       = $InternalName
    CatalogRoot        = $CatalogRoot
    DistDir            = $DistDir
    WorkInProgressRoot = $SourceRoot
}
if ($WhatIf) { $syncArgs.WhatIf = $true }

& $syncScript @syncArgs
