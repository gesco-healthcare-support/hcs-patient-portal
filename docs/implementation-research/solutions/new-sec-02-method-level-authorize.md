# NEW-SEC-02: Method-level [Authorize] on CUD across AppServices

## Source gap IDs

- `NEW-SEC-02` in [../../gap-analysis/10-deep-dive-findings.md](../../gap-analysis/10-deep-dive-findings.md)
  lines 62-70 (MVP-blocking security defect).
- Cross-referenced at
  [../../gap-analysis/README.md](../../gap-analysis/README.md) NEW-SEC-02
  row (line 254).
- Acknowledged in-tree at
  [../../../src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md](../../../src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md)
  lines 122, 134-135 ("permission gap").

## NEW-version code read

Probed the 15 `AppService : CaseEvaluationAppService` classes plus
`BookAppService` (derives `ApplicationService`). The gap-analysis claim that
"15+ AppServices need updating" substantially overstates the scope. After
line-level audit:

- **12 of 15 AppServices are already clean**: every `CreateAsync`,
  `UpdateAsync`, `DeleteAsync` (and any `DeleteByIdsAsync` /
  `DeleteAllAsync`) carries a method-level
  `[Authorize(CaseEvaluationPermissions.<Entity>.<Create|Edit|Delete>)]`.
  Verified in: `ApplicantAttorneysAppService`,
  `AppointmentApplicantAttorneysAppService`, `AppointmentLanguagesAppService`,
  `AppointmentStatusesAppService`, `AppointmentTypesAppService`,
  `DoctorAvailabilitiesAppService`, `DoctorsAppService`,
  `LocationsAppService`, `PatientsAppService` (the standard CRUD surface),
  `StatesAppService`, `WcabOfficesAppService`, `BookAppService`.
- **AppointmentsAppService** (`Appointments/AppointmentsAppService.cs`) is
  the primary defect: class-level `[Authorize]` (line 28, bare -- not
  `[Authorize(CaseEvaluationPermissions.Appointments.Default)]`); method-level
  `[Authorize]` on `CreateAsync` (line 161, bare); method-level `[Authorize]`
  on `UpdateAsync` (line 294, bare). `DeleteAsync` (line 155) is correctly
  gated with `[Authorize(...Appointments.Delete)]`.
- **AppointmentEmployerDetailsAppService** is the second defect: class-level
  `[Authorize]` (line 22, bare); `CreateAsync` (line 89) and `UpdateAsync`
  (line 109) have bare `[Authorize]`. `DeleteAsync` (line 83) is correctly
  gated.
- **AppointmentAccessorsAppService** is the third and worst defect:
  class-level `[Authorize]` (line 21, bare); `CreateAsync` (line 92),
  `UpdateAsync` (line 109), AND `DeleteAsync` (line 86) all have bare
  `[Authorize]`. This means a Patient-role user with view access can grant
  themselves accessor roles on any appointment.
- Four custom mutating helper methods use bare `[Authorize]`:
  `PatientsAppService.GetOrCreatePatientForAppointmentBookingAsync`
  (line 92, creates `IdentityUser` + `IdentityRole` + `Patient`),
  `PatientsAppService.UpdatePatientForAppointmentBookingAsync` (line 189,
  calls `PatientManager.UpdateAsync`),
  `PatientsAppService.UpdateMyProfileAsync` (line 328, self-service update --
  arguably intentional scope but still not permission-scoped),
  `AppointmentsAppService.UpsertApplicantAttorneyForAppointmentAsync`
  (line 400, creates/updates `ApplicantAttorney` records).
- Permission constants are already declared for every entity that needs them:
  `CaseEvaluationPermissions.cs`
  ([../../../src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs](../../../src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs))
  defines `Create`, `Edit`, `Delete` children for Appointments,
  AppointmentEmployerDetails, and AppointmentAccessors. No new permission
  constants need to be introduced for the three defective services.
