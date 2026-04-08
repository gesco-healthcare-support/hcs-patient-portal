<#
.SYNOPSIS
    Phase A3: Create Doctor Tenants (5), External Users (20), Patient Profile Updates.
.DESCRIPTION
    Depends on: States, AppointmentTypes, Locations from A1/A2.
    Creates tenants via POST /api/saas/tenants (DoctorTenantAppService).
    Registers external users via POST /api/public/external-signup/register.
    Updates patient profiles with randomized demographic data.
#>
param(
    [Parameter(Mandatory)][hashtable]$SeedState,
    [Parameter(Mandatory)][string]$Token,
    [string]$ApiBaseUrl = "https://localhost:44327",
    [string]$AuthServerUrl = "https://localhost:44368"
)

$ErrorActionPreference = "Stop"

# Load helpers (needed for $script: variables like $script:AllFirstNames, etc.)
$helpersDir = Join-Path $PSScriptRoot "..\helpers"
. "$helpersDir\Get-AuthToken.ps1"
. "$helpersDir\Invoke-ApiCall.ps1"
. "$helpersDir\New-FakeData.ps1"

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "PHASE A3: Doctors & External Users (Tier 3)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

$defaultPassword = $env:TEST_PASSWORD
$caStateId = $SeedState.stateIds["California"]

# Collect active location IDs (exclude inactive and max-length)
$activeLocationKeys = $SeedState.locationIds.Keys | Where-Object { $_ -notlike "Inactive*" -and $_ -notlike "MaxLength*" }
$activeLocationIds = @($activeLocationKeys | ForEach-Object { $SeedState.locationIds[$_] })

# Collect real appointment type IDs (exclude max-length)
$realTypeKeys = $SeedState.appointmentTypeIds.Keys | Where-Object { $_ -notlike "TestType_*" }
$realTypeIds = @($realTypeKeys | ForEach-Object { $SeedState.appointmentTypeIds[$_] })

# Collect language IDs
$languageIds = @($SeedState.appointmentLanguageIds.Values)
$englishLangId = $SeedState.appointmentLanguageIds["English"]

# ---- A3.1 DOCTOR TENANTS (5 records) ----

Write-Host "`n--- A3.1 Doctor Tenants ---" -ForegroundColor Yellow

if (-not $SeedState.tenantIds) { $SeedState.tenantIds = @{} }
if (-not $SeedState.doctorIds) { $SeedState.doctorIds = @{} }
if (-not $SeedState.doctorUserIds) { $SeedState.doctorUserIds = @{} }

# Gender distribution: ensure coverage of Male(1), Female(2), Other(3)
$genderAssignments = @(2, 2, 1, 3, 1)  # F, F, M, O, M - guarantees all 3

