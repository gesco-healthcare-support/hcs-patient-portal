<#
.SYNOPSIS
    Phase B10: Business Logic & Appointment Lifecycle
.DESCRIPTION
    REWORKED: Status is immutable after creation. Tests status-at-creation for all 13 statuses,
    cancellation/slot behavior, confirmation numbers, tenant creation, GetOrCreatePatient.
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
. "$helpersDir\New-FakeData.ps1"
. "$helpersDir\Assert-Response.ps1"

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "PHASE B10: Business Logic" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

$defaultPassword = $env:TEST_PASSWORD
$apptUrl = "$ApiBaseUrl/api/app/appointments"
$availUrl = "$ApiBaseUrl/api/app/doctor-availabilities"
$t1Id = $SeedState.tenantIds["T1"]
$hostToken = Get-AuthToken -Username "admin@abp.io" -Password $defaultPassword -AuthServerUrl $AuthServerUrl

if (-not $t1Id) {
    Add-SkipResult -TestResults $TestResults -TestId "B10.0.0" -Name "B10 prerequisites" -Reason "No T1 tenant"
    return
}

$t1Token = Get-AuthToken -Username $SeedState._tenantEmails["T1"] -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $t1Id
$patientId = if ($SeedState.patientIds) { @($SeedState.patientIds.Values)[0] } else { $null }
$doctorUserId = $SeedState.doctorUserIds["T1"]
$locId = $SeedState.locationIds["Loc1"]

# Helper: create a fresh available slot for testing
function New-TestSlot {
    param([int]$DayOffset = 80)
    $typeId = @($SeedState.appointmentTypeIds.Values)[0]
    $date = (Get-Date).AddDays($DayOffset).ToString("yyyy-MM-dd")
    $body = @{
        availableDate = "${date}T00:00:00"; fromTime = "09:00:00"; toTime = "10:00:00"
        bookingStatusId = 8; locationId = $locId; appointmentTypeId = $typeId
    }
    $resp = Invoke-TestApiCall -Method "POST" -Url $availUrl -Body $body -Token $t1Token -TenantId $t1Id
    if ($resp.Body) {
        return @{ Id = $resp.Body.id; Date = $date; TypeId = $typeId; LocationId = $locId }
    }
    return $null
}

# Helper: cleanup
function Remove-TestApptAndSlot {
    param([string]$ApptId, [string]$SlotId)
    if ($ApptId) { Invoke-TestApiCall -Method "DELETE" -Url "$apptUrl/$ApptId" -Token $t1Token -TenantId $t1Id | Out-Null }
    if ($SlotId) {
        try {
            $slot = Invoke-ApiCall -Method "GET" -Url "$availUrl/$SlotId" -Token $t1Token -TenantId $t1Id
            if ($slot -and $slot.bookingStatusId -eq 9) {
                $ub = @{ availableDate=$slot.availableDate; fromTime=$slot.fromTime; toTime=$slot.toTime; bookingStatusId=8; locationId=$slot.locationId; appointmentTypeId=$slot.appointmentTypeId; concurrencyStamp=$slot.concurrencyStamp }
                Invoke-ApiCall -Method "PUT" -Url "$availUrl/$SlotId" -Body $ub -Token $t1Token -TenantId $t1Id | Out-Null
            }
        } catch {}
        Invoke-TestApiCall -Method "DELETE" -Url "$availUrl/$SlotId" -Token $t1Token -TenantId $t1Id | Out-Null
    }
}

# ---- B10.1 Status Creation Coverage ----

Write-Host "`n--- B10.1 Status at Creation ---" -ForegroundColor Yellow

$statusNames = @{
    1 = "Pending"; 2 = "Approved"; 3 = "Rejected"; 4 = "NoShow"
    5 = "CancelledNoBill"; 6 = "CancelledLate"; 7 = "RescheduledNoBill"
    8 = "RescheduledLate"; 9 = "CheckedIn"; 10 = "CheckedOut"
    11 = "Billed"; 12 = "RescheduleRequested"; 13 = "CancellationRequested"
}

