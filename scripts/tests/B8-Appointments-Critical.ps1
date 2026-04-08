<#
.SYNOPSIS
    Phase B8: Appointment Booking - Critical Path
.DESCRIPTION
    Tests happy path booking, all validation failure cases, list filtering,
    applicant attorney, and appointment update. CORRECTED: no patientId filter, no status via PUT.
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
Write-Host "PHASE B8: Appointment Booking - Critical Path" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

$defaultPassword = $env:TEST_PASSWORD
$apptUrl = "$ApiBaseUrl/api/app/appointments"
$availUrl = "$ApiBaseUrl/api/app/doctor-availabilities"
$t1Id = $SeedState.tenantIds["T1"]

if (-not $t1Id) {
    Add-SkipResult -TestResults $TestResults -TestId "B8.0.0" -Name "B8 prerequisites" -Reason "No T1 tenant"
    return
}

$t1Token = Get-AuthToken -Username $SeedState._tenantEmails["T1"] -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $t1Id
$hostToken = Get-AuthToken -Username "admin@abp.io" -Password $defaultPassword -AuthServerUrl $AuthServerUrl

# Helper: safely format date value (may be DateTime or string from API)
function Format-DateOnly($val) {
    if ($val -is [DateTime]) { return $val.ToString("yyyy-MM-dd") }
    return ([string]$val).Substring(0, 10)
}
function Format-TimeOnly($val) {
    if ($val -is [TimeSpan]) { return $val.ToString("hh\:mm\:ss") }
    return [string]$val
}

# Helper: find an available slot for testing
# NOTE: GET /api/app/doctor-availabilities returns DoctorAvailabilityWithNavigationPropertiesDto
# Each item has .doctorAvailability (the actual slot), .location, .appointmentType
function Find-AvailableSlot {
    param([string]$Token, [string]$TenantId)
    $resp = Invoke-TestApiCall -Method "GET" -Url "$($availUrl)?bookingStatusId=8&maxResultCount=50" -Token $Token -TenantId $TenantId
    if ($resp.Success -and $resp.Body -and $resp.Body.items) {
        foreach ($item in $resp.Body.items) {
            $slot = $item.doctorAvailability
            if (-not $slot) { continue }
            $slotDate = if ($slot.availableDate -is [DateTime]) { $slot.availableDate } else { [DateTime]::Parse($slot.availableDate) }
            if ($slotDate -gt (Get-Date).AddDays(3)) {
                return $slot
            }
        }
    }
    return $null
}

# Helper: cleanup appointment (delete + release slot)
function Remove-TestAppointment {
    param([string]$ApptId, [string]$SlotId, [string]$Token, [string]$TenantId)
    if ($ApptId) {
        Invoke-TestApiCall -Method "DELETE" -Url "$apptUrl/$ApptId" -Token $Token -TenantId $TenantId | Out-Null
    }
    if ($SlotId) {
        try {
            $slot = Invoke-ApiCall -Method "GET" -Url "$availUrl/$SlotId" -Token $Token -TenantId $TenantId
            if ($slot -and $slot.bookingStatusId -eq 9) {
                $updateBody = @{
                    availableDate = $slot.availableDate; fromTime = $slot.fromTime; toTime = $slot.toTime
                    bookingStatusId = 8; locationId = $slot.locationId; appointmentTypeId = $slot.appointmentTypeId
                    concurrencyStamp = $slot.concurrencyStamp
                }
                Invoke-ApiCall -Method "PUT" -Url "$availUrl/$SlotId" -Body $updateBody -Token $Token -TenantId $TenantId | Out-Null
            }
        } catch {}
    }
}

# Get common IDs
$patientId = if ($SeedState.patientIds) { @($SeedState.patientIds.Values)[0] } else { $null }
$doctorUserId = $SeedState.doctorUserIds["T1"]
$typeId = @($SeedState.appointmentTypeIds.Values)[0]

# ---- B8.1 Happy Path ----

Write-Host "`n--- B8.1 Happy Path Booking ---" -ForegroundColor Yellow

