<#
.SYNOPSIS
    Master orchestrator for HCS Case Evaluation Portal E2E testing (Plan B).
.DESCRIPTION
    Runs test phases B1-B16 sequentially, accumulates results,
    and produces a Test-Report.md with PASS/FAIL/FAIL-EXPECTED/SKIP results.
    Requires Plan A seed data (seed-state.json) to be present.
.EXAMPLE
    .\Master-Test.ps1
    .\Master-Test.ps1 -SkipPrerequisites
    .\Master-Test.ps1 -StartFromPhase "B5"
    .\Master-Test.ps1 -SkipPhases @("B1", "B14", "B15")
    .\Master-Test.ps1 -IncludeBuildTests
#>
param(
    [string]$ApiBaseUrl = "https://localhost:44327",
    [string]$AuthServerUrl = "https://localhost:44368",
    [switch]$SkipPrerequisites,
    [string]$StartFromPhase = "",
    [string[]]$SkipPhases = @(),
    [switch]$IncludeBuildTests
)

$ErrorActionPreference = "Stop"

$scriptRoot = $PSScriptRoot
$helpersDir = Join-Path $scriptRoot "helpers"
$testsDir = Join-Path $scriptRoot "tests"
$stateFile = Join-Path $scriptRoot "seed-state.json"
$reportFile = Join-Path $scriptRoot "Test-Report.md"

# Load helpers
. "$helpersDir\Get-AuthToken.ps1"
. "$helpersDir\Invoke-ApiCall.ps1"
. "$helpersDir\New-FakeData.ps1"
. "$helpersDir\Read-SeedState.ps1"
. "$helpersDir\Assert-Response.ps1"

Write-Host @"

  +==================================================+
  |  HCS Case Evaluation Portal - E2E Test Runner    |
  |  Plan B: Validate all endpoints & business logic  |
  +==================================================+

"@ -ForegroundColor Cyan

$startTime = Get-Date

# ---- Load Seed State ----

Write-Host "Loading seed state..." -ForegroundColor Yellow
$SeedState = Read-SeedState -Path $stateFile

if (-not $SeedState) {
    Write-Host "`nFATAL: Cannot run tests without seed data. Run Master-Seed.ps1 first." -ForegroundColor Red
    exit 1
}

# ---- Initialize Test Results ----

# Initialize with a dummy entry to avoid PS 5.x "empty collection" validation error
# when passing to functions with [Parameter(Mandatory)][System.Collections.ArrayList]
$TestResults = [System.Collections.ArrayList]::new()
$TestResults.Add(@{ TestId = "_INIT"; Phase = "_"; Name = "Initialization"; Status = "PASS"; Details = ""; GapId = ""; DurationMs = 0; Timestamp = (Get-Date).ToString("o") }) | Out-Null
$defaultPassword = $env:TEST_PASSWORD

# ---- Phase Execution Helper ----

function Invoke-TestPhase {
    param(
        [string]$PhaseId,
        [string]$ScriptFile,
        [System.Collections.ArrayList]$Results,
        [hashtable]$ExtraParams = @{}
    )

    # Skip if before start phase
    if ($StartFromPhase -and $PhaseId -lt $StartFromPhase) {
        Write-Host "`n[SKIP] Phase $PhaseId - starting from $StartFromPhase" -ForegroundColor DarkGray
        return
    }

    # Skip if in skip list
    if ($SkipPhases -contains $PhaseId) {
        Write-Host "`n[SKIP] Phase $PhaseId - in skip list" -ForegroundColor DarkGray
        return
    }

    if (-not (Test-Path $ScriptFile)) {
        Write-Host "`n[SKIP] Phase $PhaseId - script not found: $ScriptFile" -ForegroundColor DarkYellow
        return
    }

    $phaseStart = Get-Date
    Write-Host "`n" -NoNewline

    $params = @{
        TestResults  = $Results
        SeedState    = $SeedState
        ApiBaseUrl   = $ApiBaseUrl
        AuthServerUrl = $AuthServerUrl
    } + $ExtraParams

    try {
        & $ScriptFile @params
    } catch {
        Write-Host "  [ERROR] Phase $PhaseId crashed: $_" -ForegroundColor Red
        Add-TestResult -TestResults $Results -TestId "$PhaseId.0.0" -Name "Phase $PhaseId execution" -Status "FAIL" -Details "Phase script threw: $_"
    }

    $elapsed = (Get-Date) - $phaseStart
    $phaseCount = ($Results | Where-Object { $_.Phase -eq $PhaseId }).Count
    Write-Host "  Phase $PhaseId completed: $phaseCount tests in $($elapsed.TotalSeconds.ToString('F1'))s" -ForegroundColor DarkCyan
}