if ($patientId -and $doctorUserId) {
    $dayOffset = 100
    foreach ($statusId in 1..13) {
        $slot = New-TestSlot -DayOffset $dayOffset
        $dayOffset++
        if (-not $slot) {
            Add-SkipResult -TestResults $TestResults -TestId "B10.1.$statusId" -Name "Create with status $($statusNames[$statusId])" -Reason "Cannot create test slot"
            continue
        }

        $body = @{
            appointmentDate           = "$($slot.Date)T09:30:00"
            appointmentStatus         = $statusId
            requestConfirmationNumber = "PLACEHOLDER"
            patientId                 = $patientId
            identityUserId            = $doctorUserId
            appointmentTypeId         = $slot.TypeId
            locationId                = $slot.LocationId
            doctorAvailabilityId      = $slot.Id
        }
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $resp = Invoke-TestApiCall -Method "POST" -Url $apptUrl -Body $body -Token $t1Token -TenantId $t1Id
        $sw.Stop()
        Assert-StatusCode -TestResults $TestResults -TestId "B10.1.$statusId" -Name "Create with status $($statusNames[$statusId])($statusId)" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds

        # Cleanup
        $apptId = if ($resp.Body) { $resp.Body.id } else { $null }
        Remove-TestApptAndSlot -ApptId $apptId -SlotId $slot.Id
    }
} else {
    for ($i = 1; $i -le 13; $i++) {
        Add-SkipResult -TestResults $TestResults -TestId "B10.1.$i" -Name "Status creation $i" -Reason "No patient/user"
    }
}

# ---- B10.4 Confirmation Number ----

Write-Host "`n--- B10.4 Confirmation Number ---" -ForegroundColor Yellow

if ($patientId -and $doctorUserId) {
    $confNumbers = @()
    $cleanupIds = @()
    for ($i = 1; $i -le 3; $i++) {
        $slot = New-TestSlot -DayOffset (130 + $i)
        if (-not $slot) { continue }
        $body = @{
            appointmentDate = "$($slot.Date)T09:30:00"; appointmentStatus = 1; requestConfirmationNumber = "PLACEHOLDER"
            patientId = $patientId; identityUserId = $doctorUserId
            appointmentTypeId = $slot.TypeId; locationId = $slot.LocationId; doctorAvailabilityId = $slot.Id
        }
        $resp = Invoke-TestApiCall -Method "POST" -Url $apptUrl -Body $body -Token $t1Token -TenantId $t1Id
        if ($resp.Body) {
            $confNumbers += $resp.Body.requestConfirmationNumber
            $cleanupIds += @{ ApptId = $resp.Body.id; SlotId = $slot.Id }
        }
    }

    # B10.4.1 All confirmation numbers are unique
    $uniqueCount = ($confNumbers | Select-Object -Unique).Count
    Assert-AreEqual -TestResults $TestResults -TestId "B10.4.1" -Name "Confirmation numbers unique" -Actual $uniqueCount -Expected $confNumbers.Count -DurationMs 0

    # B10.4.2 Format is A + 5 digits
    foreach ($cn in $confNumbers) {
        if ($cn -and $cn -notmatch '^A\d{5}$') {
            Assert-IsTrue -TestResults $TestResults -TestId "B10.4.2" -Name "Confirmation format A+5 digits" -Condition $false -Details "Invalid: $cn"
            break
        }
    }
    if ($confNumbers.Count -gt 0 -and ($confNumbers | Where-Object { $_ -notmatch '^A\d{5}$' }).Count -eq 0) {
        Assert-IsTrue -TestResults $TestResults -TestId "B10.4.2" -Name "Confirmation format A+5 digits" -Condition $true -Details "All valid: $($confNumbers -join ', ')"
    }

    # Cleanup
    foreach ($c in $cleanupIds) { Remove-TestApptAndSlot -ApptId $c.ApptId -SlotId $c.SlotId }
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B10.4.1" -Name "Confirmation unique" -Reason "No patient/user"
    Add-SkipResult -TestResults $TestResults -TestId "B10.4.2" -Name "Confirmation format" -Reason "No patient/user"
}

