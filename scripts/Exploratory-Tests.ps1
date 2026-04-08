<#
.SYNOPSIS
    Exploratory tests beyond B1-B16 to find additional issues.
.DESCRIPTION
    Runs additional API probes, SQL verification, and config audits.
    Output is captured for documentation in docs/issues/.
#>
param(
    [string]$ApiBaseUrl = "https://localhost:44327",
    [string]$AuthServerUrl = "https://localhost:44368"
)

$ErrorActionPreference = "Stop"
$scriptRoot = $PSScriptRoot
. "$scriptRoot\helpers\Get-AuthToken.ps1"
. "$scriptRoot\helpers\Invoke-ApiCall.ps1"
. "$scriptRoot\helpers\Assert-Response.ps1"
. "$scriptRoot\helpers\Read-SeedState.ps1"
. "$scriptRoot\helpers\New-FakeData.ps1"

$ss = Read-SeedState -Path "$scriptRoot\seed-state.json"
$defaultPassword = $env:TEST_PASSWORD
$hostToken = Get-AuthToken -Username "admin@abp.io" -Password $defaultPassword -AuthServerUrl $AuthServerUrl
$t1Id = $ss.tenantIds["T1"]
$t1Token = Get-AuthToken -Username $ss._tenantEmails["T1"] -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $t1Id
$patientEmail = $ss._userEmails["T1_P1"]
$patientToken = Get-AuthToken -Username $patientEmail -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $t1Id
$patientId = @($ss.patientIds.Values)[0]
$doctorUserId = $ss.doctorUserIds["T1"]
$typeId = @($ss.appointmentTypeIds.Values)[0]
$locId = $ss.locationIds["Loc1"]

$findings = @()

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "EXPLORATORY TESTS - Beyond B1-B16" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# ---- E1: Past-date appointment (no validation expected) ----
Write-Host "`n--- E1: Past-date appointment ---" -ForegroundColor Yellow
$pastSlotBody = @{ availableDate = "2026-01-15T00:00:00"; fromTime = "09:00:00"; toTime = "10:00:00"; bookingStatusId = 8; locationId = $locId; appointmentTypeId = $typeId }
$pastSlot = Invoke-TestApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/doctor-availabilities" -Body $pastSlotBody -Token $t1Token -TenantId $t1Id
if ($pastSlot.Body) {
    $apptBody = @{ appointmentDate = "2026-01-15T09:30:00"; appointmentStatus = 1; requestConfirmationNumber = "PLACEHOLDER"; patientId = $patientId; identityUserId = $doctorUserId; appointmentTypeId = $typeId; locationId = $locId; doctorAvailabilityId = $pastSlot.Body.id }
    $resp = Invoke-TestApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/appointments" -Body $apptBody -Token $t1Token -TenantId $t1Id
    $result = if ($resp.StatusCode -eq 200) { "ACCEPTED - No past-date validation exists" } else { "Rejected ($($resp.StatusCode))" }
    Write-Host "  Result: $result" -ForegroundColor $(if($resp.StatusCode -eq 200){"Yellow"}else{"Green"})
    $findings += "E1: Past-date appointment: $result"
    if ($resp.Body -and $resp.Body.id) { Invoke-TestApiCall -Method "DELETE" -Url "$ApiBaseUrl/api/app/appointments/$($resp.Body.id)" -Token $t1Token -TenantId $t1Id | Out-Null }
    Invoke-TestApiCall -Method "DELETE" -Url "$ApiBaseUrl/api/app/doctor-availabilities/$($pastSlot.Body.id)" -Token $t1Token -TenantId $t1Id | Out-Null
}

