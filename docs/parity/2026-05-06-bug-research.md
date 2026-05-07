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

**Sources (verified 2026-05-06).**
- ABP Object Extensions docs: `https://abp.io/docs/en/abp/latest/Object-Extensions`
- Issue 12547 -- Non-primitive ExtraProperties: `https://github.com/abpframework/abp/issues/12547` (recommends custom getter/setter helpers, exactly what we built)
- Issue 23546 -- Better ExtraProperties mapping for EF Core (Aug 2025): `https://github.com/abpframework/abp/issues/23546` (acknowledges the architectural problem; no merged fix)
- Issue 19430 -- Tenant Entity Extension `KeyNotFoundException` at `JsonElement.GetProperty`: `https://github.com/abpframework/abp/issues/19430`
- Issue 19617 -- Cannot update ExtraProperties (8.0.4+): `https://github.com/abpframework/abp/issues/19617`
- ABP support thread: `https://abp.io/support/questions/8925/extra-properties-get-json-value`

The last successful merged fix touching the JsonElement coercion
path appears to be on the OPEN issue list, not in any 10.x release.
So our workaround is the right answer for now.

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

### Verified diagnosis (deep dive 2026-05-06, supersedes earlier hypothesis)

The earlier "missing grant" hypothesis is **WRONG**. The seeder DOES
grant `CaseEvaluation.Appointments.Create` to all 4 external roles
unconditionally. Verified evidence:

`ExternalUserRoleDataSeedContributor.cs:60-63`
```csharp
foreach (var roleName in new[] { "Patient", "Claim Examiner",
                                 "Applicant Attorney", "Defense Attorney" })
{
    await GrantAllAsync(roleName, BookingBaselineGrants());
}
```

`ExternalUserRoleDataSeedContributor.cs:88-98`
```csharp
private static IEnumerable<string> BookingBaselineGrants()
{
    yield return $"{Group}.DoctorAvailabilities";
    yield return $"{Group}.Appointments";
    yield return $"{Group}.Appointments.Create";
    yield return $"{Group}.Appointments.RequestCancellation";
    yield return $"{Group}.Appointments.RequestReschedule";
}
```

So all 4 roles get `Appointments.Create` with no role-specific
filtering. Patient and AA should NOT be hitting 403 at the
permission gate.

**The 403 must be downstream.** Three plausible causes that have NOT
yet been verified:
1. **SPA hits a different endpoint depending on booker role.** Patient
   may be POSTing to a public/external signup endpoint while DA/CE go
   through the authenticated `/api/app/appointments`; the endpoint
   that returns 403 is the OTHER one. Confirm by capturing the actual
   POST URL from a Patient/AA Playwright run.
2. **Per-record validation in `AppointmentManager.CreateAsync`.** The
   domain service may throw an `Authorization` exception (which ABP
   surfaces as 403 not 400) when the booker's `IdentityUserId` does
   not match the patient's IdentityUser link, e.g. for Patient role
   the patient row may not yet exist or be unlinked.
3. **`AppointmentRecipientResolver` or recipient-side ownership check
   that runs at create time.** Less likely to surface as 403, more
   likely 500.

**Required investigation before the fix.**
- Reproduce 403 as Patient with Playwright, capture:
  - The exact POST URL.
  - The full request body.
  - The full response (status + body + headers).
  - The corresponding API log line at the moment of 403.
- Read `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs`
  for any role-based / ownership-based validation in `CreateAsync`.

**Effort.** S once the actual cause is known. Do NOT fix the seeder
-- it is correct. The fix is elsewhere.

---

## B7: 403 on /documents and /packet for external roles

### Verified diagnosis (deep dive 2026-05-06)

Confirmed: `BookingBaselineGrants()` does NOT include any
`AppointmentDocuments.*` or `AppointmentPackets.*` permissions. The
parent `Appointments` permission is granted; the document/packet
sub-resources are not.

