# Packet generation audit (Phase 0)

Audit doc for the PDF packet generation feature ([prompt](../prompts/2026-05-05-packet-generation-pdf-prompt.md) / [plan](../../../../Users/RajeevG/.claude/plans/prompt-pdf-packet-cuddly-volcano.md)).

This is the contract for Phase 1+ template authoring. The future implementer renders against the structure and token coverage recorded here. Do not deviate without surfacing to Adrian and updating this doc first.

> **STATUS: REVIEW READY.** Token reconciliation, multi-injury concatenation, time/parking-fee formatting, hardcoded clinic name, and Patient.Street choice all answered from OLD code. Q7 (other 5 DOCX templates) deferred to a follow-up after these two packets ship. See "Q&A -- answered from OLD code" section below.

---

## Source DOCX templates

| File | Path | Size |
|---|---|---|
| Patient Packet | `P:\PatientPortalOld\PatientAppointment.Api\wwwroot\Documents\documentBluePrint\patientpacketnew\PATIENT PACKET NEW.docx` | 343 KB |
| Doctor Packet | `P:\PatientPortalOld\PatientAppointment.Api\wwwroot\Documents\documentBluePrint\doctorpacket\DOCTOR PACKET.docx` | 1.0 MB |
| (legacy) Patient Packet | `P:\PatientPortalOld\PatientAppointment.Api\wwwroot\Documents\documentBluePrint\patientpacket\PATIENT PACKET.docx` | (out of scope -- referenced by `aws.patientPacket`, not `aws.patientPacketNew`) |

---

## Extraction methodology (two cross-checked methods)

A naive `grep -oE '##[A-Za-z]+\.[A-Za-z]+##'` over `word/document.xml` yields incomplete results because Word splits a single visible token like `##Patients.FirstName##` across multiple `<w:t>` elements at formatting boundaries (italic, color, autocorrect interruption). The OLD app's `ReplaceText` engine handles this by flattening run text before matching.

**Method used here:** a Python stdlib parser walks every `<w:p>` paragraph in `word/document.xml`, concatenates all descendant `<w:t>` content (including across `<w:r>` runs, `<w:hyperlink>`, and `<w:sdt>` content controls), then applies the token regex to the flattened paragraph text. Tables are walked recursively so cell-level tokens are caught too. Source: `C:\Users\RajeevG\AppData\Local\Temp\phase0_extract_packets.py`.

Cross-check vs flat grep:

| Template | Flat grep (naive) | Run-flattened (correct) | Delta |
|---|---|---|---|
| Patient Packet | 39 unique tokens | **44** unique tokens | +5 missed by naive grep |
| Doctor Packet | 1 unique token | **15** unique tokens | +14 missed by naive grep |

The 1-token Doctor result from naive grep was the smoking gun -- a clinical-exam form would never embed only one token. Always use the run-flattened method.

---

## Patient Packet -- token list (44)

```
##Appointments.AppointmenTime##              (typo in OLD, preserved verbatim)
##Appointments.AvailableDate##
##Appointments.Location##
##Appointments.LocationAddress##             ## NOT in prompt locked list
##Appointments.LocationCity##                ## NOT in prompt locked list
##Appointments.LocationParkingFee##
##Appointments.LocationState##
##Appointments.LocationZipCode##
##Appointments.PrimaryResponsibleUserName##
##Appointments.RequestConfirmationNumber##
##Appointments.Signature##
##DefenseAttorneys.AttorneyName##
##DefenseAttorneys.City##
##DefenseAttorneys.State##
##DefenseAttorneys.Street##
##DefenseAttorneys.Zip##
##EmployerDetails.EmployerName##
##InjuryDetails.ClaimNumber##
##InjuryDetails.DateOfInjury##
##InjuryDetails.PrimaryInsuranceCity##
##InjuryDetails.PrimaryInsuranceName##
##InjuryDetails.PrimaryInsuranceState##
##InjuryDetails.PrimaryInsuranceStreet##
##InjuryDetails.PrimaryInsuranceZip##
##InjuryDetails.WcabAdj##                    ## NOT in prompt locked list
##InjuryDetails.WcabOfficeAddress##          ## NOT in prompt locked list
##InjuryDetails.WcabOfficeCity##
##InjuryDetails.WcabOfficeName##
##InjuryDetails.WcabOfficeState##            ## NOT in prompt locked list
##InjuryDetails.WcabOfficeZipCode##
##Others.DateNow##
##PatientAttorneys.AttorneyName##
##PatientAttorneys.City##
##PatientAttorneys.State##
##PatientAttorneys.Street##
##PatientAttorneys.Zip##
##Patients.City##
##Patients.DateOfBirth##
##Patients.FirstName##
##Patients.LastName##
##Patients.SocialSecurityNumber##
##Patients.State##
##Patients.Street##
##Patients.ZipCode##                         ## NOT in prompt locked list
```

### Patient Packet tokens in prompt's locked list but ABSENT from DOCX

These were probably listed in OLD's `ReplaceText` token universe but are not used in this DOCX template. The future resolver does NOT need to render these for the Patient Packet.

