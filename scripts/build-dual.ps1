# Build CN and Global Release zips into dist/cn and dist/global.
param(
    [string]$ProjectDir = "$PSScriptRoot\..\HeelsToggle",
    [string]$DistDir = "$PSScriptRoot\..\dist",
    [string]$AssemblyName = "HeelsDesignLinker",
    [switch]$SkipGlobal,
    [switch]$SkipCn
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\lib\PublishHelpers.ps1"

function Invoke-PluginBuild {
    param(
        [bool]$UseCn,
        [string]$Label,
        [string]$OutDir
    )

    $dev = Test-DalamudDevReady -UseCn $UseCn
    if (-not $dev.Ready) {
        throw "Dalamud dev DLL not found for $Label build: $($dev.DalamudDll). Launch the game once with the matching launcher."
    }

    $flag = if ($UseCn) { "true" } else { "false" }
    Write-Host ""
    Write-Host "=== Build $Label (Use_Dalamud_CN=$flag) ===" -ForegroundColor Cyan
    Write-Host "Using: $($dev.DalamudDll)"

    Push-Location $ProjectDir
    try {
        # Write-Host 消费 dotnet 输出，避免污染函数返回值（否则 $cnZip 会变成对象数组，Get-FileHash 报空路径）
        dotnet build -c Release -v minimal -p:Use_Dalamud_CN=$flag 2>&1 | Write-Host
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed for $Label (exit $LASTEXITCODE)"
        }
    } finally {
        Pop-Location
    }

    $zipSrc = Resolve-PluginZip -ProjectDir $ProjectDir -AssemblyName $AssemblyName
    $version = Assert-ValidPluginZip -ZipPath $zipSrc -AssemblyName $AssemblyName
    if ($version) {
        Write-Host "AssemblyVersion in zip: $version"
    }

    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
    $zipDest = Join-Path $OutDir "latest.zip"
    Copy-Item -Force $zipSrc $zipDest
    Write-Host "Collected -> $zipDest" -ForegroundColor Green

    return $zipDest
}

if (Test-Path $DistDir) {
    Remove-Item -Recurse -Force $DistDir
}
New-Item -ItemType Directory -Force -Path $DistDir | Out-Null

# 不用 $built.Cn / $built.Global：PowerShell 点号访问 hashtable 不可靠（Global 还是保留名）
$cnZip = $null
$globalZip = $null

if (-not $SkipCn) {
    $cnZip = Invoke-PluginBuild -UseCn $true -Label "CN" -OutDir (Join-Path $DistDir "cn")
}

if (-not $SkipGlobal) {
    $globalDev = Test-DalamudDevReady -UseCn $false
    if (-not $globalDev.Ready) {
        Write-Warning "Skipping Global build: $($globalDev.DalamudDll) not found. Install XIVLauncher (international) and launch the game once, or pass -SkipGlobal intentionally."
    } else {
        $globalZip = Invoke-PluginBuild -UseCn $false -Label "Global" -OutDir (Join-Path $DistDir "global")
    }
} else {
    Write-Host "SkipGlobal: not building dist/global/latest.zip" -ForegroundColor Yellow
}

if ($cnZip -and $globalZip -and (Test-Path -LiteralPath $cnZip) -and (Test-Path -LiteralPath $globalZip)) {
    $same = (Get-FileHash -LiteralPath $cnZip).Hash -eq (Get-FileHash -LiteralPath $globalZip).Hash
    Write-Host ""
    Write-Host "CN vs Global zip identical: $same" -ForegroundColor $(if ($same) { "Green" } else { "Yellow" })
}

Write-Host ""
Write-Host "Dual build complete. Output: $DistDir" -ForegroundColor Green
if ($cnZip) { Write-Host "  CN:     $cnZip" }
if ($globalZip) { Write-Host "  Global: $globalZip" }
if (-not $cnZip -and -not $globalZip) { Write-Warning "No zips were produced." }
