<#
.SYNOPSIS
    Removes all data created by Master-Seed.ps1 in strict reverse tier order.
.DESCRIPTION
    Reads seed-state.json and deletes records from Tier 6 down to Tier 1.
    After deleting appointments, PUTs linked slots back to Available(8) to work around
    the known slot-release gap (AppointmentsAppService.DeleteAsync doesn't release slots).
    Handles 404s gracefully (already deleted = not an error).
.EXAMPLE
    .\Remove-SeedData.ps1
    .\Remove-SeedData.ps1 -KeepStateFile
#>
param(
    [string]$ApiBaseUrl = "https://localhost:44327",
    [string]$AuthServerUrl = "https://localhost:44368",
    [switch]$KeepStateFile
)

$ErrorActionPreference = "Stop"

$scriptRoot = $PSScriptRoot
$helpersDir = Join-Path $scriptRoot "helpers"
$stateFile = Join-Path $scriptRoot "seed-state.json"

# Load helpers
. "$helpersDir\Get-AuthToken.ps1"
. "$helpersDir\Invoke-ApiCall.ps1"

Write-Host @"

  ╔══════════════════════════════════════════════════╗
  ║  HCS Case Evaluation Portal - Data Cleaner      ║
  ║  Removes all seeded data in reverse order        ║
  ╚══════════════════════════════════════════════════╝

"@ -ForegroundColor Yellow

if (-not (Test-Path $stateFile)) {
    Write-Host "No seed-state.json found at $stateFile. Nothing to clean." -ForegroundColor Yellow
    exit 0
}

# PS 5.x compatible: ConvertFrom-Json returns PSCustomObject; convert recursively to hashtable
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

$seedState = Get-Content $stateFile -Raw | ConvertFrom-Json | ConvertTo-Hashtable
$defaultPassword = $env:TEST_PASSWORD

$hostToken = Get-AuthToken -Username "admin@abp.io" -Password $env:TEST_PASSWORD -AuthServerUrl $AuthServerUrl

$deletedCount = 0
$errorCount = 0

function Remove-Entity {
    param(
        [string]$Label,
        [string]$Url,
        [string]$Token,
        [string]$TenantId = ""
    )

    try {
        Invoke-ApiCall -Method "DELETE" -Url $Url -Token $Token -TenantId $TenantId | Out-Null
        $script:deletedCount++
        Write-Host "    [DEL] $Label" -ForegroundColor DarkGray
        return $true
    } catch {
        if ($_.ToString() -match "404") {
            Write-Host "    [404] $Label (already deleted)" -ForegroundColor DarkYellow
            return $true
        }
        $script:errorCount++
        Write-Host "    [ERR] $Label : $_" -ForegroundColor Red
        return $false
    }
}

$startTime = Get-Date

# ---- TIER 6: Appointment-level child entities ----

Write-Host "`n--- Tier 6: Appointment Child Entities ---" -ForegroundColor Yellow

# 6a. AppointmentApplicantAttorneys (links - deleted by deleting the appointment usually, but clean up explicitly)
# These are managed via the upsert endpoint; no direct delete API exists for the link.
# They'll be cascade-cleaned when appointments are deleted.
Write-Host "  AppointmentApplicantAttorney links: will be removed with appointments" -ForegroundColor DarkGray

# 6b. AppointmentAccessors
if ($seedState.appointmentAccessorIds) {
    Write-Host "  Deleting $($seedState.appointmentAccessorIds.Count) accessors..." -ForegroundColor White
    foreach ($accId in $seedState.appointmentAccessorIds) {
        # Need to figure out which tenant this belongs to - try with host token first
        Remove-Entity -Label "Accessor $accId" -Url "$ApiBaseUrl/api/app/appointment-accessors/$accId" -Token $hostToken
    }
}

# 6c. AppointmentEmployerDetails
if ($seedState.appointmentEmployerDetailIds) {
    Write-Host "  Deleting $($seedState.appointmentEmployerDetailIds.Count) employer details..." -ForegroundColor White
    foreach ($key in @($seedState.appointmentEmployerDetailIds.Keys)) {
        $detailId = $seedState.appointmentEmployerDetailIds[$key]
        # Determine tenant from key
        $tenantKey = if ($key -like "T*_*") { $key.Split("_")[0] } else { "T1" }
        $tenantId = $seedState.tenantIds[$tenantKey]
        $tenantEmail = $seedState._tenantEmails[$tenantKey]
        $tenantToken = Get-AuthToken -Username $tenantEmail -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $tenantId
        Remove-Entity -Label "EmployerDetail $key" -Url "$ApiBaseUrl/api/app/appointment-employer-details/$detailId" -Token $tenantToken -TenantId $tenantId
    }
}

# 6d. ApplicantAttorney entities
if ($seedState.applicantAttorneyEntityIds) {
    Write-Host "  Deleting $($seedState.applicantAttorneyEntityIds.Count) applicant attorneys..." -ForegroundColor White
    foreach ($key in @($seedState.applicantAttorneyEntityIds.Keys)) {
        $aaId = $seedState.applicantAttorneyEntityIds[$key]
        $tenantKey = $key.Split("_")[0]
        $tenantId = $seedState.tenantIds[$tenantKey]
        $tenantEmail = $seedState._tenantEmails[$tenantKey]
        $tenantToken = Get-AuthToken -Username $tenantEmail -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $tenantId
        Remove-Entity -Label "ApplicantAttorney $key" -Url "$ApiBaseUrl/api/app/applicant-attorneys/$aaId" -Token $tenantToken -TenantId $tenantId
    }
}

# ---- TIER 5: Appointments ----

Write-Host "`n--- Tier 5: Appointments ---" -ForegroundColor Yellow

if ($seedState.appointmentIds) {
    Write-Host "  Deleting $($seedState.appointmentIds.Count) appointments..." -ForegroundColor White
    foreach ($key in @($seedState.appointmentIds.Keys)) {
        $apptId = $seedState.appointmentIds[$key]
        $tenantKey = $key.Split("_")[0]
        $tenantId = $seedState.tenantIds[$tenantKey]
        $tenantEmail = $seedState._tenantEmails[$tenantKey]
        $tenantToken = Get-AuthToken -Username $tenantEmail -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $tenantId

        Remove-Entity -Label "Appointment $key" -Url "$ApiBaseUrl/api/app/appointments/$apptId" -Token $tenantToken -TenantId $tenantId

        # WORKAROUND: Release the booked slot back to Available(8)
        # Known gap: DeleteAsync doesn't release slots
        $slotId = $seedState._appointmentSlotMap[$apptId]
        if ($slotId) {
            try {
                $slot = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/doctor-availabilities/$slotId" -Token $tenantToken -TenantId $tenantId
                if ($slot -and $slot.bookingStatusId -eq 9) {  # Still Booked
                    $updateBody = @{
                        availableDate     = $slot.availableDate
                        fromTime          = $slot.fromTime
                        toTime            = $slot.toTime
                        bookingStatusId   = 8  # Available
                        locationId        = $slot.locationId
                        appointmentTypeId = $slot.appointmentTypeId
                        concurrencyStamp  = $slot.concurrencyStamp
                    }
                    Invoke-ApiCall -Method "PUT" -Url "$ApiBaseUrl/api/app/doctor-availabilities/$slotId" -Body $updateBody -Token $tenantToken -TenantId $tenantId | Out-Null
                    Write-Host "    [FIX] Slot $slotId released back to Available" -ForegroundColor DarkCyan
                }
            } catch {
                Write-Host "    [WARN] Could not release slot $slotId : $_" -ForegroundColor Yellow
            }
        }
    }
}

# ---- TIER 4: DoctorAvailabilities ----

Write-Host "`n--- Tier 4: Doctor Availabilities ---" -ForegroundColor Yellow

if ($seedState.availabilitySlotIds) {
    Write-Host "  Deleting $($seedState.availabilitySlotIds.Count) slots..." -ForegroundColor White
    foreach ($key in @($seedState.availabilitySlotIds.Keys)) {
        $slotId = $seedState.availabilitySlotIds[$key]
        $tenantKey = $key.Split("_")[0]
        $tenantId = $seedState.tenantIds[$tenantKey]
        $tenantEmail = $seedState._tenantEmails[$tenantKey]
        $tenantToken = Get-AuthToken -Username $tenantEmail -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $tenantId
        Remove-Entity -Label "Slot $key" -Url "$ApiBaseUrl/api/app/doctor-availabilities/$slotId" -Token $tenantToken -TenantId $tenantId
    }
}

# ---- TIER 3: Tenants (cascades tenant-scoped data), Patients ----

Write-Host "`n--- Tier 3: Tenants & Patients ---" -ForegroundColor Yellow

# Delete tenants (this cascades all tenant-scoped data in ABP)
if ($seedState.tenantIds) {
    Write-Host "  Deleting $($seedState.tenantIds.Count) tenants..." -ForegroundColor White
    foreach ($key in @($seedState.tenantIds.Keys)) {
        $tenantId = $seedState.tenantIds[$key]
        Remove-Entity -Label "Tenant $key" -Url "$ApiBaseUrl/api/saas/tenants/$tenantId" -Token $hostToken
    }
}

# Delete patients (host-scoped entities)
if ($seedState.patientIds) {
    Write-Host "  Deleting $($seedState.patientIds.Count) patients..." -ForegroundColor White
    foreach ($key in @($seedState.patientIds.Keys)) {
        $patientId = $seedState.patientIds[$key]
        Remove-Entity -Label "Patient $key" -Url "$ApiBaseUrl/api/app/patients/$patientId" -Token $hostToken
    }
}

# ---- TIER 2: Locations & WCAB Offices ----

Write-Host "`n--- Tier 2: Locations & WCAB Offices ---" -ForegroundColor Yellow

if ($seedState.locationIds) {
    Write-Host "  Deleting $($seedState.locationIds.Count) locations..." -ForegroundColor White
    foreach ($key in @($seedState.locationIds.Keys)) {
        $locId = $seedState.locationIds[$key]
        Remove-Entity -Label "Location $key" -Url "$ApiBaseUrl/api/app/locations/$locId" -Token $hostToken
    }
}

if ($seedState.wcabIds) {
    Write-Host "  Deleting $($seedState.wcabIds.Count) WCAB offices..." -ForegroundColor White
    foreach ($key in @($seedState.wcabIds.Keys)) {
        $wcabId = $seedState.wcabIds[$key]
        Remove-Entity -Label "WCAB $key" -Url "$ApiBaseUrl/api/app/wcab-offices/$wcabId" -Token $hostToken
    }
}

# ---- TIER 1: Reference Data ----

Write-Host "`n--- Tier 1: Reference Data ---" -ForegroundColor Yellow

if ($seedState.appointmentLanguageIds) {
    Write-Host "  Deleting $($seedState.appointmentLanguageIds.Count) languages..." -ForegroundColor White
    foreach ($key in @($seedState.appointmentLanguageIds.Keys)) {
        $langId = $seedState.appointmentLanguageIds[$key]
        Remove-Entity -Label "Language $key" -Url "$ApiBaseUrl/api/app/appointment-languages/$langId" -Token $hostToken
    }
}

if ($seedState.appointmentStatusIds) {
    Write-Host "  Deleting $($seedState.appointmentStatusIds.Count) statuses..." -ForegroundColor White
    foreach ($key in @($seedState.appointmentStatusIds.Keys)) {
        $statusId = $seedState.appointmentStatusIds[$key]
        Remove-Entity -Label "Status $key" -Url "$ApiBaseUrl/api/app/appointment-statuses/$statusId" -Token $hostToken
    }
}

if ($seedState.appointmentTypeIds) {
    Write-Host "  Deleting $($seedState.appointmentTypeIds.Count) types..." -ForegroundColor White
    foreach ($key in @($seedState.appointmentTypeIds.Keys)) {
        $typeId = $seedState.appointmentTypeIds[$key]
        Remove-Entity -Label "Type $key" -Url "$ApiBaseUrl/api/app/appointment-types/$typeId" -Token $hostToken
    }
}

if ($seedState.stateIds) {
    Write-Host "  Deleting $($seedState.stateIds.Count) states..." -ForegroundColor White
    foreach ($key in @($seedState.stateIds.Keys)) {
        $stateId = $seedState.stateIds[$key]
        Remove-Entity -Label "State $key" -Url "$ApiBaseUrl/api/app/states/$stateId" -Token $hostToken
    }
}

# ---- Delete seed state file ----

if (-not $KeepStateFile) {
    Remove-Item $stateFile -Force -ErrorAction SilentlyContinue
    Write-Host "`nDeleted seed-state.json" -ForegroundColor Yellow
}

# ---- Summary ----

$totalTime = (Get-Date) - $startTime

Write-Host @"

  ╔══════════════════════════════════════════════════╗
  ║  CLEANUP COMPLETE                                ║
  ╠══════════════════════════════════════════════════╣
  ║  Deleted: $deletedCount records
  ║  Errors:  $errorCount
  ║  Time:    $($totalTime.TotalSeconds.ToString('F1'))s
  ╚══════════════════════════════════════════════════╝

"@ -ForegroundColor $(if ($errorCount -eq 0) { "Green" } else { "Yellow" })
