# Bug Catalog Research -- 2026-05-06

Research output for the 15 bugs surfaced during today's lifecycle test
(Patient + AA + DA + CE register/verify/login + DA & CE book + admin
SoftwareOne Approve A00005 / Reject A00006). Pure research; no code
changes here. Consolidated from 5 parallel `Explore` subagents reading
both NEW (`W:\patient-portal\replicate-old-app`) and OLD
(`P:\PatientPortalOld`) trees plus ABP/ng-bootstrap/ACS docs.

Goal: every bug has OLD behavior (parity contract), NEW behavior
(current state), root cause, and a sized fix path -- so the
issue-by-issue fix session can start with full context.

## Active directive (locked 2026-05-06)

Phase 1 email scope: only THREE email events are active.

1. Email verification (AuthServer registration).
2. Appointment requested (single email per stakeholder per booking).
3. Appointment approved / rejected.

All other handlers (nudges, "still pending", OfficeAdmin fan-out,
attorney CC duplicates, packet-ready notifications) stay gated off
until Adrian re-enables. See `B15` and memory
`project_email-scope-phase1.md`.

---

## Severity / effort summary

| ID  | Title                                                              | Effort | Layer          |
|-----|--------------------------------------------------------------------|--------|----------------|
| B1  | abp-lookup-select FormControl binding                              | M      | Angular        |
| B2  | Slot generation skips preview-then-submit                          | NONE   | (false alarm)  |
| B3  | ABP `GetProperty<bool>` on ExtraProperties                         | S      | .NET / ABP     |
| B4  | ACS SMTP rate-limit                                                | M      | infra / ops    |
| B5  | AuthServer `/Account/Logout` doesn't clear SPA tokens              | S      | AuthServer     |
| B6  | 403 on POST /api/app/appointments (Patient + AA roles)             | S      | Permissions    |
| B7  | 403 on /documents and /packet for external roles                   | S      | Permissions    |
| B8  | DOB year dropdown only +/-10 years                                 | S      | Angular        |
| B9  | Per-tenant `OfficeEmail` empty                                     | S      | Seed           |
| B10 | Welcome banner display name inconsistency                          | XS     | Seed           |
| B11 | Booking form is role-agnostic; CE has no Claim Examiner section    | M      | Angular        |
| B12 | AA/DA email persisted even when "Include" unchecked                | S      | Angular + API  |
| B13 | CE booker tagged as `Patient` recipient role                       | S      | Domain         |
| B14 | Action dropdown on Review page barely visible                      | S      | Angular        |
| B15 | Duplicate booking emails ("requested" + "still pending")           | S      | Application    |
| B16 | Broken links inside outbound email bodies                          | S-M    | Templates      |

---

## B1: abp-lookup-select FormControl binding

**Symptom.** The lookup-select used in the booking form for fields like
appointmentTypeId, locationId, stateId behaves wrong with Reactive
Forms (init value not loading and/or changes not propagating).

**Origin.** The component is `LookupSelectComponent` from
`@volo/abp.commercial.ng.ui` (ABP Commercial library), not custom code
in this repo. Imported in
`angular/src/app/appointments/appointment-add.component.ts:20` and used
in `appointment-add.component.html:55-77` etc. Form is fully Reactive:
control is `formControlName="appointmentTypeId"` plus `[getFn]` and
`(valueChange)` event.

**OLD parity.** OLD does not use this component at all -- it uses
native `<select>` elements. So this is a NEW-only regression.

**Root cause (hypothesis).** The library's `ControlValueAccessor`
implementation likely doesn't fire `onChange` on `writeValue` patches,
or doesn't repopulate from the server lookup before the form is
patched. Needs to be confirmed by stepping through the library's
`writeValue` / `registerOnChange` / `registerOnTouched`. Until that's
inspected, treat the cause as unknown.

**Fix paths (in order of preference).**
1. Wrap the ABP lookup in a thin `ControlValueAccessor` shim that
   listens to the lookup's `(valueChange)` and forwards to the form
   control. Lowest blast radius.
2. Replace the lookup with a native `<select>` populated from the
   lookup service via `*ngFor`. Highest parity with OLD, fewest
   moving parts. This is also the path that aligns with the OLD
   "form looks visually like OLD" goal.
3. File a ticket against ABP Commercial.

**Effort.** M (1-2 days incl. confirming the actual `writeValue` flow).

---

## B2: Slot generation preview-then-submit -- FALSE ALARM

**Symptom (reported).** Slot generator in NEW skips a preview step
that OLD has.

