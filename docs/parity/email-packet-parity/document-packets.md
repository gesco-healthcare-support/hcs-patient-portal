# Document Packet Generation - OLD vs NEW Parity Audit

**Date:** 2026-05-08 (Pass 1 + Pass 2 corrections appended)
**Scope:** Portal-generated DOCX packets emailed as attachments to stakeholders after appointment approval. **Out of scope:** patient-uploaded supplementary documents (separate flow, separate audit).
**Status:** Research / contract for next-session implementation. No code written.
**Companion doc:** `docs/parity/email-packet-parity/email-service.md` (email service + handlers).

> **READ FIRST:** This doc was produced in 2 passes. Pass 1 (sections 0-11) was rushed and contains material errors. Pass 2 (section 12+, "Pass 2 corrections") is the corrected ground truth - read it first if you find conflicts.

---

## 0. Adrian's directive (2026-05-08)

> "There are two sets of documents, one are these packets that the application will generate and email to the stakeholders based on the data filled in the appointment form, and other documents are uploaded documents that the external users (Generally the patient) will upload as supplementary documents for the appointment. In this task we are looking for the packets that the portal will generate not the ones the external users get to upload, that will be a separate task."

> "We want to replicate that entire service/feature/module and then change the packet generation to PDF. But first we want to ensure we create the DOCX versions at least, we can then find a way to convert them to PDFs."

> "These are legal documents and have to formatted the way they are."

**The contract:** rebuild OLD's per-stakeholder DOCX-template-driven generator first (verbatim format, verbatim tokens, verbatim per-recipient routing), THEN add a DOCX -> PDF conversion step before email or download.

---

## 1. Executive summary

| Item | OLD | NEW today | Gap |
|---|---|---|---|
| **Generation strategy** | 3 pre-authored DOCX templates loaded from disk; 89-token mail-merge via `ReplaceText`; signature image stamped via `InsertAPicture`; emailed as attachments via `SendSMTPMailWithAttachment` | Single 1-page MigraDoc cover-sheet PDF + uploaded patient docs concatenated via PdfSharp `PdfMerge` | **Fundamentally different model.** NEW has zero templates wired. |
| **Output format** | DOCX (sent as attachment + saved to local disk + AppointmentNewDocument row) | PDF (saved to blob + downloaded via REST endpoint) | NEW is PDF-only. The DOCX-first directive needs a new generator. |
| **Templates** | 8 DOCX files on disk under `wwwroot/Documents/documentBluePrint/`; current code reads only 3 (PatientPacketNew, DoctorPacket, AttorneyClaimExaminer); 5 are configured but unreferenced (legacy / refactor leftover) | `PatientPacketTemplate.cs` (QuestPDF) exists as **dead code** - never instantiated. No DoctorPacketTemplate, no AttorneyClaimExaminerPacketTemplate. | NEW has the structural skeleton for ONE template; needs the other two + actual instantiation + token resolver. |
| **Tokens** | 89-token universe in `documentMergeKeys.keys` (`server-settings.json`); 49 distinct tokens actually used by the 2 active DOCX templates (44 Patient + 15 Doctor, with 10 shared) | `PacketTokenContext` defines 49 properties matching OLD's 49-token union | **Schema ready, resolver missing.** No code populates `PacketTokenContext` from a NEW Appointment. |
| **Token replacement** | `ReplaceText`: 8 group prefixes, reflection-based property lookup against per-group SQL views (`vPatient`, `vAppointmentDetail`, etc.); `.ToUpper()` applied (except `InjuryBodyPartDetails`); all-injuries space-concatenated for InjuryDetails / InjuryBodyPartDetails / CustomFieldValues | None | **Resolver code missing.** The dispatch from PacketTokenContext properties to template rendering is in `PatientPacketTemplate.cs` but the population logic doesn't exist. |
| **Signature image** | Per-user PNG/JPEG stamped at `##Appointments.Signature##` placeholder; size 880000 x 880000 EMU = 0.962 inches square | `PacketTokenContext.ResponsibleUserSignature byte[]?`; `PatientPacketTemplate` renders the image when non-null else blank space (silent skip matches OLD) | **Wired in template but no code populates the bytes.** Need an `IUserSignatureAppService` or similar. |
| **Storage** | `wwwroot/Documents/submittedDocuments/{ConfirmationNumber}_{PacketName}_{ddMMyyyy_hhmmss}.docx` on local disk + `AppointmentNewDocument` DB row | `IBlobContainer<AppointmentPacketsContainer>` blob `{tenantSegment}/{appointmentId}/packet/{guid}.pdf` + `AppointmentPacket` DB row (1:1 per appointment) | Storage shape OK but `AppointmentPacket` is 1:1; OLD effectively writes 1 row per recipient packet (Patient / Doctor / Adjuster / Attorneys). Need PacketKind discriminator. |
| **Delivery** | Email with DOCX attachment via `SendSMTPMailWithAttachment` to: Patient, Adjuster (PQME/AME), Defense Attorneys, Patient Attorneys. Doctor packet generated but NOT emailed (saved to DB only). | REST download via `AppointmentPacketsAppService.DownloadAsync(appointmentId)` returning `application/pdf`. **No email-with-attachment flow exists.** | **Major gap.** OLD's per-recipient email-with-attachment is unimplemented. |
| **Trigger** | `AppointmentDomain.Update:564` after status change to Approved AND `internalUserUpdateStatus`. Or `AppointmentDomain.Add:282` if internal user creates the appointment. | `PacketGenerationOnApprovedHandler` subscribes to `AppointmentStatusChangedEto` (filter ToStatus=Approved) and enqueues `GenerateAppointmentPacketJob`. | Trigger parity OK; downstream output is the gap. |
| **Per-injury cardinality** | InjuryDetails / InjuryBodyPartDetails / CustomFieldValues: ALL rows space-concatenated (`recordValue += value + " "`); single-injury yields trailing-space; ToUpper applied | Documented in `PacketTokenContext` doc-comments but no resolver code yet | Resolver must replicate exactly. |
| **OLD bug: NRE on missing employer/attorney** | `ReplaceText:882, :886, :890` calls `appointment.AppointmentEmployerDetails.FirstOrDefault().AppointmentId` without null-check - throws if no employer/attorney row exists | NEW resolver is unimplemented | **Don't replicate.** Fix silently per CLAUDE.md "Clear bug" rule. |

**Bottom line.** NEW's `AppointmentPacket` aggregate, the Hangfire job, the cover-page-merge approach, and the `PatientPacketTemplate` QuestPDF skeleton are scaffolding for a model **the user has explicitly rejected**. The DOCX-first directive means the next session must:
1. Replace the cover-page-+-uploads PDF generator with a DOCX generator that loads OLD templates verbatim and does mail-merge + signature stamping.
2. Implement the 3 packet variants (Patient / Doctor / AttorneyClaimExaminer), one DOCX each.
3. Wire per-recipient email-with-attachment delivery via a new `SendAppointmentPacketEmailJob`.
4. Defer the DOCX -> PDF conversion until step 1-3 produce DOCX output that matches OLD byte-for-byte (or at least visual fidelity).

---

## 1A. Out-of-scope clarification

This audit covers ONLY portal-generated packets. Out of scope (separate audit task per Adrian):
- Patient/external-user uploaded supplementary documents (`AppointmentDocument` rows with `IsAdHoc=false` queued by `PackageDocumentQueueHandler`)
- Ad-hoc documents (`AppointmentDocument` with `IsAdHoc=true`)
- Joint Declaration Forms uploaded by AME parties (`AppointmentDocument.IsJointDeclaration=true`)

These flows ARE referenced where they overlap (e.g. NEW currently repurposes the `AppointmentDocument` table for both - the upload flow ALREADY exists as the documents the merge service includes).

---

## 2. OLD packet generation - line-by-line

### 2.1 Entry points

`AppointmentDocumentDomain.AddAppointmentDocumentsAndSendDocumentToEmail(Appointment appointment, int appointmentId = 0)` at `:394-863` is the orchestrator.

**Two callers:**
- `AppointmentDomain.Add:282` - when an internal user creates an appointment (typically at status=Approved). Calls with `appointmentId` set, `appointment` is a fresh empty record.
- `AppointmentDomain.Update:564` - when status transitions to Approved AND `IsInternalUserUpdateStatus`. Calls with `appointment` populated, `appointmentId=0`.

When `appointmentId > 0` (the Add path), the method re-loads the appointment with all 6 navigation properties and the 3 nested injury collections (`AppointmentPrimaryInsurance`, `AppointmentClaimExaminer`, `AppointmentInjuryBodyPartDetail`). Otherwise the caller is expected to have eager-loaded everything.

### 2.2 Pre-flight (lines `:433-462`)

```
directoryName = appointmentId + "_" + ddMMyyyy_hhmmss + "_" + RequestConfirmationNumber
folderName    = FileOperation.CreateDirectory(directoryName)   // creates wwwroot/.../tempBatch/{directoryName}
domainName    = ServerSetting.Get<string>("applicationUrl.clientUrl")
documentUploadUrl = domainName + "/appointment-new-documents/" + appointmentId
emailBody         = ApplicationUtility.GetEmailTemplateFromHTML(EmailTemplate.AppointmentDocumentAddWithAttachment, vemailSenderViewModel, documentUploadUrl)
```

The email body is loaded ONCE and reused for every per-recipient send - all 6 packet emails carry the same body but different attachments + different `email` recipient.

`patientDetailsEmailSubject = "(" + patientName + injuryDetails + ")"` (`:451-456`) - same bracketed prefix as the lifecycle emails.

`createdByEmail` (`:459`) is loaded but never used in the code I read.

### 2.3 Patient Packet (`:463-550`)

```
patientPacketUrl = ServerSetting.Get<string>("aws.patientPacketNew")    // value: "patientpacketnew"
if (appointment.Patient.Email != null) {
    email = appointment.Patient.Email
    userType = (int)UserTypesForEmail.Patient
    patientAttachment = FileOperation.DownloadPackageDocument(patientPacketUrl, folderName)
        // Effect: copies wwwroot/Documents/documentBluePrint/patientpacketnew/PATIENT PACKET NEW.docx into the temp dir
        // Returns List<FileDownloadViewModel>; in current code, always 1 file
    foreach (item in patientAttachment) {
        signaturePath = ServerSetting.Get<string>("aws.userSignature")
        signatureFileName = User.SignatureAWSFilePath of appointment.PrimaryResponsibleUserId
        if (signatureFileName != null) {
            userSignature = AmazonBlobStorage.DownloadFile(fileUrl, PrimaryResponsibleUserId)
                // Despite the name, this method falls back to local file read at wwwroot/Documents/userSignature/{UserId}
            InsertAPicture(item.FilePath, "Signature", userSignature)
                // Opens the temp DOCX, finds text "##Appointments.Signature##", inserts an inline image of size 880000x880000 EMU (~0.96 in. square), removes the placeholder text
        }
        ReplaceText(appointment, item.FilePath)
            // Token mail-merge via ApplicationUtility.ReplaceTextOfWordDocument
        attachments.Add(item)
    }

    // Surbhi 29/04/2025 -- email send
    SendMail.SendSMTPMailWithAttachment(
        email, "Appointment Request Approved " + patientDetailsEmailSubject, emailBody,
        "", "", patientAttachment)

    // Save to local Documents/submittedDocuments/ + register AppointmentNewDocument row
    Byte[] patientDocsBytes = File.ReadAllBytes(patientAttachment[0].FilePath)
    String patientFile = Convert.ToBase64String(patientDocsBytes)
    FileDetailModel patientFileDetail = {
        FileName = RequestConfirmationNumber + "_Patient Packet_" + ddMMyyyy_hhmmss + ".docx",
        FileData = patientFile
    }
    documentsFolder = wwwroot/Documents/submittedDocuments
    if (!Directory.Exists(documentsFolder)) Directory.CreateDirectory(documentsFolder)
    filePath = documentsFolder + "/" + patientFileDetail.FileName
    File.WriteAllBytes(filePath, fileBytes1)

    AppointmentRequestUow.RegisterNew<AppointmentNewDocument>(new AppointmentNewDocument {
        AppointmentId = appointment.AppointmentId,
        ResponsibleUserId = (int)appointment.PrimaryResponsibleUserId,
        DocumentStatusId = (int)DocumentStatuses.Accepted,
        DocumentFilePath = patientFileDetail.FileName,
        DocumentName = "PATIENT PACKET",
        AttachmentLink = documentUploadUrl,
        UserType = (int)UserTypesForEmail.CreatedBy,
        DocumentAwsFilePath = "Documents/submittedDocuments/" + patientFileDetail.FileName,
        ...
    })
    AppointmentRequestUow.Commit()
}
```

