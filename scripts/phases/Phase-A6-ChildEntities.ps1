<#
.SYNOPSIS
    Phase A6: Create Appointment Child Entities (Tier 6).
.DESCRIPTION
    Creates: ApplicantAttorneys (4), AppointmentEmployerDetails (~12),
    AppointmentAccessors (~16), AppointmentApplicantAttorneys (~8 links).
    Depends on: All previous phases.
    Auth: Tenant admin per tenant, __tenant header required.
#>
param(
    [Parameter(Mandatory)][hashtable]$SeedState,
    [string]$ApiBaseUrl = "https://localhost:44327",
    [string]$AuthServerUrl = "https://localhost:44368"
)

$ErrorActionPreference = "Stop"

# Load helpers (needed for $script: variables like $script:AllFirstNames, etc.)
$helpersDir = Join-Path $PSScriptRoot "..\helpers"
. "$helpersDir\Get-AuthToken.ps1"
. "$helpersDir\Invoke-ApiCall.ps1"
. "$helpersDir\New-FakeData.ps1"

$defaultPassword = $env:TEST_PASSWORD

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "PHASE A6: Child Entities (Tier 6)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

$caStateId = $SeedState.stateIds["California"]

if (-not $SeedState.applicantAttorneyEntityIds) { $SeedState.applicantAttorneyEntityIds = @{} }
if (-not $SeedState.appointmentEmployerDetailIds) { $SeedState.appointmentEmployerDetailIds = @{} }
if (-not $SeedState.appointmentAccessorIds) { $SeedState.appointmentAccessorIds = @() }
if (-not $SeedState.appointmentApplicantAttorneyIds) { $SeedState.appointmentApplicantAttorneyIds = @{} }

# ---- A6.1 APPLICANT ATTORNEY ENTITIES (4 records) ----

Write-Host "`n--- A6.1 Applicant Attorney Entities ---" -ForegroundColor Yellow

foreach ($tenantKey in @("T1", "T2", "T3", "T4")) {
    $aaUserKey = "${tenantKey}_AA1"
    if ($tenantKey -eq "T4") { $aaUserKey = "${tenantKey}_NullPatient" }  # T4 has no AA user

    # Check if we have an AA user for this tenant
    $aaUserId = $SeedState.applicantAttorneyUserIds[$aaUserKey]
    if (-not $aaUserId -and $tenantKey -ne "T4") {
        $aaUserKey = ($SeedState.applicantAttorneyUserIds.Keys | Where-Object { $_ -like "${tenantKey}_*" } | Select-Object -First 1)
        $aaUserId = $SeedState.applicantAttorneyUserIds[$aaUserKey]
    }

    if (-not $aaUserId) {
        Write-Host "  [SKIP] No AA user for $tenantKey" -ForegroundColor DarkGray
        continue
    }

    $tenantId = $SeedState.tenantIds[$tenantKey]
    $tenantEmail = $SeedState._tenantEmails[$tenantKey]
    $tenantToken = Get-AuthToken -Username $tenantEmail -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $tenantId

    $isNullFirm = ($tenantKey -eq "T4")  # T4 AA: all optional fields null

    if ($isNullFirm) {
        $body = @{
            identityUserId = $aaUserId
        }
    } else {
        $aaLastName = New-FakeLastName
        $firmName = New-FakeFirmName -LawyerLastName $aaLastName
        $addr = New-FakeAddress -StateId $caStateId

        $body = @{
            identityUserId = $aaUserId
            firmName       = $firmName
            firmAddress    = $addr.Street
            webAddress     = New-FakeWebAddress -FirmName $firmName
            phoneNumber    = New-FakeCaPhone
            faxNumber      = New-FakeFaxNumber
            street         = $addr.Street
            city           = $addr.City
            zipCode        = $addr.ZipCode
            stateId        = $caStateId
        }
    }

    try {
        $result = Invoke-ApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/applicant-attorneys" -Body $body -Token $tenantToken -TenantId $tenantId
        $SeedState.applicantAttorneyEntityIds[$aaUserKey] = $result.id
        $label = if ($isNullFirm) { "(null firm)" } else { $body.firmName }
        Write-Host "  [CREATE] $tenantKey AA: $label (ID: $($result.id))" -ForegroundColor Green
    } catch {
        Write-Host "  [ERROR] Failed to create AA for $tenantKey : $_" -ForegroundColor Red
    }
}