**Finding.** The 2-step preview-then-submit flow is **already
implemented** in NEW:

- `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability-generate.component.ts:171-223`
  -- `generate()` calls `service.generatePreview()` then renders
  `this.preview: DoctorAvailabilitySlotsPreviewDto[]` to a table.
- `:226-246` -- `submit()` iterates the preview and posts each slot
  via `service.create()`.

OLD (`P:\PatientPortalOld\patientappointment-portal\src\app\components\doctor-management\doctors-availabilities\add\doctors-availability-add.component.ts:95-178`)
follows the same shape (`manageDoctorAvailabilities()` previews,
`submitData()` confirms).

**Possible reason for the report.** During today's test, the admin may
have hit Submit on the preview without realizing the preview render
had already happened (e.g. preview rendered above the fold and looked
like the form). Worth re-running the flow once after the other fixes
land to confirm.

**Action.** None for now. If the report repeats after the next round of
fixes, dig into whether the preview UI is visually obvious enough.
File this under a UX polish task if it does.

---

## B3: ABP `GetProperty<bool>` on ExtraProperties

**Symptom.** `entity.GetProperty<bool>(name)` throws when the stored
value is a `JsonElement` representing a bool. ABP's
`ChangeTypePrimitiveExtended<T>` only handles primitives.

**Existing patch.**
`src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs:420-460`
-- `ReadBoolExtensionProperty()` calls non-generic `GetProperty(name)`
to get raw `object?`, then `CoerceBool()` handles `bool`, `string`,
`JsonElement`, and stringified-numeric fallbacks. Used at lines
393-396 for `IsExternalUser` and `IsAccessor`.

**Why the generic version fails.** When ABP serializes
`ExtraProperties` back to JSON in `AbpExtraPropertiesEntityChangeInfo`
or via `System.Text.Json`, primitives are reconstructed as
`JsonElement`. `ChangeTypePrimitiveExtended<bool>` does an `is bool`
check first; `JsonElement` fails it and falls through to
`Convert.ChangeType` which throws on the boxed JsonElement.

**Sources.**
- ABP Object Extensions docs: `https://abp.io/docs/en/abp/latest/Object-Extensions`
- Related ABP issue: `https://github.com/abpframework/abp/issues/19430`
- ABP support thread: `https://abp.io/support/questions/8925/extra-properties-get-json-value`

**Fix paths.**
1. **Document the existing pattern.** The patch in
   `ExternalSignupAppService.cs` is the canonical workaround. Promote
   `ReadBoolExtensionProperty` + `CoerceBool` into a shared helper
   (e.g. `ExtensionPropertyExtensions.GetBoolOrDefault` in
   `Domain.Shared`) so future bool-typed extension props don't each
   re-implement coercion. **Recommended.**
2. Custom `JsonConverter<bool>` registered with the global serializer
   so deserialization always materializes a real `bool`. Higher
   blast radius, may interact with ABP's own converters.
3. File a GitHub issue requesting a `JsonElement`-aware overload of
   `GetProperty<T>` in ABP. Track-only.

**Effort.** S (lift existing helper to shared location, point B3 at it).

---

## B4: ACS SMTP rate-limit "4.5.127 Excessive message rate"

**Symptom.** Lifecycle test fired ~7 emails in <2s; ACS rejected all
with `4.5.127 Message rejected. Excessive message rate from sender`.

**Current config.**
`src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.json:25-34`
-- `Abp.Mailing.Smtp.*` against `smtp.azurecomm.net:587` SSL. We are on
the **default Azure-managed domain** (`azurecomm.net` subdomain).

### Authoritative rate-limit table (ACS service-limits, 2026-03-05)

Source: `https://learn.microsoft.com/en-us/azure/communication-services/concepts/service-limits`

**Azure-managed domain** (what we use today):

| Operation       | Per minute | Per hour | Higher limits available? |
| --------------- | ---------- | -------- | ------------------------ |
| Send Email      | **5**      | **10**   | **NO -- HARD CAP**       |
| Get Email Status| 10         | 20       | NO                       |

**Custom verified domain** (after we set up DKIM/SPF on a Gesco-owned
domain):

| Operation       | Per minute | Per hour | Higher limits available?     |
| --------------- | ---------- | -------- | ---------------------------- |
| Send Email      | **30**     | **100**  | YES (raise via Azure Support) |
| Get Email Status| 60         | 200      | YES                          |