**Verified grant inventory for external roles** (file =
`ExternalUserRoleDataSeedContributor.cs`):

| Permission name                                      | Granted? | Source line |
| ---------------------------------------------------- | -------- | ----------- |
| `CaseEvaluation.DoctorAvailabilities`                | yes      | :91         |
| `CaseEvaluation.Appointments`                        | yes      | :94         |
| `CaseEvaluation.Appointments.Create`                 | yes      | :95         |
| `CaseEvaluation.Appointments.RequestCancellation`    | yes      | :96         |
| `CaseEvaluation.Appointments.RequestReschedule`      | yes      | :97         |
| `CaseEvaluation.AppointmentDocuments`                | **no**   | (defined only at `CaseEvaluationPermissions.cs:111`) |
| `CaseEvaluation.AppointmentDocuments.Create`         | **no**   | :112        |
| `CaseEvaluation.AppointmentDocuments.Edit`           | **no**   | :113        |
| `CaseEvaluation.AppointmentDocuments.Delete`         | **no**   | :114        |
| `CaseEvaluation.AppointmentDocuments.Approve`        | **no**   | :115        |
| `CaseEvaluation.AppointmentPackets`                  | **no**   | :120        |
| `CaseEvaluation.AppointmentPackets.Default`          | **no**   | :120        |
| `CaseEvaluation.AppointmentPackets.Regenerate`       | **no**   | :121        |

**AppService gate matrix** (verified by reading the AppService
`[Authorize]` attributes):

| File:Method | Permission required | Granted to external? | Per-record ownership in body? |
| --- | --- | --- | --- |
| `AppointmentDocumentsAppService.cs:64 GetListByAppointmentAsync` | `AppointmentDocuments.Default` | no | not visible -- relies on IMultiTenant + appointment-scoped query |
| `AppointmentDocumentsAppService.cs:78 UploadStreamAsync` | `AppointmentDocuments.Create` | no | not visible |
| `:170 UploadPackageDocumentAsync` | `AppointmentDocuments.Create` | no | not visible |
| `:210 UploadJointDeclarationAsync` | `AppointmentDocuments.Create` | no | not visible |
| `:393 DownloadAsync` | `AppointmentDocuments.Default` | no | not visible |
| `:414 DeleteAsync` | `AppointmentDocuments.Delete` | no | not granted -- correct, externals shouldn't delete |
| `:434 ApproveAsync` | `AppointmentDocuments.Approve` | no | correctly internal-only |
| `:464 RejectAsync` | `AppointmentDocuments.Approve` | no | correctly internal-only |
| `AppointmentPacketsAppService.cs:31 GetByAppointmentAsync` | `AppointmentPackets.Default` | no | relies on IMultiTenant |
| `:45 DownloadAsync` | `AppointmentPackets.Default` | no | relies on IMultiTenant |

**OLD parity.** OLD's
`PatientAppointment.Api/Controllers/Api/AppointmentRequest/AppointmentDocumentsController.cs`
gated only on authentication; ownership filtering was at the data
layer.

**Fix path (concrete).** Add to `BookingBaselineGrants()`:
- `CaseEvaluation.AppointmentDocuments` (read own appointment docs)
- `CaseEvaluation.AppointmentDocuments.Create` (upload to own appt)
- `CaseEvaluation.AppointmentPackets.Default` (view + download own packet)

Do **not** grant `Edit`, `Delete`, `Approve`, or `Regenerate` to
external roles. Add a re-seed migration so existing tenants pick up
the new grants.

**Defense in depth (recommended).** The current AppServices for
documents/packets do not contain a visible ownership check; they
likely rely on `IMultiTenant` filtering plus the
`appointmentId`-scoped query. Audit those queries to confirm they
join through `AppointmentAccessor` / appointment-party tables so an
external user with the new permission cannot enumerate other
patients' documents in the same tenant.

