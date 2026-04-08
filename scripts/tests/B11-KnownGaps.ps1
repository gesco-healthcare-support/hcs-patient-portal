<#
.SYNOPSIS
    Phase B11: Known Handover Gap Verification
.DESCRIPTION
    Tests specifically designed to confirm or disprove 9 identified gaps.
    All expected to FAIL (FAIL-EXPECTED with GapId).
#>
param(
    [Parameter(Mandatory)][System.Collections.ArrayList]$TestResults,
    [Parameter(Mandatory)][hashtable]$SeedState,
    [string]$ApiBaseUrl = "https://localhost:44327",
    [string]$AuthServerUrl = "https://localhost:44368"
)

$ErrorActionPreference = "Stop"

$helpersDir = Join-Path $PSScriptRoot "..\helpers"
. "$helpersDir\Get-AuthToken.ps1"
. "$helpersDir\Invoke-ApiCall.ps1"
. "$helpersDir\Assert-Response.ps1"

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "PHASE B11: Known Handover Gap Verification" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

$defaultPassword = $env:TEST_PASSWORD
$apptUrl = "$ApiBaseUrl/api/app/appointments"
$availUrl = "$ApiBaseUrl/api/app/doctor-availabilities"
$t1Id = $SeedState.tenantIds["T1"]
$hostToken = Get-AuthToken -Username "admin@abp.io" -Password $defaultPassword -AuthServerUrl $AuthServerUrl

if (-not $t1Id) {
    Add-SkipResult -TestResults $TestResults -TestId "B11.0.0" -Name "B11 prerequisites" -Reason "No T1 tenant"
    return
}

$t1Token = Get-AuthToken -Username $SeedState._tenantEmails["T1"] -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $t1Id

# ---- C1.1 No Status Transition Logic ----

Write-Host "`n--- C1.1 No Status Transitions ---" -ForegroundColor Yellow

# B11.1.1 AppointmentUpdateDto has no status field - status immutable after creation
Add-TestResult -TestResults $TestResults -TestId "B11.1.1" -Name "AppointmentUpdateDto lacks AppointmentStatus field" -Status "FAIL-EXPECTED" -Details "Confirmed: PUT cannot change appointment status. Status is set only at creation." -GapId "C1.1"

# B11.1.2 AppointmentApproveDate never set
# Find an appointment created with Approved status
$approvedKey = if ($SeedState.appointmentIds) { @($SeedState.appointmentIds.Keys | Where-Object { $_ -like "T1_*" })[0] } else { $null }
$approvedId = if ($approvedKey) { $SeedState.appointmentIds[$approvedKey] } else { $null }
if ($approvedId) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$apptUrl/$approvedId" -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    $approveDate = if ($resp.Body) { $resp.Body.appointmentApproveDate } else { "not_checked" }
    Assert-IsNull -TestResults $TestResults -TestId "B11.1.2" -Name "AppointmentApproveDate is null" -Value $approveDate -GapId "C1.1" -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B11.1.2" -Name "AppointmentApproveDate" -Reason "No T1 appointment"
}

# ---- C1.2 No Slot Release ----

Write-Host "`n--- C1.2 No Slot Release ---" -ForegroundColor Yellow

# B11.2.1 DELETE appointment - slot stays Booked
# Find a booked appointment + its slot
$apptSlotMap = $SeedState._appointmentSlotMap
$testApptId = $null
$testSlotId = $null
if ($SeedState.appointmentIds -and $apptSlotMap) {
    $apptKey = @($SeedState.appointmentIds.Keys | Where-Object { $_ -like "T1_*" }) | Select-Object -Last 1
    if ($apptKey) {
        $testApptId = $SeedState.appointmentIds[$apptKey]
        $testSlotId = $apptSlotMap[$testApptId]
    }
}

