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

function ConvertTo-CheckResult {
    param([string]$Check, [string]$Status)
    return @{ Check = $Check; Status = $Status }
}

function Write-CheckFailure {
    param([string]$Message, $ErrorRecord = $null, [string]$Hint = "")
    Write-Host "  FAIL: $Message" -ForegroundColor Red
    if ($ErrorRecord) { Write-Host "  Error: $($ErrorRecord.Exception.Message)" -ForegroundColor Yellow }
    if ($Hint) { Write-Host "  Hint: $Hint" -ForegroundColor Yellow }
}

function Test-AuthServerReachable {
    param([string]$AuthServerUrl)

    Write-Host "`n[1/5] Checking AuthServer at $AuthServerUrl..." -ForegroundColor Cyan
    $oidcUrl = "$AuthServerUrl/.well-known/openid-configuration"
    try {
        $response = Invoke-RestMethod -Uri $oidcUrl -Method Get -TimeoutSec 10
    } catch {
        Write-CheckFailure -Message "Cannot reach AuthServer at $oidcUrl" -ErrorRecord $_
        return ConvertTo-CheckResult "AuthServer" "FAIL"
    }
    if ($response.token_endpoint) {
        Write-Host "  PASS: AuthServer is running. Token endpoint: $($response.token_endpoint)" -ForegroundColor Green
        return ConvertTo-CheckResult "AuthServer" "PASS"
    }
    Write-CheckFailure -Message "AuthServer responded but missing token_endpoint"
    return ConvertTo-CheckResult "AuthServer" "FAIL"
}

function Test-ApiHostReachable {
    <# Records nothing when the host answers with an empty body, as this check always has. #>
    param([string]$ApiBaseUrl)

    Write-Host "`n[2/5] Checking API Host at $ApiBaseUrl..." -ForegroundColor Cyan
    $configUrl = "$ApiBaseUrl/api/abp/application-configuration"
    try {
        $response = Invoke-RestMethod -Uri $configUrl -Method Get -TimeoutSec 10
    } catch {
        Write-CheckFailure -Message "Cannot reach API Host at $configUrl" -ErrorRecord $_
        return ConvertTo-CheckResult "API Host" "FAIL"
    }
    if ($null -ne $response) {
        Write-Host "  PASS: API Host is running and responding" -ForegroundColor Green
        return ConvertTo-CheckResult "API Host" "PASS"
    }
}

function Test-LocalDbRunning {
    $info = & sqllocaldb info MSSQLLocalDB 2>&1
    return (($info -join "`n") -match "State:\s*Running")
}

function Test-LocalDbAvailable {
    Write-Host "`n[3/5] Checking SQL Server LocalDB..." -ForegroundColor Cyan
    try {
        if (Test-LocalDbRunning) {
            Write-Host "  PASS: MSSQLLocalDB is running" -ForegroundColor Green
            return ConvertTo-CheckResult "LocalDB" "PASS"
        }
        Write-Host "  WARN: MSSQLLocalDB is not running. Attempting to start..." -ForegroundColor Yellow
        & sqllocaldb start MSSQLLocalDB 2>&1 | Out-Null
        Start-Sleep -Seconds 2
        if (Test-LocalDbRunning) {
            Write-Host "  PASS: MSSQLLocalDB started successfully" -ForegroundColor Green
            return ConvertTo-CheckResult "LocalDB" "PASS"
        }
        Write-CheckFailure -Message "Could not start MSSQLLocalDB"
        return ConvertTo-CheckResult "LocalDB" "FAIL"
    } catch {
        Write-CheckFailure -Message "sqllocaldb command not found or failed" -ErrorRecord $_
        return ConvertTo-CheckResult "LocalDB" "FAIL"
    }
}

function Get-HostAdminToken {
    <# Returns @{ Result = ...; Token = ... }; Token is $null when acquisition failed. #>
    param([string]$AuthServerUrl)

    Write-Host "`n[4/5] Acquiring host admin token..." -ForegroundColor Cyan
    try {
        $token = Get-AuthToken -Username "admin@abp.io" -Password $env:TEST_PASSWORD -AuthServerUrl $AuthServerUrl
    } catch {
        Write-CheckFailure -Message "Could not acquire host admin token" -ErrorRecord $_ `
            -Hint "Ensure DbMigrator has been run to seed admin user"
        return @{ Result = (ConvertTo-CheckResult "Admin Token" "FAIL"); Token = $null }
    }
    if ($token) {
        Write-Host "  PASS: Host admin token acquired successfully" -ForegroundColor Green
        return @{ Result = (ConvertTo-CheckResult "Admin Token" "PASS"); Token = $token }
    }
    Write-CheckFailure -Message "Token acquisition returned empty"
    return @{ Result = (ConvertTo-CheckResult "Admin Token" "FAIL"); Token = $token }
}

function Test-ApiFunctional {
    param([string]$ApiBaseUrl, [string]$Token)

    Write-Host "`n[5/5] Testing API functionality (GET states)..." -ForegroundColor Cyan
    try {
        Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/states?maxResultCount=1" -Token $Token | Out-Null
    } catch {
        Write-CheckFailure -Message "API functional test failed" -ErrorRecord $_
        return ConvertTo-CheckResult "API Functional" "FAIL"
    }
    Write-Host "  PASS: API returned response for states endpoint" -ForegroundColor Green
    return ConvertTo-CheckResult "API Functional" "PASS"
}

function Write-CheckSummary {
    param([array]$Results)

    Write-Host "`n========================================" -ForegroundColor White
    Write-Host "PREREQUISITE CHECK SUMMARY" -ForegroundColor White
    Write-Host "========================================" -ForegroundColor White
    foreach ($r in $Results) {
        $color = if ($r.Status -eq "PASS") { "Green" } else { "Red" }
        Write-Host "  [$($r.Status)] $($r.Check)" -ForegroundColor $color
    }
    Write-Host "========================================`n" -ForegroundColor White
}

function Test-Prerequisites {
    param(
        [string]$AuthServerUrl,
        [string]$ApiBaseUrl
    )

    $results = @()
    $results += @(Test-AuthServerReachable -AuthServerUrl $AuthServerUrl)
    $results += @(Test-ApiHostReachable -ApiBaseUrl $ApiBaseUrl)
    $results += @(Test-LocalDbAvailable)
    $admin = Get-HostAdminToken -AuthServerUrl $AuthServerUrl
    $results += @($admin.Result)
    $results += @(Test-ApiFunctional -ApiBaseUrl $ApiBaseUrl -Token $admin.Token)

    Write-CheckSummary -Results $results

    if (@($results | Where-Object { $_.Status -ne "PASS" }).Count -gt 0) {
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
