<#
.SYNOPSIS
    One-shot fixture for Phase 1E.11 visual-diff: seeds a Falkinstein PQME
    appointment with the entity graph PacketTokenResolver walks, approves
    it via the API as Clinic Staff, polls until the 3 packet rows are
    Generated, and dumps each rendered DOCX to tmp/packet-parity-output/.

.DESCRIPTION
    Prereqs (script aborts if missing):
      - Docker stack up: sql-server (healthy), authserver (healthy on
        http://localhost:44368), api (healthy on http://localhost:44327)
      - falkinstein.localhost subdomain bypassed via curl --resolve
        (Windows resolver does not auto-map *.localhost to 127.0.0.1)
      - Falkinstein tenant + staff@falkinstein.test seeded by DbMigrator
        in Development env

    Notes:
      - All seeded entities use deterministic NEWID()s captured into SQL
        variables so the script is re-runnable; output dir is keyed by
        the appointment Id so successive runs do not collide.
      - The seeded AppointmentType is a fixture-only "PQME" row -- the
        DbMigrator's seeded names ("Panel QME" etc.) do not match
        GenerateAppointmentPacketJob.AttorneyClaimExaminerTypes
        ("PQME", "PQMEREEVAL", "AME", "AMEREEVAL"), so the orchestrator
        would skip the AttyCE packet without this fixture row.
      - Polls AppAppointmentPackets via sqlcmd every 3s, max 60s.

    Exit codes:
      0 = 3 packets generated + downloaded
      1 = prereq failure (stack down, env var missing)
      2 = SQL fixture failed
      3 = token acquisition failed
      4 = approve POST failed
      5 = packet generation did not complete within deadline
      6 = one or more downloads failed
#>

param(
    [string]$ApiBase     = "http://localhost:44327",
    [string]$AuthBase    = "http://localhost:44368",
    [string]$TenantHost  = "falkinstein.localhost",
    [string]$AuthPort    = "44368",
    [string]$Username    = "staff@falkinstein.test",
    [string]$Password    = "1q2w3E*r",
    [string]$OutRoot     = "tmp/packet-parity-output",
    [int]   $PollMaxSec  = 60,
    [int]   $PollIntervalSec = 3
)

$ErrorActionPreference = "Stop"

# --- Resolve repo paths ------------------------------------------------------
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $repoRoot

$curl = "C:\Users\RajeevG\AppData\Local\Programs\Git\mingw64\bin\curl.exe"
if (-not (Test-Path $curl)) {
    Write-Error "curl.exe not at $curl"
    exit 1
}

# --- Pull SA password from .env ---------------------------------------------
$saLine = Get-Content .env | Select-String '^MSSQL_SA_PASSWORD='
if (-not $saLine) { Write-Error "MSSQL_SA_PASSWORD not in .env"; exit 1 }
$saPassword = ($saLine.Line -replace '^MSSQL_SA_PASSWORD=','').Trim('"').Trim()

function Invoke-Sql([string]$Query, [switch]$NoHeader) {
    $args = @('exec','replicate-old-app-sql-server-1',
              '/opt/mssql-tools18/bin/sqlcmd','-S','localhost','-U','sa','-P',$saPassword,
              '-C','-d','CaseEvaluation')
    if ($NoHeader) { $args += @('-h','-1','-W') }
    $args += @('-Q',$Query)
    return (& docker @args 2>&1) -join "`n"
}

# --- Step 1: SQL fixture -----------------------------------------------------
Write-Host "Step 1: Seeding fixture..." -ForegroundColor Cyan

$sql = @'
SET NOCOUNT ON;
-- sqlcmd defaults QUOTED_IDENTIFIER OFF; filtered indexes (added in Phase 1A)
-- require it ON for any DML on the database. Same for ANSI_NULLS.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
BEGIN TRANSACTION;

DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT Id FROM SaasTenants WHERE Name = 'Falkinstein');
DECLARE @StaffUserId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM AbpUsers WHERE UserName = 'staff@falkinstein.test');
DECLARE @CaStateId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM AppStates WHERE Name = 'California');
DECLARE @WcabOfficeId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM AppWcabOffices ORDER BY Name);
IF @TenantId IS NULL OR @StaffUserId IS NULL OR @CaStateId IS NULL OR @WcabOfficeId IS NULL
BEGIN
    PRINT 'PREREQ_MISSING: tenant/staff/state/wcab';
    ROLLBACK; RETURN;
