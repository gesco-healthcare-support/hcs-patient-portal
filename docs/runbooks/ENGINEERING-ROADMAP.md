# Engineering Roadmap — Path to Alpha

Living technical companion to `docs/status-reports/2026-05-18-status-for-manager.md`.

The status report is for leadership and describes the work in non-technical terms. **This doc is for the developer.** Every line item from the status report appears here with: the code paths that implement (or need to implement) it, the existing plan doc reference, the bug-ID cross-reference, and what's actually left to do. Update it as items ship — it's a working document, not a snapshot.

| Source of truth | Where |
|---|---|
| Leadership-facing summary + dates | `docs/status-reports/2026-05-18-status-for-manager.md` |
| OLD-app parity audit (1076 lines, categorized) | `docs/parity/_remaining-from-old-audit-2026-05-15.md` |
| Individual bug entries (50+ files, YAML frontmatter) | `docs/runbooks/findings/bugs/` |
| Plan docs (5 top-level + 8 in SlotGenerationRework/) | `docs/plans/` |
| Manual test scenarios (R1/R2/R3) | `docs/runbooks/HARDENING-TEST-SUITE.md` |
| Repo-wide documentation map | `docs/INDEX.md` |

---

## At a glance

| Milestone | Best case | Realistic | Engineering trigger |
|---|---|---|---|
| Internal Alpha | 4 weeks | 6 weeks | Stage 1 tasks below all merged on `feat/replicate-old-app` → `main`, smoke-tested end to end |
| Closed Beta | 9 weeks | 13 weeks | Stage 2 tasks merged + one pilot office onboarded on test data |
| Production launch (first office) | 12 weeks | 17 weeks | Stage 3 + HIPAA pass + production hosting wired |

Calendar assumes solo dev with Claude Code, 5 working days per week, 30% buffer.

Branch state: `feat/replicate-old-app` is the working branch. Promotion cascade: `feat/replicate-old-app → main → development → staging → production`.

---

## Section 1 — What's shipped today (with code refs)

For each leadership-facing claim, the actual code that backs it. If a claim couldn't be tied to specific code during the survey, that's flagged inline.

### Patient / attorney / adjuster surface