- Controllers in `src/.../HttpApi/Controllers/` delegate every method to the
  AppService with no additional `[Authorize]` attributes. The AppService
  attribute is therefore the ONLY enforcement point for permission checks
  in this architecture (per ADR-002, manual controllers). Confirmed by grep
  of `AppointmentController.cs:80-100`.
- ABP's `AuthorizationInterceptor`
  (<https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.Authorization/Volo/Abp/Authorization/AuthorizationInterceptor.cs>,
  accessed 2026-04-24) intercepts every AppService method call and invokes
  `IMethodInvocationAuthorizationService.CheckAsync` which reads the method's
  `[Authorize]` attribute metadata. Confirms that the attribute is the
  single point of enforcement.

## Live probes

- **Static-only proof** chosen per the research brief. Full probe log:
  [../probes/new-sec-02-method-level-authorize-2026-04-24T23-30-00.md](../probes/new-sec-02-method-level-authorize-2026-04-24T23-30-00.md).
  Probe enumerated all 15 AppServices via grep, confirmed the three defective
  services and four defective helper methods listed above, cross-referenced
  Microsoft and ABP docs, and cited the in-tree Appointments `CLAUDE.md`
  bug acknowledgement.
- Dynamic proof was intentionally skipped. The only seeded credential
  `admin@abp.io` is the host admin (all permissions granted) per
  `probes/service-status.md`. A live `POST /api/app/appointments` with that
  token cannot distinguish "policy check passed" from "no policy check
  present" -- both produce HTTP 201. A real demonstration of the bypass
  requires a second low-privilege test user, which the Live Verification
  Protocol forbids because IdentityUser creation leaves persistent state
  that cannot be reliably reversed inside a subagent run.

## OLD-version reference

N/A. This is a NEW-side defect (per 10-deep-dive-findings.md classification).
OLD used a different authorization model (monolithic role-based with stored
procedures); it is not a useful reference for the NEW ABP permission system.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, C# latest. ABP attribute-based authorization
  is the idiomatic pattern; policy handlers are overkill for simple permission
  checks.
- Riok.Mapperly (ADR-001), manual controllers (ADR-002), dual DbContext
  (ADR-003), doctor-per-tenant (ADR-004), no `ng serve` (ADR-005) are not
  engaged by this fix -- pure source change to the Application layer.
- HIPAA applicability: Appointments, AppointmentEmployerDetails,
  AppointmentAccessors, and Patients all carry PHI or PHI-adjacent data
  (appointment times, employer contact info, who has accessor access).
  Missing permission enforcement on create/update is a direct Security Rule
  `164.312(a)(1)` (Access Control) and `164.308(a)(4)` (Information Access
  Management) violation.
- Permission constants exist. No migration, no seed data change, no DbContext
  touch. Fix is limited to `.cs` files in
  `src/HealthcareSupport.CaseEvaluation.Application/` and optionally one
  test project update.
- Must not break existing `[AllowAnonymous]` paths (e.g.
  `ExternalSignupAppService.RegisterAsync`,
  `ExternalSignupAppService.GetTenantOptionsAsync`). None of the 7 defective
  targets are anonymous, so no tension.
- Must preserve class-level `[Authorize(CaseEvaluationPermissions.<Entity>.Default)]`
  as the baseline read-gate for `GetAsync`/`GetListAsync` -- the fix adds
  method-level overrides, it does not replace the class-level attribute.

## Research sources consulted

1. Microsoft Learn -- Simple authorization in ASP.NET Core (policies.md,
   accessed 2026-04-24):
   <https://learn.microsoft.com/en-us/aspnet/core/security/authorization/simple>
   -- "If neither Roles nor Policy is specified, [Authorize] uses the default
   policy: Authenticated (signed-in) users are authorized."
2. Microsoft Learn -- Policy-based authorization in ASP.NET Core (accessed
   2026-04-24):
   <https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies>
   -- "If multiple policies are applied at the controller and action levels,
   all policies must pass before access is granted." Confirms AND semantics
   of class + method level `[Authorize]`.