**Other size caps (apply to both):**
- Recipients per email: 50
- Total email size incl. attachments: 10 MB
- Authenticated connections per subscription: 250
- Quota increase request lead time: up to 72 hours for evaluation

The 4.5.127 error fires when we exceed the per-minute window (5/min
on managed). The retry window is ~60s rolling.

### When can we resume testing?

- **On the current managed domain:** at most 10 emails per hour. After
  B15 fix + Phase 1 scope, a single end-to-end booking flow fires:
  - 1 register-verification email
  - 1-2 booking-requested emails (depending on stakeholder count)
  - 1 approve OR reject email
  - = roughly 3-4 emails per full lifecycle
  - So ~2-3 lifecycles per hour before we burn the quota.
- **After moving to a custom verified domain:** 100/hour. ~25
  lifecycles per hour, raisable.
- **After moving to ACS Email REST API + verified domain:** 1-2M/hour
  ceiling per Microsoft, gated by a 1% failure-rate floor and a
  2-4 week ramp-up.

### Mitigation options (ranked)

1. **Papercut / smtp4dev for local dev.** Zero rate limit, no
   external dependency. Add `appsettings.Development.json` override
   pointing to `localhost:1025`. Production keeps ACS. **Highest
   priority -- unblocks dev testing immediately.**
2. **Set up a custom verified domain on ACS.** Adds DKIM + SPF
   records on a Gesco subdomain (e.g. `mail.gesco.com`), gets us
   30/min + 100/hour out of the box, raisable. Lead time = DNS
   propagation + ACS verification (~24-48 hours).
3. **Switch from SMTP relay to ACS Email REST API.** Use
   `Azure.Communication.Email` NuGet to replace ABP's `IEmailSender`.
   Same per-domain rate caps but better async queueing and no SMTP
   handshake overhead. Requires code change.
4. **Request quota increase on the existing custom domain.** Only
   useful AFTER step 2. Up to 72-hour lead time.

**Note.** Once B15 + the email-scope directive shrink per-event email
count from ~7 down to 1-3, the 5/min cap becomes survivable for
single-flow QA but not for parallel testing. Step 1 (Papercut) is
still needed if we want to run two test sessions in parallel.

**Effort.** S (Papercut). S (verified domain DNS + ACS verify). M
(REST API refactor).

---

## B5: AuthServer `/Account/Logout` doesn't clear SPA tokens

**Symptom.** When user lands on AuthServer Razor `/Account/Logout`
directly (typed URL, old link), AuthServer cookie is cleared but the
SPA's localStorage tokens (`access_token`, `refresh_token`,
`id_token`) persist. SPA's user-menu Logout works correctly because
it goes through `AuthService.logout()` from `@abp/ng.core`.

**Current flow.**
1. User hits `/Account/Logout`.
2. ABP's stock Logout handler clears the cookie.
3. Redirects to `/` -> `IndexModel` (AuthServer/Pages/Index.cshtml.cs:9-30)
   -> redirects anonymous to `/Account/Login`.
4. SPA tokens are never touched -- SPA still thinks it's logged in.

**Correct OAuth/OIDC end-session flow (RP-Initiated Logout).**
1. SPA hits `/connect/endsession?id_token_hint=<token>&post_logout_redirect_uri=<spa-url>`.
2. AuthServer validates, clears cookies, redirects back to the SPA
   `post_logout_redirect_uri`.
3. SPA's OAuth listener clears its own state on landing.

**Config gap.** `src/.../AuthServer/appsettings.json` does not list
`PostLogoutRedirectUris` for the SPA OpenIddict client (or the seed
data does not seed them).

**Fix path.**
1. Add a custom `Pages/Account/Logout.cshtml(.cs)` in AuthServer that
   redirects to `/connect/endsession` with `id_token_hint` (read from
   the cookie session) and a default `post_logout_redirect_uri`.
2. Seed / configure SPA OpenIddict app with
   `PostLogoutRedirectUris` including the per-subdomain SPA URL
   (e.g. `http://falkinstein.localhost:4200/`). Note: must respect the
   wildcard-domain pattern documented in
   `project_tenant-routing-architecture.md`.
3. SPA side requires no change -- `OAuthService.logOut()` already
   calls the right endpoint.

**Effort.** S.

**Parity note.** OLD app is a single Angular + .NET process; OAuth
end-session is not a meaningful OLD parity question. We're following
the OAuth standard.

---

## B6: 403 on POST /api/app/appointments after Book