# ---- Execute All Phases ----

Invoke-TestPhase -PhaseId "B1" -ScriptFile "$testsDir\B1-Infrastructure.ps1" -Results $TestResults `
    -ExtraParams @{ IncludeBuildTests = $IncludeBuildTests.IsPresent }

Invoke-TestPhase -PhaseId "B2" -ScriptFile "$testsDir\B2-Authentication.ps1" -Results $TestResults

Invoke-TestPhase -PhaseId "B3" -ScriptFile "$testsDir\B3-ReferenceData-CRUD.ps1" -Results $TestResults

Invoke-TestPhase -PhaseId "B4" -ScriptFile "$testsDir\B4-LocationsWcab-CRUD.ps1" -Results $TestResults

Invoke-TestPhase -PhaseId "B5" -ScriptFile "$testsDir\B5-Doctors-CRUD.ps1" -Results $TestResults

Invoke-TestPhase -PhaseId "B6" -ScriptFile "$testsDir\B6-ExternalSignup.ps1" -Results $TestResults

Invoke-TestPhase -PhaseId "B7" -ScriptFile "$testsDir\B7-Availability-CRUD.ps1" -Results $TestResults

Invoke-TestPhase -PhaseId "B8" -ScriptFile "$testsDir\B8-Appointments-Critical.ps1" -Results $TestResults

Invoke-TestPhase -PhaseId "B9" -ScriptFile "$testsDir\B9-ChildEntities-CRUD.ps1" -Results $TestResults

Invoke-TestPhase -PhaseId "B10" -ScriptFile "$testsDir\B10-BusinessLogic.ps1" -Results $TestResults

Invoke-TestPhase -PhaseId "B11" -ScriptFile "$testsDir\B11-KnownGaps.ps1" -Results $TestResults

Invoke-TestPhase -PhaseId "B12" -ScriptFile "$testsDir\B12-MultiTenancy.ps1" -Results $TestResults

Invoke-TestPhase -PhaseId "B13" -ScriptFile "$testsDir\B13-Security-Auth.ps1" -Results $TestResults

Invoke-TestPhase -PhaseId "B14" -ScriptFile "$testsDir\B14-DataIntegrity.ps1" -Results $TestResults

Invoke-TestPhase -PhaseId "B15" -ScriptFile "$testsDir\B15-Concurrency.ps1" -Results $TestResults

Invoke-TestPhase -PhaseId "B16" -ScriptFile "$testsDir\B16-Credentials-Licenses.ps1" -Results $TestResults

# ---- Remove init placeholder ----
$initEntry = $TestResults | Where-Object { $_.TestId -eq "_INIT" }
if ($initEntry) { $TestResults.Remove($initEntry) }

# ---- Generate Report ----

Write-TestReport -TestResults $TestResults -OutputPath $reportFile

# ---- Summary ----

$totalTime = (Get-Date) - $startTime
$totalTests = $TestResults.Count
$passed = ($TestResults | Where-Object { $_.Status -eq "PASS" }).Count
$failed = ($TestResults | Where-Object { $_.Status -eq "FAIL" }).Count
$failedExpected = ($TestResults | Where-Object { $_.Status -eq "FAIL-EXPECTED" }).Count
$skipped = ($TestResults | Where-Object { $_.Status -eq "SKIP" }).Count

$summaryColor = if ($failed -eq 0) { "Green" } else { "Yellow" }

Write-Host @"

  +==================================================+
  |  E2E TESTING COMPLETE                             |
  +--------------------------------------------------+
  |  Total:    $totalTests tests
  |  Passed:   $passed
  |  Failed:   $failed (unexpected)
  |  Expected: $failedExpected (known gaps)
  |  Skipped:  $skipped
  |  Time:     $($totalTime.TotalSeconds.ToString('F1'))s
  |  Report:   $reportFile
  +==================================================+

"@ -ForegroundColor $summaryColor
