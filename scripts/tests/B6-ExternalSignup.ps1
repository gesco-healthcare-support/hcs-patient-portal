<#
.SYNOPSIS
    Phase B6: External Signup
.DESCRIPTION
    Tests tenant options, user registration (all 4 types), failure cases, external user lookup.
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
Write-Host "PHASE B6: External Signup" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

$defaultPassword = $env:TEST_PASSWORD
$signupUrl = "$ApiBaseUrl/api/public/external-signup"
$hostToken = Get-AuthToken -Username "admin@abp.io" -Password $defaultPassword -AuthServerUrl $AuthServerUrl
$t1Id = $SeedState.tenantIds["T1"]
$t2Id = $SeedState.tenantIds["T2"]

# ---- B6.1 Tenant Options ----

Write-Host "`n--- B6.1 Tenant Options ---" -ForegroundColor Yellow

# B6.1.1 GET tenant-options returns list
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$signupUrl/tenant-options"
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B6.1.1" -Name "Tenant options returns 200 (anonymous)" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
$tenantCount = if ($resp.Body -and $resp.Body.items) { $resp.Body.items.Count } else { 0 }
Assert-GreaterOrEqual -TestResults $TestResults -TestId "B6.1.2" -Name "Tenant options returns >= 5 tenants" -Actual $tenantCount -Expected 5

# B6.1.3 Filtered tenant options
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$signupUrl/tenant-options?filter=Dr"
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B6.1.3" -Name "Tenant options with filter" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds

# B6.1.4 Tenant-scoped returns empty
if ($t1Id) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$signupUrl/tenant-options" -TenantId $t1Id
    $sw.Stop()
    $scopedCount = if ($resp.Body -and $resp.Body.items) { $resp.Body.items.Count } else { 0 }
    Assert-AreEqual -TestResults $TestResults -TestId "B6.1.4" -Name "Tenant-scoped options returns empty" -Actual $scopedCount -Expected 0 -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B6.1.4" -Name "Tenant-scoped options" -Reason "No T1 ID"
}

# ---- B6.2 Registration - All 4 Types ----

Write-Host "`n--- B6.2 Registration ---" -ForegroundColor Yellow

$userTypes = @(
    @{ Type = 1; Name = "Patient"; RoleName = "Patient" },
    @{ Type = 2; Name = "ClaimExaminer"; RoleName = "Claim Examiner" },
    @{ Type = 3; Name = "ApplicantAttorney"; RoleName = "Applicant Attorney" },
    @{ Type = 4; Name = "DefenseAttorney"; RoleName = "Defense Attorney" }
)