END

-- Fixture-only AppointmentType "PQME" so the job triggers the AttyCE branch.
DECLARE @PqmeTypeId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM AppAppointmentTypes WHERE Name = 'PQME');
IF @PqmeTypeId IS NULL
BEGIN
    SET @PqmeTypeId = NEWID();
    INSERT INTO AppAppointmentTypes (Id, Name, Description, CreationTime, IsDeleted)
    VALUES (@PqmeTypeId, 'PQME', 'Panel QME (fixture: matches GenerateAppointmentPacketJob name filter).', SYSUTCDATETIME(), 0);
END

-- Always create fresh rows for each fixture run so the DOCX comparison
-- is reproducible without depending on previously-seeded data.
DECLARE @DoctorId UNIQUEIDENTIFIER = NEWID();
DECLARE @LocationId UNIQUEIDENTIFIER = NEWID();
DECLARE @AvailabilityId UNIQUEIDENTIFIER = NEWID();
DECLARE @PatientId UNIQUEIDENTIFIER = NEWID();
DECLARE @ApplicantAttorneyId UNIQUEIDENTIFIER = NEWID();
DECLARE @DefenseAttorneyId UNIQUEIDENTIFIER = NEWID();
DECLARE @AppointmentId UNIQUEIDENTIFIER = NEWID();
DECLARE @EmployerDetailId UNIQUEIDENTIFIER = NEWID();
DECLARE @AppAppAttId UNIQUEIDENTIFIER = NEWID();
DECLARE @AppDefAttId UNIQUEIDENTIFIER = NEWID();
DECLARE @InjuryId UNIQUEIDENTIFIER = NEWID();
DECLARE @InsuranceId UNIQUEIDENTIFIER = NEWID();
DECLARE @ClaimExaminerId UNIQUEIDENTIFIER = NEWID();

DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @SlotDate DATE = DATEADD(DAY, 14, CAST(@Now AS DATE));
DECLARE @SlotDateTime DATETIME2 = CAST(@SlotDate AS DATETIME2);
DECLARE @InjuryDate DATE = DATEADD(MONTH, -6, CAST(@Now AS DATE));
DECLARE @DueDate DATETIME2 = DATEADD(DAY, 30, @Now);
DECLARE @ConfNumber NVARCHAR(50) = 'PKT-' + LEFT(REPLACE(CAST(NEWID() AS NVARCHAR(36)),'-',''), 8);

-- 1) Doctor (parent of DoctorAvailability)
INSERT INTO AppDoctors (Id, TenantId, FirstName, LastName, Email, Gender, ExtraProperties, ConcurrencyStamp, CreationTime, IsDeleted)
VALUES (@DoctorId, @TenantId, 'Yuri', 'Falkinstein', 'doc@falkinstein.test', 1, '{}', LOWER(CAST(NEWID() AS NVARCHAR(36))), @Now, 0);

-- 2) Location (Patient Packet pulls this)
INSERT INTO AppLocations (Id, Name, Address, City, ZipCode, ParkingFee, StateId, AppointmentTypeId, ExtraProperties, ConcurrencyStamp, CreationTime, IsDeleted, IsActive)
VALUES (@LocationId, 'WCSI Main Office', '12345 W. Olympic Blvd. Suite 200', 'Los Angeles', '90064', 15.00, @CaStateId, NULL, '{}', LOWER(CAST(NEWID() AS NVARCHAR(36))), @Now, 0, 1);

-- 3) Doctor Availability for slot 14 days out
INSERT INTO AppDoctorAvailabilities (Id, TenantId, AvailableDate, FromTime, ToTime, BookingStatusId, LocationId, AppointmentTypeId, ExtraProperties, ConcurrencyStamp, CreationTime, IsDeleted)
VALUES (@AvailabilityId, @TenantId, @SlotDateTime, '09:00', '10:00', 1, @LocationId, @PqmeTypeId, '{}', LOWER(CAST(NEWID() AS NVARCHAR(36))), @Now, 0);

