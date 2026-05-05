# Prompt: PDF packet generation (Patient Packet + Doctor Packet) — strict OLD parity

Run this prompt in a fresh Claude Code session. Branch:
`feat/replicate-old-app-track-domain` worktree at
`W:\patient-portal\replicate-old-app`.

---

## Mission

Replicate OLD's two-packet generation feature (Patient Packet + Doctor
Packet) on the NEW stack as **PDF** files (the only format change versus
OLD; everything else must match exactly). Both packets are generated
automatically when an internal user approves an appointment. The Patient
Packet is emailed to the patient as an attachment; the Doctor Packet is
stored on the appointment for the office to retrieve.

Output is one PDF per packet, layout pixel-identical to OLD's DOCX
templates, every static label preserved verbatim, every token-driven
field populated from NEW's appointment data.

These are legal medical documents. Errors propagate to compliance and
legal exposure. **Treat every label, checkbox, and form field as
load-bearing.** No "simplification" passes. No "good enough" sections.
If the OLD template has a checkbox grid for sensory perception, NEW's
PDF has the same grid in the same order with the same labels.

---

## Why this matters

**Why the layout must be exact:** Patients and doctors fill in the
non-token fields by hand during the appointment. The packet is a clinical
intake form — questions are sequenced, grouped, and labeled the way the
provider expects. Reordering or relabeling sections is a usability
regression and a documentation-integrity risk.

**Why we must keep every token:** Token-replaced fields carry case
metadata (patient identity, claim number, attorney info, employer info,
custom fields). Missing tokens mean the doctor receives a packet without
knowing whose case it is — or worse, with another patient's data on the
wrong sheet. Every `##Group.Field##` in the OLD DOCX must resolve to
real data from NEW's appointment row.

**Why PDF (not DOCX):** Adrian's directive (2026-05-05). PDFs are
immutable; recipients cannot accidentally edit or save with corruption.

**Why a separate session:** This is multi-day work. Layout authoring +
token mapping + email handler + storage entity + smoke test. Doing it
in a session with other tracks dilutes attention and risks shortcuts.
This prompt isolates the work.

---

## Decisions already locked (do not re-litigate)

1. **Two distinct packets per appointment** — Patient Packet (intake form
   the patient fills before the visit, then emailed to them) + Doctor
   Packet (clinical-exam form the doctor fills during the visit, stored
   for the office). Match OLD's split exactly. Adrian-confirmed
   2026-05-05.
2. **Output format = PDF.** Library = **QuestPDF** (community license is
   free for our use; fluent C# API; renders dynamic-length sections
   cleanly). No PuppeteerSharp, no headless Chrome, no DOCX-to-PDF
   conversion at runtime. Adrian-confirmed 2026-05-05.