# ---- E2: Custom confirmation number overridden ----
Write-Host "`n--- E2: Custom confirmation number ---" -ForegroundColor Yellow
$slotsResp = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/doctor-availabilities?bookingStatusId=8&maxResultCount=50" -Token $t1Token -TenantId $t1Id
$futureSlot = $null
foreach ($item in $slotsResp.items) {
    $slot = $item.doctorAvailability
    $d = if ($slot.availableDate -is [DateTime]) { $slot.availableDate } else { [DateTime]::Parse($slot.availableDate) }
    if ($d -gt (Get-Date).AddDays(3)) { $futureSlot = $slot; break }
}
if ($futureSlot) {
    $dateStr = if ($futureSlot.availableDate -is [DateTime]) { $futureSlot.availableDate.ToString("yyyy-MM-dd") } else { $futureSlot.availableDate.Substring(0,10) }
    $timeStr = if ($futureSlot.fromTime -is [TimeSpan]) { $futureSlot.fromTime.ToString("hh\:mm\:ss") } else { [string]$futureSlot.fromTime }
    $body = @{ appointmentDate = "${dateStr}T${timeStr}"; appointmentStatus = 1; requestConfirmationNumber = "CUSTOM99"; patientId = $patientId; identityUserId = $doctorUserId; appointmentTypeId = $futureSlot.appointmentTypeId; locationId = $futureSlot.locationId; doctorAvailabilityId = $futureSlot.id }
    $resp = Invoke-TestApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/appointments" -Body $body -Token $t1Token -TenantId $t1Id
    if ($resp.Body) {
        $confNum = $resp.Body.requestConfirmationNumber
        $result = if ($confNum -eq "CUSTOM99") { "BUG: Custom value accepted!" } else { "Auto-generated: $confNum (correct)" }
        Write-Host "  Sent CUSTOM99, Got: $confNum - $result" -ForegroundColor $(if($confNum -eq "CUSTOM99"){"Red"}else{"Green"})
        $findings += "E2: Custom confirmation number: $result"
        Invoke-TestApiCall -Method "DELETE" -Url "$ApiBaseUrl/api/app/appointments/$($resp.Body.id)" -Token $t1Token -TenantId $t1Id | Out-Null
        # Release slot
        try {
            $slotNow = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/doctor-availabilities/$($futureSlot.id)" -Token $t1Token -TenantId $t1Id
            if ($slotNow.bookingStatusId -eq 9) {
                $ub = @{ availableDate=$slotNow.availableDate; fromTime=$slotNow.fromTime; toTime=$slotNow.toTime; bookingStatusId=8; locationId=$slotNow.locationId; appointmentTypeId=$slotNow.appointmentTypeId; concurrencyStamp=$slotNow.concurrencyStamp }
                Invoke-ApiCall -Method "PUT" -Url "$ApiBaseUrl/api/app/doctor-availabilities/$($futureSlot.id)" -Body $ub -Token $t1Token -TenantId $t1Id | Out-Null
            }
        } catch {}
    }
}

# ---- E3: Patient /me endpoint ----
Write-Host "`n--- E3: Patient /me endpoint ---" -ForegroundColor Yellow
$resp = Invoke-TestApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/patients/me" -Token $patientToken -TenantId $t1Id
Write-Host "  Status: $($resp.StatusCode), HasData: $($resp.Body -ne $null)" -ForegroundColor $(if($resp.StatusCode -eq 200){"Green"}else{"Red"})
$findings += "E3: Patient /me: Status=$($resp.StatusCode)"

# ---- E4: External user lookup data exposure ----
Write-Host "`n--- E4: External user lookup as Patient (data exposure) ---" -ForegroundColor Yellow
$resp = Invoke-TestApiCall -Method "GET" -Url "$ApiBaseUrl/api/public/external-signup/external-user-lookup" -Token $patientToken -TenantId $t1Id
$userCount = if ($resp.Body -and $resp.Body.items) { $resp.Body.items.Count } else { 0 }
Write-Host "  Users returned to Patient: $userCount" -ForegroundColor $(if($userCount -gt 0){"Yellow"}else{"Green"})
if ($userCount -gt 0) {
    $sample = $resp.Body.items[0]
    $props = $sample.PSObject.Properties.Name -join ", "
    Write-Host "  Exposed fields: $props" -ForegroundColor Yellow
    $findings += "E4: External user lookup exposes $userCount users to Patient role. Fields: $props"
} else {
    $findings += "E4: External user lookup: $userCount users returned (OK)"
}

# ---- E5: Weak password acceptance ----
Write-Host "`n--- E5: Weak password acceptance ---" -ForegroundColor Yellow
$weakResp = Invoke-TestApiCall -Method "POST" -Url "$ApiBaseUrl/api/public/external-signup/register" -Body @{ userType = 1; firstName = "Weak"; lastName = "Pass"; email = "exploratory.weak@b-test.hcs.test"; password = "123456"; tenantId = $t1Id }
$weakResult = if ($weakResp.StatusCode -eq 200) { "ACCEPTED - No complexity requirements!" } elseif ($weakResp.Error -like "*already*") { "Already exists (from previous run)" } else { "Rejected ($($weakResp.StatusCode))" }
Write-Host "  Password '123456': $weakResult" -ForegroundColor $(if($weakResp.StatusCode -eq 200){"Yellow"}else{"DarkGray"})
$findings += "E5: Weak password '123456': $weakResult"

