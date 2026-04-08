<#
.SYNOPSIS
    Phase A1: Create reference/lookup data (Tier 1 entities).
.DESCRIPTION
    Creates States (10), AppointmentTypes (6), AppointmentStatuses (13), AppointmentLanguages (12).
    Auth: Host admin, no tenant header.
    Idempotent: checks for existing records before creating.
#>
param(
    [Parameter(Mandatory)][hashtable]$SeedState,
    [Parameter(Mandatory)][string]$Token,
    [string]$ApiBaseUrl = "https://localhost:44327"
)

$ErrorActionPreference = "Stop"

# Load helpers (needed for $script: variables like $script:WesternStates, $script:LanguagePool)
$helpersDir = Join-Path $PSScriptRoot "..\helpers"
. "$helpersDir\Get-AuthToken.ps1"
. "$helpersDir\Invoke-ApiCall.ps1"
. "$helpersDir\New-FakeData.ps1"

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "PHASE A1: Reference Data (Tier 1)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

# ---- A1.1 STATES (10 records) ----

Write-Host "`n--- A1.1 States ---" -ForegroundColor Yellow

# California MUST be first (required for locations)
$stateNames = @("California")
$otherStates = $script:WesternStates | Where-Object { $_ -ne "California" } | Get-Random -Count 7
$stateNames += $otherStates
# Orphan state (referenced by nothing)
$stateNames += "North Dakota"
# Max-length boundary test - State has no consts max, test DB default (use 50 chars)
$stateNames += (New-MaxLengthString -Length 50 -Prefix "TestState_")

if (-not $SeedState.stateIds) { $SeedState.stateIds = @{} }

foreach ($name in $stateNames) {
    # Idempotency: check if already exists
    $existing = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/states?filterText=$([uri]::EscapeDataString($name))&maxResultCount=5" -Token $Token
    $match = $existing.items | Where-Object { $_.name -eq $name }

    if ($match) {
        Write-Host "  [SKIP] State '$name' already exists (ID: $($match.id))" -ForegroundColor DarkGray
        $SeedState.stateIds[$name] = $match.id
    } else {
        $body = @{ name = $name }
        $result = Invoke-ApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/states" -Body $body -Token $Token
        Write-Host "  [CREATE] State '$name' (ID: $($result.id))" -ForegroundColor Green
        $SeedState.stateIds[$name] = $result.id
    }
}

Write-Host "  States complete: $($SeedState.stateIds.Count) records" -ForegroundColor White

# ---- A1.2 APPOINTMENT TYPES (6 records) ----

Write-Host "`n--- A1.2 Appointment Types ---" -ForegroundColor Yellow

$appointmentTypes = @(
    @{ Name = "Qualified Medical Examination (QME)"; Description = "State-assigned panel evaluation for unrepresented injured workers" },
    @{ Name = "Agreed Medical Examination (AME)"; Description = "Mutually agreed evaluation when worker has attorney representation" },
    @{ Name = "Supplemental Medical Report"; Description = "Follow-up examination for additional findings or updated medical status" },
    @{ Name = "Deposition"; Description = "Sworn testimony by the evaluating physician regarding examination findings" },
    @{ Name = "Record Review"; Description = "Review of medical records without a physical examination" },
    @{ Name = (New-MaxLengthString -Length 100 -Prefix "TestType_"); Description = (New-MaxLengthString -Length 200 -Prefix "TestDesc_") }
)

if (-not $SeedState.appointmentTypeIds) { $SeedState.appointmentTypeIds = @{} }