**Reported.** Patient and AA users see 403 when booking. DA and CE
succeed (today's test).

**NEW code.**
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs:35` -- class-level `[Authorize]`
- `:552-557` -- `CreateAsync` with method-level
  `[Authorize(CaseEvaluationPermissions.Appointments.Create)]`
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs:94-107`
  -- permission name `CaseEvaluation.Appointments.Create`.
- `src/HealthcareSupport.CaseEvaluation.Domain/Identity/ExternalUserRoleDataSeedContributor.cs:88-98`
  -- `BookingBaselineGrants()` yields `Appointments.Create` for ALL 4
  external roles.

**Permission matrix (per seed).**

| Role               | Create | Expected |
|--------------------|--------|----------|
| Patient            | granted | should be 200 |
| Applicant Attorney | granted | should be 200 |
| Defense Attorney   | granted | (was 200 today) |
| Claim Examiner     | granted | (was 200 today) |

**Root cause (most likely).** Seed contributor is tenant-scoped (`if
(context?.TenantId != null)`). For Patient and AA, either: (a) the
seed didn't run for the tenant they registered into; (b) the role
assignment on the IdentityUser didn't take (the user has the role
membership row but no permission grant); (c) cached permission
provider hadn't refreshed when the user logged in.

**Verification before fix.**
- Query `AbpPermissionGrants` for the tenant, role keys `Patient`
  and `ApplicantAttorney`, permission name
  `CaseEvaluation.Appointments.Create`. If row missing, the seed
  didn't write it.
- Query `AbpUserRoles` for the failing user -- confirm the role row
  exists and points to the tenant role, not the host role.
- Reproduce 403 with Playwright + capture API log line for the
  permission check.

**Fix path.**
1. If seed didn't run: re-run db-migrator on the tenant, or
   manually `INSERT` the missing grant rows.
2. If seed ran but grant is missing: investigate whether `BookingBaselineGrants` is being filtered at runtime
   (look for `if (RoleName == ...)` in the contributor) and
   correct.
3. Add a backfill migration that asserts each external role has the
   booking baseline -- protects against the same drift in new
   tenants.

**Effort.** S.

---

## B7: 403 on /documents and /packet for external roles

**NEW code.**
- `src/HealthcareSupport.CaseEvaluation.Application/AppointmentDocuments/AppointmentDocumentsAppService.cs:63`
  -- `[Authorize(CaseEvaluationPermissions.AppointmentDocuments.Default)]`
- `:78` -- `[Authorize(CaseEvaluationPermissions.AppointmentDocuments.Create)]`
- `src/HealthcareSupport.CaseEvaluation.Application/AppointmentDocuments/AppointmentPacketsAppService.cs:30, 44`
  -- both methods gated on `AppointmentPackets.Default`.
- `ExternalUserRoleDataSeedContributor.cs:88-98` -- baseline does
  NOT include `AppointmentDocuments.*` or `AppointmentPackets.*`.

**Permission matrix.**

| Role               | Documents view | Packet view | Documents upload |
|--------------------|----------------|-------------|------------------|
| Patient            | 403 (missing)  | 403         | 403              |
| Applicant Attorney | 403            | 403         | 403              |
| Defense Attorney   | 403            | 403         | 403              |
| Claim Examiner     | 403            | 403         | 403              |

**OLD parity.** OLD's
`PatientAppointment.Api/Controllers/Api/AppointmentRequest/AppointmentDocumentsController.cs`
allows authenticated users to fetch documents for appointments they
are involved in -- ownership/accessor check at the data layer, not
permission check at the controller.

**Fix path.**
1. Add to `BookingBaselineGrants()` in
   `ExternalUserRoleDataSeedContributor.cs`:
   - `CaseEvaluation.AppointmentDocuments` (read)
   - `CaseEvaluation.AppointmentDocuments.Create` (upload)
   - `CaseEvaluation.AppointmentPackets` (read packet)
2. Verify per-record ownership is enforced INSIDE the AppService:
   the caller's IdentityUserId must match either the appointment
   booker, the patient, an `AppointmentAccessor` row, or the linked
   AA/DA/CE row. If not, add `IRepository<AppointmentAccessor>`-
   based filter before the Get.
3. Migration / re-seed for existing tenants.

**Effort.** S (grants + ownership check + migration).

---

## B8: DOB year dropdown only +/-10 years

**NEW code.**
- `angular/src/app/appointments/appointment-add.component.html:234-244`
  -- `<input ngbDatepicker>` with NO `[minDate]`, `[maxDate]`, or
  `navigation` attributes.
- `angular/src/app/appointments/appointment/components/appointment-view.component.html:216-226`
  -- same pattern.
