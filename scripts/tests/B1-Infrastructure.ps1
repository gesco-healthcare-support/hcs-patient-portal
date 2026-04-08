<#
.SYNOPSIS
    Phase B1: Infrastructure & Build Verification
.DESCRIPTION
    Tests: build compilation (optional), database schema, service startup, Redis config.
#>
param(
    [Parameter(Mandatory)][System.Collections.ArrayList]$TestResults,
    [Parameter(Mandatory)][hashtable]$SeedState,
    [string]$ApiBaseUrl = "https://localhost:44327",
    [string]$AuthServerUrl = "https://localhost:44368",
    [bool]$IncludeBuildTests = $false
)

$ErrorActionPreference = "Stop"

# Load helpers
$helpersDir = Join-Path $PSScriptRoot "..\helpers"
. "$helpersDir\Get-AuthToken.ps1"
. "$helpersDir\Invoke-ApiCall.ps1"
. "$helpersDir\Assert-Response.ps1"

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "PHASE B1: Infrastructure & Build Verification" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

# ---- B1.1 Build Tests (Optional) ----

Write-Host "`n--- B1.1 Build & Compilation ---" -ForegroundColor Yellow

if ($IncludeBuildTests) {
    $projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

    # B1.1.1 dotnet restore
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $output = & dotnet restore "$projectRoot" 2>&1
        $success = ($LASTEXITCODE -eq 0)
        $sw.Stop()
        Assert-IsTrue -TestResults $TestResults -TestId "B1.1.1" -Name "dotnet restore succeeds" -Condition $success -Details "Exit code: $LASTEXITCODE" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Add-TestResult -TestResults $TestResults -TestId "B1.1.1" -Name "dotnet restore succeeds" -Status "FAIL" -Details "Exception: $_" -DurationMs $sw.ElapsedMilliseconds
    }

    # B1.1.2 dotnet build
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $output = & dotnet build "$projectRoot" --no-restore 2>&1
        $success = ($LASTEXITCODE -eq 0)
        $sw.Stop()
        Assert-IsTrue -TestResults $TestResults -TestId "B1.1.2" -Name "dotnet build succeeds" -Condition $success -Details "Exit code: $LASTEXITCODE" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Add-TestResult -TestResults $TestResults -TestId "B1.1.2" -Name "dotnet build succeeds" -Status "FAIL" -Details "Exception: $_" -DurationMs $sw.ElapsedMilliseconds
    }

    # B1.1.3 Angular npm install
    $angularDir = Join-Path $projectRoot "angular"
    if (Test-Path $angularDir) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            Push-Location $angularDir
            $output = & npm install 2>&1
            $success = ($LASTEXITCODE -eq 0)
            Pop-Location
            $sw.Stop()
            Assert-IsTrue -TestResults $TestResults -TestId "B1.1.3" -Name "npm install succeeds" -Condition $success -Details "Exit code: $LASTEXITCODE" -DurationMs $sw.ElapsedMilliseconds
        } catch {
            Pop-Location
            $sw.Stop()
            Add-TestResult -TestResults $TestResults -TestId "B1.1.3" -Name "npm install succeeds" -Status "FAIL" -Details "Exception: $_" -DurationMs $sw.ElapsedMilliseconds
        }
    } else {
        Add-SkipResult -TestResults $TestResults -TestId "B1.1.3" -Name "npm install succeeds" -Reason "angular/ directory not found"
    }

    # B1.1.4 Angular build
    if (Test-Path $angularDir) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            Push-Location $angularDir
            $output = & npx ng build --configuration development 2>&1
            $success = ($LASTEXITCODE -eq 0)
            Pop-Location
            $sw.Stop()
            Assert-IsTrue -TestResults $TestResults -TestId "B1.1.4" -Name "Angular build succeeds" -Condition $success -Details "Exit code: $LASTEXITCODE" -DurationMs $sw.ElapsedMilliseconds
        } catch {
            Pop-Location
            $sw.Stop()
            Add-TestResult -TestResults $TestResults -TestId "B1.1.4" -Name "Angular build succeeds" -Status "FAIL" -Details "Exception: $_" -DurationMs $sw.ElapsedMilliseconds
        }
    } else {
        Add-SkipResult -TestResults $TestResults -TestId "B1.1.4" -Name "Angular build succeeds" -Reason "angular/ directory not found"
    }

    # B1.1.5 dotnet test
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $output = & dotnet test "$projectRoot" --no-build 2>&1
        $success = ($LASTEXITCODE -eq 0)
        $sw.Stop()
        Assert-IsTrue -TestResults $TestResults -TestId "B1.1.5" -Name "dotnet test passes" -Condition $success -Details "Exit code: $LASTEXITCODE" -DurationMs $sw.ElapsedMilliseconds
    } catch {
        $sw.Stop()
        Add-TestResult -TestResults $TestResults -TestId "B1.1.5" -Name "dotnet test passes" -Status "FAIL" -Details "Exception: $_" -DurationMs $sw.ElapsedMilliseconds
    }
} else {
    Write-Host "  Build tests skipped (use -IncludeBuildTests to enable)" -ForegroundColor DarkGray
    Add-SkipResult -TestResults $TestResults -TestId "B1.1.1" -Name "dotnet restore" -Reason "IncludeBuildTests not set"
    Add-SkipResult -TestResults $TestResults -TestId "B1.1.2" -Name "dotnet build" -Reason "IncludeBuildTests not set"
    Add-SkipResult -TestResults $TestResults -TestId "B1.1.3" -Name "npm install" -Reason "IncludeBuildTests not set"
    Add-SkipResult -TestResults $TestResults -TestId "B1.1.4" -Name "Angular build" -Reason "IncludeBuildTests not set"
    Add-SkipResult -TestResults $TestResults -TestId "B1.1.5" -Name "dotnet test" -Reason "IncludeBuildTests not set"
}