-- 4) Patient (IdentityUserId is required; point at staff user as a fixture)
INSERT INTO AppPatients (Id, FirstName, LastName, MiddleName, Email, GenderId, DateOfBirth, PhoneNumber, SocialSecurityNumber, Address, City, ZipCode, RefferedBy, CellPhoneNumber, PhoneNumberTypeId, Street, InterpreterVendorName, ApptNumber, OthersLanguageName, StateId, AppointmentLanguageId, IdentityUserId, TenantId, ExtraProperties, ConcurrencyStamp, CreationTime, IsDeleted)
VALUES (@PatientId, 'Synthetic', 'TestPatient', 'M', 'patient@example.test', 1, '1985-03-15', '555-0100', '999-00-0001', NULL, 'Los Angeles', '90064', NULL, NULL, 1, '100 Main St', NULL, NULL, NULL, @CaStateId, NULL, @StaffUserId, @TenantId, '{}', LOWER(CAST(NEWID() AS NVARCHAR(36))), @Now, 0);

-- 5) Applicant Attorney (Patient Packet)
INSERT INTO AppApplicantAttorneys (Id, TenantId, FirmName, FirmAddress, WebAddress, PhoneNumber, FaxNumber, Street, City, ZipCode, StateId, ExtraProperties, ConcurrencyStamp, CreationTime, IsDeleted, IdentityUserId, Email)
VALUES (@ApplicantAttorneyId, @TenantId, 'Smith & Associates', NULL, NULL, '555-0200', NULL, '200 Attorney Way', 'Los Angeles', '90067', @CaStateId, '{}', LOWER(CAST(NEWID() AS NVARCHAR(36))), @Now, 0, NULL, 'applicant.atty@example.test');

-- 6) Defense Attorney (Patient Packet)
INSERT INTO AppDefenseAttorneys (Id, TenantId, FirmName, FirmAddress, WebAddress, PhoneNumber, FaxNumber, Street, City, ZipCode, StateId, IdentityUserId, ExtraProperties, ConcurrencyStamp, CreationTime, IsDeleted, Email)
VALUES (@DefenseAttorneyId, @TenantId, 'Defense Law Group', NULL, NULL, '555-0300', NULL, '300 Defense Plaza', 'Los Angeles', '90071', @CaStateId, NULL, '{}', LOWER(CAST(NEWID() AS NVARCHAR(36))), @Now, 0, 'defense.atty@example.test');

-- 7) Appointment in Pending state with PrimaryResponsibleUserId set
INSERT INTO AppAppointments (Id, TenantId, PanelNumber, AppointmentDate, IsPatientAlreadyExist, RequestConfirmationNumber, DueDate, InternalUserComments, AppointmentApproveDate, PatientId, IdentityUserId, AppointmentTypeId, LocationId, ExtraProperties, ConcurrencyStamp, CreationTime, IsDeleted, AppointmentStatus, DoctorAvailabilityId, ApplicantAttorneyEmail, ClaimExaminerEmail, DefenseAttorneyEmail, PatientEmail, IsBeyondLimit, PrimaryResponsibleUserId)
VALUES (@AppointmentId, @TenantId, 'PAN-12345', @SlotDateTime, 0, @ConfNumber, @DueDate, NULL, NULL, @PatientId, @StaffUserId, @PqmeTypeId, @LocationId, '{}', LOWER(CAST(NEWID() AS NVARCHAR(36))), @Now, 0, 1, @AvailabilityId, 'applicant.atty@example.test', 'examiner@example.test', 'defense.atty@example.test', 'patient@example.test', 0, @StaffUserId);

-- 8) Employer Detail (Doctor + AttyCE Packet)
INSERT INTO AppAppointmentEmployerDetails (Id, TenantId, EmployerName, Occupation, PhoneNumber, Street, City, ZipCode, AppointmentId, StateId, ExtraProperties, ConcurrencyStamp, CreationTime, IsDeleted)
VALUES (@EmployerDetailId, @TenantId, 'Acme Manufacturing Co.', 'Assembly Line Worker', '555-0400', '400 Industrial Park', 'Long Beach', '90802', @AppointmentId, @CaStateId, '{}', LOWER(CAST(NEWID() AS NVARCHAR(36))), @Now, 0);

-- 9) M2M: Appointment <-> ApplicantAttorney
INSERT INTO AppAppointmentApplicantAttorneys (Id, TenantId, AppointmentId, ApplicantAttorneyId, IdentityUserId, ExtraProperties, ConcurrencyStamp, CreationTime, IsDeleted)
VALUES (@AppAppAttId, @TenantId, @AppointmentId, @ApplicantAttorneyId, NULL, '{}', LOWER(CAST(NEWID() AS NVARCHAR(36))), @Now, 0);