- ng-bootstrap default: shows ~+/-10 years from today; navigation
  defaults to `select` only when `minDate`/`maxDate` are wider than
  one decade (otherwise both arrows and select are constrained).
- Source: `https://ng-bootstrap.github.io/#/components/datepicker/api`

**OLD code.**
- `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\add\appointment-add.component.html:129`
  uses a custom `<rx-date>` component; year range is unconstrained
  (input parses any year).

**Fix path.** In each component using `ngbDatepicker` for DOB:
- Add `dobMinDate = { year: 1900, month: 1, day: 1 }` and
  `dobMaxDate = today` properties (TS).
- Bind `[minDate]="dobMinDate" [maxDate]="dobMaxDate" navigation="select"`
  in the template.

Files to touch:
- `angular/src/app/appointments/appointment-add.component.ts`/`.html`
- `angular/src/app/appointments/appointment/components/appointment-view.component.ts`/`.html`
- `angular/src/app/account/register/register.component.ts`/`.html`
  if DOB is added to registration in the future. (Today register has
  no DOB; AuthServer Razor handles registration so this may be moot.)

**Effort.** S.

---

## B9: Per-tenant `OfficeEmail` empty

**NEW code.**
- Resolver: `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Notifications/AppointmentRecipientResolver.cs:127-129`
  -- reads
  `await _settingProvider.GetOrNullAsync(CaseEvaluationSettings.NotificationsPolicy.OfficeEmail)`.
  Empty -> `AddIfPresent` skips (lines 106-111). Logged today.
- Setting definition: `src/HealthcareSupport.CaseEvaluation.Domain/Settings/CaseEvaluationSettingDefinitionProvider.cs:38`
  default `""`.
- Tenant seed: `src/HealthcareSupport.CaseEvaluation.Domain/Saas/FalkinsteinTenantDataSeedContributor.cs`
  inserts the tenant but does not set any settings.

**OLD parity.** OLD has no equivalent "office mailbox" concept that
search surfaced; treat as NEW feature with no OLD constraint.

**Fix path.**
1. Add to `FalkinsteinTenantDataSeedContributor.SeedAsync()` after the
   tenant insert:
   ```csharp
   using (_currentTenant.Change(tenant.Id))
   {
       await _settingProvider.SetAsync(
           CaseEvaluationSettings.NotificationsPolicy.OfficeEmail,
           "<demo-office-mailbox>");
   }
   ```
2. Add the same setting to a tenant-onboarding admin UI later (out
   of scope for now).

**Effort.** S.

**Phase 1 caveat.** Per the active email scope directive, the
OfficeAdmin recipient role is itself out-of-scope for now, so this
fix can wait. Worth doing the seed line so the dev environment has a
non-empty value when the directive is lifted.

---

## B10: Welcome banner display name inconsistency

**NEW code.**
- Banner getter: `angular/src/app/home/home.component.ts:185-193`
  builds `[user.name, user.surname].filter(Boolean).join(' ')` then
  falls back to `userName` (which is the email for our seeded
  internal users).
- External users: `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs:485-494`
  sets `user.Name = input.FirstName; user.Surname = input.LastName`
  during registration. So they show "First Last".
- Internal users: `src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUsersDataSeedContributor.cs:184-211`
  creates `new IdentityUser(Guid.NewGuid(), userName, email, tenantId)`
  -- Name + Surname remain `null`. So the banner falls back to
  `userName` which equals the email. This is the visible
  inconsistency.

**OLD parity.**
`P:\PatientPortalOld\patientappointment-portal\src\app\components\shared\top-bar\top-bar.component.ts:40-54`
-- always shows `firstName + ' ' + lastName`. Never email.

**Fix path.**
1. In `InternalUsersDataSeedContributor.EnsureUserWithRoleAsync()`,
   set `user.Name` and `user.Surname` based on the email prefix or a
   role-derived label (e.g. `admin@...` -> Name="Admin",
   Surname="User"; `supervisor@...` -> Name="Staff", Surname="Supervisor").
2. As a safety net, update `displayUserName` in `home.component.ts`
   to suppress the email fallback (return a generic role label
   instead).

**Effort.** XS-S.

---

## B11: Booking form is role-agnostic; CE has no Claim Examiner section

**NEW code.**
- `angular/src/app/appointments/appointment-add.component.ts:819-822`
  defines role flags `isClaimExaminerRole`, `isApplicantAttorney`,
  `isDefenseAttorney`, `isItAdmin` -- but they're only used for
  field-level `[readonly]` toggling (lines 475, 587 etc.), NOT for
  section-level `*ngIf`/`@if`.