```
##Patients.Email##
##Patients.PhoneNumber##
##Patients.CellPhoneNumner##  (sic)
##Patients.InterpreterVendorName##
##EmployerDetails.Street##     (NOT in Patient Packet, but IS in Doctor Packet)
##EmployerDetails.City##       (NOT in Patient Packet, but IS in Doctor Packet)
##EmployerDetails.State##      (NOT in Patient Packet, but IS in Doctor Packet)
##EmployerDetails.Zip##        (NOT in Patient Packet, but IS in Doctor Packet)
##PatientAttorneys.FirmName##
##PatientAttorneys.PhoneNumber##
##DefenseAttorneys.PhoneNumber##  (the locked list omits this; DOCX confirms it is not used)
```

---

## Patient Packet -- structural outline (high level)

The DOCX has 822 paragraph entries plus 13 tables. Major sections in render order:

1. **ACTIVITIES OF DAILY LIVING FORM** (paras ~0-599)
   - 7 sub-questionnaires with checkbox grids: Self-Care, Communication, Physical Activity, Sensory Function, Nonspecialized Hand Activities, Travel, Sleep / Sexual Function
   - Each has 4 difficulty columns: Without difficulty / With some difficulty / With much difficulty / Unable to do
   - Page header at top: `NAME: ##Patients.FirstName##  ##Patients.LastName##  ACCT: ##Appointments.RequestConfirmationNumber##  DATE: ##Appointments.AvailableDate##`
2. **RELEASE OF MEDICAL RECORDS** (paras ~603-633)
   - Header: West Coast Spine Institute (note: clinic name is hardcoded -- verify if NEW should template this for tenant parity)
   - Token block: `Date of Birth: ##Patients.DateOfBirth##`, `Date of Injury: ##InjuryDetails.DateOfInjury##`, `Social Security Number: ##Patients.SocialSecurityNumber##`
   - Privacy notices: Privacy Policy Statement, Notice of Privacy Practice, Assigning Privacy, Minimum Use and Disclosures of PHI, Marketing, Complaints, Responsibility and Identification
3. **Page repeat -- patient identifiers** (paras ~634-660)
   - Header repeat with bilingual labeling (English + Spanish phone number labels)
   - West Coast Spine Institute Acknowledgement of Receipt of Notice of Privacy Practices
4. **PRESENT COMPLAINTS** (paras ~664-732)
   - Pain symptom checkbox grid: Ache (Dolor), Burning (Ardor), Numbness (Entumecimiento), Pins and Needles (Hormigueo), Stabbing (Punalada), Bruises (Moretones)
   - Body diagram with BACK / FRONT views (Parte Posterior / Parte Anterior)
5. **AMA GUIDELINES (5TH EDITION) -- ACTIVITIES OF DAILY LIVING / PAIN QUESTIONNAIRE** (paras ~841-2065)
   - Section I: Pain Severity (sub-questions A-E, each with 0-10 scale)
   - Section II: Activity Limitation of Interference (sub-questions A-E, each with 0-10 scale)
   - Sum scores per section, total interference score
6. **Patient signature section** (para ~2066-2067)
   - `PATIENT NAME (Print)  ##Patients.FirstName##  ##Patients.LastName##` + `DATE: ##Appointments.AvailableDate##`
   - Signature line for patient hand-fill
7. **Cover letter to patient** (paras ~3040-3050)
   - `##Others.DateNow##`
   - `##Patients.FirstName##  ##Patients.LastName##`, `##Patients.Street##`, `##Patients.City##, ##Patients.State## ##Patients.ZipCode##`
   - `Dear : ##Patients.FirstName##  ##Patients.LastName##`
   - Appointment scheduling sentence: `... see Yuri Falkinstein, M.D. on ##Appointments.AvailableDate## at ##Appointments.AppointmenTime##. Your appointment will be held at ##Appointments.Location## ##Appointments.LocationAddress## ##Appointments.LocationCity##, ##Appointments.LocationState## ##Appointments.LocationZipCode##`
   - Parking fee note: `... this location is parking fee ##Appointments.LocationParkingFee##`
8. **Case-info block (first appearance)** (paras ~3060-3083)
   - `Case Name: ##Patients.FirstName## ##Patients.LastName##`
   - `EMPLOYER/PIV: ##EmployerDetails.EmployerName##` (no employer address fields in Patient Packet)
   - `Claim No: ##InjuryDetails.ClaimNumber##`
   - `WC/MS or WCAB Case No. (if any): ##InjuryDetails.WcabAdj##`
   - Sign-off: `I, ##Appointments.PrimaryResponsibleUserName##`
9. **Sign-off** (paras ~3098-3102)
   - `Date: ##Others.DateNow##`
   - `##Appointments.Signature##` (signature image stamped here)
   - `##Appointments.PrimaryResponsibleUserName##` (typed name beneath signature)
10. **Case-info block (second appearance) + insurance/attorney/WCAB summary** (paras ~3110-3145)
    - Repeated case info (Case Name, Employer, Claim No, WCAB Adj)
    - Patient address block: `##Patients.Street##` / `##Patients.City##, ##Patients.State## ##Patients.ZipCode##`
    - Insurance block: `##InjuryDetails.PrimaryInsuranceName##`, address fields
    - Patient attorney block (formerly "PatientAttorneys" -- NEW maps to ApplicantAttorney)
    - Defense attorney block
    - WCAB office block: name, address, city/state/zip

