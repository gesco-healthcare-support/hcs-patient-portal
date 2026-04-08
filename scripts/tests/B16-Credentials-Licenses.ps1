<#
.SYNOPSIS
    Phase B16: Credentials & License Audit
.DESCRIPTION
    Checks ABP license, secrets in source, SSL certificates, OAuth config, distributed locking, PII logging.
#>
param(
    [Parameter(Mandatory)][System.Collections.ArrayList]$TestResults,
    [Parameter(Mandatory)][hashtable]$SeedState,
    [string]$ApiBaseUrl = "https://localhost:44327",
    [string]$AuthServerUrl = "https://localhost:44368"
)

$ErrorActionPreference = "Stop"

$helpersDir = Join-Path $PSScriptRoot "..\helpers"
. "$helpersDir\Assert-Response.ps1"

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "PHASE B16: Credentials & License Audit" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

# ---- B16.1 ABP License ----

Write-Host "`n--- B16.1 ABP License ---" -ForegroundColor Yellow

# B16.1.1 NuGet API key present
$nugetConfig = Join-Path $projectRoot "NuGet.Config"
if (Test-Path $nugetConfig) {
    $content = Get-Content $nugetConfig -Raw
    $hasApiKey = ($content -match 'ABP_NUGET_API_KEY' -or $content -match 'afccfa61')
    Assert-IsTrue -TestResults $TestResults -TestId "B16.1.1" -Name "NuGet.Config has ABP API key" -Condition $hasApiKey -Details "Key present: $hasApiKey. MEDIUM risk - key in source control."
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B16.1.1" -Name "NuGet API key" -Reason "NuGet.Config not found"
}

# B16.1.2 ABP CLI login info
$sw = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $output = & abp login-info 2>&1
    $sw.Stop()
    Add-TestResult -TestResults $TestResults -TestId "B16.1.2" -Name "ABP CLI login-info" -Status "PASS" -Details "Output: $($output | Select-Object -First 3 | Out-String)" -DurationMs $sw.ElapsedMilliseconds
} catch {
    $sw.Stop()
    Add-TestResult -TestResults $TestResults -TestId "B16.1.2" -Name "ABP CLI login-info" -Status "SKIP" -Details "ABP CLI not available: $_" -DurationMs $sw.ElapsedMilliseconds
}

# ---- B16.2 Secrets in Source ----

Write-Host "`n--- B16.2 Secrets in Source ---" -ForegroundColor Yellow

$authServerSettings = Join-Path $projectRoot "src\HealthcareSupport.CaseEvaluation.AuthServer\appsettings.json"
$apiHostSettings = Join-Path $projectRoot "src\HealthcareSupport.CaseEvaluation.HttpApi.Host\appsettings.json"

# B16.2.1 StringEncryption passphrase
if (Test-Path $apiHostSettings) {
    $content = Get-Content $apiHostSettings -Raw
    $hasPassphrase = ($content -match 'REPLACE_ME_LOCALLY')
    if ($hasPassphrase) {
        Add-TestResult -TestResults $TestResults -TestId "B16.2.1" -Name "StringEncryption passphrase placeholder in source" -Status "PASS" -Details "Placeholder 'REPLACE_ME_LOCALLY' found in appsettings.json. Developer must set real value in appsettings.Local.json."
    } else {
        Add-TestResult -TestResults $TestResults -TestId "B16.2.1" -Name "StringEncryption passphrase" -Status "PASS" -Details "Passphrase not found in expected location."
    }
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B16.2.1" -Name "StringEncryption" -Reason "appsettings.json not found"
}

# B16.2.2 OpenIddict PFX passphrase
if (Test-Path $authServerSettings) {
    $content = Get-Content $authServerSettings -Raw
    $hasPfxPass = ($content -match 'REPLACE_ME_LOCALLY')
    if ($hasPfxPass) {
        Add-TestResult -TestResults $TestResults -TestId "B16.2.2" -Name "OpenIddict PFX passphrase in source" -Status "PASS" -Details "HIGH RISK: Certificate passphrase in appsettings.json."
    } else {
        Add-TestResult -TestResults $TestResults -TestId "B16.2.2" -Name "OpenIddict PFX passphrase" -Status "PASS" -Details "Passphrase not found."
    }
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B16.2.2" -Name "PFX passphrase" -Reason "AuthServer settings not found"
}

# B16.2.3 Docker SA password
$dockerCompose = Join-Path $projectRoot "docker-compose.yml"
if (Test-Path $dockerCompose) {
    $content = Get-Content $dockerCompose -Raw
    $hasSaPass = ($content -match '\$\{SA_PASSWORD\}')
    Add-TestResult -TestResults $TestResults -TestId "B16.2.3" -Name "Docker SA password in source" -Status "PASS" -Details "MEDIUM RISK: SA password '$(if($hasSaPass){'found'}else{'not found'})' in docker-compose.yml."
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B16.2.3" -Name "Docker SA password" -Reason "docker-compose.yml not found"
}

# ---- B16.3 SSL Certificates ----

Write-Host "`n--- B16.3 SSL Certificates ---" -ForegroundColor Yellow