- `:442-666` html renders the AA card and DA card unconditionally.
- No top-level "Claim Examiner / Adjuster" section. The only CE
  fields live inside the per-injury modal at `:995-1129`, gated by
  `claimExaminer.isActive` -- a per-injury attribute, not a booker
  attribute.

**OLD code.**
- `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\add\appointment-add.component.ts:66, 145-159`
  reads `userRoleId`, sets `isAdjusterLogin / isPatientLogin /
  isPatientAttorneyLogin`.
- `appointment-add.component.html:600, 651` uses
  `*ngIf="showFormBaseOnRole"` to gate AA / DA / Authorized-User
  sections.
- Inside the injury modal, CE fields are pre-filled with the booker's
  identity and disabled when `isAdjusterLogin` is true (HTML
  line 378 `[disabled]="isAdjusterLogin"`).

**Fix path.**
1. Add `@if`-driven section rendering in
   `appointment-add.component.html`:
   - "Applicant Attorney Details" card -> visible iff booker is
     Patient OR ApplicantAttorney.
   - "Defense Attorney Details" card -> visible iff booker is
     Patient OR DefenseAttorney.
   - NEW "Claim Examiner / Adjuster Details" card (sibling of the
     attorney cards) -> visible iff `isClaimExaminerRole`.
2. CE card pre-fills CE name + email from `currentUser`, marks
   `[readonly]="isClaimExaminerRole && !isItAdmin"` per OLD
   pattern.
3. Patient demographics card: when booker is Patient, pre-fill
   First/Last/Email from `currentUser` and mark readonly. When
   booker is AA/DA/CE, those fields stay editable for them to enter
   the patient's info.

**Effort.** M (form template restructure + role flag plumbing +
visual QA).

**Parity-flag candidate.** The OLD field "Authorized User" section
exists only for Patient role per the OLD HTML. Confirm whether NEW
`additional-authorized-users` table replaces it or is in addition.

---

## B12: AA/DA email persisted even when "Include" unchecked

**NEW code.**
- Conditional validators ARE wired:
  `angular/src/app/appointments/appointment-add.component.ts:456-477`
  subscribes to checkbox changes;
  `:656-664` `applyConditionalEmailValidator()` strips
  `Validators.required` when the include flag is false.
- HTML hides the card body with `@if (form.get('applicantAttorneyEnabled')?.value)`
  at `:457-551`. Symmetric for DA.
- Payload construction: `:900-908`
  `applicantAttorneyEmail: rawAfter.applicantAttorneyEnabled ? raw.applicantAttorneyEmail : undefined`.
  When the include flag flips off, the field value is NOT cleared --
  whatever the user typed remains in the FormGroup. `undefined` is
  serialized away by JSON.stringify, but that depends on the HTTP
  client behavior.
- Backend: `AppointmentsAppService.CreateAsync` -- need to confirm it
  doesn't accept a non-empty email when the corresponding "include"
  flag is also false. Today's reproduction shows
  `unused-aa@example.com` and `unused-da@example.com` in
  StatusChange/Rejected fan-out, which means the placeholders ARE
  being persisted somewhere.

**Hypothesis.** Either (a) the SPA sends the email anyway because the
form field value is still set despite the checkbox being unchecked, or
(b) the backend persists whatever non-null email it gets, or (c) a
seed/test fixture contains the placeholders.

**Fix path.**
1. SPA: when checkbox flips off, call
   `this.form.get('applicantAttorneyEmail')?.reset()` and same for
   DA. This clears the value so payload is genuinely null.
2. SPA: in payload construction lines 900-908, change `undefined` to
   `null` so the field is explicitly cleared on the wire.
3. Backend `AppointmentCreateDto` validator: if include flag is
   false, force the email field to null. If include flag is true,
   require email to be a real email (not `unused-*@example.com`).
4. Recipient resolver
   (`src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Notifications/AppointmentRecipientResolver.cs:202-209`):
   skip recipients whose email matches `^unused-.*@example\.com$`
   as a defense-in-depth filter.

**Effort.** S.

---

## B13: CE booker tagged as `Patient` recipient role

