<#
.SYNOPSIS
    Phase A5: Create Appointments (28 total across T1/T2/T3).
.DESCRIPTION
    Depends on: All previous phases (patients, availability slots, types, locations).
    Creates each appointment directly with its desired final status via AppointmentCreateDto.
    CRITICAL: AppointmentUpdateDto does NOT have AppointmentStatus - cannot change status after creation.
    Each creation books one Available(8) slot → slot becomes Booked(9) automatically.
    Auth: Tenant admin per tenant, __tenant header required.
#>
param(
    [Parameter(Mandatory)][hashtable]$SeedState,
    [string]$ApiBaseUrl = "https://localhost:44327",
    [string]$AuthServerUrl = "https://localhost:44368"
)

$ErrorActionPreference = "Stop"

# Load helpers (needed for $script: variables like $script:AllFirstNames, etc.)
$helpersDir = Join-Path $PSScriptRoot "..\helpers"
. "$helpersDir\Get-AuthToken.ps1"
. "$helpersDir\Invoke-ApiCall.ps1"
. "$helpersDir\New-FakeData.ps1"

$defaultPassword = $env:TEST_PASSWORD

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "PHASE A5: Appointments (Tier 5)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

if (-not $SeedState.appointmentIds) { $SeedState.appointmentIds = @{} }
if (-not $SeedState.appointmentConfirmationNumbers) { $SeedState.appointmentConfirmationNumbers = @{} }
# Map: appointmentId → slotId (for cleanup script to release slots)
if (-not $SeedState._appointmentSlotMap) { $SeedState._appointmentSlotMap = @{} }

# All 13 statuses must appear at least once
$allStatuses = @(1,2,3,4,5,6,7,8,9,10,11,12,13)
$statusCoverage = [System.Collections.Generic.HashSet[int]]::new()

# Appointment distribution: T1=13, T2=10, T3=5
$tenantApptCounts = @{ "T1" = 13; "T2" = 10; "T3" = 5 }

$globalApptIndex = 0