# ---- B1.2 Database Schema Verification ----

Write-Host "`n--- B1.2 Database Schema ---" -ForegroundColor Yellow

$connString = "Server=(LocalDb)\MSSQLLocalDB;Database=CaseEvaluation;Trusted_Connection=True;TrustServerCertificate=true"

function Invoke-SqlQuery {
    param([string]$Query, [string]$ConnectionString)
    Add-Type -AssemblyName System.Data
    $conn = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $Query
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
        $table = New-Object System.Data.DataTable
        $adapter.Fill($table) | Out-Null
        return ,$table
    } finally {
        $conn.Close()
    }
}

# B1.2.1 App tables exist
$sw = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $tables = Invoke-SqlQuery -Query "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME LIKE 'App%' ORDER BY TABLE_NAME" -ConnectionString $connString
    $tableNames = @($tables | ForEach-Object { $_.TABLE_NAME })
    $expectedTables = @("AppAppointmentAccessors", "AppAppointmentApplicantAttorneys", "AppAppointmentEmployerDetails",
        "AppAppointmentLanguages", "AppAppointmentStatuses", "AppAppointmentTypes", "AppAppointments",
        "AppApplicantAttorneys", "AppDoctorAppointmentType", "AppDoctorAvailabilities", "AppDoctorLocation",
        "AppDoctors", "AppLocations", "AppPatients", "AppStates", "AppWcabOffices")
    $missingTables = @()
    foreach ($t in $expectedTables) {
        if ($tableNames -notcontains $t) { $missingTables += $t }
    }
    $sw.Stop()
    Assert-IsTrue -TestResults $TestResults -TestId "B1.2.1" -Name "All 16 App* tables exist" -Condition ($missingTables.Count -eq 0) -Details "Found $($tableNames.Count) tables. Missing: $($missingTables -join ', ')" -DurationMs $sw.ElapsedMilliseconds
} catch {
    $sw.Stop()
    Add-TestResult -TestResults $TestResults -TestId "B1.2.1" -Name "All 16 App* tables exist" -Status "FAIL" -Details "SQL Error: $_" -DurationMs $sw.ElapsedMilliseconds
}

# B1.2.2 AbpUsers table exists
$sw = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $result = Invoke-SqlQuery -Query "SELECT COUNT(*) AS Cnt FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AbpUsers'" -ConnectionString $connString
    $sw.Stop()
    Assert-IsTrue -TestResults $TestResults -TestId "B1.2.2" -Name "AbpUsers table exists" -Condition ($result.Rows[0].Cnt -gt 0) -DurationMs $sw.ElapsedMilliseconds
} catch {
    $sw.Stop()
    Add-TestResult -TestResults $TestResults -TestId "B1.2.2" -Name "AbpUsers table exists" -Status "FAIL" -Details "SQL Error: $_" -DurationMs $sw.ElapsedMilliseconds
}

# B1.2.3 admin@abp.io user exists
$sw = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $result = Invoke-SqlQuery -Query "SELECT COUNT(*) AS Cnt FROM AbpUsers WHERE Email = 'admin@abp.io'" -ConnectionString $connString
    $sw.Stop()
    Assert-IsTrue -TestResults $TestResults -TestId "B1.2.3" -Name "admin@abp.io user exists" -Condition ($result.Rows[0].Cnt -gt 0) -DurationMs $sw.ElapsedMilliseconds
} catch {
    $sw.Stop()
    Add-TestResult -TestResults $TestResults -TestId "B1.2.3" -Name "admin@abp.io user exists" -Status "FAIL" -Details "SQL Error: $_" -DurationMs $sw.ElapsedMilliseconds
}