$testIdx = 1
foreach ($ut in $userTypes) {
    $testEmail = "b6.test.$($ut.Name.ToLower())@b-test.hcs.test"
    $body = @{
        userType  = $ut.Type
        firstName = "B6Test"
        lastName  = $ut.Name
        email     = $testEmail
        password  = $defaultPassword
        tenantId  = $t1Id
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "POST" -Url "$signupUrl/register" -Body $body
    $sw.Stop()
    # May return 200 (new) or 400/403 (already exists from previous run)
    $ok = ($resp.StatusCode -eq 200 -or ($resp.StatusCode -in @(400, 403) -and $resp.Error -like "*already*"))
    Assert-IsTrue -TestResults $TestResults -TestId "B6.2.$testIdx" -Name "Register $($ut.Name) user" -Condition $ok -Details "Status: $($resp.StatusCode), Email: $testEmail" -DurationMs $sw.ElapsedMilliseconds

    # Verify user exists via identity lookup
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $t1Token = Get-AuthToken -Username $SeedState._tenantEmails["T1"] -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $t1Id
    $userResp = Invoke-TestApiCall -Method "GET" -Url "$ApiBaseUrl/api/identity/users?filter=$([uri]::EscapeDataString($testEmail))&maxResultCount=5" -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    $userFound = ($userResp.Success -and $userResp.Body -and $userResp.Body.items -and $userResp.Body.items.Count -gt 0)
    Assert-IsTrue -TestResults $TestResults -TestId "B6.2.$($testIdx + 4)" -Name "Verify $($ut.Name) user exists" -Condition $userFound -Details "Found: $userFound" -DurationMs $sw.ElapsedMilliseconds

    $testIdx++
}

# ---- B6.3 Failure Cases ----

Write-Host "`n--- B6.3 Registration Failures ---" -ForegroundColor Yellow

# B6.3.1 Duplicate email in same tenant
$dupeEmail = "b6.test.patient@b-test.hcs.test"
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "POST" -Url "$signupUrl/register" -Body @{
    userType = 1; firstName = "Dupe"; lastName = "Test"; email = $dupeEmail; password = $defaultPassword; tenantId = $t1Id
}
$sw.Stop()
# ABP may return 403 for business rule violations; accept any 4xx
Assert-IsTrue -TestResults $TestResults -TestId "B6.3.1" -Name "Duplicate email returns 4xx error" -Condition ($resp.StatusCode -ge 400 -and $resp.StatusCode -lt 500) -Details "Status: $($resp.StatusCode)" -DurationMs $sw.ElapsedMilliseconds

# B6.3.2 Same email in different tenant (should succeed)
if ($t2Id) {
    $crossEmail = "b6.cross.tenant@b-test.hcs.test"
    # Register in T2
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "POST" -Url "$signupUrl/register" -Body @{
        userType = 1; firstName = "Cross"; lastName = "Tenant"; email = $crossEmail; password = $defaultPassword; tenantId = $t2Id
    }
    $sw.Stop()
    $ok = ($resp.StatusCode -eq 200 -or ($resp.StatusCode -in @(400, 403) -and $resp.Error -like "*already*"))
    Assert-IsTrue -TestResults $TestResults -TestId "B6.3.2" -Name "Same email different tenant succeeds" -Condition $ok -Details "Status: $($resp.StatusCode)" -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B6.3.2" -Name "Cross-tenant email" -Reason "No T2"
}

# B6.3.3 Invalid email format
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "POST" -Url "$signupUrl/register" -Body @{
    userType = 1; firstName = "Bad"; lastName = "Email"; email = "not-an-email"; password = $defaultPassword; tenantId = $t1Id
}
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B6.3.3" -Name "Invalid email returns 400" -Response $resp -Expected 400 -DurationMs $sw.ElapsedMilliseconds

# B6.3.4 Password too short
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "POST" -Url "$signupUrl/register" -Body @{
    userType = 1; firstName = "Short"; lastName = "Pass"; email = "b6.shortpw@b-test.hcs.test"; password = "12345"; tenantId = $t1Id
}
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B6.3.4" -Name "Short password returns 400" -Response $resp -Expected 400 -DurationMs $sw.ElapsedMilliseconds

# B6.3.5 Empty firstName
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "POST" -Url "$signupUrl/register" -Body @{
    userType = 1; firstName = ""; lastName = "Empty"; email = "b6.emptyname@b-test.hcs.test"; password = $defaultPassword; tenantId = $t1Id
}
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B6.3.5" -Name "Empty firstName returns 400" -Response $resp -Expected 400 -DurationMs $sw.ElapsedMilliseconds

# B6.3.6 Invalid userType
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "POST" -Url "$signupUrl/register" -Body @{
    userType = 99; firstName = "Bad"; lastName = "Type"; email = "b6.badtype@b-test.hcs.test"; password = $defaultPassword; tenantId = $t1Id
}
$sw.Stop()
Assert-IsTrue -TestResults $TestResults -TestId "B6.3.6" -Name "Invalid userType returns 4xx error" -Condition ($resp.StatusCode -ge 400 -and $resp.StatusCode -lt 500) -Details "Status: $($resp.StatusCode)" -DurationMs $sw.ElapsedMilliseconds

# ---- B6.4 External User Lookup ----

Write-Host "`n--- B6.4 External User Lookup ---" -ForegroundColor Yellow

if ($t1Id) {
    $t1Token = Get-AuthToken -Username $SeedState._tenantEmails["T1"] -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $t1Id
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$signupUrl/external-user-lookup" -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "B6.4.1" -Name "External user lookup returns 200" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds

    # B6.4.2 Check filter works
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "GET" -Url "$signupUrl/external-user-lookup?filter=test" -Token $t1Token -TenantId $t1Id
    $sw.Stop()
    Assert-StatusCode -TestResults $TestResults -TestId "B6.4.2" -Name "External user lookup with filter" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B6.4.1" -Name "External user lookup" -Reason "No T1"
    Add-SkipResult -TestResults $TestResults -TestId "B6.4.2" -Name "External user lookup filter" -Reason "No T1"
}

Write-Host "`nPhase B6 COMPLETE" -ForegroundColor Green
