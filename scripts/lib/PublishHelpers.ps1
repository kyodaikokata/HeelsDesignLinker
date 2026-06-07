function Get-PluginZipCandidates {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectDir,
        [Parameter(Mandatory)]
        [string]$AssemblyName
    )

    @(
        (Join-Path $ProjectDir "bin\Release\$AssemblyName\latest.zip"),
        (Join-Path $ProjectDir "bin\Release\net10.0-windows\$AssemblyName\latest.zip"),
        (Join-Path $ProjectDir "bin\Release\net10.0-windows\latest.zip")
    )
}

function Resolve-PluginZip {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectDir,
        [Parameter(Mandatory)]
        [string]$AssemblyName
    )

    $zip = Get-PluginZipCandidates -ProjectDir $ProjectDir -AssemblyName $AssemblyName |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1

    if (-not $zip) {
        throw "latest.zip not found under $ProjectDir\bin\Release. Run dotnet build -c Release first."
    }

    $zip
}

function Test-DalamudDevReady {
    param([bool]$UseCn)

    $root = if ($UseCn) {
        Join-Path $env:APPDATA "XIVLauncherCN\addon\Hooks\dev\Dalamud.dll"
    } else {
        Join-Path $env:APPDATA "XIVLauncher\addon\Hooks\dev\Dalamud.dll"
    }

    [PSCustomObject]@{
        UseCn = $UseCn
        Label = if ($UseCn) { "CN" } else { "Global" }
        DalamudDll = $root
        Ready = Test-Path $root
    }
}

function Assert-ValidPluginZip {
    param(
        [Parameter(Mandatory)]
        [string]$ZipPath,
        [Parameter(Mandatory)]
        [string]$AssemblyName
    )

    if (-not (Test-Path $ZipPath)) {
        throw "Zip not found: $ZipPath"
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        Write-Host "zip entries ($ZipPath):"
        $zip.Entries | ForEach-Object { Write-Host "  - $($_.FullName)" }

        $bad = $zip.Entries | Where-Object {
            $_.FullName -match '[/\\]' -or
            $_.FullName -match '\.deps\.json$' -or
            $_.Name -eq 'latest.zip'
        }
        if ($bad) {
            throw "Invalid zip structure (nested paths, deps.json, or nested zip)."
        }

        $required = @("$AssemblyName.dll", "$AssemblyName.json", "$AssemblyName.pdb")
        $names = $zip.Entries | ForEach-Object { $_.Name }
        foreach ($r in $required) {
            if ($names -notcontains $r) {
                throw "latest.zip missing $r (expected dll + pdb + json)."
            }
        }

        if ($zip.Entries.Count -ne 3) {
            throw "latest.zip must contain exactly 3 files, found $($zip.Entries.Count)."
        }

        $manifestEntry = $zip.Entries | Where-Object { $_.Name -eq "$AssemblyName.json" } | Select-Object -First 1
        if ($manifestEntry) {
            $stream = $manifestEntry.Open()
            try {
                $reader = New-Object System.IO.StreamReader($stream)
                $json = $reader.ReadToEnd()
            } finally {
                $reader.Close()
                $stream.Close()
            }

            if ($json -notmatch '"IconUrl"\s*:\s*"[^"]+"') {
                throw "latest.zip manifest missing IconUrl. Add <IconUrl> to the csproj so Dalamud can show the plugin icon after install."
            }

            if ($json -match '"AssemblyVersion"\s*:\s*"([^"]+)"') {
                return $Matches[1]
            }
        }
    } finally {
        $zip.Dispose()
    }

    return $null
}

function Update-PluginMasterLastUpdate {
    param(
        [Parameter(Mandatory)]
        [string]$MasterPath
    )

    if (-not (Test-Path $MasterPath)) {
        throw "pluginmaster not found: $MasterPath"
    }

    $lastUpdate = [int][double]::Parse((Get-Date -Date (Get-Date).ToUniversalTime() -UFormat %s))
    $text = Get-Content $MasterPath -Raw -Encoding UTF8

    if ($text -match '"LastUpdate"\s*:\s*"[^"]*"') {
        $text = [regex]::Replace($text, '"LastUpdate"\s*:\s*"[^"]*"', "`"LastUpdate`": `"$lastUpdate`"", 1)
    } else {
        throw "LastUpdate field not found in $MasterPath"
    }

    [System.IO.File]::WriteAllText($MasterPath, $text.TrimEnd() + "`r`n", [System.Text.UTF8Encoding]::new($false))
    Write-Host "Updated LastUpdate = $lastUpdate in $MasterPath"
    return $lastUpdate
}

function Write-PluginMasterArray {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [object]$Entry
    )

    $json = ($Entry | ConvertTo-Json -Depth 10)
    [System.IO.File]::WriteAllText($Path, "[`r`n$json`r`n]", [System.Text.UTF8Encoding]::new($false))
}