$testSlot = Find-AvailableSlot -Token $t1Token -TenantId $t1Id
if ($testSlot -and $patientId -and $doctorUserId) {
    $slotDateStr = Format-DateOnly $testSlot.availableDate
    $slotTimeStr = Format-TimeOnly $testSlot.fromTime
    $apptDate = "${slotDateStr}T${slotTimeStr}"

    $bookBody = @{
        appointmentDate              = $apptDate
        appointmentStatus            = 1  # Pending
        requestConfirmationNumber    = "PLACEHOLDER"
        patientId                    = $patientId
        identityUserId               = $doctorUserId
        appointmentTypeId            = $testSlot.appointmentTypeId
        locationId                   = $testSlot.locationId
        doctorAvailabilityId         = $testSlot.id
    }

    # B8.1.1 POST booking
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "POST" -Url $apptUrl -Body $bookBody -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "B8.1.1" -Name "Appointment POST booking" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
    $testApptId = if ($resp.Body) { $resp.Body.id } else { $null }
    $confNum = if ($resp.Body) { $resp.Body.requestConfirmationNumber } else { "" }

    # B8.1.2 Confirmation number format
    Assert-Match -TestResults $TestResults -TestId "B8.1.2" -Name "Confirmation number format A+5 digits" -Value $confNum -Pattern "^A\d{5}$"

    # B8.1.3 Slot becomes Booked(9)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $slotResp = Invoke-TestApiCall -Method "GET" -Url "$availUrl/$($testSlot.id)" -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    $slotStatus = if ($slotResp.Body) { $slotResp.Body.bookingStatusId } else { 0 }
    Assert-AreEqual -TestResults $TestResults -TestId "B8.1.3" -Name "Slot becomes Booked(9)" -Actual $slotStatus -Expected 9 -DurationMs $sw.ElapsedMilliseconds

    # B8.1.4 GET with navigation properties
    if ($testApptId) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $navResp = Invoke-TestApiCall -Method "GET" -Url "$apptUrl/with-navigation-properties/$testApptId" -Token $t1Token -TenantId $t1Id
        $sw.Stop()
        $hasPatient = ($navResp.Body -and $navResp.Body.patient)
        Assert-IsTrue -TestResults $TestResults -TestId "B8.1.4" -Name "Appointment nav props populated" -Condition $hasPatient -Details "Patient present: $hasPatient" -DurationMs $sw.ElapsedMilliseconds
    }

    # Cleanup
    Remove-TestAppointment -ApptId $testApptId -SlotId $testSlot.id -Token $t1Token -TenantId $t1Id
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B8.1.1" -Name "Happy path booking" -Reason "No available slot or missing patient/user IDs"
    Add-SkipResult -TestResults $TestResults -TestId "B8.1.2" -Name "Confirmation number" -Reason "No booking"
    Add-SkipResult -TestResults $TestResults -TestId "B8.1.3" -Name "Slot status" -Reason "No booking"
    Add-SkipResult -TestResults $TestResults -TestId "B8.1.4" -Name "Nav props" -Reason "No booking"
}

# ---- B8.2 Booking Validation ----

Write-Host "`n--- B8.2 Booking Validation ---" -ForegroundColor Yellow