Write-Host "  Applicant Attorneys: $($SeedState.applicantAttorneyEntityIds.Count) records" -ForegroundColor White

# ---- A6.2 APPOINTMENT EMPLOYER DETAILS (~12 records) ----

Write-Host "`n--- A6.2 Appointment Employer Details ---" -ForegroundColor Yellow

$apptKeys = @($SeedState.appointmentIds.Keys)
# ~60% of appointments get employer details
$targetCount = [Math]::Ceiling($apptKeys.Count * 0.6)
$selectedApptKeys = @($apptKeys | Get-Random -Count ([Math]::Min($targetCount, $apptKeys.Count)))

$empDetailCount = 0

foreach ($apptKey in $selectedApptKeys) {
    $apptId = $SeedState.appointmentIds[$apptKey]
    $tenantKey = $apptKey.Split("_")[0]
    $tenantId = $SeedState.tenantIds[$tenantKey]
    $tenantEmail = $SeedState._tenantEmails[$tenantKey]
    $tenantToken = Get-AuthToken -Username $tenantEmail -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $tenantId

    $empData = New-FakeEmployerData
    $addr = New-FakeAddress -StateId $caStateId

    $body = @{
        appointmentId = $apptId
        employerName  = $empData.EmployerName
        occupation    = $empData.Occupation
        phoneNumber   = (New-FakeCellPhone)  # 10 digits fits 12-char max
        street        = $addr.Street
        city          = $addr.City
        zipCode       = $addr.ZipCode
        stateId       = $caStateId
    }

    try {
        $result = Invoke-ApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/appointment-employer-details" -Body $body -Token $tenantToken -TenantId $tenantId
        $detailKey = "${apptKey}_emp"
        $SeedState.appointmentEmployerDetailIds[$detailKey] = $result.id
        $empDetailCount++
        Write-Host "    [CREATE] $($apptKey): $($empData.EmployerName) / $($empData.Occupation)" -ForegroundColor DarkGray
    } catch {
        Write-Host "    [ERROR] Failed for $apptKey : $_" -ForegroundColor Red
    }
}

# Max-length record on the first appointment
if ($apptKeys.Count -gt 0) {
    $firstApptKey = $apptKeys[0]
    $firstApptId = $SeedState.appointmentIds[$firstApptKey]
    $tenantKey = $firstApptKey.Split("_")[0]
    $tenantId = $SeedState.tenantIds[$tenantKey]
    $tenantEmail = $SeedState._tenantEmails[$tenantKey]
    $tenantToken = Get-AuthToken -Username $tenantEmail -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $tenantId

    $maxBody = @{
        appointmentId = $firstApptId
        employerName  = New-MaxLengthString -Length 255 -Prefix "MaxEmp_"
        occupation    = New-MaxLengthString -Length 255 -Prefix "MaxOcc_"
        phoneNumber   = "1234567890"  # 10 digits fits 12-char max
        street        = New-MaxLengthString -Length 255 -Prefix "MaxSt_"
        city          = New-MaxLengthString -Length 255 -Prefix "MaxCity_"
        zipCode       = "1234567890"  # 10-char max
        stateId       = $caStateId
    }

    try {
        $result = Invoke-ApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/appointment-employer-details" -Body $maxBody -Token $tenantToken -TenantId $tenantId
        $SeedState.appointmentEmployerDetailIds["MaxLengthRecord"] = $result.id
        $empDetailCount++
        Write-Host "    [CREATE] Max-length employer detail record" -ForegroundColor Green
    } catch {
        Write-Host "    [ERROR] Max-length employer detail: $_" -ForegroundColor Red
    }
}

