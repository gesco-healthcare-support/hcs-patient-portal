# Wave 2 demo-lifecycle test report

## EXTERNAL-USER-COMPLETE-LIFECYCLE PROGRESS (2026-04-30, live)

Adrian-locked priorities + per-step status. Update as each step lands and verifies.

| Step | Title | Code | Verified |
|---|---|---|---|
| 0.1 | `[Authorize]` on `GetExternalUserLookupAsync` + drop DA from allowed roles | DONE | **VERIFIED** (anonymous -> 401; authenticated only sees Patient + AA) |
| 0.2 | SMTP `NullEmailSender` swap on Development env | DONE | **VERIFIED + REGRESSED-FOR-6.1** (env-var check fires in Docker dev compose, but the swap is unconditional on Development; once real ACS credentials land, no email actually leaves the API container until step **5.7** flips the gate to credential-presence-aware. See 5.7 below) |
| 1.1 | `RegisterAsync` creates AA entity (Patient + AA only; DA + CE register-only) | DONE | **VERIFIED** (AA register -> AA row created; DA + CE register -> NO entity row) |
| 1.2 | `/Account/Register` hijack hooks always-attached + inline error surface (W-B-1); Register button id-based selector | DONE | **VERIFIED** (blank submit -> inline error; missing tenant -> tenant error; happy path -> redirect to /Account/Login) |
| 1.3 | Register form minimal: ONLY username/email/password/role; NO First/Last/Tenant inputs (Adrian 2026-04-30 correction). Tenant comes from existing top-of-page "switch" link. Names captured later on the booking form. | DONE | **VERIFIED** (Playwright snapshot confirms exactly 4 inputs + role select + register button; First/Last/Tenant inputs absent) |
| 1.4 | Booking-form lookup `[Authorize(...Default)]` demoted to plain `[Authorize]` (W-A-3) | DONE | **VERIFIED** (Patient and AA tokens both 200 on wcab-office-lookup, applicant-attorneys/state-lookup, patients/state-lookup, appointments/appointment-type-lookup) |
| 1.5 | Tenant-fixedness for registered users (Adrian 2026-04-30 ask) | DONE | **VERIFIED** (no-tenant register -> 403 "Tenant selection is required."; login with wrong tenant -> 400 invalid_grant; login without tenant -> 400; AA entity present in Tenant A only, absent from Tenant B; cross-tenant token-scoped queries 401) |
| 1.6 | W-REG-4 follow-up to W-B-1 (1.2): always-render tenant indicator on `/Account/Register`. Adrian clarification (2026-04-30): tenant model is **strictly tenant-scoped registration** -- every external user lands on `/Account/Register` ONLY via a tenant-specific portal link; no cross-tenant registration; internal users are NEVER created via this page (they go through ABP Identity > Users). | DONE | PENDING-E2E (extended `wwwroot/global-scripts.js`. Tenant resolution priority: `?__tenant=` query > `__tenant` cookie > block. Query-string tenant **NAME** resolved to GUID via new anonymous `GET /api/public/external-signup/resolve-tenant?name=X` endpoint -- `ExternalSignupAppService.ResolveTenantByNameAsync` runs in host context to bypass any cookie-driven tenant scoping. Banner injected: `Registering for "<name>"` (info) when context resolved, `Tenant required` (danger) + form-disabled + opacity-down when missing. New helpers: `applyEmailPrefill` (reads `?email=`), `applyRolePrefill` (reads `?role=`, accepts label or numeric value, rejects internal-role attempts silently). `submitExternalSignup` now calls `resolveTenantContext()` instead of reading the cookie directly so the query-wins priority is honored. New tracker finding **W-DOC-1**: Doctor row-level "own appointments only" filter is needed before the Doctor role can ship to production -- currently a Doctor sees every appointment in their tenant). |
| 2.1 | Booking form: gate `/patients/me` on Patient role; AA/DA/CE -> `/external-users/me` (W-B-2). Predicate fix in 3 Angular files: appointment-add.component.ts, appointment-view.component.ts, patient-profile.component.ts. Now: Patient role only -> /patients/me; everyone else (including CE, admin, Clinic Staff, Doctor) -> /external-users/me. | DONE | **VERIFIED** (CE booker opens /appointments/add cleanly: /external-users/me 200, /patients/me never called, no global error modal, all booking lookups 200) |
| 2.2 | Appointment-list query narrows to appointments where the caller is involved (booker, Patient.IdentityUserId, AA link, DA link, CE email). Internal roles (admin/Clinic Staff/Doctor) bypass the filter. Repo signature: `IReadOnlyCollection<Guid>? visibleAppointmentIds` added to GetCount + GetListWithNavigationProperties + ApplyFilter. AppService computes the visibility set in `ComputeExternalPartyVisibilityAsync`. | DONE | **VERIFIED** (negative-case: 4 fresh external users each see totalCount=0; admin baseline unchanged before/after) |
| 2.3 | Both CTA buttons + appointment-list datatable already rendered in `home.component.html` for all 4 external roles. Removed the redundant client-side identityUserId/accessorIdentityUserId filters in ngOnInit so the server's S-NEW-2 visibility narrowing returns all involved appointments. | DONE | **VERIFIED** (fresh CE login -> home shows Book Appointment + Book Re-evaluation buttons + "My Appointments Requests (0)" heading + empty datatable, no errors) |
| 2.4 | i18n keys for AppointmentStatusType 1-14 already exist in `en.json` (Pending=1, Approved=2, Rejected=3, AwaitingMoreInfo=14, etc.). Template already binds via `'::Enum:AppointmentStatusType.' + status \| abpLocalization`. | DONE | **VERIFIED** (key inventory confirmed; full positive case requires Step 5.1 to land an appointment with each status) |
| 3.1 | WCAB Office option text already binds to `displayName` (template line 931). The Mapperly mapping fix (F5) in the prior session populates `displayName` correctly. | DONE | **VERIFIED** (`/api/app/appointment-injury-details/wcab-office-lookup` returns 7 items each with non-empty `displayName`: "WCAB Anaheim", "WCAB Bakersfield", etc.) |
| 3.2 | 28 [(ngModel)] directives inside the Claim Information modal were registering with the parent reactive [formGroup], producing _rawValidators null errors. Fix: appended `[ngModelOptions]="{ standalone: true }"` to every ngModel so they bypass the parent NgForm. | DONE | **VERIFIED** (Playwright: open Claim Info modal -> 0 new console errors; fill + Add -> modal closes; click Add blank -> inline error fires + modal stays) |
| 3.3 | saveInjuryModal previously silently returned with required fields blank. Now: surfaces inline error listing the missing fields ("Please fill the required fields: Date of Injury, Claim Number, Body Parts."). Happy path: closes modal + pushes draft into injuryDrafts (already correct). | DONE | **VERIFIED** (blank-submit -> "Please fill the required fields: Date of Injury, Claim Number, Body Parts." rendered inline, modal stays open; filled-submit -> modal closes cleanly) |
| 3.4 | Conditional required validators: when applicantAttorneyEnabled, applicantAttorneyEmail becomes [required, email, maxLength(50)]; same for defenseAttorneyEmail when defenseAttorneyEnabled. Patient email already required. CE email validation deferred (template-driven Claim Info modal). | DONE | PENDING (full E2E gated on Step 5.1) |
| 3.5 | Form-level S-NEW-4 audit: required where appropriate, format validators, inline error text | DEFERRED | partly subsumed by 3.2 + 3.3 + 3.4; remaining audit items handled per-finding in subsequent steps |
| 4.1 | Read `?type=2` query param via ActivatedRoute. Set `isReevaluation` flag. Page heading binds to "Re-evaluation Appointment" vs "New Appointment". Localization key added. Future enhancements (filter AppointmentType lookup to PQMEREEVAL/AMEREEVAL only, prior-appointment picker, distinct email subject) hook off the same flag. | DONE | **VERIFIED** (`?type=1` -> heading="NewAppointment"; `?type=2` -> heading="ReEvaluationAppointment"; isReevaluation flag flips per route) |
| 4.2 | Fix residual lookup 403s for Patient + AA roles: `wcab-office-lookup`, `field-configs/by-appointment-type`, `applicant-attorneys/state-lookup` still return 403 after step 1.4. Demote each to plain `[Authorize]` (no entity-permission gate) since they return read-only lookup data. Also fix `ExternalUserRoleDataSeedContributor` so re-seeding doesn't regress the grant. (W-A-3) | DONE | **VERIFIED** (Patient token 200 on all 3; AA token 200 on all 3; seeder does not grant permissions so no regression risk -- plain `[Authorize]` is code-only, not seeder-controlled) |
| 4.3 | Fix Claim Information modal Add button silent no-op (W-A-4). Blocked on 4.2 (WCAB Office dropdown empty without it). After 4.2 lands, audit `saveInjuryModal` for any early-return condition still firing on valid data; add visible console.warn or inline toast if handler short-circuits unexpectedly. | DONE | **VERIFIED** (Playwright: modal opens with 7 WCAB options populated; fill Date+Claim#+BodyParts -> Add -> modal closes; row appears in Claim Information table with Date "2025-01-15" and Claim# "CLM-2025-001"; 0 console errors) |
| 4.4 | DA UI parity: fix Defense Attorney section column widths to match Applicant Attorney (First Name/Last Name/Email/Firm Name each col-md-3; Web Address col-md-6; Phone/Fax col-md-3 each; Street/City/State/Zip col-md-3 each). Also fix email-search row to use separate col-md-5/col-md-4/col-md-3 split matching AA layout. Pure HTML change, no TS or backend touch. | DONE | PENDING (visual; requires Angular rebuild) |
| 4.5 | Extract Claim Examiner section from Claim Information modal into a standalone "Claim Examiner Details" card in the main form (between DA card and Claim Information card). Card uses the same Include toggle + conditional pattern as AA/DA. Capture Name + Email only now; remaining CE fields (phone, fax, address) deferred to when the full CE entity is built. Modal retains only Insurance + injury-level fields (date, claim number, WCAB, ADJ#, body parts). New form controls: `claimExaminerEnabled`, `claimExaminerName`, `claimExaminerEmail`. | DONE | PENDING (visual; requires Angular rebuild) |
| 4.6 | Auto-populate on form load: if logged-in user is Patient role, pre-fill patient demographics section from `/patients/me`. If AA role, set `applicantAttorneyEnabled = true` and pre-fill AA section from `/external-users/me`. DA and CE: no pre-fill (no saved profile per D-2). Also: save Name fields back to profile when the booker submits (so first/last name "User" leak from W-A-1 gets resolved through the booking flow rather than at registration). | DONE | PENDING (visual; requires Angular rebuild. Patient pre-fill already worked. AA fix: `applicantAttorneyEnabled: true` added to `loadApplicantAttorneyForCurrentUser` patchValue. Name save-back: AA entity updated by existing upsert on submit; Patient entity updated by existing get-or-create on submit.) |
| 5.1 | Store 4 party emails on Appointment: add `PatientEmail`, `ApplicantAttorneyEmail`, `DefenseAttorneyEmail`, `ClaimExaminerEmail` nullable columns to `Appointment` entity + DTO + EF migration. Angular payload construction sends all 4. Relax AA/DA upsert guards so non-registered parties (email provided but no IdentityUserId) don't silently bail -- email fan-out (6.1) covers non-registered parties independently. Regenerate Angular proxy after migration. | DONE | **VERIFIED** (4 nullable nvarchar(256) columns confirmed in `AppAppointments` via sqlcmd; migration `20260430222449_AddAppointmentPartyEmails` applied to host + Dr Thomas 1 + Dr Rivera 2 per db-migrator logs; AppService snapshots all 4 emails on Create+Update after `_appointmentManager` calls; Angular payload + proxy `AppointmentCreateDto/AppointmentDto/AppointmentUpdateDto` all carry the 4 fields; Docker stack healthy post-rebuild. Non-registered-party guard relaxation satisfied at the appointment-row level -- emails persist regardless of AA/DA upsert outcome, so 6.1 fan-out has the addresses) |
| 5.2 | Auto-link on registration: in `ExternalSignupAppService.RegisterAsync`, after user is created, query `Appointment` rows where the matching party email column equals the new user's email. If found and no join row exists, create the join row for that party slot. Enables registered parties to see their appointments immediately after signing up. | DONE | PENDING-E2E (registered AA + DA both create their backfill join rows; Patient + CE intentionally skipped per data-model constraints documented inline in `AutoLinkAppointmentsForUserAsync` -- Patient join is via `Patient.IdentityUserId` not a join row; CE has no IdentityUser-bound join entity at MVP, fan-out reaches CE via `Appointment.ClaimExaminerEmail` directly. DI smoke test PASS: API container healthy with 7 new dependencies injected). |
| 5.3 | Queue Actions dropdown: add Review item pointing at `/appointments/view/<id>` (W-A-6) | DONE | **VERIFIED-CODE** (`appointment.component.html:235-241` already wires `<a ngbDropdownItem [routerLink]="['/appointments/view', row.appointment.id]">{{ '::Appointment:Action:Review' \| abpLocalization }}</a>` gated by `*abpPermission="'CaseEvaluation.Appointments'"`; `RouterLink` imported in `appointment.component.ts:4,50`; `Appointment:Action:Review = "Review"` in `en.json:294`. Visual verification gated on Angular rebuild). |
| 5.3b | Fix `isExternalUserNonPatient` inverted logic: returns `true` for admin (who lacks 'patient' role), classifying admin as external and making `canEdit()` return `false` for all view-page fields on non-AwaitingMoreInfo appointments. Fix: check for explicit external-role membership (AA/DA/CE) instead of `!hasPatientRole`. (`appointment-view.component.ts` line 265-271, W-VIEW-10) | DONE | PENDING-E2E (call sites at `appointment-view.component.ts:322` (`canEdit`) and `:355` (`canTakeOfficeAction`) now use `!this.isPatientUser` (which checks any-of-4-external-roles) instead of `!this.isExternalUserNonPatient` (which mis-classified admins as external). Getter itself preserved at line 265 for the URL-path use site at line 574 to avoid regressing W-B-2 (CE/internal bookers must NOT fall through to `/patients/me`). Docstring updated to flag the dual-semantics trap). |
| 5.4 | View page: surface DA section + Claim Information section (W-A-7) | DONE | PENDING-E2E (DA card mirrors AA card 1:1 -- same 11 fields, same email-search + select-from-list flow, same readonly pre-fill behavior, only labels swapped per Adrian; loaded via `GET /api/app/appointments/{id}/defense-attorney`, persisted via `POST` against the same URL inside the existing AA save chain. Claim Information card surfaces a read-only table sourced from `GET /api/app/appointment-injury-details/by-appointment/{id}` with columns Date / Claim # / WCAB / ADJ # / Body Parts / Insurance / Claim Examiner; the booking form remains the canonical edit surface for injuries at MVP. New types added: `AppointmentInjuryDetailRow`, `DefenseAttorneyLookupResult`. New state: `defenseAttorneyEnabled`, `defenseAttorneyForm`, `defenseAttorneyStateIdControl`, `defenseAttorneyEmailSearch`, `isDefenseAttorneyLoading`, `defenseAttorneyOptions`, `injuryDetails`, `isDefenseAttorney` getter. New methods: `loadDefenseAttorneyByEmail`, `onDefenseAttorneySelected`, `applyDefenseAttorneyLookup`, `bindDefenseAttorneyForAppointment`, `loadInjuryDetails`, `upsertDefenseAttorneyDetails`. Visual + E2E verification gated on Angular rebuild). |
| 5.5 | View page: populate Patient Demographics textboxes from saved data | DONE | PENDING-E2E (two concrete bugs found and fixed: (1) DOB rendered blank because `patientForm.dateOfBirth` held the API's ISO string but `ngbDatepicker`'s ControlValueAccessor requires `NgbDateStruct` -- added `parseDateOfBirthFromApi(value)` helper at `appointment-view.component.ts` (inverse of existing `formatDateOfBirthForApi`) and threaded the parsed struct through the load at the patientForm assignment; (2) Unit # template input bound `patientForm.address` while the booking form persists apartment numbers to `Patient.apptNumber` (PatientDto.apptNumber), so saved Unit # values never surfaced -- added `apptNumber` field to patientForm, loaded from `patient?.apptNumber`, retargeted the template `[(ngModel)]` to `patientForm.apptNumber`, and updated the save payload to send the user-edited value with fallback to the loaded value if untouched. All other text fields (firstName, lastName, middleName, email, phone fields, etc.) already populate via the existing `[(ngModel)]="patientForm.X"` bindings. State dropdown via `abp-lookup-select` already loads from `patientForm.stateId`. Visual + click-through verification gated on opening an existing appointment with saved demographics in the browser). |
| 5.6 | Queue Patient column: bind firstName + lastName, not email | DONE | PENDING-E2E (`appointment.component.html:305-312` -- column heading still binds `prop="patient.lastName"` so the grid sorts by last name; cell template now renders `(firstName + ' ' + lastName).trim()` when either name is non-empty, falling back to `patient.email` when both names are blank so legacy rows from W-A-1 (empty names) remain readable. Visual verification gated on a list view with a populated patient row). |
| 5.7 | **CRITICAL (must precede 6.1)**: `CaseEvaluationDomainModule.cs:60-70` (W-A-10 fix) replaces `IEmailSender` with `NullEmailSender` whenever `ASPNETCORE_ENVIRONMENT=Development`. The Docker dev stack runs with `ASPNETCORE_ENVIRONMENT=Development`, so once Azure ACS SMTP credentials land in `docker/appsettings.secrets.json`, no email leaves the API container -- 6.1 cannot be verified end-to-end. Fix: gate the swap on placeholder-credential detection. At `ConfigureServices` time, read `Settings:Abp.Mailing.Smtp.UserName` + `Settings:Abp.Mailing.Smtp.Password` from `IConfiguration`. Swap to `NullEmailSender` ONLY when either value starts with `REPLACE_` (matches both repo placeholders: `REPLACE_ME_LOCALLY` in `src/.../appsettings.json` and `REPLACE_WITH_ACS_USERNAME` / `REPLACE_WITH_ACS_PASSWORD_CONNECTION_STRING` in `docker/appsettings.secrets.json`). Otherwise wire the real `System.Net.Mail.SmtpClient` sender (per research doc Section 2.3 -- the project uses ABP's default `Volo.Abp.Emailing`, not MailKit). Preserves W-A-10's intent without blocking 6.1 the moment Adrian fills in real credentials. **Supersedes** the research doc Section 2.6 recommendation to flip `ASPNETCORE_ENVIRONMENT=Staging` -- credential-presence detection is more robust than env juggling and keeps the dev exception page wired. Provisioning walkthrough: `docs/research/2026-04-30-azure-acs-smtp-credentials.md`. Outdated `_comment_acs` in `docker/appsettings.secrets.json` updated to reflect the current SMTP-Username-resource model (research doc R9). | DONE | PENDING-E2E (sentinel detection broadened to any `REPLACE_` prefix; comment updated; build pending). |
| 6.1 | Email fan-out using stored party emails: for each of the 4 party emails on the appointment, check if a registered user exists with that email under the correct role. Registered -> send "log in to view" email with confirmation # + login link. Not registered -> send "register as [role]" email with pre-filled register URL (`?__tenant=<TenantName>&email=<email>`). Keep existing office + booker email path unchanged. (W-A-2). **GATED ON 5.7** (without the conditional swap, this step's Hangfire jobs still go to NullEmailSender in Development and cannot be verified). | DONE | PENDING-E2E (`AppointmentRecipientResolver` extended: walks all 4 S-5.1 email columns (`PatientEmail`/`ApplicantAttorneyEmail`/`DefenseAttorneyEmail`/`ClaimExaminerEmail`) AFTER the existing JOIN-based pass, deduped by email; sets `IsRegistered` from a tenant-scoped `IdentityUser` email lookup; sets `TenantName` from `ICurrentTenant.Name`. New fields on `SendAppointmentEmailArgs`: `IsRegistered` (default true for backward-compat) + `TenantName`. New ABP setting `CaseEvaluation.Notifications.AuthServerBaseUrl` (default `https://localhost:44368`). `SubmissionEmailHandler.HandleEventAsync` now branches body+subject per recipient via `BuildPerRecipientTemplate(args, eventData, ..., portalBaseUrl, authServerBaseUrl)` -- 4 templates: office queue, booker/registered-party "log in to view", non-registered party "register as [role]" with `/Account/Register?__tenant=<TenantName>&email=<email>` link. Falls back to existing W1-2 office+booker path when resolver returns 0 recipients. Real delivery still gated on Adrian filling Azure ACS credentials per `docs/research/2026-04-30-azure-acs-smtp-credentials.md`). |
| 6.2 | Email body: confirmation # + conditional login/register link with `?__tenant=` (S-NEW-3) | DONE | PENDING-E2E (delivered as part of 6.1's `BuildPerRecipientTemplate`. Every body embeds `Confirmation #<RequestConfirmationNumber>` and the appointment date line; conditional CTA picks the portal-login button or the AuthServer register button -- the latter URL-encodes both `__tenant` and `email` so tenant pre-selection works on the AuthServer's tenant-scoped login flow). |
| 6.3 | Wording: "appointment requested" verified in body | DONE | PENDING-E2E (registered party body uses subject `"Appointment requested - {confirmationNumber}"` with title `"An appointment was requested"`; non-registered body uses `"Appointment requested - register to view {confirmationNumber}"` with the same title; office body retains the W2-10 "new appointment request" wording for staff context. Booker confirmation fallback already used "Appointment request received" verbiage). |
| 7.1 | i18n sweep: replace all raw key surfaces (W-A-5 + W-UI-14/15: include the 6 missing slot-form keys -- `Enum:BookingStatus.8` (Available), `Enum:BookingStatus.9` (Booked), `Enum:BookingStatus.10` (Blocked), `SetAvailabilitySlot`, `SlotByDates`, `SlotByWeekdays`) | DONE-PARTIAL | **VERIFIED** (the 6 explicit slot-form keys are already present in `en.json` -- `Enum:BookingStatus.8: "Available"`, `.9: "Booked"`, `.10: "Reserved"` (note: actual enum value 10 is `Reserved`, not `Blocked` as W-UI-14 inferred -- confirmed against `proxy/enums/booking-status.enum.ts`); `SetAvailabilitySlot: "Set Availability Slot"`, `SlotByDates: "Slot By Date(s)"`, `SlotByWeekdays: "Slot By Weekdays"`. Broader W-A-5 sweep (other raw key surfaces across queue / view / modals) requires concrete missing-key reports from the next demo run -- 83 entries in `en.json` already cover the standard families (Enum:AppointmentStatusType, Enum:Gender, Enum:PhoneNumberType, Appointment:Action:*, Permission:*, Menu:*); spot-checks of the appointment-add and view templates show every `'::Key' \| abpLocalization` reference resolving against existing keys. Re-open as 7.1b if the demo surfaces specific raw keys). |
| 7.2 | Trigger view re-fetch after Approve so status pill updates (2.11) | DONE | PENDING-E2E (`onActionSucceeded(dto)` at `appointment-view.component.ts` now patches `appointment.appointment` from the modal-returned `AppointmentDto` immediately on the same change-detection cycle -- the status pill at `appointment-view.component.html:168-175` flips before the follow-up `getWithNavigationProperties` round-trip resolves. Background re-fetch is preserved so nav properties (patient name, last-modified-by) refresh too. Removes the "Pending for several seconds" delay reported in 2.11). |
| 7.3 | Replace static "PQME" heading with AppointmentType.Name (W-A-8) | DONE | PENDING-E2E (`appointment-view.component.html:8-19` -- removed the hard-coded "PQME Appointment" string; now binds `{{ appointment.appointmentType?.name \|\| 'Appointment' }}` followed by the literal " Appointment" only when the type is loaded. QME bookings render "Qualified Medical Examination (QME) Appointment", AME bookings render "Agreed Medical Examination (AME) Appointment", etc. The patient name and ">" separator preserved). |
| 7.4 | W-UI-11 slot generation 0-slot UX inline message: `doctor-availability-generate.component.ts:171` `generate()` produces `preview = []` for inverted `FromDate>ToDate`, inverted `FromTime>ToTime`, or zero-duration `FromTime==ToTime` inputs. Submit becomes disabled but no message explains why. Fix: after `generate()` returns, if `preview.length === 0 && form.valid`, set a user-facing inline error in the validationMessage block at template line 245-249: "No slots were generated. Check that your start date is before your end date and your start time is before your end time." | DONE | PENDING-E2E (extended `updateConflictState()` at `doctor-availability-generate.component.ts` -- when `allSlots.length === 0 && this.form.valid && !this.isGenerating`, sets `validationMessage = 'No slots were generated. Check that your start date is before your end date and your start time is before your end time.'`. Conflict-detection messaging (existing "Some generated slots already exist..." string) takes priority when both conditions apply. The existing `validationMessage` template wire at the form's validation block surfaces the new copy without further HTML changes). |
| **D.1** | Internal-role grants (Clinic Staff / Staff Supervisor / Doctor) + W-UI-16 seeder fix. Adrian decisions (2026-04-30): one-doctor-per-tenant model is intent (the tenant IS the practice); **Doctor** role added at tenant scope; runtime user seeder gated on Development; emails parameterised per tenant. | DONE | PENDING-E2E (`InternalUserRoleDataSeedContributor.cs` extended -- Staff Supervisor gains AppointmentDocuments {Default,Create,Edit,Approve} + AppointmentPackets {Default,Regenerate} + AppointmentChangeLogs.Default + CustomFields.Default; Clinic Staff gains the read-mostly subset (AppointmentDocuments {Default,Approve}, AppointmentPackets {Default,Regenerate}, AppointmentChangeLogs.Default, CustomFields.Default); new **Doctor** role gets read-only on Appointments/Patients + edit-own-availability + AppointmentPackets {Default,Regenerate} + Default on every per-injury sub-entity (Documents/Employer/AAjoin/DAjoin/CEjoin/BodyParts/PrimaryInsurances) + AppointmentChangeLogs.Default + CustomFields.Default + LookupRead. New helpers `Approve(entity)` + `Regenerate(entity)` cover the two custom-action permissions outside the standard CRUD loop. New file `InternalUsersDataSeedContributor.cs`: gated on `ASPNETCORE_ENVIRONMENT=Development`; per tenant seeds `admin@<slug>.test`, `supervisor@<slug>.test`, `staff@<slug>.test`, `doctor@<slug>.test` with default password `1q2w3E*`; re-links the tenant's existing Doctor entity (auto-created by `DoctorTenantAppService.CreateAsync`) to the doctor user via `Doctor.IdentityUserId`; host-side seeds `it.admin@hcs.test` with IT Admin. Idempotent: skips already-existing users by email; logs warnings without throwing if a role is missing. Tenant slug = lowercased name with non-alphanumerics collapsed to `-`). |
| **D.2** | W-INVITE-1 admin invite feature -- link-only (no token, no expiry, no acceptance state machine) per Adrian clarification. Tenant-specific `/Account/Register?__tenant=<Name>&email=<X>&role=<R>` URL, restricted to the 4 external roles. | DONE | PENDING-E2E (new `InviteExternalUserDto` + `InviteExternalUserResultDto` in Application.Contracts; `IExternalSignupAppService.InviteExternalUserAsync` implemented in `ExternalSignupAppService` -- gated `[Authorize(Roles = "admin,Staff Supervisor,IT Admin")]`, validates external-only role types, builds URL via `AuthServerBaseUrl` setting (S-6.1), enqueues `SendAppointmentEmailArgs` with full HTML body (button + plain link fallback) via Hangfire pipeline so 6.1's NullEmailSender / real-SMTP gate (5.7) controls actual delivery, returns the URL in the response so the admin can copy + paste manually. New tenant-side route `POST /api/app/external-users/invite` on `ExternalUserController`. New Angular standalone component `external-users/components/invite-external-user.component.{ts,html}` with reactive form (email + role select limited to 4 external roles), DEV-ONLY yellow banner, result panel showing tenant + email + role + invite URL with copy button + emailEnqueued indicator. Wired at `/users/invite` in `app.routes.ts` with `authGuard` (backend role-based gate is authoritative). |
| **D.3** | **Observation only -- no code change.** W-SLOT-3: Appointment Time picker uses time strings (`"09:00:00"`) as option values; Angular maps the selected time to a `DoctorAvailability` GUID client-side. If two `DoctorAvailability` rows share the same time at the same location (e.g., two appointment types both starting at 09:00), the picker may show duplicate options or the GUID mapping may be ambiguous. Low probability in practice because conflict detection prevents overlapping slots at the same location. Re-evaluate only if a per-AppointmentType slot model is introduced. | NO-OP | observation |

Currently working on: **awaiting next E2E pass**. The original linear list (0.1-7.4 + 5.3b + 1.6 + D.1 + D.2) is fully closed on disk -- 0.1-4.3 are E2E-VERIFIED; 4.4-7.4 + 5.3b + 1.6 + D.1 + D.2 are DONE on disk with E2E verification pending. D.3 is observation-only (no code). Adrian's clarification of the registration architecture (2026-04-30) reframes external register as strictly tenant-scoped via portal links + admin invites; internal users are now created via ABP Identity > Users (no internal self-register page). Real SMTP active via Mailtrap sandbox -- emails will route through it for E2E testing. **Next-pass priority is browser walkthrough of every PENDING-E2E row + the Findings-to-track-next list below.**

---

- **Date:** 2026-04-29 (run executed 2026-04-29 / 2026-04-30 UTC; verification rerun 2026-04-30)
- **Branch:** `feat/mvp-wave-2`
- **Commit:** `bc5de49` (initial run); 6 working-tree files modified for fixes (uncommitted at verification time)
- **Stack:** ABP Commercial 10.0.2 / .NET 10 / Angular 20
- **Tester:** Claude Code (QA + diagnostic engineer)
- **Browser MCP:** Playwright MCP (`mcp__plugin_playwright_playwright__*`)
- **Environment:** Docker Compose stack at `W:\patient-portal\main`. All 5 services up + healthy + `db-migrator` exited 0.

---

## E2E pass results -- 2026-05-01 (post-fix verification)

Clean rebuild (volumes wiped + builder cache pruned). 2 tenants (`Dr Rivera 2`, `Dr Thomas 1`) created via host-admin SaaS UI. db-migrator restarted -> D.1 user seeder ran cleanly: 9 internal users created (1 host + 4 per tenant), Doctor entities re-linked. Mailtrap SMTP configured by Adrian as the dev sender.

| Step | Verification status | Evidence |
|---|---|---|
| 1.6 (cookie-only path) | **VERIFIED** | `/Account/Register` after switching to Dr Rivera 2 shows blue banner "Registering for the selected practice..." + locked role-Patient + minimal 4-input form. Screenshot: `register-page-1.6-fix.png`. |
| 1.6 (no-tenant path) | **VERIFIED** | Cleared cookies + reloaded `/Account/Register`. Red banner "Tenant required..." + form opacity dropped + register button disabled. Screenshot: `register-no-tenant.png`. |
| 1.6 (query-string path) | **VERIFIED** | `/Account/Register?__tenant=Dr%20Rivera%202&email=...&role=Applicant%20Attorney`. Banner showed actual tenant name "Dr Rivera 2". Role pre-filled to "Applicant Attorney". User name + email pre-filled. Screenshot: `register-query-prefill.png`. |
| D.1 admin login | **VERIFIED** | `admin@dr-rivera-2.test` / `1q2w3E*` logs in via OIDC code-flow. `/api/abp/application-configuration` reports `userName=admin@dr-rivera-2.test`, `roles=['admin']`, `tenant=Dr Rivera 2`, **141 granted policies** including `Dashboard.Tenant`, `AppointmentDocuments.Approve`, `AppointmentPackets.Regenerate`, `AppointmentChangeLogs`, all `.Delete` flavors. |
| D.1 user-role seeding | **VERIFIED via db-migrator log** | `[01:37:48 INF] InternalUsersDataSeedContributor: created user admin@dr-thomas-1.test / supervisor@... / staff@... / doctor@...` x 2 tenants. `re-linked Doctor entity ... to user doctor@dr-thomas-1.test` and `... to user doctor@dr-rivera-2.test`. |
| D.1 idempotency | **VERIFIED** | Doctor entity backed by SaaS tenant-creation flow has its IdentityUserId successfully re-keyed by the seeder. Re-running db-migrator does not duplicate. |
| D.2 invite endpoint | **VERIFIED** | `POST /api/app/external-users/invite` from `/users/invite` UI returned 200 in 74ms with body `{ inviteUrl, emailEnqueued: true, email, roleName, tenantName }`. Hangfire fired `SendAppointmentEmailJob` -- API logs: `[01:48:45 INF] SendAppointmentEmailJob: delivered (Invite/Applicant Attorney/<tenantId>) to qa.invitee.aa.20260501@hcs.test`. Screenshots: `invite-page-load.png`, `invite-result.png`. |
| D.2 UI page | **VERIFIED** | Yellow DEV-ONLY banner rendered. Email + Role form. Role dropdown limited to Patient / Applicant Attorney / Defense Attorney / Claim Examiner. Internal-roles helper text present. Invite-created green panel with copy-link button + emailEnqueued indicator. |
| 5.7 NullEmailSender gate | **VERIFIED** | API log `delivered ... to qa.invitee.aa.20260501@hcs.test` proves the real MailKit sender ran (NullEmailSender would have logged neither delivered nor failed). Mailtrap credentials are non-`REPLACE_*` so the gate correctly opted in to real SMTP. |
| 6.1 fan-out plumbing | **PARTIAL** | The invite path's `SendAppointmentEmailJob` enqueue + delivery confirms the Hangfire pipeline + per-recipient args + URL generation. Per-recipient template branching for the booking submission path was **not** exercised end-to-end because no appointment was booked in this pass (booking flow needs slot data + patient register flow which were skipped to focus on today's fixes). Adrian to test booking flow himself per his note. |
| 7.4 slot UX | **TRACKING** | Tested all 3 W-UI-11 paths (inverted dates, inverted times, zero-duration). All three are caught by **server-side validation** with global error modals before the client-side `updateConflictState()` ever runs. My 7.4 inline message is dead code in current behavior (defensive only). See W-NEW-8. |
| Mailtrap delivery | **IMPLICITLY VERIFIED** | The "delivered" API log line proves SMTP completed `250 OK`. Adrian's Mailtrap inbox should have the invite email visible. |
| Slots / appointment booking workflow | **NOT TESTED IN THIS PASS** | Scoped out -- requires full Doctor profile (M2M AppointmentTypes + Locations) + slot generation + Patient register + booking form. PUT request to seed Doctor M2Ms surfaced W-NEW-6, which has been **fixed** in the same pass. Adrian to E2E this himself in a follow-up. |

---

## Findings to track in next E2E pass

Newly-surfaced items that need attention but were NOT fixed in this pass. The E2E walkthrough should also confirm or reject each, and add new rows below as it surfaces additional issues.

| ID | Finding | Severity | Notes |
|---|---|---|---|
| **W-DOC-1** | Doctor row-level "own appointments only" filter not enforced. The new `Doctor` role grants `Appointments.Default` (read), but a doctor user currently sees every appointment in the tenant -- not just the ones where the booked DoctorAvailability belongs to them. | gap (post-MVP / hardening) | The product intent is one-doctor-per-tenant, so on a single-doctor tenant this is moot. On a future multi-doctor tenant the AppointmentsAppService must filter by `Doctor.IdentityUserId == CurrentUser.Id` (the seeded link from D.1's `InternalUsersDataSeedContributor.LinkDoctorEntityAsync` is the join key). Defer until multi-doctor tenants are spec'd. |
| **W-INVITE-2** | Invite emails currently reuse `SendAppointmentEmailJob` with an "Invite" Context tag. The job's docstring claims appointment-only delivery; if a future cleanup splits the email pipeline by domain, this needs a parallel `SendInviteEmailJob`. | tracking | Pure refactor when the time comes; the link-based invite flow works today regardless. |
| **W-7.1b** | Broader i18n sweep (W-A-5) -- the explicit 6 slot-form keys (`Enum:BookingStatus.8/9/10`, `SetAvailabilitySlot`, `SlotByDates`, `SlotByWeekdays`) are present in `en.json`, but the demo audience may surface other raw `'::Key' \| abpLocalization` references that resolve to no entry. | tracking | Open per concrete miss. Spot-checks of appointment-add and view templates passed. |
| **W-INTERNAL-1** | Internal-user creation path is now ABP Identity > Users (per Adrian Q1 / Q-D2-c clarification). The legacy "Login -> switch tenant -> Register" flow that 1.2/1.3 ships still works for the demo but is conceptually superseded for internal users. Should be explicitly documented + the legacy path soft-deprecated when subdomain pivot ships. | tracking | Wait for subdomain decision (`docs/plans/2026-04-28-tenant-subdomain-architecture-study.md`). |
| **W-NEW-1** | (E2E 2026-05-01) The 1.6 register-page banner says "Registering for the selected practice" instead of the actual tenant name when the context comes only from the cookie (no `?__tenant=` query string). The tenant name IS visible in the LeptonX header above the form, so it's not a blocker, but the banner could resolve cookie-GUID -> name for consistency. | UX polish | Add a host-context anonymous tenant-by-id endpoint (sibling of the existing `/resolve-tenant?name=`) and call it from `applyTenantBanner` when the context resolved was cookie-only. Or accept the current "selected practice" copy as good-enough since the LeptonX header already shows the name. |
| **W-NEW-2** | (E2E 2026-05-01) Multiple Angular pages render raw breadcrumb / nav i18n keys: `Menu:Home`, `Menu:DoctorManagement`, `DoctorAvailabilities`, `Doctors`. These should resolve to the localized labels via the `'::Key' \| abpLocalization` pipe. The keys exist in `en.json` (`Menu:Home`, `Menu:DoctorManagement`, etc.) but are not being looked up properly -- likely a route metadata vs. i18n-key mismatch. | bug (i18n gap) | Likely a `data: { breadcrumb: 'Menu:Home' }` shape vs. the `RoutesService` registered breadcrumb key. Audit `route.provider.ts` files in each feature folder. Subsumed by 7.1b (broader sweep). |
| **W-NEW-3** | (E2E 2026-05-01) Tenant-admin (and presumably all internal roles) lands on a near-empty home page after login -- no welcome banner, no quick-action tiles, no useful navigation prompts. The "Appointment Scheduling Portal" CTA card visible to anonymous users disappears entirely once authenticated as an internal role. | UX gap | Decide what the internal home should look like (dashboard preview, recent activity, quick links). Currently `/dashboard` is the right destination for staff -- if so, redirect `/` to `/dashboard` for internal roles, OR populate `/` with a dashboard-style home. |
| **W-NEW-4** | (E2E 2026-05-01) `/doctor-management/doctors` list-page Gender column renders raw `Enum:Gender.1` instead of "Male". The Doctor list grid template is binding the raw enum value rather than the `'::Enum:Gender.' + value \| abpLocalization` pattern used on the booking form. | bug (i18n gap) | Audit the doctor-list template and apply the standard enum-localization binding. Subsumed by 7.1b. |
| **W-NEW-5** | (E2E 2026-05-01) `/doctor-management/doctor-availabilities/generate` page title shows raw `SetAvailabilitySlot` (not "Set Availability Slot") and the BookingStatusId option labels show raw `Enum:BookingStatus.X`. The keys ARE present in `en.json` -- this is a binding/lookup-resolution miss, not a missing key. | bug (i18n gap) | Likely the slot-generate template uses `{{ 'SetAvailabilitySlot' \| abpLocalization }}` (no `::` prefix scoping it to the project's localization namespace). Audit the template. Subsumed by 7.1b. |
| **W-NEW-6** | (E2E 2026-05-01) `Doctor.Email` retained the original tenant-admin email after D.1's re-link, causing UI-side Doctor PUTs to fail with `DuplicateEmail`. **FIXED in same E2E pass** -- `InternalUsersDataSeedContributor.LinkDoctorEntityAsync` now updates `Doctor.Email = doctorUser.Email` alongside the IdentityUserId re-link, and the early-return guard added an emailMatches check so existing rows get the email backfilled on the next DbMigrator run. | FIXED (2026-05-01) | Verified by re-running db-migrator post-patch; Doctor row's Email column now matches `doctor@<tenantSlug>.test`. PUTs from the Doctor edit modal succeed. |
| **W-NEW-7** | (E2E 2026-05-01) After a clean volume wipe, `db-migrator` runs ONLY the host-side seed (creating `it.admin@hcs.test`) because no tenants exist yet. Per-tenant internal users are seeded only after tenants are manually created via the SaaS UI and then `db-migrator` is restarted. | UX/onboarding gap | Two options: (a) extend `SaasDataSeedContributor` to seed the 2 demo tenants when none exist (auto-bootstrap); (b) document the manual two-step procedure prominently in `docs/database/DATA-SEEDING.md` and the workflow guide. Current workflow guide does mention this in passing. |
| **W-NEW-8** | (E2E 2026-05-01) W-UI-11's premise of "silent 0-slot" was inaccurate -- the server's slot-validation already returns 400 with explicit "To date must be greater than or equal to from date." / "To time must be greater than from time." messages that the Angular global error handler shows as a confirmation modal. My 7.4 client-side fallback `validationMessage` is therefore dead code in the current behavior. | tracking (positive finding) | Either (a) remove the 7.4 branch as redundant, or (b) keep it as a defensive safety net for future server-side changes that might allow 0-slot returns. The client-side message DOES provide better UX (inline vs. global modal), so option (c) is to broaden the client-side check to fire on the 400 responses too -- but that's scope creep. Leaving the code in as inert; reopen if a real silent-0-slot path appears. |
| **W-NEW-9** | (E2E 2026-05-01) Logging into AuthServer `/Account/Login` directly (not via Angular's OIDC flow) authenticates the user but does NOT issue an OAuth code back to localhost:4200 -- so navigating to localhost:4200 afterward keeps the OLD localStorage tokens. This is observable as: I log in as `admin@dr-rivera-2.test` but the Angular app reports `currentUser.userName=admin` (the previous host admin). | UX gap (development only) | Document: always start login from the Angular "Login" button on `localhost:4200` so the OIDC code-flow runs end-to-end. Direct `/Account/Login` access is a shortcut that breaks the SPA token-refresh path. Possibly add a one-line note on the login page itself. |

---

## Archive: Wave 2 pre-pass research and findings (Rounds 3-6)

The sections below are preserved for reference. They captured the rounds of investigation, finding catalogues, and fix-order discussions that produced the linear progress table at the top of this document. **Do not edit these sections during the next E2E pass** -- new findings go into the "Findings to track in next E2E pass" table above; new lifecycle items (if any) get new rows in the linear progress table.

## Verification rerun -- 2026-04-30 results

After applying fixes for Findings 1, 5, 7, 8, and 10 (Findings 2, 3, 9, 11 deferred / non-code; Findings 4, 6 to be retriaged based on rerun results), Docker reinstalled, full stack rebuilt from clean volume, tenants and minimal demo data reseeded via host UI. Verification results below; full original report sections follow unchanged.

| Finding | Result | Evidence |
|---|---|---|
| 1 (CORS) | **FIXED** | OPTIONS preflight returns 204 with `Access-Control-Allow-Origin: http://localhost:44368`. Register page loads with 0 console errors, tenant dropdown displays correctly, registration POST succeeds (HTTP 200 redirect to /Account/Login). New user `qa.patient.20260430.1@hcs.test` created with role Patient and Patient domain entity. |
| 5 (WCAB Mapperly) | **FIXED** | `GET /api/app/appointment-injury-details/wcab-office-lookup` returns 200 with 7 WcabOffice entries each carrying a `displayName` ("WCAB Anaheim", etc.). API logs show no Mapperly exception; previous 500 was the AbpException for missing object mapping. |
| 7 (sticky toast) | **FIXED (implicit)** | Across 50+ clicks during the rerun, no Playwright "subtree intercepts pointer events" error from `<abp-confirmation>` was observed. The pointer-events SCSS override lets clicks pass through the wrapper while preserving toast/modal content interactivity. |
| 8 (slot dedup) | **FIXED** | Generated 8 slots at HCS Demo Clinic North 2026-05-04 10:00-12:00, then attempted an overlapping block at HCS Demo Clinic South for the same date+time. Submit was enabled, slots saved, no "Some generated slots already exist" warning. Same-location duplicate paths still blocked correctly. |
| 10 (Distinct lookups) | **FIXED** | Booking form's AppointmentType dropdown shows exactly 2 entries ("Qualified Medical Examination (QME)", "Record Review") and Location dropdown shows exactly 2 entries ("Demo Clinic North", "Demo Clinic South") -- one per Doctor->X edge. Pre-fix would have shown duplicates one-per-edge. |
| 4 (`/patients/me` 404) | **NO FIX NEEDED** | Once Finding 1 unblocked the proper register flow, `RegisterAsync` correctly creates a Patient entity and `/patients/me` returns 200. The 404 only reproduces via the host-admin Identity Users workaround, which is no longer the primary onboarding path. Recommendation in section 4 stands as documentation-only; do not ship the get-or-create code fix unless host-admin direct user creation becomes a supported onboarding path. |
| 6 (Angular `_rawValidators`) | **STILL REPRODUCES -- INDEPENDENT BUG** | After Finding 5 fixed the WCAB lookup 500, the modal still emits 8 `Cannot read properties of null (reading '_rawValidators')` errors when clicking Claim Information's Add. Verified with admin user where ALL lookups return 200. Confirms Finding 6 is NOT a cascade from Finding 5; the modal's Angular FormGroup setup has its own structural issue. Needs separate investigation. |

### New issues surfaced by the rerun

These were latent under the original report's Finding 3 (over-broad Patient permissions) and only became visible once that grant was reset:

- **Patient role lookup-policy gaps.** Strictly-scoped Patient role gets 403 on `field-configs/by-appointment-type`, `applicant-attorneys/state-lookup`, and `appointment-injury-details/wcab-office-lookup`. Booking form needs these for a Patient booker to fill the form. Permission scoping in `ExternalUserRoleDataSeedContributor.cs` is too tight for the booking flow.
- **Saas tenant creation does not assign admin role to new tenant admin user.** Creating a tenant via the UI's "+ New tenant" form (admin email + password) creates the IdentityUser in the new tenant scope but with **0 roles assigned**. The user can authenticate but has no permissions. Workaround: use host admin's "Login with this tenant" feature which always lands as the implicit admin. This is a likely fork-specific deviation from stock ABP SaaS behavior.
- **WCAB Office dropdown renders blank option text** despite API returning correct `displayName`. Suggests the Angular template binds `option` text from a different property than `displayName` (probably `name` directly), and the typeahead component does not see the LookupDto's standard shape. Worth a separate finding.
- **`/patients/me` 404 also fires for non-Patient role users** (admin booking on behalf). The booking form calls `/patients/me` regardless of who is booking; should be gated by role.

### Working-tree changes summary (all on `feat/mvp-wave-2`, uncommitted at verification)

- `docker-compose.yml` -- Finding 1 (1 line)
- `src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationMappers.cs` -- Finding 5 (+13 LOC, new mapper class)
- `src/HealthcareSupport.CaseEvaluation.Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` -- Finding 8 (+/-21 LOC)
- `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/CLAUDE.md` -- Finding 8 docstring update (+/-13 LOC)
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs` -- Finding 10 (+10 LOC)
- `angular/src/styles.scss` -- Finding 7 (+21 LOC)

Total: 6 files, 79 lines added/modified (+/-58), 1 line deleted. Net 5 of 7 in-scope findings fixed; F4 retired as not-applicable; F6 escalated to its own investigation ticket.

---

## Workflow runs (2026-04-30)

After the verification rerun, two end-to-end demo workflows were executed: Workflow A (Patient as booker) and Workflow B (Applicant Attorney as booker). Both used the now-working `/Account/Register` self-register flow, fresh tenant `Dr Rivera 2`, doctor "Dr Rivera 2 Rivera" wired to all 6 AppointmentTypes (QME, Panel QME, AME, Record Review, Deposition, Supplemental Medical Report) and 2 Locations (Demo Clinic North/South), and 24 DoctorAvailability slots covering all 6 types across 2026-05-04, 2026-05-05, 2026-05-06.

### Workflow A -- Patient as booker -- summary

- **Register:** PASS. `qa.patient.workflow-a@hcs.test` registered via Login -> switch tenant -> Register flow. Patient domain entity auto-created.
- **Login:** PASS. Patient lands on the W2 home with role badge.
- **Booking form (most of it):** PASS. AppointmentType + Location dropdowns populated cleanly (Finding 10 holds). Patient Demographics, Employer, Applicant Attorney, Defense Attorney all fillable with Marcus Whitfield demo data. AppointmentDate + Time slot picker worked (8 slots populated for 2026-05-04 10:00-12:00 at HCS Demo Clinic North).
- **Claim Information modal:** BLOCKED. Modal opens but the inner Add button does not close the modal nor add the claim row to the parent form. WCAB Office dropdown is empty for the Patient role due to a 403 on `wcab-office-lookup` (separate finding W-A-3 below). Form submits without a claim attached (no validation block).
- **Submit:** PASS-WITH-DATA-LOSS. POST `/api/app/appointments` returns 200 and creates appointment `A00001` for Marcus Whitfield, QME, Demo Clinic North, 2026-05-04 10:00 AM. Defense Attorney section was filled but **does not appear in the saved appointment view** (data loss; see W-A-7).
- **Email fan-out:** ONLY 1 of 6 expected recipients. `SendAppointmentEmailJob` fired exactly once (Job #1) with `To: qa.patient.workflow-a@hcs.test`, `Role: 1` (Patient). No emails to the Applicant Attorney (Helena Vargas), Defense Attorney (Brent Locke), Claim Examiner, or Office mailbox. W2-10 fan-out is regressed or never wired for non-self recipients. See W-A-2.
- **Tenant admin queue:** PASS-WITH-DEMO-KILLERS. Appointment row shows in `/appointments` queue. Status renders raw `Enum:AppointmentStatusType.1` (i18n key not resolved). Patient column shows the booker's email instead of "Marcus Whitfield". Actions dropdown contains **only Edit + Delete -- no Review item** despite commit `bc5de49` allegedly adding it. Workaround: navigate directly to `/appointments/view/<guid>`. See W-A-5, W-A-6.
- **Review view:** Heading reads "PQME Appointment > Marcus Whitfield" -- "PQME" is an OLD-code enum label, not the newer "QME" that was actually selected. Action dropdown options render as raw i18n keys: `Appointment:Action:ChooseAction`, `Appointment:Action:Approve`, `Appointment:Action:Reject`, `Appointment:Action:SendBack`, with a `Appointment:Action:Submit` button. The status pill in the page header reads `Enum:AppointmentStatusType.1`. Claim Information section is **entirely absent** from the view (consistent with the form-side modal not adding the row). Defense Attorney section is **also absent**. See W-A-4, W-A-5, W-A-6, W-A-7, W-A-8.
- **Approve:** PASS. Picking "Appointment:Action:Approve" + Submit triggers a confirmation modal (also raw i18n) -> clicking Approve flips status 1 -> 2 (Approved), sets `appointmentApproveDate`, fires Job #2 (another single-recipient email, same Role: 1 to Patient only -- still no fan-out), and schedules Job #3.
- **Approval email fan-out:** ONLY 1 of 6 expected recipients (same as submission email). See W-A-2.
- **Packet generation:** FAIL. `GenerateAppointmentPacketJob` (Job #3) is enqueued but every retry throws `System.ObjectDisposedException: Cannot access a disposed context instance` from `InternalDbSet<...>.IQueryable.get_Provider()` at `Domain/AppointmentDocuments/AppointmentPacketManager.cs:37` -> `Domain/AppointmentDocuments/Jobs/GenerateAppointmentPacketJob.cs:102, 93`. Hangfire keeps the job in the Scheduled queue indefinitely. `GET /api/app/appointments/<id>/packet` returns 204. **No packet PDF is ever produced.** See W-A-9.
- **Hidden SMTP failure:** Backend logs reveal `System.Net.Mail.SmtpException: 5.7.3 Authentication unsuccessful` for every email send attempt. Hangfire still marks the jobs as Succeeded (caught/swallowed downstream of the SMTP send). The "Hangfire-as-inbox" observation surrogate is confirming queueing only, not actual delivery. See W-A-10.

### Workflow B -- Applicant Attorney as booker -- summary

- **Register via direct `/Account/Register` (with explicit Tenant + Role dropdowns):** SILENT FAILURE. Picking Dr Rivera 2 + Applicant Attorney + filling username/email/password + clicking Register did not create the user (subsequent login returned "Invalid username or password"; `/api/identity/users?Filter=qa.attorney` returned 0 hits). Antiforgery / form-handler issue specific to the standalone Register page. See W-B-1.
- **Register via Login -> switch tenant -> Register link:** PASS. Same form, different entry path -- registration succeeded and `qa.attorney.workflow-b@hcs.test` was created with role `Applicant Attorney`.
- **Login:** PASS. AA lands on `/` with role badge `Applicant Attorney`.
- **Booking form:** PARTIAL. AA section auto-populates with the booker's IdentityUser values (`firstName: "qa.attorney.workflow-b@hcs.test"`, `lastName: "User"`, `email: "qa.attorney.workflow-b@hcs.test"`) -- the auto-population logic works as the prompt expects, but the underlying defaults (email-as-firstName, "User"-as-lastName) are the same hardcoded leak as Workflow A.
- **`/api/app/patients/me` 404:** REPRODUCES for AA role. The booking form calls `/patients/me` regardless of booker role, and for non-Patient external users the API throws `EntityNotFoundException(Patient, identityUserId)`. The proper endpoint for AA's profile is `/api/app/external-users/me` which returns 200. See W-B-2.
- **Lookup permission gaps:** Same set of 403s as Patient role -- `field-configs/by-appointment-type`, `applicant-attorneys/state-lookup`, `wcab-office-lookup` -- because the strictly-scoped `Applicant Attorney` role lacks the corresponding CaseEvaluation lookup permissions. See W-A-3 (same root cause).
- **Booking + approve + packet:** NOT EXECUTED. Form-side issues from Workflow A reproduce identically here; the new diagnostic value would be marginal. The remaining lifecycle steps (form submit, queue review, approve, packet) all reduce to W-A-2 / W-A-9 / W-A-10 once submission would succeed.

### Findings from the workflow runs

#### W-A-1: Booking form auto-fills firstName from email and lastName from hardcoded "User" string
- **Severity:** bug (demo-killer; visible in every email body, every appointment row, every search result)
- **Symptom:** New external users register with `firstName = "<email>"` and `lastName = "User"` because `ExternalSignupAppService.RegisterAsync` passes `input.FirstName` / `input.LastName` straight from the registration form, but the AuthServer's `Account/Register` page does not collect first/last name -- it only collects username/email/password. Either the AuthServer client-side code is hardcoding `lastName: "User"` and using `userName` as `firstName`, or the AppService should derive these defaults differently.
- **Suspect files:** `src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/Account/Register*.cshtml*`, `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs:170-225`.
- **Recommended fix:** Add First Name + Last Name fields to the Register page UI and require them; OR change the post-register experience to immediately route the user to a profile-completion form before they can book.

#### W-A-2: Email fan-out fires only to the booker; no AA / DA / Examiner / Office emails
- **Severity:** blocker (W2-10 regressed or never wired)
- **Symptom:** Both submission (Job #1) and approval (Job #2) `SendAppointmentEmailJob`s fire with exactly one `To` recipient -- the booker. The form clearly captured the AA email (helena.vargas@aaattorney.test), DA email (brent.locke@dlaw.test), and the booker's own (qa.patient.workflow-a@hcs.test). Per-recipient `Role` field on the args was Role=1 for all jobs.
- **Hypothesis:** The fan-out "resolve recipients then enqueue one job per recipient" loop is missing or short-circuited. It may be feeding only `CurrentUser.Email` rather than walking the appointment's AA/DA/CE/Office relationships.
- **Suspect files:** `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Notifications/AppointmentSubmittedHandler.cs` (or equivalent event handler), the W2-10 fan-out wiring -- search for `SendAppointmentEmailJob` enqueue sites.
- **Recommended fix:** Verify the recipient resolution: should produce one email per (booker, patient, AA, DA, claim examiner, office) when each is set on the appointment.

#### W-A-3: Patient and AA roles cannot fill the booking form due to 403s on lookup endpoints
- **Severity:** blocker (booker can never produce a valid appointment with WCAB)
- **Symptom:** `wcab-office-lookup`, `field-configs/by-appointment-type`, and `applicant-attorneys/state-lookup` all return 403 for Patient and Applicant Attorney roles. The Claim Information modal's WCAB Office dropdown stays empty. Field-configs for the chosen AppointmentType never load.
- **Hypothesis:** `[Authorize(<EntityName>.Default)]` policies on the lookup methods are too restrictive. External roles need read-only access to lookups even though they cannot CRUD the underlying entities.
- **Suspect files:** `src/HealthcareSupport.CaseEvaluation.Domain/Identity/ExternalUserRoleDataSeedContributor.cs`, the `[Authorize]` attributes on `GetWcabOfficeLookupAsync` (`AppointmentInjuryDetailsAppService.cs:67`), `GetStateLookupAsync` on `ApplicantAttorneyAppService`, and `GetByAppointmentTypeAsync` on `AppointmentTypeFieldConfigsAppService`.
- **Recommended fix:** Either (a) downgrade these lookup methods to `[Authorize]` (any authenticated user) since they return only IDs + display names, or (b) seed read-only versions of the relevant `.Default` policies for Patient / Applicant Attorney / Defense Attorney / Claim Examiner roles.

#### W-A-4: Booking form's Claim Information modal Add button does not commit the claim row
- **Severity:** blocker (Claim Number, Body Parts, WCAB selection are required for the demo's Stage 6+ but never make it to the appointment)
- **Symptom:** All required fields (Date Of Injury, Claim Number, Body Parts) are filled and `ng-valid`. Insurance and Claim Examiner sub-toggles are off. Add button is enabled but clicking it does not close the modal nor add a row to the parent form's Claim Information table. No console error, no network request fires from the click. Reproduces with both JS-injected click and real Playwright click.
- **Hypothesis:** The component's Add handler depends on a state derived from a 403'd lookup (Finding W-A-3). Even with required fields valid, an internal precondition (e.g. `wcabOfficeId` being set OR a mapped attorney loaded) keeps the handler short-circuited.
- **Suspect files:** `angular/src/app/appointments/.../claim-information.component.ts` (the add-claim handler), `angular/src/app/appointments/.../appointment-add.component.ts` (the parent's claims array push).
- **Recommended fix:** First land W-A-3 to make the lookup populate. If the modal still doesn't close, audit the Add handler for early-return conditions and add a console.warn or visible toast when the handler is no-op.

#### W-A-5: i18n keys render raw across the queue, view page, and approval modal
- **Severity:** bug (demo-killer for live audience)
- **Symptom:** Multiple visible strings render their L10n key instead of the translated text:
  - `Enum:AppointmentStatusType.1` in the queue's status column and the view-page header
  - `Enum:Gender.1`, `Enum:Gender.2`, `Enum:Gender.3` next to the gender radios
  - `Enum:PhoneNumberType.28`, `Enum:PhoneNumberType.29` next to the phone-type radios
  - `Appointment:Action:ChooseAction`, `Appointment:Action:Approve`, `Appointment:Action:Reject`, `Appointment:Action:SendBack`, `Appointment:Action:Submit`, `Appointment:Action:ViewChangeLog`, `Appointment:Action:Cancel`
  - `Appointment:Modal:ApproveTitle`, `Appointment:Modal:ApproveBody`
- **Hypothesis:** Missing `en.json` localization entries OR the Angular template is using `'Enum:Gender.1'` as a literal instead of binding through the `abpLocalization` pipe.
- **Suspect files:** `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`, the templates that bind these strings.

#### W-A-6: Appointments queue Actions dropdown is missing the Review item
- **Severity:** blocker (the prompt explicitly calls out commit `bc5de49` as adding Review)
- **Symptom:** Actions dropdown on the queue row shows only Edit + Delete. No Review item. The view page IS reachable directly via URL (`/appointments/view/<guid>`), so the route exists; only the dropdown wiring is missing.
- **Suspect files:** `angular/src/app/appointments/appointment/components/appointment.abstract.component.ts` (the abstract base that defines the row Actions dropdown), the routes definition.
- **Recommended fix:** Add a "Review" entry to the row action items pointing at `/appointments/view/<id>`.

#### W-A-7: Defense Attorney + Claim Information data submitted from the booking form do not appear on the view page
- **Severity:** bug (data exists but is not surfaced; office reviewer sees an incomplete record)
- **Symptom:** The booking form's Defense Attorney section was fully filled (Brent Locke + 9 fields). The view page header includes Applicant Attorney Details but no Defense Attorney Details section is rendered. Same for Claim Information (although for that one the modal-add bug means the data wasn't actually saved -- so this is a downstream consequence of W-A-4).
- **Hypothesis:** The `/api/app/appointments/<id>/with-navigation-properties` response may not include the AppointmentDefenseAttorneys or AppointmentInjuryDetails relations, OR the view template's *ngIf gates them on a missing flag.
- **Suspect files:** `angular/src/app/appointments/.../appointment-view.component.html`, the ApplicationService method that loads the detail DTO.

#### W-A-8: View page heading uses "PQME" branding for QME bookings
- **Severity:** cosmetic
- **Symptom:** Selecting "Qualified Medical Examination (QME)" for the booking and approving it produces a heading "PQME Appointment > Marcus Whitfield" on the view page. PQME was the OLD code's enum label (`PatientPortalOld/PatientAppointment.Models/Enums/AppointmentType.cs:11`); this string is leaking into the new UI.
- **Recommended fix:** Replace static "PQME" with the actual selected `AppointmentType.Name` from the appointment record.

#### W-A-9: Packet generation job throws `ObjectDisposedException` (DbContext disposed before first query)
- **Severity:** blocker (no packet PDF can ever be generated -- the entire Stage 8 packet flow fails)
- **Symptom:** Hangfire keeps `Job #3` (`GenerateAppointmentPacketJob`) in the Scheduled queue, retrying. Each attempt throws:
  ```
  System.ObjectDisposedException: Cannot access a disposed context instance...
    at Microsoft.EntityFrameworkCore.Internal.InternalDbSet`1.System.Linq.IQueryable.get_Provider()
    at HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentPacketManager.EnsureGeneratingAsync at Domain/AppointmentDocuments/AppointmentPacketManager.cs:line 37
    at HealthcareSupport.CaseEvaluation.AppointmentDocuments.Jobs.GenerateAppointmentPacketJob.GenerateInsideTenantAsync at Domain/AppointmentDocuments/Jobs/GenerateAppointmentPacketJob.cs:line 102
    at HealthcareSupport.CaseEvaluation.AppointmentDocuments.Jobs.GenerateAppointmentPacketJob.ExecuteAsync at Domain/AppointmentDocuments/Jobs/GenerateAppointmentPacketJob.cs:line 93
  ```
- **Hypothesis:** The job resolves a DbContext (or repository) outside its UoW scope, then awaits an SMTP send (which fails with W-A-10), and by the time control returns to `EnsureGeneratingAsync` the DbContext has been disposed. Or `using` block scoping is wrong on the manager.
- **Suspect files:** `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/Jobs/GenerateAppointmentPacketJob.cs:93,102`, `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/AppointmentPacketManager.cs:37`.
- **Recommended fix:** Open a fresh UoW (`_unitOfWorkManager.Begin()`) inside `ExecuteAsync` and execute all repository work within it, OR scope the repository per-iteration via `IServiceScopeFactory`.

#### W-A-10: SMTP authentication failing in production code path; Hangfire reports false success
- **Severity:** bug (silent failure -- everything looks green in Hangfire while no email actually delivers)
- **Symptom:** Backend logs every send attempt with:
  ```
  System.Net.Mail.SmtpException: The SMTP server requires a secure connection or the client was not authenticated.
  The server response was: 5.7.57 Client not authenticated to send mail. Error: 535 5.7.3 Authentication unsuccessful.
  ```
- **Hypothesis:** SMTP credentials in `appsettings.secrets.json` are wrong / expired, OR the email pipeline catches and swallows SMTP exceptions before Hangfire learns about them, OR the dev configuration was supposed to use a no-op delivery channel and isn't.
- **Recommended fix:** Confirm intended dev-time email behavior with Adrian. If "Hangfire-as-inbox" is the expected dev model, skip the SMTP call entirely in `Development` environment (use `MailKitMailSender` swap or a `NoOpEmailSender`). If SMTP IS supposed to work, fix the credentials. Either way, do not let SMTP exceptions be swallowed silently -- they should surface as failed Hangfire jobs.

#### W-B-1: Direct `/Account/Register` URL silently fails to register
- **Severity:** bug (one of two register entry points is broken)
- **Symptom:** Browsing directly to `http://localhost:44368/Account/Register`, picking a tenant from the visible Tenant dropdown, picking a role, filling username/email/password, and clicking Register: the page reloads to itself with the form reset and no error or success indication. No user is created in the DB. Reproducing the same form via Login -> switch tenant -> Register link works correctly.
- **Hypothesis:** The standalone `/Account/Register` page either (a) does not get the antiforgery cookie set in the absence of the prior Login redirect chain, (b) the tenant dropdown's value isn't bound to the same form field as the inferred-from-tenant-cookie path, or (c) the form validation silently rejects without rendering an error message.
- **Suspect files:** `src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/Account/Register.cshtml.cs`, `Register.cshtml`.

#### W-B-2: `/api/app/patients/me` 404 fires for non-Patient role bookers
- **Severity:** bug (downgraded from a Workflow A-only finding to general)
- **Symptom:** When a booker with role `Applicant Attorney` opens the booking form, the form unconditionally calls `/api/app/patients/me` and the server throws `EntityNotFoundException(Patient, identityUserId)`. The form's global error handler then pops up the "An error has occurred!" modal and a 403/404 overlay (W-A-... existing toast finding) is in play.
- **Recommended fix:** Either (a) gate the `/patients/me` call on `roles.includes('Patient')`, or (b) make the call always succeed by returning a draft profile when the IdentityUser is non-Patient.

### Status of email fan-out (across both workflows)

| Stage | Expected recipients | Actual recipients |
|---|---|---|
| Submission (Workflow A) | 6 (booker, patient, AA, DA, CE, office) | 1 (booker only) |
| Approval (Workflow A) | 6 | 1 (booker only) |
| Submission (Workflow B) | not executed | -- |

### Status of approval lifecycle (Workflow A)

| Action | Server-side result | UI result |
|---|---|---|
| Booker Save | `appointmentStatus: 1` (Pending), 1 Hangfire email job | Redirected to home; appointment in queue |
| Tenant admin Approve | `appointmentStatus: 2` (Approved), `appointmentApproveDate` set, 1 more email job, 1 packet job scheduled | View page returns to display; status pill still shows raw `Enum:AppointmentStatusType.1` (likely a stale render -- did not re-fetch) |
| Packet generation | Scheduled, retried indefinitely with `ObjectDisposedException` | "No packet has been generated yet" forever |
| Packet download | `GET /packet` returns 204 | -- |

---

## Round 3: Multi-role + cross-cutting probes (2026-04-30)

After Workflow A and B, executed 17 additional priority-ordered probes covering all internal + external roles, security/HIPAA surfaces, and lifecycle stages we had not yet reached. Findings below in logical groups.

### Round-3 findings (additional)

#### W-X-2: HIPAA-grade leak -- `external-user-lookup` returns full external-user list to anonymous callers
- **Severity:** **blocker** (HIPAA breach)
- **Symptom:** `GET http://localhost:44327/api/public/external-signup/external-user-lookup?__tenant=Dr%20Rivera%202` (no auth header, no cookie) returns a JSON array containing every Patient + Applicant Attorney + Defense Attorney in the named tenant -- with `identityUserId`, `firstName`, `lastName`, `email`, and `userRole`. Anyone who can reach the API can enumerate every external user in every tenant; tenant names are also publicly listable via `/api/public/external-signup/tenant-options`.
- **Reproduction (one-liner):**
  ```
  curl 'http://localhost:44327/api/public/external-signup/external-user-lookup?__tenant=Dr%20Rivera%202'
  ```
- **Suspect file:** `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs:62` -- `GetExternalUserLookupAsync` is missing both `[Authorize]` and `[AllowAnonymous]`. The CLAUDE.md for ExternalSignups already flags this as `Gotcha #6`. Matching controller route at `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/ExternalSignups/ExternalSignupController.cs` (`api/public/external-signup/external-user-lookup`) inherits the no-attribute behavior.
- **Recommended fix:** Add `[Authorize]` to `GetExternalUserLookupAsync`. The booker-form's "Or select from list" feature that consumes this endpoint should be moved to an authenticated `/api/app/external-signup/...` route.

#### W-I-1: `Clinic Staff` role permissions are insufficient to do office work
- **Severity:** blocker (Clinic Staff is the actual office persona for the demo, not the seeded `admin`)
- **Symptom:** Granted `CaseEvaluation.*` policies for `Clinic Staff` are 13: read-only on lookups + Appointments(Default/Create/Edit) + Patients(Default/Create/Edit) + Dashboard.Tenant. The role has **no AppointmentDocuments perms**, **no AppointmentPackets perms**, **no Approve action perm**, **no audit-log access (`AppointmentChangeLogs`)**, and **no `.Delete` perms**. A Clinic Staff member cannot upload + approve documents, cannot regenerate the packet, and cannot inspect audit history.
- **Suspect file:** `src/HealthcareSupport.CaseEvaluation.Domain/Identity/CaseEvaluationRoleDataSeedContributor.cs` (or wherever the tenant-side roles get their grant set seeded).
- **Recommended fix:** Decide which actions Clinic Staff should be able to perform and seed the matching policy set; minimally add `AppointmentDocuments` (read + Approve), `AppointmentPackets` (read + Regenerate), `AppointmentChangeLogs`.

#### W-I-2: `Staff Supervisor` role missing critical supervisory powers
- **Severity:** blocker
- **Symptom:** Granted 50 CaseEvaluation policies but **no `AppointmentDocuments.*` (so cannot Approve docs)**, **no `AppointmentPackets.*` (so cannot Regenerate)**, **no `AppointmentChangeLogs` (so cannot view the audit log)**, and **no `.Delete` perms anywhere**. Supervisor cannot do the supervisory things.
- **Suspect file:** Same seeder as W-I-1.
- **Recommended fix:** Mirror the admin-role grant set for clinical workflow but keep system-level policies (`SystemParameters`, `CustomFields.Delete`, etc.) admin-only.

#### W-I-3: `Doctor` role has zero CaseEvaluation policies -- the role is non-functional
- **Severity:** blocker (Doctors cannot use the system at all)
- **Symptom:** Login as a user with role `Doctor` and the application-configuration endpoint reports `grantedPolicies` count for `CaseEvaluation.*` = 0. Cannot read appointments, patients, slots, anything.
- **Suspect file:** Same seeder as W-I-1.
- **Recommended fix:** Define what doctors should be able to do (likely: read own appointments, read patient demographics, generate packet for their own appointments, mark their own slots) and seed accordingly.

#### W-I-4: No `IT Admin` role at tenant level (informational)
- **Severity:** nice-to-have / product-decision
- **Symptom:** The host scope has an `IT Admin` role; tenant scope only has `admin`, `Clinic Staff`, `Staff Supervisor`, `Doctor` + 4 external roles. There is no separate Identity-management persona at the tenant level distinct from the all-powerful `admin`.
- **Recommended:** Decide whether tenants need an Identity-only admin persona. If yes, seed an `IT Admin` tenant role with `AbpIdentity.*` perms but no `CaseEvaluation.*`.

#### W-G-1: Document upload returns 500 due to ABP validator reflecting `Stream.ReadTimeout`
- **Severity:** blocker (no documents can ever be uploaded -- breaks Stage 8 entirely)
- **Symptom:** `POST /api/app/appointments/<id>/documents` with a `multipart/form-data` body (Document Name + File) returns 500 with the inner exception
  ```
  Property accessor 'ReadTimeout' on object 'Microsoft.AspNetCore.Http.ReferenceReadStream' threw the following exception: 'Timeouts are not supported on this stream.'
  System.InvalidOperationException: Timeouts are not supported on this stream.
    at System.IO.Stream.get_ReadTimeout()
    at Volo.Abp.Validation.DataAnnotationObjectValidationContributor.ValidateObjectRecursively
    at Volo.Abp.Validation.ObjectValidator.GetErrorsAsync
    at Volo.Abp.Validation.MethodInvocationValidator.AddMethodParameterValidationErrorsAsync
    ...
    at HealthcareSupport.CaseEvaluation.Controllers.AppointmentDocuments.AppointmentDocumentController.UploadAsync at HttpApi/Controllers/AppointmentDocuments/AppointmentDocumentController.cs:line 43
  ```
- **Hypothesis:** ABP's `DataAnnotationObjectValidationContributor` reflects every property on the input DTO (`UploadAppointmentDocumentForm`), and one of its properties is an `IFormFile` whose underlying `Stream` returns `ReferenceReadStream` -- whose `ReadTimeout` getter throws because the stream type does not support timeouts. The validator should skip stream-typed properties or catch reflection exceptions.
- **Suspect files:** `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/AppointmentDocuments/AppointmentDocumentController.cs:43` (controller method); `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentDocuments/UploadAppointmentDocumentForm.cs` (DTO with the IFormFile property and `[Required]`/`[StringLength]` attributes that trigger validation).
- **Recommended fix:** Decorate the form DTO class with `[DisableValidation]` and validate manually inside `UploadAsync`, OR add `[ValidationIgnore]` on the file-bearing property.

#### W-X-9: Identity > Users with role `Doctor` does not auto-create the `Doctor` domain entity
- **Severity:** bug
- **Symptom:** Created a user via host-admin Identity > Users with `roleNames: ['Doctor']`. The `Doctor` count remains 1 (the seeded one). Same gap as Patient role had originally (now fixed because RegisterAsync handles it for Patient but no equivalent for Doctor).
- **Recommended fix:** Either (a) hook a `IdentityUserCreatedEventHandler` that detects the role and creates the matching domain entity (Doctor / Patient / etc.), or (b) document that the host-admin Identity Users path is only for non-clinical roles.

#### W-H-1: Re-evaluation booking (`?type=2`) is structurally identical to Initial (`?type=1`)
- **Severity:** bug or product-decision
- **Symptom:** `/appointments/add?type=2` renders the same form as `/appointments/add?type=1` -- same sections, same fields, no Re-eval-specific UI. The OLD code (`P:\PatientPortalOld\PatientAppointment.Models\Enums\AppointmentType.cs`) had `PQMEREEVAL=2` and `AMEREEVAL=4` distinguishing initial from re-evaluation; the NEW code lost the distinction in the form layer.
- **Recommended fix:** Decide whether Re-eval is a separate flow. If yes: surface "Re-evaluation of which prior appointment?" picker, conditional fields, and a different email subject. If no: drop the `?type=2` route or label both as "New Appointment".

#### W-X-4: Edit modal from queue exposes only 4 appointment-level fields
- **Severity:** bug (downgrade of the office workflow -- you can edit the panel number but not fix patient demographics or claim info)
- **Symptom:** Clicking Actions -> Edit on a queue row opens a modal containing only `panelNumber`, `appointmentDate`, `requestConfirmationNumber`, `dueDate`. No Patient Demographics, Employer, AA/DA, or Claim Information sections.
- **Recommended fix:** Either remove the Edit action (since the View page is where editing actually happens) or expand the Edit modal to mirror the booking form so office staff can correct intake errors without going through the full view.

#### W-X-1: Cross-tenant isolation holds for the cases tested (informational)
- **Severity:** PASS
- **Symptom:** A user authenticated to Dr Thomas 1 (with no Clinical roles) attempting to GET Dr Rivera 2 appointment GUIDs via `/api/app/appointments/<guid>`, `/packet`, `/documents`, and `/appointment-change-logs/by-appointment/<guid>` receives 403/404 in all cases. Tenant filter holds. Combined with the original report's result (Rivera admin probing Thomas data also gets 404), cross-tenant isolation is solid.

#### Universal external-role read-block on lookup endpoints
- **Severity:** blocker (subsumes the original Finding W-A-3)
- **Symptom:** Patient, Applicant Attorney, Defense Attorney, and Claim Examiner all receive 0 `CaseEvaluation.*` granted policies. The booking form's `wcab-office-lookup`, `field-configs/by-appointment-type`, and `doctors` calls 403 for all four. The form is unfillable for any external booker.
- **Recommended fix:** Same as Finding W-A-3 (originally Patient-only) -- demote the lookup-method `[Authorize]` policies to authenticated-only, OR seed read-only `.Default` perms for all four external roles on Appointment-touching lookups.

---

## Round 4: Slot Generation + View Page UI Tests (2026-04-30)

Executed 20 targeted UI tests covering slot generation scenarios (W-UI-1 to W-UI-10) and appointment view page behavior (W-VIEW-1 to W-VIEW-10). No code changes were made during this session; findings and diagnosis only.

### Slot generation findings

#### W-UI-1: SlotByWeekdays mode is per-month only -- no multi-month spanning
- **Severity:** bug (UX gap)
- **Symptom:** Using "Slot By Weekdays" mode with a date range spanning multiple months (e.g., 2026-01-01 to 2026-12-31) generates slots only for the month of the selected start date. Remaining months in the range are silently skipped. Covering an entire year with 1 appointment type on 2 specific weekdays requires 12 separate form submissions.
- **Root cause:** The backend `GenerateSlotByWeekdaysAsync` method processes only within the month of the passed `AvailableDate`; the frontend month-range picker implies multi-month coverage but each API call passes a single representative date.
- **Recommended fix:** Either (a) label the Weekdays mode "per-month" to set user expectations, or (b) loop the API call client-side once per calendar month in the selected range so one form submission covers the full span.

#### W-UI-2: Conflict detection is time+location only -- type-blind, blocks adding a second appointment type to an existing time slot
- **Severity:** bug (UX confusion; domain behavior is architecturally correct)
- **Symptom:** With a QME slot at Demo Clinic North 2026-05-04 10:00-11:00, attempting to add a Record Review slot at the same location/date/time shows "Some generated slots already exist" and blocks Submit. Conflict detection uses (Location, Date, FromTime, ToTime) only -- appointment type is intentionally not part of the key because one doctor handles one patient per slot regardless of type.
- **Root cause:** `AppointmentConflictDetectionService` checks time+location overlap only. The error message implies "you already made this slot" which confuses users who expect different appointment types to occupy independent slots.
- **Recommended fix:** UX only. Change the conflict message to: "A slot already exists at this location and time. Each time slot can only be assigned to one appointment type -- remove the conflicting existing slot first or choose a different time."

#### W-UI-3: Partial conflict resolution via sub-slot deletion works but has no affordance
- **Severity:** cosmetic
- **Symptom:** When the generated preview contains conflicting sub-slots, the user can delete individual conflicting rows from the preview table to unlock Submit. This works correctly. However, there is no inline guidance pointing the user to this action; the only visible indicator is the warning banner and a disabled Submit button.
- **Recommended fix:** Add a tooltip or inline hint on each conflicting row: "Conflicts with an existing slot. Delete this row or adjust the time to proceed."

#### W-UI-4: No weekend exclusion option for SlotByDates mode
- **Severity:** bug (UX gap -- most clinics operate Mon-Fri)
- **Symptom:** `SlotByDates` generates slots for every calendar day in the selected range, including Saturdays and Sundays. A Mon-Sun range produces 7 slot-days. The user must manually delete Saturday and Sunday sub-slots from the preview before submitting.
- **Recommended fix:** Add a "Skip weekends" checkbox (default: checked) to the SlotByDates form. When checked, filter Saturday and Sunday from the generated preview before render and before the submit payload.

#### W-UI-5: No holiday awareness -- slot generation proceeds on public holidays without warning
- **Severity:** cosmetic
- **Symptom:** Generating slots on public holidays (e.g., 2026-01-01, 2026-12-25) produces valid `DoctorAvailability` rows with no indication that the date is a holiday. No holiday calendar exists in the system.
- **Recommended fix:** Post-MVP enhancement. Holiday awareness requires a per-tenant configurable holiday calendar. Until built, document that the user is responsible for avoiding holiday dates.

#### W-UI-6: No past-date protection -- slot generation accepts dates in the past without warning
- **Severity:** bug
- **Symptom:** The slot-creation form accepts a date in the past (e.g., 2026-04-01 when today is 2026-04-30) with no warning. Slots are created successfully. Past-date slots are not bookable (the booker form shows only future available slots) but they accumulate in the availability table and inflate slot counts.
- **Recommended fix:** Add a frontend validator: if `AvailableDate < today`, show inline warning "This date is in the past. Past-date slots cannot be booked." Disable Submit when all generated sub-slots are past-dated.

#### W-UI-7: 3 appointment types on one day across multiple months -- SlotByDates works correctly
- **Severity:** PASS
- **Observation:** Generating slots for 3 appointment types at one location across multiple consecutive months using `SlotByDates` mode produces the correct slot count in one submission. No conflicts arise when the time slots are clean. No issues found.

#### W-UI-8: 1 appointment type on two specific weekdays for an entire year -- confirms W-UI-1 limitation
- **Severity:** bug (see W-UI-1 -- same root cause)
- **Symptom:** This test case is the canonical reproduction of the SlotByWeekdays per-month limitation. Covering all of 2026 for Tuesday + Thursday with one appointment type requires 12 separate form submissions. No visual cue on the form indicates that a single submission will not span the full year.

#### W-UI-9: Slot generation on holidays and weekends proceeds without backend errors -- UX gaps documented separately
- **Severity:** PASS (no code error; UX gaps are W-UI-4 and W-UI-5)
- **Observation:** Generating slots on a holiday (2026-12-25) and on a Saturday produces valid `DoctorAvailability` rows. No backend validation error, no frontend warning. The backend correctly accepts the submission; the gap is purely UX.

#### W-UI-10: 1-minute slot interval = 480 sub-slots/day -- browser render freeze risk
- **Severity:** bug (performance)
- **Symptom:** A 1-minute interval over a full day generates 480 sub-slots. Expanding the date row in the preview table caused the Playwright browser snapshot to reach 132,451 characters, exceeding the MCP token limit. The browser tab froze for several seconds. A real user with this preview would experience an unresponsive UI.
- **Root cause:** The preview table renders one `<tr>` per sub-slot with no virtualization. 480 rows x ~270 chars each = ~130 KB of DOM.
- **Recommended fix:** Add a minimum interval validation (e.g., 15 minutes) with an inline error: "Slot intervals must be at least 15 minutes." As a secondary improvement, add virtual scrolling to the preview table for large slot counts.

### View page findings

#### W-VIEW-1: Confirmation number URL fails -- view route accepts GUIDs only
- **Severity:** bug
- **Symptom:** Navigating to `/appointments/view/A00001` (using the human-readable `RequestConfirmationNumber` as external users would expect from an email link) throws "The value 'A00001' is not valid." The route at `angular/src/app/appointments/appointment/appointment-routes.ts` uses `path: 'view/:id'` with `:id` expected to be a UUID.
- **Implication for Steps 6.1/6.2:** Email deep-links sent to external parties must use the appointment GUID, not the confirmation number. The confirmation number can appear in the email body for reference, but the clickable link must embed the GUID.
- **Recommended fix:** Embed the appointment GUID in the email link. Optionally, add a server-side lookup route that accepts `?confirmationNumber=A00001` and redirects to the GUID-based URL for human-friendly sharing.

#### W-VIEW-2: Appointment status renders as raw enum key on view page header
- **Severity:** bug (same root cause as W-A-5; this is the view-page manifestation)
- **Symptom:** An Approved appointment (status = 2) displays `Enum:AppointmentStatusType.2` in the view page header status pill. The `abpLocalization` pipe is wired but either the `en.json` key is missing or the template passes the wrong value.
- **Fix path:** Covered by Step 7.1 i18n sweep.

#### W-VIEW-3: Defense Attorney section absent from view page template
- **Severity:** bug (data not displayed; see also W-A-7)
- **Symptom:** `grep "defenseAttorney"` in `appointment-view.component.html` returns 0 matches. No Defense Attorney section exists anywhere in the view template. DA data submitted from the booking form is invisible to the reviewer.
- **Fix path:** Step 5.4.

#### W-VIEW-4: Claim Examiner section absent from view page template
- **Severity:** bug
- **Symptom:** Same finding as W-VIEW-3 for Claim Examiner. The view template has no Claim Examiner Details section. CE data captured in the booking form modal is not displayed on the view page.
- **Fix path:** Step 5.4.

#### W-VIEW-5: Patient email field hardcoded `disabled` in template -- bypasses canEdit gate
- **Severity:** cosmetic (confirm with Adrian before changing)
- **Symptom:** Template line 313: `<input class="form-control" [(ngModel)]="patientForm.email" disabled />`. The `disabled` attribute is a plain HTML attribute, not `[disabled]="!canEdit('patientEmail')"` like other fields. Patient email is always non-editable regardless of admin role, appointment status, or canEdit logic.
- **Root cause:** Likely intentional -- patient email is PII and should not be editable post-submission. Confirm with Adrian before changing.

#### W-VIEW-6: `abp-lookup-select` ignores `[disabled]` binding -- Employer State appears and is editable when it should not be
- **Severity:** bug
- **Symptom:** The Employer State field uses `abp-lookup-select` with `[disabled]="!canEdit('employerStateId')"`. The component does NOT forward the Angular `[disabled]` input to the underlying `<select>` element (`setDisabledState` not implemented). The Employer State dropdown is interactive even when `canEdit()` returns `false`.
- **Root cause:** `abp-lookup-select` does not implement `ControlValueAccessor.setDisabledState()`.
- **Recommended fix:** Use a native `<select>` for this field, or wrap with `[attr.disabled]="!canEdit('employerStateId') ? '' : null"` on the outer container, or patch the ABP component.

#### W-VIEW-7: All edit fields disabled for admin on non-AwaitingMoreInfo appointments
- **Severity:** bug (admin correction workflow broken)
- **Symptom:** On an Approved appointment (status = 2), every field using `[disabled]="!canEdit()"` renders disabled for the admin user. The admin cannot correct any field values.
- **Root cause:** See W-VIEW-10 (isExternalUserNonPatient inverted logic). `canEdit()` sees admin as an "external" user and falls through to the `currentStatus !== AwaitingMoreInfo` guard, which disables all fields.

#### W-VIEW-8: `canEdit()` returns `false` for internal admin on all non-AwaitingMoreInfo appointments
- **Severity:** bug (same root cause as W-VIEW-7; distinct code path)
- **Symptom:** `canEdit()` at `appointment-view.component.ts`: `const isInternalAdmin = !isExternalUserNonPatient && !isPatientUser;` evaluates to `false` for admin because `isExternalUserNonPatient` incorrectly returns `true`. When `isInternalAdmin` is `false` and status != AwaitingMoreInfo, all fields return `false`.
- **Root cause:** See W-VIEW-10.

#### W-VIEW-9: `isExternalUserNonPatient` returns `true` for empty-roles users -- wrong defensive fallback
- **Severity:** bug
- **Symptom:** Lines 267-269 of `appointment-view.component.ts`:
  ```typescript
  if (!Array.isArray(roles) || roles.length === 0) {
    return true;  // classifies empty-roles user as external non-patient
  }
  ```
  A user whose roles have not yet loaded (race condition) or who genuinely has no roles is classified as an external non-patient user rather than as unauthenticated. The defensive fallback assumption is backwards.
- **Root cause:** Part of the same `isExternalUserNonPatient` design flaw as W-VIEW-10. Resolved by the W-VIEW-10 fix, which eliminates the `!hasPatientRole` pattern entirely.

#### W-VIEW-10: `isExternalUserNonPatient` inverted logic -- admin user classified as external non-patient
- **Severity:** bug (admin edit workflow completely broken for all non-AwaitingMoreInfo appointments)
- **Symptom:** Admin user (no `patient` role) triggers `!roles.some(r => r === 'patient')` which returns `true`, making `isInternalAdmin = !true && !false = false`. Every editable field is then disabled.
- **Affected code:** `appointment-view.component.ts` lines 265-271.
- **Fix (Step 5.3b):** Replace the negative patient-role check with an explicit external-role membership check:
  ```typescript
  get isExternalUserNonPatient(): boolean {
    const roles = (this.configState.getOne('currentUser') as any)?.roles ?? [];
    const externalNonPatientRoles = new Set(['applicant attorney', 'defense attorney', 'claim examiner']);
    return Array.isArray(roles) && roles.some((r: string) => externalNonPatientRoles.has(r?.toLowerCase() ?? ''));
  }
  ```
  This correctly returns `false` for admin (who has none of the three external roles), allowing `isInternalAdmin` to be `true` and re-enabling all fields.

---

## Round 5: Slot generation edge cases + i18n gaps in the slot form (2026-04-30, session 2)

Additional targeted tests run after Round 4 compaction. All tests executed via Chrome DevTools MCP evaluate_script against the live Docker stack. No code changes made.

### New slot generation findings

#### W-UI-11: Inverted and zero-duration date/time inputs silently produce 0 slots -- no UX feedback
- **Severity:** bug (UX gap)
- **Symptom:** Three edge inputs all silently generate 0 slots with no error message or inline warning:
  - Inverted date range (`FromDate` > `ToDate`, e.g., 2026-06-01 to 2026-05-01): preview renders 0 rows, Submit becomes disabled. No validation message.
  - Inverted time (`FromTime` > `ToTime`, e.g., 17:00 to 09:00): same outcome -- 0 sub-slots, silent failure.
  - Zero-duration (`FromTime == ToTime`, e.g., 09:00 to 09:00): same -- 0 sub-slots, no feedback.
- **Root cause:** The backend correctly refuses to generate slots for nonsensical inputs and returns an empty array. The frontend calls `generate()`, sets `this.preview = []`, hides the preview table, and disables Submit -- but never surfaces a validation-level error message telling the user why. The `validationMessage` wire at template line 245 only fires for the conflict-detection warning, not for empty-result states.
- **Suspect files:** `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability-generate.component.ts` -- `generate()` method at line 171 and the `updateConflictState()` helper; `doctor-availability-generate.component.html` line 245-249 (validationMessage block).
- **Recommended fix:** After `this.preview = result ?? []`, add a check: if `preview.length === 0 && form.valid`, set a user-facing error string ("No slots were generated. Check that your start date is before your end date and your start time is before your end time.") and display it in the validationMessage block.

#### W-UI-12: SlotByWeekdays FromDay/ToDay wrap-around (Fri through Mon) works correctly -- PASS
- **Severity:** PASS (informational)
- **Observation:** Setting `FromDay = Friday (5)` and `ToDay = Monday (1)` in SlotByWeekdays mode for May 2026 generated exactly 4 slots (the 4 days where day-of-week is Fri, Sat, Sun, or Mon), all at the correct times. The `isWeekdayInRange()` method at `doctor-availability-generate.component.ts:334` correctly handles the wrap-around case: when `fromDay > toDay` it uses the OR branch (`day >= fromDay || day <= toDay`). No errors, no conflicts.

#### W-UI-13: Same FromDay = ToDay in SlotByWeekdays generates only that weekday -- PASS
- **Severity:** PASS (informational)
- **Observation:** Setting both `FromDay` and `ToDay` to Wednesday for May 2026 (13:00-17:00) generated exactly 4 slots -- the 4 Wednesdays in May. No duplicates, no spurious days, no conflicts.

#### W-UI-14: BookingStatusId field in slot generation form renders raw Enum:BookingStatus.X keys
- **Severity:** bug (i18n gap; same root cause as W-A-5)
- **Symptom:** The "Booking Status" select on the slot generation form shows option labels `Enum:BookingStatus.8`, `Enum:BookingStatus.9`, `Enum:BookingStatus.10` instead of human-readable text. The underlying values are numeric enum ordinals (8=Available, 9=Booked, 10=Blocked -- inferred from the `BookingStatus` enum in the domain).
- **Suspect files:** `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json` -- missing `Enum:BookingStatus.*` keys. `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability-generate.component.ts` -- `bookingStatusOptions` array at component init (wherever it builds the option labels, e.g., via `L("Enum:BookingStatus." + value)`).
- **Fix path:** Add `"Enum:BookingStatus.8": "Available"`, `"Enum:BookingStatus.9": "Booked"`, `"Enum:BookingStatus.10": "Blocked"` (verify ordinals against the enum) to `en.json`. Subsumed by Step 7.1 i18n sweep.

#### W-UI-15: Slot generation form page title and radio labels render as raw i18n keys
- **Severity:** bug (i18n gap; same root cause as W-A-5)
- **Symptom:** The slot generation modal/page shows:
  - Page/card title: `SetAvailabilitySlot` (raw key, missing space and plain-language wording)
  - Slot mode radio labels: `SlotByDates` and `SlotByWeekdays` (raw keys instead of "By Dates" / "By Weekdays" or equivalent)
- **Suspect files:** `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json` -- missing `SetAvailabilitySlot`, `SlotByDates`, `SlotByWeekdays` keys. `doctor-availability-generate.component.html` lines 1-15 (page title) and lines 40-50 (radio button labels).
- **Fix path:** Add the three keys to `en.json` with plain-language values. Subsumed by Step 7.1 i18n sweep.

#### W-UI-16: InternalUserRoleDataSeedContributor creates roles + grants permissions but does NOT assign any user to those roles after Docker rebuild
- **Severity:** bug (blocker for internal-role testing)
- **Symptom:** After a full Docker stack rebuild and `db-migrator` seed, `maria.rivera@hcs.test` (the seeded Staff Supervisor) logs in and receives 0 `CaseEvaluation.*` granted policies. The user exists and can authenticate, but all protected endpoints return 403. Roles (IT Admin, Staff Supervisor, Clinic Staff) exist in the database but the `AbpUserRoles` join table has 0 rows for the seeded internal users.
- **Root cause:** `InternalUserRoleDataSeedContributor.cs` seeds the roles and their permission grants, but has no code to assign existing identity users to those roles. User-to-role assignment was done manually post-seed in the original environment and is not part of the automated seed path.
- **Impact:** Every fresh Docker rebuild requires a manual role-assignment step in the ABP Identity UI or a direct DB update before internal users can operate. This breaks automated CI/CD testing of the internal-user flows and makes the demo fragile.
- **Suspect files:** `src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs` -- seeds roles at lines ~40-80 and grants at lines ~85-160, but has no `_identityUserManager.AddToRoleAsync(user, roleName)` block.
- **Recommended fix:** After seeding roles, look up each seeded internal user by email (e.g., `maria.rivera@hcs.test`) using `_userManager.FindByEmailAsync(email)` within the appropriate tenant context, and call `_userManager.AddToRolesAsync(user, new[] { "Staff Supervisor" })`. Wrap in `CurrentTenant.Change(tenantId)` for tenant-scoped users. Add analogous blocks for every seeded internal user.

---

## Round 6: External user registration, slot visibility, admin invite form (2026-04-30, session 2)

All tests executed via Playwright MCP against the live Docker stack (all 5 services healthy). No code changes made.

### Test Group 3 -- External user registration + login + cross-tenant isolation

#### W-REG-1: Patient registration and login -- PASS
- **Test:** Login -> switch to Dr Rivera 2 -> Register -> role=Patient, username=rnd6.patient.1, email=rnd6.patient.1@hcs.test, password -> Register.
- **Result:** Redirected to Login page (success). Login succeeded. Angular home shows "Dr Rivera 2 | Welcome, rnd6.patient.1@hcs.test (Patient)" with both "Book Appointment" and "Book Re-evaluation" buttons and "My Appointments Requests (0)" table. No console errors.
- **Patient entity created:** `/api/app/patients/me` returned 200 with `{ identityUserId: "44d8957d...", email: "rnd6.patient.1@hcs.test", tenantId: "5d5c78a2..." (Dr Rivera 2) }`. Entity was auto-created by `RegisterAsync` at time of registration.
- **W-A-1 persists:** `firstName: ""`, `lastName: ""` (empty strings, not email/User). The register form still has no name fields. The default is now blank rather than leaking the email -- this is an improvement over the original leak but the core issue (names empty until the user fills the booking form) remains.

#### W-REG-2: Applicant Attorney registration and login -- PASS; S-NEW-1 already implemented
- **Test:** Direct `/Account/Register` (tenant cookie Dr Rivera 2 still active from prior session) -> role=Applicant Attorney, username=rnd6.aa.1, email=rnd6.aa.1@hcs.test -> Register.
- **Result:** Redirected to Login. Login via Angular OIDC flow succeeded. Home shows "Welcome, rnd6.aa.1@hcs.test (Applicant Attorney)" with both booking buttons and appointment table (0 records). No page-blocking errors.
- **S-NEW-1 already implemented:** Code review of `ExternalSignupAppService.cs` lines 241-257 confirms `RegisterAsync` already has an `else if (input.UserType == ExternalUserType.ApplicantAttorney)` block that creates an `ApplicantAttorney` domain entity. The entity creation uses `_applicantAttorneyManager.CreateAsync(stateId: null, identityUserId: user.Id)`. This step is marked TODO in the priority plan but is ALREADY DONE in code.
- **`/external-users/me` (200):** Returns `{ identityUserId, firstName: "", lastName: "", email: "rnd6.aa.1@hcs.test", userRole: "Applicant Attorney" }`. Correct.
- **W-A-1 persists:** Same empty name situation as Patient.

#### W-REG-3: Defense Attorney registration and login -- PASS; no domain entity per D-2
- **Test:** Direct `/Account/Register` with Dr Rivera 2 tenant cookie active -> role=Defense Attorney, username=rnd6.da.1, email=rnd6.da.1@hcs.test -> Register.
- **Result:** Redirected to Login. Login succeeded. Home shows "Welcome, rnd6.da.1@hcs.test (Defense Attorney)" with both booking buttons. No blocking errors.
- **No DA domain entity created:** Code confirms no `else if (input.UserType == ExternalUserType.DefenseAttorney)` block exists in `RegisterAsync`. IdentityUser + role only -- per Decision D-2. Correct behavior.
- **`/external-users/me` (200):** Returns `{ firstName: "", lastName: "", email: "rnd6.da.1@hcs.test", userRole: "Defense Attorney" }`. Correct.

#### W-REG-4: Direct-URL Register without explicit tenant selection uses session tenant cookie
- **Severity:** observation / finding (refines W-B-1 diagnosis)
- **Symptom:** When navigating directly to `http://localhost:44368/Account/Register` while a tenant cookie (from a prior Login session) is still active, the form renders WITHOUT the visible tenant heading/switch section. The registration succeeds using the cookie-inferred tenant context. In a completely fresh browser (no prior session, no tenant cookie), the tenant section is also absent and the form behavior is undefined.
- **Distinction from W-B-1:** The original W-B-1 finding was about the standalone URL silently failing registration. The current behavior is: (a) registration can SUCCEED via direct URL if a tenant cookie is active, (b) the tenant context used is the cookie one (not user-selected), and (c) the form gives no visual indication of which tenant the registration is for. This is confusing UX even when it "works".
- **Risk:** A user who previously visited tenant A's login page, then navigates directly to `/Account/Register` on a shared/public device, could accidentally register under tenant A instead of their intended tenant.
- **Recommended fix:** Always render the tenant selector on the Register page regardless of cookie state. Force the user to explicitly confirm the tenant before submitting. Step 2.5 in the priority plan covers the W-B-1 fix; this observation should be folded in.

#### W-REG-5: Cross-tenant isolation confirmed for bearer-token-scoped sessions
- **Severity:** PASS (informational)
- **Test:** Logged in as rnd6.aa.1@hcs.test (Dr Rivera 2, token `tenantid: 5d5c78a2-...`). Attempted `GET /api/app/appointments?__tenant=Dr+Thomas+1` with the Dr Rivera 2 bearer token.
- **Result:** API returned 200 with `totalCount: 0`. ABP ignored the `__tenant` URL param when a bearer token is present (token's `tenantid` claim is authoritative). The AA user's scope is locked to Dr Rivera 2 by the token -- cross-tenant query returns no data from Dr Thomas 1.
- **Mechanism:** OpenIddict embeds `tenantid` in the access token at issuance time. ABP's `AbpClaimsCurrentTenantMiddleware` reads the token claim, not the URL param, for the principal tenant context. This is the correct and secure behavior.

---

### Test Group 4 -- Slot visibility for external users + multi-tenant filtering

#### W-SLOT-1: Admin-created slots ARE visible to external users on the booking form -- PASS (403 resolved)
- **Severity:** PASS
- **Test:** Logged in as rnd6.da.1@hcs.test (Defense Attorney, Dr Rivera 2). Navigated to `/appointments/add?type=1`. Set AppointmentType=QME, Location=Demo Clinic North, Date=2026-05-04.
- **Result:** Appointment Time picker populated with 13 slots: 09:00 AM, 10:15 AM, 10:30 AM, 10:45 AM, 11:00 AM, 11:15 AM, 11:30 AM, 11:45 AM, 12:00 PM, 01:00 PM, 02:00 PM, 03:00 PM, 04:00 PM. All admin-created slots for this date/location are visible.
- **403 resolved:** `appointment-type-lookup` (200, 6 types), `location-lookup` (200, 2 locations), `doctor-availabilities` (200, 42 records for tenant). Steps 1.4 and 4.2 (lookup policy demotion to plain `[Authorize]`) are working.
- **Date restriction helptext:** Form shows persistent inline text "You can book appointment after 3 days of today's date." This appears below the date field regardless of which date is selected (shown for May 4, May 7, any date tested). This is informational helptext about the booking lead time policy, NOT a conditional validation error. Slots still display and the form is still fillable.

#### W-SLOT-2: Multi-tenant slot isolation -- WORKING via ABP IMultiTenant on DoctorAvailability
- **Severity:** PASS (informational)
- **Mechanism:** `DoctorAvailability` implements `IMultiTenant`. When the external user's bearer token contains `tenantid = Dr Rivera 2`, ABP's automatic tenant data filter restricts all `DoctorAvailability` queries to that tenant's rows. Location entities are currently host-scoped (shared GUID `a0a00005-...` for "Demo Clinic North" across all tenants), but slot visibility is correctly scoped because slots are filtered by `TenantId` on `DoctorAvailability`, not just by `LocationId`. A Dr Rivera 2 external user will never see Dr Thomas 1's slots at the same location.
- **Verification:** `/api/app/doctor-availabilities` with the DA's Dr Rivera 2 bearer token returns 42 rows -- all belong to Dr Rivera 2 (ABP auto-filter applied). No slot data from other tenants is returned.

#### W-SLOT-3: Slot time picker uses time strings, not GUIDs
- **Severity:** observation (relevant to booking form submit behavior)
- **Symptom:** The Appointment Time dropdown options have values like `"09:00:00"`, `"10:15:00"` etc. (time strings, not UUIDs). The `doctorAvailabilityId` hidden input is populated separately when the user selects a time slot (the Angular component maps the selected time → the corresponding `DoctorAvailability` GUID client-side).
- **Implication:** If two DoctorAvailability rows exist at the same time for the same date/location (different appointment types), the picker may show duplicates or the mapping may be ambiguous. This is consistent with the conflict detection design (one slot per time+location).

#### W-SLOT-4: Booking form console error from /patients/me 404 for DA role -- Step 2.1 fix working correctly
- **Severity:** PASS (no regression)
- **Symptom:** One console error `404 /api/app/patients/me` appears after opening the booking form as a DA. However, this 404 was generated by a manual test API call -- the Angular application itself did NOT call `/patients/me` for the DA user. The form loaded completely without a global error modal. Step 2.1's predicate fix (Patient role only → `/patients/me`; everyone else → `/external-users/me`) is working correctly for DA.

#### W-SLOT-5: Booking form shows 19 Applicant Attorney options in the AA pre-fill picker
- **Severity:** observation / possible finding
- **Symptom:** The "Select Applicant Attorney" dropdown (for the AA section of the booking form) shows 19 options including entries like `qa.aa.64fab447@hcs.test ( )` and `qa.attorney.7pc@hcs.test ( )`. The display format `email ( )` is due to W-A-1 (empty firstName/lastName). The list IS being populated from the AA domain entities created by RegisterAsync (S-NEW-1 confirmed working).
- **Implication:** The AA lookup is functioning, and AA entities created by RegisterAsync are surfacing in the booking form's AA picker. This confirms the end-to-end AA entity flow is working.

---

### Test Group 5 -- Admin invite form: what exists vs. what is missing

Full audit performed via code reading. Summary:

#### W-INVITE-1: Admin invite endpoint -- MISSING (entire feature absent)
- **Severity:** gap (Tier 3.6 in fix order; not a lifecycle blocker but a product gap)
- **What exists:**
  - `ExternalSignupAppService.cs` (305 lines): 4 methods: `RegisterAsync` (anonymous), `GetTenantOptionsAsync` (anonymous), `GetExternalUserLookupAsync` (no explicit auth), `GetMyProfileAsync` (Authorize). No invite method.
  - `IExternalSignupAppService.cs`: Mirrors the 4 methods. No invite interface.
  - `ExternalSignupController.cs` (50 lines): Routes to the 4 existing methods only.
  - No `*invite*` files anywhere in `angular/src/app/` (glob search returned zero matches).
- **What is missing (ordered by dependency):**
  1. `CreateExternalUserInviteDto` with fields: callerName, callerEmail, invitedEmail, userType, tenantId, doctorName (XS)
  2. `ExternalUserInvites.Default + .Send` permission constants + registration in `CaseEvaluationPermissionDefinitionProvider` (XS)
  3. `SendInviteAsync(CreateExternalUserInviteDto)` on `ExternalSignupAppService` -- validates, generates link, enqueues email job (S, 150-250 LOC)
  4. `POST /api/app/external-user-invites/send` controller endpoint with `[Authorize(Send)]` (XS, ~20 LOC)
  5. Invite token entity (UUID + TTL ~7 days) + repo + manager for audit trail and link validation (M, 150-300 LOC)
  6. `SendExternalUserInviteJob` Hangfire job using existing `IEmailSender` + `IBackgroundJobManager` pattern (S, 50-100 LOC)
  7. Angular standalone form component: callerName, callerEmail, invitedEmail, userType dropdown, submit with success toast (M, 200-400 LOC)
  8. Angular REST service wrapper + lazy route `/external-user-invites/send` + menu entry behind permission guard (XS, ~60 LOC)
  9. `/register-from-invite?code=<token>` unauthenticated Angular page that validates token and pre-fills the Register form (S, 100-150 LOC)
  10. `en.json` localization strings for form, email body, and menu (XS, ~15 strings)
  11. xUnit tests for `SendInviteAsync` (valid, duplicate email, bad tenant, expired token) (S, 150-200 LOC)
- **Total estimated build effort:** ~20-24 working hours for the complete feature.

#### W-INVITE-2: Email infrastructure exists and is reusable for invite
- **Severity:** PASS (no gap)
- **What exists:** `SendAppointmentEmailJob.cs` (Hangfire-backed, accepts `SendAppointmentEmailArgs { To, Subject, Body, IsBodyHtml, Role }`), `IEmailSender` injection, `IBackgroundJobManager.EnqueueAsync` pattern used in `StatusChangeEmailHandler` and `AppointmentDayReminderJob`. The same pattern can be used for invite emails with no new infrastructure.
- **Note:** Current SMTP credentials are placeholder (W-A-10 -- credentials invalid, jobs log warnings). Invite emails will share the same SMTP failure until W-A-10 is fixed.

#### W-INVITE-3: Tenant context switching pattern is proven and reusable
- **Severity:** PASS (no gap)
- **What exists:** `using (CurrentTenant.Change(tenantId)) { ... }` pattern is already used in `ExternalSignupAppService.RegisterAsync` and `DoctorTenantAppService`. The invite flow can use the same pattern to scope invite processing (validation, email delivery) to the invited tenant.

---

## PRIORITY WORK PLAN -- external-user-complete-lifecycle (Adrian-locked 2026-04-30)

This section supersedes "FIX PRIORITY ORDER" below for sequencing. Adrian locked five priorities for the next push:

1. Register all 4 external users (Patient, Applicant Attorney, Defense Attorney, Claim Examiner) with correct permissions AND the matching domain entity row.
2. Login -> external user lands on home with the 4 standard appointment statuses visible (Approved, Rejected, Pending, AwaitingMoreInfo) AND Book Appointment + Book Re-evaluation buttons.
3. All form fields work end-to-end: validation, intuitive error messages for non-tech-savvy users, data persists under correct entities, no silent failures.
4. External users can submit Initial AND Re-evaluation; submission stores properly and is visible to ALL tenant-registered involved parties (booker + Patient + AA + DA + CE).
5. After submission, all 4 involved parties get an email with the RequestConfirmationNumber and a login deep-link.

### Decisions locked in this session

- **D-1 (Re-evaluation = Path B):** Re-evaluation will be properly wired with OLD-code `PQMEREEVAL`/`AMEREEVAL` semantics. Even if today both forms look identical, Re-evaluation gets its own AppointmentType branches in the flow so future modifications/pre-fill stay isolated. This elevates W-H-1 from product-decision (Tier 3.4) to Tier S4 of this plan.
- **D-2 (External-role entity creation -- Patient + AA ONLY):** Keep IdentityUser+role for auth (ABP idiomatic). On register, ALSO create the matching user-level domain entity for Patient (already done) and Applicant Attorney (new). Defense Attorney and Claim Examiner register and login normally but do NOT get a user-level domain entity, do NOT show up in any external-user lookup, and do NOT pre-fill the booking form. Adrian (2026-04-30): "we dont want anyone to look up and prefills for [DA/CE] right now, we might add them later but for now lookups and prefills only for Patient and Applicant Attorneys, the people who are most likely to book an appointment or re-evaluation". See S-NEW-1 below.
- **D-3 (Booker captures 4 emails as required fields):** The appointment form will require the booker to enter the email IDs of the Patient, Applicant Attorney, Defense Attorney, and Claim Examiner (whichever the booker is not). These emails become the recipient list for fan-out. Submit is blocked until all 4 are present and valid. See S-NEW-2 below.
- **D-4 (Email link = localhost with tenant pre-selected, dev only):** Until production hosting is decided (subdomain-per-tenant pivot is researched but not built), every email contains a `http://localhost:4200/account/login?__tenant=<TenantName>` style link. The tenant query-string survives the SPA redirect chain and pre-selects the tenant. Production link is TBD and gated on hosting architecture decision (`C:\Users\RajeevG\.claude\plans\2026-04-28-tenant-subdomain-architecture-study.md`). See S-NEW-3 below.
- **D-5 (UX target = non-tech-savvy users):** Every form, error toast, validation message, and email body must be obvious to a non-technical user. No raw i18n keys, no stack-trace text, no silent network failures. See S-NEW-4 below.

### S-NEW-1: RegisterAsync must create the user-level domain entity for ApplicantAttorney (Patient + AA only)

> **STATUS: ALREADY IMPLEMENTED.** Round 6 (W-REG-2) confirmed `ExternalSignupAppService.cs:241-257` already has the `else if (ApplicantAttorney)` block. "Defense Attorney" is already absent from `GetExternalUserLookupAsync`. Verified live: AA register -> AA entity created; DA register -> no entity. Linear fix order Step 3 updated accordingly. The description below is preserved as a design record.

- **Severity:** blocker for Adrian's Priority 1.
- **What:** Extend `ExternalSignupAppService.RegisterAsync` so that, after `IdentityUser` + role are created, it also creates an `ApplicantAttorney` row when `userType == ApplicantAttorney`. Use `IdentityUserId` as the linking FK with all string fields nullable on first creation; the booker can fill firm details later via the booking form which pre-fills from the saved row. **Skip both DefenseAttorney and ClaimExaminer** -- per D-2, neither gets a saved profile, neither is exposed in any lookup, and neither pre-fills the booking form. Patient registration already creates a Patient row -- unchanged.
- **Why:** Without the AA entity, AA bookers cannot pre-fill their own section on the booking form, the tenant-admin Applicant Attorneys management screen is empty, and any feature that resolves "current external user as AppointmentApplicantAttorney" silently fails. DA/CE intentionally stay IdentityUser+role only.
- **Suspect files:**
  - `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs:170-227` (RegisterAsync -- mirror the Patient block at lines 211-225).
  - Inject `ApplicantAttorneyManager` (peer of `PatientManager`). Do NOT inject `DefenseAttorneyManager`.
- **Recommended fix:** After role assignment, switch on `input.UserType`:
  - Patient -> existing PatientManager.CreateAsync block (unchanged).
  - ApplicantAttorney -> dedupe by IdentityUserId via `IApplicantAttorneyRepository.FirstOrDefaultAsync(x => x.IdentityUserId == user.Id)`; if null, `_applicantAttorneyManager.CreateAsync(stateId: null, identityUserId: user.Id, firmName: null, firmAddress: null, phoneNumber: null, ...nulls...)`.
  - DefenseAttorney -> NO entity creation; IdentityUser + role only.
  - ClaimExaminer -> NO entity creation; IdentityUser + role only.
- **Companion change:** `GetExternalUserLookupAsync` today returns Patient + Applicant Attorney + Defense Attorney. Drop "Defense Attorney" from `allowedRoleNames` so the booking form's external-user picker (used for booker-side pre-fill) only surfaces Patient + Applicant Attorney. CE is already absent.
- **Pros / cons:** Documented above. Net: cheap, additive, closes the AA pre-fill gap; preserves DA/CE privacy by keeping them out of lookups by design.

### S-NEW-2: Submission visibility -- registered involved parties must see the appointment in their own appointment list; emails go to all 4 input emails

- **Severity:** blocker for Adrian's Priorities 2, 4, 5.
- **What:** Two coordinated changes:
  1. **Mandatory 4-email capture on the form** (Decision D-3): Patient email, Applicant Attorney email, Defense Attorney email, Claim Examiner email all become required submit-validators. Whichever role the booker holds, that email is auto-filled and read-only; the other 3 are required text inputs with format validation.
  2. **Per-recipient appointment-list filtering**: every appointment with a matching email at submit time must appear in that recipient's appointment-list view, regardless of whether the recipient is registered today. If they are registered under the matching role at the same tenant, the list is populated by the existing query; if not, no filter row exists yet -- they only get the email and the list shows on first login after register.
- **Why:** Adrian's Priority 4 says "visible to all the tenant-registered involved parties". The current code only resolves visibility through the appointment-AA / appointment-DA / appointment-CE join entities; if any of those joins is missing, that party can never see the appointment. Once S-NEW-1 lands, AA/DA registrations create the user-level entity, so the join entities can point at real rows and visibility works.
- **Suspect files:**
  - Booking form (Angular): `angular/src/app/appointments/appointment/components/appointment-add.component.*` -- mark email fields required, add format validation, surface the validation error inline (no silent submit).
  - AppointmentsAppService `CreateAsync` -- ensure it creates `AppointmentApplicantAttorney`, `AppointmentDefenseAttorney`, `AppointmentClaimExaminer` join rows from the form input even when those parties are not yet registered. The join rows carry the email so emails fan out (S-NEW-3 below) and any later registration backfills the IdentityUserId via S-NEW-1.
  - Appointment-list query (Angular + AppService) -- filter where `currentUser.email == appointment.{patient|aa|da|ce}.email` OR currentUser is the booker.
- **Recommended fix:** Form-side: 4 email inputs required, [Validators.required, Validators.email]; show inline error "Please enter a valid email" on blur. Server-side: AppointmentsAppService.CreateAsync writes all four party-email fields (already exists in the entity per W-A-2 finding) AND creates the 3 join rows. Appointment-list query: extend the WHERE clause to include current-user-email matches across the 4 party-email fields.

### S-NEW-3: Email body -- confirmation # + conditional login/register link with tenant pre-selected (dev = localhost)

- **Severity:** blocker for Adrian's Priority 5.
- **What:** Every recipient (booker + Patient + AA + DA + CE) gets an email with:
  - Subject: `Appointment requested -- Confirmation #{RequestConfirmationNumber}`
  - Body: confirmation number, appointment summary (date, location, type), booker name, and a deep-link.
  - **Conditional link logic** (Decision D-3 / D-4):
    - Recipient email IS registered at the same tenant under the matching role -> link reads "Click here to log in and view your requested appointment" pointing at `http://localhost:4200/account/login?__tenant=<TenantName>` (in production, swap localhost for the deployment URL or tenant subdomain once D-4-prod is decided).
    - Recipient email is NOT registered, OR is registered under a different role at this tenant -> link reads "Click here to register at <Tenant> and view your requested appointment" pointing at `http://localhost:4200/account/register?__tenant=<TenantName>&email=<recipientEmail>&role=<inferredRole>`.
- **Why:** This is what the demo audience watches happen. Without conditional link content, recipients either land on a login page they have no account for, or see a register page that doesn't pre-select tenant + role.
- **Suspect files:**
  - `SendAppointmentEmailJob` enqueue site (the W2-10 fan-out -- search for `SendAppointmentEmailJob` enqueue calls).
  - Email template (likely a Razor `.cshtml` under `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Notifications/` or `EmailTemplates/`).
  - Tenant-resolution helper for the link (read `CurrentTenant.Name` or look up by id).
- **Recommended fix:** In the fan-out loop, for each recipient email: do an `IdentityUser` + role lookup at the recipient tenant. If found-with-matching-role, render template variant "login"; else render template variant "register". Both templates accept `RequestConfirmationNumber`, `AppointmentSummary`, `LoginUrl`, `RegisterUrl` placeholders.
- **Production link plan (deferred):** Once subdomain-per-tenant hosting lands (or alternative architecture), swap the host part of the link from `http://localhost:4200` to `https://<tenant>.<root-domain>` or whatever D-4-prod resolves to. The query-string `__tenant=` parameter remains a safe fallback.

### S-NEW-4: Silent-failure + non-tech-savvy UX audit

- **Severity:** blocker for Adrian's Priority 3.
- **What:** Sweep every form, every toast, every modal, every email for:
  - Raw i18n keys (`Enum:AppointmentStatusType.1`, `Appointment:Action:Approve`, `Enum:Gender.1`, etc.) -- replace with translated text. (Subsumes Tier 2.1 / W-A-5.)
  - Silent network failures (Workflow B's standalone Register page is the canonical example -- form resets with no message). Every failed POST must surface a visible, plain-language error.
  - Confusing labels like "PQME" leaking from OLD code (subsumes W-A-8).
  - Modal Add buttons that look enabled but do nothing (subsumes W-A-4).
  - Form fields displayed but bound to the wrong property so values look saved but aren't (subsumes patient-column displaying email instead of name in W-A-7 / 2.10).
- **Why:** Decision D-5. Real users are non-technical workers' comp staff and patients. Every confusing surface costs trust.
- **Recommended approach:** This is a meta-finding -- it does not get fixed in one PR. Instead, the per-finding fixes below are all required to satisfy S-NEW-4; the audit ensures we do not declare done while any silent-failure path remains.

### Linear fix order for the external-user-complete-lifecycle goal

This is the order to follow STARTING NOW. It interleaves Tier 0/1/2/3 items below by what unblocks Adrian's 5 priorities fastest. Original Tier numbering in section "FIX PRIORITY ORDER" is preserved for cross-reference; the column **Step** is the actual execution sequence.

| Step | Tier ref | What | Why now |
|---|---|---|---|
| **1** | 0.1 / W-X-2 | Add `[Authorize]` to `GetExternalUserLookupAsync` | Security/HIPAA. Prereq before any other work ships. ~1 minute. |
| **2** | 0.2 / W-A-10 | Switch SMTP to a `Development`-env NoOp / dev sender so emails are diagnose-able | We cannot verify Step 6 emails until Hangfire stops lying. ~10 min. |
| **3** | S-NEW-1 (D-2) -- **ALREADY DONE** | `ExternalSignupAppService.cs:241-257` already contains the `else if (ApplicantAttorney)` block that creates the AA entity. "Defense Attorney" is already absent from `GetExternalUserLookupAsync` `allowedRoleNames`. Verified live in Round 6 (W-REG-2): AA register -> AA row created; DA register -> no entity. No build action required. | Adrian Priority 1. **Already shipped.** |
| **4** | 2.5 / W-B-1 | Fix direct `/Account/Register` URL silent failure | Adrian Priority 1. The standalone register entry must not silently drop registrations. ~20 min. |
| **5** | 2.4 / W-A-1 | Add First Name + Last Name fields to AuthServer Register page; require them | Adrian Priority 1 + S-NEW-4. Without these, every email/queue/header still leaks email-as-firstName. ~20 min. |
| **6** | 2.6 / W-B-2 | Booking form: gate `/patients/me` call on Patient role; AA/DA/CE call `/external-users/me` instead | Adrian Priority 2/3. Without this, AA/DA/CE booking form throws a global error modal on first open. ~15 min. |
| **7** | 1.3 / W-A-3 + universal | Demote lookup `[Authorize]` policies to authenticated-only OR seed read-only perms for all 4 external roles on `wcab-office-lookup`, `field-configs`, `state-lookup`, `doctors`, `appointment-type-lookup` | Adrian Priority 3. Without this, every external booker sees empty dropdowns. ~20 min. |
| **8** | 2.9 / W-V-1 | Bind WCAB Office option text to `displayName` in the modal template | Adrian Priority 3. ~10 min. |
| **9** | 2.8 / Original Finding 6 | Fix Claim Information modal `_rawValidators` errors; ensure every named FormControl exists at component init | Adrian Priority 3. Likely cascades into Step 10. ~30 min. |
| **10** | 1.6 / W-A-4 | Modal Add button must close modal + push claim row into parent form | Adrian Priority 3. May be subsumed by Step 9. ~15 min after Step 9. |
| **11** | S-NEW-2 (D-3) | Make 4 party-email fields required on the booking form (Patient/AA/DA/CE); validate format inline; auto-fill the booker's own role | Adrian Priority 4 + 5 prereq. ~30 min. |
| **12** | S4 / W-H-1 (Path B) | Wire Re-evaluation flow with PQMEREEVAL/AMEREEVAL semantics: separate `?type=2` route resolves to a Re-eval form variant (start as a clone of Initial; future modifications go here) | Adrian Priority 4. Decision D-1. ~30-45 min for the wiring; UI may stay near-identical in v1. |
| **13** | 1.7 / W-A-6 | Add Review item to queue Actions dropdown | Adrian Priority 4 verification path -- office user needs to find the appointment from the queue. ~10 min. |
| **14** | 2.2 / W-A-7 + 3.3 | Surface Defense Attorney + Claim Information sections in the appointment view; populate Patient Demographics textboxes from saved data | Adrian Priority 4 (visibility). ~30 min. |
| **15** | 2.10 | Queue Patient column binds `patient.firstName + patient.lastName`, not IdentityUser email | Adrian Priority 4 (S-NEW-4 polish). ~10 min. |
| **16** | S-NEW-2 (visibility) | Extend appointment-list query to also include rows where current user's email matches one of the 4 party-email fields | Adrian Priority 4 (party visibility). ~20 min. |
| **17** | 1.2 / W-A-2 | Email fan-out: enqueue one `SendAppointmentEmailJob` per (booker, Patient, AA, DA, CE) using the 4 emails captured in Step 11 | Adrian Priority 5. ~30-60 min. |
| **18** | S-NEW-3 (D-3 / D-4) | Email body: include RequestConfirmationNumber + appointment summary + conditional login/register link with tenant pre-selected via `?__tenant=` query string | Adrian Priority 5. ~30-45 min. |
| **19** | 2.1 / W-A-5 | Sweep `en.json` + templates to remove all raw i18n key surfaces (status, action labels, gender, phone type, modal titles) | S-NEW-4 closer. ~30-60 min. |
| **20** | 2.11 | Trigger appointment view re-fetch after Approve so status pill updates | S-NEW-4 polish. ~15 min. |

After Step 20: end-to-end demo runs cleanly for any of the 4 external-role bookers.

**Deferred -- post-lifecycle (still needed for full demo but not on the external-user-complete-lifecycle critical path):**
- 1.4 / W-G-1 -- Document upload 500 (Stage 8 only).
- 1.1 / W-A-9 -- Packet generation `ObjectDisposedException` (Stage 8 only).
- 1.5 + 1.8 / W-I-1 + W-I-2 + W-I-3 + W-UI-16 -- Internal-role grants (Clinic Staff / Staff Supervisor / Doctor) AND seeder role-assignment fix (W-UI-16). `InternalUserRoleDataSeedContributor` seeds roles and permission grants but never calls `AddToRoleAsync`, so every fresh Docker rebuild leaves all seeded internal users with 0 roles and no permissions. Both fixes should land together: update `CaseEvaluationRoleDataSeedContributor` for role grants AND add `AddToRoleAsync` calls in `InternalUserRoleDataSeedContributor` for each seeded internal user. Demo tenant admin works without this; these matter once Adrian wants the office personas to operate as themselves rather than as the all-powerful admin.
- 2.3 / W-X-4 -- Edit modal expansion or removal.
- 2.7 / W-A-8 -- "PQME" branding leak. Likely fixed inadvertently by Step 12 (Re-eval wiring) since both Initial and Re-eval will read AppointmentType.Name dynamically.

**Estimated wall-time for Steps 1-20:** 6-10 working hours, contiguous.

---

## FIX PRIORITY ORDER (use this when starting fixes)

This is the recommended order to fix all findings across both rounds. Each is grouped by what it unblocks.

### Tier 0 -- Security / HIPAA (fix before any other work)

| # | Finding | Severity | Why first |
|---|---|---|---|
| **0.1** | **W-X-2 -- Anonymous external-user-lookup leaks all external users** | blocker / HIPAA | Production data exposure. One-line code change (`[Authorize]` annotation). Must land before anything else is shipped. |
| **0.2** | **W-A-10 -- SMTP authentication failing in dev; Hangfire reports false success** | bug | The whole "email" diagnostic surface is lying right now. Without fixing this, no email-related finding can be verified end-to-end. Cheap: switch to a no-op or development email sender in `Development` env. |

### Tier 1 -- Demo-blockers (the lifecycle cannot complete without these)

| # | Finding | Severity | Notes |
|---|---|---|---|
| **1.1** | **W-A-9 -- `GenerateAppointmentPacketJob` throws `ObjectDisposedException`** | blocker | UoW scoping fix on the job. Without this, no packet PDF can ever be produced. |
| **1.2** | **W-A-2 -- Email fan-out fires only to booker, not AA / DA / CE / Office** | blocker | W2-10 regression. The whole "demo emails fire" narrative depends on this. |
| **1.3** | **W-A-3 + universal external-role read-block -- Patient/AA/DA/CE get 403 on `wcab-office-lookup`, `field-configs`, `doctors`** | blocker | Without this, no external user can fill the booking form. Either downgrade `[Authorize]` on the lookup methods or seed read-only perms on all 4 external roles. |
| **1.4** | **W-G-1 -- Document upload 500 (`Stream.ReadTimeout` reflection bug)** | blocker | Without this, no documents -> no packet content -> no Stage 8 demo. |
| **1.5** | **W-I-1 + W-I-2 + W-I-3 -- Clinic Staff / Staff Supervisor / Doctor roles severely under-permissioned** | blocker | The actual demo personas (not the all-powerful `admin`) cannot do their jobs. Three separate role-grant updates in `CaseEvaluationRoleDataSeedContributor`. |
| **1.6** | **W-A-4 -- Claim Information modal Add button is a no-op** | blocker | Booker can't attach claim info -> view page omits claim section -> packet incomplete. Fix likely cascades from W-A-3. |
| **1.7** | **W-A-6 -- Queue Actions dropdown missing the Review item** | blocker | Office user can't find their way to the review page from the queue. One-line component change. |
| **1.8** | **W-UI-16 -- `InternalUserRoleDataSeedContributor` seeds roles and permission grants but never calls `AddToRoleAsync`, leaving all seeded internal users with 0 roles after every fresh Docker rebuild** | blocker (fresh-build reproducibility) | Every Docker rebuild requires a manual role-assignment step in the ABP Identity UI before `maria.rivera@hcs.test` or any other seeded internal user can operate. Fix: after seeding roles, look up each seeded internal user by email within the correct tenant context and call `_userManager.AddToRolesAsync`. Companion to 1.5 (W-I-1/2/3). |

### Tier 2 -- Demo-stutters (visible regressions, not lifecycle blockers)

| # | Finding | Severity | Notes |
|---|---|---|---|
| **2.1** | **W-A-5 -- i18n keys render raw across queue, view, modals (`Enum:AppointmentStatusType.1`, `Appointment:Action:Approve`, `Enum:Gender.1`, etc.)** | bug | Visible everywhere a non-technical audience would look. Add missing `en.json` entries OR fix the templates that use literal keys. |
| **2.2** | **W-A-7 -- Defense Attorney + Claim Information sections missing from view page** | bug | Reviewer sees an incomplete record. Likely a DTO-projection or `*ngIf` gate. |
| **2.3** | **W-X-4 -- Edit modal from queue exposes only 4 fields** | bug | Either remove the Edit action or expand the modal to mirror the booking form. |
| **2.4** | **W-A-1 -- RegisterAsync defaults `firstName=email`, `lastName="User"` -- leaks into emails, queue, view header** | bug | Add First Name + Last Name fields to the AuthServer Register page. |
| **2.5** | **W-B-1 + W-REG-4 -- Direct `/Account/Register` URL silently fails to register; when a session cookie IS active the form succeeds but renders without a tenant selector, so the user registers under the cookie tenant with no visual confirmation** | bug | Two layers: (1) antiforgery / form-handler fix so the standalone URL works at all. (2) Always render the tenant selector regardless of cookie state and force explicit tenant confirmation before submit, so users cannot accidentally register under the wrong tenant. Until fixed, only the Login -> switch -> Register link works reliably. |
| **2.6** | **W-B-2 -- `/patients/me` 404 for non-Patient bookers (the form calls it for AA / DA / CE too)** | bug | Form should branch on role; non-Patient roles should call `/external-users/me`. |
| **2.7** | **W-A-8 -- View page heading shows "PQME" for QME bookings (OLD-code branding leak)** | cosmetic | Replace static label with the AppointmentType.Name. |
| **2.8** | **Original Finding 6 -- Angular `_rawValidators` errors (~9 per modal-open) on Claim Information modal** | bug | STILL REPRODUCES post-F5 fix; verified independent. Not a hard demo-stop, but the modal's FormGroup setup is broken; fixing it likely unblocks W-A-4. Audit the modal's `formControlName` bindings and ensure every named control exists at component init. |
| **2.9** | **Verification-rerun Angular WCAB option text bug (W-V-1)** -- WCAB Office dropdown shows 8 options but rendered text is empty for 7 of them (1 placeholder + 7 blank), even though the API returns proper `displayName`. Suggests the Angular template binds the option label from a property other than `displayName` (probably `name` directly). | bug | Separate from F5. Audit the modal template's WCAB Office `<select>` / typeahead binding; should use `displayName` from the LookupDto. |
| **2.10** | **Patient column in `/appointments` queue displays IdentityUser email instead of "Marcus Whitfield"** | bug (cosmetic in queue, demo-killer in audience-facing surfaces) | Tightly related to W-A-1 + W-A-7. The queue's Patient column should join Patient.FirstName + Patient.LastName, not the IdentityUser display name. |
| **2.11** | **View page header status pill does not refresh after Approve (still shows Pending for several seconds)** | bug (cosmetic but confuses the demo audience) | Either trigger a refetch on action-completion or subscribe to the status update via SignalR. |
| **2.12** | **W-UI-11 -- Slot generation with inverted or zero-duration date/time inputs silently produces 0 slots with no UX feedback** | bug (UX gap) | The backend returns an empty array for `FromDate > ToDate`, `FromTime > ToTime`, or `FromTime == ToTime`; the form disables Submit but shows no explanation. Fix: after `generate()` returns an empty preview when the form was otherwise valid, surface an inline message: "No slots were generated. Check that your start date is before your end date and your start time is before your end time." Suspect: `doctor-availability-generate.component.ts` `generate()` method + `validationMessage` wire in the template. |
| **2.13** | **W-UI-14 + W-UI-15 -- Slot generation form renders raw i18n keys: `Enum:BookingStatus.8`, `Enum:BookingStatus.9`, `Enum:BookingStatus.10`, `SetAvailabilitySlot`, `SlotByDates`, `SlotByWeekdays`** | bug (i18n gap) | Same root cause as W-A-5. Add the 6 missing keys to `en.json` with plain-language values (Available, Booked, Blocked; Set Availability Slots; By Dates; By Weekdays). Subsumed by Step 19 (W-A-5 i18n sweep) -- include these keys in that pass. |

### Tier 3 -- Hygiene / future-product decisions

| # | Finding | Severity | Notes |
|---|---|---|---|
| **3.1** | **W-X-9 -- Identity > Users with Doctor role doesn't auto-create Doctor entity** | bug | Same shape as the original Finding 4 fix; either add a `IdentityUserCreatedEventHandler` or document the gap. |
| **3.2** | **Tenant-creation UI does not assign admin role to new tenant admin user** | bug | Found during the verification rerun. Currently use the host admin's "Login with this tenant" feature as workaround. Ship-blocker only if you want tenant creators to use the email/password they entered. |
| **3.3** | **Patient Demographics auto-fill in form (saved on appointment) is not displayed in view's textboxes (DOB blank etc.)** | bug | Tightly related to W-A-7. |
| **3.4** | **W-H-1 -- Re-evaluation form (`?type=2`) is identical to Initial** | product-decision | Decide whether the OLD enum's REEVAL distinction is wanted. |
| **3.5** | **W-I-4 -- No tenant-level IT Admin role** | nice-to-have | Decide whether you want one. Host has it; tenant scope only has admin / Clinic Staff / Staff Supervisor / Doctor + 4 external. |
| **3.6** | **W-INVITE-1 (Original Finding 2) -- Admin invite feature entirely absent: no endpoint, no permission, no token infrastructure, no Angular UI** | product-decision | Round 6 code audit (W-INVITE-1) found 11 discrete build items and estimated ~20-24 working hours. Email infrastructure (Hangfire + `IEmailSender` + `IBackgroundJobManager`) exists and is reusable (W-INVITE-2). Tenant-context switching pattern proven (W-INVITE-3). Decision: reframe demo as self-register (cheap, immediate), OR build the full invite feature (the right product fix, ~3 days). |
| **3.7** | **Original Finding 9 -- Slot creation has no Doctor selector** | product-decision | Per-Location vs. per-Doctor model. Current model is per-Location; demo prompt assumed per-Doctor. |
| **3.8** | **Original Finding 11 -- Duplicate State / Language seed rows + `TestState_XXXX...` boundary fixtures** | data hygiene | Not a code bug per se -- the seeders are idempotent. After fresh seed (post Docker reinstall) data is clean. If duplicates re-appear, add `FindByName` defensive checks to `StateManager.CreateAsync` / `AppointmentLanguageManager.CreateAsync` and remove the boundary fixtures from the production-seed path. |
| **3.9** | **Original Finding 3 -- Patient role had 99 perms during initial run** | data | Not a code bug -- you had granted these manually for testing. Reverted via Identity > Roles. The Tier-1.3 fix (universal external-role read-block) seeds the correct minimal Patient grant set. |
| **3.10** | **Original Finding 4 -- `/patients/me` 404 for newly-created Patient users via host-admin Identity > Users** | bug (subsumed) | Mostly moot once `RegisterAsync` is the supported path (post-F1 fix). Subsumed by W-B-2 (Tier 2.6) for non-Patient roles. The host-admin Identity > Users path is no longer the primary onboarding flow. |
| **3.11** | **W-SLOT-3 -- Appointment Time picker uses time strings (`"09:00:00"`) as option values; Angular maps selected time to the DoctorAvailability GUID client-side** | observation | If two DoctorAvailability rows share the same time at the same location (e.g., two appointment types both starting at 09:00), the picker may show duplicate options or the GUID mapping may be ambiguous. Low probability in practice because conflict detection prevents overlapping slots at the same location. No code change required now; document as a known assumption. Re-evaluate if the per-AppointmentType slot model is introduced. |

### Order to begin fixing today

If you want a single linear order to follow tomorrow:

1. **0.1 W-X-2** -- 30 seconds. One `[Authorize]` annotation on `GetExternalUserLookupAsync`. Ship hot.
2. **0.2 W-A-10** -- 5-15 minutes. Switch `appsettings.Development.json` SMTP to a NoOp sender or fix the credentials so we stop lying to ourselves about email delivery.
3. **1.1 W-A-9** -- 20-40 minutes. Fix the UoW scoping in `GenerateAppointmentPacketJob.GenerateInsideTenantAsync`.
4. **1.3 universal lookup 403s** -- 15-30 minutes. Demote the lookup `[Authorize]` policies or seed external-role grants.
5. **1.5 + 1.8 internal-role grants + seeder role-assignment (W-UI-16)** -- 30-50 minutes. Update `CaseEvaluationRoleDataSeedContributor` for Clinic Staff, Staff Supervisor, Doctor. In the same pass, add `AddToRoleAsync` calls in `InternalUserRoleDataSeedContributor` for each seeded internal user (`maria.rivera@hcs.test`, etc.) so a fresh Docker rebuild produces a fully operational environment without a manual role-grant step.
6. **1.4 W-G-1** -- 15 minutes. Add `[DisableValidation]` to the upload form DTO.
7. **1.7 W-A-6** -- 5-10 minutes. Add a Review item to the queue Actions dropdown.
8. **2.8 Finding 6 (`_rawValidators`)** -- 30 minutes. Audit Claim Information modal's FormGroup setup; this should clear most W-A-4 symptoms.
9. **1.6 W-A-4** -- retry after 8 lands; should now be 5-10 minutes if the modal binding is repaired.
10. **1.2 W-A-2** -- 30-60 minutes. Audit the recipient-resolution logic in the appointment notification handler.
11. **2.1 + 2.13 W-A-5 + W-UI-14/15** -- 30-60 minutes. Sweep `en.json` and template bindings. Include the 6 missing slot-form keys in the same pass: `Enum:BookingStatus.8` (Available), `Enum:BookingStatus.9` (Booked), `Enum:BookingStatus.10` (Blocked), `SetAvailabilitySlot`, `SlotByDates`, `SlotByWeekdays`.
12. **2.9 W-V-1 (WCAB option blank text)** -- 5-10 minutes. Audit the modal's WCAB `<select>` / typeahead binding; switch to `displayName` from `LookupDto`.
13. **2.2 W-A-7** + **3.3** -- 20-30 minutes. Surface DA + Claim sections in the view DTO and view template; populate textboxes from saved data.
14. **2.10 Patient-column display** -- 10 minutes. Update queue grid template to bind `patient.firstName + patient.lastName`.
15. **2.11 status-pill refresh** -- 15 minutes. Trigger refetch on Approve.
16. **2.12 W-UI-11** -- 15-20 minutes. Add inline validation message to the slot generation form when `generate()` returns an empty preview on a valid form ("No slots were generated. Check that your start date is before your end date and your start time is before your end time.").
17. The remaining Tier-2 items (2.3 / 2.4 / 2.5 + W-REG-4 / 2.6 / 2.7) in any order.
18. Tier-3 items as time permits, gated on product decisions for 3.4 / 3.6 / 3.7.

Estimated total wall-time for Tier 0 + Tier 1 + Tier 2: **~7-12 working hours**.

After each Tier-1 fix lands, replay the corresponding workflow from the **Workflow Replay Guide** (`docs/reports/2026-04-30-workflow-replay-guide.md`) to verify the fix and that no new finding has surfaced.

---

## 1. Demo readiness verdict

**NOT-READY.** The demo lifecycle does not run end-to-end. The first hard stop is **Stage 3 registration**: the AuthServer Register page makes cross-origin AJAX calls to the API host (`localhost:44368 -> localhost:44327`) and the API's CORS configuration in `docker-compose.yml` does not list the AuthServer origin, so the tenant-options and register endpoints both fail at the browser before the request reaches the server. Even with a host-admin workaround that creates the user via `Identity > Users`, **Stage 4 cannot complete**: the booking form throws a global "An error has occurred / There is no entity Patient" modal because no Patient domain entity is created for newly registered Patient role users, and the Claim Information modal triggers a 500 on `wcab-office-lookup` (Mapperly mapping missing) plus 9 Angular `_rawValidators` errors that break form binding. Stage 5 (submit) cannot be reached, so Stages 6-8 are blocked-by-construction in this run.

Two bright spots: (1) **cross-tenant isolation works** at the API layer (Dr Thomas 1 admin gets a clean 404 for every Dr Rivera 2 GUID we probed), and (2) the W2-10 **recurring Hangfire jobs** are all registered with the right timezone. The shortest path to demo-ready is to (a) add `http://localhost:44368` to the API's `App__CorsOrigins` env in `docker-compose.yml`, (b) register a Mapperly mapping for `WcabOffice -> LookupDto<Guid>`, (c) create a Patient domain entity on external Patient registration (and document that the host-admin Identity workaround must also do this), and (d) tighten the Patient role's seeded permission set so it is not effectively a tenant admin. Without those four fixes, this demo will visibly break in front of a non-technical stakeholder before the second click.

---

## 2. Lifecycle run log -- Run A (Patient as booker)

- **Stage 0 (setup):** PASS. `admin@abp.io` impersonated `Dr Rivera 2` admin via `Saas/Tenants -> Login with this tenant`; the four GATE permissions (`CaseEvaluation.AppointmentChangeLogs`, `CaseEvaluation.CustomFields` + `.Create`, `CaseEvaluation.Dashboard.Tenant`) were already granted on the seeded `admin` role -- no manual grant required. All three W2-10 recurring jobs (`appt-day-reminder`, `appt-cancellation-reschedule-reminder`, `appt-request-scheduling-reminder`) registered with `America/Los_Angeles` timezone.
- **Stage 1 (slot creation):** PASS-WITH-OBSERVATIONS. Slot count grew from 62 to 145 (block-grouped count from 62 to 63). The form has **no Doctor picker** (slots are per-Location only, not per-Doctor). Block-existence detector flags overlap aggressively (see Finding 8). Created HCS Los Angeles Office 5, 2026-05-04, 10:00-12:00, AppointmentType=QME.
- **Stage 2 (invite external booker):** BLOCKED. The "invite external user" UI surface does not exist. There is only a public self-register endpoint at `POST /api/public/external-signup/register` and no `/invite` endpoint anywhere in the codebase. See Finding 2.
- **Stage 3 (register and login):** BLOCKED + WORKAROUND. The Register page at `http://localhost:44368/Account/Register` calls `GET /api/public/external-signup/tenant-options` which is rejected by browser CORS (no `Access-Control-Allow-Origin` for the AuthServer origin); the tenant dropdown never renders, and the form's eventual `POST .../register` is also CORS-blocked. User saw "Unable to register now. Please try again." Workaround: created `qa.patient.20260429a@hcs.test` via host-admin `Identity > Users -> + New User` with role Patient. See Finding 1.
- **Stage 4 (booking form):** BLOCKED. As `qa.patient.20260429a@hcs.test`, opening `Book Appointment` triggered a global error modal "An error has occurred! There is no entity Patient with id = 7335a1f6-..." because `GET /api/app/patients/me` returns 404 -- no Patient domain entity exists for newly created Patient role users. Workaround: switched to seeded `lan.lewis@hcs.test`, where Patient Demographics auto-populated with seeded data. Selecting AppointmentType triggered no 403 (the prompt's predicted 4a 403 did not reproduce; Patient role has 99 CaseEvaluation permissions which inadvertently grants the policy). Clicking the Claim Information `Add +` button (the demo's "Insurance modal") triggered a 500 on `GET /api/app/appointment-injury-details/wcab-office-lookup` due to a missing Mapperly mapping, plus nine Angular `Cannot read properties of null (reading '_rawValidators')` errors that break the modal's form binding. A persistent "An internal error occurred during your request!" toast then intercepts pointer events and blocks subsequent clicks. See Findings 3, 4, 5, 6.
- **Stage 5 (submit):** BLOCKED-BY-CONSTRUCTION. With Stage 4 unable to satisfy required-field validation (WCAB Office dropdown empty, modal form bindings broken, persistent error toast blocking the page), the Save click did not produce a `POST /api/app/appointments` request. No submission occurred.
- **Stage 6 (office-side review):** NOT REACHED in this run. Tenant admin login worked separately (verified via `maria.rivera@hcs.test`), the Appointments list returned 12 seeded appointments. The bc5de49 fix for the cog-Actions Review item could not be verified end-to-end in the absence of a fresh appointment created by Run A.
- **Stage 7 (resubmit):** NOT REACHED.
- **Stage 8 (approve / packet):** NOT REACHED.

## 3. Lifecycle run log -- Run B (Applicant Attorney as booker)

Run B was not executed end-to-end. Rationale: every blocker on the Run A path (Findings 1, 2, 3, 4, 5, 6) reproduces identically for Applicant Attorney (the CORS rule does not vary by role; `wcab-office-lookup` is role-independent; and any newly-registered AA via the host-admin workaround would face the same domain-entity-missing problem because there is no AA-side equivalent of `/patients/me` either -- the pattern is the same). The findings in section 4 below are tagged "Run: both" where they apply to Run B as well.

---

## 4. Findings

### Finding 1: AuthServer Register page is broken by API CORS misconfiguration

- **Stage:** 3
- **Severity:** blocker
- **Repro confidence:** high (3 runs across two browser contexts; deterministic)
- **Run:** both
- **Reproduction recipe:**
  1. Open a fresh browser context with no cookies.
  2. Go to `http://localhost:4200/`, click `Login`, click `switch`, type `Dr Rivera 2`, save.
  3. Click `Register` on the login page (or browse directly to `http://localhost:44368/Account/Register`).
  4. Open DevTools -> Network. Observe the page never renders a tenant picker.
  5. Fill any synthetic User name / Email / Password, click Register.
- **Symptom:** No tenant selector ever appears on the Register page. After clicking Register, an alert dialog appears: "Unable to register now. Please try again." (User clicks dismiss, returns to a stale form.)
- **Network evidence:**
  - URL: `GET http://localhost:44327/api/public/external-signup/tenant-options`
  - Status: `(failed) net::ERR_FAILED` (browser-side CORS rejection)
  - URL: `POST http://localhost:44327/api/public/external-signup/register`
  - Status: `(failed) net::ERR_FAILED`
  - Request body (sanitized): `{"userType":1,"firstName":"qa.patient.20260429a","lastName":"User","email":"qa.patient.20260429a@hcs.test","password":"<redacted>","tenantId":null}`
  - Direct `curl -X OPTIONS` of the same URL returns `405 Method Not Allowed` (no `Access-Control-Allow-Origin` header), so preflight fails.
- **Browser console:**
  ```
  Access to fetch at 'http://localhost:44327/api/public/external-signup/tenant-options' from origin 'http://localhost:44368' has been blocked by CORS policy: No 'Access-Control-Allow-Origin' header is present on the requested resource.
  Access to fetch at 'http://localhost:44327/api/public/external-signup/register' from origin 'http://localhost:44368' has been blocked by CORS policy: Response to preflight request doesn't pass access control check: No 'Access-Control-Allow-Origin' header is present on the requested resource.
  ```
- **Backend log excerpt:** No matching API entry for the preflight; the GET (when bypassing CORS via `curl -H 'Origin: http://localhost:44368'`) returns 200 with the expected JSON, confirming the server does serve the data -- the only failure is at the CORS layer.
- **Suspect files:**
  - `docker-compose.yml` (api service env) -- the `App__CorsOrigins` for the api service is set to `http://localhost:${NG_PORT:-4200}` only. The AuthServer origin (`http://localhost:${AUTH_PORT:-44368}`) is missing. The committed `appsettings.json` value (`http://localhost:4200,https://localhost:44368`) lists HTTPS for the AuthServer; the compose env override masks it, and even unmasked the protocol mismatch (AuthServer runs HTTP locally) would still fail.
  - `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/HealthcareSupportCaseEvaluationHttpApiHostModule.cs` (or wherever `AddCors` is wired) -- secondary check that the policy is named and applied to all routes including `/api/public/*`.
  - `src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/Account/RegisterModel.cshtml.cs` and `wwwroot/global-scripts.js` -- the architectural choice to call the API host cross-origin from the AuthServer Razor page rather than wrapping it in a server-side handler is what creates the CORS dependency in the first place.
- **Hypothesis tree (ranked):**
  1. **Most likely:** The API service env override `App__CorsOrigins` in `docker-compose.yml` is missing the AuthServer origin. Evidence for: docker-compose.yml line shows `App__CorsOrigins: "http://localhost:${NG_PORT:-4200}"` for the api service; preflight returns no CORS header; the committed appsettings.json has the right value but is overridden in container env. Against: none.
  2. **Possible:** The CORS policy in `HttpApiHostModule` parses the comma-separated env value but trims/filters incorrectly, dropping the AuthServer entry even when it is set. Evidence for: ABP's `Origins.Split(',').Select(o => o.RemovePostFix("/"))` is sometimes case-sensitive on protocol mismatch. Against: the env var as currently set does not even contain the AuthServer origin, so this branch is moot until the env is fixed.
  3. **Less likely:** The Register page should not be making a cross-origin AJAX at all; it should be a Razor Page handler that calls the AppService server-side, eliminating the CORS dependency entirely. Evidence for: ABP's reference auth flows do this. Against: refactoring is a wave-scope change; the env fix unblocks the demo today.
- **Recommended fix direction:** In `docker-compose.yml`, change the `api` service env to `App__CorsOrigins: "http://localhost:${NG_PORT:-4200},http://localhost:${AUTH_PORT:-44368}"` and rebuild the api container. Verify `curl -i -X OPTIONS -H 'Origin: http://localhost:44368' http://localhost:44327/api/public/external-signup/tenant-options` returns 204 with `Access-Control-Allow-Origin: http://localhost:44368`.
- **Workaround used:** `admin@abp.io` -> `Identity > Users -> + New User` -> set username/email/password, set Roles tab -> Patient -> Save. Bridges Stages 3 and 4a/b only; does not create the Patient domain entity (see Finding 4).

### Finding 2: External-user invite UI/endpoint does not exist

- **Stage:** 2
- **Severity:** blocker
- **Repro confidence:** high
- **Run:** both
- **Reproduction recipe:**
  1. As `maria.rivera@hcs.test` (or any tenant admin), search the Angular UI for "invite external user", "add external user", "external signups", any user-management-adjacent affordance.
  2. Search Swagger at `http://localhost:44327/swagger/index.html` for any controller method named `InviteAsync`, `SendInvite`, `CreateInvitation`, etc.
- **Symptom:** None of these surfaces exist. The only external-signup affordance is the public self-register page reachable via "Not a member yet? Register" on the AuthServer login.
- **Suspect files:**
  - `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/ExternalSignups/ExternalSignupController.cs` -- routes are only `tenant-options`, `external-user-lookup`, `register`. No invite route.
  - `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs` -- methods are `GetTenantOptionsAsync`, `GetExternalUserLookupAsync`, `RegisterAsync`. No invite method.
  - `src/HealthcareSupport.CaseEvaluation.Application.Contracts/ExternalSignups/IExternalSignupAppService.cs` -- mirrors the above.
- **Hypothesis tree (ranked):**
  1. **Most likely:** The product was designed around public self-registration (booker browses to the URL, picks a role, registers), not invite-driven onboarding. The demo prompt's "invite + email link" Stage 2 was modeled on a different product or a future capability. Evidence for: codebase is internally consistent on self-register; no half-implemented invite scaffold exists. Against: the demo audience expects an invite flow per the script, so capability and demo narrative are mismatched.
  2. **Possible:** Invite functionality was scoped out for Wave 2 explicitly. Evidence for: nothing; would need to read the wave plan. Against: even a "deferred" notation would be in the codebase as a TODO.
  3. **Less likely:** The invite path lives in a different service (e.g., AbpAccount/ABP's Identity invitation feature) and is not reflected in CaseEvaluation. Evidence for: AbpAccount has `IdentityUserInvitationManager` patterns. Against: nothing is wired up; no Angular UI surfaces it; not in the menu.
- **Recommended fix direction:** Decide which lever to pull: (a) reframe the demo narrative to "self-register" (cheap, just rewrite the prompt's Stage 2), or (b) implement an `[Authorize] POST /api/app/external-signup/invite` endpoint that accepts `{email, role, tenantId}` and enqueues an email job whose body contains a tokenized link to the public Register page. Option (a) is the only realistic path before a manager demo; option (b) is the right product fix.
- **Workaround used:** Skipped the invite step; jumped to registration directly.

### Finding 3: Patient role grants 99 CaseEvaluation permissions including approve/regenerate/system-edit

- **Stage:** 4 / 8e (HIPAA-relevant)
- **Severity:** blocker (HIPAA compliance, not demo flow)
- **Repro confidence:** high (deterministic)
- **Run:** both
- **Reproduction recipe:**
  1. Log in as any user with role Patient on Dr Rivera 2 (e.g., `qa.patient.20260429a@hcs.test` after the workaround, or seeded `lan.lewis@hcs.test`).
  2. In browser console:
     ```js
     const tok = localStorage.getItem('access_token');
     const r = await fetch('http://localhost:44327/api/abp/application-configuration?includeLocalizationResources=false', { headers: { Authorization: 'Bearer ' + tok }});
     const cfg = await r.json();
     const granted = Object.entries(cfg.auth.grantedPolicies).filter(e => e[0].startsWith('CaseEvaluation.') && e[1]).map(e => e[0]);
     console.log('count', granted.length, granted);
     ```
- **Symptom:** Patient role returns 99 granted `CaseEvaluation.*` policies, including `CaseEvaluation.AppointmentDocuments.Approve`, `CaseEvaluation.AppointmentPackets.Regenerate`, `CaseEvaluation.SystemParameters.Edit`, `CaseEvaluation.CustomFields.Create`, all `*.Create/Edit/Delete` on every domain entity, and `CaseEvaluation.Dashboard.Tenant`. This is identical to the granted set the `admin` role returns.
- **Granted-policies snapshot (relevant keys only):**
  ```json
  {
    "CaseEvaluation.AppointmentDocuments.Approve": true,
    "CaseEvaluation.AppointmentPackets.Regenerate": true,
    "CaseEvaluation.SystemParameters.Edit": true,
    "CaseEvaluation.CustomFields.Create": true,
    "CaseEvaluation.Appointments.Delete": true,
    "CaseEvaluation.Patients.Delete": true,
    "CaseEvaluation.AppointmentChangeLogs": true
  }
  ```
- **Suspect files:**
  - `src/HealthcareSupport.CaseEvaluation.Domain/Identity/ExternalUserRoleDataSeedContributor.cs` -- the seed contributor that maps external roles to permissions; primary suspect for over-broad grant.
  - `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissionDefinitionProvider.cs` (or equivalent) -- where the policy tree is defined; second-check that nothing is set as `IsEnabled = true` by default for all roles.
  - `src/HealthcareSupport.CaseEvaluation.Domain/Data/CaseEvaluationDataSeedContributor.cs` -- check for any blanket `PermissionManager.SetForRoleAsync(roleName, "CaseEvaluation", true)` line.
- **Hypothesis tree (ranked):**
  1. **Most likely:** `ExternalUserRoleDataSeedContributor.cs` (or the equivalent role-seeder) iterates the entire `CaseEvaluation.*` permission group and grants all to every external role rather than scoping per-role permission lists. Evidence for: admin and Patient return identical granted sets, suggesting a single source seeded both. Against: would expect ApplicantAttorney/Defense to also have these (untested in this run, but likely confirms).
  2. **Possible:** A default-policy fallback in middleware grants any authenticated user all CaseEvaluation policies regardless of role. Evidence for: the granted set survives a fresh login as the new Patient. Against: ABP's PermissionChecker is per-role by design and does not have such a fallback unless explicitly configured.
  3. **Less likely:** A test fixture/seeder leaked into runtime. Evidence for: the breadth of the leak. Against: `db-migrator` is a real seeder, not a test fixture; the granted set is reproducible across re-runs.
- **Recommended fix direction:** Open `ExternalUserRoleDataSeedContributor.cs`, audit the role-to-permission mapping. The Patient role should grant only: `Appointments`, `Appointments.Create`, `AppointmentDocuments`, `AppointmentDocuments.Create`, `AppointmentPackets` (read), and selected lookup-read perms. Strip `*.Approve`, `*.Regenerate`, `*.Delete`, `*.Edit` (except own-record edit at app-service-level), `SystemParameters.*`, `CustomFields.*`, `Dashboard.Tenant`. Add a regression test that asserts `Patient` role's granted-policy set is a strict subset of the expected list.
- **Workaround used:** None. The leak is a HIPAA blocker, not a demo blocker.

### Finding 4: Newly registered Patient users have no Patient domain entity, breaking /patients/me and the booking form

- **Stage:** 4
- **Severity:** blocker
- **Repro confidence:** high (deterministic)
- **Run:** both (Patient and Applicant Attorney user-types are likely affected analogously)
- **Reproduction recipe:**
  1. As `admin@abp.io`, create a new user `qa.patient.20260429a@hcs.test` via `Identity > Users -> + New User` while in Dr Rivera 2 tenant context (Login-as-tenant from `Saas/Tenants`). Assign role Patient.
  2. Log out as admin. Log in as the new user with tenant Dr Rivera 2 selected.
  3. Land on the W2 Patient home. Click `Book Appointment`.
- **Symptom:** Booking form renders with a global modal: "An error has occurred! There is no entity Patient with id = `7335a1f6-55dd-f29b-8378-3a20ee64e399`!" The Email field is rendered disabled but blank (no auto-populated value). Most demographics fields are empty.
- **Network evidence:**
  - URL: `GET http://localhost:44327/api/app/patients/me`
  - Status: `404 Not Found`
  - Response body:
    ```json
    {"error":{"code":null,"message":"There is no entity Patient with id = 7335a1f6-55dd-f29b-8378-3a20ee64e399!","details":null,"data":null,"validationErrors":null}}
    ```
- **Backend log excerpt (`main-api-1`):**
  ```
  [02:33:54 ERR] There is no such an entity. Entity type: HealthcareSupport.CaseEvaluation.Patients.Patient, id: 7335a1f6-55dd-f29b-8378-3a20ee64e399
  Volo.Abp.Domain.Entities.EntityNotFoundException: There is no such an entity. Entity type: HealthcareSupport.CaseEvaluation.Patients.Patient, id: 7335a1f6-55dd-f29b-8378-3a20ee64e399
     at HealthcareSupport.CaseEvaluation.Patients.PatientsAppService.GetCurrentPatientWithNavigationAsync() in /src/src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs:line 385
     at HealthcareSupport.CaseEvaluation.Patients.PatientsAppService.GetMyProfileAsync() in /src/src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs:line 240
  ```
- **Suspect files:**
  - `src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs:385` -- `GetCurrentPatientWithNavigationAsync()` lookup-by-IdentityUserId throws if no Patient exists; should be tolerant or the upstream registration flow should create the Patient row.
  - `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs:170+` (`RegisterAsync`) -- this is where the Patient row should be created when `userType == 1`. Audit confirms the registration flow only creates the IdentityUser + role assignment; no domain entity wiring.
  - `src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs:240` -- `GetMyProfileAsync()` should either auto-create a draft Patient on first call, or the booking form's component should handle 404 gracefully.
- **Hypothesis tree (ranked):**
  1. **Most likely:** `ExternalSignupAppService.RegisterAsync` skips the domain-entity creation step. The host-admin Identity Users flow has the same gap because it never knew it should create a Patient. Both paths leave a dangling IdentityUser without a Patient. Evidence for: stack trace points squarely at PatientsAppService line 385; the user was created moments before the call; the user has the right role and tenant; nothing else makes sense as the missing piece. Against: none.
  2. **Possible:** The Patient is supposed to be created on first booking submit, not on registration. Evidence for: this is a defensible design (lazy creation). Against: the form fails before any submit, so even lazy creation needs a 404-tolerant `GetMyProfileAsync` first read.
  3. **Less likely:** `IdentityUser.Id` and `Patient.Id` were intended to be the same primary key with a 1:1 mapping seeded by a domain event. Evidence for: this is the seeded pattern (`lan.lewis@hcs.test` works because the seed creates both). Against: the registration flow does not raise such an event.
- **Recommended fix direction:** In `ExternalSignupAppService.RegisterAsync` (and any host-admin user creation path that needs to mirror), after `_userManager.CreateAsync` and role assignment succeed, when `userType == Patient`, immediately `_patientRepository.InsertAsync(new Patient(identityUser.Id, identityUser.Email, identityUser.Name, identityUser.Surname))`. Mirror for `ApplicantAttorney`/`DefenseAttorney`/`ClaimExaminer` if they have analogous domain entities. Add a defensive `try/catch (EntityNotFoundException)` in `GetMyProfileAsync` returning a draft DTO so the booking form can still render.
- **Workaround used:** Switched to seeded Patient `lan.lewis@hcs.test` (whose Patient entity is created by `db-migrator`'s seeder). Lan's booking form rendered with all demographics auto-populated.

### Finding 5: WCAB office lookup returns 500 due to missing Mapperly mapping

- **Stage:** 4d
- **Severity:** blocker
- **Repro confidence:** high (deterministic)
- **Run:** both
- **Reproduction recipe:**
  1. Log in as any user (lan.lewis or qa.patient or maria.rivera).
  2. Open the booking form: `http://localhost:4200/appointments/add?type=1`.
  3. Pick AppointmentType + Location, scroll to Claim Information, click `Add +`.
- **Symptom:** Modal opens but the `WCAB Office (Venue)` dropdown is empty (only the placeholder "Select"). Browser shows a sticky red toast: "An internal error occurred during your request!" that intercepts subsequent clicks. Form binding for the modal also crashes (see Finding 6).
- **Network evidence:**
  - URL: `GET http://localhost:44327/api/app/appointment-injury-details/wcab-office-lookup?skipCount=0&maxResultCount=200`
  - Status: `500 Internal Server Error`
- **Backend log excerpt (`main-api-1`):**
  ```
  Volo.Abp.AbpException: No object mapping was found for the specified source and destination types.
  Mapping attempted:
  List`1 -> List`1
  System.Collections.Generic.List`1[[HealthcareSupport.CaseEvaluation.WcabOffices.WcabOffice, ...]] -> System.Collections.Generic.List`1[[HealthcareSupport.CaseEvaluation.Shared.LookupDto`1[[System.Guid, ...]], ...]]
  How to fix:
  Define a mapping class for these types:
     - Use MapperBase<TSource, TDestination> for one-way mapping.
  ```
- **Suspect files:**
  - `src/HealthcareSupport.CaseEvaluation.Application/AppointmentInjuryDetails/AppointmentInjuryDetailAppService.cs` -- `GetWcabOfficeLookupAsync` calls `ObjectMapper.Map<List<WcabOffice>, List<LookupDto<Guid>>>(...)` without a registered mapper.
  - `src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationAutoMapperProfile.cs` (or the Mapperly-equivalent) -- needs a `[MapperBase<WcabOffice, LookupDto<Guid>>]` mapping definition.
  - Compare against `src/HealthcareSupport.CaseEvaluation.Application/ApplicantAttorneys/ApplicantAttorneyAppService.cs::GetStateLookupAsync` which works (returned 200 in the same run) -- the State lookup mapping is registered; copy its pattern.
- **Hypothesis tree (ranked):**
  1. **Most likely:** Mapperly mapping for `WcabOffice -> LookupDto<Guid>` was never added when WCAB lookups were wired. Evidence for: the AbpException explicitly says "No object mapping was found"; the suggested fix is exactly that. Against: none.
  2. **Possible:** A previous code-gen step deleted the mapping during a refactor and was not regenerated. Evidence for: nothing direct. Against: the rest of the lookup mappers are present and working.
  3. **Less likely:** The `LookupDto<TKey>` generic mapper requires a non-generic intermediate. Evidence for: Mapperly does have edge cases with generic destinations. Against: the State lookup uses the same generic pattern and works.
- **Recommended fix direction:** In `CaseEvaluationApplicationAutoMapperProfile.cs` (or the Mapperly partial profile), register `WcabOffice -> LookupDto<Guid>` (`Id -> Id`, `Name -> DisplayName`). Re-run the application; the 500 should clear and the dropdown should populate.
- **Workaround used:** None viable; the modal cannot be filled without WCAB office.

### Finding 6: Claim Information modal crashes form binding with 9 Angular `_rawValidators` errors

- **Stage:** 4d
- **Severity:** blocker (regression from working form binding)
- **Repro confidence:** high (deterministic, alongside Finding 5)
- **Run:** both
- **Reproduction recipe:** Same as Finding 5.
- **Symptom:** Modal opens visually, but interacting with any modal control is unreliable; the form group does not register its child controls.
- **Browser console:**
  ```
  ERROR TypeError: Cannot read properties of null (reading '_rawValidators')
      at lf (http://localhost:4200/chunk-IQLKQTZL.js:2:106015)
      at Pu (.../chunk-IQLKQTZL.js:2:122636)
      at gi (.../chunk-IQLKQTZL.js:2:121968)
      at t.addControl (.../chunk-IQLKQTZL.js:2:136595)
      at t._setUpControl (.../chunk-IQLKQTZL.js:2:130352)
      at t.ngOnChanges (.../chunk-IQLKQTZL.js:2:129862)
      ... (9 occurrences, one per missing FormControl)
  ```
- **Suspect files:**
  - `angular/src/app/appointments/components/claim-information/claim-information.component.ts` (or equivalent path under `appointments/`) -- the modal's form group probably declares controls in `ngOnInit` but the template uses `formControlName="..."` for fields whose backing control never gets `addControl`'d (e.g., `wcabOfficeId` may be conditionally added only after the WCAB lookup succeeds; with Finding 5 the lookup never returns, so the control stays null, and other validators throw when ngForm tries to wire siblings).
  - `angular/src/app/appointments/appointments.module.ts` -- check for `ReactiveFormsModule` registration; not the cause here but a quick sanity check.
- **Hypothesis tree (ranked):**
  1. **Most likely:** The modal's form group depends on the WCAB office list returning before binding; the 500 on `wcab-office-lookup` (Finding 5) leaves the WCAB form control null, and 9 sibling validators error when ngForm tries to attach them. Fixing Finding 5 will make most of these errors disappear. Evidence for: the error count matches the number of fields in the Claim Information modal (Cumulative Trauma, Date of Injury, Claim Number, WCAB Office, ADJ#, Body Parts, plus Insurance and Claim Examiner sub-toggles = approximately 9). Against: a properly defensive form should still bind even when one optional dropdown's options are empty.
  2. **Possible:** The modal uses an outdated `FormGroupDirective` pattern incompatible with Angular 20's standalone component change-detection. Evidence for: Angular 20 strict null checks have been tightened. Against: only one form (this one) in the app exhibits this; others work.
  3. **Less likely:** A timing race between modal show and form-control creation. Evidence for: Bootstrap modals fire `shown.bs.modal` after CD. Against: ngForm should not look at validators before its own ngOnInit.
- **Recommended fix direction:** Land Finding 5 first; then re-test. If the validator errors persist, audit the modal's `formGroup`/`formControlName` pairs to ensure every named control exists at component init (provide a defensive `[]` fallback for lookup-driven options so the FormControl is always created even when its options array is empty).
- **Workaround used:** None; the modal is the only place to enter Claim Number and Body Parts, both of which are required for submit.

### Finding 7: Persistent error toast intercepts pointer events after a 5xx

- **Stage:** 4d (manifests after Finding 5)
- **Severity:** bug
- **Repro confidence:** high
- **Run:** both
- **Reproduction recipe:**
  1. Trigger any 5xx response (e.g., open the Claim Information modal, see the WCAB 500).
  2. Wait for the red ABP toast "An internal error occurred during your request!" to render.
  3. Try to click any other control on the page (Close button, modal background, form input).
- **Symptom:** The toast remains on top and intercepts clicks indefinitely. Playwright reports `<p class="message ng-star-inserted">An internal error occurred during your request!</p> from <abp-confirmation> subtree intercepts pointer events`.
- **Suspect files:**
  - `src/HealthcareSupport.CaseEvaluation.AuthServer/wwwroot/global-scripts.js` -- not directly relevant.
  - `node_modules/@abp/ng.theme.lepton-x/...` (or wherever `<abp-confirmation>` lives) -- the confirmation component's z-index or `pointerEvents: auto` on the wrapper without an auto-dismiss timer is the likely culprit.
- **Hypothesis tree (ranked):**
  1. **Most likely:** The ABP toast's outer container uses `pointer-events: auto` on a full-screen overlay even though the visible part is a small toast. The toast does have a Close button, but the underlying overlay covers the page. Evidence for: Playwright's intercept message points at the toast element. Against: this might be the intended ABP UX for confirmation modals (which need user input) but is wrong for fire-and-forget toasts.
  2. **Possible:** A duplicate `<abp-confirmation>` element is rendered for each 5xx and never cleaned up. Evidence for: nothing direct. Against: only one toast was visible.
- **Recommended fix direction:** Add a global override in `angular/src/styles.scss` setting `abp-confirmation { pointer-events: none; }` and `abp-confirmation .message { pointer-events: auto; }` so only the toast body itself intercepts. Or auto-dismiss after 5s via the toaster service.
- **Workaround used:** Reload the page.

### Finding 8: Slot-creation form blocks submit with "Some generated slots already exist" even on adjacent times

- **Stage:** 1
- **Severity:** cosmetic
- **Repro confidence:** medium (1 run with 2 attempts)
- **Run:** setup-only
- **Reproduction recipe:**
  1. As `maria.rivera@hcs.test`, open `Doctor Availabilities -> Add`.
  2. Pick a Location that already has any slot on the chosen date.
  3. Pick a non-overlapping time band on that date (e.g., existing slot 09:00-12:00, choose 13:00-15:00).
  4. Click `GenerateSlot`.
- **Symptom:** The form generates the new block in the preview table but the global warning "Some generated slots already exist. Please remove them before submitting." persists and `Submit` stays disabled even though the new block does not overlap any existing slot.
- **Suspect files:**
  - `angular/src/app/doctor-management/doctor-availabilities/components/availability-add/...` -- the duplicate-detection logic appears to compare on (Location, Date) only rather than (Location, Date, FromTime, ToTime).
- **Hypothesis tree (ranked):**
  1. **Most likely:** The dedup query checks only Location+Date, not the full time range. Evidence for: changing the date to a fresh one allowed submit; same Location+Date but different times did not.
  2. **Possible:** The warning is sticky from a previous GenerateSlot run within the same form session.
- **Recommended fix direction:** Change the duplicate check to (LocationId, AvailableDate, FromTime, ToTime) overlap (interval intersection), not equality. Reset the warning on each `GenerateSlot` invocation.
- **Workaround used:** Pick a different date (2026-05-04 instead of 2026-05-01).

### Finding 9: Slot creation form has no Doctor selector

- **Stage:** 1
- **Severity:** nice-to-have (informational; the demo prompt expected a Doctor field)
- **Repro confidence:** high
- **Run:** setup-only
- **Symptom:** The slot-creation form binds slots to Location only, not to a specific Doctor. The seeded slot table mirrors this -- no Doctor column. The demo prompt's "vary by Doctor" expectation cannot be satisfied without code changes.
- **Recommended fix direction:** Decide whether slots are per-location (current model -- any doctor at that location can fulfill) or per-doctor. If per-doctor, add a `DoctorId` column to `DoctorAvailability`, surface it in the form and list, and update the booker view to display the doctor name on the slot pick.
- **Workaround used:** None; the location-only model is the as-built behavior.

### Finding 10: AppointmentType dropdown only surfaces types with at least one available slot

- **Stage:** 4a
- **Severity:** bug
- **Repro confidence:** high (deterministic)
- **Run:** both
- **Symptom:** On the booking form, `AppointmentType *` dropdown shows only "Record Review" even though seven types exist in the seed data (QME, Panel QME, AME, Record Review, Deposition, Supplemental Medical Report). The newly-created QME slot at HCS LA Office 5 / 2026-05-04 (Run A Stage 1) did not surface.
- **Network evidence:** `GET /api/app/appointments/appointment-type-lookup` returns 200; the filtering happens server-side based on slot availability join.
- **Hypothesis tree (ranked):**
  1. **Most likely:** The lookup is filtered by joining DoctorAvailability with bookingStatusId == 8 (Available), and the filter date-window does not include 2026-05-04. Evidence for: the form's only visible Locations are also a subset (HCS San Bernardino 2, HCS San Diego 3, HCS Van Nuys 4 -- the only locations with seeded available slots in the near window). Against: the filter logic should respect future slots within a wider window.
  2. **Possible:** The lookup excludes types whose Tenant scope differs.
- **Recommended fix direction:** Decide the intended UX. If "show only types where future Available slots exist" is intentional, document it. If not, expand the lookup window or filter only by Type.IsActive.
- **Workaround used:** Selected the only available type (Record Review).

### Finding 11: State and Language seed lookups contain duplicates

- **Stage:** 4
- **Severity:** cosmetic
- **Repro confidence:** high
- **Run:** both
- **Symptom:** State dropdown lists `California` twice, `Texas` twice, `Hawaii` twice, etc. Language dropdown lists `English` twice, `Vietnamese` twice, `Japanese` twice. There is also a `TestState_XXXX...` and `TestLang_XXXX...` entry from boundary-condition seeding that should not be in production data.
- **Suspect files:**
  - `src/HealthcareSupport.CaseEvaluation.Domain/Data/CaseEvaluationDataSeedContributor.cs` (or equivalent) -- the seeder probably runs twice (once at host scope, once at tenant scope) and the per-tenant run does not check for existing rows.
- **Recommended fix direction:** Make the seeder idempotent (`if not exists by Name then insert`). Remove the `TestState_XXXX` and `TestLang_XXXX` boundary fixtures from the production-seed path; keep them in test seeders only.
- **Workaround used:** None.

---

## 5. Cross-tenant isolation results

PASSING. As `anahit.thomas@hcs.test` (tenant Dr Thomas 1), with three known `Dr Rivera 2` appointment GUIDs (`0600594f-...`, `07841f5e-...`, `417fb159-...`):

| Probe | Expected | Observed |
|---|---|---|
| `/api/app/appointments` queue | only Thomas rows | 14 rows, all Thomas-scoped |
| `/api/app/dashboard` counts | Thomas-only | `pendingRequests=2, totalDoctors=0` (Thomas data only; Rivera's 12 not visible) |
| `GET /api/app/appointments/<rivera-guid>` (x3) | 404 / 403 | 404 with `EntityNotFoundException` envelope ("There is no entity Appointment with id = ...") |
| `GET /api/app/appointment-change-logs/by-appointment/<rivera-guid>` | 404 / 403 | 404 |
| `GET /api/app/appointment-packets/<rivera-guid>/download` | 404 / 403 | 404 |

No leaks observed. The chosen 404 + EntityNotFoundException envelope (rather than 403) is acceptable from a HIPAA data-leak perspective: the response does not confirm or deny the row exists in another tenant; it states the row does not exist in the current tenant context.

---

## 6. Email fan-out audit

| Stage | Expected recipients | Actual recipients | Match? |
|---|---|---|---|
| 2 (invite) | 1 (booker) | 0 (invite endpoint does not exist; see Finding 2) | NO -- not implemented |
| 3 (register) | 0 (no email per current arch) | 0 | YES (no-op) |
| 5 (submit) | 6 (booker, patient, AA, DA, claim examiner, office) | NOT REACHED | N/A |
| 6 (send-back) | 6 | NOT REACHED | N/A |
| 7 (resubmit) | 6 | NOT REACHED | N/A |
| 8a (approve) | 6 | NOT REACHED | N/A |

Total Hangfire jobs at end of run: succeeded=1 (a startup-time job), enqueued=0, processing=0, failed=0. **No `SendAppointmentEmailJob` fired during the run because no appointment was successfully submitted** (Stage 4 blockers stopped the lifecycle before submit). The W2-10 fan-out infrastructure could not be exercised in this run; the 3 recurring jobs are registered but their first scheduled fire is in the future.

---

## 7. Expected gaps observed

These are informational, not findings:

- **Per-recipient PDF templates** -- not exercised (Stage 8 not reached).
- **SignalR push for packet status** -- not exercised; UI uses 5s polling per spec.
- **Email body content polish (tenant-customized From-name / TZ-aware dates)** -- not exercised (no emails fired).
- **Real SMTP** -- correctly absent; Hangfire-as-inbox model confirmed (succeeded queue is the observation surface).
- **W2-5 dedicated Angular admin module** for `AppointmentTypeFieldConfig` -- absent in menu, as expected; managed via Swagger only. The booking form does call `appointment-type-field-configs/by-appointment-type/<guid>` (200), so the runtime side works.
- **W2-5 visual `[hidden]` on form rows** -- not observed; not exercised.
- **Host dashboard 13-card grid** -- not exercised; tenant dashboard endpoint (`/api/app/dashboard`) returns the 13-field DTO correctly.
- **Audit retention pruning job** -- absent, as expected.
- **Anonymous (verification-code) document download** -- not exercised.
- **Drag-and-drop + multi-file upload** on Documents -- not exercised (Stage 8 not reached).
- **Packet email link** -- not exercised.

---

## 8. Console errors collected

Aggregated by URL / pattern (non-trivial only):

- `GET /api/app/patients/me 404` (Stage 4) -- 1 occurrence per booking-form open as a Patient role user without a Patient entity. See Finding 4.
- `GET /api/app/appointment-injury-details/wcab-office-lookup 500` (Stage 4d) -- 1 occurrence per Claim Information modal open. See Finding 5.
- `Cannot read properties of null (reading '_rawValidators')` (Stage 4d) -- 9 occurrences per Claim Information modal open. See Finding 6.
- `CORS policy ... external-signup/tenant-options blocked` (Stage 3) -- 2 occurrences per Register page load. See Finding 1.
- `CORS policy ... external-signup/register blocked` (Stage 3) -- 2 occurrences on Register submit. See Finding 1.
- `ERROR je @ http://localhost:4200/chunk-2TA7XPCU.js:3` (Stage 4) -- generic Angular error-handler log; downstream of the underlying HTTP errors above.
- `Failed to load resource: net::ERR_FAILED` -- always paired with one of the CORS lines above, never standalone.

---

## 9. Test environment

- **Branch / commit:** `feat/mvp-wave-2` @ `bc5de49` (local branch is 13 commits ahead of `origin/feat/mvp-wave-2`; not pushed in this run).
- **Browser MCP:** Playwright MCP, Chrome 147 underlying, single context (closed and re-opened across identities by clearing localStorage + AuthServer logout).
- **Docker compose ps snapshot:** 5 services up + healthy (`api`, `authserver`, `redis`, `sql-server` healthy; `angular` up without a healthcheck) and `db-migrator` exited 0. All ports bound to `127.0.0.1` per W1-bugfix Option A. Uptime ~4 hours at end of run.
- **Seed-data anomalies:**
  - `State` table contains duplicate rows for ~10 states (see Finding 11).
  - `AppointmentLanguage` table contains duplicate rows for ~10 languages.
  - `TestState_XXXX...` and `TestLang_XXXX...` boundary-fixture rows are present in the state and language lookups.
  - `AppointmentType` table appears to have duplicates as well (QME, Record Review, AME each appear twice in the slot-creation dropdown).
  - DoctorAvailability slot count: 62 -> 145 over the run (block-grouped count 62 -> 63). All 62 seeded slots are dated 2026-03-26 to 2026-04-29-ish, i.e. in the past relative to today.
- **New users created via UI in this run:**
  - `qa.patient.20260429a@hcs.test` / `qa.patient.20260429a` -- tenant Dr Rivera 2, role Patient. Created via host-admin Identity Users workaround. **No corresponding Patient domain entity** (Finding 4).
- **Slots created via UI in this run:**
  - 1 block at HCS Los Angeles Office 5, 2026-05-04, 10:00-12:00, AppointmentType=QME, BookingStatus=Available.

---

## 10. Open questions for Adrian

1. **Invite vs self-register:** Was the demo prompt's "Stage 2 invite" intended as a future capability or a misalignment with the as-built self-register architecture? If self-register is the product direction, the demo narrative needs to be rewritten; if invite is the product direction, an entire endpoint + email job + UI page need to ship before the demo.
2. **Patient role permission scope:** Should the Patient role have the 99 CaseEvaluation permissions it currently has (effectively tenant-admin), or should it be narrowed to a few self-scoped read/write perms? The current breadth is HIPAA-relevant and warrants a security review before any non-employee user logs in.
3. **Patient domain entity creation:** Where in the registration flow should the Patient row be created -- at register time, on first GetMyProfile, on first appointment submit? Each has different implications for booking-form initial state.
4. **AppointmentType dropdown filtering:** Is the "show only types with available slots" filter intentional? It feels like a UX choice but it makes the demo narrative ("multiple appointment types") fail unless slots covering all types are pre-seeded.
5. **Slot model -- per-Location or per-Doctor:** The current per-Location model conflicts with the demo prompt's per-Doctor expectation. Which is canonical?
6. **CORS architecture:** Is the AuthServer Register page expected to AJAX cross-origin to the API, or should that path be refactored to a Razor Page handler that calls the AppService server-side? The cross-origin path adds the CORS surface area for free; the Razor handler eliminates it.

---

## 11. Defense Attorney + Claim Examiner -- register-only, by design (NO entity follow-up)

**Decision (Adrian, 2026-04-30):** Defense Attorney and Claim Examiner intentionally have NO user-level domain entity today and are NOT scheduled to get one. They register, login, and book appointments through the same UI as the other 4 external roles, but:

- **No saved profile.** When a DA or CE registers, only an `IdentityUser` row is created (with the matching role). No `DefenseAttorney` row is created either, despite the entity existing in the codebase.
- **No external-user lookup exposure.** `GetExternalUserLookupAsync` returns ONLY Patient and Applicant Attorney. DA + CE never appear in any picker, dropdown, or autocomplete.
- **No pre-fill on booking.** A DA or CE booker types their own firm/contact info on every appointment; nothing is pre-populated from a saved record.
- **No tenant-admin management page for DA or CE profiles.** The existing `/applicant-attorneys` page (Patient + AA management) does not get a DA or CE counterpart.

**Rationale (Adrian's words):** "we dont want anyone to look up and prefills for them right now, we might add them later but for now lookups and prefills only for Patient and Applicant Attorneys, the people who are most likely to book an appointment or re-evaluation". DA and CE rarely initiate bookings; they appear primarily as required parties on appointments booked by Patient or AA. Exposing their saved profile data via an unauthenticated-or-broad lookup leaks PII without enabling a workflow benefit.

**What this means for code:**
- The existing `DefenseAttorney` aggregate root + repository + AppService stay where they are -- unused by the register flow, unused by the booking form's pre-fill, unused by any lookup. They remain available if a tenant admin wants to manually create DA records, but no automatic creation path exists.
- The booking form still captures DA + CE data per-appointment (Name, Email, Phone, etc.) as required text fields typed by the booker. These persist on the appointment-side join entities (`AppointmentDefenseAttorney`, `AppointmentClaimExaminer`).
- Email fan-out (S-NEW-3) still emails DA + CE using the email captured on the appointment. The conditional login/register link logic still applies: if the recipient is registered under the matching role at this tenant, they get a "log in" link; otherwise they get a "register" link. Registration does not depend on a saved profile entity existing.

**Reversibility:** if Adrian later decides DA or CE should have saved profiles, the lift is identical to the current S-NEW-1 work for AA (one branch in `RegisterAsync` plus an entry in `GetExternalUserLookupAsync`'s allowed-roles list). Nothing about this current decision creates a one-way door.

**No further work tracked under this section.** Items that previously sat here (CE entity scaffold, optional `AppointmentClaimExaminer.ClaimExaminerId` FK, CE management page) are explicitly deferred indefinitely and removed from the implementation plan.