-- 10) M2M: Appointment <-> DefenseAttorney
INSERT INTO AppAppointmentDefenseAttorneys (Id, TenantId, AppointmentId, DefenseAttorneyId, IdentityUserId, ExtraProperties, ConcurrencyStamp, CreationTime, IsDeleted)
VALUES (@AppDefAttId, @TenantId, @AppointmentId, @DefenseAttorneyId, NULL, '{}', LOWER(CAST(NEWID() AS NVARCHAR(36))), @Now, 0);

-- 11) Injury Detail
INSERT INTO AppAppointmentInjuryDetails (Id, TenantId, AppointmentId, DateOfInjury, ToDateOfInjury, ClaimNumber, IsCumulativeInjury, WcabAdj, BodyPartsSummary, WcabOfficeId, ExtraProperties, ConcurrencyStamp, CreationTime, IsDeleted)
VALUES (@InjuryId, @TenantId, @AppointmentId, @InjuryDate, NULL, 'CLM-2026-0001', 0, 'ADJ-7654321', 'Lower Back, Shoulders', @WcabOfficeId, '{}', LOWER(CAST(NEWID() AS NVARCHAR(36))), @Now, 0);

-- 12) Primary Insurance (per injury)
INSERT INTO AppAppointmentPrimaryInsurances (Id, TenantId, AppointmentInjuryDetailId, Name, InsuranceNumber, Attention, PhoneNumber, FaxNumber, Street, City, Zip, StateId, IsActive, ExtraProperties, ConcurrencyStamp, CreationTime, IsDeleted)
VALUES (@InsuranceId, @TenantId, @InjuryId, 'State Farm Workers Comp', 'INS-987654', NULL, '555-0500', NULL, '500 Insurance Way', 'San Francisco', '94104', @CaStateId, 1, '{}', LOWER(CAST(NEWID() AS NVARCHAR(36))), @Now, 0);

-- 13) Claim Examiner (per injury)
INSERT INTO AppAppointmentClaimExaminers (Id, TenantId, AppointmentInjuryDetailId, Name, ClaimExaminerNumber, Email, PhoneNumber, Fax, Street, City, Zip, StateId, IsActive, ExtraProperties, ConcurrencyStamp, CreationTime, IsDeleted)
VALUES (@ClaimExaminerId, @TenantId, @InjuryId, 'Jane Examiner', 'EX-12345', 'examiner@example.test', '555-0600', NULL, '600 Examiner Blvd', 'Oakland', '94612', @CaStateId, 1, '{}', LOWER(CAST(NEWID() AS NVARCHAR(36))), @Now, 0);

COMMIT TRANSACTION;

-- Output the appointment Id so the caller can use it.
PRINT 'APPT_ID=' + CAST(@AppointmentId AS NVARCHAR(36));
PRINT 'TENANT_ID=' + CAST(@TenantId AS NVARCHAR(36));
PRINT 'STAFF_ID=' + CAST(@StaffUserId AS NVARCHAR(36));
PRINT 'CONF_NUM=' + @ConfNumber;
'@

$sqlResult = Invoke-Sql -Query $sql
$sqlResult | Write-Host

if ($sqlResult -match 'PREREQ_MISSING') {
    Write-Error "SQL fixture aborted: missing prereqs (check seed data)."
    exit 2
}
if ($sqlResult -notmatch 'APPT_ID=([0-9A-F-]+)') {
    Write-Error "SQL fixture did not return APPT_ID. Output above."
    exit 2
}
$appointmentId = $matches[1]
Write-Host "Appointment seeded: $appointmentId" -ForegroundColor Green

# --- Step 2: Acquire Clinic Staff token via subdomain ------------------------
Write-Host "`nStep 2: Acquiring token..." -ForegroundColor Cyan

$tokenBody = "grant_type=password&client_id=CaseEvaluation_App&username=$([uri]::EscapeDataString($Username))&password=$([uri]::EscapeDataString($Password))&scope=offline_access CaseEvaluation openid profile email roles"

