<#
.SYNOPSIS
    Phase A4: Create Doctor Availability Slots (~95 total).
.DESCRIPTION
    Depends on: Tenants, Doctors, Locations, AppointmentTypes from A1-A3.
    Each slot is created individually via POST /api/app/doctor-availabilities.
    TimeOnly fields sent as "HH:mm:ss" strings.
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
Write-Host "PHASE A4: Doctor Availability Slots (Tier 4)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

if (-not $SeedState.availabilitySlotIds) { $SeedState.availabilitySlotIds = @{} }

# Helper: Generate slots for a date range
function New-SlotsForDateRange {
    param(
        [DateTime]$StartDate,
        [DateTime]$EndDate,
        [array]$LocationIds,
        [array]$TypeIds,         # Nullable items ok
        [int]$SlotDurationMinutes = 60,
        [int]$MinSlotsPerDay = 2,
        [int]$MaxSlotsPerDay = 4,
        [string]$StartHour = "09:00",
        [string]$EndHour = "17:00"
    )

    $slots = @()
    $current = $StartDate.Date

    while ($current -le $EndDate.Date) {
        # Weekdays only
        if ($current.DayOfWeek -in @([DayOfWeek]::Saturday, [DayOfWeek]::Sunday)) {
            $current = $current.AddDays(1)
            continue
        }

        $slotsToday = Get-Random -Minimum $MinSlotsPerDay -Maximum ($MaxSlotsPerDay + 1)
        $dayStart = [TimeSpan]::Parse("${StartHour}:00")
        $dayEnd = [TimeSpan]::Parse("${EndHour}:00")
        $availableMinutes = ($dayEnd - $dayStart).TotalMinutes

        # Pick random start times within the day
        $usedStarts = @()
        for ($s = 0; $s -lt $slotsToday; $s++) {
            # Ensure no overlapping slots: pick sequential times
            $offset = [int]($availableMinutes / $slotsToday) * $s
            $slotStart = $dayStart.Add([TimeSpan]::FromMinutes($offset))
            $slotEnd = $slotStart.Add([TimeSpan]::FromMinutes($SlotDurationMinutes))

            # Don't exceed day end
            if ($slotEnd -gt $dayEnd) { break }

            $locId = $LocationIds | Get-Random
            # TypeIds pool may contain $null entries (for null-AppointmentType edge case)
            $typeId = $TypeIds[(Get-Random -Minimum 0 -Maximum $TypeIds.Count)]

            $slots += @{
                AvailableDate = $current
                FromTime      = $slotStart.ToString("hh\:mm\:ss")
                ToTime        = $slotEnd.ToString("hh\:mm\:ss")
                LocationId    = $locId
                AppointmentTypeId = $typeId
                BookingStatusId = 8  # Available
            }
        }

        $current = $current.AddDays(1)
    }

    return $slots
}

$today = (Get-Date).Date