# Null-optional record
if ($apptKeys.Count -gt 1) {
    $nullApptKey = $apptKeys[1]
    $nullApptId = $SeedState.appointmentIds[$nullApptKey]
    $tenantKey = $nullApptKey.Split("_")[0]
    $tenantId = $SeedState.tenantIds[$tenantKey]
    $tenantEmail = $SeedState._tenantEmails[$tenantKey]
    $tenantToken = Get-AuthToken -Username $tenantEmail -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $tenantId

    $nullBody = @{
        appointmentId = $nullApptId
        employerName  = "Self-Employed Contractor"
        occupation    = "Independent Worker"
    }

    try {
        $result = Invoke-ApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/appointment-employer-details" -Body $nullBody -Token $tenantToken -TenantId $tenantId
        $SeedState.appointmentEmployerDetailIds["NullOptionalRecord"] = $result.id
        $empDetailCount++
        Write-Host "    [CREATE] Null-optional employer detail record" -ForegroundColor Green
    } catch {
        Write-Host "    [ERROR] Null-optional employer detail: $_" -ForegroundColor Red
    }
}

Write-Host "  Employer Details: $empDetailCount records" -ForegroundColor White

# ---- A6.3 APPOINTMENT ACCESSORS (~16 records) ----

Write-Host "`n--- A6.3 Appointment Accessors ---" -ForegroundColor Yellow

$accessorCount = 0

foreach ($tenantKey in @("T1", "T2", "T3")) {
    $tenantId = $SeedState.tenantIds[$tenantKey]
    $tenantEmail = $SeedState._tenantEmails[$tenantKey]
    $tenantToken = Get-AuthToken -Username $tenantEmail -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $tenantId

    # Collect external user IDs for this tenant
    $externalUsers = @()
    $ceKey = "${tenantKey}_CE1"
    $aaKey = "${tenantKey}_AA1"
    $daKey = "${tenantKey}_DA1"

    if ($SeedState.claimExaminerUserIds[$ceKey]) {
        $externalUsers += @{ UserId = $SeedState.claimExaminerUserIds[$ceKey]; Type = "CE" }
    }
    if ($SeedState.applicantAttorneyUserIds[$aaKey]) {
        $externalUsers += @{ UserId = $SeedState.applicantAttorneyUserIds[$aaKey]; Type = "AA" }
    }
    if ($SeedState.defenseAttorneyUserIds[$daKey]) {
        $externalUsers += @{ UserId = $SeedState.defenseAttorneyUserIds[$daKey]; Type = "DA" }
    }

    if ($externalUsers.Count -eq 0) { continue }

    # Get appointments for this tenant
    $tenantApptKeys = @($SeedState.appointmentIds.Keys | Where-Object { $_ -like "${tenantKey}_*" })

    $apptIdx = 0
    foreach ($apptKey in $tenantApptKeys) {
        $apptId = $SeedState.appointmentIds[$apptKey]
        $apptIdx++

        # First appointment: all 3 user types as accessors (coverage requirement)
        # Other appointments: random 1-2 accessors
        if ($apptIdx -eq 1) {
            $selectedUsers = $externalUsers
        } elseif ($apptIdx -eq ($tenantApptKeys.Count)) {
            # Last appointment: no accessors (empty list edge case)
            continue
        } else {
            $accessorUsersCount = Get-Random -Minimum 1 -Maximum ([Math]::Min(3, $externalUsers.Count + 1))
            $selectedUsers = @($externalUsers | Get-Random -Count $accessorUsersCount)
        }

        foreach ($user in $selectedUsers) {
            # Alternate between View(23) and Edit(24)
            $accessType = @(23, 24) | Get-Random

            $body = @{
                identityUserId = $user.UserId
                appointmentId  = $apptId
                accessTypeId   = $accessType
            }

            try {
                $result = Invoke-ApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/appointment-accessors" -Body $body -Token $tenantToken -TenantId $tenantId
                $SeedState.appointmentAccessorIds += $result.id
                $accessorCount++
                $typeName = if ($accessType -eq 23) { "View" } else { "Edit" }
                Write-Host "    [CREATE] $apptKey + $($user.Type): $typeName" -ForegroundColor DarkGray
            } catch {
                Write-Host "    [ERROR] Accessor for $apptKey : $_" -ForegroundColor Red
            }
        }
    }
}

