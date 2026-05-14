[CmdletBinding()]
param()

# Phase 1 POC orchestrator. Mimics OLD's full execution flow per
# AppointmentDocumentDomain.cs:394-630:
#   1. Load OLD packet template DOCX (Patient Packet, Doctor Packet)
#   2. Replace ##Token## placeholders with synthetic data, ToUpper'd
#      to match OLD's reflection-based GetColumnValues behavior at
#      :1070, :1142, :1187. Multi-row groups (InjuryDetails,
#      InjuryBodyPartDetails, CustomFieldValues) get space-concatenated
#      values per OLD GetColumnValuesForInjury at :1133-1142.
#   3. Stamp the user's signature image at the
#      ##Appointments.Signature## placeholder (Patient Packet only --
#      the Doctor Packet has no signature token per Phase 0 audit).
#   4. Save the resulting DOCX (this is OLD's final artifact -- the
#      file OLD's email handler attaches and OLD stores in
#      AppointmentNewDocuments).
#   5. NEW STEP: convert that DOCX to PDF via Word's PDF exporter.
#
# Outputs in docs/parity/samples/poc/ for each packet:
#   poc-{packet}-old-flow.docx   <-- mirrors OLD's intermediate output
#   poc-{packet}-old-flow.pdf    <-- our new step on top of OLD's flow

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$convertScript = Join-Path $PSScriptRoot 'Convert-DocxToPdf.ps1'
$samplesDir = Join-Path $repoRoot 'docs\parity\samples\poc'

if (-not (Test-Path -LiteralPath $samplesDir)) {
    New-Item -ItemType Directory -Path $samplesDir -Force | Out-Null
}

$patientDocx = 'P:\PatientPortalOld\PatientAppointment.Api\wwwroot\Documents\documentBluePrint\patientpacketnew\PATIENT PACKET NEW.docx'
$doctorDocx  = 'P:\PatientPortalOld\PatientAppointment.Api\wwwroot\Documents\documentBluePrint\doctorpacket\DOCTOR PACKET.docx'

if (-not (Test-Path -LiteralPath $patientDocx)) { throw "Patient Packet DOCX not found: $patientDocx" }
if (-not (Test-Path -LiteralPath $doctorDocx))  { throw "Doctor Packet DOCX not found: $doctorDocx" }

# -- Synthetic signature PNG -----------------------------------------------
# Generates a small placeholder signature image with cursive-looking
# text "Sample Staff" on a transparent background. This stands in for
# OLD's per-user uploaded signature (User.SignatureAWSFilePath ->
# wwwroot\Documents\userSignature\{userId}). For the POC any image
# proves the InsertAPicture flow works end-to-end.