| Status-report claim | Implementation |
|---|---|
| Register + accept Terms and Conditions | AuthServer Razor at `src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/Account/` (Login, Register, ConfirmUser, ResendVerification cshtml). T&C modal shipped via PR #204 (commit `39477af`). Backend: `Application/ExternalSignups/ExternalSignupAppService.cs` |
| Log in / reset password / verify email | AuthServer Razor pages: `Login`, `ForgotPassword`, `ResetPassword`, `ConfirmUser`, `EmailConfirmation`, `ResendVerification`. SPA `/account/*` was deleted in PR #201 |
| Invitation by email + signup from link | `Domain/Invitations/` + migration `20260515183211_Added_Invitations`. Plan: `docs/plans/2026-05-15-invite-external-user.md` (status: draft — but the feature shipped via PR #202; plan needs a status flip to `shipped`). Commit `f230d93` |
| 7-step guided booking with dedupe | `angular/src/app/appointments/appointment-add.component.ts` (~1594 lines) + 7 section components under `appointments/sections/`. Dedupe in `Application/Patients/PatientsAppService.cs` + `AppointmentBookingValidators.cs` |
| One-time-link document upload (no login) | `Application/AppointmentDocuments/AppointmentDocumentsAppService.cs` + `HttpApi/Controllers/AppointmentDocuments/`. Anonymous verification-code-based access |
| Joint Declaration Form + auto-cancel | Handler `Application/Notifications/Handlers/JdfAutoCancelledEmailHandler.cs`; job `Domain/Notifications/Jobs/JointDeclarationAutoCancelJob.cs` |
| Reschedule / cancellation request | Backend: `Application/AppointmentChangeRequests/AppointmentChangeRequestsAppService{,Approval}.cs`. **Patient-facing submission UI is the gap flagged in Stage 1 #6 (OBS-12)** |
| View own appointments + download PDF packet | `angular/src/app/appointments/` + `angular/src/app/appointment-packet/appointment-packet.component.ts`. Backend `Domain/PackageDetails/DocumentPackage.cs` + `BlobContainers/DocumentPackagesContainer.cs` |

### Clinic staff / IT Admin surface

| Status-report claim | Implementation |
|---|---|
| IT Admin creates internal users | `Application/InternalUsers/InternalUsersAppService.cs` + `angular/src/app/internal-users/components/internal-users-form.component.{ts,html}`. Tenant-admin variant shipped 2026-05-19 (see `docs/plans/SlotGenerationRework/2026-05-19-tenant-admin-internal-users-seed-swap.md`, status: shipped) |
| Approve/reject appointment + reason + assign responsible | `Application/Appointments/AppointmentsAppService.Approval.cs`, `AppointmentApprovalValidator.cs`, `Domain/Appointments/AppointmentManager.cs`. Hardened 2026-05-19: reject now requires reason (BUG-024 fix), invalid-transition returns 400 (`CaseEvaluationDomainErrorCodes.AppointmentInvalidTransition`) |
| Approve/reject reschedule + cancellation | `Application/AppointmentChangeRequests/AppointmentChangeRequestsAppService.Approval.cs` + email handlers `ChangeRequestApprovedEmailHandler.cs`, `ChangeRequestRejectedEmailHandler.cs` |
| Accept/reject individual documents | `Domain/AppointmentDocuments/AppointmentDocumentManager.cs` + handlers `DocumentAcceptedEmailHandler.cs`, `DocumentRejectedEmailHandler.cs` |
| Three PDF packets on approval | `Domain/PackageDetails/DocumentPackage.cs` with `PacketKind` composite unique (migration `20260508215314_Packet1A_Add_PacketKind_And_CompositeUnique`); handlers `PatientPacketEmailHandler.cs`, `AttyCEPacketEmailHandler.cs`, `PackageDocumentQueueHandler.cs`. **Only two distinct handlers exist — the patient packet and the atty/CE packet. The OLD-parity internal-recipient packet is the Stage 2 "two-packet split" item** |
| Share read/edit access with another user (Accessors) | `Domain/AppointmentAccessors/` + handler `AccessorInvitedEmailHandler.cs`. Guard `Application/Appointments/AppointmentReadAccessGuard.cs`. Section component `appointment-add-authorized-users.component.ts` |
| Admin screens (doctors, availability, locations, types, WCAB, attorneys, languages) | `angular/src/app/{doctors,doctor-availabilities,locations,appointment-types,wcab-offices,applicant-attorneys,defense-attorneys,appointment-languages,states}/`. Backend pairs in matching `Application/` folders |
| Dashboard counters click-through | `Application/Dashboards/DashboardAppService.cs` + `angular/src/app/dashboard/tenant-dashboard/tenant-dashboard.component.ts`. **Several tiles are pinned to 0 until day-of-exam states ship (DashboardAppService.cs:112-125)** |

### Behind the scenes

| Status-report claim | Implementation |
|---|---|
| Modern login replacing legacy custom code | AuthServer + OpenIddict. `Domain/OpenIddict/OpenIddictDataSeedContributor.cs`. Subdomain wildcard validation in `CaseEvaluationAuthServerModule.cs:103-111` (`AbpOpenIddictWildcardDomainOptions`) — closes BUG-016 |
| Cloud storage, vendor-neutral | ABP BlobStoring with MinIO swap for 7 document containers: `Domain/BlobContainers/{AppointmentDocumentsContainer.cs,DocumentPackagesContainer.cs,...}` |
| SMTP email delivery | AuthServer: direct SMTP for auth-mails via `Application/Emailing/CaseEvaluationAccountEmailer.cs` (overrides ABP's `IAccountEmailer`). API: Hangfire `Domain/Appointments/Jobs/SendAppointmentEmailJob.cs` for everything else |
| "Eighteen distinct email notifications wired" | 21 handler files in `Application/Notifications/Handlers/` — the "18" likely counts user-facing email types after deduping (StatusChange covers multiple status transitions). Audit `_remaining-from-old-audit-2026-05-15.md` §6 says 18 wired / 41 templates unwired |
| ~10 background tasks | 9 distinct job classes: `Domain/Notifications/Jobs/{DueDateApproachingJob, DueDateDocumentIncompleteJob, InternalStaffQueueDigestJob, JointDeclarationAutoCancelJob, PackageDocumentReminderJob, PendingDailyDigestJob}` + `Domain/Appointments/Notifications/Jobs/{AppointmentDayReminderJob, CancellationRescheduleReminderJob, RequestSchedulingReminderJob}` + `Domain/Appointments/Jobs/SendAppointmentEmailJob` |
| Privacy safeguards (rate-limit, generic messages, no echo) | Rate-limit on `/api/public/external-signup/register` (PR #197). Anti-enumeration messages locked in `Localization/CaseEvaluation/en.json` Registration:DuplicateEmail (BUG-026 fix). Status code maps in `HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` (BUG-003, BUG-023 fixes) |

---

## Section 2 — What's still missing (engineering detail)

### 2.1 Features not yet built

| Status-report item | Engineering scope | Plan? | Bug? |
|---|---|---|---|
| **Check-in / check-out / no-show / billed actions + today's view** | Statuses exist (`Domain.Shared/Enums/AppointmentStatusType.cs:13 NoShow=4`, `:20 Billed=11`) but no AppService transitions or UI actions exist. New methods on `AppointmentsAppService` (CheckInAsync / CheckOutAsync / MarkNoShowAsync / MarkBilledAsync); new Angular route under `appointments/` for the today-view list. Parity audit: `docs/parity/wave-1-parity/clinic-staff-check-in-check-out.md`. **High severity (audit §11). Wave 1.** | None | None — net-new feature |
| **Internal notes (threaded, edit history)** | New aggregate at `Domain/AppointmentNotes/`, AppService, Angular thread UI. Parity doc: `docs/parity/wave-1-parity/appointment-notes.md`. **Medium severity. Wave 2.** | None | None |
| **"Ask us a question" widget** | New `Domain/UserQueries/` + SubmitQueryAppService + floating-button Angular component + staff inbox. Parity: `docs/parity/wave-1-parity/external-user-submit-query.md`. Design: `docs/design/external-user-submit-query-design.md`. **Medium severity. Wave 1.** | None | None |
| **"Patient already exists" prompt during staff approval** | Extends `AppointmentsAppService.Approval.cs` + approval modal in `angular/src/app/appointments/`. Surfaces during approve when the patient submitted demographics match an existing row | None | None |
| **Two-packet split** (separate internal-recipient packet) | New handler alongside `PatientPacketEmailHandler.cs` + `AttyCEPacketEmailHandler.cs`. `DocumentPackage.PacketKind` enum already exists — extend with new kind. Parity: `docs/parity/email-packet-parity/document-packets.md` | None | None |
| **Appointment Request Report + Excel/PDF export** | Deferred to Phase 2 per Adrian decision. Audit §2.O / `internal-user-reports.md`. New `Application/Reports/` + `angular/src/app/reports/`. **Wave 3 / post-launch.** | None | None |
| **Per-appointment Print / Export-to-Excel buttons** | Deferred to Phase 2. Audit §2.P. **Wave 3.** | None | None |

### 2.2 Correctness gaps

| Status-report item | Engineering scope | Plan? | Bug? |
|---|---|---|---|
| **One-doctor-per-office safeguard** | Guard `DoctorsAppService.CreateAsync` (reject second create per tenant) + `DeleteAsync` (reject when 4 dependent buckets non-empty). EF migration `Phase19_DoctorOnePerTenantUniqueIndex` (filtered unique index). Two new error codes in `CaseEvaluationDomainErrorCodes.cs`. Maps to HTTP 400 via `CaseEvaluationHttpApiHostModule.cs` | `docs/plans/SlotGenerationRework/2026-05-15-doctor-invariant-enforcement.md` (status: draft, sequence 1 of 7) | — |
| **Appointment slot scheduling rework** (capacity + multi-type + multi-weekday + multi-range) | Schema: `Domain/DoctorAvailabilities/DoctorAvailability.cs` gains `Capacity int NOT NULL DEFAULT 1`, drops `AppointmentTypeId`, gets new join `DoctorAvailabilityAppointmentType` entity. Domain logic: `IAppointmentRepository.GetActiveCountForSlotAsync`, new bookable predicate, three new error codes. Generation API: rewrite `GeneratePreviewAsync` + new `CreateRangeAsync`. UI: new `<app-multi-lookup-select>` component, FormArray time ranges, weekday checkboxes. Picker UI: surface `RemainingCapacity`, refetch on three new errors | Umbrella: `docs/plans/2026-05-15-slot-generation-rework.md`. Phase plans 1-6: `docs/plans/SlotGenerationRework/2026-05-15-slot-rework-phase-{1-schema,2-domain-logic,3-generation-api,4-generation-ui,5-picker-ui,6-tests-hardening}.md` (all status: draft) | — |

### 2.3 Screens that need to be built (backend exists)

| Status-report item | Engineering scope | Plan? |
|---|---|---|
| **Notification template editor** | Backend exists: `Application/NotificationTemplates/NotificationTemplatesAppService.cs`. Need: Angular admin component under `angular/src/app/notification-templates/`. Parity: `docs/parity/wave-1-parity/it-admin-notification-templates.md` | None |
| **System parameter editor** | Backend exists: `Application/SystemParameters/SystemParametersAppService.cs`. Need: Angular admin component under `angular/src/app/system-parameters/`. Parity: `docs/parity/wave-1-parity/it-admin-system-parameters.md` | None |
| **Custom field admin (partial — only consumer view exists)** | Backend exists: `Application/CustomFields/CustomFieldsAppService.cs`. SPA has only the consumer-side `appointments/sections/appointment-add-custom-fields.component.*`. Need: admin CRUD UI. Parity: `docs/parity/wave-1-parity/it-admin-custom-fields.md` | None |
| **Cross-appointment audit log view** | Per-appointment view exists; cross-appointment view does not. ABP audit infra already runs. Need Angular admin route consuming the audit endpoints | None |
| **Cross-appointment document admin views** | Parity: `docs/parity/wave-1-parity/clinic-staff-document-review.md`. New AppService methods + Angular admin view | None |
| **Reschedule + cancellation submission screens** | Backend ready (`AppointmentChangeRequestsAppService.cs`); SPA submission UI missing. OBS-12 docs the gap | None |

### 2.4 Email content polish

| Item | Engineering scope | Plan? |
|---|---|---|
| **~40 unbranded plain-text templates** | Templates referenced in `Domain.Shared/NotificationTemplates/NotificationTemplateConsts.cs`; seeded by `Domain/NotificationTemplates/`. Need HTML body + brand chrome migration. Parity audit: `docs/parity/wave-1-parity/email-coverage-audit.md` + `_branding.md`. **Audit §1 says 41 unwired templates remain** | None |

### 2.5 Known bugs to close before launch

Status of every BUG/OBS entry that was open at session start:

**Fixed today (2026-05-19, on `feat/replicate-old-app`):**
- BUG-013 (CORS on ConfirmUser) — **fixed by redesign** (B-3 work removed the surface)
- BUG-016 (OpenIddict subdomain wildcards) — **fixed by redesign** (`AbpOpenIddictWildcardDomainOptions`)
- BUG-020 (SMTP password decrypt noise) — **fixed** (IsEncrypted=false override). Also closes OBS-11
- BUG-023 (signup validation 403 → 400) — **fixed** (status-code map + Scriban 7.1.0 → 7.2.0 CVE bump)
- BUG-024 (reject accepts empty reason) — **fixed** (DTO `[Required]` + `AppointmentInvalidTransition` 400 mapping)
- BUG-026 (DuplicateEmail literal `{0}`) — **fixed** (en.json wording)

**Still open before alpha:**

| ID | Severity | Title | Component | Fix shape |
|---|---|---|---|---|
| BUG-011 | high | Reset-password link lands on SPA `/error` | `angular/src/app/account/reset-password` (suspected — frontmatter says so) | **Re-verify first** — PR #201 deleted the SPA `/account/*` routes; the entire failure mode may already be moot. If still reproducible, route to AuthServer Razor reset surface |
| BUG-008 | medium | PUT /me concurrency stamp goes stale on submit retry | `angular/.../patients/me` + `Application/Patients/PatientAppService.UpdateMeAsync` | Return fresh stamp in response DTO; merge into form state before retry |
| BUG-009 | medium | BookingDateInsideLeadTime surfaces as "internal error" | `Application/Appointments/AppointmentAppService.cs` + `Domain.Shared/Localization` | Localized message gap; add the key or use `WithData("DefaultMessage", ...)` |
| BUG-010 | medium | Synthetic .test user emails silently dropped | `docker/appsettings.secrets.json` + MailKit | Detect synthetic domains, route to local pickup folder |
| BUG-012 | medium | AA/DA Firm Name lacks client `required` + server localized error | `AuthServer wwwroot/global-scripts.js` + `Application/ExternalSignups` | Set `required` on injected input; throw `UserFriendlyException` with localized message |
| BUG-014 | medium | Portal/AuthServer URLs hardcoded in settings defaults | `Domain/Settings/CaseEvaluationSettingDefinitionProvider.cs:43,53` | Inject `IConfiguration`, source defaults from env vars |
| BUG-018 | medium | SMTP rate-limit reported as misleading "Configure ACS credentials" | `Domain/Appointments/Jobs/SendAppointmentEmailJob.cs:104,156` | Token-bucket throttle ~2.5/sec + categorize SMTP failures + update log strings |
| BUG-022 | medium | BookingDatePastMaxHorizon thrown for dates inside configured range | `AppointmentBookingValidators.cs` + `BookingPolicyValidator.cs` | **Defer — Slot rework Phase 3 rewrites this validator anyway** |
| BUG-025 | medium | No document upload size + filetype limit | `Application/AppointmentDocuments/AppointmentDocumentsAppService.cs` + Kestrel/FormOptions | `Domain.Shared/AppointmentDocuments/AppointmentDocumentConsts.cs` already declares `MaxFileSizeBytes = 25 MB` but it's not enforced. Wire it + content-type allowlist (PDF/DOCX/PNG/JPG) |
| BUG-021 (ce-cannot-book) | low | Datepicker all-disabled while slot fetch in flight | `appointment-add.component.ts:1057-1074 markAppointmentDateDisabled` | Show loading state or disable the trigger until `isAvailableDatesLoading` resolves |
| BUG-021 (login-tempdata) | low | Stock Login.cshtml swallows TempData success banner | Stock RCL view (no local override) | Add local `Pages/Account/Login.cshtml` override (mirror ForgotPassword pattern) |
| OBS-21 | low | claimE1 verification click did not flip EmailConfirmed | AuthServer EmailConfirmation.cshtml.cs OR mail delivery | Reproduce; byte-compare API log URL vs email body |
| OBS-22 | low | dotnet/ng watchers miss source edits via bind-mount on Windows | `Dockerfile.dev` files | Workaround: `docker compose restart`. Better fix bundled in Slot rework Phase 2 pre-flight |
| OBS-15..19 | observation | Booking-flow UX / role-section visibility | `angular/src/app/appointments/` | All in the booking-flow neighborhood; fold into Slot Phases 4-5 where the picker is being rewritten anyway |
| OBS-2..7 | stub | Stub entries needing rehydration | n/a | Content debt — rehydrate before treating as actionable |

**Seed data gaps:**
- **SEED-2** (open) — no `DemoDoctorDataSeedContributor`. OLD has no "+ New Doctor" UI, so doctors MUST come from seed. **Hard blocker for booking on fresh DBs.** Spec drafted inside the doc.
- **SEED-3** (open) — non-AME slots missing from seed. Downstream of SEED-2; same PR.
- **OBS-10** — seeded appointment types may miss Re-Eval, Consultation per OLD. Pair with SEED-2/3.

---

## Section 3 — Path to Alpha (Stage 1) — engineering tasks

Mirrors the status report's Stage 1 with file-level scope. Total effort: 17-27 working days = 4-6 calendar weeks.

### Task 1 — One-doctor-per-office safeguard

**Effort:** 1-2 days  
**Plan:** `docs/plans/SlotGenerationRework/2026-05-15-doctor-invariant-enforcement.md` (status: draft, sequence 1 of 7)  
**Files:**
- `src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorsAppService.cs` (guards on Create / Delete)
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainErrorCodes.cs` (two new codes)
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` (400 mappings)
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs` (filtered unique index)
- New EF migration `Phase19_DoctorOnePerTenantUniqueIndex`
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json` (error keys)
- 5 new `[Fact]` tests + 1 `[Skip]` placeholder

**Risk:** the migration will fail if any tenant has two non-deleted Doctor rows (by design — refuses to auto-pick a winner). Run the probe SQL the plan documents before the migration; manually dedupe if needed.

### Task 2 — Slot scheduling rework

**Effort:** 7-11 days  
**Plans:** Umbrella `docs/plans/2026-05-15-slot-generation-rework.md` + six phase docs `docs/plans/SlotGenerationRework/2026-05-15-slot-rework-phase-{1-6}-*.md`  
**Sequence (hard chain, declared in each plan's frontmatter):**
```
Task 1 (doctor invariant) → Phase 1 (schema) → Phase 2 (domain logic)
  → Phase 3 (generation API) → Phase 4 (gen UI) → Phase 5 (picker UI) → Phase 6 (tests)
```

**Per-phase one-liner + key files:**
- **Phase 1 schema**: add `Capacity` col, new `DoctorAvailabilityAppointmentType` join entity, drop `AppointmentTypeId`. Migration `Phase20_DoctorAvailabilityCapacityAndTypeSet`. Bundles a pre-flight fix to `Directory.Build.props` + `docker-compose.yml` named-volume cache that also closes OBS-22's bind-mount race.
- **Phase 2 domain logic**: `IAppointmentRepository.GetActiveCountForSlotAsync`, capacity-aware bookable predicate, three new error codes (`AppointmentBookingSlotFull/SlotClosed/SlotTypeMismatch`), repurpose `Reserved` status, retire `SlotCascadeHandler.cs` stamping logic. Migration `Phase21_RepurposeReservedAndBackfill` (no Down — data-migration cliff).
- **Phase 3 generation API**: rewrite `DoctorAvailabilityGenerateInputDto` (single object, `SelectedDays`, `TimeRanges[]`, `Capacity`, `AppointmentTypeIds[]`). New `CreateRangeAsync` batched insert. Existing `CreateAsync` kept for single-slot edit.
- **Phase 4 generation UI**: full reactive-form rewrite of `doctor-availability-generate.component.{ts,html}`. New shared `<app-multi-lookup-select>` (~120 lines). Drops the slot-mode radio.
- **Phase 5 picker UI**: extend `appointments/sections/appointment-add-schedule.component.{ts,html}` to show `RemainingCapacity`, drop full slots from the dropdown, error-driven refetch on three new codes. Same neighborhood as OBS-15/16/17/18/19 — fold those in here.
- **Phase 6 tests + hardening**: HRD-R1.12.{1-6} + HRD-R2.10.{1-3} + unit + integration + Playwright. Concurrency tests need real SQL Server (`[Trait("Backend", "SqlServer")]`).

**Open questions inside the plans (decide before starting Phase 1):**
1. `Reserved` semantic repurpose vs new `Closed` enum? Plan recommends repurpose.
2. Seat numbers for multi-capacity? Plan recommends "any seat".
3. Visually mark wildcard slots in picker? Plan recommends no.
4. Active-count exclusion list — verify reschedule path doesn't double-count.

**Sub-tip:** add test scaffolding (`SeedSlotAsync`, `RunInScope`, `WrapAsync`, `NextMonday`) early in Phase 2, not at Phase 6. Otherwise Phase 6 becomes a 2-week test-debt cliff.

### Task 3 — Check-in / check-out / no-show / billed + today's-view

**Effort:** 4-6 days. **No plan exists.** Net-new feature.  
**Files (to create / modify):**
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs` — new methods `CheckInAsync`, `CheckOutAsync`, `MarkNoShowAsync`, `MarkBilledAsync`
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs` — state-machine triggers (statuses 9, 10, 4, 11 already in `AppointmentStatusType.cs`)
- `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Appointments/AppointmentController.cs` — 4 new endpoints
- `angular/src/app/appointments/` — new "today's view" route + components (no current route exists)
- Email handlers `PatientAppointmentCheckedIn/CheckedOut/NoShow.cs` — codes exist in `NotificationTemplateConsts.cs` but handlers are unwired
- Audit doc: `docs/parity/wave-1-parity/clinic-staff-check-in-check-out.md`

**Pre-work:** write the plan doc (`docs/plans/2026-05-20-day-of-exam-actions.md`) before coding.

### Task 4 — Close remaining S-effort bugs

**Effort:** 2-3 days  
**Files + bugs:**
- BUG-021 (ce-cannot-book): `appointment-add.component.ts:1057-1074`
- BUG-021 (login-tempdata): new `src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/Account/Login.cshtml` override
- BUG-011: re-verify reproducibility post-PR-#201 first; if real, route to AuthServer Razor reset
- BUG-014 + BUG-015 + BUG-016 form a family (URL config trio). BUG-016 already done; BUG-014/15 are still open and pair naturally
- BUG-009 (lead-time localization): pairs with the existing `AbpExceptionHttpStatusCodeOptions` family — same one-line `options.Map` extension

**Note:** BUG-022 (date picker rejecting valid dates) is deferred — Slot rework Phase 3 rewrites the validator that throws it. Don't fix it twice.

### Task 5 — Document upload size + filetype allowlist

**Effort:** 1 day  
**Files:**
- `src/HealthcareSupport.CaseEvaluation.Application/AppointmentDocuments/AppointmentDocumentsAppService.cs` — enforce the existing `MaxFileSizeBytes = 25 MB` constant + new content-type allowlist (PDF/DOCX/PNG/JPG)
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainErrorCodes.cs` — new `FileTooLarge` + `FileTypeNotAllowed`
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` — 400 mappings + Kestrel form-options tuning
**Bug:** BUG-025

### Task 6 — Verify reschedule + cancellation submission screens

**Effort:** 1-2 days. Action paths already work end to end. Build the missing SPA submission UI:
- `angular/src/app/appointments/appointment/components/` — new reschedule + cancellation submit components
- Existing proxies: `angular/src/app/proxy/appointment-change-requests/` (auto-generated, do not edit)
- Backend already supports the action; this is pure UI work.
**Bug:** OBS-12 documents the gap.

### Task 7 — Walk the 18 wired email flows on real SMTP

**Effort:** 1-2 days  
**Plan:** the rewritten `docs/runbooks/HARDENING-TEST-SUITE.md` is the script to follow. (Its driving plan doc shipped + was removed 2026-05-20; the runbook itself is now the source of truth.)  
**Output:** any bugs surfaced get filed under `docs/runbooks/findings/bugs/`.

---

## Section 4 — Stage 2 (Beta) and Stage 3 (Production)

The status report has the full breakdown. Engineering scopes for the items most likely to surprise:

**Stage 2:**
- Email template branding (#8, 5-8 days): touches `Domain/NotificationTemplates/` seeders + `Domain.Shared/NotificationTemplates/NotificationTemplateConsts.cs`. 41 templates to migrate per audit §1.
- Internal notes (#9): new `Domain/AppointmentNotes/` aggregate + AppService + Angular thread UI. Parity doc exists.
- "Ask us a question" widget (#10): new `Domain/UserQueries/` + SubmitQueryAppService + floating-button Angular component.
- Notification + System parameter + Custom field editors (#11-13): pure SPA work over existing AppServices.
- Two-packet split (#16): new handler alongside the existing `PatientPacketEmailHandler` + `AttyCEPacketEmailHandler`. `PacketKind` enum already supports the schema.
- Accessor invite when invitee unregistered (#17, 1-3d uncertain): bridge `AccessorInvitedEmailHandler.cs` to `Domain/Invitations/InvitationManager.cs`.

**Stage 3:**
- Cumulative-trauma date-range UX (#19): OBS-15 territory, in `appointments/sections/appointment-add-claim-information.component.*`.
- Cross-appointment audit log view (#20): new Angular admin route over ABP's existing audit infrastructure.

---

## Section 5 — Cross-cutting engineering notes

### 5.1 Parallel-eligible work

These can land in any order alongside the slot chain — no overlap with the slot-rework file set:
- **BUG-008**, **BUG-014/15**, **BUG-018**, **BUG-025** — isolated files
- `docs/plans/2026-05-15-invite-external-user.md` — already shipped via PR #202; just needs frontmatter `status: shipped`

Inside the slot chain itself, **Phases 5 and 6 (gen UI + picker UI)** could land on parallel sub-branches off the merged Phase 4 proxy — disjoint Angular trees. Only worth doing if you ever get a second pair of hands.

### 5.2 Audit-discrepancy reconciliation

The OLD-parity audit (`_remaining-from-old-audit-2026-05-15.md`) predates several of this session's fixes and these merges. **Check the audit's claims against the actual current state** before re-doing work it lists as outstanding:

- §2.AA "Terms & Conditions checkbox + modal" — audit says NOT STARTED. **PR #204 shipped this.**
- §2.BB "Confirm/T&C noted as NOT STARTED" — same.
- §2.AA "ConfirmPasswordMismatch / FirmNameRequiredForAttorney 403→400" — **BUG-023 fix landed 2026-05-19.**
- §2.U "Internal user management" — **partially shipped via PR #203 + tenant-admin seed-swap 2026-05-19.** Self-edit and soft-delete remain.

### 5.3 Test coverage gaps to know about

- `test/CLAUDE.md` flags coverage gaps: Patients, Appointments, Locations, DoctorAvailabilities, all host-only lookup entities have no tests.
- EF Core tests MUST sit in the shared collection (`[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]`) — without it xUnit parallelizes and corrupts the shared in-memory SQLite.
- SQL Server-specific concurrency tests use `[Trait("Backend", "SqlServer")]` and need the real DB, not the in-memory SQLite.

### 5.4 Pre-commit + push hygiene

- The host's `dotnet format` pre-commit hook fails when NuGet audit raises a new CVE warn-as-error. When that happens, look up the patched version (today's Scriban 7.1.0 → 7.2.0 was a 2-minute fix). Don't `--no-verify`.
- The `dotnet watch` + `ng build --watch` watchers miss source edits through the Docker bind-mount on Windows (OBS-22). After any edit, verify the dist mtime moved; if not, `docker compose restart <svc>`. The Slot Phase 1 pre-flight `obj/` race fix should kill this for good once it ships.

### 5.5 Housekeeping items parked from earlier sessions

- 3 `@audit.test` synthetic patient rows still in Falkinstein DB from prior audit runs
- 1 duplicate `admin@abp.io` Falkinstein-scoped row (cosmetic, harmless)
- Both = ~5 minutes of SQL cleanup before the next HRD run

---

## Section 6 — How to use this doc

- **Mark items done in place** when they ship. Update bug status, plan status, and add commit/PR refs.
- **When the status report gets refreshed**, mirror the changes here too. The two docs should stay in lockstep.
- **The single biggest risk** is the slot rework's 7-phase chain. It cannot be parallelized internally. Budget realistic time and ship Phase 1 with the docker `obj/` fix bundled in.
- **The 50-bug-doc directory is the truth source for individual bug state.** When in doubt, grep frontmatter.

---

*Last full refresh: 2026-05-20 (built from `_remaining-from-old-audit-2026-05-15.md`, the 2026-05-18 status report, all bug docs, and all plan docs). Owner: Adrian.*