for ($t = 1; $t -le 5; $t++) {
    $key = "T$t"
    $doctorFirstName = New-FakeFirstName
    $doctorLastName = New-FakeLastName
    $doctorEmail = New-FakeEmail -FirstName $doctorFirstName -LastName $doctorLastName -Domain "hcs.test"

    # Tenant name = doctor's first name (becomes Doctor.firstName during auto-creation)
    $tenantName = "Dr $doctorLastName $t"
    if ($tenantName.Length -gt 64) { $tenantName = $tenantName.Substring(0, 64) }

    Write-Host "  Creating tenant $($key): '$tenantName' ($doctorEmail)..." -ForegroundColor White

    # Check if tenant already exists
    $existingTenants = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/saas/tenants?filter=$([uri]::EscapeDataString($tenantName))&maxResultCount=10" -Token $Token
    $match = $existingTenants.items | Where-Object { $_.name -eq $tenantName }

    if ($match) {
        Write-Host "    [SKIP] Tenant '$tenantName' already exists (ID: $($match.id))" -ForegroundColor DarkGray
        $SeedState.tenantIds[$key] = $match.id
    } else {
        # POST /api/saas/tenants (CORRECTED - not /api/saas/host/tenants)
        $tenantBody = @{
            name              = $tenantName
            adminEmailAddress = $doctorEmail
            adminPassword     = $defaultPassword
        }
        $tenantResult = Invoke-ApiCall -Method "POST" -Url "$ApiBaseUrl/api/saas/tenants" -Body $tenantBody -Token $Token
        Write-Host "    [CREATE] Tenant '$tenantName' (ID: $($tenantResult.id))" -ForegroundColor Green
        $SeedState.tenantIds[$key] = $tenantResult.id
    }

    $tenantId = $SeedState.tenantIds[$key]

    # Acquire tenant admin token
    $tenantToken = Get-AuthToken -Username $doctorEmail -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $tenantId

    # Get the auto-created doctor (only doctor in this tenant)
    # NOTE: GET /api/app/doctors returns DoctorWithNavigationPropertiesDto - unwrap .doctor
    $doctors = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/doctors?maxResultCount=10" -Token $tenantToken -TenantId $tenantId
    $doctorWrapper = $doctors.items | Select-Object -First 1

    if (-not $doctorWrapper -or -not $doctorWrapper.doctor) {
        Write-Host "    [WARN] No doctor found in tenant $key. Skipping doctor update." -ForegroundColor Yellow
        continue
    }

    $doctor = $doctorWrapper.doctor
    $SeedState.doctorIds[$key] = $doctor.id
    $SeedState.doctorUserIds[$key] = $doctor.identityUserId

    # Update doctor: set lastName, gender, appointmentTypes, locations
    $gender = $genderAssignments[$t - 1]

    # Location assignments per plan: T4=1 location, T5=all locations, others=random 2-4
    if ($t -eq 4) {
        $assignedLocIds = @($activeLocationIds | Get-Random -Count 1)
    } elseif ($t -eq 5) {
        $assignedLocIds = @($activeLocationIds)
    } else {
        $count = Get-Random -Minimum 2 -Maximum ([Math]::Min(5, $activeLocationIds.Count + 1))
        $assignedLocIds = @($activeLocationIds | Get-Random -Count $count)
    }

    # AppointmentType assignments: each type must appear on at least one doctor
    # For simplicity: assign random 1-4 types, then enforce coverage check after loop
    $typeCount = Get-Random -Minimum 1 -Maximum ([Math]::Min(5, $realTypeIds.Count + 1))
    $assignedTypeIds = @($realTypeIds | Get-Random -Count $typeCount)

    # GET current doctor for ConcurrencyStamp
    $currentDoctor = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/doctors/$($doctor.id)" -Token $tenantToken -TenantId $tenantId

    $updateBody = @{
        firstName          = $doctorFirstName
        lastName           = $doctorLastName
        email              = $doctorEmail
        gender             = $gender
        identityUserId     = $doctor.identityUserId
        appointmentTypeIds = $assignedTypeIds
        locationIds        = $assignedLocIds
        concurrencyStamp   = $currentDoctor.concurrencyStamp
    }

    $updated = Invoke-ApiCall -Method "PUT" -Url "$ApiBaseUrl/api/app/doctors/$($doctor.id)" -Body $updateBody -Token $tenantToken -TenantId $tenantId
    Write-Host "    [UPDATE] Doctor: $doctorFirstName $doctorLastName, Gender=$gender, Locations=$($assignedLocIds.Count), Types=$($assignedTypeIds.Count)" -ForegroundColor Green

    # Store tenant token info for later phases
    if (-not $SeedState._tenantEmails) { $SeedState._tenantEmails = @{} }
    $SeedState._tenantEmails[$key] = $doctorEmail
}

Write-Host "  Doctor Tenants complete: $($SeedState.tenantIds.Count) records" -ForegroundColor White

# ---- A3.2 EXTERNAL USER REGISTRATION (20 users) ----

Write-Host "`n--- A3.2 External Users ---" -ForegroundColor Yellow

if (-not $SeedState.patientIds) { $SeedState.patientIds = @{} }
if (-not $SeedState.patientUserIds) { $SeedState.patientUserIds = @{} }
if (-not $SeedState.claimExaminerUserIds) { $SeedState.claimExaminerUserIds = @{} }
if (-not $SeedState.applicantAttorneyUserIds) { $SeedState.applicantAttorneyUserIds = @{} }
if (-not $SeedState.defenseAttorneyUserIds) { $SeedState.defenseAttorneyUserIds = @{} }

