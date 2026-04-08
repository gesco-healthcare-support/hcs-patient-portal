<#
.SYNOPSIS
    Phase A2: Create Locations (8) and WCAB Offices (7) - Tier 2 entities.
.DESCRIPTION
    Depends on: States from A1 (for StateId FKs).
    Auth: Host admin, no tenant header.
#>
param(
    [Parameter(Mandatory)][hashtable]$SeedState,
    [Parameter(Mandatory)][string]$Token,
    [string]$ApiBaseUrl = "https://localhost:44327"
)

$ErrorActionPreference = "Stop"

# Load helpers (needed for $script: variables like $script:CaCityZipPairs)
$helpersDir = Join-Path $PSScriptRoot "..\helpers"
. "$helpersDir\Get-AuthToken.ps1"
. "$helpersDir\Invoke-ApiCall.ps1"
. "$helpersDir\New-FakeData.ps1"

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "PHASE A2: Locations & WCAB Offices (Tier 2)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

$caStateId = $SeedState.stateIds["California"]
if (-not $caStateId) { throw "California state not found in seed state. Run Phase A1 first." }

# Get a non-CA state for the out-of-state edge case
$nonCaStateName = $SeedState.stateIds.Keys | Where-Object { $_ -ne "California" -and $_ -notlike "TestState_*" -and $_ -ne "North Dakota" } | Get-Random
$nonCaStateId = $SeedState.stateIds[$nonCaStateName]

# Get type IDs for optional assignment
$typeIds = @($SeedState.appointmentTypeIds.Values)

# ---- A2.1 LOCATIONS (8 records) ----

Write-Host "`n--- A2.1 Locations ---" -ForegroundColor Yellow

if (-not $SeedState.locationIds) { $SeedState.locationIds = @{} }

# 6 active locations with CA addresses
$parkingFees = @(0, 5, 8, 10, 15, 20, 25, 30)

for ($i = 1; $i -le 6; $i++) {
    $addr = New-FakeAddress -StateId $caStateId
    $locName = "HCS $($addr.City) Office $i"
    # Truncate to 50-char max
    if ($locName.Length -gt 50) { $locName = $locName.Substring(0, 50) }

    $key = "Loc$i"

    # Idempotency
    $existing = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/locations?filterText=$([uri]::EscapeDataString($locName))&maxResultCount=5" -Token $Token
    $match = $existing.items | Where-Object { $_.name -eq $locName }

    if ($match) {
        Write-Host "  [SKIP] Location '$locName' already exists" -ForegroundColor DarkGray
        $SeedState.locationIds[$key] = $match.id
        continue
    }

    # 3 of 6 get an AppointmentTypeId; other 3 get null
    $typeId = $null
    if ($i -le 3 -and $typeIds.Count -gt 0) {
        $typeId = $typeIds[($i - 1) % $typeIds.Count]
    }

    $body = @{
        name              = $locName
        address           = $addr.Street
        city              = $addr.City
        zipCode           = $addr.ZipCode
        parkingFee        = $parkingFees | Get-Random
        isActive          = $true
        stateId           = $caStateId
        appointmentTypeId = $typeId
    }

    $result = Invoke-ApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/locations" -Body $body -Token $Token
    Write-Host "  [CREATE] Location '$locName' (ID: $($result.id))" -ForegroundColor Green
    $SeedState.locationIds[$key] = $result.id
}

# 1 inactive location
$inactiveAddr = New-FakeAddress -StateId $caStateId
$inactiveName = "HCS Closed $($inactiveAddr.City)"
if ($inactiveName.Length -gt 50) { $inactiveName = $inactiveName.Substring(0, 50) }

$existing = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/locations?filterText=$([uri]::EscapeDataString($inactiveName))&maxResultCount=5" -Token $Token
$match = $existing.items | Where-Object { $_.name -eq $inactiveName }