Add-Type -AssemblyName System.Drawing
$signaturePath = Join-Path $env:TEMP "poc-synthetic-signature.png"
$bmp = New-Object System.Drawing.Bitmap 240, 80
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
$g.Clear([System.Drawing.Color]::White)
$font = New-Object System.Drawing.Font("Segoe Script", 28, [System.Drawing.FontStyle]::Italic)
$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(20, 60, 130))
$g.DrawString("Sample Staff", $font, $brush, 5, 10)
$bmp.Save($signaturePath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose(); $font.Dispose(); $brush.Dispose()

Write-Host "Synthetic signature: $signaturePath" -ForegroundColor DarkGray

# -- Synthetic token map ---------------------------------------------------
# Per .claude/rules/test-data.md: obviously-fake placeholders only.
# Values are upper-case to mirror OLD .ToUpper() at :1070.

$dateNow = (Get-Date).ToString("MM/dd/yyyy").ToUpper()

$tokens = [ordered] @{
    # Patients (vPatient) -- single record per OLD :1080
    'Patients.FirstName'                        = 'JANE'
    'Patients.LastName'                         = 'SAMPLE'
    'Patients.MiddleName'                       = 'Q'
    'Patients.DateOfBirth'                      = '01/01/1990'
    'Patients.SocialSecurityNumber'             = 'SYNTHETIC-SSN'
    'Patients.Street'                           = '123 SAMPLE ST'
    'Patients.City'                             = 'LOS ANGELES'
    'Patients.State'                            = 'CALIFORNIA'
    'Patients.ZipCode'                          = '90001'

    # Appointments (vAppointmentDetail) -- single record
    'Appointments.RequestConfirmationNumber'    = 'APT-100001'
    'Appointments.AvailableDate'                = '06/15/2026'
    'Appointments.AppointmenTime'               = '9:00 AM'
    'Appointments.AppointmentType'              = 'AME'
    'Appointments.Location'                     = 'DEMO CLINIC'
    'Appointments.LocationAddress'              = '456 DEMO BLVD'
    'Appointments.LocationCity'                 = 'ENCINO'
    'Appointments.LocationState'                = 'CALIFORNIA'
    'Appointments.LocationZipCode'              = '91426'
    'Appointments.LocationParkingFee'           = '10'
    'Appointments.PrimaryResponsibleUserName'   = 'DR. SAMPLE STAFF'
    # Appointments.Signature handled by signature-image stamping below.

    # EmployerDetails -- FirstOrDefault per OLD :882
    'EmployerDetails.EmployerName'              = 'DEMO EMPLOYER LLC'
    'EmployerDetails.Street'                    = '789 EMPLOYER WAY'
    'EmployerDetails.City'                      = 'BURBANK'
    'EmployerDetails.State'                     = 'CALIFORNIA'
    'EmployerDetails.Zip'                       = '91505'

    # PatientAttorneys -- FirstOrDefault per OLD :886
    'PatientAttorneys.AttorneyName'             = 'ATTORNEY APPLICANT'
    'PatientAttorneys.Street'                   = '100 APPLICANT LN'
    'PatientAttorneys.City'                     = 'GLENDALE'
    'PatientAttorneys.State'                    = 'CALIFORNIA'
    'PatientAttorneys.Zip'                      = '91201'

    # DefenseAttorneys -- FirstOrDefault per OLD :890
    'DefenseAttorneys.AttorneyName'             = 'ATTORNEY DEFENSE'
    'DefenseAttorneys.Street'                   = '200 DEFENSE DR'
    'DefenseAttorneys.City'                     = 'PASADENA'
    'DefenseAttorneys.State'                    = 'CALIFORNIA'
    'DefenseAttorneys.Zip'                      = '91101'

    # InjuryDetails -- ALL rows space-concatenated per OLD :1133-1142.
    # Trailing space mirrors `recordValue += columnValue + " ";` so a
    # single-injury appointment renders the value with a trailing space.
    'InjuryDetails.ClaimNumber'                 = 'CLM-555555 '
    'InjuryDetails.DateOfInjury'                = '01/01/2026 '
    'InjuryDetails.WcabAdj'                     = 'ADJ-9999 '
    'InjuryDetails.WcabOfficeName'              = 'VAN NUYS WCAB '
    'InjuryDetails.WcabOfficeAddress'           = '6150 VAN NUYS BLVD '
    'InjuryDetails.WcabOfficeCity'              = 'VAN NUYS '
    'InjuryDetails.WcabOfficeState'             = 'CALIFORNIA '
    'InjuryDetails.WcabOfficeZipCode'           = '91401 '
    'InjuryDetails.PrimaryInsuranceName'        = 'DEMO INSURANCE CO '
    'InjuryDetails.PrimaryInsuranceStreet'      = '300 INSURANCE PKWY '
    'InjuryDetails.PrimaryInsuranceCity'        = 'LONG BEACH '
    'InjuryDetails.PrimaryInsuranceState'       = 'CALIFORNIA '
    'InjuryDetails.PrimaryInsuranceZip'         = '90802 '
    'InjuryDetails.ClaimExaminerName'           = 'EXAMINER SAMPLE '

    # Others -- computed per OLD :909
    'Others.DateNow'                            = $dateNow
}

# -- Jobs ------------------------------------------------------------------
# OLD-flow naming. Patient gets the signature; Doctor doesn't (per
# Phase 0 audit -- DOCTOR PACKET.docx has no Signature token).

$jobs = @(
    @{
        Name       = 'Patient Packet -- OLD flow (tokens + signature -> DOCX -> PDF)'
        Source     = $patientDocx
        OutputPdf  = (Join-Path $samplesDir 'poc-patient-packet-old-flow.pdf')
        OutputDocx = (Join-Path $samplesDir 'poc-patient-packet-old-flow.docx')
        Tokens     = $tokens
        Signature  = $signaturePath
    }
    @{
        Name       = 'Doctor Packet -- OLD flow (tokens only -> DOCX -> PDF)'
        Source     = $doctorDocx
        OutputPdf  = (Join-Path $samplesDir 'poc-doctor-packet-old-flow.pdf')
        OutputDocx = (Join-Path $samplesDir 'poc-doctor-packet-old-flow.docx')
        Tokens     = $tokens
        Signature  = $null
    }
)

$results = @()
foreach ($job in $jobs) {
    Write-Host ""
    Write-Host "==> $($job.Name)" -ForegroundColor Cyan

    $params = @{
        SourceDocx             = $job.Source
        OutputPdf              = $job.OutputPdf
        OutputIntermediateDocx = $job.OutputDocx
        TokenReplacements      = $job.Tokens
    }
    if ($job.Signature) { $params.SignatureImagePath = $job.Signature }

    $r = & $convertScript @params

    Write-Host ("    DOCX: {0} ({1:N0} bytes)" -f $r.OutputIntermediateDocx, $r.IntermediateDocxBytes) -ForegroundColor Green
    Write-Host ("    PDF:  {0} ({1:N0} bytes; {2} tokens replaced; signature: {3})" -f $r.OutputPdf, $r.OutputPdfBytes, $r.TokensReplaced, $r.SignaturePlaced) -ForegroundColor Green
    $results += $r
}

Write-Host ""
Write-Host "Done. Outputs in $samplesDir" -ForegroundColor Cyan
$results | Format-Table -AutoSize -Property OutputIntermediateDocx, IntermediateDocxBytes, OutputPdf, OutputPdfBytes, TokensReplaced, SignaturePlaced
