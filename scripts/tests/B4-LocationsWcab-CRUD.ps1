<#
.SYNOPSIS
    Phase B4: Locations & WCAB Offices CRUD
.DESCRIPTION
    Tests CRUD + navigation properties, isActive filter, Excel download (WCAB only), boundary lengths.
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
Write-Host "PHASE B4: Locations & WCAB Offices CRUD" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

$hostToken = Get-AuthToken -Username "admin@abp.io" -Password $env:TEST_PASSWORD -AuthServerUrl $AuthServerUrl
$caStateId = $SeedState.stateIds["California"]

# ---- B4.1 Locations ----

Write-Host "`n--- B4.1 Locations ---" -ForegroundColor Yellow
$locUrl = "$ApiBaseUrl/api/app/locations"

# B4.1.1 GET list count
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$($locUrl)?maxResultCount=100" -Token $hostToken
$sw.Stop()
$locCount = if ($resp.Body) { $resp.Body.totalCount } else { 0 }
Assert-GreaterOrEqual -TestResults $TestResults -TestId "B4.1.1" -Name "Locations count >= 8" -Actual $locCount -Expected 8 -DurationMs $sw.ElapsedMilliseconds

# B4.1.2 GET single by ID
$locId = $SeedState.locationIds["Loc1"]
if ($locId) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$locUrl/$locId" -Token $hostToken
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "B4.1.2" -Name "Location GET by ID" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B4.1.2" -Name "Location GET by ID" -Reason "No Loc1 in seed state"
}

# B4.1.3 isActive=true filter
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$($locUrl)?isActive=true&maxResultCount=100" -Token $hostToken
$sw.Stop()
$activeCount = if ($resp.Body) { $resp.Body.totalCount } else { 0 }
Assert-GreaterOrEqual -TestResults $TestResults -TestId "B4.1.3" -Name "Active locations >= 6" -Actual $activeCount -Expected 6 -DurationMs $sw.ElapsedMilliseconds

# B4.1.4 isActive=false filter
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$($locUrl)?isActive=false&maxResultCount=100" -Token $hostToken
$sw.Stop()
$inactiveCount = if ($resp.Body) { $resp.Body.totalCount } else { 0 }
Assert-GreaterOrEqual -TestResults $TestResults -TestId "B4.1.4" -Name "Inactive locations >= 1" -Actual $inactiveCount -Expected 1 -DurationMs $sw.ElapsedMilliseconds

# B4.1.5 with-navigation-properties
if ($locId) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$locUrl/with-navigation-properties/$locId" -Token $hostToken
    $sw.Stop()
    $hasState = ($resp.Success -and $resp.Body -and $resp.Body.state)
    Assert-IsTrue -TestResults $TestResults -TestId "B4.1.5" -Name "Location nav props include State" -Condition $hasState -Details "State present: $hasState" -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B4.1.5" -Name "Location nav props" -Reason "No Loc1"
}

# B4.1.6 state-lookup endpoint
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$locUrl/state-lookup?maxResultCount=50" -Token $hostToken
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B4.1.6" -Name "Location state-lookup" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds

# B4.1.7 POST create with null appointmentTypeId
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$body = @{ name = "B4TestLoc"; address = "123 Test St"; city = "TestCity"; zipCode = "90001"; isActive = $true; stateId = $caStateId; appointmentTypeId = $null }
$resp = Invoke-TestApiCall -Method "POST" -Url $locUrl -Body $body -Token $hostToken
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B4.1.7" -Name "Location POST with null appointmentTypeId" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
$testLocId = if ($resp.Body) { $resp.Body.id } else { $null }

# B4.1.8 DELETE test location
if ($testLocId) {
    $resp = Invoke-TestApiCall -Method "DELETE" -Url "$locUrl/$testLocId" -Token $hostToken
    Assert-IsTrue -TestResults $TestResults -TestId "B4.1.8" -Name "Location DELETE cleanup" -Condition ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 204) -Details "Status: $($resp.StatusCode)"
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B4.1.8" -Name "Location DELETE cleanup" -Reason "No test location created"
}

# B4.1.9 GET deleted returns 404
if ($testLocId) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$locUrl/$testLocId" -Token $hostToken
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "B4.1.9" -Name "Location GET deleted returns 404" -Response $resp -Expected 404 -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B4.1.9" -Name "Location GET deleted returns 404" -Reason "No test location"
}

# B4.1.10 Name boundary: 50 chars OK
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$name50 = "X" * 50
$body = @{ name = $name50; address = "Test"; city = "Test"; zipCode = "99999"; isActive = $true; stateId = $caStateId }
$resp = Invoke-TestApiCall -Method "POST" -Url $locUrl -Body $body -Token $hostToken
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B4.1.10" -Name "Location name 50 chars OK" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
if ($resp.Body -and $resp.Body.id) {
    Invoke-TestApiCall -Method "DELETE" -Url "$locUrl/$($resp.Body.id)" -Token $hostToken | Out-Null
}