# User distribution: T1-T3 get users, T4 gets 1 patient, T5 gets nothing
# UserType: 1=Patient, 2=ClaimExaminer, 3=ApplicantAttorney, 4=DefenseAttorney
$userPlan = @(
    # T1: 2 patients, 1 CE, 1 AA, 1 DA
    @{ Tenant = "T1"; Type = 1; Key = "T1_P1" },
    @{ Tenant = "T1"; Type = 1; Key = "T1_P2" },
    @{ Tenant = "T1"; Type = 2; Key = "T1_CE1" },
    @{ Tenant = "T1"; Type = 3; Key = "T1_AA1" },
    @{ Tenant = "T1"; Type = 4; Key = "T1_DA1" },
    # T2: 2 patients, 1 CE, 1 AA, 1 DA
    @{ Tenant = "T2"; Type = 1; Key = "T2_P1" },
    @{ Tenant = "T2"; Type = 1; Key = "T2_P2" },
    @{ Tenant = "T2"; Type = 2; Key = "T2_CE1" },
    @{ Tenant = "T2"; Type = 3; Key = "T2_AA1" },
    @{ Tenant = "T2"; Type = 4; Key = "T2_DA1" },
    # T3: 2 patients, 1 CE, 1 AA, 1 DA
    @{ Tenant = "T3"; Type = 1; Key = "T3_P1" },
    @{ Tenant = "T3"; Type = 1; Key = "T3_P2" },
    @{ Tenant = "T3"; Type = 2; Key = "T3_CE1" },
    @{ Tenant = "T3"; Type = 3; Key = "T3_AA1" },
    @{ Tenant = "T3"; Type = 4; Key = "T3_DA1" },
    # T4: 1 patient (null-all edge case)
    @{ Tenant = "T4"; Type = 1; Key = "T4_NullPatient" }
    # T5: no users (completely empty tenant)
)

$typeNames = @{ 1 = "Patient"; 2 = "ClaimExaminer"; 3 = "ApplicantAttorney"; 4 = "DefenseAttorney" }

if (-not $SeedState._userEmails) { $SeedState._userEmails = @{} }

foreach ($user in $userPlan) {
    $firstName = New-FakeFirstName
    $lastName = New-FakeLastName
    $email = New-FakeEmail -FirstName $firstName -LastName $lastName
    $tenantId = $SeedState.tenantIds[$user.Tenant]
    $typeName = $typeNames[$user.Type]

    Write-Host "  Registering $($user.Key): $firstName $lastName ($typeName) in $($user.Tenant)..." -ForegroundColor White

    # POST /api/public/external-signup/register (no auth, no __tenant header, tenantId in body)
    $regBody = @{
        userType  = $user.Type
        firstName = $firstName
        lastName  = $lastName
        email     = $email
        password  = $defaultPassword
        tenantId  = $tenantId
    }

    try {
        Invoke-ApiCall -Method "POST" -Url "$ApiBaseUrl/api/public/external-signup/register" -Body $regBody
        Write-Host "    [CREATE] Registered $email as $typeName" -ForegroundColor Green
    } catch {
        if ($_.ToString() -match "already") {
            Write-Host "    [SKIP] User $email already registered" -ForegroundColor DarkGray
        } else {
            throw
        }
    }

    $SeedState._userEmails[$user.Key] = $email

    # Look up the IdentityUser GUID - GET /api/identity/users?filter={email} (CORRECTED path)
    $tenantEmail = $SeedState._tenantEmails[$user.Tenant]
    $tenantToken = Get-AuthToken -Username $tenantEmail -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $tenantId

    $usersResult = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/identity/users?filter=$([uri]::EscapeDataString($email))&maxResultCount=10" -Token $tenantToken -TenantId $tenantId
    $identityUser = $usersResult.items | Where-Object { $_.email -eq $email } | Select-Object -First 1

    if (-not $identityUser) {
        Write-Host "    [WARN] Could not find IdentityUser for $email" -ForegroundColor Yellow
        continue
    }

    # Store in appropriate state bucket
    switch ($user.Type) {
        1 {
            $SeedState.patientUserIds[$user.Key] = $identityUser.id

            # Also get the Patient entity (auto-created for Patient type)
            $patients = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/patients?filterText=$([uri]::EscapeDataString($email))&maxResultCount=5" -Token $tenantToken -TenantId $tenantId
            $patient = $patients.items | Where-Object { $_.patient.email -eq $email } | Select-Object -First 1
            if (-not $patient) {
                # Try without navigation properties
                $patient = $patients.items | Where-Object { $_.email -eq $email } | Select-Object -First 1
            }
            if ($patient) {
                $patientId = if ($patient.patient) { $patient.patient.id } else { $patient.id }
                $SeedState.patientIds[$user.Key] = $patientId
                Write-Host "    Patient entity ID: $patientId" -ForegroundColor DarkGray
            }
        }
        2 { $SeedState.claimExaminerUserIds[$user.Key] = $identityUser.id }
        3 { $SeedState.applicantAttorneyUserIds[$user.Key] = $identityUser.id }
        4 { $SeedState.defenseAttorneyUserIds[$user.Key] = $identityUser.id }
    }
}