$tokenJson = & $curl -s `
    --resolve "${TenantHost}:${AuthPort}:127.0.0.1" `
    -X POST `
    "http://${TenantHost}:${AuthPort}/connect/token" `
    -H "Content-Type: application/x-www-form-urlencoded" `
    --data $tokenBody

$tokenObj = $null
try { $tokenObj = $tokenJson | ConvertFrom-Json } catch {}
if (-not $tokenObj -or -not $tokenObj.access_token) {
    Write-Error "Token acquisition failed. Response: $tokenJson"
    exit 3
}
$token = $tokenObj.access_token
Write-Host "Token acquired ($($token.Length) chars)." -ForegroundColor Green

# --- Step 3: Approve the appointment ----------------------------------------
Write-Host "`nStep 3: Approving appointment $appointmentId..." -ForegroundColor Cyan

$approveResp = & $curl -s -w "%{http_code}" `
    -X POST `
    "$ApiBase/api/app/appointments/$appointmentId/approve" `
    -H "Authorization: Bearer $token" `
    -H "Content-Length: 0"

$status = $approveResp.Substring($approveResp.Length - 3)
$body = $approveResp.Substring(0, $approveResp.Length - 3)
Write-Host "POST /approve HTTP $status"
if ($status -ne '200') {
    Write-Host "Body: $body" -ForegroundColor Red
    Write-Error "Approve failed"
    exit 4
}

# --- Step 4: Poll AppAppointmentPackets for 3 Generated rows -----------------
Write-Host "`nStep 4: Polling for packet generation (max ${PollMaxSec}s)..." -ForegroundColor Cyan

$pollDeadline = (Get-Date).AddSeconds($PollMaxSec)
$ready = $false
$lastSummary = ""
while ((Get-Date) -lt $pollDeadline) {
    $countQuery = "SELECT COUNT(*) FROM AppAppointmentPackets WHERE AppointmentId = '$appointmentId' AND Status = 2;"
    $statusQuery = "SELECT Kind, Status, COALESCE(ErrorMessage,'') FROM AppAppointmentPackets WHERE AppointmentId = '$appointmentId' ORDER BY Kind;"
    # sqlcmd -h -1 -W returns "<value>\n\n(N rows affected)" -- extract the integer line.
    $countRaw = Invoke-Sql -Query $countQuery -NoHeader
    $countMatch = ($countRaw -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^\d+$' } | Select-Object -First 1)
    $count = if ($countMatch) { [int]$countMatch } else { 0 }
    $rowsRaw = (Invoke-Sql -Query $statusQuery -NoHeader).Trim()
    if ($rowsRaw -ne $lastSummary) {
        Write-Host "  state: $rowsRaw"
        $lastSummary = $rowsRaw
    }
    if ($count -ge 3) { $ready = $true; break }
    Start-Sleep -Seconds $PollIntervalSec
}

if (-not $ready) {
    Write-Error "Packet generation did not complete (3 Generated rows) within ${PollMaxSec}s. Last state: $lastSummary"
    exit 5
}
Write-Host "All 3 packets generated." -ForegroundColor Green

# --- Step 5: Download each kind ---------------------------------------------
# Phase 2 (2026-05-11): blob extension is .pdf since DOCX -> PDF conversion
# lands inside GenerateAppointmentPacketJob.GenerateKindAsync now. The
# download endpoint streams whatever the blob contains, so the local file
# follows the same extension.
Write-Host "`nStep 5: Downloading PDF packets..." -ForegroundColor Cyan
$outDir = Join-Path $repoRoot (Join-Path $OutRoot $appointmentId)
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$kinds = @('Patient','Doctor','AttorneyClaimExaminer')
$dlFailures = @()
foreach ($kind in $kinds) {
    $out = Join-Path $outDir "$kind.pdf"
    $httpCode = & $curl -s -w "%{http_code}" -o $out `
        -X GET `
        "$ApiBase/api/app/appointments/$appointmentId/packet/download/$kind" `
        -H "Authorization: Bearer $token"
    $bytes = if (Test-Path $out) { (Get-Item $out).Length } else { 0 }
    Write-Host ("  {0,-22} HTTP {1}  {2:N0} bytes -> {3}" -f $kind, $httpCode, $bytes, $out)
    if ($httpCode -ne '200' -or $bytes -lt 10000) { $dlFailures += "$kind ($httpCode, $bytes bytes)" }
}

if ($dlFailures.Count -gt 0) {
    Write-Error "Download failures: $($dlFailures -join '; ')"
    exit 6
}

# --- Done -------------------------------------------------------------------
Write-Host "`nSUCCESS" -ForegroundColor Green
Write-Host "Appointment: $appointmentId"
Write-Host "Output dir:  $outDir"
exit 0