foreach ($t in 1..5) {
    $key = "T$t"
    $tenantId = $SeedState.tenantIds[$key]
    if (-not $tenantId) { continue }

    $tenantEmail = $SeedState._tenantEmails[$key]
    $tenantToken = Get-AuthToken -Username $tenantEmail -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $tenantId

    Write-Host "`n--- $key Availability Slots ---" -ForegroundColor Yellow

    # Get this doctor's assigned locations and types
    $doctorId = $SeedState.doctorIds[$key]
    $doctorDetail = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/doctors/with-navigation-properties/$doctorId" -Token $tenantToken -TenantId $tenantId

    # Extract location IDs and type IDs from the doctor's nav properties
    # DoctorWithNavigationPropertiesDto has: .doctor, .identityUser, .appointmentTypes[], .locations[]
    $docLocationIds = @()
    $docTypeIds = @()

    if ($doctorDetail.locations) {
        $docLocationIds = @($doctorDetail.locations | ForEach-Object { $_.id })
    }
    if ($doctorDetail.appointmentTypes) {
        $docTypeIds = @($doctorDetail.appointmentTypes | ForEach-Object { $_.id })
    }

    # Fallback if nav properties aren't populated
    if ($docLocationIds.Count -eq 0) {
        $docLocationIds = @($SeedState.locationIds.Values | Select-Object -First 2)
    }
    if ($docTypeIds.Count -eq 0) {
        $docTypeIds = @($SeedState.appointmentTypeIds.Values | Select-Object -First 2)
    }

    # Add $null to type IDs pool (allows null-AppointmentType slots)
    $typePool = $docTypeIds + @($null)

    $allSlots = @()

    switch ($t) {
        { $_ -in 1,2,3 } {
            # Active doctors: past + present + future slots
            # Past: 5-7 days in the past 30 days
            $pastDays = Get-Random -Minimum 5 -Maximum 8
            $pastStart = $today.AddDays(-30)
            $pastEnd = $today.AddDays(-3)
            $pastSlots = New-SlotsForDateRange -StartDate $pastStart -EndDate $pastEnd `
                -LocationIds $docLocationIds -TypeIds $typePool `
                -MinSlotsPerDay 2 -MaxSlotsPerDay 4
            # Pick only the number of days we want
            $pastSlots = $pastSlots | Get-Random -Count ([Math]::Min($pastSlots.Count, $pastDays * 3))
            $allSlots += $pastSlots

            # Present: current week (±3 days)
            $presentStart = $today.AddDays(-3)
            $presentEnd = $today.AddDays(3)
            $presentSlots = New-SlotsForDateRange -StartDate $presentStart -EndDate $presentEnd `
                -LocationIds $docLocationIds -TypeIds $typePool `
                -MinSlotsPerDay 2 -MaxSlotsPerDay 4
            $allSlots += $presentSlots

            # Future: 10-15 days in next 60 days
            $futureStart = $today.AddDays(7)
            $futureEnd = $today.AddDays(60)
            $futureSlots = New-SlotsForDateRange -StartDate $futureStart -EndDate $futureEnd `
                -LocationIds $docLocationIds -TypeIds $typePool `
                -MinSlotsPerDay 1 -MaxSlotsPerDay 3
            $futureSlots = $futureSlots | Get-Random -Count ([Math]::Min($futureSlots.Count, 15 * 2))
            $allSlots += $futureSlots
        }
        4 {
            # Zero-appointment doctor: future slots only
            $futureStart = $today.AddDays(5)
            $futureEnd = $today.AddDays(30)
            $allSlots = New-SlotsForDateRange -StartDate $futureStart -EndDate $futureEnd `
                -LocationIds $docLocationIds -TypeIds $typePool `
                -MinSlotsPerDay 1 -MaxSlotsPerDay 2
            $allSlots = @($allSlots | Get-Random -Count ([Math]::Min($allSlots.Count, 10)))
        }
        5 {
            # Zero-appointment doctor: mix of past and future
            $pastStart = $today.AddDays(-20)
            $pastEnd = $today.AddDays(-5)
            $pastSlots = New-SlotsForDateRange -StartDate $pastStart -EndDate $pastEnd `
                -LocationIds $docLocationIds -TypeIds $typePool `
                -MinSlotsPerDay 1 -MaxSlotsPerDay 2

            $futureStart = $today.AddDays(5)
            $futureEnd = $today.AddDays(30)
            $futureSlots = New-SlotsForDateRange -StartDate $futureStart -EndDate $futureEnd `
                -LocationIds $docLocationIds -TypeIds $typePool `
                -MinSlotsPerDay 1 -MaxSlotsPerDay 2

            $allSlots = @($pastSlots) + @($futureSlots)
            $allSlots = @($allSlots | Get-Random -Count ([Math]::Min($allSlots.Count, 12)))
        }
    }

    # Deliberate edge cases
    # 1 Reserved slot (BookingStatus=10) per active tenant
    if ($allSlots.Count -gt 0 -and $t -le 3) {
        $allSlots[-1].BookingStatusId = 10  # Reserved
    }

    # 1 null-AppointmentType slot per active tenant (for conditional validation testing)
    if ($allSlots.Count -gt 1 -and $t -le 3) {
        $allSlots[-2].AppointmentTypeId = $null
    }

    # For T5: mark 2 slots as Reserved
    if ($t -eq 5 -and $allSlots.Count -ge 2) {
        $allSlots[0].BookingStatusId = 10
        $allSlots[1].BookingStatusId = 10
    }

    # Create each slot
    $slotCount = 0
    foreach ($slot in $allSlots) {
        $slotCount++
        $slotKey = "${key}_slot_$($slot.AvailableDate.ToString('yyyyMMdd'))_$($slot.FromTime -replace ':','')"

        # Deduplicate key
        $suffix = 0
        $origKey = $slotKey
        while ($SeedState.availabilitySlotIds.ContainsKey($slotKey)) {
            $suffix++
            $slotKey = "${origKey}_$suffix"
        }

        $body = @{
            availableDate     = $slot.AvailableDate.ToString("yyyy-MM-ddT00:00:00")
            fromTime          = $slot.FromTime
            toTime            = $slot.ToTime
            bookingStatusId   = $slot.BookingStatusId
            locationId        = $slot.LocationId
            appointmentTypeId = $slot.AppointmentTypeId
        }

        try {
            $result = Invoke-ApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/doctor-availabilities" -Body $body -Token $tenantToken -TenantId $tenantId
            $SeedState.availabilitySlotIds[$slotKey] = $result.id
            $statusLabel = switch ($slot.BookingStatusId) { 8 { "Available" }; 9 { "Booked" }; 10 { "Reserved" } }
            Write-Host "    [$slotCount] $($slot.AvailableDate.ToString('yyyy-MM-dd')) $($slot.FromTime)-$($slot.ToTime) [$statusLabel]" -ForegroundColor DarkGray
        } catch {
            Write-Host "    [ERROR] Failed to create slot $slotKey : $_" -ForegroundColor Red
        }
    }

    Write-Host "  $($key): $slotCount slots created" -ForegroundColor Green
}

Write-Host "`nPhase A4 COMPLETE" -ForegroundColor Green
Write-Host "  Total slots: $($SeedState.availabilitySlotIds.Count)"

return $SeedState