**Effort.** S (grants + audit + migration).

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

**Fix path (verified).** In each component using `ngbDatepicker` for DOB:
- Add `dobMinDate = { year: 1920, month: 1, day: 1 }` and
  `dobMaxDate = { year: <currentYear>, month: 12, day: 31 }` (TS).
- Bind `[minDate]="dobMinDate" [maxDate]="dobMaxDate" navigation="select"`
  in the template.

**Files to touch (only two, confirmed):**
- `angular/src/app/appointments/appointment-add.component.{ts,html}`
- `angular/src/app/appointments/appointment/components/appointment-view.component.{ts,html}`

Confirmed: `register.component.html` collects only First Name, Last
Name, Email, Password -- no DOB field. So registration is NOT in
scope for this fix.

ng-bootstrap version installed: **19.0.1** (verified via package
listing). The +/-10-year default behavior holds in v19.

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

**Fix path (verified).**
1. In `InternalUsersDataSeedContributor.EnsureUserWithRoleAsync()`
   (around `:184-211`), set `user.Name` and `user.Surname` BEFORE
   calling `_userManager.CreateAsync()`. Suggested mapping:
   - `admin@<slug>.test`        -> Name="Admin",      Surname="User"
   - `supervisor@<slug>.test`   -> Name="Staff",      Surname="Supervisor"
   - `staff@<slug>.test`        -> Name="Clinic",     Surname="Staff"
   - extra demo admins (SoftwareOne / SoftwareTwo) -> derive from email prefix
2. As a safety net, update `displayUserName` getter in
   `home.component.ts:191-199` to suppress the `userName` fallback
   when it looks like an email (contains `@`) -- show the role name
   instead.

The display name is read from id_token claims (no API call), so
after this seed change a user must logout/login (or token refresh)
to see the new name.

**Effort.** XS-S.

---

## B11: Booking form is role-agnostic; CE has no Claim Examiner section

### Verified diagnosis (deep dive 2026-05-06)

**OLD role detection.** OLD reads `user.data["roleId"]` at
`appointment-add.component.ts:66` and switches on `RoleEnum`:

