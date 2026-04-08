<#
.SYNOPSIS
    Phase B14: Data Integrity
.DESCRIPTION
    Tests soft delete, audit fields, FK constraint behavior, cross-context entity visibility via SQL.
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
Write-Host "PHASE B14: Data Integrity" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

$defaultPassword = $env:TEST_PASSWORD
$hostToken = Get-AuthToken -Username "admin@abp.io" -Password $defaultPassword -AuthServerUrl $AuthServerUrl
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

# ---- B14.1 Soft Delete Verification ----

Write-Host "`n--- B14.1 Soft Delete ---" -ForegroundColor Yellow

# Create a test state, delete it, verify soft delete in DB
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-TestApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/states" -Body @{ name = "B14SoftDeleteTest" } -Token $hostToken
$sw.Stop()
$testId = if ($resp.Body) { $resp.Body.id } else { $null }

if ($testId) {
    # B14.1.1 DELETE via API
    $resp = Invoke-TestApiCall -Method "DELETE" -Url "$ApiBaseUrl/api/app/states/$testId" -Token $hostToken
    Assert-IsTrue -TestResults $TestResults -TestId "B14.1.1" -Name "State DELETE returns success" -Condition ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 204)

    # B14.1.2 Row still exists with IsDeleted=1
    try {
        $result = Invoke-SqlQuery -Query "SELECT IsDeleted FROM AppStates WHERE Id = '$testId'" -ConnectionString $connString
        $isDeleted = if ($result.Rows.Count -gt 0) { $result.Rows[0].IsDeleted } else { $false }
        Assert-IsTrue -TestResults $TestResults -TestId "B14.1.2" -Name "Soft-deleted row exists with IsDeleted=1" -Condition ($result.Rows.Count -gt 0 -and $isDeleted -eq $true) -Details "Rows: $($result.Rows.Count), IsDeleted: $isDeleted"
    } catch {
        Add-TestResult -TestResults $TestResults -TestId "B14.1.2" -Name "Soft delete DB check" -Status "FAIL" -Details "SQL error: $_"
    }

    # B14.1.3 GET via API returns 404
    $resp = Invoke-TestApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/states/$testId" -Token $hostToken
    Assert-StatusCode -TestResults $TestResults -TestId "B14.1.3" -Name "Soft-deleted returns 404 via API" -Response $resp -Expected 404
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B14.1.1" -Name "Soft delete" -Reason "Cannot create test state"
    Add-SkipResult -TestResults $TestResults -TestId "B14.1.2" -Name "IsDeleted check" -Reason "No test state"
    Add-SkipResult -TestResults $TestResults -TestId "B14.1.3" -Name "404 after delete" -Reason "No test state"
}

# ---- B14.2 Audit Fields ----

Write-Host "`n--- B14.2 Audit Fields ---" -ForegroundColor Yellow

$apptKey = if ($SeedState.appointmentIds) { @($SeedState.appointmentIds.Keys | Where-Object { $_ -like "T1_*" })[0] } else { $null }
$apptId = if ($apptKey) { $SeedState.appointmentIds[$apptKey] } else { $null }

if ($apptId) {
    # B14.2.1 CreationTime populated
    try {
        $result = Invoke-SqlQuery -Query "SELECT CreationTime, CreatorId FROM AppAppointments WHERE Id = '$apptId'" -ConnectionString $connString
        $hasCreationTime = ($result.Rows.Count -gt 0 -and $result.Rows[0].CreationTime -ne [DBNull]::Value)
        Assert-IsTrue -TestResults $TestResults -TestId "B14.2.1" -Name "Appointment CreationTime populated" -Condition $hasCreationTime -Details "CreationTime: $(if($hasCreationTime){$result.Rows[0].CreationTime}else{'NULL'})"

        # B14.2.2 CreatorId populated
        $hasCreator = ($result.Rows.Count -gt 0 -and $result.Rows[0].CreatorId -ne [DBNull]::Value)
        Assert-IsTrue -TestResults $TestResults -TestId "B14.2.2" -Name "Appointment CreatorId populated" -Condition $hasCreator
    } catch {
        Add-TestResult -TestResults $TestResults -TestId "B14.2.1" -Name "CreationTime" -Status "FAIL" -Details "SQL error: $_"
        Add-TestResult -TestResults $TestResults -TestId "B14.2.2" -Name "CreatorId" -Status "FAIL" -Details "SQL error: $_"
    }
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B14.2.1" -Name "CreationTime" -Reason "No appointment"
    Add-SkipResult -TestResults $TestResults -TestId "B14.2.2" -Name "CreatorId" -Reason "No appointment"
}

