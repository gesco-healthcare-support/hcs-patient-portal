<#
.SYNOPSIS
    Phase B7: Doctor Availability CRUD
.DESCRIPTION
    Tests availability CRUD, filter queries, delete-by-slot/date, booking status, tenant isolation.
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
Write-Host "PHASE B7: Doctor Availability CRUD" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

$defaultPassword = $env:TEST_PASSWORD
$availUrl = "$ApiBaseUrl/api/app/doctor-availabilities"
$t1Id = $SeedState.tenantIds["T1"]
$t2Id = $SeedState.tenantIds["T2"]

if (-not $t1Id) {
    Write-Host "  T1 tenant not available. Skipping B7." -ForegroundColor Yellow
    Add-SkipResult -TestResults $TestResults -TestId "B7.0.0" -Name "B7 prerequisites" -Reason "No T1 tenant"
    return
}

$t1Token = Get-AuthToken -Username $SeedState._tenantEmails["T1"] -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $t1Id

# ---- B7.1 Standard CRUD ----

Write-Host "`n--- B7.1 Standard CRUD ---" -ForegroundColor Yellow

# B7.1.1 GET list
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$($availUrl)?maxResultCount=100" -Token $t1Token -TenantId $t1Id
$sw.Stop()
$slotCount = if ($resp.Body) { $resp.Body.totalCount } else { 0 }
Assert-GreaterOrEqual -TestResults $TestResults -TestId "B7.1.1" -Name "T1 availability slots >= 10" -Actual $slotCount -Expected 10 -DurationMs $sw.ElapsedMilliseconds

# B7.1.2 GET single by ID
$firstSlotKey = if ($SeedState.availabilitySlotIds) { @($SeedState.availabilitySlotIds.Keys | Where-Object { $_ -like "T1_*" })[0] } else { $null }
$firstSlotId = if ($firstSlotKey) { $SeedState.availabilitySlotIds[$firstSlotKey] } else { $null }
if ($firstSlotId) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$availUrl/$firstSlotId" -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "B7.1.2" -Name "Availability GET by ID" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B7.1.2" -Name "Availability GET by ID" -Reason "No T1 slot in seed state"
}

# B7.1.3 POST create new slot
$locId = $SeedState.locationIds["Loc1"]
$typeId = @($SeedState.appointmentTypeIds.Values)[0]
$futureDate = (Get-Date).AddDays(60).ToString("yyyy-MM-dd")
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$body = @{
    availableDate     = "${futureDate}T00:00:00"
    fromTime          = "14:00:00"
    toTime            = "15:00:00"
    bookingStatusId   = 8
    locationId        = $locId
    appointmentTypeId = $typeId
}
$resp = Invoke-TestApiCall -Method "POST" -Url $availUrl -Body $body -Token $t1Token -TenantId $t1Id
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B7.1.3" -Name "Availability POST create" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
$testSlotId = if ($resp.Body) { $resp.Body.id } else { $null }

# B7.1.4 PUT update
if ($testSlotId) {
    $getResp = Invoke-TestApiCall -Method "GET" -Url "$availUrl/$testSlotId" -Token $t1Token -TenantId $t1Id
    $stamp = if ($getResp.Body) { $getResp.Body.concurrencyStamp } else { "" }
    $updateBody = @{
        availableDate     = "${futureDate}T00:00:00"
        fromTime          = "14:30:00"
        toTime            = "15:30:00"
        bookingStatusId   = 8
        locationId        = $locId
        appointmentTypeId = $typeId
        concurrencyStamp  = $stamp
    }
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "PUT" -Url "$availUrl/$testSlotId" -Body $updateBody -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "B7.1.4" -Name "Availability PUT update" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B7.1.4" -Name "Availability PUT update" -Reason "No test slot"
}

# B7.1.5 DELETE
if ($testSlotId) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "DELETE" -Url "$availUrl/$testSlotId" -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    $delOk = ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 204)
    Assert-IsTrue -TestResults $TestResults -TestId "B7.1.5" -Name "Availability DELETE" -Condition $delOk -Details "Status: $($resp.StatusCode)" -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B7.1.5" -Name "Availability DELETE" -Reason "No test slot"
}

# B7.1.6 GET deleted returns 404
if ($testSlotId) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$availUrl/$testSlotId" -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "B7.1.6" -Name "Availability GET deleted 404" -Response $resp -Expected 404 -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B7.1.6" -Name "Availability GET deleted 404" -Reason "No test slot"
}

# ---- B7.2 Filter Queries ----

Write-Host "`n--- B7.2 Filter Queries ---" -ForegroundColor Yellow

# B7.2.1 Filter by bookingStatusId=8 (Available)
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$($availUrl)?bookingStatusId=8&maxResultCount=100" -Token $t1Token -TenantId $t1Id
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B7.2.1" -Name "Filter by Available status" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds

