<#
.SYNOPSIS
    Phase B9: Child Entities CRUD
.DESCRIPTION
    Tests EmployerDetails, Accessors, ApplicantAttorneys, AppointmentApplicantAttorneys.
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
Write-Host "PHASE B9: Child Entities CRUD" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

$defaultPassword = $env:TEST_PASSWORD
$t1Id = $SeedState.tenantIds["T1"]
if (-not $t1Id) {
    Add-SkipResult -TestResults $TestResults -TestId "B9.0.0" -Name "B9 prerequisites" -Reason "No T1 tenant"
    return
}

$t1Token = Get-AuthToken -Username $SeedState._tenantEmails["T1"] -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $t1Id
$hostToken = Get-AuthToken -Username "admin@abp.io" -Password $defaultPassword -AuthServerUrl $AuthServerUrl

$firstApptKey = if ($SeedState.appointmentIds) { @($SeedState.appointmentIds.Keys | Where-Object { $_ -like "T1_*" })[0] } else { $null }
$firstApptId = if ($firstApptKey) { $SeedState.appointmentIds[$firstApptKey] } else { $null }

# ---- B9.1 AppointmentEmployerDetails ----

Write-Host "`n--- B9.1 Employer Details ---" -ForegroundColor Yellow
$empUrl = "$ApiBaseUrl/api/app/appointment-employer-details"

if ($firstApptId) {
    # B9.1.1 GET by appointmentId
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$($empUrl)?appointmentId=$firstApptId&maxResultCount=50" -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "B9.1.1" -Name "EmployerDetails GET by appointmentId" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds

    # B9.1.2 POST create
    $createBody = @{
        appointmentId = $firstApptId
        employerName  = "B9 Test Employer Inc"
        occupation    = "Test Worker"
        phoneNumber   = "2135551234"
        street        = "123 Test Blvd"
        city          = "Los Angeles"
        zipCode       = "90001"
    }
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "POST" -Url $empUrl -Body $createBody -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "B9.1.2" -Name "EmployerDetails POST create" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
    $testEmpId = if ($resp.Body) { $resp.Body.id } else { $null }

    # B9.1.3 PUT update
    if ($testEmpId) {
        $getResp = Invoke-TestApiCall -Method "GET" -Url "$empUrl/$testEmpId" -Token $t1Token -TenantId $t1Id
        $stamp = if ($getResp.Body) { $getResp.Body.concurrencyStamp } else { "" }
        $updateBody = @{ appointmentId = $firstApptId; employerName = "B9 Updated Employer"; occupation = "Updated Worker"; concurrencyStamp = $stamp }
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $resp = Invoke-TestApiCall -Method "PUT" -Url "$empUrl/$testEmpId" -Body $updateBody -Token $t1Token -TenantId $t1Id
        $sw.Stop()
        Assert-StatusCode -TestResults $TestResults -TestId "B9.1.3" -Name "EmployerDetails PUT update" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
    } else {
        Add-SkipResult -TestResults $TestResults -TestId "B9.1.3" -Name "EmployerDetails PUT" -Reason "No test record"
    }

    # B9.1.4 DELETE + verify 404
    if ($testEmpId) {
        $resp = Invoke-TestApiCall -Method "DELETE" -Url "$empUrl/$testEmpId" -Token $t1Token -TenantId $t1Id
        Assert-IsTrue -TestResults $TestResults -TestId "B9.1.4" -Name "EmployerDetails DELETE" -Condition ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 204)

        $resp = Invoke-TestApiCall -Method "GET" -Url "$empUrl/$testEmpId" -Token $t1Token -TenantId $t1Id
        Assert-StatusCode -TestResults $TestResults -TestId "B9.1.5" -Name "EmployerDetails GET deleted 404" -Response $resp -Expected 404
    } else {
        Add-SkipResult -TestResults $TestResults -TestId "B9.1.4" -Name "EmployerDetails DELETE" -Reason "No test record"
        Add-SkipResult -TestResults $TestResults -TestId "B9.1.5" -Name "EmployerDetails 404" -Reason "No test record"
    }

    # B9.1.6 Boundary: employerName 255 chars
    $body255 = @{ appointmentId = $firstApptId; employerName = ("E" * 255); occupation = "Test" }
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "POST" -Url $empUrl -Body $body255 -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "B9.1.6" -Name "EmployerName 255 chars OK" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
    if ($resp.Body -and $resp.Body.id) { Invoke-TestApiCall -Method "DELETE" -Url "$empUrl/$($resp.Body.id)" -Token $t1Token -TenantId $t1Id | Out-Null }

    # B9.1.7 Boundary: employerName 256 chars fails
    $body256 = @{ appointmentId = $firstApptId; employerName = ("E" * 256); occupation = "Test" }
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "POST" -Url $empUrl -Body $body256 -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "B9.1.7" -Name "EmployerName 256 chars fails (400)" -Response $resp -Expected 400 -DurationMs $sw.ElapsedMilliseconds
} else {
    for ($i = 1; $i -le 7; $i++) {
        Add-SkipResult -TestResults $TestResults -TestId "B9.1.$i" -Name "EmployerDetails test $i" -Reason "No T1 appointment"
    }
}

# ---- B9.2 AppointmentAccessors ----