3. **Layout fidelity = exact.** Authored to look like OLD when both are
   placed side by side. Page size, margins, section order, label text,
   checkbox grids, field underlines, font weight all match. Adrian-
   confirmed 2026-05-05 ("This is a legal medical document. It has to be
   exactly same.").
4. **Token coverage = complete.** Every `##Group.Field##` in OLD's DOCX
   templates resolves in the NEW PDFs. Adrian-confirmed 2026-05-05
   ("don't misplace or forget any TOKENS to be replaced").
5. **Non-token fields stay blank** — the patient and doctor write into
   them by hand at the visit. Do NOT pre-fill anything OLD doesn't
   pre-fill. Adrian-confirmed 2026-05-05.
6. **Signature image** — OLD looks up `User.SignatureAWSFilePath` per
   responsible user and stamps the image at the `Signature` placeholder.
   No seed signature file is committed in either OLD or NEW. NEW's
   `IdentityUser` has no signature column. **For Phase 1A demo**: render
   the responsible user's name in a script font as a placeholder where
   OLD stamps the image, with a `[Signature on file]` watermark below.
   File a follow-up `_parity-flags.md` row noting the image-vs-text
   deviation. Adding a signature column to IdentityUser is a separate
   ticket.
7. **Trigger** = `AppointmentStatusChangedEto` with `ToStatus = Approved`.
   The existing `PacketGenerationOnApprovedHandler` is the right
   subscriber pattern; rewrite the JOB it enqueues, not the handler.
8. **Email** = Patient Packet only, sent to the patient via the existing
   notification infrastructure with the PDF attached. Doctor Packet is
   stored only (mirrors OLD).
9. **Storage** = both packets stored as appointment-scoped blobs that
   internal staff can list and download. Schema decisions are open
   (replace `AppointmentPacket` 1:1 with N:1 keyed by `PacketKind`, OR
   add `PacketKind` discriminator on existing entity, OR introduce
   `AppointmentDocument` rows of a special kind). The future agent
   chooses, justifies, and stops for Adrian review BEFORE migrating.

---

## Anti-patterns — do not do any of these

- Author a "simplified" or "data-summary" PDF that does not match OLD's
  visual layout. The user explicitly rejected this.
- Skip a token because mapping it to a NEW entity is awkward. Every
  token in OLD's DOCX gets resolved; if a mapping is unclear, **stop
  and surface the question**.
- Inline the entire OLD DOCX content as text in the C# templates. Use
  the templates as the layout reference; render in QuestPDF using its
  declarative API; do not paste hundreds of lines of form text into
  string literals when you can structure them as `Column`/`Row`
  /`Table` blocks.
- Re-use the existing `CoverPageGenerator` or `PacketMergeService`
  unchanged. The current "merge uploaded docs into one packet" feature
  is a different concept — **the new packet generation is templated
  forms, not merged uploads**. Keep both concepts cleanly separated.
- Generate both packets serially in one DB transaction. Each packet is
  one job invocation; if Patient Packet fails, Doctor Packet should
  still succeed (or vice versa). Use one job per packet.
- Email the Doctor Packet. OLD doesn't.
- Pre-fill non-token fields. Patient and doctor fill them by hand.
- Hand-edit `angular/src/app/proxy/`. Regenerate via `abp generate-proxy`
  after the backend lands.
- Skip ASCII-only review. Match `~/.claude/rules/code-standards.md`:
  no smart quotes, no em dashes, no Unicode decoration in source code.

---

## Phases (with stop-points)

Each phase ends in a Stop point. The agent surfaces the diff + a brief
recap to Adrian before starting the next phase. This is intentional --
this is a long, complex task and Adrian needs review gates.

### Phase 0 — Audit + plan presentation (NO code)

1. Read OLD source citations (next section). Open the three DOCX
   templates and capture the visible structure section by section. Use
   `unzip -p path/to.docx word/document.xml` to extract the OOXML, then
   describe the structure to Adrian as a per-template Markdown outline:
     - Section title
     - Number of sub-sections / sub-headings
     - Notable form-field layouts (checkbox grids, signature line
       placements, table grids)
     - Token positions (sentence-by-sentence is fine; specifics matter)
2. Verify the token surface is exactly the union of:
     - `Patient Packet`: tokens listed in this prompt's `Locked tokens`
       section.
     - `Doctor Packet`: same list.
   Run `grep -oE '##[A-Za-z]+\\.[A-Za-z]+##'` over the extracted XML for
   each DOCX and diff against the locked list. Surface ANY new tokens
   the agent finds.
3. Map each token to NEW's entity model. NEW does not have OLD's
   `vPatient` / `vAppointmentDetail` etc. database views; the resolver
   queries entities directly. Build a Markdown table with three columns
   per token: OLD source view, NEW entity field, transform (formatting,
   nullable handling). Tokens that cannot be resolved against current
   NEW entities are flagged for Adrian decision (`MISSING-IN-NEW`).
4. Choose schema approach for storing two packets per appointment.
   Justify the choice in 5 sentences max.
5. **Stop point.** Present (1)-(4) to Adrian as a single Markdown
   document at `docs/parity/packet-generation-audit.md`. Wait for
   approval. Do not write production code until Adrian approves the
   plan.

### Phase 1 — Add QuestPDF + author Patient Packet template

1. Add `QuestPDF` (community license) to
   `src/HealthcareSupport.CaseEvaluation.Domain/HealthcareSupport.CaseEvaluation.Domain.csproj`.
   Configure the license at app startup in
   `CaseEvaluationDomainModule.ConfigureServices` per QuestPDF's
   `Settings.License = LicenseType.Community;`.
2. Author `PatientPacketTemplate.cs` under
   `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/Templates/`.
   Implement `IDocument` with QuestPDF's fluent API. Replicate
   `PATIENT PACKET NEW.docx` page by page, section by section. Tokens
   are passed via a context object. Static labels, checkbox grids, and
   form-field underlines are coded directly.
3. Build a `PacketTokenContext` POCO that carries every resolved token
   value. Mark each property with the OLD token name in an XML doc
   comment so the renderer-author can grep it.
4. Spot-check rendered output by saving a sample PDF for an in-memory
   `PacketTokenContext` populated with synthetic data (HIPAA -- per
   `~/.claude/rules/hipaa-data.md`, never real patient PHI). Save the
   sample to `docs/parity/samples/patient-packet-sample.pdf` for Adrian
   review.
5. **Stop point.** Adrian opens the sample PDF and the rendered DOCX
   side by side, signs off on visual fidelity. Iterate if Adrian flags
   layout deltas. Do not start Doctor Packet template until Adrian
   approves the Patient Packet visual match.

### Phase 2 — Author Doctor Packet template

Same flow as Phase 1, applied to `DOCTOR PACKET.docx`. Sample saved to
`docs/parity/samples/doctor-packet-sample.pdf`.

**Stop point.** Adrian visual sign-off.

### Phase 3 — Token resolver (data mapping)

1. Build `PacketTokenResolver` under
   `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/Templates/`.
   Single class, one method per OLD entity group:
     - `ResolvePatient(Patient patient)` -> 11 tokens
     - `ResolveAppointment(Appointment appointment, AppointmentType type, Location location, IdentityUser? responsibleUser)` -> 8 tokens
     - `ResolveEmployerDetails(AppointmentEmployerDetail? row)` -> 5 tokens
     - `ResolveApplicantAttorneys(IEnumerable<AppointmentApplicantAttorneyView>)` -> 6 tokens (NB: OLD's `PatientAttorneys`)
     - `ResolveDefenseAttorneys(IEnumerable<AppointmentDefenseAttorneyView>)` -> 5 tokens
     - `ResolveInjuryDetails(IEnumerable<AppointmentInjuryDetailView>)` -> N tokens (loop the first injury per OLD; clarify with Adrian on multi-injury appointments)
     - `ResolveInjuryBodyPartDetails(IEnumerable<AppointmentInjuryBodyPartDetailView>)` -> N tokens
     - `ResolveCustomFieldValues(IEnumerable<CustomFieldValueView>)` -> dynamic
     - `ResolveOthers()` -> 1 token (`DateNow`)
2. Each method returns a populated portion of `PacketTokenContext`. The
   entry point `ResolveForAppointment(appointmentId)` calls each in
   sequence and returns one `PacketTokenContext`.
3. Nullable handling: missing entity rows yield empty strings (OLD's
   `String.IsNullOrEmpty(...) ? "" : value` pattern). Never throw on
   a missing optional row.
4. Date formatting: dates render `MM/dd/yyyy`, times render `hh:mm tt`
   (matches OLD). UTC -> Pacific Time conversion is **the AppService's
   responsibility**, not the resolver's; the resolver formats whatever
   `DateTime` is passed in.
5. Unit tests at `test/...Domain.Tests/AppointmentDocuments/PacketTokenResolverUnitTests.cs`:
   one fact per OLD entity group, asserting:
     - All locked tokens are populated.
     - Synthetic values flow through (e.g. patient first name "Jane"
       arrives as "Jane" in `Patients.FirstName` token slot).
     - Nullable scenarios produce empty string, not `null`, and never throw.

**Stop point.** Adrian reviews the resolver, the test list, and the
locked-token coverage table. Approve before wiring into the job.

### Phase 4 — Storage entity + manager

1. Decide schema (per Phase 0 step 4 -- already approved). Implement
   the chosen path:
     - **If two-row keyed by PacketKind**: extend `AppointmentPacket`
       with `PacketKind` enum (Patient / Doctor) + composite uniqueness
       on `(AppointmentId, PacketKind)`. EF migration.
     - **If `AppointmentDocument` rows**: add `PacketKind` discriminator
       on `AppointmentDocument`, write `IsPacket = true`. EF migration.
     - **If new `AppointmentPacketRecipient` table**: 1:N from
       `AppointmentPacket`. EF migration.
2. Update `AppointmentPacketManager` accordingly:
     - `EnsureGeneratingAsync(tenantId, appointmentId, packetKind, blobName)`
     - `MarkGeneratedAsync(packetId, blobName)`
     - `MarkFailedAsync(packetId, errorMessage)`
3. Filename convention OLD-verbatim:
   `{ConfNum}_{Patient|Doctor} Packet_{ddMMyyyy_hhmmss}.pdf`. Apply at
   download time (AppService layer); blob storage uses GUID paths
   (existing convention).

**Stop point.** Adrian reviews the migration + the EF Core warnings
output. Run `dotnet ef migrations add ...` and confirm the migration
diff is what you expect before applying.

### Phase 5 — Generation job (rewrite)

1. Replace `GenerateAppointmentPacketJob.ExecuteAsync` body:
     - Load the appointment + supporting entities.
     - Run `PacketTokenResolver.ResolveForAppointmentAsync(appointmentId)`
       (note: methods become `async Task<X>` for repository fetches).
     - Render Patient Packet PDF in-memory, save to blob, write packet
       row.
     - Render Doctor Packet PDF in-memory, save to blob, write packet
       row.
     - Each packet's failure is independent: catch QuestPDF / IO
       exceptions per-packet and call `MarkFailedAsync`. Other packet
       still proceeds.
2. Preserve the existing job-args shape (`GenerateAppointmentPacketArgs`)
   so the handler doesn't change.
3. **Decommission the old "merge uploaded documents" path entirely.** It
   was a placeholder for the packet feature and now that we have the
   real packet feature, the merge path becomes dead code. Remove
   `CoverPageGenerator.cs` + `PacketMergeService.cs` + their references.
4. **Stop point.** Adrian reviews the job rewrite + the deletions.

### Phase 6 — Email integration (Patient Packet only)

1. After Patient Packet generation succeeds, publish a new
   `PatientPacketGeneratedEto`:
     - `AppointmentId`, `TenantId`, `PacketBlobName`, `PatientEmail`,
       `OccurredAt`.
2. New handler under
   `src/HealthcareSupport.CaseEvaluation.Domain/Notifications/Handlers/`:
   `PatientPacketEmailHandler` listening to that event. Pulls the
   blob, attaches to the email, sends via the existing email sender
   abstraction.
3. Subject format OLD-verbatim:
   `Appointment Request Approved (Patient: {first} {last} - Claim: {claim} - ADJ: {adj})`.
   When `claim` or `adj` is empty, drop the corresponding " - " segment
   (matches OLD's `String.IsNullOrEmpty` pattern at line 455).
4. Body uses the existing `AppointmentDocumentAddWithAttachment`
   notification template (or the closest NEW equivalent in
   `NotificationTemplate` master data); confirm the template exists
   before wiring -- if missing, surface to Adrian to add it as
   master-data.
5. Smoke-test the attachment via the local SMTP sandbox container
   (Mailtrap / MailHog). Save a screenshot of the captured email +
   attachment to `docs/parity/samples/patient-packet-email.png`.
6. **Stop point.** Adrian reviews captured email + attachment.

### Phase 7 — Frontend surface (read-side only)

1. Regenerate the Angular proxy after Phase 4 backend ships.
2. In `appointment-view.component.ts`, render two new rows under the
   appointment's documents tab labeled "Patient Packet" and "Doctor
   Packet" with download links. Format the download filename OLD-style.
3. Permission gate: external users see only the Patient Packet row;
   internal users (admin / Clinic Staff / Staff Supervisor / IT Admin /
   Doctor) see both. Mirrors OLD's UserType filter on
   `AppointmentNewDocument` rows.
4. **Stop point.** Adrian sanity-checks the appointment-view UI in the
   browser.

### Phase 8 — End-to-end verification

1. Bring the stack up clean (`docker compose down -v && docker compose
   up -d --build`).
2. Run the full demo path: register external user -> log in -> book ->
   admin login -> approve.
3. Verify:
   - Patient Packet PDF appears under appointment documents.
   - Doctor Packet PDF appears under appointment documents (admin only).
   - Patient receives email with Patient Packet attached.
   - Token-driven fields in the rendered PDFs match the booked data.
   - All form fields the patient/doctor fill by hand are blank.
4. Save the smoke-test PDFs to `docs/parity/samples/` for the record.
5. Final commit + PR to main. PR title: `feat(packets): replicate OLD
   Patient + Doctor packets as PDFs`. PR body lists every locked
   decision, the visual fidelity samples, the token mapping table, and
   any deferred follow-ups.

---

## Inputs (read these first; do not paste their content into source)

### OLD source — code

| Path | Lines | Role |
|---|---|---|
| `P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDocumentDomain.cs` | 394-630 | The `AddAppointmentDocumentsAndSendDocumentToEmail` orchestrator: downloads templates, replaces tokens, inserts signature, emails patient, stores both packets |
| same file | 865-952 | `ReplaceText(appointment, filePath)`: the token-replacement engine. Maps OLD entity views to `##Token##` placeholders. The 8 entity groups + `Others` are listed inline. |
| same file | 954+ | `InsertAPicture`: stamps the responsible user's signature image at the named placeholder. NEW will not stamp an image (no signature column); render the user's name in a script font instead. |
| `P:\PatientPortalOld\PatientAppointment.DbEntities\Models\AppointmentDocument.cs` | 1-139 | OLD's appointment-document row schema (now stored as `AppointmentNewDocument`); for cross-reference only -- NEW uses different schema |
| `P:\PatientPortalOld\PatientAppointment.DbEntities\Constants\ApplicationConstants.cs` | 69 | `EmailTemplate.AppointmentDocumentAddWithAttachment` template name |

### OLD source — DOCX templates

| Path | Used for | Role |
|---|---|---|
| `P:\PatientPortalOld\PatientAppointment.Api\wwwroot\Documents\documentBluePrint\patientpacketnew\PATIENT PACKET NEW.docx` | Patient Packet | This is the active Patient Packet template (referenced by `aws.patientPacketNew` in OLD config). The "patientpacket" / `aws.patientPacket` is the legacy version -- ignore it. |
| `P:\PatientPortalOld\PatientAppointment.Api\wwwroot\Documents\documentBluePrint\doctorpacket\DOCTOR PACKET.docx` | Doctor Packet | Active Doctor Packet template |
| `P:\PatientPortalOld\PatientAppointment.Api\wwwroot\Documents\documentBluePrint\patientpacket\PATIENT PACKET.docx` | (legacy) | Older Patient Packet -- do NOT use, kept here for context |

To extract the OOXML and inspect:
- `unzip -p '<path>'/PATIENT\ PACKET\ NEW.docx word/document.xml > /tmp/ptpacket.xml`
- Use any Word / LibreOffice install to view the rendered layout side
  by side with the QuestPDF render. Adrian can install LibreOffice if
  the demo box doesn't have Word.

### NEW source — entry points

| Path | Role |
|---|---|
| `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/Handlers/PacketGenerationOnApprovedHandler.cs` | Subscriber that enqueues the job. KEEP -- only the job changes. |
| `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/Jobs/GenerateAppointmentPacketJob.cs` | The job to rewrite (Phase 5). |
| `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/AppointmentPacket.cs` | Entity to extend (Phase 4) for two-packet shape. |
| `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/AppointmentPacketManager.cs` | Manager to extend (Phase 4). |
| `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/CoverPageGenerator.cs` | DELETE in Phase 5 (this was the placeholder cover-page; replaced by template-based packets). |
| `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/PacketMergeService.cs` | DELETE in Phase 5 (merge-uploaded-docs is a different feature; the new packets are templated). |
| `src/HealthcareSupport.CaseEvaluation.Domain/BlobContainers/AppointmentPacketsContainer.cs` | KEEP (blob storage abstraction for packet PDFs). |
| `src/HealthcareSupport.CaseEvaluation.Domain/Notifications/Handlers/` | Where the new `PatientPacketEmailHandler` lands (Phase 6). |
| `src/HealthcareSupport.CaseEvaluation.Application/AppointmentDocuments/AppointmentPacketsAppService.cs` | Read-side AppService for the frontend (Phase 7) -- list, download. |

---

## Locked tokens (the source of truth)

These were extracted from the OLD DOCX templates with
`grep -oE '##[A-Za-z]+\\.[A-Za-z]+##'`. Phase 0 verifies coverage; do
not "extend" this list without surfacing to Adrian.

### Patient Packet — `PATIENT PACKET NEW.docx` (extracted 2026-05-05)

| Token | OLD entity view |
|---|---|
| `##Patients.FirstName##`, `##Patients.LastName##`, `##Patients.DateOfBirth##`, `##Patients.Email##`, `##Patients.PhoneNumber##`, `##Patients.CellPhoneNumner##` (sic), `##Patients.SocialSecurityNumber##`, `##Patients.City##`, `##Patients.State##`, `##Patients.Street##`, `##Patients.InterpreterVendorName##` | `vPatient` |
| `##Appointments.RequestConfirmationNumber##`, `##Appointments.AvailableDate##`, `##Appointments.AppointmenTime##` (sic), `##Appointments.Location##`, `##Appointments.LocationState##`, `##Appointments.LocationZipCode##`, `##Appointments.LocationParkingFee##`, `##Appointments.PrimaryResponsibleUserName##`, `##Appointments.Signature##` | `vAppointmentDetail` (+ `IdentityUser` for responsible) |
| `##EmployerDetails.EmployerName##`, `##EmployerDetails.Street##`, `##EmployerDetails.City##`, `##EmployerDetails.State##`, `##EmployerDetails.Zip##` | `vAppointmentEmployerDetail` |
| `##PatientAttorneys.AttorneyName##`, `##PatientAttorneys.FirmName##`, `##PatientAttorneys.PhoneNumber##`, `##PatientAttorneys.Street##`, `##PatientAttorneys.City##`, `##PatientAttorneys.State##`, `##PatientAttorneys.Zip##` | `vAppointmentPatientAttorney` (NEW: `AppointmentApplicantAttorney` join + `ApplicantAttorney`) |
| `##DefenseAttorneys.AttorneyName##`, `##DefenseAttorneys.Street##`, `##DefenseAttorneys.City##`, `##DefenseAttorneys.State##`, `##DefenseAttorneys.Zip##` | `vAppointmentDefenseAttorney` |
| `##InjuryDetails.ClaimNumber##`, `##InjuryDetails.DateOfInjury##`, `##InjuryDetails.PrimaryInsuranceName##`, `##InjuryDetails.PrimaryInsuranceStreet##`, `##InjuryDetails.PrimaryInsuranceCity##`, `##InjuryDetails.PrimaryInsuranceState##`, `##InjuryDetails.PrimaryInsuranceZip##`, `##InjuryDetails.WcabOfficeName##`, `##InjuryDetails.WcabOfficeCity##`, `##InjuryDetails.WcabOfficeZipCode##` | `vInjuryDetail` (NEW: `AppointmentInjuryDetail` + `AppointmentPrimaryInsurance` + `WcabOffice`) |
| `##Others.DateNow##` | computed (`DateTime.Today`) |

### Doctor Packet — `DOCTOR PACKET.docx` (extracted 2026-05-05)

| Token | OLD entity view |
|---|---|
| `##Patients.FirstName##`, `##Patients.LastName##` | `vPatient` |
| `##Appointments.AvailableDate##` | `vAppointmentDetail` |
| `##InjuryDetails.ClaimExaminerName##` | `vInjuryDetail` (NEW: `AppointmentClaimExaminer` joined off injury) |
| `##Appointments.Signature##` (implicit -- check during Phase 0 audit) | (signature placeholder) |

The Doctor Packet has many fewer tokens; most of its content is the
clinical-exam form filled by hand at the visit.

If Phase 0's grep finds tokens not in this list, **stop** and surface
them to Adrian; do not silently extend the list.

---

## Constraints

- **HIPAA: never use real patient PHI** in samples, smoke tests, seed
  data, or commit messages. Synthetic data only per
  `~/.claude/rules/hipaa-data.md` and `~/.claude/rules/test-data.md`.
- **ASCII only** in source code per `~/.claude/rules/code-standards.md`.
- **Branch:** stay on `feat/replicate-old-app-track-domain`. Do not push
  to origin without explicit Adrian approval. Squash-merge to `main`
  only after Phase 8 sign-off and a PR review.
- **No `ng serve`, no `yarn start`, no `ng build --watch`.** Use the
  build-then-static-serve pattern documented in
  `W:\patient-portal\replicate-old-app\CLAUDE.md` "Critical constraints".
- **Stop-points are mandatory.** Each phase's stop-point requires Adrian
  acknowledgment before the next phase starts. Do not chain phases.
- **Tests:** Phase 3 has unit tests; Phases 1, 2, 4, 5, 6, 7 use the
  visual / smoke verification gates. Don't add tests to Phases 1-2-4-5-
  6-7 unless Adrian asks -- the visual fidelity is the test.
- **Commit cadence:** one commit per phase boundary, conventional-commit
  format per `~/.claude/rules/commit-format.md`. Phase commits use the
  `feat(packets):` scope.

---

## Success criteria (acceptance)

A reviewer can verify the work as follows:

- [ ] `docs/parity/packet-generation-audit.md` exists and lists every
  OLD token alongside its NEW entity mapping; no `MISSING-IN-NEW` rows
  remain unresolved.
- [ ] `docs/parity/samples/patient-packet-sample.pdf` and
  `docs/parity/samples/doctor-packet-sample.pdf` exist; placed beside
  the OLD DOCX rendered output, the layouts are visually identical
  section-by-section.
- [ ] Token-driven fields in both sample PDFs reflect the synthetic
  context data exactly. Static labels match OLD verbatim (every
  checkbox label, section heading, instruction text).
- [ ] `AppointmentPacket` (or its replacement) carries a `PacketKind`
  discriminator with values Patient + Doctor; the EF migration applied
  cleanly via `dotnet ef database update`.
- [ ] `GenerateAppointmentPacketJob` produces two PDFs per appointment;
  per-packet failure is isolated.
- [ ] `PatientPacketEmailHandler` sends an email with PDF attachment
  when Patient Packet generation succeeds. Mailtrap capture + screenshot
  saved.
- [ ] Doctor Packet is NOT emailed.
- [ ] `appointment-view.component.ts` lists both packets with role-gated
  visibility.
- [ ] Full demo path runs end to end without errors: register, login,
  book, approve, both PDFs appear, patient email arrives.
- [ ] PR description embeds the locked-decisions list verbatim, the
  token mapping table, links to the sample PDFs, and any
  `_parity-flags.md` rows added.

---

## Reasoning affordance

Before writing any code, take the audit step seriously. Open every
DOCX in LibreOffice (or a Word installation), screenshot each section,
write the section-by-section outline. The QuestPDF rendering will only
match what you understood, so understanding upfront pays multiples
later in the phases. Use the audit doc as the contract you render
against; do not deviate from it without surfacing the change.

The token mapping in Phase 0 step 3 is also where most decisions are
made. Each "transform" cell -- date format, nullable handling, field
concatenation -- becomes load-bearing in the resolver. Spending an
hour on a careful mapping table saves a day of layout iteration.

---

## When to stop and ask Adrian

- Phase 0 step 5: present plan, wait for sign-off.
- Phase 1 step 5, Phase 2 step 5: visual side-by-side, wait for visual
  sign-off.
- Phase 3 step 5: resolver review, wait for sign-off.
- Phase 4 step 3: migration diff review, wait for sign-off.
- Phase 5 step 4: job rewrite + deletion review, wait for sign-off.
- Phase 6 step 6: email capture review, wait for sign-off.
- Phase 7 step 4: UI sanity check, wait for sign-off.
- Phase 8 step 5: PR review, before merge.
- ANY tokens found that aren't in the locked list above.
- ANY entity field on the NEW side that doesn't have an obvious source
  for an OLD token.
- ANY decision about layout that the DOCX template doesn't make
  clear-cut.

When in doubt, stop. The work is meticulous by design.

---

## Source for this prompt

`docs/prompts/2026-05-05-packet-generation-pdf-prompt.md` (this file).
This file is the contract for the agent and the reviewer. Update it in
place if a phase decision changes; the agent re-reads it at every
session start.