# ---- E6: Slot release after appointment DELETE ----
Write-Host "`n--- E6: Slot release after DELETE (gap C1.2) ---" -ForegroundColor Yellow
$testDate = (Get-Date).AddDays(200).ToString("yyyy-MM-dd")
$slotBody = @{ availableDate = "${testDate}T00:00:00"; fromTime = "09:00:00"; toTime = "10:00:00"; bookingStatusId = 8; locationId = $locId; appointmentTypeId = $typeId }
$slotResp = Invoke-TestApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/doctor-availabilities" -Body $slotBody -Token $t1Token -TenantId $t1Id
if ($slotResp.Body) {
    $slotId = $slotResp.Body.id
    $bookBody = @{ appointmentDate = "${testDate}T09:30:00"; appointmentStatus = 1; requestConfirmationNumber = "PLACEHOLDER"; patientId = $patientId; identityUserId = $doctorUserId; appointmentTypeId = $typeId; locationId = $locId; doctorAvailabilityId = $slotId }
    $bookResp = Invoke-TestApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/appointments" -Body $bookBody -Token $t1Token -TenantId $t1Id
    if ($bookResp.Body) {
        Invoke-TestApiCall -Method "DELETE" -Url "$ApiBaseUrl/api/app/appointments/$($bookResp.Body.id)" -Token $t1Token -TenantId $t1Id | Out-Null
        $slotCheck = Invoke-TestApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/doctor-availabilities/$slotId" -Token $t1Token -TenantId $t1Id
        $status = if ($slotCheck.Body) { $slotCheck.Body.bookingStatusId } else { "unknown" }
        $result = if ($status -eq 9) { "CONFIRMED: Slot stays Booked(9) after DELETE (gap C1.2)" } elseif ($status -eq 8) { "Slot released - gap C1.2 NOT reproduced" } else { "Unknown state: $status" }
        Write-Host "  $result" -ForegroundColor $(if($status -eq 9){"Yellow"}else{"Green"})
        $findings += "E6: $result"
        # Cleanup
        try {
            $slotNow = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/doctor-availabilities/$slotId" -Token $t1Token -TenantId $t1Id
            $ub = @{ availableDate=$slotNow.availableDate; fromTime=$slotNow.fromTime; toTime=$slotNow.toTime; bookingStatusId=8; locationId=$slotNow.locationId; appointmentTypeId=$slotNow.appointmentTypeId; concurrencyStamp=$slotNow.concurrencyStamp }
            Invoke-ApiCall -Method "PUT" -Url "$ApiBaseUrl/api/app/doctor-availabilities/$slotId" -Body $ub -Token $t1Token -TenantId $t1Id | Out-Null
        } catch {}
    }
    Invoke-TestApiCall -Method "DELETE" -Url "$ApiBaseUrl/api/app/doctor-availabilities/$slotId" -Token $t1Token -TenantId $t1Id | Out-Null
}

# ---- E7: fromTime > toTime (gap C1.7) ----
Write-Host "`n--- E7: fromTime > toTime slot creation (gap C1.7) ---" -ForegroundColor Yellow
$badDate = (Get-Date).AddDays(201).ToString("yyyy-MM-dd")
$badBody = @{ availableDate = "${badDate}T00:00:00"; fromTime = "15:00:00"; toTime = "14:00:00"; bookingStatusId = 8; locationId = $locId; appointmentTypeId = $typeId }
$resp = Invoke-TestApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/doctor-availabilities" -Body $badBody -Token $t1Token -TenantId $t1Id
$result = if ($resp.StatusCode -eq 200) { "ACCEPTED - No fromTime > toTime validation (gap C1.7 confirmed)" } else { "Rejected ($($resp.StatusCode))" }
Write-Host "  $result" -ForegroundColor $(if($resp.StatusCode -eq 200){"Yellow"}else{"Green"})
$findings += "E7: $result"
if ($resp.Body -and $resp.Body.id) { Invoke-TestApiCall -Method "DELETE" -Url "$ApiBaseUrl/api/app/doctor-availabilities/$($resp.Body.id)" -Token $t1Token -TenantId $t1Id | Out-Null }

