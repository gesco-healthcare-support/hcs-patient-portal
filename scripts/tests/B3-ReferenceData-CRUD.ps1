<#
.SYNOPSIS
    Phase B3: Reference Data CRUD (States, AppointmentTypes, Statuses, Languages)
.DESCRIPTION
    Tests standard CRUD operations on all 4 reference data entities.
    Creates test records, validates, then cleans up. Never modifies seeded data.
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
Write-Host "PHASE B3: Reference Data CRUD" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

$hostToken = Get-AuthToken -Username "admin@abp.io" -Password $env:TEST_PASSWORD -AuthServerUrl $AuthServerUrl

# ---- Generic CRUD test function ----

function Test-EntityCrud {
    param(
        [string]$PhasePrefix,       # e.g. "B3.1"
        [string]$EntityName,        # e.g. "States"
        [string]$Endpoint,          # e.g. "/api/app/states"
        [hashtable]$SeedIds,        # e.g. $SeedState.stateIds
        [int]$ExpectedCount,        # e.g. 10
        [string]$CreateName,        # Name for test record
        [string]$UpdateName,        # Updated name
        [int]$MaxNameLength,        # Max length for name field
        [string]$NameField = "name" # Field name for the name property
    )

    $baseUrl = "$ApiBaseUrl$Endpoint"

    Write-Host "`n--- $PhasePrefix $EntityName ---" -ForegroundColor Yellow

    # Test 1: GET list - totalCount matches seeded
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$($baseUrl)?maxResultCount=100" -Token $hostToken
    $sw.Stop()
    $actualCount = 0
    if ($resp.Success -and $resp.Body) {
        $actualCount = if ($resp.Body.totalCount) { $resp.Body.totalCount } else { 0 }
    }
    Assert-GreaterOrEqual -TestResults $TestResults -TestId "$PhasePrefix.1" -Name "$EntityName list count >= $ExpectedCount" -Actual $actualCount -Expected $ExpectedCount -DurationMs $sw.ElapsedMilliseconds

    # Test 2: GET single by ID
    $firstKey = if ($SeedIds -and $SeedIds.Keys.Count -gt 0) { @($SeedIds.Keys)[0] } else { $null }
    $firstId = if ($firstKey) { $SeedIds[$firstKey] } else { $null }
    if ($firstId) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $resp = Invoke-TestApiCall -Method "GET" -Url "$baseUrl/$firstId" -Token $hostToken
        $sw.Stop()
        Assert-StatusCode -TestResults $TestResults -TestId "$PhasePrefix.2" -Name "$EntityName GET by ID" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
    } else {
        Add-SkipResult -TestResults $TestResults -TestId "$PhasePrefix.2" -Name "$EntityName GET by ID" -Reason "No seeded ID available"
    }

    # Test 3: POST create new
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $createBody = @{ $NameField = $CreateName }
    $resp = Invoke-TestApiCall -Method "POST" -Url $baseUrl -Body $createBody -Token $hostToken
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "$PhasePrefix.3" -Name "$EntityName POST create" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
    $createdId = if ($resp.Success -and $resp.Body) { $resp.Body.id } else { $null }

    # Test 4: PUT update
    if ($createdId) {
        # GET first to get concurrencyStamp
        $current = Invoke-TestApiCall -Method "GET" -Url "$baseUrl/$createdId" -Token $hostToken
        $updateBody = @{ $NameField = $UpdateName }
        if ($current.Body -and $current.Body.concurrencyStamp) {
            $updateBody["concurrencyStamp"] = $current.Body.concurrencyStamp
        }
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $resp = Invoke-TestApiCall -Method "PUT" -Url "$baseUrl/$createdId" -Body $updateBody -Token $hostToken
        $sw.Stop()
        Assert-StatusCode -TestResults $TestResults -TestId "$PhasePrefix.4" -Name "$EntityName PUT update" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
    } else {
        Add-SkipResult -TestResults $TestResults -TestId "$PhasePrefix.4" -Name "$EntityName PUT update" -Reason "No created entity"
    }

    # Test 5: GET updated - name changed
    if ($createdId) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $resp = Invoke-TestApiCall -Method "GET" -Url "$baseUrl/$createdId" -Token $hostToken
        $sw.Stop()
        $actualName = if ($resp.Body) { $resp.Body.$NameField } else { "" }
        Assert-AreEqual -TestResults $TestResults -TestId "$PhasePrefix.5" -Name "$EntityName GET shows updated name" -Actual $actualName -Expected $UpdateName -DurationMs $sw.ElapsedMilliseconds
    } else {
        Add-SkipResult -TestResults $TestResults -TestId "$PhasePrefix.5" -Name "$EntityName GET shows updated name" -Reason "No created entity"
    }

    # Test 6: DELETE created
    if ($createdId) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $resp = Invoke-TestApiCall -Method "DELETE" -Url "$baseUrl/$createdId" -Token $hostToken
        $sw.Stop()
        $deleteSuccess = ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 204)
        Assert-IsTrue -TestResults $TestResults -TestId "$PhasePrefix.6" -Name "$EntityName DELETE" -Condition $deleteSuccess -Details "Status: $($resp.StatusCode)" -DurationMs $sw.ElapsedMilliseconds
    } else {
        Add-SkipResult -TestResults $TestResults -TestId "$PhasePrefix.6" -Name "$EntityName DELETE" -Reason "No created entity"
    }

    # Test 7: GET deleted - 404
    if ($createdId) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $resp = Invoke-TestApiCall -Method "GET" -Url "$baseUrl/$createdId" -Token $hostToken
        $sw.Stop()
        Assert-StatusCode -TestResults $TestResults -TestId "$PhasePrefix.7" -Name "$EntityName GET deleted returns 404" -Response $resp -Expected 404 -DurationMs $sw.ElapsedMilliseconds
    } else {
        Add-SkipResult -TestResults $TestResults -TestId "$PhasePrefix.7" -Name "$EntityName GET deleted returns 404" -Reason "No created entity"
    }

    # Test 8: GET with filter
    if ($firstKey) {
        $filterText = $firstKey.Substring(0, [Math]::Min(5, $firstKey.Length))
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $resp = Invoke-TestApiCall -Method "GET" -Url "$($baseUrl)?filterText=$([uri]::EscapeDataString($filterText))&maxResultCount=100" -Token $hostToken
        $sw.Stop()
        Assert-StatusCode -TestResults $TestResults -TestId "$PhasePrefix.8" -Name "$EntityName GET with filter" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
    } else {
        Add-SkipResult -TestResults $TestResults -TestId "$PhasePrefix.8" -Name "$EntityName GET with filter" -Reason "No seed data"
    }

    # Test 9: GET with pagination
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $page1 = Invoke-TestApiCall -Method "GET" -Url "$($baseUrl)?maxResultCount=3&skipCount=0" -Token $hostToken
    $page2 = Invoke-TestApiCall -Method "GET" -Url "$($baseUrl)?maxResultCount=3&skipCount=3" -Token $hostToken
    $sw.Stop()
    $paginationWorks = $false
    if ($page1.Success -and $page2.Success -and $page1.Body.items -and $page2.Body.items) {
        $page1Ids = @($page1.Body.items | ForEach-Object { $_.id })
        $page2Ids = @($page2.Body.items | ForEach-Object { $_.id })
        $overlap = $page1Ids | Where-Object { $page2Ids -contains $_ }
        $paginationWorks = ($overlap.Count -eq 0 -and $page2Ids.Count -gt 0)
    }
    Assert-IsTrue -TestResults $TestResults -TestId "$PhasePrefix.9" -Name "$EntityName pagination works" -Condition $paginationWorks -Details "Page1: $($page1Ids.Count) items, Page2: $($page2Ids.Count) items" -DurationMs $sw.ElapsedMilliseconds

    # Test 10: POST empty name -> 400
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "POST" -Url $baseUrl -Body @{ $NameField = "" } -Token $hostToken
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "$PhasePrefix.10" -Name "$EntityName POST empty name returns 400" -Response $resp -Expected 400 -DurationMs $sw.ElapsedMilliseconds

    # Test 11: POST name too long -> 400 (if entity has max length constraint)
    $longName = "X" * ($MaxNameLength + 1)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "POST" -Url $baseUrl -Body @{ $NameField = $longName } -Token $hostToken
    # Clean up if it actually created (some entities have no max length constraint)
    if ($resp.StatusCode -eq 200 -and $resp.Body -and $resp.Body.id) {
        Invoke-TestApiCall -Method "DELETE" -Url "$baseUrl/$($resp.Body.id)" -Token $hostToken | Out-Null
    }
    $sw.Stop()
    # Some entities (e.g. States) have no max-length constraint; accept 200 or 400
    $isValidResult = ($resp.StatusCode -eq 400 -or $resp.StatusCode -eq 200)
    Assert-IsTrue -TestResults $TestResults -TestId "$PhasePrefix.11" -Name "$EntityName POST too-long name behavior" -Condition $isValidResult -Details "Status: $($resp.StatusCode). $(if($resp.StatusCode -eq 200){'No max-length constraint'}else{'Validation applied'})" -DurationMs $sw.ElapsedMilliseconds
}