**NEW code.**
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Notifications/AppointmentRecipientResolver.cs:131-133`
  ```csharp
  var bookerUser = await _identityUserRepository.FindAsync(appointment.IdentityUserId);
  AddIfPresent(bookerUser?.Email, RecipientRole.Patient, "booker");
  ```
  The booker's role is hardcoded to `RecipientRole.Patient` regardless of
  who actually booked. The booker's IdentityUserRoles are never read.
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Appointments/Notifications/RecipientRole.cs:1-20`
  defines the enum: `Patient, ApplicantAttorney, DefenseAttorney,
  ClaimExaminer, OfficeAdmin, ResponsibleUser`.

**OLD parity.** Search for the OLD app's notification recipient
resolution did not turn up an exact equivalent (probably lived in
`PatientAppointment.Api/Notifications`). The OLD app email templates
referenced the booker by their actual role
(`{{ApplicantAttorneyName}}` etc.), implying the resolver knew who the
booker was.

**Fix path.**
1. Replace the hardcoded `Patient` with a role-detection step. Two
   options:
   - **Option A (preferred).** Look up the booker's IdentityUserRoles
     and map the first matching role to `RecipientRole`. Cache result
     per appointment to avoid extra queries.
   - **Option B.** Add a `BookerRole` column to the Appointment entity
     and populate from `CurrentUser.Roles` at create time. Cheaper at
     read time, costs a migration.
2. If the booker matches one of the appointment-linked parties (e.g.
   the AA on the appointment IS the booker), suppress the duplicate
   recipient row.

**Effort.** S (Option A) or S+migration (Option B).

---

## B14: Action dropdown on Review page barely visible

**NEW code.**
- `angular/src/app/appointments/appointment/components/appointment-view.component.html:25-52`
  -- bare `<select class="form-select form-select-sm">` with no
  `<label>`. Sits in a `d-flex gap-2` toolbar alongside Submit, Save,
  Upload Documents, View change log, Help, Back. Submit at lines
  45-52 (`btn btn-success btn-sm`) is the trigger.

**OLD code.**
- `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\view\appointment-view.component.html:1-154`
  handles approve/reject inside a modal dialog -- there's no toolbar
  dropdown at all in OLD. The toolbar approach is a NEW design
  choice.

**Root cause.** Bootstrap's `.form-select` borders are subtle
(1px light gray); when surrounded by primary/secondary buttons, the
select blends in visually. No `<label>` provides semantic context.

**Fix path (preferred).** Replace select + Submit with two inline
buttons:
```html
@if (canTakeOfficeAction) {
  <button class="btn btn-success btn-sm" (click)="dispatchAction('approve')">Approve</button>
  <button class="btn btn-danger btn-sm" (click)="dispatchAction('reject')">Reject</button>
}
```
The same modal flows already collect Responsible User /
RejectionReason, so the buttons map 1:1 to the existing modal trigger.

**Alternative (lighter touch).** Keep the select, add
`<label class="form-label small fw-bold mb-0 me-1">Action</label>`
prefix, bump border to `border-2`, set background to
`bg-light` so it stands out from the toolbar.

**Effort.** S either way.

---

## B16: Broken links inside outbound email bodies

**Reported (2026-05-06).** Adrian observed that the links embedded in
the outgoing emails (email verification, appointment-requested,
approved / rejected) had bugs. The exact symptom (404? wrong host?
wrong tenant subdomain? double-encoded token? port mismatch?) was not
captured -- the rate-limit blast meant most emails never landed in a
usable inbox to inspect.

**Why this didn't surface in code review.** The link-construction
logic is split across:
- `src/HealthcareSupport.CaseEvaluation.AuthServer/Emailing/CaseEvaluationAccountEmailer.cs`
  (verification link) -- builds
  `{base}/account/email-confirmation?userId={guid}&confirmationToken={token}`
  where `{base}` is read from `App:SelfUrl` or similar.
- `src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailBodies/*.html`
  (appointment-requested, approved, rejected) -- contain a
  `{{AppointmentUrl}}` placeholder substituted by
  `TemplateVariableSubstitutor`.
- The substitutor's URL resolver -- needs to compute the per-tenant
  subdomain (`falkinstein.localhost:4200/appointments/view/{id}`)
  using `ICurrentTenant` + a config base.

Likely failure modes:
1. **Hardcoded `localhost`** instead of the tenant subdomain
   (`{tenantSlug}.localhost`). Means clicking the link lands on the
   wrong host; if the SPA is configured to require a tenant subdomain,
   the route won't resolve.
2. **Wrong port.** Container internal port (8080) vs external port
   (4200/44368/44327) confusion.
3. **Wrong scheme.** `http://` vs `https://` mismatch -- AuthServer
   is on http in dev, browsers may treat the click as a security warning.