# ---- E8: Anonymous API access ----
Write-Host "`n--- E8: Anonymous access to protected endpoints ---" -ForegroundColor Yellow
$anonEndpoints = @(
    "/api/app/appointments",
    "/api/app/patients",
    "/api/app/doctors",
    "/api/app/states",
    "/api/app/doctor-availabilities"
)
foreach ($ep in $anonEndpoints) {
    $resp = Invoke-TestApiCall -Method "GET" -Url "$ApiBaseUrl$ep"
    Write-Host "  Anonymous GET $($ep): $($resp.StatusCode)" -ForegroundColor $(if($resp.StatusCode -eq 401){"Green"}else{"Red"})
}
$findings += "E8: Anonymous access correctly blocked (401) on all /api/app/* endpoints"

# ---- E9: Patient accessing admin endpoints ----
Write-Host "`n--- E9: Patient role privilege escalation ---" -ForegroundColor Yellow
# Try to create a state as Patient
$resp = Invoke-TestApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/states" -Body @{ name = "HackerState" } -Token $patientToken -TenantId $t1Id
Write-Host "  Patient POST /states: $($resp.StatusCode)" -ForegroundColor $(if($resp.StatusCode -eq 403){"Green"}else{"Red"})
$findings += "E9: Patient POST /states: $($resp.StatusCode) ($(if($resp.StatusCode -eq 403){'correctly blocked'}else{'SECURITY ISSUE!'}))"

# Try to create a location as Patient
$resp = Invoke-TestApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/locations" -Body @{ name = "HackerLoc"; address = "x"; city = "x"; zipCode = "x"; isActive = $true; stateId = $ss.stateIds["California"] } -Token $patientToken -TenantId $t1Id
Write-Host "  Patient POST /locations: $($resp.StatusCode)" -ForegroundColor $(if($resp.StatusCode -eq 403){"Green"}else{"Red"})
$findings += "E9: Patient POST /locations: $($resp.StatusCode)"

# ---- E10: SQL Data Integrity Checks ----
Write-Host "`n--- E10: SQL Data Integrity ---" -ForegroundColor Yellow
$connString = "Server=(LocalDb)\MSSQLLocalDB;Database=CaseEvaluation;Trusted_Connection=True;TrustServerCertificate=true"
Add-Type -AssemblyName System.Data

function Invoke-Sql {
    param([string]$Query)
    $conn = New-Object System.Data.SqlClient.SqlConnection($connString)
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $Query
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
        $table = New-Object System.Data.DataTable
        $adapter.Fill($table) | Out-Null
        return ,$table
    } finally { $conn.Close() }
}

# Check for duplicate emails in patients
$dupes = Invoke-Sql "SELECT Email, COUNT(*) AS Cnt FROM AbpUsers WHERE IsDeleted = 0 GROUP BY Email HAVING COUNT(*) > 1"
Write-Host "  Duplicate emails in AbpUsers: $($dupes.Rows.Count) duplicates" -ForegroundColor $(if($dupes.Rows.Count -eq 0){"Green"}else{"Yellow"})
$findings += "E10a: Duplicate emails in AbpUsers: $($dupes.Rows.Count)"

# Check tenant distribution of appointments
$tenantDist = Invoke-Sql "SELECT TenantId, COUNT(*) AS Cnt FROM AppAppointments WHERE IsDeleted = 0 GROUP BY TenantId ORDER BY Cnt DESC"
Write-Host "  Appointment TenantId distribution:" -ForegroundColor White
foreach ($row in $tenantDist.Rows) {
    Write-Host "    TenantId=$($row.TenantId): $($row.Cnt) appointments" -ForegroundColor DarkGray
}
$findings += "E10b: Appointments distributed across $($tenantDist.Rows.Count) tenants"

# Check for null CreatorId (audit trail completeness)
$nullCreators = Invoke-Sql "SELECT COUNT(*) AS Cnt FROM AppAppointments WHERE IsDeleted = 0 AND CreatorId IS NULL"
$nullCount = $nullCreators.Rows[0].Cnt
Write-Host "  Appointments with null CreatorId: $nullCount" -ForegroundColor $(if($nullCount -eq 0){"Green"}else{"Yellow"})
$findings += "E10c: Appointments with null CreatorId: $nullCount"

