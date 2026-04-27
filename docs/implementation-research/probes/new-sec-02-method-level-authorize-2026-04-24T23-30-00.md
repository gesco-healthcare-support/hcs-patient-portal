# Probe log: new-sec-02-method-level-authorize

**Timestamp (local):** 2026-04-24T23:30:00
**Purpose:** Enumerate all AppService `CreateAsync` / `UpdateAsync` / `DeleteAsync`
methods in the NEW codebase and determine which carry a method-level
`[Authorize(permission)]` attribute vs a bare `[Authorize]` (authentication-only).
The gap-analysis claim (NEW-SEC-02) asserted that "most" mutating AppService
methods lack specific permission attributes. This probe tests that claim and
narrows the actual scope by static source inspection.

Dynamic proof was intentionally skipped: the only seeded credential is
`admin@abp.io` (host admin, all permissions), and the Live Verification
Protocol forbids creating a throwaway non-admin user (persistent state the
subagent cannot reliably revert). The brief explicitly permits static-only
proof under these conditions.

## Command 1 -- enumerate AppService classes

````
grep -rn "public class \w\+AppService : CaseEvaluationAppService" \
  W:/patient-portal/main/src/HealthcareSupport.CaseEvaluation.Application/
````

## Response 1 (files discovered)

Status: read-only grep; no HTTP.

15 `*AppService : CaseEvaluationAppService` classes plus `BookAppService`
(derives `ApplicationService`), `DoctorTenantAppService` (derives
`TenantAppService`), `ExternalSignupAppService` (derives
`CaseEvaluationAppService` but has no CUD CRUD -- it is a registration flow),
and `UserExtendedAppService` (derives ABP's `IdentityUserAppService`, not
scoped to this gap).

15 AppServices with standard CRUD (Create/Update/Delete) surface area:

````
ApplicantAttorneysAppService
AppointmentAccessorsAppService
AppointmentApplicantAttorneysAppService
AppointmentEmployerDetailsAppService
AppointmentLanguagesAppService
AppointmentStatusesAppService
AppointmentTypesAppService
AppointmentsAppService
DoctorAvailabilitiesAppService
DoctorsAppService
LocationsAppService
PatientsAppService
StatesAppService
WcabOfficesAppService
BookAppService
````

## Command 2 -- enumerate attributes on each mutating method