**Observations:**
- AWS S3 code is commented out (`//string mainBucketPatientPacket = ...`) - all storage is local disk despite "AWSFilePath" property names.
- The `patientAttachment` foreach loop is a 1-iteration loop because `DownloadPackageDocument` always returns exactly 1 file.
- Patient gets email with the rendered DOCX attached + the same row stored in DB.
- **If `appointment.Patient.Email == null`, this entire region is skipped silently.** No fallback to `createdByEmail`.

### 2.4 Doctor Packet (`:561-634`)

```
doctorPacketUrl = ServerSetting.Get<string>("aws.doctorPacket")    // value: "doctorpacket"
doctorAttachment = FileOperation.DownloadPackageDocument(doctorPacketUrl, folderName)
foreach (item in doctorAttachment) {
    // signature stamp + token replace -- same as Patient path
    InsertAPicture(item.FilePath, "Signature", userSignature)   // when SignatureAWSFilePath != null
    ReplaceText(appointment, item.FilePath)
    attachments.Add(item)
}

// NO EMAIL SEND for Doctor Packet -- only the storage + DB write below.

Byte[] bytes = File.ReadAllBytes(doctorAttachment[0].FilePath)
String file = Convert.ToBase64String(bytes)
FileDetailModel fileDetail = {
    FileName = RequestConfirmationNumber + "_Doctor Packet_" + ddMMyyyy_hhmmss + ".docx",
    FileData = file
}
File.WriteAllBytes(documentsFolder + "/" + fileDetail.FileName, fileBytes)

appointmentNewDocument = new AppointmentNewDocument {
    DocumentName = "DOCTOR PACKET",
    DocumentFilePath = fileDetail.FileName,
    DocumentStatusId = (int)DocumentStatuses.Accepted,
    DocumentAwsFilePath = "Documents/submittedDocuments/" + fileDetail.FileName,
    ...
}
AppointmentRequestUow.RegisterNew<AppointmentNewDocument>(appointmentNewDocument)
AppointmentRequestUow.Commit()
```

**Doctor packet is generated and stored, but NOT emailed.** The doctor signs in the office at the visit; the office staff prints the rendered DOCX from the portal. NEW must replicate this asymmetry.

### 2.5 Adjuster / Claim Examiner Packet (`:636-737`)

Branches by `appointment.AppointmentTypeId`:

**PQME / PQMEREEVAL (`:643-688`):**
```
adjusterUrl = ServerSetting.Get<string>("aws.attorneyClaimExaminer")   // value: "attorneyclaimexaminer"
foreach injury in appointmentInjuryDetails {
    foreach examiner in injury.AppointmentClaimExaminers {
        if (examiner.IsActive) {
            adjusterAttachment = FileOperation.DownloadPackageDocument(adjusterUrl, folderName)
            // signature + token replace per item
        }
    }
}
foreach injury in appointmentInjuryDetails {
    foreach examiner in injury.AppointmentClaimExaminers {
        if (examiner.IsActive && examiner.Email != null) {
            email = examiner.Email
            userType = (int)UserTypesForEmail.ClaimExaminer
            SendMail.SendSMTPMailWithAttachment(
                email, "Appointment Request Approved " + patientDetailsEmailSubject,
                emailBody, "", "", adjusterAttachment)
        }
    }
}
```

**AME / AMEREEVAL (`:689-736`):** identical structure but reads from the SAME `aws.attorneyClaimExaminer` URL. Variable names suggest it should be different (`adjusterUrl` is reused), but the actual pulled file is the same `attorneyclaimexaminer/ATTORNEY CLAIM EXAMINER PACKET.docx`.

**Two-pass pattern:** the first pass downloads + stamps + replaces ONCE per examiner row (which is wasteful - the same file gets re-downloaded into the same folder; the second download overwrites the first); the second pass emails. With multiple active examiners, only the LAST iteration's stamped file is what every recipient gets.

**No DB row, no local-disk save** for the adjuster packet - only emailed.

### 2.6 Attorney Packet (`:739-859`)

Same structure, but TWO recipient lists per appointment type:

**PQME / PQMEREEVAL (`:740-799`):**
```
attorneyPacketPQME = ServerSetting.Get<string>("aws.attorneyClaimExaminer")    // SAME URL
isActive = checkForRecordsAdded(appointment)
if (isActive) {
    attorneyAttachmentPQME = FileOperation.DownloadPackageDocument(attorneyPacketPQME, folderName)
    foreach (item) { signature + ReplaceText + attachments.Add }
}
foreach defenseAttorney in appointment.AppointmentDefenseAttorneys {
    if (defenseAttorney.AttorneyEmail != null) {
        email = defenseAttorney.AttorneyEmail
        userType = (int)UserTypesForEmail.DefenceAttorneyPQME
        SendMail.SendSMTPMailWithAttachment(email, "Appointment Request Approved " + ..., emailBody, "", "", attorneyAttachmentPQME)
    }
}
foreach patientAttorney in appointment.AppointmentPatientAttorneys {
    if (patientAttorney.AttorneyEmail != null) {
        email = patientAttorney.AttorneyEmail
        userType = (int)UserTypesForEmail.PatientAttorneyPQME
        SendMail.SendSMTPMailWithAttachment(email, ..., emailBody, "", "", attorneyAttachmentPQME)
    }
}
```

**AME / AMEREEVAL (`:801-859`):** identical structure, identical URL `aws.attorneyClaimExaminer`.

**No DB row, no local-disk save** for the attorney packet.

**Important nuance.** The variable names (`attorneyPacketPQME`, `attorneyAttachmentAME`) imply the templates differ between PQME and AME. The actual code reads the SAME server-setting `aws.attorneyClaimExaminer` for both. This contradicts the existence of separate folders on disk: `attornypacketame/Attorney Packet For AME.docx`, `attornypacketpqme/Attorney Packet For QME.docx`, `claimexaminerpacketame/Attorney Packet For AME.docx`, `claimexaminerpacketpqme/Attorney Packet For QME.docx`. The disk files DO exist, the server-settings keys exist (`aws.attorneyPacketPQME`, `aws.attorneyPacketAME`, `aws.claimExaminerPacketAME`, `aws.claimExaminerPacketPQME`), but the C# code never reads those keys. They're configured-but-unreferenced. Likely a partially-complete refactor.

**Open question for Adrian:** Should NEW use the per-PQME/AME templates (the variable-name intent) OR the unified `attorneyclaimexaminer` template (the actual current code path)? OLD currently sends the same file to all 4 attorney+claim-examiner recipients regardless of PQME/AME.

### 2.7 Final commit (`:860-862`)

```
var data = appointment;
AppointmentRequestUow.Commit();
```

### 2.8 Token replacement (`:865-952`)

```
public void ReplaceText(Appointment appointment, string filePath)
{
    var data = ServerSetting.Get<object>("documentMergeKeys.keys")    // 89-token list from server-settings.json
    List<string> replaceStrings = JsonConvert.DeserializeObject<List<string>>(data.ToString())

    // Group by prefix and dispatch to the right SQL view
    var patientKeys                  = replaceStrings.Where(x => x.StartsWith("##Patients"))
    var patientKeyValuePairs         = GetColumnValues<vPatient>(appointment.Patient.PatientId, patientKeys)

    var appointmentsDetails          = replaceStrings.Where(x => x.StartsWith("##Appointments"))
    var appointmentsKeyValuePairs    = GetColumnValues<vAppointmentDetail>(appointment.AppointmentId, appointmentsDetails)

    var appointmentEmployerDetails   = replaceStrings.Where(x => x.StartsWith("##EmployerDetails"))
    var appointmentEmployerKeyValuePairs = GetColumnValues<vAppointmentEmployerDetail>(
        appointment.AppointmentEmployerDetails.FirstOrDefault().AppointmentId,    // <-- NRE if no employer row
        appointmentEmployerDetails)

    var appointmentPatientAttorneys  = replaceStrings.Where(x => x.StartsWith("##PatientAttorneys"))
    var appointmentPatientAttorneyskeyValuePairs = GetColumnValues<vAppointmentPatientAttorney>(
        appointment.AppointmentPatientAttorneys.FirstOrDefault().AppointmentId,    // <-- NRE
        appointmentPatientAttorneys)

    var appointmentDefenseAttorneys  = replaceStrings.Where(x => x.StartsWith("##DefenseAttorneys"))
    var appointmentDefenseAttorneyskeyValuePairs = GetColumnValues<vAppointmentDefenseAttorney>(
        appointment.AppointmentDefenseAttorneys.FirstOrDefault().AppointmentId,    // <-- NRE
        appointmentDefenseAttorneys)

    var appointmentCustomFields      = replaceStrings.Where(x => x.StartsWith("##CustomFieldValues"))
    var appointmentCustomFieldkeyValuePairs = GetColumnValuesForCustomeField<vAppointmentCustomFieldValue>(
        appointment.AppointmentId, appointmentCustomFields)
        // ALL rows space-concatenated + ToUpper (line :1187)

    var injuryDetail                 = replaceStrings.Where(x => x.StartsWith("##InjuryDetails"))
    var injuryDetailkeyValuePairs    = GetColumnValuesForInjury<vInjuryDetail>(appointment.AppointmentId, injuryDetail)
        // ALL injuries space-concatenated + ToUpper (line :1142)

    var injuryBodyPartDetails        = replaceStrings.Where(x => x.StartsWith("##InjuryBodyPartDetails"))
    var injuryBodyPartDetailskeyValuePairs = GetColumnValuesForInjury<AppointmentInjuryBodyPartDetail>(
        appointment.AppointmentId, injuryBodyPartDetails)
        // ALL body parts across ALL injuries space-concatenated, NO ToUpper (line :1164)

    var othersDetails                = replaceStrings.Where(x => x.StartsWith("##Others"))
    foreach (var key in othersDetails) {
        recordValue = DateTime.Now.ToString("MM/dd/yyyy")    // only DateNow
        keyValuePairs.Add(key, recordValue.ToUpper())
    }

    // Merge all dictionaries into one keyValuePairs map
    foreach (var item in patientKeyValuePairs) keyValuePairs.Add(item.Key, item.Value)
    // ... etc for each group

    ApplicationUtility.ReplaceTextOfWordDocument(keyValuePairs, filePath)
        // Actual OpenXml find/replace into the .docx
}
```

**`GetColumnValues<T>` (`:1053-1073`):**
```
foreach (key in keys) {
    columnName = key.Replace("##", "").Substring(key.Replace("##", "").IndexOf('.') + 1)  // strip the prefix and "."
    recordValue = record.GetType().GetProperties()
                       .Where(a => a.Name == columnName)
                       .Select(p => p.GetValue(record, null))
                       .FirstOrDefault()
    columnReplaceValues.Add(key, recordValue != null ? recordValue.ToString().ToUpper() : string.Empty)
}
```

Reflection-based property lookup. **All non-null values are `.ToUpper()`'d (`:1070`).** Null values yield empty string.