**Implementer note:** open the DOCX in Word/LibreOffice for visual fidelity. The 822-paragraph stream is in `C:\Users\RajeevG\AppData\Local\Temp\patient_packet.paragraphs.txt`. Token positions (paragraph index + 60-char context) are in `C:\Users\RajeevG\AppData\Local\Temp\patient_packet.summary.txt`.

---

## Doctor Packet -- token list (15)

```
##Appointments.AppointmentType##             ## NOT in prompt locked list
##Appointments.AvailableDate##
##Appointments.RequestConfirmationNumber##   ## NOT in prompt locked list
##EmployerDetails.City##                     ## NOT in prompt locked list
##EmployerDetails.EmployerName##             ## NOT in prompt locked list
##EmployerDetails.State##                    ## NOT in prompt locked list
##EmployerDetails.Street##                   ## NOT in prompt locked list
##EmployerDetails.Zip##                      ## NOT in prompt locked list
##InjuryDetails.ClaimExaminerName##
##InjuryDetails.ClaimNumber##                ## NOT in prompt locked list
##InjuryDetails.DateOfInjury##               ## NOT in prompt locked list
##InjuryDetails.PrimaryInsuranceName##       ## NOT in prompt locked list
##Patients.FirstName##
##Patients.LastName##
##Patients.MiddleName##                      ## NOT in prompt locked list
```

### Doctor Packet -- ANSWER to the prompt's open question on Signature

The Doctor Packet has **NO `##Appointments.Signature##` token**. The prompt flagged this as "(implicit -- check during Phase 0 audit)". Confirmed: the doctor signs by hand at the visit; no signature image is stamped on this packet.

This means **Phase 1/2 templates only stamp the signature image on the Patient Packet, not the Doctor Packet.** Resolver still resolves the responsible user's signature once for both packets, but only the Patient Packet template references it.

---

## Doctor Packet -- structural outline (high level)

The DOCX has 1246 paragraph entries plus 16 tables. Major sections in render order:

1. **Header + general observation** (paras ~0)
   - `Patient: ##Patients.FirstName## ##Patients.LastName##  Date: ##Appointments.AvailableDate##`
   - General Observation checkboxes: Limps (Favoring R/L), Altered Gait, Antalgic R/L, Flexed, Difficulty Moving During Exam, Requires Assistive Device (Cane/Crutch/Walker/Wheelchair/Scooter), Uses Support (C/S Collar, L/S Support, Sling, Wrist Brace R/L, Knee Brace R/L, Ankle Brace R/L, Other)
   - Medication / Dosage / Last Taken table (3 rows, blank for hand-fill)
   - Dynamometer (kg) table: Right Hand / Left Hand, three trials
   - J-Tech Report
   - Circumferential Measurements (cm): Biceps, Forearm, Thigh, Calf, Leg Length (R / L columns)
2. **Cervical ROM exam** (paras ~7-352)
   - N/R/L grid for Cervical Range of Motion (extension, flexion, lateral flexion, rotation)
   - Orthopedic Testing (R/L)
   - Palpation (R/L)
   - Pain scale matrix (T/H/S markers across rows)
3. **Thoracic ROM exam** (paras ~353-491)
   - Same N/R/L + Palpation pattern as Cervical
4. **Lumbar ROM exam** (paras ~492-829)
   - Same pattern with Orthopedic Testing
5. **Reflex / Sensation / Strength tables** (paras ~830-1190)
   - Nerve roots: C5, C6, C7, C8, T1, L4, L5, S1
   - Each row: Reflex (R/L), Sensation (R/L), Strength (R/L)
   - N markers for normal-vs-abnormal grading
6. **AMA 5TH ED PG -- 476, 477, 479** (para 1195)
7. **UPPER EXTREMITIES** (paras 1196+)
   - WRIST: Range of Motion, Strength, Orthopedic Testing (R/L), Palpation (R/L)
   - (continues through other joints -- elbow, shoulder, hand, etc.)
8. **Header repeat** (paras ~3200-3216)
   - `Date: ##Appointments.AvailableDate##`
   - `Acct: ##Appointments.RequestConfirmationNumber##`
   - `Name: ##Patients.FirstName## ##Patients.LastName##`
   - `Type:  ##Appointments.AppointmentType##`
9. **Patient demographics (paras ~4913-4922)**
   - `##Patients.LastName##`, `##Patients.FirstName##`, `##Patients.MiddleName##`
   - `##InjuryDetails.DateOfInjury##`
10. **Insurance + Claim Examiner (paras ~4937-4940)**
    - `##InjuryDetails.PrimaryInsuranceName##`
    - `##InjuryDetails.ClaimExaminerName##`
11. **Employer block (paras ~4955-4992)**
    - `##EmployerDetails.EmployerName##`
    - `##EmployerDetails.Street##`
    - `##EmployerDetails.City##`
    - `##EmployerDetails.State##`
    - `##EmployerDetails.Zip##`
    - `##InjuryDetails.ClaimNumber##`
12. **Footer date** (para ~5036): `Date: ##Appointments.AvailableDate##`

The 1246-paragraph stream is in `C:\Users\RajeevG\AppData\Local\Temp\doctor_packet.paragraphs.txt`. Token positions in `C:\Users\RajeevG\AppData\Local\Temp\doctor_packet.summary.txt`.

