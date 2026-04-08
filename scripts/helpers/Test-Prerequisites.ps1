<#
.SYNOPSIS
    Verifies all prerequisites are met before running seed scripts.
.DESCRIPTION
    Checks that AuthServer, API Host, and LocalDB are running and accessible.
    Acquires a host admin token to prove the database was seeded by DbMigrator.
    Exits with error if any check fails.
#>
param(
    [string]$AuthServerUrl = "https://localhost:44368",
    [string]$ApiBaseUrl = "https://localhost:44327"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $PSScriptRoot
. "$PSScriptRoot\Get-AuthToken.ps1"
. "$PSScriptRoot\Invoke-ApiCall.ps1"

function Test-Prerequisites {
    param(
        [string]$AuthServerUrl,
        [string]$ApiBaseUrl
    )

    $allPassed = $true
    $results = @()

    # Check 1: AuthServer OpenID Configuration
    Write-Host "`n[1/5] Checking AuthServer at $AuthServerUrl..." -ForegroundColor Cyan
    try {
        $oidcUrl = "$AuthServerUrl/.well-known/openid-configuration"
        $response = Invoke-RestMethod -Uri $oidcUrl -Method Get -TimeoutSec 10
        if ($response.token_endpoint) {
            Write-Host "  PASS: AuthServer is running. Token endpoint: $($response.token_endpoint)" -ForegroundColor Green
            $results += @{ Check = "AuthServer"; Status = "PASS" }
        } else {
            Write-Host "  FAIL: AuthServer responded but missing token_endpoint" -ForegroundColor Red
            $allPassed = $false
            $results += @{ Check = "AuthServer"; Status = "FAIL" }
        }
    } catch {
        Write-Host "  FAIL: Cannot reach AuthServer at $oidcUrl" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Yellow
        $allPassed = $false
        $results += @{ Check = "AuthServer"; Status = "FAIL" }
    }

    # Check 2: API Host Application Configuration
    Write-Host "`n[2/5] Checking API Host at $ApiBaseUrl..." -ForegroundColor Cyan
    try {
        $configUrl = "$ApiBaseUrl/api/abp/application-configuration"
        $response = Invoke-RestMethod -Uri $configUrl -Method Get -TimeoutSec 10
        if ($null -ne $response) {
            Write-Host "  PASS: API Host is running and responding" -ForegroundColor Green
            $results += @{ Check = "API Host"; Status = "PASS" }
        }
    } catch {
        Write-Host "  FAIL: Cannot reach API Host at $configUrl" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Yellow
        $allPassed = $false
        $results += @{ Check = "API Host"; Status = "FAIL" }
    }

    # Check 3: LocalDB Instance
    Write-Host "`n[3/5] Checking SQL Server LocalDB..." -ForegroundColor Cyan
    try {
        $localDbInfo = & sqllocaldb info MSSQLLocalDB 2>&1
        $localDbString = $localDbInfo -join "`n"
        if ($localDbString -match "State:\s*Running") {
            Write-Host "  PASS: MSSQLLocalDB is running" -ForegroundColor Green
            $results += @{ Check = "LocalDB"; Status = "PASS" }
        } else {
            Write-Host "  WARN: MSSQLLocalDB is not running. Attempting to start..." -ForegroundColor Yellow
            & sqllocaldb start MSSQLLocalDB 2>&1 | Out-Null
            Start-Sleep -Seconds 2
            $localDbInfo2 = & sqllocaldb info MSSQLLocalDB 2>&1
            $localDbString2 = $localDbInfo2 -join "`n"
            if ($localDbString2 -match "State:\s*Running") {
                Write-Host "  PASS: MSSQLLocalDB started successfully" -ForegroundColor Green
                $results += @{ Check = "LocalDB"; Status = "PASS" }
            } else {
                Write-Host "  FAIL: Could not start MSSQLLocalDB" -ForegroundColor Red
                $allPassed = $false
                $results += @{ Check = "LocalDB"; Status = "FAIL" }
            }
        }
    } catch {
        Write-Host "  FAIL: sqllocaldb command not found or failed" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Yellow
        $allPassed = $false
        $results += @{ Check = "LocalDB"; Status = "FAIL" }
    }

    # Check 4: Host Admin Token Acquisition
    Write-Host "`n[4/5] Acquiring host admin token..." -ForegroundColor Cyan
    try {
        $token = Get-AuthToken -Username "admin@abp.io" -Password $env:TEST_PASSWORD -AuthServerUrl $AuthServerUrl
        if ($token) {
            Write-Host "  PASS: Host admin token acquired successfully" -ForegroundColor Green
            $results += @{ Check = "Admin Token"; Status = "PASS" }
        } else {
            Write-Host "  FAIL: Token acquisition returned empty" -ForegroundColor Red
            $allPassed = $false
            $results += @{ Check = "Admin Token"; Status = "FAIL" }
        }
    } catch {
        Write-Host "  FAIL: Could not acquire host admin token" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "  Hint: Ensure DbMigrator has been run to seed admin user" -ForegroundColor Yellow
        $allPassed = $false
        $results += @{ Check = "Admin Token"; Status = "FAIL" }
    }

    # Check 5: API Functional Test
    Write-Host "`n[5/5] Testing API functionality (GET states)..." -ForegroundColor Cyan
    try {
        $statesResponse = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/states?maxResultCount=1" -Token $token
        Write-Host "  PASS: API returned response for states endpoint" -ForegroundColor Green
        $results += @{ Check = "API Functional"; Status = "PASS" }
    } catch {
        Write-Host "  FAIL: API functional test failed" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Yellow
        $allPassed = $false
        $results += @{ Check = "API Functional"; Status = "FAIL" }
    }

    # Summary
    Write-Host "`n========================================" -ForegroundColor White
    Write-Host "PREREQUISITE CHECK SUMMARY" -ForegroundColor White
    Write-Host "========================================" -ForegroundColor White
    foreach ($r in $results) {
        $color = if ($r.Status -eq "PASS") { "Green" } else { "Red" }
        Write-Host "  [$($r.Status)] $($r.Check)" -ForegroundColor $color
    }
    Write-Host "========================================`n" -ForegroundColor White

    if (-not $allPassed) {
        Write-Host "PREREQUISITES NOT MET. Fix the issues above before running seed scripts." -ForegroundColor Red
        return $false
    }

    Write-Host "ALL PREREQUISITES PASSED. Ready to seed." -ForegroundColor Green
    return $true
}

# Run if called directly
if ($MyInvocation.InvocationName -ne ".") {
    $result = Test-Prerequisites -AuthServerUrl $AuthServerUrl -ApiBaseUrl $ApiBaseUrl
    if (-not $result) { exit 1 }
}