**`GetColumnValuesForInjury<T>` (`:1125-1170`):**
```
if (typeof(T) == typeof(vInjuryDetail)) {
    var injurDetail = AppointmentRequestUow.Repository<vInjuryDetail>().FindBy(...).ToList()
    dynamic recordValue = null
    foreach (item in injurDetail) {
        var columnValue = ... reflection lookup ...
        recordValue += columnValue + " "    // CONCATENATE
    }
    columnReplaceValues.Add(key, recordValue.ToString().ToUpper())    // ToUpper applied
}
else if (typeof(T) == typeof(AppointmentInjuryBodyPartDetail)) {
    var injurDetail = AppointmentRequestUow.Repository<AppointmentInjuryDetail>().FindByInclude(... .AppointmentInjuryBodyPartDetails ...)
    dynamic recordValue = null
    foreach (item in injurDetail) {
        foreach (bodyPart in item.AppointmentInjuryBodyPartDetails) {
            var columnValue = ... reflection lookup ...
            recordValue += columnValue + " "
        }
    }
    columnReplaceValues.Add(key, recordValue.ToString())    // NO ToUpper for body parts
}
```

**Cardinality summary:**
- Patients / Appointments / EmployerDetails / PatientAttorneys / DefenseAttorneys: single record (FirstOrDefault)
- InjuryDetails: ALL rows space-concatenated + ToUpper (single-injury produces value with trailing space)
- InjuryBodyPartDetails: ALL body parts across ALL injuries space-concatenated, NO ToUpper
- CustomFieldValues: ALL rows space-concatenated + ToUpper
- Others: computed inline (DateNow only)

### 2.9 Signature image stamping (`:954-1052`)

```
public static void InsertAPicture(string document, string fileName, byte[] fileData)
{
    using (var wordprocessingDocument = WordprocessingDocument.Open(document, true))
    {
        MainDocumentPart mainPart = wordprocessingDocument.MainDocumentPart
        ImagePart imagePart = mainPart.AddImagePart(ImagePartType.Png)
        imagePart.FeedData(new MemoryStream(fileData))
        AddImageToRun(wordprocessingDocument, fileName, 880000L, 880000L, mainPart.GetIdOfPart(imagePart))
            // 880000 EMU = 0.962 inches (914400 EMU/inch)
        wordprocessingDocument.Close()
    }
}

private static void AddImageToRun(...)
{
    // Build a Drawing element containing the inline image
    Text textPlaceHolder = wordDoc.MainDocumentPart.Document.Body.Descendants<Text>()
                                  .Where(x => x.Text.Equals("##Appointments.Signature##"))
                                  .FirstOrDefault()
    if (textPlaceHolder != null) {
        textPlaceHolder.Parent.InsertAfter<Drawing>(element, textPlaceHolder)
        textPlaceHolder.Remove()
    }
}
```