$validSlot = Find-AvailableSlot -Token $t1Token -TenantId $t1Id
if ($validSlot -and $patientId -and $doctorUserId) {
    $vDateStr = Format-DateOnly $validSlot.availableDate
    $vTimeStr = Format-TimeOnly $validSlot.fromTime
    $validBody = @{
        appointmentDate           = "${vDateStr}T${vTimeStr}"
        appointmentStatus         = 1
        requestConfirmationNumber = "PLACEHOLDER"
        patientId                 = $patientId
        identityUserId            = $doctorUserId
        appointmentTypeId         = $validSlot.appointmentTypeId
        locationId                = $validSlot.locationId
        doctorAvailabilityId      = $validSlot.id
    }
    $fakeGuid = "00000000-0000-0000-0000-000000000001"

    # B8.2.1-5 Empty required GUIDs
    $requiredFields = @(
        @{ Field = "patientId"; TestId = "B8.2.1"; Name = "Empty patientId" },
        @{ Field = "identityUserId"; TestId = "B8.2.2"; Name = "Empty identityUserId" },
        @{ Field = "appointmentTypeId"; TestId = "B8.2.3"; Name = "Empty appointmentTypeId" },
        @{ Field = "locationId"; TestId = "B8.2.4"; Name = "Empty locationId" },
        @{ Field = "doctorAvailabilityId"; TestId = "B8.2.5"; Name = "Empty doctorAvailabilityId" }
    )

    foreach ($rf in $requiredFields) {
        $testBody = @{}
        foreach ($k in $validBody.Keys) { $testBody[$k] = $validBody[$k] }
        $testBody[$rf.Field] = "00000000-0000-0000-0000-000000000000"
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $resp = Invoke-TestApiCall -Method "POST" -Url $apptUrl -Body $testBody -Token $t1Token -TenantId $t1Id
        $sw.Stop()
        Assert-IsTrue -TestResults $TestResults -TestId $rf.TestId -Name "$($rf.Name) returns 4xx" -Condition ($resp.StatusCode -ge 400 -and $resp.StatusCode -lt 500) -Details "Status: $($resp.StatusCode)" -DurationMs $sw.ElapsedMilliseconds
    }

    # B8.2.6-10 Non-existent GUIDs
    $nonExistFields = @(
        @{ Field = "patientId"; TestId = "B8.2.6"; Name = "Non-existent patientId" },
        @{ Field = "identityUserId"; TestId = "B8.2.7"; Name = "Non-existent identityUserId" },
        @{ Field = "appointmentTypeId"; TestId = "B8.2.8"; Name = "Non-existent appointmentTypeId" },
        @{ Field = "locationId"; TestId = "B8.2.9"; Name = "Non-existent locationId" },
        @{ Field = "doctorAvailabilityId"; TestId = "B8.2.10"; Name = "Non-existent doctorAvailabilityId" }
    )

    foreach ($rf in $nonExistFields) {
        $testBody = @{}
        foreach ($k in $validBody.Keys) { $testBody[$k] = $validBody[$k] }
        $testBody[$rf.Field] = $fakeGuid
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $resp = Invoke-TestApiCall -Method "POST" -Url $apptUrl -Body $testBody -Token $t1Token -TenantId $t1Id
        $sw.Stop()
        Assert-IsTrue -TestResults $TestResults -TestId $rf.TestId -Name "$($rf.Name) returns 4xx" -Condition ($resp.StatusCode -ge 400 -and $resp.StatusCode -lt 500) -Details "Status: $($resp.StatusCode)" -DurationMs $sw.ElapsedMilliseconds
    }

    # B8.2.11 Location mismatch
    $otherLocId = $SeedState.locationIds["Loc2"]
    if ($otherLocId -and $otherLocId -ne $validSlot.locationId) {
        $testBody = @{}
        foreach ($k in $validBody.Keys) { $testBody[$k] = $validBody[$k] }
        $testBody["locationId"] = $otherLocId
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $resp = Invoke-TestApiCall -Method "POST" -Url $apptUrl -Body $testBody -Token $t1Token -TenantId $t1Id
        $sw.Stop()
        Assert-IsTrue -TestResults $TestResults -TestId "B8.2.11" -Name "Location mismatch returns 4xx" -Condition ($resp.StatusCode -ge 400 -and $resp.StatusCode -lt 500) -Details "Status: $($resp.StatusCode)" -DurationMs $sw.ElapsedMilliseconds
    } else {
        Add-SkipResult -TestResults $TestResults -TestId "B8.2.11" -Name "Location mismatch" -Reason "No different location"
    }

    # B8.2.12 Date mismatch
    $testBody = @{}
    foreach ($k in $validBody.Keys) { $testBody[$k] = $validBody[$k] }
    $wrongDate = (Get-Date).AddDays(90).ToString("yyyy-MM-dd")
    $testBody["appointmentDate"] = "${wrongDate}T$(Format-TimeOnly $validSlot.fromTime)"
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "POST" -Url $apptUrl -Body $testBody -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    Assert-IsTrue -TestResults $TestResults -TestId "B8.2.12" -Name "Date mismatch returns 4xx" -Condition ($resp.StatusCode -ge 400 -and $resp.StatusCode -lt 500) -Details "Status: $($resp.StatusCode)" -DurationMs $sw.ElapsedMilliseconds

} else {
    for ($i = 1; $i -le 12; $i++) {
        Add-SkipResult -TestResults $TestResults -TestId "B8.2.$i" -Name "Booking validation $i" -Reason "No available slot"
    }
}