For each AppService, search for method-level `[Authorize(...)]` attributes
adjacent to `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `DeleteByIdsAsync`,
`DeleteAllAsync`:

````
grep -n "\[Authorize[^\]]*\]\|public.*\(CreateAsync\|UpdateAsync\|DeleteAsync\|DeleteByIdsAsync\|DeleteAllAsync\)" \
  <path-to-each-AppService>.cs
````

## Response 2 -- per-service attribute inventory

Redacted for brevity; full detail in the NEW-version-code-read section of the
solution brief. Key results (vulnerable = bare `[Authorize]` on a CUD method,
no permission policy name):

| AppService | CreateAsync | UpdateAsync | DeleteAsync | Status |
|---|---|---|---|---|
| AppointmentsAppService | `[Authorize]` (L161) | `[Authorize]` (L294) | `[Authorize(...Delete)]` | **VULNERABLE** on C, U |
| AppointmentEmployerDetailsAppService | `[Authorize]` (L89) | `[Authorize]` (L109) | `[Authorize(...Delete)]` | **VULNERABLE** on C, U |
| AppointmentAccessorsAppService | `[Authorize]` (L92) | `[Authorize]` (L109) | `[Authorize]` (L86) | **VULNERABLE** on C, U, D |
| ApplicantAttorneysAppService | `[Authorize(...Create)]` | `[Authorize(...Edit)]` | `[Authorize(...Delete)]` | clean |
| AppointmentApplicantAttorneysAppService | `[Authorize(...Create)]` | `[Authorize(...Edit)]` | `[Authorize(...Delete)]` | clean |
| AppointmentLanguagesAppService | `[Authorize(...Create)]` | `[Authorize(...Edit)]` | `[Authorize(...Delete)]` | clean |
| AppointmentStatusesAppService | `[Authorize(...Create)]` | `[Authorize(...Edit)]` | `[Authorize(...Delete)]` | clean (+ batch delete gated) |
| AppointmentTypesAppService | `[Authorize(...Create)]` | `[Authorize(...Edit)]` | `[Authorize(...Delete)]` | clean |
| DoctorAvailabilitiesAppService | `[Authorize(...Create)]` | `[Authorize(...Edit)]` | `[Authorize(...Delete)]` | clean |
| DoctorsAppService | `[Authorize(...Create)]` | `[Authorize(...Edit)]` | `[Authorize(...Delete)]` | clean |
| LocationsAppService | `[Authorize(...Create)]` | `[Authorize(...Edit)]` | `[Authorize(...Delete)]` | clean (+ batch delete gated) |
| PatientsAppService | `[Authorize(...Create)]` | `[Authorize(...Edit)]` | `[Authorize(...Delete)]` | clean (canonical CreateAsync/UpdateAsync) |
| StatesAppService | `[Authorize(...Create)]` | `[Authorize(...Edit)]` | `[Authorize(...Delete)]` | clean |
| WcabOfficesAppService | `[Authorize(...Create)]` | `[Authorize(...Edit)]` | `[Authorize(...Delete)]` | clean (+ batch delete gated) |
| BookAppService | `[Authorize(...Create)]` | `[Authorize(...Edit)]` | `[Authorize(...Delete)]` | clean |

## Command 3 -- identify bare-`[Authorize]` custom mutators in Patients/Appointments

````
grep -n "\[Authorize\]" \
  W:/patient-portal/main/src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs \
  W:/patient-portal/main/src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs
````

## Response 3 -- additional bare-`[Authorize]` mutating helper methods

Beyond the standard CreateAsync / UpdateAsync / DeleteAsync surface, two
classes expose custom helper methods that mutate data with only bare
`[Authorize]`:

- `PatientsAppService.cs:92` `GetOrCreatePatientForAppointmentBookingAsync` --
  creates IdentityUser, IdentityRole, Patient via `PatientManager.CreateAsync`.
- `PatientsAppService.cs:189` `UpdatePatientForAppointmentBookingAsync` --
  updates Patient via `PatientManager.UpdateAsync`.
- `PatientsAppService.cs:328` `UpdateMyProfileAsync` -- updates Patient for the
  current user (self-service; intentional scope, but still uses bare
  `[Authorize]` -- should likely check a `Patients.UpdateMyProfile` permission
  or rely on `CurrentUser.Id` ownership alone; see sub-question).
- `AppointmentsAppService.cs:400` `UpsertApplicantAttorneyForAppointmentAsync`
  -- creates/updates `ApplicantAttorney` and `AppointmentApplicantAttorney`.

## Interpretation

The original gap text ("15+ AppServices need updating") **substantially
overstates** the scope. The defect is concentrated in three entity surfaces
plus a small number of custom mutating helpers:

1. **AppointmentsAppService** -- CreateAsync (line 161) and UpdateAsync
   (line 294) use bare `[Authorize]`. DeleteAsync is already gated with
   `[Authorize(CaseEvaluationPermissions.Appointments.Delete)]`.
2. **AppointmentEmployerDetailsAppService** -- CreateAsync (line 89) and
   UpdateAsync (line 109) use bare `[Authorize]`. DeleteAsync is already
   gated.
3. **AppointmentAccessorsAppService** -- CreateAsync, UpdateAsync, AND
   DeleteAsync all use bare `[Authorize]`.

Plus 4 custom mutating helper methods (PatientsAppService x3 ,
AppointmentsAppService x1) that also use bare `[Authorize]` and mutate data.

**Total: 3 services + 4 helpers = 10 method signatures to harden**, not "15
services x 3-5 methods each" as the original gap estimate suggested.

### Why static proof is sufficient

The single seeded credential `admin@abp.io` (via LocalDB data seed) holds the
host admin role, which has every permission granted. A live probe with that
token cannot distinguish "permission check passed" from "no permission check
present" -- both return 200 for a mutating call. A dynamic probe proving the
bypass requires creating a second low-privilege test user (e.g.  a user
granted `Appointments.Default` but not `Appointments.Create`), calling
`POST /api/app/appointments`, and observing that the request succeeds where
it should have returned 403. That user creation mutates the `AbpUsers` and
`AbpUserRoles` tables; the Live Verification Protocol forbids IdentityUser
creation because reliable revert is not possible inside the subagent
lifecycle (role-user links, login history rows, OpenIddict sessions etc. may
outlive the explicit `DELETE`).

The static evidence chain is complete and independent:

1. Microsoft docs confirm bare `[Authorize]` uses the default policy which
   only checks authentication: see
   <https://learn.microsoft.com/en-us/aspnet/core/security/authorization/simple>
   (accessed 2026-04-24): "If neither Roles nor Policy is specified,
   [Authorize] uses the default policy: Authenticated (signed-in) users are
   authorized. Unauthenticated (signed-out) users are unauthorized."
2. Microsoft docs confirm that controller-level and action-level policies
   combine on an AND basis, so a bare method-level `[Authorize]` does not
   override or replace a class-level `[Authorize(...Default)]`; both must
   pass. But `Appointments.Default` covers view access (granted to all
   operator roles per `CaseEvaluationPermissionDefinitionProvider.cs`), which
   means the AND reduces to "authenticated + has view" -- still not
   "has Create".
3. ABP docs confirm `[Authorize]` works the same on AppServices because
   ABP's `AuthorizationInterceptor` (at
   `framework/src/Volo.Abp.Authorization/Volo/Abp/Authorization/AuthorizationInterceptor.cs`)
   calls `IMethodInvocationAuthorizationService.CheckAsync` which reads the
   same attribute metadata.
4. The NEW codebase itself acknowledges the bug: the Appointments feature
   `CLAUDE.md`
   (`W:/patient-portal/main/src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md:122`
   and line 134-135) explicitly states "Appointments.Edit permission is
   checked on the Angular list UI but never checked in the AppService --
   UpdateAsync only requires [Authorize] (any authenticated user). Same for
   CreateAsync."

## Cleanup (if mutating)

N/A. All commands were read-only (grep + file reads). No HTTP calls issued.
No database state modified.