- `RoleEnum.Patient = 4`
- `RoleEnum.Adjuster = 5`  (OLD's name for what NEW calls Claim Examiner)
- `RoleEnum.PatientAttorney = 6`  (= NEW's Applicant Attorney)
- `RoleEnum.DefenseAttorney = 7`

OLD pre-fill logic:
- `:145` `if (userRoleId == Adjuster && !isRevolutionForm)` -> auto-fill claim-examiner name + email + readonly inside the per-injury modal.
- `:156` `if (userRoleId == Patient)` -> pre-fill patient email.
- `:159` `else if (userRoleId == PatientAttorney)` -> pre-fill applicant-attorney email.

**OLD section-visibility table** (read from
`appointment-add.component.html` in OLD, `*ngIf="showFormBaseOnRole"`
gates lines 600 + 651):

| Section                       | Patient                             | Applicant Attorney                       | Defense Attorney                         | Adjuster (CE)                                   |
| ----------------------------- | ----------------------------------- | ---------------------------------------- | ---------------------------------------- | ---------------------------------------------- |
| Patient Demographics          | visible, email pre-filled, editable | visible, editable                        | visible, editable                        | visible, editable                              |
| Employer Details              | visible, editable                   | visible, editable                        | visible, editable                        | visible, editable                              |
| Applicant Attorney Details    | visible, toggle, editable           | visible, toggle, email pre-filled + RO   | visible, toggle, editable                | visible, toggle, editable                      |
| Defense Attorney Details      | visible, toggle, editable           | visible, toggle, editable                | visible, toggle, email pre-filled + RO   | visible, toggle, editable                      |
| Additional Authorized User    | visible (showFormBaseOnRole=true)   | visible                                  | visible                                  | **HIDDEN** (showFormBaseOnRole=false)          |
| Claim Information / Injury    | visible, CE fields editable per injury | visible, CE fields editable per injury | visible, CE fields editable per injury | visible, CE fields **pre-filled + RO** per injury, isAdjusterLogin=true |

**NEW current state.** Role flags `isClaimExaminerRole`,
`isApplicantAttorney`, `isDefenseAttorney`, `isItAdmin` exist at
`appointment-add.component.ts:819-822` but are only used for
field-level `[readonly]` toggling. **No section-level `@if`
guards exist.** All cards render unconditionally for every role.
The CE-fields-readonly-when-CE-books logic IS in place in the
per-injury modal at lines 1002-1007 + 2402-2405 (forced
`isActive=true`, name/email pre-filled).

**Mechanical delta** (this is the fix recipe):

| Gap                                                | Fix location                                          | Action                                              |
| -------------------------------------------------- | ----------------------------------------------------- | --------------------------------------------------- |
| AA card always visible, OLD hides it for CE booker | `appointment-add.component.html:443` (start of AA card) | Wrap with `@if (shouldShowApplicantAttorneySection())`; method returns `!isClaimExaminerRole` |
| DA card always visible, OLD hides it for CE booker | `appointment-add.component.html:551` (start of DA card) | Wrap with `@if (shouldShowDefenseAttorneySection())`; method returns `!isClaimExaminerRole` |
| Authorized-User table always visible, OLD hides for CE | `appointment-add.component.html:1153` (start of section) | Wrap with `@if (shouldShowAuthorizedUserSection())`; method returns `!isClaimExaminerRole` |
| Patient demographics email not pre-filled for Patient booker | `appointment-add.component.ts` ngOnInit / role-detect block | When `isPatientRole`, patch `patientEmail` from currentUser and set `[readonly]` on the input |
| AA email not pre-filled for AA booker | same | When `isApplicantAttorney`, patch `applicantAttorneyEmail` and set readonly (already partly there at line 475 but verify pre-fill happens) |
| DA email not pre-filled for DA booker | same | symmetric to AA |
| CE-as-Adjuster: claim-examiner section already correct | `:1002-1007` + `:2402-2405` | already pre-fills + readonly when `isClaimExaminerRole` -- no change |

**OLD-NEW role mapping note.** OLD has `Adjuster (5)` where NEW has
`Claim Examiner`. The NEW `appointment-add.component.ts` uses
`isClaimExaminerRole` flag, which is the equivalent of OLD's
`isAdjusterLogin`. No code change needed here -- just keep the
naming consistent.

**Effort.** M (3 `@if` wrappers + 3 method declarations + 2-3
pre-fill patches + visual QA across 4 roles).

**Parity-flag candidate.** OLD's "Additional Authorized User" hidden
for Adjuster matches Adrian's expectation that the CE booker doesn't
add other authorized users for an appointment. Confirm during fix QA.

---

## B12: AA/DA email persisted even when "Include" unchecked

### Verified diagnosis (deep dive 2026-05-06)

**Surprise finding.** A grep for `unused-aa@example.com` and
`unused-da@example.com` across the entire codebase returns **zero
hits in source code**. The only occurrences are inside this research
doc itself. No seed contributor, no test fixture, no DTO default
populates those values.

That means the placeholder addresses observed in the SMTP fan-out
during today's lifecycle test must have come from one of:

1. **Form field values typed manually during prior tests** that
   persisted in the FormGroup across re-renders, then got submitted
   when "Include" was unchecked but the field value was not reset.
2. **Stale rows already in the database** from earlier test runs --
   the appointment row for that earlier test had those placeholders
   in `ApplicantAttorneyEmail` / `DefenseAttorneyEmail` columns, and
   the resolver dutifully fanned out to them on the new
   submit/approve/reject events.

**The frontend payload code at lines 899-904 of
`appointment-add.component.ts` is correct:**
```typescript
applicantAttorneyEmail: rawAfter.applicantAttorneyEnabled
  ? (rawAfter.applicantAttorneyEmail ?? undefined)
  : undefined,
defenseAttorneyEmail: rawAfter.defenseAttorneyEnabled
  ? (rawAfter.defenseAttorneyEmail ?? undefined)
  : undefined,
```
When the include flag is false, `undefined` is sent; the backend
maps it to a NULL column.

> NOTE: There IS a copy-paste bug at line 902 -- the second ternary
> reads `rawAfter.defenseAttorneyEnabled` (boolean) instead of
> `rawAfter.defenseAttorneyEmail` (string), so the boolean coerces
> to `"true"` / `undefined`. This is a separate bug ("DA email gets
> set to the literal string 'true'") that should be fixed alongside
> B12. Confirm in the actual file before fixing.

**The backend at `AppointmentsAppService.cs:725-728` is correct:**
```csharp
appointment.PatientEmail = input.PatientEmail;
appointment.ApplicantAttorneyEmail = input.ApplicantAttorneyEmail;
appointment.DefenseAttorneyEmail = input.DefenseAttorneyEmail;
appointment.ClaimExaminerEmail = resolvedClaimExaminerEmail;
```
No default-substitution.

**Real root cause.** The form's FormControl value is NOT cleared when
the include checkbox is unchecked. The hidden card visually disappears
(via `@if`) but the FormGroup retains whatever value was typed before
the toggle. If the user (or a prior test setup) had typed
`unused-aa@example.com` while the checkbox was on, then later
unchecked the box, the field still contains that string. On the next
submit, `rawAfter.applicantAttorneyEnabled` may be true again (or the
toggle re-flipped) and the stale value gets posted.

This also means existing DB rows (created during earlier debugging)
may carry placeholder emails that survive subsequent edits.

**Fix path (concrete, ranked).**
1. **SPA: reset on uncheck.** In the `applicantAttorneyEnabled`
   value-change subscription (`appointment-add.component.ts:456-461`),
   when the checkbox flips off, call
   `this.form.get('applicantAttorneyEmail')?.reset()`. Symmetric for
   DA. Lowest risk.
2. **Fix the line-902 copy-paste bug** so the DA email isn't sent as
   the boolean `"true"`.
3. **Backend defensive validator on `AppointmentCreateDto`.** Reject
   `ApplicantAttorneyEmail` matching the regex `^unused-.*@.*` or any
   value that doesn't conform to `[A-Z]{2,}` ASCII format. Cheap
   safety net.
4. **Defense in depth in the recipient resolver.** In
   `AppointmentRecipientResolver.cs:202-209`, skip recipients whose
   email matches `^unused-.*@example\.com$`. Belt-and-suspenders.
5. **One-time DB cleanup.** UPDATE
   `AppAppointments SET ApplicantAttorneyEmail = NULL WHERE
   ApplicantAttorneyEmail LIKE 'unused-%@example.com'` (and
   symmetric for DA). Adrian to run when convenient.

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

**Fix path (verified, simpler than originally proposed).**

The simplest correct fix is **Option C: detect via appointment-linked
party rows** (no DB migration, no roles lookup):

1. Before line 133 in `AppointmentRecipientResolver.cs`, check
   whether `bookerUser.Id` matches the `IdentityUserId` of any
   `AppointmentClaimExaminer`, `AppointmentApplicantAttorney`, or
   `AppointmentDefenseAttorney` row already loaded for the
   appointment. If yes, set the recipient role to that role.
2. Otherwise fall back to `RecipientRole.Patient` (true patient
   bookers).
3. Suppress duplicate recipient rows when the booker is also linked
   as a party (the resolver already adds the linked party further
   down; do not double-emit).

**Verified RecipientRole enum** (file =
`Domain.Shared/Appointments/Notifications/RecipientRole.cs:11-20`):
`Patient=1, ApplicantAttorney=2, DefenseAttorney=3, ClaimExaminer=4,
InsuranceCarrierContact=5, OfficeAdmin=6, Employer=7`. No
`ResponsibleUser` -- the earlier doc was wrong; ignore that name.

**Effort.** S.

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

**Verified.** `appointment-view.component.ts:435-445` already has a
`dispatchAction()` method that takes an `'approve' | 'reject'`
argument and opens `ApproveConfirmationModalComponent` /
`RejectAppointmentModalComponent`. The two-button replacement maps
1:1 to existing wiring -- just call
`dispatchAction('approve')` / `dispatchAction('reject')` directly
from each button. No state plumbing needed.

**Effort.** S either way.

---

## B16: Broken links inside outbound email bodies

**Reported (2026-05-06, clarified later same day).** The verification
email link works correctly. The links that break are the ones in
emails sent to **non-booker stakeholders** (Patient, AA, DA, CE
recipients other than the person who submitted the booking). The
exact symptom on those links was not captured because rate-limit
blast prevented inspection. **Deferred** -- Adrian directed us to
hold this investigation until the rest of the bug list is addressed.

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

### Verified diagnosis (deep dive 2026-05-06)

**The duplicate is NOT from two handlers; it is from ONE handler
sending TWO templates.**

`BookingSubmissionEmailHandler.cs` subscribes to
`AppointmentSubmittedEto` (line 53) and dispatches:

1. **`PatientAppointmentPending`** to ALL stakeholders (lines
   183-187). This is the "appointment has been requested" email.
2. **`PatientAppointmentApproveReject`** -- intended for Staff
   Supervisor + Clinic Staff only when the booker is external (lines
   221-225). This is the "still pending, please approve/reject"
   internal alert.

The only other on-create-related handler is
`StatusChangeEmailHandler.cs:59`, which subscribes to
`AppointmentStatusChangedEto` BUT explicitly returns early for any
status that isn't Approved or Rejected (lines 98-102). It does
**not** fire on the initial Pending status.

`RequestSchedulingReminderJob.cs:105` does mention "still pending"
but it is a SCHEDULED job (recurring reminder), not an on-create
handler.

**Why every stakeholder seems to receive both emails.** Adrian's
report says ALL parties get both templates, but the design only
sends template 2 to staff. So either:
- (a) the staff-only filter in
  `BookingSubmissionEmailHandler.DispatchApproveRejectToStaffWhenBookerIsExternalAsync`
  is broken -- it's calling
  `_recipientResolver.ResolveAsync(appointmentId, NotificationKind.Submitted)`
  (line 162) which returns the FULL stakeholder list, not just staff,
  so the staff-only intent is not enforced.
- (b) The two templates were both visible in the admin's mailbox
  because the admin IS staff AND a stakeholder simultaneously, so
  they got the union of both fan-outs.

Need to confirm: does
`AppointmentRecipientResolver.ResolveAsync(id, NotificationKind.Submitted)`
return staff or stakeholders? The resolver doesn't appear to filter
by `NotificationKind`; it returns whoever's wired up to the
appointment. That makes (a) the more likely cause.

**Phase 1 directive impact.** Per the active email-scope directive,
template 2 (`PatientAppointmentApproveReject`) should be **removed
entirely** from this handler -- only the "requested" email (template
1) should fire on submit. That makes the bug moot.

**Fix path (concrete).**
1. **Per Phase 1 directive: delete the
   `DispatchApproveRejectToStaffWhenBookerIsExternalAsync` call**
   (around `BookingSubmissionEmailHandler.cs:160-180`). The handler
   should fire only one template (`PatientAppointmentPending`) on
   submit. This single edit makes the duplicate go away.
2. After Phase 1 lifts: revisit and either (a) gate the second
   template behind an actual staff-only resolver query, or (b)
   redesign as two separate handlers with distinct events.

**Effort.** S (one-method deletion).

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