4. **Double URL-encoding** of the confirmation token. ABP signs
   tokens as base64 with `+`/`/`/`=`; if the token is
   `Uri.EscapeDataString`-encoded once and then also passed through
   Razor's `@Url.PageLink` (which encodes again), the consumer sees
   a corrupted token and 401s with "invalid token".
5. **Missing trailing slash or path segment.** `/account/email-confirmation`
   vs `/Account/ConfirmUser` -- the actual SPA route for verification
   needs to be confirmed.

**Investigation steps (after rate-limit unblock).**
1. Switch SMTP to Papercut (or set up custom verified domain) so we
   can actually receive emails.
2. Trigger one of each event:
   - Register a new user -> capture verification email.
   - Book an appointment -> capture requested email.
   - Approve / reject -> capture status email.
3. Inspect the raw HTML of each email; extract every `href` and
   verify against:
   - Expected SPA route in `angular/src/app/app.routes.ts`.
   - Per-tenant subdomain pattern in
     `project_tenant-routing-architecture.md`.
   - Token encoding survives a round-trip through
     `decodeURIComponent`.
4. Click each link in a real browser session, confirm landing route
   actually exists and the token is consumed correctly.

**Fix path.** Once the symptoms are concretely captured, this likely
reduces to:
- Centralizing URL building in a single helper that takes
  `(tenantSlug, route, queryParams)` and returns a fully-formed,
  correctly-encoded URL.
- Replacing every ad-hoc string concatenation in
  `CaseEvaluationAccountEmailer.cs` and
  `TemplateVariableSubstitutor.cs` with the centralized helper.
- Unit-testing the helper with all 3 event types and a non-default
  tenant subdomain.

**Effort.** S-M depending on whether the cause is one bad
concatenation (S) or fundamentally missing tenant-aware URL building
(M).

**Blocked by.** B4 (need working email delivery to inspect rendered
links).

---

## B15: Duplicate booking emails ("requested" + "still pending")

**Symptom (today).** When a stakeholder books a new appointment, every
recipient gets two emails almost simultaneously: "appointment has been
requested" then "the requested appointment is still pending". The
second is a status-still-pending nudge that should not fire on the
same submit.

**Likely paths.**
- `BookingSubmissionEmailHandler` -- the legitimate "requested" email.
- Some other handler subscribed to either the same domain event OR a
  status-changed-to-Pending event triggered by the create. Suspect
  candidates: `StatusChangeEmailHandler` firing for the
  `Created -> Pending` transition as if it were a status change, OR a
  `PendingNudge` / `AppointmentReminder` handler running at submit
  time.

**Investigation steps before the fix.**
1. Grep `src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/`
   for handlers subscribing to `AppointmentCreatedEto`,
   `AppointmentStatusChangedEto`, or similar. Enumerate all handlers
   that emit emails on submit.
2. Check the Hangfire dashboard / SQL for the two job rows queued
   when a booking is submitted -- their job names tell you which
   handler fired each.
3. Confirm against the "Phase 1 email scope" memory entry: the only
   submit-time email allowed is the single "requested" email per
   stakeholder.

**Fix path.**
1. Identify the redundant handler. Either delete it, gate it behind
   a feature flag, or change its event subscription so it doesn't
   fire on the create transition.
2. Document the policy in
   `docs/parity/email-handlers-demo-critical.md`: which handlers are
   active in Phase 1 and which are gated off.

**Effort.** S (once the duplicate handler is identified).

---

## What's blocking the next session

Before starting fixes:

- **B6.** Need to confirm the actual permission-grant rows in the DB
  for Patient and AA roles in Falkinstein tenant. Five-line SQL query
  -- can run in the next session.
- **B12.** Confirm whether the `unused-*@example.com` placeholder
  actually originates from form values or from the backend test
  fixtures. One Playwright run will tell us.
- **B15.** List the registered handlers under
  `Notifications/Handlers/` to find the second sender.

Everything else has the diagnosis to start the fix on.

---

## Sources

- ABP Object Extensions: `https://abp.io/docs/en/abp/latest/Object-Extensions`
- ng-bootstrap datepicker API: `https://ng-bootstrap.github.io/#/components/datepicker/api`
- ACS SMTP rate limits: `https://learn.microsoft.com/en-us/azure/communication-services/concepts/email/email-smtp-connectivity`
- ABP issue 19430 (extra-properties JsonElement): `https://github.com/abpframework/abp/issues/19430`
- ABP support 8925: `https://abp.io/support/questions/8925/extra-properties-get-json-value`