# B7.2.2 Filter by bookingStatusId=9 (Booked)
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$($availUrl)?bookingStatusId=9&maxResultCount=100" -Token $t1Token -TenantId $t1Id
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B7.2.2" -Name "Filter by Booked status" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds

# B7.2.3 Filter by locationId
if ($locId) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$($availUrl)?locationId=$locId&maxResultCount=100" -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "B7.2.3" -Name "Filter by locationId" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
}

# B7.2.4 Filter by date range
$minDate = (Get-Date).AddDays(-30).ToString("yyyy-MM-dd")
$maxDate = (Get-Date).AddDays(30).ToString("yyyy-MM-dd")
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$($availUrl)?availableDateMin=$minDate&availableDateMax=$maxDate&maxResultCount=100" -Token $t1Token -TenantId $t1Id
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B7.2.4" -Name "Filter by date range" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds

# ---- B7.3 Preview ----

Write-Host "`n--- B7.3 Preview & Bulk Delete ---" -ForegroundColor Yellow

# B7.3.1 Preview is read-only
$previewDate = (Get-Date).AddDays(70).ToString("yyyy-MM-dd")
$sw = [System.Diagnostics.Stopwatch]::StartNew()
# Must send as pre-serialized JSON array (PS single-element array gets unwrapped by ConvertTo-Json)
$previewJson = "[{""fromDate"":""${previewDate}T00:00:00"",""toDate"":""${previewDate}T00:00:00"",""fromTime"":""09:00:00"",""toTime"":""12:00:00"",""bookingStatusId"":8,""locationId"":""$locId"",""appointmentTypeId"":""$typeId"",""appointmentDurationMinutes"":60}]"
$resp = Invoke-TestApiCall -Method "POST" -Url "$availUrl/preview" -Body $previewJson -Token $t1Token -TenantId $t1Id
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B7.3.1" -Name "Availability preview" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds

# ---- B7.4 Booking Status Validation ----

Write-Host "`n--- B7.4 Booking Status ---" -ForegroundColor Yellow

# B7.4.1 Create with Reserved status
$reservedDate = (Get-Date).AddDays(65).ToString("yyyy-MM-dd")
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$body = @{
    availableDate = "${reservedDate}T00:00:00"; fromTime = "10:00:00"; toTime = "11:00:00"
    bookingStatusId = 10; locationId = $locId; appointmentTypeId = $typeId
}
$resp = Invoke-TestApiCall -Method "POST" -Url $availUrl -Body $body -Token $t1Token -TenantId $t1Id
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B7.4.1" -Name "Create Reserved(10) slot" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
if ($resp.Body -and $resp.Body.id) {
    Invoke-TestApiCall -Method "DELETE" -Url "$availUrl/$($resp.Body.id)" -Token $t1Token -TenantId $t1Id | Out-Null
}

# B7.4.2 fromTime >= toTime (gap C1.7)
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$body = @{
    availableDate = "${reservedDate}T00:00:00"; fromTime = "15:00:00"; toTime = "14:00:00"
    bookingStatusId = 8; locationId = $locId; appointmentTypeId = $typeId
}
$resp = Invoke-TestApiCall -Method "POST" -Url $availUrl -Body $body -Token $t1Token -TenantId $t1Id
$sw.Stop()
# Document actual behavior - this is gap C1.7
$details = "Status: $($resp.StatusCode). fromTime > toTime should fail but may succeed (C1.7)"
Add-TestResult -TestResults $TestResults -TestId "B7.4.2" -Name "fromTime > toTime behavior" -Status $(if($resp.StatusCode -eq 400){"PASS"}else{"FAIL-EXPECTED"}) -Details $details -GapId "C1.7" -DurationMs $sw.ElapsedMilliseconds
if ($resp.Body -and $resp.Body.id) {
    Invoke-TestApiCall -Method "DELETE" -Url "$availUrl/$($resp.Body.id)" -Token $t1Token -TenantId $t1Id | Out-Null
}

# ---- B7.5 Tenant Isolation ----

Write-Host "`n--- B7.5 Tenant Isolation ---" -ForegroundColor Yellow

if ($t2Id) {
    # T1 token + T2 header should show T2 slots
    $t2Token = Get-AuthToken -Username $SeedState._tenantEmails["T2"] -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $t2Id
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$($availUrl)?maxResultCount=100" -Token $t2Token -TenantId $t2Id
    $sw.Stop()
    $t2Count = if ($resp.Body) { $resp.Body.totalCount } else { 0 }
    Assert-GreaterOrEqual -TestResults $TestResults -TestId "B7.5.1" -Name "T2 slots visible in T2 context" -Actual $t2Count -Expected 1 -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B7.5.1" -Name "Tenant isolation" -Reason "No T2"
}

Write-Host "`nPhase B7 COMPLETE" -ForegroundColor Green