# B1.2.4 OpenIddict clients registered
$sw = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $result = Invoke-SqlQuery -Query "SELECT ClientId FROM OpenIddictApplications WHERE ClientId IN ('CaseEvaluation_App', 'CaseEvaluation_Swagger')" -ConnectionString $connString
    $clientIds = @($result | ForEach-Object { $_.ClientId })
    $hasApp = $clientIds -contains "CaseEvaluation_App"
    $hasSwagger = $clientIds -contains "CaseEvaluation_Swagger"
    $sw.Stop()
    Assert-IsTrue -TestResults $TestResults -TestId "B1.2.4" -Name "OpenIddict clients registered" -Condition ($hasApp -and $hasSwagger) -Details "CaseEvaluation_App: $hasApp, CaseEvaluation_Swagger: $hasSwagger" -DurationMs $sw.ElapsedMilliseconds
} catch {
    $sw.Stop()
    Add-TestResult -TestResults $TestResults -TestId "B1.2.4" -Name "OpenIddict clients registered" -Status "FAIL" -Details "SQL Error: $_" -DurationMs $sw.ElapsedMilliseconds
}

# B1.2.5 Custom roles seeded
$sw = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $result = Invoke-SqlQuery -Query "SELECT Name FROM AbpRoles WHERE Name IN ('Patient', 'Claim Examiner', 'Applicant Attorney', 'Defense Attorney')" -ConnectionString $connString
    $roleNames = @($result | ForEach-Object { $_.Name })
    $sw.Stop()
    Assert-GreaterOrEqual -TestResults $TestResults -TestId "B1.2.5" -Name "Custom roles seeded" -Actual $roleNames.Count -Expected 4 -DurationMs $sw.ElapsedMilliseconds
} catch {
    $sw.Stop()
    Add-TestResult -TestResults $TestResults -TestId "B1.2.5" -Name "Custom roles seeded" -Status "FAIL" -Details "SQL Error: $_" -DurationMs $sw.ElapsedMilliseconds
}

# ---- B1.3 Service Startup Verification ----

Write-Host "`n--- B1.3 Service Startup ---" -ForegroundColor Yellow

# B1.3.1 AuthServer OpenID configuration
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$AuthServerUrl/.well-known/openid-configuration"
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B1.3.1" -Name "AuthServer OpenID configuration" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds

# B1.3.2 AuthServer has token endpoint
if ($resp.Success -and $resp.Body) {
    Assert-IsNotNull -TestResults $TestResults -TestId "B1.3.2" -Name "AuthServer token_endpoint present" -Value $resp.Body.token_endpoint
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B1.3.2" -Name "AuthServer token_endpoint present" -Reason "OpenID config not available"
}

# B1.3.3 API Host application configuration
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$ApiBaseUrl/api/abp/application-configuration"
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B1.3.3" -Name "API Host application configuration" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds

# B1.3.4 Health check endpoint
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$ApiBaseUrl/health-status"
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B1.3.4" -Name "Health check endpoint" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds

# B1.3.5 Swagger UI loads
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$ApiBaseUrl/swagger/index.html"
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B1.3.5" -Name "Swagger UI loads" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds

# B1.3.6 JWKS endpoint
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "GET" -Url "$AuthServerUrl/.well-known/jwks"
$sw.Stop()
Assert-StatusCode -TestResults $TestResults -TestId "B1.3.6" -Name "JWKS endpoint responds" -Response $resp -Expected 200 -DurationMs $sw.ElapsedMilliseconds

# ---- B1.4 Redis Config ----

Write-Host "`n--- B1.4 Redis Configuration ---" -ForegroundColor Yellow

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

# B1.4.1 Check AuthServer appsettings
$authServerSettings = Join-Path $projectRoot "src\HealthcareSupport.CaseEvaluation.AuthServer\appsettings.json"
$sw = [System.Diagnostics.Stopwatch]::StartNew()
if (Test-Path $authServerSettings) {
    $content = Get-Content $authServerSettings -Raw
    $hasRedisDisabled = ($content -match '"IsEnabled"\s*:\s*false')
    $sw.Stop()
    Assert-IsTrue -TestResults $TestResults -TestId "B1.4.1" -Name "AuthServer Redis disabled" -Condition $hasRedisDisabled -Details "Checked appsettings.json for Redis.IsEnabled: false" -DurationMs $sw.ElapsedMilliseconds
} else {
    $sw.Stop()
    Add-SkipResult -TestResults $TestResults -TestId "B1.4.1" -Name "AuthServer Redis disabled" -Reason "appsettings.json not found"
}

# B1.4.2 Check API Host appsettings
$apiHostSettings = Join-Path $projectRoot "src\HealthcareSupport.CaseEvaluation.HttpApi.Host\appsettings.json"
$sw = [System.Diagnostics.Stopwatch]::StartNew()
if (Test-Path $apiHostSettings) {
    $content = Get-Content $apiHostSettings -Raw
    $hasRedisDisabled = ($content -match '"IsEnabled"\s*:\s*false')
    $sw.Stop()
    Assert-IsTrue -TestResults $TestResults -TestId "B1.4.2" -Name "API Host Redis disabled" -Condition $hasRedisDisabled -Details "Checked appsettings.json for Redis.IsEnabled: false" -DurationMs $sw.ElapsedMilliseconds
} else {
    $sw.Stop()
    Add-SkipResult -TestResults $TestResults -TestId "B1.4.2" -Name "API Host Redis disabled" -Reason "appsettings.json not found"
}

Write-Host "`nPhase B1 COMPLETE" -ForegroundColor Green
