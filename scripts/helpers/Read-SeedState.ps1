<#
.SYNOPSIS
    Loads seed-state.json and returns it as a nested hashtable.
.DESCRIPTION
    Reads the seed state file created by Master-Seed.ps1 (Plan A),
    converts PSCustomObject to nested hashtables for PS 5.x compatibility,
    and validates that all 6 phases (A1-A6) completed successfully.
#>

function ConvertTo-Hashtable {
    param([Parameter(ValueFromPipeline)][object]$InputObject)
    process {
        if ($null -eq $InputObject) { return $null }
        if ($InputObject -is [System.Collections.IEnumerable] -and $InputObject -isnot [string]) {
            $array = @()
            foreach ($item in $InputObject) { $array += ConvertTo-Hashtable $item }
            return ,$array
        }
        if ($InputObject -is [PSCustomObject]) {
            $hash = @{}
            foreach ($prop in $InputObject.PSObject.Properties) {
                $hash[$prop.Name] = ConvertTo-Hashtable $prop.Value
            }
            return $hash
        }
        return $InputObject
    }
}

function Read-SeedState {
    param(
        [string]$Path
    )

    if (-not $Path) {
        $Path = Join-Path $PSScriptRoot "..\seed-state.json"
    }

    if (-not (Test-Path $Path)) {
        Write-Host "  [ERROR] seed-state.json not found at: $Path" -ForegroundColor Red
        Write-Host "  Run Master-Seed.ps1 (Plan A) first to create seed data." -ForegroundColor Yellow
        return $null
    }

    try {
        $raw = Get-Content $Path -Raw -ErrorAction Stop
        $seedState = $raw | ConvertFrom-Json | ConvertTo-Hashtable
    } catch {
        Write-Host "  [ERROR] Failed to parse seed-state.json: $_" -ForegroundColor Red
        return $null
    }

    # Validate meta
    if (-not $seedState.meta) {
        Write-Host "  [ERROR] seed-state.json missing 'meta' section" -ForegroundColor Red
        return $null
    }

    $completed = $seedState.meta.completedPhases
    if (-not $completed) {
        Write-Host "  [ERROR] seed-state.json missing 'meta.completedPhases'" -ForegroundColor Red
        return $null
    }

    $requiredPhases = @("A1", "A2", "A3", "A4", "A5", "A6")
    $missing = @()
    foreach ($phase in $requiredPhases) {
        if ($completed -notcontains $phase) {
            $missing += $phase
        }
    }

    if ($missing.Count -gt 0) {
        Write-Host "  [WARN] seed-state.json missing phases: $($missing -join ', ')" -ForegroundColor Yellow
        Write-Host "  Some tests may be skipped due to incomplete seed data." -ForegroundColor Yellow
    }

    # Validate key sections exist
    $keySections = @("stateIds", "appointmentTypeIds", "appointmentStatusIds", "locationIds", "tenantIds")
    foreach ($section in $keySections) {
        if (-not $seedState[$section]) {
            Write-Host "  [WARN] seed-state.json missing '$section' section" -ForegroundColor Yellow
        }
    }

    Write-Host "  Seed state loaded: $($completed.Count) phases completed, last saved $($seedState.meta.lastSaved)" -ForegroundColor DarkGray
    return $seedState
}