if ($match) {
    Write-Host "  [SKIP] Inactive location already exists" -ForegroundColor DarkGray
    $SeedState.locationIds["InactiveLocation"] = $match.id
} else {
    $body = @{
        name     = $inactiveName
        address  = $inactiveAddr.Street
        city     = $inactiveAddr.City
        zipCode  = $inactiveAddr.ZipCode
        isActive = $false
        stateId  = $caStateId
    }
    $result = Invoke-ApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/locations" -Body $body -Token $Token
    Write-Host "  [CREATE] Inactive Location '$inactiveName'" -ForegroundColor Green
    $SeedState.locationIds["InactiveLocation"] = $result.id
}

# 1 max-length / out-of-state location
$maxLocName = New-MaxLengthString -Length 50 -Prefix "MaxLoc_"

$existing = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/locations?filterText=$([uri]::EscapeDataString($maxLocName))&maxResultCount=5" -Token $Token
$match = $existing.items | Where-Object { $_.name -eq $maxLocName }

if ($match) {
    Write-Host "  [SKIP] Max-length location already exists" -ForegroundColor DarkGray
    $SeedState.locationIds["MaxLengthLocation"] = $match.id
} else {
    $body = @{
        name              = $maxLocName
        address           = New-MaxLengthString -Length 100 -Prefix "Addr_"
        city              = New-MaxLengthString -Length 50 -Prefix "City_"
        zipCode           = New-MaxLengthString -Length 15 -Prefix "Zip_"
        parkingFee        = 99999.99
        isActive          = $true
        stateId           = $nonCaStateId
        appointmentTypeId = $null
    }
    $result = Invoke-ApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/locations" -Body $body -Token $Token
    Write-Host "  [CREATE] Max-length Location" -ForegroundColor Green
    $SeedState.locationIds["MaxLengthLocation"] = $result.id
}

Write-Host "  Locations complete: $($SeedState.locationIds.Count) records" -ForegroundColor White

# ---- A2.2 WCAB OFFICES (7 records) ----

Write-Host "`n--- A2.2 WCAB Offices ---" -ForegroundColor Yellow

if (-not $SeedState.wcabIds) { $SeedState.wcabIds = @{} }

# 6 active + 1 inactive
$wcabCities = $script:CaCityZipPairs | Get-Random -Count 7

for ($i = 0; $i -lt 7; $i++) {
    $cityInfo = $wcabCities[$i]
    $wcab = New-WcabOfficeName -City $cityInfo.City
    $isActive = ($i -lt 6)  # Last one is inactive
    $key = if ($isActive) { "Office$($i + 1)" } else { "InactiveOffice" }

    # Idempotency
    $existing = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/wcab-offices?filterText=$([uri]::EscapeDataString($wcab.Name))&maxResultCount=5" -Token $Token
    $match = $existing.items | Where-Object { $_.name -eq $wcab.Name }

    if ($match) {
        Write-Host "  [SKIP] WCAB '$($wcab.Name)' already exists" -ForegroundColor DarkGray
        $SeedState.wcabIds[$key] = $match.id
        continue
    }

    $addr = New-FakeAddress -StateId $caStateId
    $body = @{
        name         = $wcab.Name
        abbreviation = $wcab.Abbreviation
        address      = $addr.Street
        city         = $cityInfo.City
        zipCode      = $cityInfo.ZipCode
        isActive     = $isActive
        stateId      = $caStateId
    }

    $result = Invoke-ApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/wcab-offices" -Body $body -Token $Token
    $activeLabel = if ($isActive) { "" } else { " [INACTIVE]" }
    Write-Host "  [CREATE] WCAB '$($wcab.Name)'$activeLabel" -ForegroundColor Green
    $SeedState.wcabIds[$key] = $result.id
}

Write-Host "  WCAB Offices complete: $($SeedState.wcabIds.Count) records" -ForegroundColor White

Write-Host "`nPhase A2 COMPLETE" -ForegroundColor Green
Write-Host "  Locations: $($SeedState.locationIds.Count)"
Write-Host "  WCAB Offices: $($SeedState.wcabIds.Count)"

return $SeedState
