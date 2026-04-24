# Capabilities blocked on open scope questions

**Phase 4 output.** 10 of 39 capabilities carry a `Blocked by open question` dependency. Each group below quotes the verbatim question from `docs/gap-analysis/README.md`, states the Phase 2 brief's recommended default answer, and lists the downstream wave-position consequences.

Adrian's answers here directly determine wave composition. Brief-recommended defaults preserve the Phase 3 ordering.

## Feature-scope decisions (Q1-Q16)

### Q1 -- Defense Attorney as first-class entity?

Verbatim from `docs/gap-analysis/README.md:231`:
> Defense Attorney as first-class entity? OLD has it, NEW has only ApplicantAttorney + the role name. If yes, add `AppointmentDefenseAttorney` entity + service + UI. (Tracks 2, 3, 9)

Affects: [attorney-defense-patient-separation](solutions/attorney-defense-patient-separation.md), [joint-declarations](solutions/joint-declarations.md), [appointment-documents](solutions/appointment-documents.md).

- **If Q1 = no** (brief default): add `AttorneyType` enum on `AppointmentApplicantAttorney` join, rename entity to `PatientAttorney`. **Effort S-M (~1.5-2 days).** Attorney-separation stays in Wave 1; downstream capabilities use the unified entity.
- **If Q1 = yes:** split into 2 parallel entities `PatientAttorney` + `DefenseAttorney`, each with identical 17-col firm schema. **Effort jumps to M (~5 days).** Attorney-separation still Wave 1 but 2.5x effort.

### Q2 -- Patient Attorney vs Applicant Attorney

Verbatim `README.md:232`:
> Patient Attorney vs Applicant Attorney: same concept renamed, or two distinct roles OLD had that NEW conflates? (Tracks 2, 3)

Affects: same set as Q1. Brief recommends **rename** (NEW's `ApplicantAttorney` = OLD's `PatientAttorney`; "Applicant" is WCAB terminology).

### Q3 -- Claim Examiner sub-entity

Verbatim `README.md:233`:
> Claim Examiner sub-entity on Appointment: required for workers-comp tracking? (Track 2)

Affects: [appointment-injury-workflow](solutions/appointment-injury-workflow.md).

- **If Q3 = yes** (recommended; workers-comp requires claim examiner per insurance carrier): keep `AppointmentClaimExaminer` in the injury sub-graph. Wave 2, L effort.
- **If Q3 = no:** drop the claim-examiner sub-entity, shave ~1 day off injury-workflow.

### Q4 -- Injury details + body parts + primary insurance

Verbatim `README.md:234`:
> Primary Insurance, Injury details (incl. body parts), WorkerCompensation: required for workers-comp IME? (Track 2)

Affects: [appointment-injury-workflow](solutions/appointment-injury-workflow.md).

- **If Q4 = yes** (recommended; core workers-comp IME data): full 4-entity aggregate. Wave 2, L (~7 days).
- **If Q4 = no:** drops from MVP; workers-comp IME cannot run without it so this is essentially a must.

### Q5 -- 13-state enforcement

Verbatim `README.md:234`:
> Is MVP meant to enforce the 13-state lifecycle or leave it advisory (current NEW state)? (Tracks 2, 3)

Affects: [appointment-state-machine](solutions/appointment-state-machine.md) plus downstream cascades.

