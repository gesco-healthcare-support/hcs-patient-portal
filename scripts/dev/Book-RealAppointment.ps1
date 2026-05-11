<#
.SYNOPSIS
    Full API end-to-end booking + packet pipeline test. No Angular.

.DESCRIPTION
    Exercises every REST endpoint a real booking flow touches:
      Doctor -> Location -> DoctorAvailability -> Patient (via
      for-appointment-booking) -> Appointment -> EmployerDetail ->
      ApplicantAttorney upsert -> DefenseAttorney upsert -> InjuryDetail
      -> PrimaryInsurance -> ClaimExaminer -> BodyParts -> Approve
      as Clinic Staff -> poll AppAppointmentPackets -> download
      3 PDFs.

    All entity creates use the admin token. Final approve uses the staff
    token to exercise the Clinic Staff approval path. Subdomain tenant
    routing (falkinstein.localhost:44368) is honored via curl --resolve.

.NOTES
    Exit codes:
      0 = all 3 PDFs downloaded
      1 = prereq failure (stack down, env var missing)
      3 = token acquisition failed
      4 = booking-flow API call failed
      5 = approve POST failed
      6 = packet generation did not complete in deadline
      7 = one or more downloads failed
#>

param(
    [string]$ApiBase     = "http://localhost:44327",
    [string]$AuthBase    = "http://localhost:44368",
    [string]$TenantHost  = "falkinstein.localhost",
    [string]$AuthPort    = "44368",
    [string]$AdminUser   = "admin@falkinstein.test",
    [string]$StaffUser   = "staff@falkinstein.test",
    [string]$Password    = "1q2w3E*r",
    [string]$OutRoot     = "tmp/packet-parity-output",
    [int]   $PollMaxSec  = 90,
    [int]   $PollIntervalSec = 3
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $repoRoot

$curl = "C:\Users\RajeevG\AppData\Local\Programs\Git\mingw64\bin\curl.exe"
if (-not (Test-Path $curl)) {
    Write-Error "curl.exe not at $curl"
    exit 1
}

# Pull SA password for the sqlcmd-based polling + staff-user-id lookup.
$saLine = Get-Content .env | Select-String '^MSSQL_SA_PASSWORD='
if (-not $saLine) { Write-Error "MSSQL_SA_PASSWORD not in .env"; exit 1 }
$saPassword = ($saLine.Line -replace '^MSSQL_SA_PASSWORD=','').Trim('"').Trim()

# ---- Helpers ---------------------------------------------------------------

function Get-Token {
    param([string]$Username, [string]$Password)
    $body = "grant_type=password&client_id=CaseEvaluation_App&username=$([uri]::EscapeDataString($Username))&password=$([uri]::EscapeDataString($Password))&scope=offline_access CaseEvaluation openid profile email roles"
    $json = & $curl -s `
        --resolve "${TenantHost}:${AuthPort}:127.0.0.1" `
        -X POST `
        "http://${TenantHost}:${AuthPort}/connect/token" `
        -H "Content-Type: application/x-www-form-urlencoded" `
        --data $body
    $obj = $null
    try { $obj = $json | ConvertFrom-Json } catch {}
    if (-not $obj -or -not $obj.access_token) {
        Write-Host "Token response: $json" -ForegroundColor Red
        Write-Error "Token acquisition failed for $Username"
        exit 3
    }
    return $obj.access_token
}

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Token,
        $Body = $null
    )
    $url = "$ApiBase$Path"
    $args = @('-s', '-w', "`n%{http_code}", '-X', $Method, $url, '-H', "Authorization: Bearer $Token")
    $tempFile = $null
    if ($Body -ne $null) {
        # PowerShell + curl + Windows command-line JSON via --data is
        # fragile (curl globbing, embedded-quote escapes, etc.). Write the
        # body to a temp file and use --data-binary @file to bypass shell
        # interpretation entirely.
        $json = $Body | ConvertTo-Json -Depth 10 -Compress
        $tempFile = [System.IO.Path]::GetTempFileName()
        [System.IO.File]::WriteAllText($tempFile, $json, [System.Text.UTF8Encoding]::new($false))
        $args += @('-H', 'Content-Type: application/json', '--data-binary', "@$tempFile")
    } else {
        $args += @('-H', 'Content-Length: 0')
    }
    try {
        $raw = & $curl @args
    } finally {
        if ($tempFile -and (Test-Path $tempFile)) { Remove-Item -Force $tempFile }
    }
    $rawStr = ($raw -join "`n")
    # Last line is the HTTP status; everything before is the response body.
    $lines = $rawStr -split "`n"
    $status = $lines[-1]
    $body = ($lines[0..($lines.Count - 2)] -join "`n")
    if ($status -notmatch '^2\d\d$') {
        Write-Host "API $Method $Path -> HTTP $status" -ForegroundColor Red
        Write-Host "Body: $body" -ForegroundColor Red
        Write-Error "API call failed"
        exit 4
    }
    if ([string]::IsNullOrWhiteSpace($body)) { return $null }
    try { return ($body | ConvertFrom-Json) }
    catch {
        Write-Host "Non-JSON response from $Method $Path : $body" -ForegroundColor Yellow
        return $body
    }
}

function Invoke-Sql {
    param([string]$Query)
    return (& docker exec replicate-old-app-sql-server-1 `
        /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $saPassword `
        -C -d CaseEvaluation -h -1 -W -Q $Query 2>&1) -join "`n"
}

# ---- Step 1: tokens --------------------------------------------------------
Write-Host "Step 1: Acquiring tokens..." -ForegroundColor Cyan
$adminToken = Get-Token -Username $AdminUser -Password $Password
$staffToken = Get-Token -Username $StaffUser -Password $Password
Write-Host "  admin token: $($adminToken.Length) chars"
Write-Host "  staff token: $($staffToken.Length) chars" -ForegroundColor Green

# Staff user id: needed for Appointment.IdentityUserId (booker) and for
# PrimaryResponsibleUserId on the approve call. Pull from the DB --
# /api/identity/users is admin-only and overkill for this single lookup.
$staffUserIdRaw = (Invoke-Sql "SET NOCOUNT ON; SELECT CAST(Id AS NVARCHAR(36)) FROM AbpUsers WHERE UserName = 'staff@falkinstein.test';").Trim()
$staffUserId = ($staffUserIdRaw -split "`n" | Where-Object { $_ -match '^[0-9A-Fa-f-]{36}$' } | Select-Object -First 1)
if (-not $staffUserId) {
    Write-Error "Could not resolve staff@falkinstein.test user id from DB."
    exit 1
}
Write-Host "  staff user id: $staffUserId" -ForegroundColor DarkGray

# ---- Step 2: reference data lookups ----------------------------------------
Write-Host "`nStep 2: Resolving reference data..." -ForegroundColor Cyan
$caState = Invoke-Api -Method GET -Path "/api/app/states?Filter=California&MaxResultCount=1" -Token $adminToken
$caStateId = $caState.items[0].id
Write-Host "  California: $caStateId"

$wcab = Invoke-Api -Method GET -Path "/api/app/wcab-offices?MaxResultCount=1" -Token $adminToken
$wcabOfficeId = $wcab.items[0].wcabOffice.id
if (-not $wcabOfficeId) { $wcabOfficeId = $wcab.items[0].id }
Write-Host "  WcabOffice: $wcabOfficeId"

$type = Invoke-Api -Method GET -Path "/api/app/appointment-types?FilterText=PQME&MaxResultCount=5" -Token $adminToken
$pqmeType = $type.items | Where-Object { $_.name -eq 'PQME' } | Select-Object -First 1
if (-not $pqmeType) { $pqmeType = $type.items[0] }
$pqmeTypeId = $pqmeType.id
Write-Host "  PQME AppointmentType: $pqmeTypeId  ($($pqmeType.name))"

$lang = Invoke-Api -Method GET -Path "/api/app/appointment-languages?FilterText=English&MaxResultCount=1" -Token $adminToken
$languageId = $lang.items[0].id
Write-Host "  English Language: $languageId"

# ---- Step 3: Doctor + Location + Availability ------------------------------
Write-Host "`nStep 3: Creating Doctor / Location / DoctorAvailability..." -ForegroundColor Cyan

$confSuffix = (Get-Date).ToString('yyMMddHHmmss')

$doctor = Invoke-Api -Method POST -Path "/api/app/doctors" -Token $adminToken -Body @{
    firstName = "Yuri"
    lastName  = "Falkinstein"
    email     = "doc-$confSuffix@falkinstein.test"
    gender    = 1  # Gender enum: 0=NotSpecified, 1=Male (depends on enum); Doctor table requires int.
    appointmentTypeIds = @($pqmeTypeId)
    locationIds = @()
}
$doctorId = $doctor.id
Write-Host "  Doctor: $doctorId"

$location = Invoke-Api -Method POST -Path "/api/app/locations" -Token $adminToken -Body @{
    name = "WCSI Main Office (E2E $confSuffix)"
    address = "12345 W. Olympic Blvd."
    city = "Los Angeles"
    zipCode = "90064"
    parkingFee = 15.00
    isActive = $true
    stateId = $caStateId
    appointmentTypeId = $pqmeTypeId
}
$locationId = $location.id
Write-Host "  Location: $locationId"

$slotDate = (Get-Date).AddDays(14).ToString('yyyy-MM-dd')
$availability = Invoke-Api -Method POST -Path "/api/app/doctor-availabilities" -Token $adminToken -Body @{
    availableDate    = "${slotDate}T00:00:00.000Z"
    fromTime         = "09:00:00"
    toTime           = "10:00:00"
    bookingStatusId  = 8  # BookingStatus.Available (enum at Domain.Shared/Enums/BookingStatus.cs)
    locationId       = $locationId
    appointmentTypeId = $pqmeTypeId
}
$availabilityId = $availability.id
Write-Host "  Availability: $availabilityId  ($slotDate 09:00)"

# ---- Step 4: Patient (via for-appointment-booking) -------------------------
Write-Host "`nStep 4: Creating Patient..." -ForegroundColor Cyan
$patientResp = Invoke-Api -Method POST -Path "/api/app/patients/for-appointment-booking/get-or-create" -Token $adminToken -Body @{
    firstName            = "Synthetic"
    lastName             = "TestPatient-$confSuffix"
    middleName           = "M"
    email                = "patient-$confSuffix@example.test"
    genderId             = 1
    dateOfBirth          = "1985-03-15T00:00:00.000Z"
    phoneNumber          = "555-0100"
    socialSecurityNumber = "999-00-0001"
    address              = "100 Main St"
    city                 = "Los Angeles"
    zipCode              = "90064"
    cellPhoneNumber      = "555-0101"
    phoneNumberTypeId    = 1
    street               = "100 Main St"
    stateId              = $caStateId
    appointmentLanguageId = $languageId
}
# The endpoint returns PatientWithNavigationPropertiesDto -- structure may
# be either { patient: {...}, identityUser: {...} } or flat. Check both.
$patientId = $null
if ($patientResp.patient -and $patientResp.patient.id) { $patientId = $patientResp.patient.id }
elseif ($patientResp.id) { $patientId = $patientResp.id }
if (-not $patientId) {
    Write-Host "Patient response shape unexpected: $($patientResp | ConvertTo-Json -Depth 5)" -ForegroundColor Red
    exit 4
}
Write-Host "  Patient: $patientId"

# ---- Step 5: Appointment ---------------------------------------------------
Write-Host "`nStep 5: Creating Appointment..." -ForegroundColor Cyan
$apptDate = "${slotDate}T09:00:00.000Z"
$dueDate  = (Get-Date).AddDays(45).ToString('yyyy-MM-ddT00:00:00.000Z')
$appt = Invoke-Api -Method POST -Path "/api/app/appointments" -Token $adminToken -Body @{
    panelNumber               = "PAN-$confSuffix"
    appointmentDate           = $apptDate
    requestConfirmationNumber = "PLACEHOLDER"  # server overwrites via AppointmentManager.CreateAsync
    dueDate                   = $dueDate
    appointmentStatus         = 1  # Pending (per AppointmentStatusType enum at Domain.Shared/Enums/AppointmentStatusType.cs)
    patientId                 = $patientId
    identityUserId            = $staffUserId
    appointmentTypeId         = $pqmeTypeId
    locationId                = $locationId
    doctorAvailabilityId      = $availabilityId
    patientEmail              = "patient-$confSuffix@example.test"
    applicantAttorneyEmail    = "atty.applicant-$confSuffix@example.test"
    defenseAttorneyEmail      = "atty.defense-$confSuffix@example.test"
    claimExaminerEmail        = "examiner-$confSuffix@example.test"
    isPatientAlreadyExist     = $false
    customFieldValues         = @()
}
$appointmentId = $appt.id
$confNumber    = $appt.requestConfirmationNumber
Write-Host "  Appointment: $appointmentId  ($confNumber)"

# ---- Step 6: Employer detail -----------------------------------------------
Write-Host "`nStep 6: Creating child entities..." -ForegroundColor Cyan
$null = Invoke-Api -Method POST -Path "/api/app/appointment-employer-details" -Token $adminToken -Body @{
    appointmentId = $appointmentId
    employerName  = "Acme Manufacturing Co."
    occupation    = "Assembly Line Worker"
    phoneNumber   = "555-0400"
    street        = "400 Industrial Park"
    city          = "Long Beach"
    zipCode       = "90802"
    stateId       = $caStateId
}
Write-Host "  EmployerDetail OK"

# ---- Step 7: Applicant + Defense attorney upserts --------------------------
# The /appointments/{id}/applicant-attorney route creates an ApplicantAttorney
# and the M2M link in one call. IdentityUserId is required but we re-use
# staffUserId as a placeholder -- semantic accuracy doesn't affect the
# packet render which only reads the attorney FirmName/Street/City/State/Zip.
$null = Invoke-Api -Method POST -Path "/api/app/appointments/$appointmentId/applicant-attorney" -Token $adminToken -Body @{
    applicantAttorneyId = $null
    identityUserId      = $staffUserId
    firstName           = "John"
    lastName            = "Applicant"
    email               = "atty.applicant-$confSuffix@example.test"
    firmName            = "Smith & Associates"
    phoneNumber         = "555-0200"
    street              = "200 Attorney Way"
    city                = "Los Angeles"
    stateId             = $caStateId
    zipCode             = "90067"
}
Write-Host "  ApplicantAttorney upsert OK"

$null = Invoke-Api -Method POST -Path "/api/app/appointments/$appointmentId/defense-attorney" -Token $adminToken -Body @{
    defenseAttorneyId = $null
    identityUserId    = $staffUserId
    firstName         = "Jane"
    lastName          = "Defense"
    email             = "atty.defense-$confSuffix@example.test"
    firmName          = "Defense Law Group"
    phoneNumber       = "555-0300"
    street            = "300 Defense Plaza"
    city              = "Los Angeles"
    stateId           = $caStateId
    zipCode           = "90071"
}
Write-Host "  DefenseAttorney upsert OK"

# ---- Step 8: Injury detail + insurance + claim examiner + body parts ------
$injury = Invoke-Api -Method POST -Path "/api/app/appointment-injury-details" -Token $adminToken -Body @{
    appointmentId      = $appointmentId
    dateOfInjury       = "2025-11-10T00:00:00.000Z"
    toDateOfInjury     = $null
    claimNumber        = "CLM-E2E-$confSuffix"
    isCumulativeInjury = $false
    wcabAdj            = "ADJ-7654321"
    bodyPartsSummary   = "Lower Back, Right Shoulder, Left Shoulder"
    wcabOfficeId       = $wcabOfficeId
}
$injuryId = $injury.id
Write-Host "  InjuryDetail: $injuryId"

$null = Invoke-Api -Method POST -Path "/api/app/appointment-primary-insurances" -Token $adminToken -Body @{
    appointmentInjuryDetailId = $injuryId
    isActive          = $true
    name              = "State Farm Workers Comp"
    insuranceNumber   = "INS-987654"
    phoneNumber       = "555-0500"
    street            = "500 Insurance Way"
    city              = "San Francisco"
    zip               = "94104"
    stateId           = $caStateId
}
Write-Host "  PrimaryInsurance OK"

$null = Invoke-Api -Method POST -Path "/api/app/appointment-claim-examiners" -Token $adminToken -Body @{
    appointmentInjuryDetailId = $injuryId
    isActive            = $true
    name                = "Jane Examiner"
    claimExaminerNumber = "EX-12345"
    email               = "examiner-$confSuffix@example.test"
    phoneNumber         = "555-0600"
    street              = "600 Examiner Blvd"
    city                = "Oakland"
    zip                 = "94612"
    stateId             = $caStateId
}
Write-Host "  ClaimExaminer OK"

foreach ($bp in @("Lower Back", "Right Shoulder", "Left Shoulder")) {
    $null = Invoke-Api -Method POST -Path "/api/app/appointment-body-parts" -Token $adminToken -Body @{
        appointmentInjuryDetailId = $injuryId
        bodyPartDescription       = $bp
    }
}
Write-Host "  3 BodyParts OK"

# ---- Step 9: Approve as Clinic Staff (if needed) ---------------------------
# AppointmentsAppService.CreateAsync auto-approves when the booker is an
# internal user (admin/staff/supervisor/IT admin) per OLD parity
# (AppointmentsAppService.cs:668-680). The Phase 11h fast-path skips the
# Pending step entirely. We check the appointment's current status before
# attempting the explicit approve; if it's already Approved (2), there is
# nothing to do here -- the AppointmentStatusChangedEto was already
# published from CreateAsync, which should have triggered packet generation.
$apptDetail = Invoke-Api -Method GET -Path "/api/app/appointments/$appointmentId" -Token $staffToken
Write-Host "`nStep 9: Appointment status after create = $($apptDetail.appointmentStatus)" -ForegroundColor Cyan
if ($apptDetail.appointmentStatus -eq 2) {
    Write-Host "  Already Approved (internal-user fast-path)." -ForegroundColor Yellow
} else {
    Write-Host "  Approving via /appointment-approvals/$appointmentId/approve..." -ForegroundColor Cyan
    $null = Invoke-Api -Method POST -Path "/api/app/appointment-approvals/$appointmentId/approve" -Token $staffToken -Body @{
        primaryResponsibleUserId = $staffUserId
        overridePatientMatch     = $false
        internalUserComments     = "End-to-end API test approval"
    }
    Write-Host "  Approved." -ForegroundColor Green
}

# ---- Step 10: Poll for 3 generated packets ---------------------------------
Write-Host "`nStep 10: Polling for packet generation (max ${PollMaxSec}s)..." -ForegroundColor Cyan
$pollDeadline = (Get-Date).AddSeconds($PollMaxSec)
$ready = $false
$lastSummary = ""
while ((Get-Date) -lt $pollDeadline) {
    $countQuery = "SET NOCOUNT ON; SELECT COUNT(*) FROM AppAppointmentPackets WHERE AppointmentId = '$appointmentId' AND Status = 2;"
    $statusQuery = "SET NOCOUNT ON; SELECT Kind, Status, COALESCE(ErrorMessage,'') FROM AppAppointmentPackets WHERE AppointmentId = '$appointmentId' ORDER BY Kind;"
    $countRaw = Invoke-Sql -Query $countQuery
    $countMatch = ($countRaw -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^\d+$' } | Select-Object -First 1)
    $count = if ($countMatch) { [int]$countMatch } else { 0 }
    $rowsRaw = (Invoke-Sql -Query $statusQuery).Trim()
    if ($rowsRaw -ne $lastSummary) {
        Write-Host "  state: $rowsRaw"
        $lastSummary = $rowsRaw
    }
    if ($count -ge 3) { $ready = $true; break }
    Start-Sleep -Seconds $PollIntervalSec
}
if (-not $ready) {
    Write-Error "Packet generation did not complete (3 Generated rows) within ${PollMaxSec}s. Last state: $lastSummary"
    exit 6
}
Write-Host "  All 3 packets generated." -ForegroundColor Green

# ---- Step 11: Download each kind -------------------------------------------
Write-Host "`nStep 11: Downloading PDF packets..." -ForegroundColor Cyan
$outDir = Join-Path $repoRoot (Join-Path $OutRoot $appointmentId)
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$kinds = @('Patient','Doctor','AttorneyClaimExaminer')
$dlFailures = @()
foreach ($kind in $kinds) {
    $out = Join-Path $outDir "$kind.pdf"
    $httpCode = & $curl -s -w "%{http_code}" -o $out `
        -X GET `
        "$ApiBase/api/app/appointments/$appointmentId/packet/download/$kind" `
        -H "Authorization: Bearer $staffToken"
    $bytes = if (Test-Path $out) { (Get-Item $out).Length } else { 0 }
    Write-Host ("  {0,-22} HTTP {1}  {2:N0} bytes -> {3}" -f $kind, $httpCode, $bytes, $out)
    if ($httpCode -ne '200' -or $bytes -lt 10000) { $dlFailures += "$kind ($httpCode, $bytes bytes)" }
}
if ($dlFailures.Count -gt 0) {
    Write-Error "Download failures: $($dlFailures -join '; ')"
    exit 7
}

Write-Host "`nSUCCESS" -ForegroundColor Green
Write-Host "Appointment:   $appointmentId"
Write-Host "Confirmation:  $confNumber"
Write-Host "Output dir:    $outDir"
exit 0