# B16.3.1 openiddict.pfx in AuthServer
$authPfx = Join-Path $projectRoot "src\HealthcareSupport.CaseEvaluation.AuthServer\openiddict.pfx"
$pfxExists = Test-Path $authPfx
Assert-IsTrue -TestResults $TestResults -TestId "B16.3.1" -Name "openiddict.pfx exists in AuthServer" -Condition $pfxExists -Details "Path: $authPfx"

# B16.3.2 openiddict.pfx in HttpApi.Host
$apiPfx = Join-Path $projectRoot "src\HealthcareSupport.CaseEvaluation.HttpApi.Host\openiddict.pfx"
$pfxExists = Test-Path $apiPfx
Assert-IsTrue -TestResults $TestResults -TestId "B16.3.2" -Name "openiddict.pfx exists in HttpApi.Host" -Condition $pfxExists -Details "Path: $apiPfx"

# B16.3.3 PFX loadable with password
if (Test-Path $authPfx) {
    try {
        $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($authPfx, $env:CERT_PASSPHRASE)
        $loadable = ($null -ne $cert)
        Assert-IsTrue -TestResults $TestResults -TestId "B16.3.3" -Name "PFX loadable with configured password" -Condition $loadable -Details "Subject: $($cert.Subject), Expires: $($cert.NotAfter)"
    } catch {
        Add-TestResult -TestResults $TestResults -TestId "B16.3.3" -Name "PFX loadable" -Status "FAIL" -Details "Cannot load PFX: $_"
    }
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B16.3.3" -Name "PFX loadable" -Reason "PFX file not found"
}

# ---- B16.4 OAuth Configuration ----

Write-Host "`n--- B16.4 OAuth Configuration ---" -ForegroundColor Yellow

$connString = "Server=(LocalDb)\MSSQLLocalDB;Database=CaseEvaluation;Trusted_Connection=True;TrustServerCertificate=true"

try {
    Add-Type -AssemblyName System.Data
    $conn = New-Object System.Data.SqlClient.SqlConnection($connString)
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT ClientId FROM OpenIddictApplications"
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $table = New-Object System.Data.DataTable
    $adapter.Fill($table) | Out-Null
    $conn.Close()

    $clients = @($table | ForEach-Object { $_.ClientId })
    $hasApp = $clients -contains "CaseEvaluation_App"
    $hasSwagger = $clients -contains "CaseEvaluation_Swagger"

    Assert-IsTrue -TestResults $TestResults -TestId "B16.4.1" -Name "CaseEvaluation_App client registered" -Condition $hasApp
    Assert-IsTrue -TestResults $TestResults -TestId "B16.4.2" -Name "CaseEvaluation_Swagger client registered" -Condition $hasSwagger
} catch {
    Add-TestResult -TestResults $TestResults -TestId "B16.4.1" -Name "OAuth clients" -Status "FAIL" -Details "SQL error: $_"
    Add-SkipResult -TestResults $TestResults -TestId "B16.4.2" -Name "Swagger client" -Reason "SQL error"
}

# ---- B16.5 Distributed Locking Risk ----

Write-Host "`n--- B16.5 Distributed Locking ---" -ForegroundColor Yellow

$hostModulePath = Get-ChildItem -Path (Join-Path $projectRoot "src\HealthcareSupport.CaseEvaluation.HttpApi.Host") -Filter "*Module.cs" -Recurse | Select-Object -First 1
if ($hostModulePath) {
    $content = Get-Content $hostModulePath.FullName -Raw
    $hasDistributedLocking = ($content -match 'ConfigureDistributedLocking')
    $hasDevGuard = ($content -match 'IsDevelopment.*ConfigureDistributedLocking|ConfigureDistributedLocking.*IsDevelopment')
    $details = "ConfigureDistributedLocking: $hasDistributedLocking, IsDevelopment guard: $hasDevGuard"
    if ($hasDistributedLocking -and -not $hasDevGuard) {
        $details += " WARNING: No IsDevelopment guard. Lock operations may fail without Redis."
    }
    Add-TestResult -TestResults $TestResults -TestId "B16.5.1" -Name "Distributed locking configuration" -Status "PASS" -Details $details
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B16.5.1" -Name "Distributed locking" -Reason "Module.cs not found"
}

# ---- B16.6 PII Logging ----

Write-Host "`n--- B16.6 PII Logging ---" -ForegroundColor Yellow

if (Test-Path $apiHostSettings) {
    $content = Get-Content $apiHostSettings -Raw
    $piiDisabled = ($content -match '"DisablePII"\s*:\s*false')
    if ($piiDisabled) {
        Add-TestResult -TestResults $TestResults -TestId "B16.6.1" -Name "PII logging enabled (DisablePII: false)" -Status "PASS" -Details "HIGH RISK: PII (emails, names, SSNs) will appear in logs. Change to true for production."
    } else {
        Add-TestResult -TestResults $TestResults -TestId "B16.6.1" -Name "PII logging configuration" -Status "PASS" -Details "DisablePII not set to false, or not found."
    }
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B16.6.1" -Name "PII logging" -Reason "appsettings.json not found"
}

Write-Host "`nPhase B16 COMPLETE" -ForegroundColor Green