Write-Host "  Accessors: $accessorCount records" -ForegroundColor White

# ---- A6.4 APPOINTMENT APPLICANT ATTORNEY LINKS (~8) ----

Write-Host "`n--- A6.4 Appointment-Attorney Links ---" -ForegroundColor Yellow

$aaLinkCount = 0

foreach ($tenantKey in @("T1", "T2", "T3")) {
    $tenantId = $SeedState.tenantIds[$tenantKey]
    $tenantEmail = $SeedState._tenantEmails[$tenantKey]
    $tenantToken = Get-AuthToken -Username $tenantEmail -Password $defaultPassword -AuthServerUrl $AuthServerUrl -TenantId $tenantId

    $aaUserKey = "${tenantKey}_AA1"
    $aaUserId = $SeedState.applicantAttorneyUserIds[$aaUserKey]
    $aaEntityId = $SeedState.applicantAttorneyEntityIds[$aaUserKey]

    if (-not $aaUserId -or -not $aaEntityId) {
        Write-Host "  [SKIP] No AA user/entity for $tenantKey" -ForegroundColor DarkGray
        continue
    }

    # Look up the IdentityUser details for the AA
    $aaUserDetail = Invoke-ApiCall -Method "GET" -Url "$ApiBaseUrl/api/identity/users/$aaUserId" -Token $tenantToken -TenantId $tenantId

    # Link to ~60% of this tenant's appointments
    $tenantApptKeys = @($SeedState.appointmentIds.Keys | Where-Object { $_ -like "${tenantKey}_*" })
    $targetLinks = [Math]::Ceiling($tenantApptKeys.Count * 0.6)
    $selectedApptKeys = @($tenantApptKeys | Get-Random -Count ([Math]::Min($targetLinks, $tenantApptKeys.Count)))

    foreach ($apptKey in $selectedApptKeys) {
        $apptId = $SeedState.appointmentIds[$apptKey]

        # Use the UpsertApplicantAttorneyForAppointmentAsync endpoint
        $body = @{
            applicantAttorneyId = $aaEntityId
            identityUserId      = $aaUserId
            firstName           = if ($aaUserDetail.name) { $aaUserDetail.name } else { "Attorney" }
            lastName            = if ($aaUserDetail.surname) { $aaUserDetail.surname } else { "User" }
            email               = $aaUserDetail.email
        }

        try {
            Invoke-ApiCall -Method "POST" -Url "$ApiBaseUrl/api/app/appointments/$apptId/applicant-attorney" -Body $body -Token $tenantToken -TenantId $tenantId
            $linkKey = "${apptKey}_AA"
            $SeedState.appointmentApplicantAttorneyIds[$linkKey] = $apptId
            $aaLinkCount++
            Write-Host "    [LINK] $apptKey ↔ AA" -ForegroundColor DarkGray
        } catch {
            Write-Host "    [ERROR] AA link for $apptKey : $_" -ForegroundColor Red
        }
    }
}

Write-Host "  AA Links: $aaLinkCount records" -ForegroundColor White

Write-Host "`nPhase A6 COMPLETE" -ForegroundColor Green
Write-Host "  Applicant Attorneys: $($SeedState.applicantAttorneyEntityIds.Count)"
Write-Host "  Employer Details: $empDetailCount"
Write-Host "  Accessors: $accessorCount"
Write-Host "  AA Links: $aaLinkCount"

return $SeedState