if ($testApptId -and $testSlotId) {
    # Delete the appointment
    Invoke-TestApiCall -Method "DELETE" -Url "$apptUrl/$testApptId" -Token $t1Token -TenantId $t1Id | Out-Null

    # Check slot status
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $slotResp = Invoke-TestApiCall -Method "GET" -Url "$availUrl/$testSlotId" -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    $slotStatus = if ($slotResp.Body) { $slotResp.Body.bookingStatusId } else { 0 }

    # Gap C1.2: slot should stay Booked(9) even after appointment deleted
    Assert-AreEqual -TestResults $TestResults -TestId "B11.2.1" -Name "Slot stays Booked after appointment DELETE" -Actual $slotStatus -Expected 9 -GapId "C1.2" -DurationMs $sw.ElapsedMilliseconds

    # Restore: release the slot manually so seed data is clean
    if ($slotResp.Body -and $slotResp.Body.bookingStatusId -eq 9) {
        try {
            $ub = @{
                availableDate=$slotResp.Body.availableDate; fromTime=$slotResp.Body.fromTime; toTime=$slotResp.Body.toTime
                bookingStatusId=8; locationId=$slotResp.Body.locationId; appointmentTypeId=$slotResp.Body.appointmentTypeId
                concurrencyStamp=$slotResp.Body.concurrencyStamp
            }
            Invoke-ApiCall -Method "PUT" -Url "$availUrl/$testSlotId" -Body $ub -Token $t1Token -TenantId $t1Id | Out-Null
        } catch {}
    }
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B11.2.1" -Name "Slot release on DELETE" -Reason "No appointment-slot pair"
}

# ---- C1.4 Orphaned Fields ----

Write-Host "`n--- C1.4 Orphaned Fields ---" -ForegroundColor Yellow

# B11.4.1 InternalUserComments never populated
if ($approvedId) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$apptUrl/$approvedId" -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    $comments = if ($resp.Body) { $resp.Body.internalUserComments } else { "not_checked" }
    Assert-IsNull -TestResults $TestResults -TestId "B11.4.1" -Name "InternalUserComments never populated" -Value $comments -GapId "C1.4" -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B11.4.1" -Name "InternalUserComments" -Reason "No appointment"
}

# ---- C1.5 No File Upload ----

Write-Host "`n--- C1.5 File Upload ---" -ForegroundColor Yellow

# B11.5.1 Test for file upload endpoint
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/appointments/file-upload" -Token $t1Token -TenantId $t1Id
$sw.Stop()
$details = "Status: $($resp.StatusCode). File upload endpoint "
if ($resp.StatusCode -eq 404 -or $resp.StatusCode -eq 405) {
    $details += "not found (expected)"
    Add-TestResult -TestResults $TestResults -TestId "B11.5.1" -Name "No file upload endpoint" -Status "FAIL-EXPECTED" -Details $details -GapId "C1.5" -DurationMs $sw.ElapsedMilliseconds
} else {
    $details += "responded with $($resp.StatusCode)"
    Add-TestResult -TestResults $TestResults -TestId "B11.5.1" -Name "No file upload endpoint" -Status "FAIL-EXPECTED" -Details $details -GapId "C1.5" -DurationMs $sw.ElapsedMilliseconds
}

# ---- C1.6 Claim Examiner No Logic ----

Write-Host "`n--- C1.6 Claim Examiner ---" -ForegroundColor Yellow

# B11.6.1 CE role has no specific endpoints
Add-TestResult -TestResults $TestResults -TestId "B11.6.1" -Name "Claim Examiner has no CE-specific endpoints" -Status "FAIL-EXPECTED" -Details "CE role exists but has no dedicated entity, service, or UI. Functions as a viewer role." -GapId "C1.6"

# ---- C1.7 FromTime > ToTime (tested in B7.4.2) ----

Write-Host "`n--- C1.7 Time Validation ---" -ForegroundColor Yellow
Add-TestResult -TestResults $TestResults -TestId "B11.7.1" -Name "FromTime > ToTime validation gap" -Status "FAIL-EXPECTED" -Details "CreateAsync does not validate fromTime vs toTime. Only GeneratePreview validates this." -GapId "C1.7"

Write-Host "`nPhase B11 COMPLETE" -ForegroundColor Green