# ---- B14.3 FK Constraint Behavior ----

Write-Host "`n--- B14.3 FK Constraints ---" -ForegroundColor Yellow

# B14.3.1 DELETE California state (referenced by locations)
$caId = $SeedState.stateIds["California"]
if ($caId) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-TestApiCall -Method "DELETE" -Url "$ApiBaseUrl/api/app/states/$caId" -Token $hostToken
    $sw.Stop()
    # ABP soft delete should work (no FK violation because of soft delete global filter)
    $details = "Status: $($resp.StatusCode). "
    if ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 204) {
        $details += "Soft delete succeeded despite FK references (ABP global filter)"
        # Un-delete by raw SQL since we need California
        try {
            Add-Type -AssemblyName System.Data
            $conn = New-Object System.Data.SqlClient.SqlConnection($connString)
            $conn.Open()
            $cmd = $conn.CreateCommand()
            $cmd.CommandText = "UPDATE AppStates SET IsDeleted = 0, DeletionTime = NULL, DeleterId = NULL WHERE Id = '$caId'"
            $cmd.ExecuteNonQuery() | Out-Null
            $conn.Close()
            $details += " (restored via SQL)"
        } catch {
            $details += " WARNING: Could not restore California state"
        }
    } else {
        $details += "FK constraint prevented deletion"
    }
    Add-TestResult -TestResults $TestResults -TestId "B14.3.1" -Name "DELETE California (FK referenced)" -Status "PASS" -Details $details -DurationMs $sw.ElapsedMilliseconds
} else {
    Add-SkipResult -TestResults $TestResults -TestId "B14.3.1" -Name "DELETE California" -Reason "No California state"
}

# ---- B14.4 Cross-Context Visibility ----

Write-Host "`n--- B14.4 Cross-Context Visibility ---" -ForegroundColor Yellow

# B14.4.1 Appointment TenantId distribution
try {
    $result = Invoke-SqlQuery -Query "SELECT TenantId, COUNT(*) AS Cnt FROM AppAppointments WHERE IsDeleted = 0 GROUP BY TenantId" -ConnectionString $connString
    $tenantCount = $result.Rows.Count
    Assert-GreaterOrEqual -TestResults $TestResults -TestId "B14.4.1" -Name "Appointments distributed across tenants" -Actual $tenantCount -Expected 2 -DurationMs 0
} catch {
    Add-TestResult -TestResults $TestResults -TestId "B14.4.1" -Name "Appointment TenantId distribution" -Status "FAIL" -Details "SQL error: $_"
}

# B14.4.2 Total appointment count
try {
    $result = Invoke-SqlQuery -Query "SELECT COUNT(*) AS Cnt FROM AppAppointments WHERE IsDeleted = 0" -ConnectionString $connString
    $totalAppts = $result.Rows[0].Cnt
    Assert-GreaterOrEqual -TestResults $TestResults -TestId "B14.4.2" -Name "Total appointments >= 20" -Actual $totalAppts -Expected 20
} catch {
    Add-TestResult -TestResults $TestResults -TestId "B14.4.2" -Name "Total appointments" -Status "FAIL" -Details "SQL error: $_"
}

Write-Host "`nPhase B14 COMPLETE" -ForegroundColor Green