# ---- B8.3 Appointment List Filtering ----

Write-Host "`n--- B8.3 List Filtering ---" -ForegroundColor Yellow

# B8.3.1 Filter by identityUserId (doctor)
if ($doctorUserId) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$($apptUrl)?identityUserId=$doctorUserId&maxResultCount=100" -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    $filteredCount = if ($resp.Body) { $resp.Body.totalCount } else { 0 }
    Assert-GreaterOrEqual -TestResults $TestResults -TestId "B8.3.1" -Name "Filter by identityUserId" -Actual $filteredCount -Expected 1 -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B8.3.1" -Name "Filter by identityUserId" -Reason "No doctor user ID"
}

# B8.3.2 Filter by appointmentTypeId
if ($typeId) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$($apptUrl)?appointmentTypeId=$typeId&maxResultCount=100" -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "B8.3.2" -Name "Filter by appointmentTypeId" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
}

# B8.3.3 Filter by date range
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$minDate = (Get-Date).AddDays(-60).ToString("yyyy-MM-dd")
$maxDate = (Get-Date).AddDays(60).ToString("yyyy-MM-dd")
$resp = Invoke-TestApiCall -Method "GET" -Url "$($apptUrl)?appointmentDateMin=$minDate&appointmentDateMax=$maxDate&maxResultCount=100" -Token $t1Token -TenantId $t1Id
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B8.3.3" -Name "Filter by date range" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds

# B8.3.4 Empty T4 context
$t4Id = $SeedState.tenantIds["T4"]
if ($t4Id -and $SeedState._tenantEmails["T4"]) {
    $t4Token = Get-AuthToken -Username $SeedState._tenantEmails["T4"] -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $t4Id
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$($apptUrl)?maxResultCount=100" -Token $t4Token -TenantId $t4Id
    $sw.Stop()
    $t4Count = if ($resp.Body) { $resp.Body.totalCount } else { 0 }
    Assert-AreEqual -TestResults $TestResults -TestId "B8.3.4" -Name "T4 empty appointment list" -Actual $t4Count -Expected 0 -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B8.3.4" -Name "T4 empty list" -Reason "No T4 tenant"
}

# ---- B8.4 Applicant Attorney on Appointment ----

Write-Host "`n--- B8.4 Applicant Attorney ---" -ForegroundColor Yellow

$firstApptKey = if ($SeedState.appointmentIds) { @($SeedState.appointmentIds.Keys | Where-Object { $_ -like "T1_*" })[0] } else { $null }
$firstApptId = if ($firstApptKey) { $SeedState.appointmentIds[$firstApptKey] } else { $null }

if ($firstApptId) {
    # B8.4.1 GET applicant attorney for appointment
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$apptUrl/$firstApptId/applicant-attorney" -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "B8.4.1" -Name "GET applicant attorney for appointment" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds

    # B8.4.2 Details-for-booking by non-existent user
    $fakeGuid = "00000000-0000-0000-0000-000000000099"
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$apptUrl/applicant-attorney-details-for-booking?identityUserId=$fakeGuid" -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    # Should return 200 with null/empty body
    Assert-StatusCode -TestResults $TestResults -TestId "B8.4.2" -Name "AA details-for-booking non-existent returns 200" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B8.4.1" -Name "GET AA for appointment" -Reason "No T1 appointment"
    Add-SkipResult -TestResults $TestResults -TestId "B8.4.2" -Name "AA details non-existent" -Reason "No T1 appointment"
}