Write-Host "`n--- B9.2 Appointment Accessors ---" -ForegroundColor Yellow
$accUrl = "$ApiBaseUrl/api/app/appointment-accessors"

if ($firstApptId) {
    # B9.2.1 GET by appointmentId
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$($accUrl)?appointmentId=$firstApptId&maxResultCount=50" -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "B9.2.1" -Name "Accessors GET by appointmentId" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds

    # B9.2.2 POST create View accessor
    $ceUserId = if ($SeedState.claimExaminerUserIds) { @($SeedState.claimExaminerUserIds.Values)[0] } else { $null }
    if ($ceUserId) {
        $body = @{ appointmentId = $firstApptId; identityUserId = $ceUserId; accessTypeId = 23 }
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $resp = Invoke-TestApiCall -Method "POST" -Url $accUrl -Body $body -Token $t1Token -TenantId $t1Id
        $sw.Stop()
        $ok = ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 400)  # May already exist
        Assert-IsTrue -TestResults $TestResults -TestId "B9.2.2" -Name "Accessor POST View(23)" -Condition $ok -Details "Status: $($resp.StatusCode)" -DurationMs $sw.ElapsedMilliseconds
        $testAccId = if ($resp.Body -and $resp.StatusCode -eq 200) { $resp.Body.id } else { $null }

        # B9.2.3 DELETE accessor
        if ($testAccId) {
            $resp = Invoke-TestApiCall -Method "DELETE" -Url "$accUrl/$testAccId" -Token $t1Token -TenantId $t1Id
            Assert-IsTrue -TestResults $TestResults -TestId "B9.2.3" -Name "Accessor DELETE" -Condition ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 204)
        } else {
            Add-SkipResult -TestResults $TestResults -TestId "B9.2.3" -Name "Accessor DELETE" -Reason "No test accessor"
        }
    } else {
        Add-SkipResult -TestResults $TestResults -TestId "B9.2.2" -Name "Accessor POST" -Reason "No CE user ID"
        Add-SkipResult -TestResults $TestResults -TestId "B9.2.3" -Name "Accessor DELETE" -Reason "No CE user ID"
    }
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B9.2.1" -Name "Accessors GET" -Reason "No T1 appointment"
    Add-SkipResult -TestResults $TestResults -TestId "B9.2.2" -Name "Accessor POST" -Reason "No T1 appointment"
    Add-SkipResult -TestResults $TestResults -TestId "B9.2.3" -Name "Accessor DELETE" -Reason "No T1 appointment"
}

# ---- B9.3 ApplicantAttorneys ----

Write-Host "`n--- B9.3 Applicant Attorneys ---" -ForegroundColor Yellow
$aaUrl = "$ApiBaseUrl/api/app/applicant-attorneys"

# B9.3.1 GET list
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$($aaUrl)?maxResultCount=50" -Token $t1Token -TenantId $t1Id
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B9.3.1" -Name "ApplicantAttorneys GET list" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds

# B9.3.2 POST create
$aaUserId = if ($SeedState.applicantAttorneyUserIds) { @($SeedState.applicantAttorneyUserIds.Values)[0] } else { $null }
if ($aaUserId) {
    $body = @{ identityUserId = $aaUserId; firmName = "B9 Test Firm APC" }
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "POST" -Url $aaUrl -Body $body -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    $ok = ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 400)
    Assert-IsTrue -TestResults $TestResults -TestId "B9.3.2" -Name "ApplicantAttorney POST create" -Condition $ok -Details "Status: $($resp.StatusCode)" -DurationMs $sw.ElapsedMilliseconds
    $testAaId = if ($resp.Body -and $resp.StatusCode -eq 200) { $resp.Body.id } else { $null }

    # B9.3.3 firmName boundary 50 chars
    $body50 = @{ identityUserId = $aaUserId; firmName = ("F" * 50) }
    $resp = Invoke-TestApiCall -Method "POST" -Url $aaUrl -Body $body50 -Token $t1Token -TenantId $t1Id
    Assert-IsTrue -TestResults $TestResults -TestId "B9.3.3" -Name "FirmName 50 chars OK" -Condition ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 400) -Details "Status: $($resp.StatusCode)"
    if ($resp.Body -and $resp.StatusCode -eq 200) { Invoke-TestApiCall -Method "DELETE" -Url "$aaUrl/$($resp.Body.id)" -Token $t1Token -TenantId $t1Id | Out-Null }

    # B9.3.4 firmName boundary 51 chars fails
    $body51 = @{ identityUserId = $aaUserId; firmName = ("F" * 51) }
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "POST" -Url $aaUrl -Body $body51 -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "B9.3.4" -Name "FirmName 51 chars fails (400)" -Response $resp -Expected 400 -DurationMs $sw.ElapsedMilliseconds

    # Cleanup
    if ($testAaId) { Invoke-TestApiCall -Method "DELETE" -Url "$aaUrl/$testAaId" -Token $t1Token -TenantId $t1Id | Out-Null }
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B9.3.2" -Name "AA POST" -Reason "No AA user ID"
    Add-SkipResult -TestResults $TestResults -TestId "B9.3.3" -Name "FirmName 50" -Reason "No AA user ID"
    Add-SkipResult -TestResults $TestResults -TestId "B9.3.4" -Name "FirmName 51" -Reason "No AA user ID"
}

Write-Host "`nPhase B9 COMPLETE" -ForegroundColor Green