foreach ($type in $appointmentTypes) {
    $existing = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/appointment-types?filterText=$([uri]::EscapeDataString($type.Name))&maxResultCount=5" -Token $Token
    $match = $existing.items | Where-Object { $_.name -eq $type.Name }

    if ($match) {
        Write-Host "  [SKIP] Type '$($type.Name.Substring(0, [Math]::Min(50, $type.Name.Length)))...' already exists" -ForegroundColor DarkGray
        $SeedState.appointmentTypeIds[$type.Name] = $match.id
    } else {
        $body = @{ name = $type.Name; description = $type.Description }
        $result = Invoke-ApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/appointment-types" -Body $body -Token $Token
        Write-Host "  [CREATE] Type '$($type.Name.Substring(0, [Math]::Min(50, $type.Name.Length)))...'" -ForegroundColor Green
        $SeedState.appointmentTypeIds[$type.Name] = $result.id
    }
}

Write-Host "  Appointment Types complete: $($SeedState.appointmentTypeIds.Count) records" -ForegroundColor White

# ---- A1.3 APPOINTMENT STATUSES (13 records - one per enum value) ----

Write-Host "`n--- A1.3 Appointment Statuses ---" -ForegroundColor Yellow

$appointmentStatuses = @(
    "Pending", "Approved", "Rejected", "NoShow", "CancelledNoBill",
    "CancelledLate", "RescheduledNoBill", "RescheduledLate", "CheckedIn",
    "CheckedOut", "Billed", "RescheduleRequested", "CancellationRequested"
)

if (-not $SeedState.appointmentStatusIds) { $SeedState.appointmentStatusIds = @{} }

foreach ($statusName in $appointmentStatuses) {
    $existing = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/appointment-statuses?filterText=$([uri]::EscapeDataString($statusName))&maxResultCount=5" -Token $Token
    $match = $existing.items | Where-Object { $_.name -eq $statusName }

    if ($match) {
        Write-Host "  [SKIP] Status '$statusName' already exists" -ForegroundColor DarkGray
        $SeedState.appointmentStatusIds[$statusName] = $match.id
    } else {
        $body = @{ name = $statusName }
        $result = Invoke-ApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/appointment-statuses" -Body $body -Token $Token
        Write-Host "  [CREATE] Status '$statusName'" -ForegroundColor Green
        $SeedState.appointmentStatusIds[$statusName] = $result.id
    }
}

Write-Host "  Appointment Statuses complete: $($SeedState.appointmentStatusIds.Count) records" -ForegroundColor White

# ---- A1.4 APPOINTMENT LANGUAGES (12 records) ----

Write-Host "`n--- A1.4 Appointment Languages ---" -ForegroundColor Yellow

# Always include English, then random 10 from pool
$languages = @("English")
$otherLanguages = $script:LanguagePool | Where-Object { $_ -ne "English" } | Get-Random -Count 10
$languages += $otherLanguages
# Max-length boundary
$languages += (New-MaxLengthString -Length 50 -Prefix "TestLang_")

if (-not $SeedState.appointmentLanguageIds) { $SeedState.appointmentLanguageIds = @{} }

foreach ($lang in $languages) {
    $existing = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/appointment-languages?filterText=$([uri]::EscapeDataString($lang))&maxResultCount=5" -Token $Token
    $match = $existing.items | Where-Object { $_.name -eq $lang }

    if ($match) {
        Write-Host "  [SKIP] Language '$lang' already exists" -ForegroundColor DarkGray
        $SeedState.appointmentLanguageIds[$lang] = $match.id
    } else {
        $body = @{ name = $lang }
        $result = Invoke-ApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/appointment-languages" -Body $body -Token $Token
        Write-Host "  [CREATE] Language '$lang'" -ForegroundColor Green
        $SeedState.appointmentLanguageIds[$lang] = $result.id
    }
}

Write-Host "  Appointment Languages complete: $($SeedState.appointmentLanguageIds.Count) records" -ForegroundColor White

Write-Host "`nPhase A1 COMPLETE" -ForegroundColor Green
Write-Host "  States: $($SeedState.stateIds.Count)"
Write-Host "  Types: $($SeedState.appointmentTypeIds.Count)"
Write-Host "  Statuses: $($SeedState.appointmentStatusIds.Count)"
Write-Host "  Languages: $($SeedState.appointmentLanguageIds.Count)"

return $SeedState
