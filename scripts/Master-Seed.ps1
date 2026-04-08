<#
.SYNOPSIS
    Master orchestrator for HCS Case Evaluation Portal data seeding.
.DESCRIPTION
    Runs prerequisite checks, then phases A1-A6 sequentially.
    Writes seed-state.json after each phase for re-runnability.
    Supports resuming from a previous run via completedPhases tracking.
.EXAMPLE
    .\Master-Seed.ps1
    .\Master-Seed.ps1 -SkipPrerequisites
    .\Master-Seed.ps1 -StartFromPhase "A3"
#>
param(
    [string]$ApiBaseUrl = "https://localhost:44327",
    [string]$AuthServerUrl = "https://localhost:44368",
    [switch]$SkipPrerequisites,
    [string]$StartFromPhase = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = $PSScriptRoot
$helpersDir = Join-Path $scriptRoot "helpers"
$phasesDir = Join-Path $scriptRoot "phases"
$stateFile = Join-Path $scriptRoot "seed-state.json"

# Load helpers
. "$helpersDir\Get-AuthToken.ps1"
. "$helpersDir\Invoke-ApiCall.ps1"
. "$helpersDir\New-FakeData.ps1"

Write-Host @"

  ╔══════════════════════════════════════════════════╗
  ║  HCS Case Evaluation Portal - Data Seeder       ║
  ║  Plan A: Populate database via REST API          ║
  ╚══════════════════════════════════════════════════╝

"@ -ForegroundColor Cyan

$startTime = Get-Date

# ---- Load or Initialize Seed State ----

# PS 5.x compatible: ConvertFrom-Json returns PSCustomObject, not hashtable.
# Recursively convert to hashtable so bracket notation ($obj[$key]) works.
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

function Save-SeedState {
    param([hashtable]$State, [string]$Path)
    $State.meta.lastSaved = (Get-Date).ToString("o")
    $State | ConvertTo-Json -Depth 10 | Set-Content -Path $Path -Encoding UTF8
    Write-Host "  [STATE] Saved to $Path" -ForegroundColor DarkGray
}

if (Test-Path $stateFile) {
    Write-Host "Loading existing seed state from $stateFile..." -ForegroundColor Yellow
    $seedState = Get-Content $stateFile -Raw | ConvertFrom-Json | ConvertTo-Hashtable
    Write-Host "  Completed phases: $($seedState.meta.completedPhases -join ', ')" -ForegroundColor DarkGray
} else {
    Write-Host "No existing seed state found. Starting fresh." -ForegroundColor Yellow
    $seedState = @{
        meta = @{
            seedDate        = (Get-Date).ToString("o")
            version         = "1.0"
            completedPhases = @()
            lastSaved       = ""
        }
    }
}

# ---- Phase execution helper ----

function Invoke-Phase {
    param(
        [string]$PhaseId,
        [string]$ScriptFile,
        [hashtable]$State,
        [hashtable]$ExtraParams = @{}
    )

    # Skip if already completed (unless forced via StartFromPhase)
    if ($State.meta.completedPhases -contains $PhaseId -and -not $StartFromPhase) {
        Write-Host "`n[SKIP] Phase $PhaseId already completed. Reloading state." -ForegroundColor DarkGray
        return $State
    }

    # Skip if we haven't reached the start phase yet
    if ($StartFromPhase -and $PhaseId -lt $StartFromPhase) {
        Write-Host "`n[SKIP] Phase $PhaseId - starting from $StartFromPhase" -ForegroundColor DarkGray
        return $State
    }

    $phaseStart = Get-Date
    Write-Host "`n" -NoNewline

    $params = @{ SeedState = $State } + $ExtraParams
    # Run phase script; capture output to avoid contaminating $State
    # The phase script modifies $State in-place (hashtable = reference type) and also returns it.
    # We suppress output to avoid PS array wrapping issues when commands emit to the success stream.
    $phaseReturn = & $ScriptFile @params
    # Use the last element if output was an array (PS wrapping issue), or the direct return
    if ($phaseReturn -is [System.Collections.Hashtable]) {
        $State = $phaseReturn
    } elseif ($phaseReturn -is [array] -and $phaseReturn.Count -gt 0) {
        # Find the hashtable in the output array (it's the return value)
        for ($ri = $phaseReturn.Count - 1; $ri -ge 0; $ri--) {
            if ($phaseReturn[$ri] -is [System.Collections.Hashtable]) {
                $State = $phaseReturn[$ri]
                break
            }
        }
    }
    # $State is already modified in-place since hashtables are reference types

    # Mark phase complete
    if ($State.meta.completedPhases -notcontains $PhaseId) {
        $State.meta.completedPhases += $PhaseId
    }

    $elapsed = (Get-Date) - $phaseStart
    Write-Host "  Phase $PhaseId completed in $($elapsed.TotalSeconds.ToString('F1'))s" -ForegroundColor DarkCyan

    # Save state after each phase
    Save-SeedState -State $State -Path $stateFile

    return $State
}

# ---- Prerequisites ----

if (-not $SkipPrerequisites) {
    . "$helpersDir\Test-Prerequisites.ps1"
    $prereqResult = Test-Prerequisites -AuthServerUrl $AuthServerUrl -ApiBaseUrl $ApiBaseUrl
    if (-not $prereqResult) {
        Write-Host "`nPrerequisites failed. Aborting." -ForegroundColor Red
        exit 1
    }
}

# ---- Get Host Admin Token ----

$defaultPassword = $env:TEST_PASSWORD
$hostToken = Get-AuthToken -Username "admin@abp.io" -Password $defaultPassword -AuthServerUrl $AuthServerUrl
Write-Host "Host admin token acquired." -ForegroundColor Green

# ---- Execute Phases ----

$seedState = Invoke-Phase -PhaseId "A1" -ScriptFile "$phasesDir\Phase-A1-ReferenceData.ps1" -State $seedState `
    -ExtraParams @{ Token = $hostToken; ApiBaseUrl = $ApiBaseUrl }

$seedState = Invoke-Phase -PhaseId "A2" -ScriptFile "$phasesDir\Phase-A2-LocationsWcab.ps1" -State $seedState `
    -ExtraParams @{ Token = $hostToken; ApiBaseUrl = $ApiBaseUrl }

$seedState = Invoke-Phase -PhaseId "A3" -ScriptFile "$phasesDir\Phase-A3-Doctors.ps1" -State $seedState `
    -ExtraParams @{ Token = $hostToken; ApiBaseUrl = $ApiBaseUrl; AuthServerUrl = $AuthServerUrl }

$seedState = Invoke-Phase -PhaseId "A4" -ScriptFile "$phasesDir\Phase-A4-Availability.ps1" -State $seedState `
    -ExtraParams @{ ApiBaseUrl = $ApiBaseUrl; AuthServerUrl = $AuthServerUrl }

$seedState = Invoke-Phase -PhaseId "A5" -ScriptFile "$phasesDir\Phase-A5-Appointments.ps1" -State $seedState `
    -ExtraParams @{ ApiBaseUrl = $ApiBaseUrl; AuthServerUrl = $AuthServerUrl }

$seedState = Invoke-Phase -PhaseId "A6" -ScriptFile "$phasesDir\Phase-A6-ChildEntities.ps1" -State $seedState `
    -ExtraParams @{ ApiBaseUrl = $ApiBaseUrl; AuthServerUrl = $AuthServerUrl }

# ---- Verification ----

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "VERIFICATION" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

$checks = @(
    @{ Name = "States"; Url = "$ApiBaseUrl/api/app/states?maxResultCount=50"; Expected = 10 },
    @{ Name = "Appointment Types"; Url = "$ApiBaseUrl/api/app/appointment-types?maxResultCount=50"; Expected = 6 },
    @{ Name = "Appointment Statuses"; Url = "$ApiBaseUrl/api/app/appointment-statuses?maxResultCount=50"; Expected = 13 },
    @{ Name = "Appointment Languages"; Url = "$ApiBaseUrl/api/app/appointment-languages?maxResultCount=50"; Expected = 12 },
    @{ Name = "Locations"; Url = "$ApiBaseUrl/api/app/locations?maxResultCount=50"; Expected = 8 },
    @{ Name = "WCAB Offices"; Url = "$ApiBaseUrl/api/app/wcab-offices?maxResultCount=50"; Expected = 7 }
)

foreach ($check in $checks) {
    try {
        $response = Invoke-ApiCall -Method "GET" -Url $check.Url -Token $hostToken
        $actual = if ($response.totalCount) { $response.totalCount } else { $response.items.Count }
        $status = if ($actual -ge $check.Expected) { "PASS" } else { "WARN" }
        $color = if ($status -eq "PASS") { "Green" } else { "Yellow" }
        Write-Host "  [$status] $($check.Name): $actual (expected >= $($check.Expected))" -ForegroundColor $color
    } catch {
        Write-Host "  [FAIL] $($check.Name): Error - $_" -ForegroundColor Red
    }
}

# Check tenant-scoped appointments
foreach ($t in 1..5) {
    $key = "T$t"
    $tenantId = $seedState.tenantIds[$key]
    if (-not $tenantId) { continue }

    $tenantEmail = $seedState._tenantEmails[$key]
    $tenantToken = Get-AuthToken -Username $tenantEmail -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $tenantId

    try {
        $response = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/appointments?maxResultCount=50" -Token $tenantToken -TenantId $tenantId
        $actual = if ($response.totalCount) { $response.totalCount } else { 0 }
        $expected = switch ($t) { 1 { 13 } 2 { 10 } 3 { 5 } default { 0 } }
        $status = if ($actual -ge $expected) { "PASS" } else { "WARN" }
        $color = if ($status -eq "PASS") { "Green" } else { "Yellow" }
        Write-Host "  [$status] $key Appointments: $actual (expected >= $expected)" -ForegroundColor $color
    } catch {
        Write-Host "  [FAIL] $key Appointments: Error - $_" -ForegroundColor Red
    }
}

# ---- Summary ----

$totalTime = (Get-Date) - $startTime

Write-Host @"

  ╔══════════════════════════════════════════════════╗
  ║  SEEDING COMPLETE                                ║
  ╠══════════════════════════════════════════════════╣
  ║  Total time: $($totalTime.TotalSeconds.ToString('F1'))s
  ║  State file: $stateFile
  ║  Completed:  $($seedState.meta.completedPhases -join ', ')
  ╚══════════════════════════════════════════════════╝

"@ -ForegroundColor Green