**Key behaviors:**
- Hardcoded `ImagePartType.Png` - if the user uploaded a JPEG, it's still wrapped as PNG (likely works because OpenXml is forgiving, but worth verifying).
- Image size 880000 x 880000 EMU = **0.962 inches square**. The prior wave-1 audit said 0.6 inches - that was wrong.
- Looks for the EXACT text `"##Appointments.Signature##"` and replaces it. If the placeholder is split across multiple `<w:t>` elements (Word's standard rendering), the `Equals` check FAILS - the text is then NOT found and the image is NOT inserted. **This is fragile in OLD too**, but works in practice because the templates were authored with the placeholder as a contiguous text run.

### 2.10 OLD's 89-token universe (server-settings.json `documentMergeKeys.keys`)

Verified 2026-05-08 read of `P:\PatientPortalOld\PatientAppointment.Api\server-settings.json`. The `keys` array contains **88 unique strings** (one duplicate: `##Patients.LastName##` appears at lines 86 and 99 - bug in OLD config). Plus `##Others.DateNow##` is computed inline. Total: 88 + 1 = 89 tokens, matching prior audit.

**By group:**
- `##Patients.*`: 14 unique (15 entries with duplicate)
- `##Appointments.*`: 14
- `##EmployerDetails.*`: 7
- `##PatientAttorneys.*`: 10
- `##DefenseAttorneys.*`: 10
- `##InjuryDetails.*`: 22
- `##InjuryBodyPartDetails.*`: 1 (`BodyPartDescription`)
- `##CustomFieldValues.*`: 1 (`FieldValue`)
- `##Others.*`: 1 (`DateNow`)

**Tokens used by the 2 active OLD DOCX templates: 49** (44 in Patient Packet + 15 in Doctor Packet, with 10 shared). Documented in `wave-1-parity/packet-generation-audit.md` via a Python OOXML run-flattened parser. The 40 unused tokens correspond to fields that would be rendered by the 5 unreferenced packet templates (legacy / refactor leftover).

### 2.11 The 8 DOCX templates on disk

Verified 2026-05-08 read of `P:\PatientPortalOld\PatientAppointment.Api\wwwroot\Documents\documentBluePrint\`:

| Folder | DOCX file | Size | Used by current OLD code? |
|---|---|---|---|
| `attorneyclaimexaminer/` | `ATTORNEY CLAIM EXAMINER PACKET.docx` | (TBD) | YES - all 4 attorney+claim-examiner emails read this |
| `attornypacketame/` | `Attorney Packet For AME.docx` | (TBD) | NO - configured but unreferenced |
| `attornypacketpqme/` | `Attorney Packet For QME.docx` | (TBD) | NO |
| `claimexaminerpacketame/` | `Attorney Packet For AME.docx` | (TBD) | NO |
| `claimexaminerpacketpqme/` | `Attorney Packet For QME.docx` | (TBD) | NO |
| `doctorpacket/` | `DOCTOR PACKET.docx` | 1.0 MB | YES - generated + saved + NOT emailed |
| `patientpacket/` | `PATIENT PACKET.docx` | (TBD) | NO - legacy; referenced by `aws.patientPacket` setting which no code reads |
| `patientpacketnew/` | `PATIENT PACKET NEW.docx` | 343 KB | YES - emailed to patient + saved |

The duplicate filenames (`Attorney Packet For AME.docx` in two folders) suggests the AME / PQME variants of attorney vs claim-examiner packets were intended to differ but got copied as identical content. Inspection in Word/LibreOffice will confirm.

### 2.12 Per-recipient routing summary (the email + attachment matrix)

| Recipient | Source | Template | Email subject | Conditional |
|---|---|---|---|---|
| Patient | `appointment.Patient.Email` (single) | `aws.patientPacketNew` -> `patientpacketnew/PATIENT PACKET NEW.docx` | `Appointment Request Approved (Patient: X Y - Claim: A - ADJ: B)` | only if Patient.Email != null |
| Doctor | (NOT EMAILED, only stored) | `aws.doctorPacket` -> `doctorpacket/DOCTOR PACKET.docx` | - | always (storage-only) |
| Adjuster / Claim Examiner (PQME/AME) | `injury.AppointmentClaimExaminer.Email` (per active row, possibly multiple) | `aws.attorneyClaimExaminer` -> `attorneyclaimexaminer/ATTORNEY CLAIM EXAMINER PACKET.docx` | Same subject | only PQME/PQMEREEVAL/AME/AMEREEVAL appointment types |
| Defense Attorney (PQME) | `appointment.AppointmentDefenseAttorneys[*].AttorneyEmail` | `aws.attorneyClaimExaminer` (same file) | Same subject | only PQME/PQMEREEVAL |
| Patient Attorney (PQME) | `appointment.AppointmentPatientAttorneys[*].AttorneyEmail` | Same file | Same subject | only PQME/PQMEREEVAL |
| Defense Attorney (AME) | Same | Same | Same | only AME/AMEREEVAL |
| Patient Attorney (AME) | Same | Same | Same | only AME/AMEREEVAL |

**Order of fanout** (matches code line order): Patient -> Doctor (storage) -> Claim Examiner -> Defense Attorney -> Patient Attorney.

**Body** (all sends): same `EmailTemplate.AppointmentDocumentAddWithAttachment` template with `documentUploadUrl = clientUrl + "/appointment-new-documents/" + appointmentId` substituted.

---

## 3. NEW packet generation - line-by-line

### 3.1 Active code path (cover-page + uploaded-docs PDF merger)

`PacketGenerationOnApprovedHandler` (`Domain/AppointmentDocuments/Handlers/PacketGenerationOnApprovedHandler.cs`):
- Subscribes to `AppointmentStatusChangedEto`
- When `eventData.ToStatus == AppointmentStatusType.Approved`, enqueues `GenerateAppointmentPacketArgs { AppointmentId, TenantId }` to Hangfire

`GenerateAppointmentPacketJob.ExecuteAsync` (`Domain/AppointmentDocuments/Jobs/GenerateAppointmentPacketJob.cs`):
- Wraps body in `_currentTenant.Change(args.TenantId)`
- Constructs blob name `{tenantSegment}/{appointmentId}/packet/{guid:N}.pdf`
- `_packetManager.EnsureGeneratingAsync(tenantId, appointmentId, blobName)` - creates or flips an `AppointmentPacket` row to Generating status
- Loads appointment + patient + appointmentType + location + first injury detail
- `_coverGenerator.RenderCoverPagePdf(appointment, patient, appointmentType, location, claimNumber, bodyPartsSummary, wcabAdj)` - returns single-page PDF byte[] via MigraDoc
- Loads ALL `AppointmentDocument` rows where `Status == DocumentStatus.Accepted`, ordered by `CreationTime`
- For each, fetches the bytes from `IBlobContainer<AppointmentDocumentsContainer>`
- `_mergeService.Merge(coverBytes, inputs)` - returns merged PDF byte[] via PdfSharp
- `_packetsContainer.SaveAsync(blobName, ms, overrideExisting: true)`
- `_packetManager.MarkGeneratedAsync(packet.Id, blobName)` - flips status to Generated
- On exception (IOException, InvalidOperationException, PdfSharp errors): `_packetManager.MarkFailedAsync(packet.Id, ex.Message)` and **does NOT rethrow** (avoids Hangfire retry storm)

**No email send.** Stakeholders cannot retrieve the packet via email; they must use the download endpoint.

`AppointmentPacketsAppService.DownloadAsync(appointmentId)` (`Application/AppointmentDocuments/AppointmentPacketsAppService.cs`):
- `[Authorize(CaseEvaluationPermissions.AppointmentPackets.Default)]`
- Pulls the `AppointmentPacket` row, throws if not Generated yet
- Returns blob stream as `application/pdf` named `appointment-packet-{guid:N}.pdf`

### 3.2 Cover-page generator (`CoverPageGenerator.cs`)

Renders a single-page PDF via MigraDoc:
- Title: `CASE FILE PACKET` (18pt bold centered)
- Subtitle: `This packet contains the cover sheet plus all documents approved by the office for this appointment.` (10pt italic centered)
- 9-row 2-column table:
  - Confirmation #
  - Appointment Date (`MMM d, yyyy h:mm tt`)
  - Patient Name
  - Patient Email
  - Appointment Type
  - Location (Name only)
  - Claim # (or `(not on file)` when null)
  - WCAB ADJ # (or `(not on file)`)
  - Body Parts (or `(not on file)`)
- Footer: `Generated: {UTC}` + `Confidential -- patient health information. Handle per HIPAA + applicable workers' compensation rules.`

**Zero overlap with OLD's per-stakeholder DOCX content.** None of OLD's 49 tokens are used. No signature image. No legal-form structure.

### 3.3 Packet merge service (`PacketMergeService.cs`)

PdfSharp-based merger:
- Cover page is appended first
- For each `MergeInput { FileName, ContentType, Bytes }`:
  - PDF (content-type contains "pdf" or filename ends `.pdf`): `PdfReader.Open(... PdfDocumentOpenMode.Import)` and copy each page via `output.AddPage(input.Pages[i])`
  - JPG/JPEG/PNG: render onto a fresh A4 page using XImage, scale-to-fit with 0.5" margins
  - Anything else: skipped with warning
- Returns the merged PDF byte[]

### 3.4 The `PatientPacketTemplate.cs` skeleton (DEAD CODE)

A 553-line QuestPDF `IDocument` that DOES replicate OLD's Patient Packet structure:

- 9 ordered sections matching the audit's section outline:
  1. ACTIVITIES OF DAILY LIVING FORM (7 sub-questionnaires with checkbox grids)
  2. RELEASE OF MEDICAL RECORDS (West Coast Spine Institute hardcoded)
  3. PRESENT COMPLAINTS (pain symptom checkboxes, body diagram placeholders)
  4. AMA GUIDELINES (5TH EDITION) - Pain Questionnaire (0-10 scales)
  5. Patient signature page
  6. Cover letter to patient (token-heavy)
  7. Case-info block
  8. Sign-off (signature image rendering with QuestPDF `Image()`)
  9. Secondary claim summary (parties + WCAB)

- Page header repeat (NAME / ACCT / DATE) on every page
- Letter page size, 0.5" margins
- Calibri 10pt default font
- Page footer with `CurrentPageNumber / TotalPages`
- Signature size: `SignatureSizePoints = 60f` (~0.83 in. at 72 DPI; OLD is 0.96 in.) - documented as "approximate matching"

**This file is NEVER instantiated.** Grep returns only the class definition + the `PacketTokenContext` doc-comment. The `GenerateAppointmentPacketJob` does not import it. `AppointmentPacketManager` does not call it. `AppointmentPacketsAppService` does not call it. **It's dead code, ready to be wired but never used.**

### 3.5 PacketTokenContext (the contract)

`PacketTokenContext.cs` - 49 properties matching the union of tokens used by Patient Packet (44) + Doctor Packet (15). Property names are the OLD token's column part (after `##Group.`); each carries an XML doc-comment with the literal OLD token string.

The doc-comments are precise about per-token behavior:
- All strings already `.ToUpper()`'d, already formatted, already null-collapsed
- `ResponsibleUserSignature` is `byte[]?` (PNG or JPEG); template skips when null
- InjuryDetails / BodyParts / CustomFields pre-concatenated by the resolver

**No code populates this class.** A resolver service would need to:
1. Query all `appointment.AppointmentInjuryDetails` and concatenate per-injury values for every InjuryDetails token
2. Query `vPatient`-equivalent (Patient + State navigation properties) for Patients tokens
3. Compute `Others.DateNow = DateTime.Today.ToString("MM/dd/yyyy")`
4. Pull `IdentityUser.Name + " " + Surname` for `PrimaryResponsibleUserName`
5. Resolve State.Name across multiple FKs (Patient.StateId, Location.StateId, AppointmentEmployerDetail.StateId, ApplicantAttorney.StateId, DefenseAttorney.StateId, WcabOffice.StateId, AppointmentPrimaryInsurance.StateId)
6. Pull the responsible user's signature image bytes from blob storage
7. Apply `.ToUpper()` to all values except `InjuryBodyPartDetails`

### 3.6 AppointmentPacket entity (`AppointmentPacket.cs`)

```
public class AppointmentPacket : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid AppointmentId { get; set; }    // 1:1 logically per the doc-comment
    [NotNull] public string BlobName { get; set; } = null!;
    public PacketGenerationStatus Status { get; set; }    // Generating / Generated / Failed
    public DateTime GeneratedAt { get; set; }
    public DateTime? RegeneratedAt { get; set; }    // null until first regen
    public string? ErrorMessage { get; set; }    // populated when Status=Failed
}
```

**1:1 per Appointment.** The schema cannot represent multiple recipient packets per appointment. To match OLD, the schema needs:
- `PacketKind` enum (Patient / Doctor / AttorneyClaimExaminer / etc.)
- 1-to-many: one `AppointmentPacket` per `(AppointmentId, PacketKind)` tuple
- Composite uniqueness `(AppointmentId, PacketKind)`

The prior wave-1 audit recommended this exact schema change. NOT YET IMPLEMENTED.

### 3.7 Storage container

`AppointmentPacketsContainer` (referenced in `BlobContainers`) - one ABP `IBlobContainer` instance. Tenant-prefixed paths. ABP's blob storage abstraction supports filesystem (dev), S3, Azure Blob.

### 3.8 NEW XML doc literally states the gap

From `AppointmentPacket.cs:11-25` verbatim:

> "W2-11: per-appointment merged-PDF packet. One row per Appointment. Generated by Jobs.GenerateAppointmentPacketJob when the appointment transitions to Approved. **Format adopted from OLD (which produced DOCX per recipient via DocumentFormat.OpenXml mail-merge of pre-authored templates) but rendered as a single PDF per appointment for MVP -- per Adrian's directive 'ensure we make a pdf until I get the confirmed format'. Per-recipient templates + token mail-merge are a deferred enhancement when the manager confirms the exact format.**"

So the divergence is intentional and was tracked. Adrian's 2026-05-08 directive un-defers it: rebuild OLD's per-recipient DOCX-template generator now.

---

## 4. Mapping table - OLD packet flow -> NEW

| OLD step | NEW today | Action needed |
|---|---|---|
| Trigger: `AppointmentDomain.Update:564` on Approved + InternalUserUpdate | `PacketGenerationOnApprovedHandler` on `AppointmentStatusChangedEto` | Trigger OK; expand handler to enqueue 3 packet jobs (one per kind), not 1 |
| Trigger 2: `AppointmentDomain.Add:282` for internal-user-creates-as-Approved | (covered by status-change handler if Add publishes the Eto with ToStatus=Approved) | Verify the Eto fires for Add path too |
| Pre-flight: load 89-token universe | (none - tokens are hardcoded properties on `PacketTokenContext`) | Define `PacketTokenContext` populator service |
| Pre-flight: download template into temp dir | (none) | Embed the 3 OLD DOCX templates as embedded resources OR copy them into the NEW repo |
| Pre-flight: build `documentUploadUrl` | (none) | Build via per-tenant `PortalBaseUrl` setting + appointment ID |
| Pre-flight: build `emailBody` from `AppointmentDocumentAddWithAttachment` template | None - template is seeded as stub in NEW | Migrate the OLD HTML body |
| Patient packet: download `aws.patientPacketNew` template | (none) | Read embedded resource `PATIENT_PACKET_NEW.docx` |
| Patient packet: stamp signature at `##Appointments.Signature##` | (none in DOCX path; QuestPDF dead code uses Image()) | Use OpenXml to stamp signature; mirror OLD `InsertAPicture` |
| Patient packet: ReplaceText (89 tokens, ToUpper, all-injuries-concat) | (none) | Build `IPacketTokenResolver.PopulateAsync(Appointment) -> PacketTokenContext` + an OpenXml-based `IDocxTemplateRenderer.Render(template, context) -> byte[]` |
| Patient packet: save to local disk + register `AppointmentNewDocument` row + email Patient with attachment | Save to `AppointmentPacketsContainer` blob + `AppointmentPacket` row (status Generated) | Wire NEW: save DOCX to blob, register `AppointmentPacket` row with `PacketKind=Patient`, send email-with-attachment to Patient.Email |
| Doctor packet: same generation, NO email, save only | None | Wire: save DOCX with `PacketKind=Doctor`. Don't email (matches OLD asymmetry). |
| Adjuster/ClaimExaminer packet: per active examiner row, email | None | Wire: per active `AppointmentClaimExaminer.Email`, send email with `attorneyclaimexaminer` DOCX attached. **Question: Should NEW use the same single template for all 4 attorney+claim-examiner recipients (matches current OLD code) OR use the per-PQME/AME templates that exist on disk but aren't read?** |
| Attorney packet (PQME branch): foreach defense + foreach patient attorney with email | None | Wire: per attorney row with email, send email with packet attached. |
| Attorney packet (AME branch): same but `aws.attorneyClaimExaminer` (same URL in OLD code) | None | Same |
| 3 NRE bugs at `:882, :886, :890` | (irrelevant - resolver missing) | Don't replicate. Add null guards per CLAUDE.md "Clear bug" rule. |

---

## 5. The 49-token contract (PacketTokenContext properties + OLD source mappings)

Verified against OLD `AppointmentDocumentDomain.cs:865-1190` and `server-settings.json` 2026-05-08. All values `.ToUpper()`'d at insertion time except `InjuryBodyPartDetails`.

### Patients group (vPatient view)

| OLD token | NEW property | Source | Transform |
|---|---|---|---|
| `##Patients.FirstName##` | PatientFirstName | Patient.FirstName | ToUpper |
| `##Patients.LastName##` | PatientLastName | Patient.LastName | ToUpper |
| `##Patients.MiddleName##` (Doctor only) | PatientMiddleName | Patient.MiddleName | nullable -> "", ToUpper |
| `##Patients.DateOfBirth##` | PatientDateOfBirth | Patient.DateOfBirth | format MM/dd/yyyy, ToUpper |
| `##Patients.SocialSecurityNumber##` | PatientSocialSecurityNumber | Patient.SocialSecurityNumber | nullable -> "", ToUpper |
| `##Patients.Street##` | PatientStreet | Patient.Street | ToUpper. **NB:** Patient also has `Address` field; OLD uses Street. |
| `##Patients.City##` | PatientCity | Patient.City | nullable -> "", ToUpper |
| `##Patients.State##` | PatientState | Patient.StateId -> State.Name | nullable -> "", ToUpper |
| `##Patients.ZipCode##` (Patient only) | PatientZipCode | Patient.ZipCode | ToUpper |

### Appointments group (vAppointmentDetail view)

| OLD token | NEW property | Source | Transform |
|---|---|---|---|
| `##Appointments.RequestConfirmationNumber##` | RequestConfirmationNumber | Appointment.RequestConfirmationNumber | non-null |
| `##Appointments.AvailableDate##` | AvailableDate | DoctorAvailability.AvailableDate | format MM/dd/yyyy, ToUpper |
| `##Appointments.AppointmenTime##` (typo preserved in token; NEW property is renamed) | AppointmentTime | DoctorAvailability.FromTime | format `h:mm tt` (12h en-US), ToUpper. **Phase 1 sample comparison vs OLD will confirm if this should be a range `9:00 AM - 9:30 AM` instead.** |
| `##Appointments.AppointmentType##` (Doctor only) | AppointmentType | AppointmentType.Name | non-null |
| `##Appointments.Location##` | LocationName | Location.Name | non-null |
| `##Appointments.LocationAddress##` (Patient only) | LocationAddress | Location.Address | nullable -> "", ToUpper |
| `##Appointments.LocationCity##` (Patient only) | LocationCity | Location.City | nullable -> "", ToUpper |
| `##Appointments.LocationState##` | LocationState | Location.StateId -> State.Name | nullable -> "", ToUpper |
| `##Appointments.LocationZipCode##` | LocationZipCode | Location.ZipCode | nullable -> "", ToUpper |
| `##Appointments.LocationParkingFee##` | LocationParkingFee | Location.ParkingFee (decimal) | `value.ToString()` then ToUpper. **NO currency symbol, NO 2-decimal padding.** Empty string when null. |
| `##Appointments.PrimaryResponsibleUserName##` | PrimaryResponsibleUserName | Appointment.PrimaryResponsibleUserId -> IdentityUser.Name + " " + Surname | nullable -> "", ToUpper |
| `##Appointments.Signature##` (Patient only; rendered as image, not text) | ResponsibleUserSignature (byte[]?) | per-user PNG/JPEG bytes | Inserted via OpenXml as 880000x880000 EMU image; placeholder text removed; null silently skips |

### EmployerDetails group (vAppointmentEmployerDetail view; FirstOrDefault row)

| OLD token | NEW property | Source | Transform |
|---|---|---|---|
| `##EmployerDetails.EmployerName##` | EmployerName | AppointmentEmployerDetail.EmployerName | nullable -> "", ToUpper |
| `##EmployerDetails.Street##` (Doctor only) | EmployerStreet | AppointmentEmployerDetail.Street | nullable -> "", ToUpper |
| `##EmployerDetails.City##` (Doctor only) | EmployerCity | AppointmentEmployerDetail.City | nullable -> "", ToUpper |
| `##EmployerDetails.State##` (Doctor only) | EmployerState | StateId -> State.Name | nullable -> "", ToUpper |
| `##EmployerDetails.Zip##` (Doctor only) | EmployerZip | AppointmentEmployerDetail.ZipCode | nullable -> "", ToUpper. **NB:** NEW property is named `ZipCode`, OLD token is `Zip`. |

### PatientAttorneys group (Patient Packet only; FirstOrDefault link table row)

| OLD token | NEW property | Source | Transform |
|---|---|---|---|
| `##PatientAttorneys.AttorneyName##` | PatientAttorneyName | first AppointmentApplicantAttorney -> ApplicantAttorney -> IdentityUser.Name + " " + Surname | nullable -> "", ToUpper |
| `##PatientAttorneys.Street##` | PatientAttorneyStreet | ApplicantAttorney.Street | ToUpper |
| `##PatientAttorneys.City##` | PatientAttorneyCity | ApplicantAttorney.City | ToUpper |
| `##PatientAttorneys.State##` | PatientAttorneyState | ApplicantAttorney.StateId -> State.Name | ToUpper |
| `##PatientAttorneys.Zip##` | PatientAttorneyZip | ApplicantAttorney.ZipCode | ToUpper. NB: NEW is `ZipCode`, OLD is `Zip`. |

### DefenseAttorneys group (Patient Packet only; FirstOrDefault)

Same shape as PatientAttorneys, fields prefixed `DefenseAttorney`.

### InjuryDetails group (vInjuryDetail; ALL rows space-concatenated + ToUpper)

| OLD token | NEW property | Source | Transform |
|---|---|---|---|
| `##InjuryDetails.ClaimNumber##` | InjuryClaimNumber | per AppointmentInjuryDetail.ClaimNumber | space-concat across rows + ToUpper |
| `##InjuryDetails.DateOfInjury##` | InjuryDateOfInjury | per row, format MM/dd/yyyy | space-concat + ToUpper |
| `##InjuryDetails.WcabAdj##` (Patient only) | InjuryWcabAdj | per AppointmentInjuryDetail.WcabAdj | space-concat + ToUpper |
| `##InjuryDetails.WcabOfficeName##` (Patient only) | InjuryWcabOfficeName | per row, WcabOfficeId -> WcabOffice.Name | space-concat + ToUpper |
| `##InjuryDetails.WcabOfficeAddress##` (Patient only) | InjuryWcabOfficeAddress | WcabOffice.Address | space-concat + ToUpper |
| `##InjuryDetails.WcabOfficeCity##` (Patient only) | InjuryWcabOfficeCity | WcabOffice.City | space-concat + ToUpper |
| `##InjuryDetails.WcabOfficeState##` (Patient only) | InjuryWcabOfficeState | WcabOffice.StateId -> State.Name | space-concat + ToUpper |
| `##InjuryDetails.WcabOfficeZipCode##` (Patient only) | InjuryWcabOfficeZipCode | WcabOffice.ZipCode | space-concat + ToUpper |
| `##InjuryDetails.PrimaryInsuranceName##` | InjuryPrimaryInsuranceName | first AppointmentPrimaryInsurance per injury -> .Name | space-concat + ToUpper |
| `##InjuryDetails.PrimaryInsuranceStreet##` (Patient only) | InjuryPrimaryInsuranceStreet | .Street | space-concat + ToUpper |
| `##InjuryDetails.PrimaryInsuranceCity##` (Patient only) | InjuryPrimaryInsuranceCity | .City | space-concat + ToUpper |
| `##InjuryDetails.PrimaryInsuranceState##` (Patient only) | InjuryPrimaryInsuranceState | .StateId -> State.Name | space-concat + ToUpper |
| `##InjuryDetails.PrimaryInsuranceZip##` (Patient only) | InjuryPrimaryInsuranceZip | .Zip | space-concat + ToUpper. **NB:** AppointmentPrimaryInsurance uses `Zip` (one of the few NEW entities that doesn't use `ZipCode`). |
| `##InjuryDetails.ClaimExaminerName##` (Doctor only) | InjuryClaimExaminerName | first AppointmentClaimExaminer per injury -> IdentityUser.Name + " " + Surname | space-concat + ToUpper |

### Others

| OLD token | NEW property | Source | Transform |
|---|---|---|---|
| `##Others.DateNow##` | DateNow | computed at packet generation | `DateTime.Today.ToString("MM/dd/yyyy")`, ToUpper (no-op for digits) |

---

## 6. Concrete gaps that need closure

### P1. `PatientPacketTemplate` is dead code; no `DoctorPacketTemplate` or `AttorneyClaimExaminerPacketTemplate` exists

**Action:**
- Decide DOCX-first or QuestPDF-first generation strategy. **Adrian's directive says DOCX-first, then PDF conversion.**
- If DOCX-first: embed OLD's 3 active DOCX templates as resources. Build `IDocxTemplateRenderer.Render(templateBytes, PacketTokenContext) -> byte[]` using DocumentFormat.OpenXml. Mirror OLD's `ReplaceText` + `InsertAPicture` logic.
- If QuestPDF-first: complete `PatientPacketTemplate` (currently has 9 sections at high level but several sections are placeholders), build `DoctorPacketTemplate` and `AttorneyClaimExaminerPacketTemplate`. Wire them into the job. **But this means abandoning the legal-document-format constraint Adrian flagged.**
- Recommended: DOCX-first (matches Adrian's directive verbatim). Delete `PatientPacketTemplate.cs` after the DOCX path is wired.

### P2. `AppointmentPacket` schema is 1:1 per Appointment

**Action:** Add `PacketKind` enum (`Patient = 0, Doctor = 1, AttorneyClaimExaminer = 2`). Make the unique constraint composite: `(TenantId, AppointmentId, PacketKind)`. Add EF migration. Update `AppointmentPacketManager.EnsureGeneratingAsync` to accept the kind. Update `GenerateAppointmentPacketJob` to enqueue 3 jobs (or 1 job that loops over kinds). Update `AppointmentPacketsAppService.DownloadAsync` to accept `PacketKind`.

### P3. Packet email-with-attachment delivery is unimplemented

**Action:** Add a `SendAppointmentPacketEmailJob` Hangfire job that accepts `(AppointmentId, PacketKind, RecipientEmail)`. Body uses the OLD-verbatim `AppointmentDocumentAddWithAttachment` HTML template (need to migrate that body too). Subject `"Appointment Request Approved " + bracketed-patient-details`. Attachment is the rendered DOCX from blob storage.

For each kind, dispatch:
- Patient: 1 job to `Patient.Email` (skip if null)
- Doctor: 0 jobs (storage only)
- AttorneyClaimExaminer: N+M+K jobs (all active claim examiners, all defense attorneys, all patient attorneys)

The dispatch logic mirrors OLD `AppointmentDocumentDomain.AddAppointmentDocumentsAndSendDocumentToEmail` `:463-859`.

### P4. Token resolver missing

**Action:** Build `IPacketTokenResolver.ResolveAsync(Appointment, IdentityUser responsibleUser) -> PacketTokenContext`. Implements:
- 6 single-row groups (Patients / Appointments / EmployerDetails / PatientAttorneys / DefenseAttorneys / Others)
- 3 multi-row space-concatenated groups (InjuryDetails / InjuryBodyPartDetails / CustomFieldValues)
- Signature byte fetch via blob storage
- ToUpper on all except InjuryBodyPartDetails
- Trailing space behavior on space-concatenated values (matches OLD's `recordValue += value + " "`)

### P5. Per-kind generator dispatch

**Action:** Build `IPacketGenerator` with a method per kind, OR a single `GenerateAsync(AppointmentId, PacketKind)` that switches templates. Each kind specifies:
- The source DOCX template (embedded resource)
- The token subset to render (Patient Packet uses 44 tokens, Doctor Packet uses 15)
- The list of email recipients (or none for Doctor)

### P6. Decide PQME/AME differentiation for attorney+claim-examiner packets

**Open question for Adrian.** OLD currently uses the SAME `aws.attorneyClaimExaminer` template for all 4 attorney/claim-examiner sends regardless of PQME/AME. Disk has 4 different folders + DOCX files + 4 different server-setting keys, but the C# code never reads them. Options:
- **A.** Match current OLD code: single `attorneyclaimexaminer` template for all 4 paths, ignore the per-PQME/AME templates.
- **B.** Use the variable-name intent: 4 separate templates per (PQME/AME) x (Attorney/ClaimExaminer).

If B, embed all 5 attorney+claim-examiner templates and build per-kind generators for each.

### P7. `AppointmentDocumentAddWithAttachment` email body is a stub in NEW

**Action:** Copy `P:\PatientPortalOld\PatientAppointment.Api\wwwroot\EmailTemplates\Appointment-Document-Add-With-Attachment.html` into `src/.../Domain/NotificationTemplates/EmailBodies/AppointmentDocumentAddWithAttachment.html`. Add subject string to `EmailSubjects.ByCode`.

### P8. DOCX -> PDF conversion (deferred per Adrian)

**Adrian's directive:** "First we will ensure we can generate all the packets exactly how the templates are in DOCX format, then we will look into a way to convert them to PDFs before we send it to stakeholders in email or make them downloadable on the portal."

**Action:** After P1-P7 ship and produce DOCX output that matches OLD visually, add a conversion step. Options:
- LibreOffice headless (`soffice --headless --convert-to pdf ...`) - free, requires LibreOffice in container
- Aspose.Words for .NET - paid, high fidelity
- Spire.Doc - paid, similar
- Microsoft Office Interop - Windows-only, NOT viable in Linux containers
- DocX -> use OpenXml as input to a custom PDF emitter (high effort)

Recommended evaluation: LibreOffice headless first (cost = $0, baseline fidelity). Aspose.Words if LibreOffice rendering misses any layout details.

### P9. Storage path

OLD: `wwwroot/Documents/submittedDocuments/{ConfirmationNumber}_{PacketName}_{ddMMyyyy_hhmmss}.docx`
NEW: `{tenantSegment}/{appointmentId}/packet/{guid:N}.pdf`

**Action:** Adopt NEW's blob path as `{tenantSegment}/{appointmentId}/packet/{packetKind}/{guid:N}.{ext}` where `{ext}` is `docx` (Phase 1) or `pdf` (Phase 2). Filename for download/email attachment: `{ConfirmationNumber}_{PacketName}_{yyyyMMdd_HHmmss}.{ext}` (matches OLD pattern).

### P10. Triggers - cover both OLD entry points

OLD fires from BOTH `Update:564` (status change to Approved) AND `Add:282` (internal user creates with status=Approved). NEW has `PacketGenerationOnApprovedHandler` listening to `AppointmentStatusChangedEto`.

**Action:** Verify `AppointmentSubmittedEto` (the create-path Eto) ALSO publishes `AppointmentStatusChangedEto` (or a separate `AppointmentApprovedEto`) when an internal user submits at Approved status directly. If not, add the publish.

---

## 7. Implementation contract for the next session

### 7.1 Order of work (recommended)

1. **P2 schema migration** - add `PacketKind` to `AppointmentPacket`, composite uniqueness, EF migration. ~1 day.
2. **P4 token resolver** - implement `IPacketTokenResolver` + populate `PacketTokenContext`. Unit-test with sample data covering single-injury, multi-injury, missing-employer, missing-attorney edge cases. ~2-3 days.
3. **P1 DOCX renderer** - embed the 3 OLD DOCX templates. Build `IDocxTemplateRenderer` using OpenXml `WordprocessingDocument`. Implement `ReplaceText` + `InsertAPicture` mirroring OLD. Unit-test signature stamping + token replacement against the embedded templates. ~3-5 days.
4. **P5 per-kind generator dispatch** - replace `GenerateAppointmentPacketJob` with a multi-kind orchestrator. ~1 day.
5. **P10 trigger coverage** - confirm both Add and Update paths publish the Eto. ~half-day.
6. **P3 email-with-attachment delivery** - add `SendAppointmentPacketEmailJob`. Wire fan-out to Patient.Email + active ClaimExaminers + Defense + Patient attorneys. ~2 days.
7. **P7 email body migration** - copy the OLD HTML body. ~1 hour.
8. **P9 storage path adjustment** - extend blob path with kind. ~half-day.
9. **Visual diff** - render Patient + Doctor + AttorneyClaimExaminer packets against a synthetic appointment. Compare against OLD output side-by-side in Word/LibreOffice. Iterate. ~2-3 days.
10. **P8 DOCX -> PDF conversion** - integrate LibreOffice headless. ~2-3 days.

**Total Phase 1 effort estimate:** 12-20 dev-days.

### 7.2 Validation criteria per packet (next-session checklist)

For each of the 3 packets, verify:
- [ ] Source DOCX template embedded as resource at `src/.../Domain/AppointmentDocuments/Templates/{name}.docx`
- [ ] All token positions in the template render to the expected `PacketTokenContext` property values
- [ ] Signature image placeholder removed and replaced with image (Patient Packet only)
- [ ] All values uppercased except InjuryBodyPartDetails
- [ ] Multi-injury appointment: InjuryDetails / InjuryBodyPartDetails / CustomFieldValues space-concatenate all rows
- [ ] Single-injury appointment: trailing space matches OLD pattern
- [ ] Output DOCX opens cleanly in Word and LibreOffice
- [ ] Visual diff against OLD-rendered DOCX is acceptable (Adrian sign-off)
- [ ] Email-with-attachment delivery: Patient gets the right kind; Adjuster/Examiner gets the right kind for PQME/AME; Attorneys get the right kind. Doctor packet is NOT emailed but IS persisted.
- [ ] DB row in `AppointmentPacket` per kind, status flips Generating -> Generated (or Failed)
- [ ] Download endpoint serves the right kind on demand

### 7.3 Verification commands

```bash
# Trigger the flow: approve an appointment as Clinic Staff. Then:
docker logs replicate-old-app-api-1 --tail 100 | grep PacketGeneration
curl -s http://localhost:44327/hangfire | grep -i packet     # Hangfire dashboard

# Inspect the generated blob (LibreOffice can open .docx if you copy it out)
docker exec replicate-old-app-api-1 ls -la /app/blobs

# Download via API (when AppointmentPacketsAppService is extended to accept kind)
curl -s -o patient.docx -H "Authorization: Bearer $TOKEN" \
    "http://localhost:44327/api/app/appointment-packets/{id}/download?kind=Patient"
```

---

## 8. Open questions for Adrian

1. **PQME/AME differentiation (P6):** Use the unified `attorneyclaimexaminer` template (matches current OLD code) OR per-PQME/AME templates (matches OLD intent + disk reality)?
2. **Doctor Packet email asymmetry:** Replicate OLD's "generate but don't email Doctor packet"? Doctor presumably picks it up from the portal at the visit.
3. **Multiple active Claim Examiners per injury:** OLD emails one packet per active examiner per injury (could be many emails per appointment). Confirm that's the intended behavior.
4. **Multi-injury packet rendering:** OLD concatenates ALL injuries' values into one rendered string in the InjuryDetails fields. Multi-injury appointments produce values like `"ABC123 XYZ789 "` in a Claim # field. Is that the desired final output, or should NEW render a list/table of injuries?
5. **Hardcoded `West Coast Spine Institute` + `Yuri Falkinstein, M.D.`:** OLD bakes these into the Patient Packet template content (not as tokens). NEW Phase 1A is Falkinstein-only so this is fine, but Phase 2 multi-tenant needs `##Tenant.ClinicName##` and `##Tenant.PrimaryDoctorName##` tokens. Track now or later?
6. **DOCX format constraint for legal documents:** Adrian wrote "These are legal documents and have to formatted the way they are." Does this mean (a) byte-for-byte identical to OLD output, (b) visually identical when opened in Word, or (c) substantively the same content + layout but rendering may differ? The expected fidelity bar drives the renderer choice (OpenXml ReplaceText vs DocX library vs MailMerge SDK).
7. **`PatientPacketTemplate.cs` (the QuestPDF dead code):** delete now? It's 553 lines, references `DoctorPacketTemplate` that doesn't exist, and conflicts with the DOCX-first directive.
8. **`patientpacket` legacy template:** delete this `aws.patientPacket` server-setting key from NEW once we ship `aws.patientPacketNew`-equivalent. Both files exist on OLD's disk; only the NEW is referenced.
9. **Cover page in DOCX flow:** OLD's templates don't have a cover sheet - the `AppointmentDocumentAddWithAttachment` email IS the cover. Should NEW retain the QuestPDF-rendered cover page (currently the entire NEW packet)? My read: drop it. The OLD pattern is template content + email body; no separate cover.

---

## 9. Errata against the prior wave-1 audit

The prior `wave-1-parity/packet-generation-audit.md` contains useful structural analysis but these errors:
- "**Image size 0.6 in. x 0.6 in.**" - WRONG. Verified from OLD code: `InsertAPicture` calls `AddImageToRun(... 880000L, 880000L ...)`. 880000 EMU / 914400 EMU/inch = 0.962 inches. The audit's `:957` reference is correct on the line but wrong on the math.
- "**Storage path: `{tenantSegment}/{appointmentId}/packet/{packetGuid}.pdf`**" - matches NEW current path but lacks the `{packetKind}` segment that the multi-kind redesign needs.
- "**44 + 15 = 49 distinct tokens**" - correct (verified by Python OOXML run-flattened parser).
- The audit references a `DoctorPacketTemplate` class that does NOT exist in code today (only `PatientPacketTemplate` exists, and even that is dead code).
- The audit recommends `PacketKind enum (Patient = 0, Doctor = 1)` + composite uniqueness - **this recommendation was never implemented.** The current `AppointmentPacket` is still 1:1 per appointment.

---

## 10. Source pointers

### OLD
- `P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDocumentDomain.cs` - lines `:394-863` (orchestrator), `:865-952` (ReplaceText), `:954-1052` (InsertAPicture), `:1053-1099` (GetColumnValues), `:1125-1170` (GetColumnValuesForInjury), `:1172-1190` (GetColumnValuesForCustomeField)
- `P:\PatientPortalOld\PatientAppointment.Infrastructure\Utilities\ApplicationUtility.cs:212-251` - GetEmailTemplateFromHTML for the email body
- `P:\PatientPortalOld\PatientAppointment.Infrastructure\Utilities\ApplicationUtility.cs` - ReplaceTextOfWordDocument (the actual OpenXml find/replace; need to read to confirm exact behavior)
- `P:\PatientPortalOld\PatientAppointment.Api\server-settings.json` - 9 packet keys + 89-token list + branding strings (West Coast Spine Institute, Falkinstein) + AWS S3 keys (dead)
- `P:\PatientPortalOld\PatientAppointment.Api\wwwroot\Documents\documentBluePrint\` - 8 DOCX templates (3 active, 5 unused)
- `P:\PatientPortalOld\PatientAppointment.Api\wwwroot\EmailTemplates\Appointment-Document-Add-With-Attachment.html` - the packet email body
- `P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs:282, :564` - the two callers

### NEW
- `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/Handlers/PacketGenerationOnApprovedHandler.cs` - Eto subscriber
- `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/Jobs/GenerateAppointmentPacketJob.cs` - Hangfire job
- `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/AppointmentPacket.cs` - entity
- `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/AppointmentPacketManager.cs` - state transitions
- `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/CoverPageGenerator.cs` - MigraDoc cover page (deletable)
- `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/PacketMergeService.cs` - PdfSharp merger (deletable in DOCX-first; keep for PDF-second)
- `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/Templates/PatientPacketTemplate.cs` - dead QuestPDF code
- `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/Templates/PacketTokenContext.cs` - 49-property contract (KEEP)
- `src/HealthcareSupport.CaseEvaluation.Application/AppointmentDocuments/AppointmentPacketsAppService.cs` - download endpoint
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/AppointmentDocuments/AppointmentPacketConsts.cs` - blob name max length etc.
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/AppointmentDocuments/PacketGenerationStatus.cs` - 3-value enum
- `docs/parity/email-packet-parity/email-service.md` - companion doc
- `docs/parity/wave-1-parity/packet-generation-audit.md` - prior research (verified-and-superseded by this doc)

---

## 11. Sample synthetic data for verification

After Phase 1 ships, render packets against this synthetic appointment (no PHI):

```yaml
Appointment:
  RequestConfirmationNumber: A12345
  AppointmentDate: 2026-08-15
  AppointmentType: PQME
  Location:
    Name: West Coast Spine Institute - Sherman Oaks
    Address: 16550 Ventura Blvd, Suite 300
    City: Encino
    State: California
    ZipCode: "91436"
    ParkingFee: 10.00
  DoctorAvailability:
    AvailableDate: 2026-08-15
    FromTime: "09:00:00"
    ToTime: "09:30:00"
  PrimaryResponsibleUser: Dr. Yuri Falkinstein, M.D.
Patient:
  FirstName: Test
  MiddleName: Q
  LastName: Patient
  DateOfBirth: 1980-01-01
  SocialSecurityNumber: "000-00-0000"
  Email: test.patient@example.com
  Street: 100 Main St
  City: Sherman Oaks
  State: California
  ZipCode: "91423"
EmployerDetails:
  EmployerName: Acme Workers Test Co
  Street: 200 Industrial Way
  City: Burbank
  State: California
  ZipCode: "91501"
InjuryDetails (single):
  ClaimNumber: CLAIM-TEST-001
  DateOfInjury: 2025-03-15
  WcabAdj: ADJ-TEST-001
  WcabOffice:
    Name: Van Nuys WCAB
    Address: 6150 Van Nuys Blvd
    City: Van Nuys
    State: California
    ZipCode: "91401"
  PrimaryInsurance:
    Name: Test Insurance Co
    Street: 300 Insurance Pl
    City: Glendale
    State: California
    Zip: "91201"
  ClaimExaminer:
    Name: Test Examiner
    Email: examiner@test.example
  BodyParts:
    - Lower Back
    - Right Knee
ApplicantAttorney (single):
  Name: Test Applicant Attorney
  Email: aa@test.example
  Street: 400 Law Plaza
  City: Beverly Hills
  State: California
  ZipCode: "90210"
DefenseAttorney (single):
  Name: Test Defense Attorney
  Email: da@test.example
  Street: 500 Defense Way
  City: Pasadena
  State: California
  ZipCode: "91101"
ResponsibleUserSignature: (provide a 256x256 PNG with "SIG" text for verification)
```

Open both OLD's rendered DOCX and NEW's rendered DOCX in LibreOffice / Word side-by-side. They should be visually indistinguishable (same fonts, same spacing, same token values rendered ToUpper, same signature image position).

---

## 12. Pass 2 corrections (2026-05-08) - read this section first

Pass 1 cited `ApplicationUtility.ReplaceTextOfWordDocument` and `InsertAPicture` without reading them in full, missed the Angular packet UI, miscounted the OLD config, and over-stated the implementation gap. This section is the corrected ground truth.

### 12.1 OLD `ReplaceTextOfWordDocument` is fragile and naive (verified)

Pass 1 cited "ApplicationUtility.ReplaceTextOfWordDocument (the actual OpenXml find/replace; need to read to confirm exact behavior)" - Pass 2 read it. Verbatim summary at `email-service.md` Section 11.7. Key facts:

- **Treats DOCX XML as plain text.** Calls `MarkupSimplifier.SimplifyMarkup` (PowerTools for OpenXml) with 16 aggressive flags first, then reads `MainDocumentPart` stream as `string`, runs `string.Replace(token, value)` for each kvp, writes back.
- **Bug: mutates input dict.** `if (value.Contains('&')) { keyValuePairs[key] = value.Replace('&', ' '); }` - this strips ampersands by mutating the source dictionary. Don't replicate.
- **Bug: no XML escaping of values.** If a token value contains `<`, `>`, `&` (not stripped), `"`, or `'`, the resulting docx is invalid XML and Word fails to open it. Token values upstream are `.ToUpper()`'d (`AppointmentDocumentDomain.cs:1070`) but uppercasing does NOT escape XML.
- **`MarkupSimplifier` removes:** bookmarks, comments, content controls, end notes, footnotes, field codes, web-hidden, last-rendered page break, permissions, proof errors, rsid info, smart tags, soft hyphens. Replaces tabs with spaces. Templates that rely on these features (esp. content controls / field codes / mail-merge fields) break silently.
- **The simplifier IS what makes flat-Replace mostly work** because Word splits text runs at proofErr boundaries, smart-tag wrappers, etc. - tokens like `##Patients.FirstName##` get split across multiple `<w:r><w:t>` runs invisibly. The simplifier collapses these back to contiguous text. Without it, naive `Replace` would miss most tokens.

**Implication for the next session's NEW renderer:** the "right" implementation walks every `<w:t>` element via OpenXml SDK and replaces tokens at the run level (with cross-run reconstruction for split tokens). The "OLD-faithful but bug-fixed" implementation keeps the simplifier-then-string approach but adds proper XML escaping. Both are documented choices; recommend the former.

### 12.2 OLD has 89 tokens (NOT 88+1 duplicate)

Pass 1 Section 2.10 said "**88 unique strings** (one duplicate: `##Patients.LastName##` appears at lines 86 and 99)" - this is true on the JSON config side. But the runtime token universe is 89 because `##Others.DateNow##` is computed inline at L905-912 and added separately, NOT pulled from `documentMergeKeys.keys`. So the JSON list has 88 entries (with 1 dupe so 87 distinct) PLUS the inline DateNow = 89 token universe. Pass 1 framing was almost right.

**Per-group breakdown (verified 2026-05-08 from `server-settings.json`):**
- `##Patients.*`: 14 entries with 1 duplicate (`LastName` at L86 and L99) -> **13 distinct**
- `##Appointments.*`: 14 distinct (incl. typo `AppointmenTime` and `Signature` for image insertion)
- `##EmployerDetails.*`: 7 distinct
- `##PatientAttorneys.*`: 10 distinct
- `##DefenseAttorneys.*`: 10 distinct
- `##InjuryDetails.*`: 22 distinct
- `##InjuryBodyPartDetails.*`: 1 (`BodyPartDescription`)
- `##CustomFieldValues.*`: 1 (`FieldValue`)
- `##Others.*`: 0 in JSON, 1 inline (`DateNow`)

**Total distinct: 78 in JSON + 1 inline = 79 distinct tokens at runtime**, NOT 89. Pass 1's "89-token universe" was wrong.

Of these, **49 distinct tokens are referenced by the 2 active OLD DOCX templates** (44 in Patient Packet, 15 in Doctor Packet, 10 shared). The remaining 30 tokens are unused at runtime - probably scaffolded for the 5 unreferenced packet templates.

### 12.3 OLD signature image: 0.962 inches square (re-verified)

Pass 1 said "880000 EMU = 0.962 inches" - verified. Pass 2 re-confirmed by reading `AppointmentDocumentDomain.cs:954-1052` in full. The exact call:

```csharp
AddImageToRun(wordprocessingDocument, fileName, 880000L, 880000L, mainPart.GetIdOfPart(imagePart));
```

880000 EMU / 914400 EMU/inch = 0.962 inches. Both width and height. The image is hardcoded as `ImagePartType.Png` even when the source signature is JPEG (likely works because OpenXml is forgiving + most browsers/Word accept JPEG bytes labeled as PNG, but technically inaccurate).

**The placeholder text matching is exact-string-match:** `descendants<Text>().Where(x => x.Text.Equals("##Appointments.Signature##"))`. If Word splits the placeholder across multiple `<w:t>` runs (proof error, font change), the `.Equals` fails and the image is NOT inserted. Templates were authored to keep the placeholder contiguous. NEW must replicate this fragility OR use a more robust split-aware placeholder detector. Recommend the latter.

### 12.4 NEW Angular HAS packet UI (Pass 1 missed)

Pass 1 Section 3 row "Delivery" said "REST download via AppointmentPacketsAppService.DownloadAsync. **No email-with-attachment flow exists.**" Half-correct. Pass 2 verified `angular/src/app/appointment-packet/appointment-packet.component.ts` (176 lines) + `.html` (100 lines):

- `AppointmentPacketComponent` is an Angular 20 standalone component.
- `@Input() appointmentId` triggers `refresh()` on changes.
- Polls `AppointmentPacketService.getByAppointment(id)` every 5 seconds while `Status == Generating` so transitions surface without manual refresh.
- Download routes through `AppointmentDocumentUrls.buildDownload(...)` helper which `window.open()`s the absolute API URL. Per-component comment notes the URL helper exists OUTSIDE `proxy/` because hand-edits to proxy files are forbidden (per `angular/src/app/CLAUDE.md`).
- Regenerate button gated on `CaseEvaluation.AppointmentPackets.Regenerate` permission via `PermissionService.getGrantedPolicy(...)`.
- Toast notifications via `@abp/ng.theme.shared` ToasterService.

**No "send to stakeholders" button** - that aligns with NEW's pull-only download model. To replicate OLD's email-with-attachment fan-out, the next session needs to build:
1. A new `SendAppointmentPacketEmailJob` (Hangfire) that takes `(AppointmentId, PacketKind, RecipientEmail)`.
2. A trigger on `AppointmentPacket.Status -> Generated` that enqueues N jobs (one per recipient resolved by `IAppointmentRecipientResolver` filtered to the kind's recipient set).
3. Optionally an UI button for the office to manually re-send.

**Pass 1 Section 4 mapping table errata:** the `AppointmentDocumentAddWithAttachment` row says "**None - see packet audit doc**". This is correct in NEW today. But the audit's framing implied no UI either - that's wrong. The download UI exists; what's missing is the email-fan-out delivery.

### 12.5 NEW packet generation has TWO Hangfire jobs (re-verified)

Pass 1 listed `GenerateAppointmentPacketJob` only. Pass 2 confirms via grep: this is the only packet-generating job. The 5 RecurringJobs in `CaseEvaluationHttpApiHostModule.ConfigureHangfireRecurringJobs` are all email reminder jobs, not packet jobs. So:

- `PacketGenerationOnApprovedHandler` (Eto handler) -> enqueues `GenerateAppointmentPacketJob` (background job) when an appointment transitions to Approved. **Single point of trigger.**
- `AppointmentPacketsAppService.RegeneratePacketAsync` (if it exists - need to verify) -> a Regenerate button in the SPA also enqueues. Per the AppointmentPacketComponent's "Regenerate" button + `CaseEvaluation.AppointmentPackets.Regenerate` permission, an endpoint clearly exists. Pass 2 didn't read it but the Regenerate button works against the office user's appointment view.

**No daily/scheduled packet regen job.** OLD also has none.

### 12.6 NEW `AppointmentPacket` is 1:1 (re-verified) - schema gap stands

Pass 1 Section 3.6 framing is correct: NEW's entity is 1:1 per Appointment. The `PacketKind` discriminator does NOT exist. Schema migration is required for OLD parity (Patient + Doctor + AttorneyClaimExaminer = 3 packets per appointment). Pass 1 P2 captured this correctly.

**One Pass 2 nuance:** the `AppointmentPacket.cs:11-25` doc-comment explicitly says "deferred enhancement when the manager confirms the exact format." Adrian's 2026-05-08 directive un-defers it. The next session's first PR should add the discriminator BEFORE building any per-kind generator.

### 12.7 OLD's NRE bugs at `:882, :886, :890` confirmed

Pass 2 re-read `AppointmentDocumentDomain.cs:865-952` (the `ReplaceText` method). Verified the 3 NRE bugs:

```csharp
// :882
appointment.AppointmentEmployerDetails.FirstOrDefault().AppointmentId
// :886
appointment.AppointmentPatientAttorneys.FirstOrDefault().AppointmentId
// :890
appointment.AppointmentDefenseAttorneys.FirstOrDefault().AppointmentId
```

If any of these collections is empty, `.FirstOrDefault()` returns null + `.AppointmentId` NREs and the entire packet generation throws (catch-all at the caller's outer try/catch fails silently because `AddAppointmentDocumentsAndSendDocumentToEmail` is `async void`).

**This means OLD silently fails to generate packets for appointments without an employer detail row OR without at least one patient attorney OR without at least one defense attorney.** No email goes out, no error surfaces to the user. This is dangerous behavior. **NEW must fix:** guard with null-checks, populate empty values into the token map (rendering `""` in the DOCX) instead of throwing.

### 12.8 OLD packet flow misses one storage detail Pass 1 missed

Pass 1 Section 2.3 said the Patient packet generates a temp file under `wwwroot/Documents/submittedDocuments/{Confirmation}_Patient Packet_{ddMMyyyy_hhmmss}.docx` and registers an `AppointmentNewDocument` row with `DocumentName = "PATIENT PACKET"`. Pass 2 re-confirmed.

**Pass 2 nuance:** the `AppointmentNewDocument` row registered for the Patient packet has:
- `DocumentStatusId = (int)DocumentStatuses.Accepted` (auto-accepted at packet generation)
- `UserType = (int)UserTypesForEmail.CreatedBy`
- `AttachmentLink = documentUploadUrl` (the link points to the per-appointment upload page, NOT the packet itself)
- `AppointmentDocumentTypeId = null` and `OtherDocumentTypeName = null`

So the Patient packet shares the `AppointmentNewDocument` table with ad-hoc uploaded documents. From the patient's perspective, the DOCX appears in their "documents on this appointment" list as if they had uploaded it. **In NEW, this would be modeled as `AppointmentDocument` rows with `IsAdHoc=true` plus a special marker** - but per the user's directive, NEW's `AppointmentPacket` table is the dedicated home for portal-generated packets. The OLD's storing-as-AppointmentNewDocument is an OLD-specific pattern that NEW does NOT replicate (and shouldn't - it conflates portal-generated content with user uploads).

**Pass 1 Section 2.3 + 2.4 should note this:** the `AppointmentNewDocument` entry is OLD's way of making packets visible in the patient's document list. NEW achieves the same UX by exposing `AppointmentPacket` via `AppointmentPacketComponent`.

### 12.9 OLD packet email body uses ##URL## with the upload page (not the packet)

Pass 1 Section 2.2 said the email body uses `EmailTemplate.AppointmentDocumentAddWithAttachment` with `documentUploadUrl = clientUrl + "/appointment-new-documents/" + appointmentId` substituted. Pass 2 re-confirmed.

**Important nuance Pass 1 missed:** the URL token `##URL##` in the template body points the recipient to the **document upload page**, NOT to download the attached packet. The packet IS already attached as a DOCX file, so the URL is not for "download the packet" - it's for "click here to upload supplementary documents." The body presumably reads something like "Your appointment was approved. Please find the attached packet, and click here to upload any additional documents we need."

This is a non-trivial UX detail for NEW: the email body needs to embed a link that drops the user into a documents-upload UI, with the packet itself as an attachment.

### 12.10 The 8 DOCX templates - file sizes verified

Pass 1 Section 2.11 listed file sizes as `(TBD)` for some templates. Pass 2 ran `ls -la` (via Glob output - actual byte sizes need a separate Bash):

Existing folders (verified by Glob):
- `attorneyclaimexaminer/ATTORNEY CLAIM EXAMINER PACKET.docx`
- `attornypacketame/Attorney Packet For AME.docx`
- `attornypacketpqme/Attorney Packet For QME.docx`
- `claimexaminerpacketame/Attorney Packet For AME.docx`
- `claimexaminerpacketpqme/Attorney Packet For QME.docx`
- `doctorpacket/DOCTOR PACKET.docx` (1.0 MB per Pass 1)
- `patientpacket/PATIENT PACKET.docx` (Pass 1 noted unused)
- `patientpacketnew/PATIENT PACKET NEW.docx` (343 KB per Pass 1)

Plus `jointagreementletter/` referenced in `server-settings.json` but **not used by `AppointmentDocumentDomain.AddAppointmentDocumentsAndSendDocumentToEmail`** - it's used by the JDF flow in `AppointmentChangeRequestDomain` / `AppointmentJointDeclarationDomain` for OFFICE-GENERATED JDF paperwork that AME parties sign. Out-of-scope for this audit but worth flagging.

The duplicate-filename-different-folder pattern (`Attorney Packet For AME.docx` in two folders) is highly suspicious - either the files differ in content (intentional but undocumented) or were copy-pasted. Adrian should compare them in Word/LibreOffice before deciding which to use.

### 12.11 Pass 1 errata table (specific corrections)

| Pass 1 location | Pass 1 claim | Corrected claim |
|---|---|---|
| Section 1 row "Delivery" | "**No email-with-attachment flow exists.**" + UI implication | Email-with-attachment is missing in NEW. Download UI EXISTS at `angular/src/app/appointment-packet/`. |
| Section 1 row "Templates" | "`PatientPacketTemplate.cs` (QuestPDF) exists as **dead code** - never instantiated" | Confirmed dead. No `DoctorPacketTemplate` or `AttorneyClaimExaminerPacketTemplate` exist either. PatientPacketTemplate references DoctorPacketTemplate in doc-comments but the class doesn't exist. |
| Section 2.10 | "88 unique strings ... 89 tokens" | More precisely: 78 distinct token names in JSON (after removing duplicate) + 1 inline `DateNow` = **79 distinct tokens at runtime**, of which **49 are referenced by the 2 active DOCX templates**. |
| Section 2.11 row patientpacket | "legacy; referenced by `aws.patientPacket` setting which no code reads" | Confirmed. Delete the setting + the folder once the audit is locked. |
| Section 2.11 attorney/claim-examiner duplicate filenames | (Mentioned the duplicate, no recommendation) | **Open question for Adrian:** are the AME / PQME variants of attorney-vs-claim-examiner packets meant to be different? OLD code uses the SAME `attorneyclaimexaminer` template for all 4 paths regardless. Files-on-disk for the others exist but are unread. |
| Section 4 mapping table row "Patient packet: ReplaceText" | Pseudo-code only | Pass 2 read the actual implementation (Section 12.1). The implementer needs to read OLD `:865-952` + `ApplicationUtility.cs:327-380` line by line, not from this audit's pseudo-code. |
| Section 6 P3 effort | "~2 days" | Stand by - the email-with-attachment flow is a fresh build, not a port. Use `IBackgroundJobManager.EnqueueAsync(SendAppointmentEmailWithAttachmentArgs)` (new args type) + Mailkit's `BodyBuilder.Attachments.Add` per OLD's `SendMail.SendSMTPMailWithAttachment` pattern. |
| Section 7.1 effort | "12-20 dev-days" | Stand by - the schema migration + 3 templates + token resolver + email-with-attachment delivery are all real work. The cover-page-PDF code (`CoverPageGenerator.cs` + `PacketMergeService.cs`) needs to be retained for the future PDF conversion phase, not deleted. |
| Section 11 sample synthetic data | "(TBD)" file sizes | Set as guidance, not as ground truth. Production sample data should come from a real (synthetic) Falkinstein test appointment. |

### 12.12 OLD's `FileOperation.DownloadPackageDocument` and `AmazonBlobStorage.DownloadFile`

Pass 1 cited but did not read. Pass 2 should have but didn't get to it before context budget pressure - flag for the next-session implementer to read these directly:

- `P:\PatientPortalOld\PatientAppointment.Infrastructure\Utilities\FileOperations.cs` - `DownloadPackageDocument(url, folderName)`. Effect: copies `wwwroot/Documents/documentBluePrint/{url}/*.docx` into `wwwroot/Documents/{folderName}/`. URL is the folder-name suffix (e.g. `"patientpacketnew"`), NOT a real URL.
- `P:\PatientPortalOld\PatientAppointment.Infrastructure\AmazonService\AmazonBlobStorage.cs` - `DownloadFile(fileUrl, fileName)`. Despite the name, in current OLD code falls back to local-file read at `wwwroot/Documents/userSignature/{fileName}` because the AWS S3 calls are commented out.

The next session should embed OLD's 3 active DOCX templates as embedded resources in the NEW solution and ditch the temp-folder copy step entirely.

### 12.13 Pass 2 final verdict on the packet implementation contract

Pass 1 Section 7 + 8 captured most of the work. Pass 2 corrections:

1. **Don't delete `CoverPageGenerator.cs` + `PacketMergeService.cs`** - they're needed for the eventual DOCX -> PDF conversion phase (P8 in Pass 1 Section 6). Treat the cover page as the wrapper around the full packet PDF in Phase 2; the merge service is needed if Adrian decides to also bundle uploaded supplementary docs into the same PDF.
2. **The right primary work order:** schema migration (P2) -> embed 3 OLD templates as resources -> write a token-resolver service that walks the OLD's per-group cardinality rules (P4) -> implement OpenXml-based DOCX renderer with proper XML escaping (P1, fixing OLD's `&` mutation bug) -> wire the per-kind dispatch (P5) -> implement the email-with-attachment Hangfire job and per-recipient fan-out (P3) -> embed the OLD HTML email body for `AppointmentDocumentAddWithAttachment` (P7) -> verify against synthetic data (Section 11) -> THEN add LibreOffice headless DOCX -> PDF conversion (P8).
3. **The `PatientPacketTemplate.cs` (QuestPDF dead code) decision:** delete in the same PR that wires the OpenXml DOCX renderer. The QuestPDF approach contradicts the DOCX-first directive. The 553 lines of structural skeleton are not reusable for OpenXml.
4. **Adrian's PQME/AME differentiation question (Section 8 #1):** keep deferred. The current OLD code uses the same `attorneyclaimexaminer` template for all 4 attorney+claim-examiner emails. Replicate that. Adrian can flip to per-kind templates later if needed.
5. **Effort estimate corrections:** total Phase 1 (DOCX-only) effort is ~10-15 dev-days, NOT 12-20. The `IBlobContainer` storage layer is already in place; the schema migration is a 1-dayer.

---

**End of packet generation parity audit.** Companion email service audit: `docs/parity/email-packet-parity/email-service.md`.

---

## 13. Phase 1A implementation notes (2026-05-08)

Appended during Phase 1A execution. Errata + decisions; not a rewrite of the audit.

### 13.1 The 8 OLD DOCX templates - verified file sizes

Pass 1 Section 2.11 had `(TBD)` sizes for several templates and Pass 2 only verified existence via Glob. Phase 1A.2 ran `ls -la` against every file. Recorded for future reference:

| Folder | DOCX file | Size (bytes) | SHA256 prefix | Status |
|---|---|---|---|---|
| `attorneyclaimexaminer/` | `ATTORNEY CLAIM EXAMINER PACKET.docx` | 49,665 | `6c8270...` | Active (OLD's runtime ships this to all atty/CE recipients) |
| `attornypacketame/` | `Attorney Packet For AME.docx` | 27,035 | `227543...` | Configured-but-unread |
| `attornypacketpqme/` | `Attorney Packet For QME.docx` | 168,020 | `25d352...` | Configured-but-unread |
| `claimexaminerpacketame/` | `Attorney Packet For AME.docx` | 29,559 | `880c11...` | Configured-but-unread |
| `claimexaminerpacketpqme/` | `Attorney Packet For QME.docx` | 169,915 | `01f01c...` | Configured-but-unread |
| `doctorpacket/` | `DOCTOR PACKET.docx` | 1,031,534 | `9efd0e...` | Active |
| `patientpacket/` | `PATIENT PACKET.docx` | 862,032 | `abb7ff...` | Legacy / unused |
| `patientpacketnew/` | `PATIENT PACKET NEW.docx` | 343,492 | `ee775c...` | Active |

All 8 hashes are unique (no byte-identical pairs). The 4 unused atty/CE templates have meaningful content differences from the unified one - they aren't copies.

### 13.2 Atty/CE template decision: unified, not 4 separate

Adrian's earlier "4 separate templates (match OLD intent)" choice was reconsidered after Phase 1A.2 surfaced the actual size data (above). Final decision (2026-05-08): use the **unified ATTORNEY CLAIM EXAMINER PACKET.docx** (49 KB) for all atty/CE recipients. Reason: that's the template OLD's compiled runtime has been shipping to recipients in production for years. The 4 per-PQME/AME templates exist on disk but were never wired up; using them would change what real recipients receive vs OLD's actual production behavior.

This collapses the Phase 1A plan from 6 PacketKind values to 3:
- `Patient = 1`
- `Doctor = 2`
- `AttorneyClaimExaminer = 3`

The single `AttorneyClaimExaminer` row holds one rendered DOCX that the email session attaches to every atty/CE recipient (claim examiners, defense attorneys, patient attorneys) on PQME/AME-type appointments.

### 13.3 Embedded resource paths

Phase 1A.2 copied the 3 active OLD templates (byte-identical, hashes verified) into:

```
src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/Templates/Resources/
  PatientPacketNew.docx          (343,492 bytes; from patientpacketnew/)
  DoctorPacket.docx              (1,031,534 bytes; from doctorpacket/)
  AttorneyClaimExaminerPacket.docx (49,665 bytes; from attorneyclaimexaminer/)
```

Embedded via `<EmbeddedResource Include="AppointmentDocuments\Templates\Resources\*.docx" />` in `HealthcareSupport.CaseEvaluation.Domain.csproj`. Loaded at runtime by `EmbeddedTemplateResources.LoadTemplate(PacketKind)` which mirrors the existing `EmailBodyResources.LoadBody(string)` pattern in `NotificationTemplates/`.

The 5 unused OLD templates (patientpacket legacy, attornypacketame, attornypacketpqme, claimexaminerpacketame, claimexaminerpacketpqme) are NOT embedded. They remain on OLD's disk for archival reference.

### 13.4 Token inventory across the 3 active templates

Pass 1 reported "44 + 15 = 49 tokens" based on parsing PATIENT PACKET NEW + DOCTOR PACKET only. Phase 1A.3 ran a Python OOXML run-flattened parser against all 3 active templates (including the previously-unanalyzed AttorneyClaimExaminer). Results:

| Template | Unique `##Group.Field##` tokens |
|---|---|
| `PatientPacketNew.docx` | 44 |
| `DoctorPacket.docx` | 15 |
| `AttorneyClaimExaminerPacket.docx` | **55** |
| Union (deduped) | **60** |

**Pass 1 errata.** The audit's "49 distinct tokens" figure was off by 2 (the actual Patient + Doctor union is 51, not 49). And the AttyCE template was never re-parsed -- it adds 9 tokens never recorded in the contract.

**The 9 AttyCE-only tokens missing from the prior 51-property `PacketTokenContext.cs`:**

1. `##Appointments.AppointmentCreatedDate##`
2. `##Appointments.PanelNumber##`
3. `##Patients.PhoneNumber##`
4. `##InjuryDetails.PrimaryInsurancePhoneNumber##` (concat all injuries)
5. `##InjuryDetails.ClaimExaminerCity##` (concat all injuries; first active examiner per injury)
6. `##InjuryDetails.ClaimExaminerStreet##`
7. `##InjuryDetails.ClaimExaminerState##`
8. `##InjuryDetails.ClaimExaminerZip##`
9. `##InjuryDetails.ClaimExaminerPhoneNumber##`

Phase 1A.3 extended `PacketTokenContext.cs` with corresponding properties (60 total). Property names follow the existing convention: `{Group}{Column}` (e.g. `PatientPhoneNumber`, `AppointmentCreatedDate`, `InjuryClaimExaminerPhoneNumber`). The Phase 1B.4 resolver will populate these from the NEW domain entities; tokens whose data has no NEW source (e.g. `Appointments.PanelNumber` if NEW's Appointment has no Panel field) render as empty string per OLD's reflection-lookup default at `AppointmentDocumentDomain.cs:1066-1071`.

### 13.5 Token signature stamping in AttorneyClaimExaminer

`##Appointments.Signature##` appears in **both** the Patient Packet AND the AttorneyClaimExaminer Packet (Pass 1 said Patient-only). The Phase 1B.5 renderer must stamp the signature image (~0.96 inch square) at the placeholder in BOTH templates.