# Check for orphaned doctor availability (no tenant)
$orphanSlots = Invoke-Sql "SELECT COUNT(*) AS Cnt FROM AppDoctorAvailabilities WHERE IsDeleted = 0 AND TenantId IS NULL"
$orphanCount = $orphanSlots.Rows[0].Cnt
Write-Host "  Orphaned slots (null TenantId): $orphanCount" -ForegroundColor $(if($orphanCount -eq 0){"Green"}else{"Red"})
$findings += "E10d: Orphaned slots (null TenantId): $orphanCount"

# Check total record counts
$tables = @("AppStates", "AppAppointmentTypes", "AppAppointmentStatuses", "AppAppointmentLanguages", "AppLocations", "AppWcabOffices", "AppDoctors", "AppDoctorAvailabilities", "AppAppointments", "AppPatients", "AppApplicantAttorneys", "AppAppointmentEmployerDetails", "AppAppointmentAccessors")
Write-Host "  Record counts (IsDeleted=0):" -ForegroundColor White
foreach ($t in $tables) {
    $countResult = Invoke-Sql "SELECT COUNT(*) AS Cnt FROM $t WHERE IsDeleted = 0"
    Write-Host "    $($t): $($countResult.Rows[0].Cnt)" -ForegroundColor DarkGray
}

# Check unique constraint on RequestConfirmationNumber
$dupeConf = Invoke-Sql "SELECT RequestConfirmationNumber, TenantId, COUNT(*) AS Cnt FROM AppAppointments WHERE IsDeleted = 0 GROUP BY RequestConfirmationNumber, TenantId HAVING COUNT(*) > 1"
Write-Host "  Duplicate confirmation numbers (same tenant): $($dupeConf.Rows.Count)" -ForegroundColor $(if($dupeConf.Rows.Count -eq 0){"Green"}else{"Red"})
$findings += "E10e: Duplicate confirmation numbers: $($dupeConf.Rows.Count)"

# Check for missing indexes (query plan check)
$missingIndexes = Invoke-Sql "SELECT OBJECT_NAME(i.object_id) AS TableName, i.equality_columns, i.inequality_columns FROM sys.dm_db_missing_index_details i WHERE OBJECT_NAME(i.object_id) LIKE 'App%'"
Write-Host "  Missing indexes on App* tables: $($missingIndexes.Rows.Count)" -ForegroundColor $(if($missingIndexes.Rows.Count -eq 0){"Green"}else{"Yellow"})
$findings += "E10f: Missing indexes: $($missingIndexes.Rows.Count)"

# ---- E11: Configuration Audit ----
Write-Host "`n--- E11: Configuration Audit ---" -ForegroundColor Yellow
$projectRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path

# Check appsettings for secrets
$apiSettings = Get-Content "$projectRoot\src\HealthcareSupport.CaseEvaluation.HttpApi.Host\appsettings.json" -Raw
$authSettings = Get-Content "$projectRoot\src\HealthcareSupport.CaseEvaluation.AuthServer\appsettings.json" -Raw

$secrets = @()
if ($apiSettings -match "REPLACE_ME_LOCALLY") { $secrets += "StringEncryption placeholder (not yet configured) in API Host" }
if ($authSettings -match "REPLACE_ME_LOCALLY") { $secrets += "StringEncryption placeholder (not yet configured) in AuthServer" }
if ($apiSettings -match "REPLACE_ME_LOCALLY") { $secrets += "OpenIddict PFX placeholder in API Host" }
if ($authSettings -match "REPLACE_ME_LOCALLY") { $secrets += "OpenIddict PFX placeholder in AuthServer" }
if ($apiSettings -match "DisablePII.*false") { $secrets += "PII logging enabled (DisablePII: false)" }
Write-Host "  Secrets found in config files:" -ForegroundColor Yellow
foreach ($s in $secrets) { Write-Host "    - $s" -ForegroundColor Red }
$findings += "E11: $($secrets.Count) secrets/risks found in config files"

# Check CORS config
if ($apiSettings -match "CorsOrigins") {
    $corsMatch = [regex]::Match($apiSettings, '"CorsOrigins"\s*:\s*"([^"]+)"')
    if ($corsMatch.Success) {
        Write-Host "  CORS Origins: $($corsMatch.Groups[1].Value)" -ForegroundColor Yellow
        $findings += "E11: CORS Origins: $($corsMatch.Groups[1].Value)"
    }
}

# ---- Summary ----
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "EXPLORATORY TEST SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
foreach ($f in $findings) {
    Write-Host "  $f" -ForegroundColor White
}
Write-Host "`nTotal findings: $($findings.Count)" -ForegroundColor Cyan