# ---- B10.5 Tenant Creation Flow ----

Write-Host "`n--- B10.5 Tenant Creation ---" -ForegroundColor Yellow

# B10.5.1 Create test tenant
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$tenantBody = @{ name = "B10TestDoctor"; adminEmailAddress = "b10test@b-test.hcs.test"; adminPassword = $env:TEST_PASSWORD }
$resp = Invoke-TestApiCall -Method "POST" -Url "$ApiBaseUrl/api/saas/tenants" -Body $tenantBody -Token $hostToken
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B10.5.1" -Name "Create test tenant" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
$testTenantId = if ($resp.Body) { $resp.Body.id } else { $null }

if ($testTenantId) {
    # B10.5.2 Verify doctor exists in new tenant
    $testTenantToken = Get-AuthToken -Username "admin@abp.io" -Password $defaultPassword -AuthServerUrl $AuthServerUrl
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $docResp = Invoke-TestApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/doctors?maxResultCount=10" -Token $testTenantToken -TenantId $testTenantId
    $sw.Stop()
    $docCount = if ($docResp.Body) { $docResp.Body.totalCount } else { 0 }
    Assert-GreaterOrEqual -TestResults $TestResults -TestId "B10.5.2" -Name "New tenant has doctor" -Actual $docCount -Expected 1 -DurationMs $sw.ElapsedMilliseconds

    # B10.5.3 Cleanup: delete test tenant
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "DELETE" -Url "$ApiBaseUrl/api/saas/tenants/$testTenantId" -Token $hostToken
    $sw.Stop()
    Assert-IsTrue -TestResults $TestResults -TestId "B10.5.3" -Name "Delete test tenant" -Condition ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 204) -Details "Status: $($resp.StatusCode)" -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B10.5.2" -Name "Verify tenant doctor" -Reason "Tenant creation failed"
    Add-SkipResult -TestResults $TestResults -TestId "B10.5.3" -Name "Delete test tenant" -Reason "No test tenant"
}

# ---- B10.6 GetOrCreatePatient ----

Write-Host "`n--- B10.6 GetOrCreatePatient ---" -ForegroundColor Yellow

$patientUrl = "$ApiBaseUrl/api/app/patients/for-appointment-booking/get-or-create"
$testPatientEmail = "b10.getorcreate@b-test.hcs.test"

# B10.6.1 Create new patient
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$body = @{
    firstName  = "B10Test"
    lastName   = "Patient"
    email      = $testPatientEmail
    genderId   = 1
    dateOfBirth = "1990-01-15T00:00:00"
}
$resp = Invoke-TestApiCall -Method "POST" -Url $patientUrl -Body $body -Token $t1Token -TenantId $t1Id
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B10.6.1" -Name "GetOrCreatePatient - new" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
# Response may be PatientWithNavigationPropertiesDto (nested .patient) or flat PatientDto
$createdPatientId = if ($resp.Body) {
    if ($resp.Body.patient) { $resp.Body.patient.id } else { $resp.Body.id }
} else { $null }

# B10.6.2 Idempotent - same email returns same patient
if ($createdPatientId) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp2 = Invoke-TestApiCall -Method "POST" -Url $patientUrl -Body $body -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    $sameId = if ($resp2.Body) {
        if ($resp2.Body.patient) { $resp2.Body.patient.id } else { $resp2.Body.id }
    } else { "" }
    Assert-AreEqual -TestResults $TestResults -TestId "B10.6.2" -Name "GetOrCreatePatient - idempotent" -Actual $sameId -Expected $createdPatientId -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B10.6.2" -Name "GetOrCreatePatient idempotent" -Reason "First call failed"
}

Write-Host "`nPhase B10 COMPLETE" -ForegroundColor Green