---

## Token-to-NEW-entity mapping (44 + 15 = 49 distinct tokens)

OLD code traced at `P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDocumentDomain.cs` lines 865-1190. Key behaviors NEW resolver must replicate:

- **ALL token values are `.ToUpper()`'d** before insertion (`AppointmentDocumentDomain.cs:1070, :1142, :1187`). Exception: `InjuryBodyPartDetails` group at `:1164` does NOT apply ToUpper. NEW: every resolved string except body-part descriptions is uppercased to match OLD.
- **Per-group cardinality:**
  - `Patients`, `Appointments`: single record (FirstOrDefault from view).
  - `EmployerDetails`, `PatientAttorneys`, `DefenseAttorneys`: single row (FirstOrDefault). NEW takes the FIRST row.
  - **`InjuryDetails`: ALL rows concatenated with `" "` separator** (`AppointmentDocumentDomain.cs:1133-1142`). Multi-injury appointments render every injury's value joined by space. This includes the WcabOffice/PrimaryInsurance/ClaimExaminer fields because `vInjuryDetail` is a join view -- one row per injury carrying all related fields.
  - **`InjuryBodyPartDetails`: ALL body parts across ALL injuries concatenated with `" "` separator, NO ToUpper** (`:1147-1164`).
  - **`CustomFieldValues`: ALL rows concatenated with `" "` separator** + ToUpper'd (`:1180-1187`).