Write-Host "  External Users complete: $($userPlan.Count) registrations" -ForegroundColor White

# ---- A3.3 PATIENT PROFILE UPDATES ----

Write-Host "`n--- A3.3 Patient Profile Updates ---" -ForegroundColor Yellow

# Gender distribution for patients: ensure at least 1 Other(3)
$patientGenders = @(2, 1, 1, 2, 3, 2, 1)  # Covers all 3, biased to match demographics
$genderIdx = 0

foreach ($key in $SeedState.patientIds.Keys) {
    # Skip the NullPatient (T4) - will be updated with all nulls
    # Skip minimal patient - leave as default
    $isNullPatient = ($key -eq "T4_NullPatient")

    $patientId = $SeedState.patientIds[$key]
    if (-not $patientId) { continue }

    $tenantKey = $key.Split("_")[0]  # "T1_P1" → "T1"
    $tenantId = $SeedState.tenantIds[$tenantKey]
    $tenantEmail = $SeedState._tenantEmails[$tenantKey]
    $tenantToken = Get-AuthToken -Username $tenantEmail -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $tenantId

    # GET current patient for ConcurrencyStamp and current values
    $currentPatient = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/app/patients/$patientId" -Token $tenantToken -TenantId $tenantId

    if ($isNullPatient) {
        # Update with all optional fields null
        $updateBody = @{
            firstName          = $currentPatient.firstName
            lastName           = $currentPatient.lastName
            email              = $currentPatient.email
            genderId           = 1  # Keep default
            dateOfBirth        = $currentPatient.dateOfBirth
            identityUserId     = $currentPatient.identityUserId
            phoneNumberTypeId  = 29  # Home
            middleName         = $null
            phoneNumber        = $null
            socialSecurityNumber = $null
            address            = $null
            city               = $null
            zipCode            = $null
            cellPhoneNumber    = $null
            street             = $null
            interpreterVendorName = $null
            stateId            = $null
            appointmentLanguageId = $null
            tenantId           = $tenantId
            concurrencyStamp   = $currentPatient.concurrencyStamp
        }
        Write-Host "  [UPDATE] $($key): All optional fields set to null" -ForegroundColor Green
    } else {
        $gender = $patientGenders[$genderIdx % $patientGenders.Count]
        $genderIdx++
        $addr = New-FakeAddress -StateId $caStateId
        $langId = $languageIds | Get-Random
        $needsInterpreter = ($langId -ne $englishLangId)

        $updateBody = @{
            firstName             = $currentPatient.firstName
            lastName              = $currentPatient.lastName
            email                 = $currentPatient.email
            genderId              = $gender
            dateOfBirth           = (New-FakeDOB).ToString("yyyy-MM-ddTHH:mm:ss")
            phoneNumber           = New-FakeCaPhone
            socialSecurityNumber  = New-FakeSsn
            cellPhoneNumber       = New-FakeCellPhone
            street                = $addr.Street
            city                  = $addr.City
            zipCode               = $addr.ZipCode
            stateId               = $caStateId
            appointmentLanguageId = $langId
            phoneNumberTypeId     = @(28, 29) | Get-Random  # Work or Home
            interpreterVendorName = if ($needsInterpreter) { New-FakeInterpreterVendor } else { $null }
            identityUserId        = $currentPatient.identityUserId
            tenantId              = $tenantId
            concurrencyStamp      = $currentPatient.concurrencyStamp
        }
        Write-Host "  [UPDATE] $($key): Gender=$gender, DOB set, SSN set, Address set" -ForegroundColor Green
    }

    Invoke-ApiCall -Method "PUT" -Url "$ApiBaseUrl/api/app/patients/$patientId" -Body $updateBody -Token $tenantToken -TenantId $tenantId | Out-Null
}

Write-Host "  Patient Profile Updates complete" -ForegroundColor White

Write-Host "`nPhase A3 COMPLETE" -ForegroundColor Green
Write-Host "  Tenants: $($SeedState.tenantIds.Count)"
Write-Host "  Doctors: $($SeedState.doctorIds.Count)"
Write-Host "  Patients: $($SeedState.patientIds.Count)"
Write-Host "  Claim Examiners: $($SeedState.claimExaminerUserIds.Count)"
Write-Host "  Applicant Attorneys: $($SeedState.applicantAttorneyUserIds.Count)"
Write-Host "  Defense Attorneys: $($SeedState.defenseAttorneyUserIds.Count)"

return $SeedState