3. ABP Framework -- Authorization (accessed 2026-04-24):
   <https://abp.io/docs/latest/framework/fundamentals/authorization>
   -- "ABP extends ASP.NET Core Authorization by adding permissions as auto
   policies and allowing authorization system to be usable in the application
   services too."
4. ABP Framework source --
   `framework/src/Volo.Abp.Authorization/Volo/Abp/Authorization/AuthorizationInterceptor.cs`
   (accessed 2026-04-24 via GitHub): confirms AppService method calls go
   through `IMethodInvocationAuthorizationService.CheckAsync` before
   `invocation.ProceedAsync`.
5. Project internal -- `Appointments/CLAUDE.md` (lines 105-122, 134-135):
   acknowledges the exact permission gap as a "Known gap" with
   `UpdateAsync` and `CreateAsync` calling out bare `[Authorize]`.

## Alternatives considered

- **A. Audit all AppService classes; add method-level `[Authorize(permission)]`
  on the 7 defective entry points (3 services with CUD, 4 helper methods) --
  chosen.** Idiomatic ABP pattern, minimal diff, matches the pattern already
  used in the 12 clean AppServices. No new abstractions.
- **B. Rely on class-level `[Authorize(...Default)]` only, accept the gap as
  "Angular hides the button" -- rejected.** Current de-facto state.
  `Appointments.Default` is granted to every operator role (per
  `CaseEvaluationPermissionDefinitionProvider.cs`), so any logged-in operator
  can POST `/api/app/appointments` directly via cURL and book an appointment
  without `Create` permission. Not defensible as a HIPAA safeguard.
- **C. Policy handler / `IAuthorizationRequirement` per entity -- rejected.**
  Over-engineered. ABP's permission system already creates policies for every
  permission string; writing custom handlers duplicates that machinery and
  spreads authorization logic across more files than necessary. Adds
  maintenance burden with no upside for an MVP security gate.
- **D. Switch to global authorization filter that reflects on
  `Permissions.<Class>.<Method>` convention -- rejected.** Too magical; hurts
  discoverability; deviates from ABP conventions. Future maintainers reading
  one AppService file would not see the gate.

## Recommended solution for this MVP

Add method-level
`[Authorize(CaseEvaluationPermissions.<Entity>.<Create|Edit|Delete>)]` to
the 7 defective entry points in `src/HealthcareSupport.CaseEvaluation.Application/`.

Specifically:

1. `Appointments/AppointmentsAppService.cs` -- change class-level
   `[Authorize]` on line 28 to
   `[Authorize(CaseEvaluationPermissions.Appointments.Default)]` (matches the
   12 clean services' class-level pattern); replace bare `[Authorize]` on
   `CreateAsync` (line 161) with
   `[Authorize(CaseEvaluationPermissions.Appointments.Create)]`; replace bare
   `[Authorize]` on `UpdateAsync` (line 294) with
   `[Authorize(CaseEvaluationPermissions.Appointments.Edit)]`.
   `DeleteAsync` already gated; leave.
2. `AppointmentEmployerDetails/AppointmentEmployerDetailsAppService.cs` --
   change class-level `[Authorize]` (line 22) to
   `[Authorize(CaseEvaluationPermissions.AppointmentEmployerDetails.Default)]`;
   replace bare `[Authorize]` on `CreateAsync` (line 89) with
   `[Authorize(CaseEvaluationPermissions.AppointmentEmployerDetails.Create)]`;
   replace bare `[Authorize]` on `UpdateAsync` (line 109) with
   `[Authorize(CaseEvaluationPermissions.AppointmentEmployerDetails.Edit)]`.
3. `AppointmentAccessors/AppointmentAccessorsAppService.cs` -- change
   class-level `[Authorize]` (line 21) to
   `[Authorize(CaseEvaluationPermissions.AppointmentAccessors.Default)]`;
   replace bare `[Authorize]` on `CreateAsync` (line 92) with
   `[Authorize(CaseEvaluationPermissions.AppointmentAccessors.Create)]`;
   replace bare `[Authorize]` on `UpdateAsync` (line 109) with
   `[Authorize(CaseEvaluationPermissions.AppointmentAccessors.Edit)]`; replace
   bare `[Authorize]` on `DeleteAsync` (line 86) with
   `[Authorize(CaseEvaluationPermissions.AppointmentAccessors.Delete)]`.
4. `Appointments/AppointmentsAppService.cs:400`
   `UpsertApplicantAttorneyForAppointmentAsync` -- change bare `[Authorize]`
   to
   `[Authorize(CaseEvaluationPermissions.AppointmentApplicantAttorneys.Edit)]`
   (this method both creates and updates; `Edit` is the broader permission,
   or split to a composite policy if desired). Sub-question for Adrian --
   see Open sub-questions.
5. `Patients/PatientsAppService.cs:92`
   `GetOrCreatePatientForAppointmentBookingAsync` -- change bare `[Authorize]`
   to `[Authorize(CaseEvaluationPermissions.Patients.Create)]`. This method
   is invoked during appointment booking; any role with
   `Appointments.Create` will also need `Patients.Create`.
6. `Patients/PatientsAppService.cs:189`
   `UpdatePatientForAppointmentBookingAsync` -- change bare `[Authorize]`
   to `[Authorize(CaseEvaluationPermissions.Patients.Edit)]`.
7. `Patients/PatientsAppService.cs:328` `UpdateMyProfileAsync` -- this is
   self-service; the `CurrentUser.Id` ownership check inside the method body
   substitutes for a permission gate. **Leave bare `[Authorize]` unless
   Adrian wants a dedicated `Patients.UpdateMyProfile` permission** (see
   sub-question).

Shape: no entity change, no DTO change, no domain-service change, no
controller change, no Angular proxy change, no migration. Pure Application
layer attribute edit. 7 bare `[Authorize]` occurrences become 7 specific
`[Authorize(...)]` occurrences. Plus 3 class-level attribute upgrades from
bare `[Authorize]` to `[Authorize(...Default)]` to bring those services in
line with the 12 already-clean services.

After the fix, seed the two missing role grants if absent: verify that the
`CaseEvaluationPermissionDefinitionProvider.cs` registers each child
permission, and confirm the host admin role (granted in
`OpenIddictDataSeedContributor.cs` or equivalent seeder) has every
permission. For the MVP, run the existing seed contributor.

## Why this solution beats the alternatives

- Matches the pattern already used by 12 of 15 AppServices in the repo;
  net delta is small and uniform, easy for reviewers to verify.
- Uses only attribute metadata that ABP's `AuthorizationInterceptor`
  already consumes -- no new runtime code paths, no new tests for
  infrastructure plumbing, no new abstractions to document.
- Honours ADR-002 (manual controllers): the controller continues to be a
  thin delegate; authorization remains on the AppService where every other
  call-site enforces it.
- The permission constants already exist with Create / Edit / Delete
  children for every defective entity, so no `CaseEvaluationPermissions.cs`
  or `CaseEvaluationPermissionDefinitionProvider.cs` change is required.

## Effort (sanity-check vs inventory estimate)

Inventory says **M (2-3 days)**. Analysis adjusts down to **S (0.5 to 1
day)**. 10 attribute edits across 3 files plus one audit sweep. The
2-3-day figure in the gap analysis assumed 15 AppServices needed CUD-wide
changes; the actual scope is 3 services + 4 helpers. Add half a day for
one unit test per defective method (see NEW-QUAL-01 overlap) and the
total stays inside S. If Adrian bundles this with NEW-QUAL-01's
permission-enforcement coverage expansion (one unit test per defective
method calling the AppService with a low-privilege mock user and
asserting `AbpAuthorizationException`), the combined work is M (1-2
days).

## Dependencies

- **Blocks:** nothing. Should ship before MVP release as a HIPAA-relevant
  hardening. If internal-role-seeds (track 05) adds new roles, this fix
  ensures those roles cannot silently mutate data they don't own.
- **Blocked by:** nothing. The permission constants
  (`CaseEvaluationPermissions.<Entity>.<Create|Edit|Delete>`) already exist
  in the contracts project.
- **Related (not blocking):** NEW-QUAL-01 (critical-path test coverage) --
  permission enforcement is explicitly named as missing coverage. The
  lowest-friction NEW-QUAL-01 task is a unit test per the 7 hardened
  methods: sign in as a user with `Default` but NOT `Create`/`Edit`/`Delete`,
  invoke the method via `IAppointmentsAppService`, assert
  `AbpAuthorizationException`.
- **Blocked by open question:** none. The fix does not depend on any of the
  32 open questions in `gap-analysis/README.md:227-271`.

## Risk and rollback

- **Blast radius:** medium. After the fix, any role that has ONLY
  `Appointments.Default` / `AppointmentEmployerDetails.Default` /
  `AppointmentAccessors.Default` but NOT the corresponding Create/Edit/Delete
  child permissions will start receiving HTTP 403 on POST/PUT/DELETE. This
  is the correct behaviour, but it WILL break any user flow that relies on
  the current permissive state. Before merging, verify the 5 seeded roles
  (Admin, Doctor, Patient, Applicant Attorney, Claim Examiner) have the
  correct Create/Edit/Delete grants for Appointments and
  AppointmentAccessors. Patients is already correctly gated, so seed
  regression there should be zero.
- **Integration points to audit:** angular
  `appointments/appointment-add.component.ts` POSTs to
  `/api/app/appointments`; it already guards the button with
  `*abpPermission="'CaseEvaluation.Appointments.Create'"` so a Create role
  that loses Create will not see the button. But external test harnesses or
  direct-API consumers (Postman, the Swagger UI if exposed) will see the 403.
- **Rollback:** one commit revert. `git revert <commit-sha>`. No data
  migration, no seed change, no downstream cascade.

## Open sub-questions surfaced by research

1. **UpdateMyProfileAsync scope** -- should `Patients.UpdateMyProfileAsync`
   (PatientsAppService.cs:328) require a dedicated
   `Patients.UpdateMyProfile` permission, or is `CurrentUser.Id` ownership
   + bare `[Authorize]` sufficient? The method restricts the target by
   `GetCurrentPatientWithNavigationAsync` which throws
   `AbpAuthorizationException` for non-patient users, so authentication +
   ownership is already enforced. Recommendation: leave bare `[Authorize]`
   and document the intentional scope in code comment.
2. **UpsertApplicantAttorneyForAppointmentAsync permission choice** --
   AppointmentsAppService.cs:400 both creates and updates
   `ApplicantAttorney` + `AppointmentApplicantAttorney`. Apply
   `AppointmentApplicantAttorneys.Edit` (broader, covers update and create
   intent on the link entity) or apply two permissions and branch internally?
   Recommendation: apply `Edit` permission as the single gate; the method is
   an upsert and callers should hold Edit authority on the link. Adrian
   decision.
3. **Should class-level bare `[Authorize]` on AppointmentsAppService be
   promoted to `[Authorize(...Default)]`?** Currently 3 of 15 services use
   bare `[Authorize]` at class level (Appointments, AppointmentAccessors,
   AppointmentEmployerDetails, AppointmentTypes -- wait,
   AppointmentTypesAppService class-level is bare too, but its CUD methods
   are gated). For consistency with the 12 clean services, class-level
   should become `[Authorize(CaseEvaluationPermissions.<Entity>.Default)]`.
   This is a code-hygiene pass, not a security fix, but shipping together
   makes the diff coherent.
4. **NEW-QUAL-01 overlap** -- bundle with a test per hardened method?
   Recommendation: ship NEW-SEC-02 as a standalone PR (4-8 file changes,
   focused, low risk) and handle test coverage in a follow-up NEW-QUAL-01
   PR. Keeps review burden manageable.