- **If Q5 = enforce** (strongly recommended -- this is the #1 MVP risk per exec summary): Stateless library + transition enforcement. Wave 1, M (~3 days). Blocks appointment-booking-cascade, appointment-change-requests, appointment-change-log-audit, dashboard-counters.
- **If Q5 = leave advisory:** capability collapses to doc-only. Downstream capabilities are simpler but silent-regression risk persists.

### Q6 -- CustomField port

Verbatim `README.md:236`:
> CustomField dynamic form builder: port from OLD, or drop? (Tracks 1, 2, 3, 9)

Affects: [custom-fields](solutions/custom-fields.md).

- **If Q6 = port** (recommended via ABP `ObjectExtensionManager` per track-10 erratum 4): S-M (~1-2 days). Wave 1.
- **If Q6 = drop:** zero effort; capability removed from MVP.

### Q7 -- Template management

Verbatim `README.md:237`:
> Template management: port from OLD or use ABP TextTemplateManagement? (Tracks 1, 2, 3, 6, 9)

Affects: [templates-email-sms](solutions/templates-email-sms.md), [scheduler-notifications](solutions/scheduler-notifications.md).

- **If Q7 = ABP TextTemplateManagement** (strongly recommended): ~10 TemplateDefinitions under Domain/Emailing/. Wave 1, M.
- **If Q7 = port from OLD:** rebuild OLD's Template entity + admin UI. Rejected in brief.

### Q8 -- SystemParameter vs ABP Settings

Verbatim `README.md:238`:
> SystemParameter: keep as entity or delegate to ABP Settings Management? (Tracks 1, 3, 9)

Affects: [system-parameters-vs-abp-settings](solutions/system-parameters-vs-abp-settings.md), [appointment-lead-time-limits](solutions/appointment-lead-time-limits.md).

- **If Q8 = ABP Settings** (strongly recommended): collapse to 12 `CaseEvaluation.*` SettingDefinitions. S (1 day). Wave 0.
- **If Q8 = port SystemParameter entity:** rebuild entity + admin UI. Rejected.

### Q9 -- Document Packages

Verbatim `README.md:239`:
> Document Packages + Package Details: required for MVP? (Tracks 1, 2, 3, 4, 9)

Affects: [document-packages](solutions/document-packages.md).

- **If Q9 = no** (recommended; OLD's runtime uses ServerSetting not DocumentPackages table -- effectively dead code): zero effort. Capability removed.
- **If Q9 = yes:** M (4-5 days). Wave 1.

### Q10 -- Notes on appointments

Verbatim `README.md:240`:
> Notes on appointments (parent/child thread): required? (Tracks 1, 2, 3, 4, 9)

Affects: [appointment-notes](solutions/appointment-notes.md).

- **If Q10 = yes:** S (1 day). Wave 2.
- **If Q10 = no:** zero effort. Removed.

### Q11 -- UserQuery / contact-us

Verbatim `README.md:241`:
> UserQuery / contact-us: required? (Tracks 2, 3, 4, 9)

Affects: [user-query-contact-us](solutions/user-query-contact-us.md).

- **If Q11 = no** (recommended default; OLD never shipped the admin inbox per the agent's research): zero effort. Add mailto link in footer post-BRAND-02.
- **If Q11 = yes:** S (~0.5-1 day).

### Q12 -- Report search + export formats

Verbatim `README.md:242`:
> Report search page + PDF/Excel export: required? Which formats? Per-entity (ABP pattern) or generic (OLD pattern)? (Tracks 3, 4, 9)

Affects: [appointment-request-report-export](solutions/appointment-request-report-export.md).

- **If Q12 = XLSX only, per-entity ABP pattern** (recommended; track-10 erratum 1 proves OLD never rendered server-side PDF): M (~3 days). Wave 3.
- **If Q12 = XLSX + PDF via QuestPDF (Gesco qualifies for free tier):** +1.5 days.
- **If Q12 = no:** zero effort; drop capability.

### Q13 -- Dashboard counters

Verbatim `README.md:243`:
> Dashboard counters: which of OLD's 13 cards are needed, per role? (Tracks 2, 3, 4, 9)

Affects: [dashboard-counters](solutions/dashboard-counters.md).

- **If Q13 = full 13 cards, per-role matrix:** S-M (~2-3 days) with tests. Wave 2.
- **If Q13 = minimal subset** (recommended default; admin gets all, clinic staff gets 4 today-view): S (~1 day). Wave 2.

### Q14 -- AppointmentChangeLog audit

Verbatim `README.md:244`:
> AppointmentChangeLog custom audit: compliance blocker (HIPAA), or can we rely on ABP's generic `AbpEntityChanges`? (Tracks 1, 2, 3, 9)

Affects: [appointment-change-log-audit](solutions/appointment-change-log-audit.md).

- **If Q14 = ABP AbpEntityChanges sufficient** (recommended; brief confirms it captures WHO/WHAT/WHICH-FIELD/WHEN/OLD/NEW per HIPAA 45 CFR 164.312(b)): S-M (2 days). Wave 2.
- **If Q14 = port OLD's custom AuditRecord tables:** rejected; M-L.

### Q15 -- Anonymous magic-link upload

Verbatim `README.md:245`:
> Anonymous upload via magic link (OLD `/upload-documents/:id/:type`): required for external attorneys/patients to submit documents without login? (Tracks 4, 7, 9)

Affects: [anonymous-document-upload](solutions/anonymous-document-upload.md).

- **If Q15 = yes** (external attorneys need to submit without login): L (~5 days). Wave 2.
- **If Q15 = no:** zero effort. External users must log in to upload.

### Q16 -- Email verification + forgot-password self-service

Verbatim `README.md:246`:
> Email verification + forgot password self-service: confirm ABP Account Module is wired and tested. (Track 5)

Affects: [account-self-service](solutions/account-self-service.md).

- **Q16 is already answered yes**: brief confirms ABP Account Module + `@volo/abp.ng.account/public` fully wired. S (0.5 day verification only). Wave 1.

## Architecture decisions (Q17-Q24)

### Q17 -- Blob storage provider

Verbatim `README.md:250`:
> Storage provider for blobs: DB BLOB (ABP default, works now) or S3 (OLD parity, needs creds)? (Tracks 1, 2, 3, 6)

Affects: [blob-storage-provider](solutions/blob-storage-provider.md) + 4 downstream document capabilities.

- **If Q17 = DB BLOB** (recommended; HIPAA stays inside SQL Server encryption boundary): M. Wave 0.
- **If Q17 = S3 now:** add AWS BAA signing + IAM/KMS/CloudTrail setup effort ~1 week; +2-3 days on capability itself.

### Q18 -- Background jobs runtime

Verbatim `README.md:251`:
> Background jobs: Hangfire/Quartz add-on vs ABP's one-shot `IAsyncBackgroundJob`? (Tracks 2, 3, 6)

Affects: [background-jobs-infrastructure](solutions/background-jobs-infrastructure.md), [scheduler-notifications](solutions/scheduler-notifications.md).

- **If Q18 = Hangfire** (strongly recommended per track-10 Part 4 + version-matched Volo.Abp.BackgroundJobs.Hangfire 10.0.2): S-M. Wave 0.
- **If Q18 = Quartz:** +1 day + 3rd-party dashboard.
- **If Q18 = ABP one-shot only:** rejected -- recurring jobs not expressible.

### Q19 -- Token lifetime

Verbatim `README.md:252`:
> Token lifetime: 12h OLD vs 1h access + 14d refresh (OIDC default) -- management preference? (Track 5)

Affects: no capability has this gap; OIDC defaults are the recommendation. Out of MVP scope.

### Q20 -- Single-device login

Verbatim `README.md:253`:
> Single-device login: OLD enforces by deleting all prior tokens on login. Required for MVP? (Track 5)

Affects: no capability. Out of MVP scope per intentional architectural difference.

### Q21 -- Internal role structure

Verbatim `README.md:254`:
> Internal role structure: one `admin` role, or three distinct (ItAdmin / StaffSupervisor / ClinicStaff)? (Track 5)

Affects: [internal-role-seeds](solutions/internal-role-seeds.md), cascades to per-role testing across every capability.

- **If Q21 = 1 admin** (simpler MVP default): zero additional effort on seeds; skip StaffSupervisor + ClinicStaff tiers.
- **If Q21 = 3 distinct + Q22 = no (empty shells):** S-M (~1 day). Wave 0.
- **If Q21 = 3 distinct + Q22 = yes (baseline grants):** M (~2-3 days; Adrian authors the matrix).

### Q22 -- External role default permissions

Verbatim `README.md:255`:
> External role default permissions: add a seed contributor so the 4 external roles have baseline grants out-of-the-box, or rely on admin to assign? (Track 5)

Affects: [internal-role-seeds](solutions/internal-role-seeds.md), [appointment-accessor-auto-provisioning](solutions/appointment-accessor-auto-provisioning.md).

- See Q21 coupling above.

### Q23 -- Seed data strategy

Verbatim `README.md:256`:
> Seed data for lookup tables: write `IDataSeedContributor` classes for States, AppointmentTypes, AppointmentStatuses, AppointmentLanguages, Locations, WcabOffices, or import PROD snapshot? (Track 1)

Affects: [lookup-data-seeds](solutions/lookup-data-seeds.md).

- **Q23 answered by brief:** code-first IDataSeedContributor for 5 reference entities + synthetic demo seeder for Locations. PROD snapshot for real HCS clinic Locations is a deployment concern, not MVP code. S (1 day). Wave 0.

### Q24 -- Cascade delete ADR

Verbatim `README.md:257`:
> Cascade delete ADR: write it, to document `SetNull` for optional parents + `Cascade` on M2M joins? (Track 1)

Affects: no capability -- cross-cutting ADR deliverable. Recommended to write during Wave 0 as part of documentation hygiene.

## Security / compliance (Q25-Q27)

### Q25, Q26, Q27

Adrian already acknowledged per track-04 notes. Q25 (anonymous endpoints in OLD): no NEW capability replicates. Q26 (OLD committed AWS + Twilio keys): rotation is post-MVP operations concern. Q27 (OLD DocumentDownloadController path-traversal): flagged as "do not replicate" -- [anonymous-document-upload](solutions/anonymous-document-upload.md) uses hardened token pattern instead.

## Process / confirmation (Q28-Q32)

### Q28 -- PATCH verb parity

Verbatim `README.md:267`:
> PATCH verb parity: does Angular 7 client actively use PATCH? Grep before deciding to drop. (Track 4)

Affects: [rest-api-parity-cleanup](solutions/rest-api-parity-cleanup.md).

- **Q28 answered by brief:** OLD Angular 7 DOES use PATCH in 17 call sites but semantics are equivalent to PUT via `RxHttp.makePatchBody`. Drop-with-PUT-migration is information-lossless. ADR-006 + ~0 code. Wave 0.

### Q29 -- PROD schema parity

Requires Adrian to provide PROD `sys.tables` + `sys.procedures` list. Out of MVP code scope; pre-launch verification item.

### Q30 -- Live Swagger reachability

Confirmed in Phase 1.5 `probes/service-status.md`.

### Q31 -- Track 9 full coverage

Follow-up capture pass needed post-[internal-role-seeds](solutions/internal-role-seeds.md) + `new-sec-04` fixes (so all 4 external roles can be exercised).

### Q32 -- Book demo feature

Verbatim `README.md:271`: ABP scaffolding `/api/app/book/*`. Safe to remove from NEW post-MVP. Not blocking any capability.

## Default-scenario summary (if Adrian accepts every brief's recommended default)

All 10 blocked-on-scope capabilities land in their stated wave with the effort estimates in `dependencies.md`. Zero capabilities drop from MVP. Total effort stays in the 70-95 engineer-day band.

## Minimal-scope summary (if Adrian answers "no" to every optional feature)

Capabilities that drop to 0 effort:
- `document-packages` (Q9=no)
- `appointment-notes` (Q10=no)
- `user-query-contact-us` (Q11=no)
- `custom-fields` (Q6=drop)
- `sms-sender-consumer` (track-06 SMS defer)
- `anonymous-document-upload` (Q15=no)

Total saved: ~8-14 days. MVP floor ~55-80 engineer-days.

## Recommended decision meeting agenda

Batch all 16 feature-scope + 4 architecture questions in a single 60-minute meeting with Adrian + business owner. Each question has a brief-recommended default answer. Only Q1/Q5/Q21-22/Q13 truly need business input; the rest have engineering-defensible defaults the brief authors recommend.