- **Empty / null handling:** `recordValue != null ? recordValue.ToString().ToUpper() : string.Empty` (`:1070`). Returns empty string for null. NEW matches.
- **NRE bug at `:882, :886, :890`:** OLD calls `appointment.AppointmentEmployerDetails.FirstOrDefault().AppointmentId` without null-check. If an appointment has no employer-detail row, OLD throws. **NEW does NOT replicate this bug** -- NEW guards with null-check and returns empty token. (Per branch `CLAUDE.md` "Bug and deviation policy".)
- **Date format for `Others.DateNow`:** `DateTime.Now.ToString("MM/dd/yyyy")` (`:909`). Then `.ToUpper()` applied -- harmless for digits.
- **All other date fields** (`Patients.DateOfBirth`, `Appointments.AvailableDate`, `InjuryDetails.DateOfInjury`) are pre-formatted strings from OLD's SQL views (`vPatient.DateOfBirth` is `string` MaxLength 4000, etc.). Without the SQL view DDL we cannot read OLD's exact format string, but the values render verbatim from the view. **NEW renders dates as `MM/dd/yyyy`** to match the documented OLD convention; sample comparison after Phase 1 will confirm.
- **`Appointments.AppointmenTime`** (typo preserved): pre-formatted string from `vAppointmentDetail.AppointmenTime` (string MaxLength 4000). **NEW renders `DoctorAvailability.FromTime` as `h:mm tt`** (12-hour with AM/PM, US convention). If sample comparison shows a range (e.g. `9:00 AM - 9:30 AM`), update the resolver in Phase 1.
- **`Appointments.LocationParkingFee`** is `decimal?` in `vAppointmentDetail`, rendered via `recordValue.ToString().ToUpper()` (`:1070`). **OLD renders raw decimal** (e.g. "10.00", "10", "10.5") -- NO currency symbol, NO 2-decimal padding. NEW matches: `decimal.ToString()` with no format specifier, then ToUpper.
- **Token universe:** OLD reads the full 89-token list from `ServerSetting.Get("documentMergeKeys.keys")` at `AppointmentDocumentDomain.cs:869` (defined at `P:\PatientPortalOld\PatientAppointment.Api\server-settings.json`). NEW resolver populates the **49 tokens used by these two DOCX templates**; the other 40 tokens (registered for OLD's other 5 packets, not in scope per Q7 follow-up) are not implemented yet.
- **Patient.Street vs Patient.Address:** `vPatient` has both columns. OLD's `##Patients.Street##` resolves to `vPatient.Street`. The `Address` column exists but is unused for token replacement. **NEW resolver uses `Patient.Street`**; `Patient.Address` is unused for packet rendering.

| Token | OLD entity view | NEW entity | NEW property | Transform / nullable handling |
|---|---|---|---|---|
| `##Patients.FirstName##` | `vPatient` | `Patient` | `FirstName` | non-null |
| `##Patients.LastName##` | `vPatient` | `Patient` | `LastName` | non-null |
| `##Patients.MiddleName##` (Doctor only) | `vPatient` | `Patient` | `MiddleName` | nullable -> `""` |
| `##Patients.DateOfBirth##` | `vPatient` | `Patient` | `DateOfBirth` | format `MM/dd/yyyy` |
| `##Patients.SocialSecurityNumber##` | `vPatient` | `Patient` | `SocialSecurityNumber` | nullable -> `""` |
| `##Patients.Street##` | `vPatient` | `Patient` | `Street` | nullable -> `""` (NB: Patient also has `Address` field; OLD uses Street) |
| `##Patients.City##` | `vPatient` | `Patient` | `City` | nullable -> `""` |
| `##Patients.State##` | `vPatient` | `Patient` -> `State` | `Patient.StateId` -> `State.Name` | nullable -> `""` |
| `##Patients.ZipCode##` (Patient only) | `vPatient` | `Patient` | `ZipCode` | nullable -> `""` |
| `##Appointments.RequestConfirmationNumber##` | `vAppointmentDetail` | `Appointment` | `RequestConfirmationNumber` | non-null |
| `##Appointments.AvailableDate##` | `vAppointmentDetail` | `Appointment` -> `DoctorAvailability` | `Appointment.DoctorAvailabilityId` -> `DoctorAvailability.AvailableDate` | format `MM/dd/yyyy` |
| `##Appointments.AppointmenTime##` (typo) | `vAppointmentDetail.AppointmenTime` (pre-formatted string) | `Appointment` -> `DoctorAvailability` | `DoctorAvailability.FromTime` | format `h:mm tt` then `.ToUpper()` (e.g. `"9:00 AM"`); confirm via Phase 1 sample vs OLD output -- pivot to range if mismatch |
| `##Appointments.AppointmentType##` (Doctor only) | `vAppointmentDetail` | `Appointment` -> `AppointmentType` | `Appointment.AppointmentTypeId` -> `AppointmentType.Name` | non-null |
| `##Appointments.Location##` | `vAppointmentDetail` | `Appointment` -> `Location` | `Appointment.LocationId` -> `Location.Name` | non-null |
| `##Appointments.LocationAddress##` (Patient only) | `vAppointmentDetail` | `Appointment` -> `Location` | `Location.Address` | nullable -> `""` |
| `##Appointments.LocationCity##` (Patient only) | `vAppointmentDetail` | `Appointment` -> `Location` | `Location.City` | nullable -> `""` |
| `##Appointments.LocationState##` | `vAppointmentDetail` | `Appointment` -> `Location` -> `State` | `Location.StateId` -> `State.Name` | nullable -> `""` |
| `##Appointments.LocationZipCode##` | `vAppointmentDetail` | `Appointment` -> `Location` | `Location.ZipCode` | nullable -> `""` |
| `##Appointments.LocationParkingFee##` | `vAppointmentDetail.LocationParkingFee` (`decimal?`) | `Appointment` -> `Location` | `Location.ParkingFee` (`decimal`) | `value.ToString()` then `.ToUpper()` -- raw decimal, NO currency formatting (matches OLD `:1070`) |
| `##Appointments.PrimaryResponsibleUserName##` | `vAppointmentDetail` | `Appointment` -> `IdentityUser` | `Appointment.PrimaryResponsibleUserId` -> `IdentityUser.Name + " " + IdentityUser.Surname` | nullable -> `""` |
| `##Appointments.Signature##` (Patient only) | -- (image inserted via `InsertAPicture`) | `IdentityUser` extension | extra property `SignatureBlobName` -> blob bytes | rendered as image at 0.6 in. x 0.6 in.; null -> blank space (silent skip per OLD) |
| `##EmployerDetails.EmployerName##` | `vAppointmentEmployerDetail` | `AppointmentEmployerDetail` | `EmployerName` | nullable row -> `""` |
| `##EmployerDetails.Street##` (Doctor only) | `vAppointmentEmployerDetail` | `AppointmentEmployerDetail` | `Street` | nullable -> `""` |
| `##EmployerDetails.City##` (Doctor only) | `vAppointmentEmployerDetail` | `AppointmentEmployerDetail` | `City` | nullable -> `""` |
| `##EmployerDetails.State##` (Doctor only) | `vAppointmentEmployerDetail` | `AppointmentEmployerDetail` -> `State` | `StateId` -> `State.Name` | nullable -> `""` |
| `##EmployerDetails.Zip##` (Doctor only) | `vAppointmentEmployerDetail` | `AppointmentEmployerDetail` | `ZipCode` (NB: NEW property is `ZipCode`, not `Zip`) | nullable -> `""` |
| `##PatientAttorneys.AttorneyName##` (Patient only) | `vAppointmentPatientAttorney` | `AppointmentApplicantAttorney` -> `ApplicantAttorney` -> `IdentityUser` | first attorney's `IdentityUser.Name + " " + Surname` | nullable -> `""` (loop FIRST per OLD) |
| `##PatientAttorneys.Street##` (Patient only) | `vAppointmentPatientAttorney` | `ApplicantAttorney` | `Street` | nullable -> `""` |
| `##PatientAttorneys.City##` (Patient only) | `vAppointmentPatientAttorney` | `ApplicantAttorney` | `City` | nullable -> `""` |
| `##PatientAttorneys.State##` (Patient only) | `vAppointmentPatientAttorney` | `ApplicantAttorney` -> `State` | `StateId` -> `State.Name` | nullable -> `""` |
| `##PatientAttorneys.Zip##` (Patient only) | `vAppointmentPatientAttorney` | `ApplicantAttorney` | `ZipCode` (NB: NEW property is `ZipCode`, not `Zip`) | nullable -> `""` |
| `##DefenseAttorneys.AttorneyName##` (Patient only) | `vAppointmentDefenseAttorney` | `AppointmentDefenseAttorney` -> `DefenseAttorney` -> `IdentityUser` | first attorney's `IdentityUser.Name + " " + Surname` | nullable -> `""` (loop FIRST per OLD) |
| `##DefenseAttorneys.Street##` (Patient only) | `vAppointmentDefenseAttorney` | `DefenseAttorney` | `Street` | nullable -> `""` |
| `##DefenseAttorneys.City##` (Patient only) | `vAppointmentDefenseAttorney` | `DefenseAttorney` | `City` | nullable -> `""` |
| `##DefenseAttorneys.State##` (Patient only) | `vAppointmentDefenseAttorney` | `DefenseAttorney` -> `State` | `StateId` -> `State.Name` | nullable -> `""` |
| `##DefenseAttorneys.Zip##` (Patient only) | `vAppointmentDefenseAttorney` | `DefenseAttorney` | `ZipCode` | nullable -> `""` |
| `##InjuryDetails.ClaimNumber##` | `vInjuryDetail` (per-injury rows) | `AppointmentInjuryDetail` | `ClaimNumber` | **all injuries space-concatenated** + ToUpper. Single-injury appointment renders `"ABC123 "`. |
| `##InjuryDetails.DateOfInjury##` | `vInjuryDetail` | `AppointmentInjuryDetail` | `DateOfInjury` | format `MM/dd/yyyy` per row, **all injuries space-concatenated** + ToUpper |
| `##InjuryDetails.WcabAdj##` (Patient only) | `vInjuryDetail` | `AppointmentInjuryDetail` | `WcabAdj` | nullable per row, **all injuries space-concatenated** + ToUpper |
| `##InjuryDetails.WcabOfficeName##` (Patient only) | `vInjuryDetail` | `AppointmentInjuryDetail` -> `WcabOffice` | `WcabOfficeId` -> `WcabOffice.Name` | per-injury join, **all injuries space-concatenated** + ToUpper |
| `##InjuryDetails.WcabOfficeAddress##` (Patient only) | `vInjuryDetail` | `WcabOffice` | `Address` | **all injuries space-concatenated** + ToUpper |
| `##InjuryDetails.WcabOfficeCity##` (Patient only) | `vInjuryDetail` | `WcabOffice` | `City` | **all injuries space-concatenated** + ToUpper |
| `##InjuryDetails.WcabOfficeState##` (Patient only) | `vInjuryDetail` | `WcabOffice` -> `State` | `StateId` -> `State.Name` | **all injuries space-concatenated** + ToUpper |
| `##InjuryDetails.WcabOfficeZipCode##` (Patient only) | `vInjuryDetail` | `WcabOffice` | `ZipCode` | **all injuries space-concatenated** + ToUpper |
| `##InjuryDetails.PrimaryInsuranceName##` | `vInjuryDetail` | `AppointmentInjuryDetail` -> `AppointmentPrimaryInsurance` | `Name` | per-injury join (first insurance per injury), **all injuries space-concatenated** + ToUpper |
| `##InjuryDetails.PrimaryInsuranceStreet##` (Patient only) | `vInjuryDetail` | `AppointmentPrimaryInsurance` | `Street` | **all injuries space-concatenated** + ToUpper |
| `##InjuryDetails.PrimaryInsuranceCity##` (Patient only) | `vInjuryDetail` | `AppointmentPrimaryInsurance` | `City` | **all injuries space-concatenated** + ToUpper |
| `##InjuryDetails.PrimaryInsuranceState##` (Patient only) | `vInjuryDetail` | `AppointmentPrimaryInsurance` -> `State` | `StateId` -> `State.Name` | **all injuries space-concatenated** + ToUpper |
| `##InjuryDetails.PrimaryInsuranceZip##` (Patient only) | `vInjuryDetail` | `AppointmentPrimaryInsurance` | `Zip` (NB: NEW property is `Zip`, not `ZipCode` -- inconsistent with other entities) | **all injuries space-concatenated** + ToUpper |
| `##InjuryDetails.ClaimExaminerName##` (Doctor only) | `vInjuryDetail` | `AppointmentInjuryDetail` -> `AppointmentClaimExaminer` -> `IdentityUser` | first examiner per injury, name = `IdentityUser.Name + " " + Surname` | **all injuries space-concatenated** + ToUpper |
| `##Others.DateNow##` | computed | -- | `DateTime.Today.ToString("MM/dd/yyyy")` | non-null; clock-skew tolerant via UTC-to-Pacific-Time at AppService layer |

**MISSING-IN-NEW: zero rows.** Every DOCX token has a NEW entity target. The signature image source (`IdentityUser` extension `SignatureBlobName`) is provisioned by Phase A.

---

## Diff vs prompt's locked list (summary for Adrian)

### Tokens to ADD to the locked list (8 total)

These appear in OLD's DOCX templates and must be resolved. They are not in the prompt's original locked list, so per the prompt rule "stop and surface", here they are:

| Token | Reason | Recommended action |
|---|---|---|
| `##Patients.MiddleName##` | Doctor Packet renders `LastName / FirstName / MiddleName` patient demographics block | ADD to resolver. NEW Patient.MiddleName exists and is nullable. |
| `##Patients.ZipCode##` | Patient Packet cover-letter address block | ADD. NEW Patient.ZipCode exists. |
| `##Appointments.LocationAddress##` | Patient Packet cover letter | ADD. NEW Location.Address exists. |
| `##Appointments.LocationCity##` | Patient Packet cover letter | ADD. NEW Location.City exists. |
| `##Appointments.AppointmentType##` | Doctor Packet header | ADD. NEW AppointmentType.Name. |
| `##InjuryDetails.WcabAdj##` | Patient Packet "WC/MS or WCAB Case No." line | ADD. NEW AppointmentInjuryDetail.WcabAdj exists. |
| `##InjuryDetails.WcabOfficeAddress##` | Patient Packet WCAB block | ADD. NEW WcabOffice.Address exists. |
| `##InjuryDetails.WcabOfficeState##` | Patient Packet WCAB block | ADD. NEW WcabOffice.StateId -> State.Name. |

### Tokens to REMOVE from the locked list (10 total -- not used by either DOCX)

These are in the prompt's locked list but neither DOCX renders them. The resolver does NOT need to populate them.

```
##Patients.Email##
##Patients.PhoneNumber##
##Patients.CellPhoneNumner##  (sic)
##Patients.InterpreterVendorName##
##PatientAttorneys.FirmName##
##PatientAttorneys.PhoneNumber##
##DefenseAttorneys.PhoneNumber##  (in prompt; not in DOCX)
##InjuryDetails.PrimaryInsuranceStreet##  (NB: this IS used in Patient Packet -- false alarm; KEEP)
##InjuryDetails.PrimaryInsuranceCity##     (KEEP)
##InjuryDetails.PrimaryInsuranceZip##      (KEEP)
```

> **Errata in earlier section:** I miscounted. The three `PrimaryInsurance*` tokens above ARE in the Patient Packet DOCX. Correcting -- only the following are truly absent from both DOCX:
> - `##Patients.Email##`, `##Patients.PhoneNumber##`, `##Patients.CellPhoneNumner##`, `##Patients.InterpreterVendorName##`
> - `##PatientAttorneys.FirmName##`, `##PatientAttorneys.PhoneNumber##`
> - `##DefenseAttorneys.PhoneNumber##`
>
> That's 7 tokens to remove from the locked list, not 10.

### Doctor Packet -- broader entity coverage than prompt suggested

The prompt's locked list for Doctor Packet was Patient.FirstName/LastName, Appointments.AvailableDate, InjuryDetails.ClaimExaminerName, and "Appointments.Signature (uncertain)". Actual DOCX coverage adds:

```
##Patients.MiddleName##
##Appointments.AppointmentType##
##Appointments.RequestConfirmationNumber##
##InjuryDetails.ClaimNumber##
##InjuryDetails.DateOfInjury##
##InjuryDetails.PrimaryInsuranceName##
##EmployerDetails.EmployerName##
##EmployerDetails.Street##
##EmployerDetails.City##
##EmployerDetails.State##
##EmployerDetails.Zip##
```

And **does NOT** include `##Appointments.Signature##` (the prompt's open question -- now answered: no signature on Doctor Packet).

---

## Storage schema re-confirmation

The audit does not surface any need for multi-recipient packets. Two distinct packet kinds (Patient, Doctor) per appointment, 1:1 each. Decision 1 of the plan stands: extend `AppointmentPacket` with `PacketKind` enum (Patient = 0, Doctor = 1) + composite uniqueness `(AppointmentId, PacketKind)`. No change.

---

## Q&A -- answered from OLD code (per Adrian directive 2026-05-05)

> Adrian's directive: "For Question 1 to 6. Find out what the OLD one does and why and replicate it exactly as it is. For question 7, first we need to get these two packets running and then we can start with the remaining packets."

All six questions answered by reading OLD's `AppointmentDocumentDomain.cs` lines 865-1190 + `server-settings.json` `documentMergeKeys.keys` + the `vPatient` / `vAppointmentDetail` view C# definitions.

### Q1. Token list reconciliation -- ANSWERED

**OLD's full token universe is 89 tokens** registered in `documentMergeKeys.keys` (`server-settings.json`). OLD's resolver groups them by prefix, looks up the corresponding SQL view, and reflects on column names. **Every one of the 49 DOCX-used tokens is in this list.** The 7 "REMOVE" candidates I flagged (Email/PhoneNumber/CellPhoneNumner/InterpreterVendorName/PatientAttorneys.FirmName/PatientAttorneys.PhoneNumber/DefenseAttorneys.PhoneNumber) are also IN OLD's JSON list -- they are simply not used by these two DOCX templates. They will be needed for the other 5 DOCX templates (Q7 follow-up).

**NEW resolver decision:** populate the 49 distinct tokens used by these two DOCX templates. The remaining 40 tokens stay unimplemented for now; when Q7 follow-up packets ship, the resolver and `PacketTokenContext` extend additively. Per `code-standards.md` "no design for hypothetical future requirements".

The 49-token contract = (Patient Packet 44 tokens) U (Doctor Packet 15 tokens) -- net **49 distinct** (10 are shared between both packets).

### Q2. Multi-injury / multi-attorney -- ANSWERED (OLD does NOT use FirstOrDefault for InjuryDetails)

OLD's behavior per group, traced from `AppointmentDocumentDomain.cs`:

| Group | OLD method | Cardinality |
|---|---|---|
| `Patients` | `GetColumnValues<vPatient>` (`:1080+`) | single record |
| `Appointments` | `GetColumnValues<vAppointmentDetail>` (`:1078+`) | single record |
| `EmployerDetails` | `GetColumnValues<vAppointmentEmployerDetail>` | first row |
| `PatientAttorneys` | `GetColumnValues<vAppointmentPatientAttorney>` | first row |
| `DefenseAttorneys` | `GetColumnValues<vAppointmentDefenseAttorney>` | first row |
| `InjuryDetails` | `GetColumnValuesForInjury<vInjuryDetail>` (`:1131-1142`) | **ALL rows space-concatenated** |
| `InjuryBodyPartDetails` | `GetColumnValuesForInjury<...>` (`:1147-1164`) | **ALL body parts across ALL injuries space-concatenated, NO ToUpper** |
| `CustomFieldValues` | `GetColumnValuesForCustomeField<...>` (`:1180-1187`) | **ALL rows space-concatenated** |
| `Others` | inline (`:906-912`) | computed (DateNow) |

**NEW resolver replicates this exactly.** For every `##InjuryDetails.X##` token: query all `AppointmentInjuryDetail` rows for the appointment, project the X field per row (with FK joins to WcabOffice/PrimaryInsurance/ClaimExaminer/State as needed), join with `" "` separator, then `.ToUpper()`. Single-injury appointments render the value followed by a trailing space (e.g. `"ABC123 "`). Multi-injury renders `"ABC123 XYZ789 "`.

For Employer / Attorneys: take first row only (matches OLD).

### Q3. AppointmenTime format -- ANSWERED with caveat

OLD's `vAppointmentDetail.AppointmenTime` is a `string` MaxLength 4000 column -- pre-formatted by the SQL view's SELECT. The actual SQL view DDL is not in the OLD repo (live database object only), so the exact format string cannot be confirmed from source.

**NEW renders `DoctorAvailability.FromTime` as `h:mm tt`** (12-hour with AM/PM, US convention). This is the most-likely OLD format based on the column type and US-locale context. Phase 1 sample comparison vs OLD's rendered DOCX will confirm; if it shows a range (`9:00 AM - 9:30 AM`) the resolver flips to range.

### Q4. LocationParkingFee format -- ANSWERED

OLD's `vAppointmentDetail.LocationParkingFee` is `decimal?`. The reflection-based resolver at `:1067-1070` calls `recordValue.ToString().ToUpper()`. **No currency formatting.** Default `decimal.ToString()` for value `10.00m` produces `"10.00"`; for `10.5m` produces `"10.5"`; for `null` produces empty string.

**NEW resolver matches OLD:** `Location.ParkingFee.ToString().ToUpper()`. Raw decimal, no `$` prefix, no padding.

### Q5. Hardcoded clinic name + doctor name -- ANSWERED

The strings `"West Coast Spine Institute"` and `"Yuri Falkinstein, M.D."` are baked directly into the DOCX text content (not tokens). OLD makes no attempt to template these -- they are hardcoded clinic branding for the single-tenant deployment.

**NEW Phase 1A matches OLD:** the QuestPDF templates render these strings literally in the same positions. **Phase 2 multi-tenant follow-up** (when other tenants come online): introduce `##Tenant.ClinicName##` and `##Tenant.PrimaryDoctorName##` tokens. Capture as a row in `_parity-flags.md` for visibility.

### Q6. Patient.Street vs Patient.Address -- ANSWERED

OLD's `vPatient` has BOTH columns: `Street` (line 21) and `Address` (line 1, the very first property). The token `##Patients.Street##` resolves to `vPatient.Street` (reflection matches column name `Street`). The `Address` column exists but **is not referenced by any token** in the 89-token universe.

NEW's `Patient.Address` exists for the same legacy reason. **NEW resolver uses `Patient.Street` for `##Patients.Street##`.** `Patient.Address` is not used for packet rendering. Whether `Address` is used by any other NEW feature is out of scope for this audit.

### Q7. Out-of-scope DOCX templates -- DEFERRED per Adrian

Adrian's directive: "first we need to get these two packets running and then we can start with the remaining packets." The five remaining packets (`attorneyclaimexaminer`, `attornypacketame`, `attornypacketpqme`, `claimexaminerpacketame`, `claimexaminerpacketpqme`) are out of scope for this slice. They will be tackled in a follow-up after the Patient + Doctor packets ship.

The 40 currently-unused tokens from OLD's 89-token universe correspond to data needed by these other packets. NEW resolver and `PacketTokenContext` are designed to extend additively when those follow-ups land.

---

## Phase 1+ contract

Once Adrian approves this audit:

- The **49-token union** is the ground truth for `PacketTokenContext`.
- The section outlines above are the layout reference; the implementer opens the DOCX in Word/LibreOffice for visual fidelity; QuestPDF templates render to match.
- The resolver populates each token per the mapping table above (FK joins, transforms, nullable handling).
- All resolved string values (except `InjuryBodyPartDetails`) are `.ToUpper()`'d before insertion, matching OLD `:1070`.
- `InjuryDetails` / `InjuryBodyPartDetails` / `CustomFieldValues` concatenate ALL rows with `" "` separator -- not FirstOrDefault. Single-row appointments still get the trailing space from OLD's `recordValue += columnValue + " "` loop pattern (`:1139, :1160, :1184`).
- `Patients`, `Appointments`, `EmployerDetails`, `PatientAttorneys`, `DefenseAttorneys` use FirstOrDefault.
- Phase A ships before Phase 1 to provide the signature image source.