# ---- B8.5 Appointment Update (REWORKED) ----

Write-Host "`n--- B8.5 Appointment Update ---" -ForegroundColor Yellow

if ($firstApptId) {
    # Get current appointment
    $current = Invoke-TestApiCall -Method "GET" -Url "$apptUrl/$firstApptId" -Token $t1Token -TenantId $t1Id

    if ($current.Success -and $current.Body) {
        $appt = $current.Body

        # B8.5.1 PUT change PanelNumber
        $newPanel = "WC-TEST-B8-001"
        $updateBody = @{
            panelNumber                  = $newPanel
            requestConfirmationNumber    = $appt.requestConfirmationNumber
            appointmentDate              = $appt.appointmentDate
            patientId                    = $appt.patientId
            identityUserId               = $appt.identityUserId
            appointmentTypeId            = $appt.appointmentTypeId
            locationId                   = $appt.locationId
            doctorAvailabilityId         = $appt.doctorAvailabilityId
            concurrencyStamp             = $appt.concurrencyStamp
        }
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $resp = Invoke-TestApiCall -Method "PUT" -Url "$apptUrl/$firstApptId" -Body $updateBody -Token $t1Token -TenantId $t1Id
        $sw.Stop()
        Assert-StatusCode -TestResults $TestResults -TestId "B8.5.1" -Name "PUT change PanelNumber" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds

        # Restore original panel number
        if ($resp.Success -and $resp.Body) {
            $restoreBody = @{
                panelNumber                  = $appt.panelNumber
                requestConfirmationNumber    = $appt.requestConfirmationNumber
                appointmentDate              = $appt.appointmentDate
                patientId                    = $appt.patientId
                identityUserId               = $appt.identityUserId
                appointmentTypeId            = $appt.appointmentTypeId
                locationId                   = $appt.locationId
                doctorAvailabilityId         = $appt.doctorAvailabilityId
                concurrencyStamp             = $resp.Body.concurrencyStamp
            }
            Invoke-TestApiCall -Method "PUT" -Url "$apptUrl/$firstApptId" -Body $restoreBody -Token $t1Token -TenantId $t1Id | Out-Null
        }

        # B8.5.2 Stale concurrencyStamp -> 409
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $staleBody = @{
            panelNumber                  = $appt.panelNumber
            requestConfirmationNumber    = $appt.requestConfirmationNumber
            appointmentDate              = $appt.appointmentDate
            patientId                    = $appt.patientId
            identityUserId               = $appt.identityUserId
            appointmentTypeId            = $appt.appointmentTypeId
            locationId                   = $appt.locationId
            doctorAvailabilityId         = $appt.doctorAvailabilityId
            concurrencyStamp             = "stale-stamp-that-does-not-exist"
        }
        $resp = Invoke-TestApiCall -Method "PUT" -Url "$apptUrl/$firstApptId" -Body $staleBody -Token $t1Token -TenantId $t1Id
        $sw.Stop()
        Assert-StatusCode -TestResults $TestResults -TestId "B8.5.2" -Name "Stale concurrencyStamp returns 409" -Response $resp -Expected 409 -DurationMs $sw.ElapsedMilliseconds
    } else {
        Add-SkipResult -TestResults $TestResults -TestId "B8.5.1" -Name "PUT change PanelNumber" -Reason "Cannot GET appointment"
        Add-SkipResult -TestResults $TestResults -TestId "B8.5.2" -Name "Stale concurrencyStamp" -Reason "Cannot GET appointment"
    }
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B8.5.1" -Name "Appointment update" -Reason "No T1 appointment"
    Add-SkipResult -TestResults $TestResults -TestId "B8.5.2" -Name "Stale stamp" -Reason "No T1 appointment"
}

Write-Host "`nPhase B8 COMPLETE" -ForegroundColor Green