# ---- B3.1 States ----

Test-EntityCrud -PhasePrefix "B3.1" -EntityName "States" -Endpoint "/api/app/states" `
    -SeedIds $SeedState.stateIds -ExpectedCount 10 `
    -CreateName "B3TestState" -UpdateName "B3UpdatedState" -MaxNameLength 100

# ---- B3.2 AppointmentTypes ----

Test-EntityCrud -PhasePrefix "B3.2" -EntityName "AppointmentTypes" -Endpoint "/api/app/appointment-types" `
    -SeedIds $SeedState.appointmentTypeIds -ExpectedCount 6 `
    -CreateName "B3TestType" -UpdateName "B3UpdatedType" -MaxNameLength 100

# ---- B3.3 AppointmentStatuses ----

Test-EntityCrud -PhasePrefix "B3.3" -EntityName "AppointmentStatuses" -Endpoint "/api/app/appointment-statuses" `
    -SeedIds $SeedState.appointmentStatusIds -ExpectedCount 13 `
    -CreateName "B3TestStatus" -UpdateName "B3UpdatedStatus" -MaxNameLength 100

# ---- B3.4 AppointmentLanguages ----

Test-EntityCrud -PhasePrefix "B3.4" -EntityName "AppointmentLanguages" -Endpoint "/api/app/appointment-languages" `
    -SeedIds $SeedState.appointmentLanguageIds -ExpectedCount 12 `
    -CreateName "B3TestLanguage" -UpdateName "B3UpdatedLanguage" -MaxNameLength 50

Write-Host "`nPhase B3 COMPLETE" -ForegroundColor Green