# B4.1.11 Name boundary: 51 chars fails
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$name51 = "X" * 51
$body = @{ name = $name51; address = "Test"; city = "Test"; zipCode = "99999"; isActive = $true; stateId = $caStateId }
$resp = Invoke-TestApiCall -Method "POST" -Url $locUrl -Body $body -Token $hostToken
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B4.1.11" -Name "Location name 51 chars fails (400)" -Response $resp -Expected 400 -DurationMs $sw.ElapsedMilliseconds

# ---- B4.2 WCAB Offices ----

Write-Host "`n--- B4.2 WCAB Offices ---" -ForegroundColor Yellow
$wcabUrl = "$ApiBaseUrl/api/app/wcab-offices"

# B4.2.1 GET list count
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$($wcabUrl)?maxResultCount=100" -Token $hostToken
$sw.Stop()
$wcabCount = if ($resp.Body) { $resp.Body.totalCount } else { 0 }
Assert-GreaterOrEqual -TestResults $TestResults -TestId "B4.2.1" -Name "WCAB Offices count >= 7" -Actual $wcabCount -Expected 7 -DurationMs $sw.ElapsedMilliseconds

# B4.2.2 GET single by ID
$wcabId = $SeedState.wcabIds["Office1"]
if ($wcabId) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$wcabUrl/$wcabId" -Token $hostToken
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "B4.2.2" -Name "WCAB GET by ID" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B4.2.2" -Name "WCAB GET by ID" -Reason "No Office1 in seed state"
}

# B4.2.3 POST + DELETE CRUD cycle
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$body = @{ name = "B4TestWCAB"; abbreviation = "B4T"; address = "Test"; city = "TestCity"; zipCode = "90001"; isActive = $true; stateId = $caStateId }
$resp = Invoke-TestApiCall -Method "POST" -Url $wcabUrl -Body $body -Token $hostToken
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B4.2.3" -Name "WCAB POST create" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
$testWcabId = if ($resp.Body) { $resp.Body.id } else { $null }
if ($testWcabId) {
    Invoke-TestApiCall -Method "DELETE" -Url "$wcabUrl/$testWcabId" -Token $hostToken | Out-Null
}

# B4.2.4 isActive filter
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$($wcabUrl)?isActive=true&maxResultCount=100" -Token $hostToken
$sw.Stop()
$activeWcab = if ($resp.Body) { $resp.Body.totalCount } else { 0 }
Assert-GreaterOrEqual -TestResults $TestResults -TestId "B4.2.4" -Name "Active WCAB offices >= 6" -Actual $activeWcab -Expected 6 -DurationMs $sw.ElapsedMilliseconds

# B4.2.5 Excel download-token
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$wcabUrl/download-token" -Token $hostToken
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B4.2.5" -Name "WCAB download-token endpoint" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
$downloadToken = if ($resp.Body) { $resp.Body.token } else { $null }

# B4.2.6 Excel download
if ($downloadToken) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$wcabUrl/as-excel-file?downloadToken=$([uri]::EscapeDataString($downloadToken))" -Token $hostToken
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "B4.2.6" -Name "WCAB as-excel-file endpoint" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B4.2.6" -Name "WCAB as-excel-file endpoint" -Reason "No download token"
}

# B4.2.7 Abbreviation boundary: 50 chars OK
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$body = @{ name = "B4BoundaryTest"; abbreviation = ("A" * 50); address = "T"; city = "T"; zipCode = "90001"; isActive = $true; stateId = $caStateId }
$resp = Invoke-TestApiCall -Method "POST" -Url $wcabUrl -Body $body -Token $hostToken
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B4.2.7" -Name "WCAB abbreviation 50 chars OK" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
if ($resp.Body -and $resp.Body.id) {
    Invoke-TestApiCall -Method "DELETE" -Url "$wcabUrl/$($resp.Body.id)" -Token $hostToken | Out-Null
}

# B4.2.8 Abbreviation boundary: 51 chars fails
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$body = @{ name = "B4BoundaryFail"; abbreviation = ("A" * 51); address = "T"; city = "T"; zipCode = "90001"; isActive = $true; stateId = $caStateId }
$resp = Invoke-TestApiCall -Method "POST" -Url $wcabUrl -Body $body -Token $hostToken
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B4.2.8" -Name "WCAB abbreviation 51 chars fails (400)" -Response $resp -Expected 400 -DurationMs $sw.ElapsedMilliseconds

Write-Host "`nPhase B4 COMPLETE" -ForegroundColor Green