foreach ($tenantKey in @("T1", "T2", "T3")) {
    $count = $tenantApptCounts[$tenantKey]
    $tenantId = $SeedState.tenantIds[$tenantKey]
    $tenantEmail = $SeedState._tenantEmails[$tenantKey]
    $tenantToken = Get-AuthToken -Username $tenantEmail -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $tenantId
    $doctorUserId = $SeedState.doctorUserIds[$tenantKey]

    Write-Host "`n--- $($tenantKey): $count Appointments ---" -ForegroundColor Yellow

    # Get available slots for this tenant
    $availableSlots = @()
    $allTenantSlotKeys = $SeedState.availabilitySlotIds.Keys | Where-Object { $_ -like "${tenantKey}_slot_*" }

    foreach ($slotKey in $allTenantSlotKeys) {
        $slotId = $SeedState.availabilitySlotIds[$slotKey]
        try {
            $slotDetail = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/doctor-availabilities/$slotId" -Token $tenantToken -TenantId $tenantId
            if ($slotDetail.bookingStatusId -eq 8) {  # Available
                $availableSlots += @{
                    Id              = $slotId
                    Key             = $slotKey
                    AvailableDate   = $slotDetail.availableDate
                    FromTime        = $slotDetail.fromTime
                    ToTime          = $slotDetail.toTime
                    LocationId      = $slotDetail.locationId
                    AppointmentTypeId = $slotDetail.appointmentTypeId
                }
            }
        } catch {
            Write-Host "    [WARN] Could not fetch slot $slotKey" -ForegroundColor Yellow
        }
    }

    if ($availableSlots.Count -lt $count) {
        Write-Host "    [WARN] Only $($availableSlots.Count) available slots for $count appointments. Adjusting count." -ForegroundColor Yellow
        $count = $availableSlots.Count
    }

    # Get patient IDs for this tenant
    $patientKeys = $SeedState.patientIds.Keys | Where-Object { $_ -like "${tenantKey}_*" }
    $patientList = @($patientKeys | ForEach-Object { @{ Key = $_; Id = $SeedState.patientIds[$_]; UserId = $SeedState.patientUserIds[$_] } })

    if ($patientList.Count -eq 0) {
        Write-Host "    [WARN] No patients for $tenantKey. Skipping." -ForegroundColor Yellow
        continue
    }

    # Get a valid appointment type ID (from doctor's types or seed state)
    $realTypeIds = @($SeedState.appointmentTypeIds.Values | Where-Object { $_ })

    for ($a = 0; $a -lt $count; $a++) {
        $globalApptIndex++
        $slot = $availableSlots[$a]

        # Pick patient (cycle through available patients for repeat-patient scenario)
        $patient = $patientList[$a % $patientList.Count]

        # Status assignment: enforce coverage of all 13 statuses
        $uncoveredStatuses = @($allStatuses | Where-Object { -not $statusCoverage.Contains($_) })
        if ($uncoveredStatuses.Count -gt 0 -and $a -lt $uncoveredStatuses.Count) {
            $status = $uncoveredStatuses[$a]
        } else {
            $status = $allStatuses | Get-Random
        }
        [void]$statusCoverage.Add($status)

        # Determine appointment type: use slot's type if set, otherwise pick one
        $apptTypeId = if ($slot.AppointmentTypeId) { $slot.AppointmentTypeId } else { $realTypeIds | Get-Random }

        # Generate appointment date within the slot's time range
        $slotDate = [DateTime]::Parse($slot.AvailableDate)
        $fromTime = $slot.FromTime
        $toTime = $slot.ToTime

        # Boundary test cases
        $apptDate = if ($a -eq 0 -and $tenantKey -eq "T1") {
            # First T1 appointment: exact boundary at FromTime
            New-FakeAppointmentDateAtBoundary -SlotDate $slotDate -FromTime $fromTime
        } elseif ($a -eq 1 -and $tenantKey -eq "T1") {
            # Second T1 appointment: 1 minute before ToTime
            New-FakeAppointmentDateNearEnd -SlotDate $slotDate -ToTime $toTime
        } else {
            New-FakeAppointmentDate -SlotDate $slotDate -FromTime $fromTime -ToTime $toTime
        }

        # Panel number: 60% get one, 40% null
        $panelNumber = if ((Get-Random -Minimum 1 -Maximum 100) -le 60) { New-FakePanelNumber } else { $null }

        # Due date: 70% get one, 30% null
        $dueDate = New-FakeDueDate -AppointmentDate $apptDate

        $apptKey = "${tenantKey}_Appt_${a}"
        $statusName = @("","Pending","Approved","Rejected","NoShow","CancelledNoBill","CancelledLate","RescheduledNoBill","RescheduledLate","CheckedIn","CheckedOut","Billed","RescheduleRequested","CancellationRequested")[$status]

        # Create appointment directly with final status (CORRECTED - no PUT needed)
        $body = @{
            appointmentDate          = $apptDate.ToString("yyyy-MM-ddTHH:mm:ss")
            appointmentStatus        = $status
            patientId                = $patient.Id
            identityUserId           = $doctorUserId
            appointmentTypeId        = $apptTypeId
            locationId               = $slot.LocationId
            doctorAvailabilityId     = $slot.Id
            panelNumber              = $panelNumber
            requestConfirmationNumber = ""  # Ignored by service - auto-generated
        }

        if ($dueDate) {
            $body.dueDate = $dueDate.ToString("yyyy-MM-ddTHH:mm:ss")
        }

        try {
            $result = Invoke-ApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/appointments" -Body $body -Token $tenantToken -TenantId $tenantId
            $SeedState.appointmentIds[$apptKey] = $result.id
            $SeedState.appointmentConfirmationNumbers[$apptKey] = $result.requestConfirmationNumber
            $SeedState._appointmentSlotMap[$result.id] = $slot.Id

            Write-Host "    [$globalApptIndex] $($result.requestConfirmationNumber) | Status=$statusName | Patient=$($patient.Key) | $($slotDate.ToString('yyyy-MM-dd'))" -ForegroundColor Green
        } catch {
            Write-Host "    [ERROR] Failed to create appointment $apptKey : $_" -ForegroundColor Red
        }
    }
}

# Verify status coverage
$missingStatuses = @($allStatuses | Where-Object { -not $statusCoverage.Contains($_) })
if ($missingStatuses.Count -gt 0) {
    Write-Host "`n  [WARN] Missing status coverage for: $($missingStatuses -join ', ')" -ForegroundColor Yellow
} else {
    Write-Host "`n  All 13 appointment statuses covered" -ForegroundColor Green
}

Write-Host "`nPhase A5 COMPLETE" -ForegroundColor Green
Write-Host "  Total appointments: $($SeedState.appointmentIds.Count)"
Write-Host "  T1: $(($SeedState.appointmentIds.Keys | Where-Object { $_ -like 'T1_*' }).Count)"
Write-Host "  T2: $(($SeedState.appointmentIds.Keys | Where-Object { $_ -like 'T2_*' }).Count)"
Write-Host "  T3: $(($SeedState.appointmentIds.Keys | Where-Object { $_ -like 'T3_*' }).Count)"

return $SeedState
