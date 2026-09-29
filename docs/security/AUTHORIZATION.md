[Home](../INDEX.md) > Security > Authorization

# Authorization & Permission Matrix

> Purpose: Document the permission surface, role mapping, and multi-tenancy enforcement rules. Audience: backend developers, security reviewers.

> For known security vulnerabilities and remediation status, see [Security Issues](THREAT-MODEL.md).

This document summarizes the permission surface and its mapping to roles, entities, and API endpoints. For permission implementation details (definition provider, localization, child-permission registration), see [backend/PERMISSIONS.md](../backend/PERMISSIONS.md).

**Source of truth:** `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs`

---

## Permission Groups

The root group is `CaseEvaluation`. There are **39 top-level groups**; the table below names some
of them and is not maintained as a complete list. Read the source file for the full tree.

> **Three things in this table were wrong until 2026-09-28**, and they are worth naming because
> each would mislead in a different direction.
>
> 1. **The multi-tenancy side column.** States, AppointmentTypes, AppointmentStatuses,
>    AppointmentLanguages, Locations and WcabOffices were marked `Host`. None of those
>    registrations passes a `MultiTenancySides` argument, so ABP registers each as `Both`. Of 138
>    `AddPermission` and `AddChild` calls, only six declare a side: the two `Dashboard` variants
>    and four genuinely host-only grants (integration dead letters, intake assignments, intake
>    impersonation, Case Tracker integration).
> 2. **`Books`** was listed. That ABP-template sample permission was removed; only a comment
>    recording the removal survives in the definition provider.
> 3. **`AppointmentAccessors`** was listed with Default, Create, Edit and Delete. **No
>    `AppointmentAccessors` permission of any kind exists.** `AppointmentAccessorsAppService`
>    carries a bare `[Authorize]` and gates per-appointment access through
>    `AppointmentReadAccessGuard` instead. Anyone who tried to grant that permission would find
>    nothing to grant, and anyone who assumed it was enforcing something would be wrong.

| Group | Default | Create | Edit | Delete | Multi-tenancy side |
|---|---|---|---|---|---|
| Dashboard | -- | -- | -- | -- | Host / Tenant (declared explicitly) |
| States | yes | yes | yes | yes | Both |
| AppointmentTypes | yes | yes | yes | yes | Both |
| AppointmentStatuses | yes | yes | yes | yes | Both |
| AppointmentLanguages | yes | yes | yes | yes | Both |
| Locations | yes | yes | yes | yes | Both |
| WcabOffices | yes | yes | yes | yes | Both |
| Doctors | yes | yes | yes | yes | Both |
| DoctorAvailabilities | yes | yes | yes | yes | Both |
| Patients | yes | yes | yes | yes | Both |
| Patients.RevealSsn | yes | -- | -- | -- | Both |
| Appointments | yes | yes | yes | yes | Both |
| AppointmentEmployerDetails | yes | yes | yes | yes | Both |
| ApplicantAttorneys | yes | yes | yes | yes | Both |
| AppointmentApplicantAttorneys | yes | yes | yes | yes | Both |

`Appointments` also carries `Approve`, `Reject`, `RequestCancellation`, `RequestReschedule`,
`PushToCaseTracker` and `ViewIntegrationDeadLetters` beyond the CRUD four.

**Dashboard permissions:** `CaseEvaluation.Dashboard.Host` and `CaseEvaluation.Dashboard.Tenant`. These gate the dashboard widgets by multi-tenancy side. The host dashboard aggregates across tenants; the tenant dashboard is scoped to the current tenant.

**Pattern:** Every CRUD-capable entity follows `GroupName.{Entity}` with child permissions `.Create`, `.Edit`, `.Delete`. `Default` grants view / list access.

---

## Roles

> **This section said "ABP seeds two default roles; this project does not define custom roles in
> code", and that the repository "does not yet define role-based permission seeds beyond ABP
> defaults".** Both were wrong. Seven named roles are seeded in code, and both seeders grant
> permission sets to them at seed time, per office. Corrected 2026-09-28.

Seven named roles plus the ABP superuser.

| Role | Seeded by | Scope | Notes |
|---|---|---|---|
| `admin` | ABP | Host + Tenant | The framework superuser. Sees every nav item |
| `IT Admin` | `InternalUserRoleDataSeedContributor` | Host | Internal |
| `Staff Supervisor` | `InternalUserRoleDataSeedContributor` | Host + Tenant | Internal |
| `Intake Staff` | `InternalUserRoleDataSeedContributor` | Host + Tenant | Internal |
| `Patient` | `ExternalUserRoleDataSeedContributor` | Tenant | External |
| `Applicant Attorney` | `ExternalUserRoleDataSeedContributor` | Tenant | External |
| `Defense Attorney` | `ExternalUserRoleDataSeedContributor` | Tenant | External |
| `Claim Examiner` | `ExternalUserRoleDataSeedContributor` | Tenant | External |

**The four external roles receive a byte-identical permission set.** The seeder loops over
`ExternalRoleConsts.All` and grants every role the same `BookingBaselineGrants()` list, so any
statement that a Defense Attorney or Claim Examiner cannot do something an Applicant Attorney can
is wrong at the permission layer. The only per-role difference is `Patients.RevealSsn`, granted to
`Patient` alone.

Differences in what those roles can actually reach come from **per-record access**, not from
permissions: see the appointment-accessor rules below.

Internal roles never appear in `ExternalRoleConsts`, and external users never reach the internal
shell -- `internalUserOnlyMatchGuard` and `externalUserOnlyMatchGuard` decide that at the route
level in the Angular app.

---

## Endpoint to Permission Map

API endpoints are defined on AppServices, which live under feature folders in the Application project. Permissions are enforced via `[Authorize(Permission)]` attributes on the AppService class or method. Each controller in `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/` is a thin delegation layer; authorization happens at the AppService.

The standard pattern per entity (e.g., Appointments):

| HTTP Verb / Route | AppService Method | Required Permission |
|---|---|---|
| `GET /api/app/appointments` | `GetListAsync` | `CaseEvaluation.Appointments` (Default) |
| `GET /api/app/appointments/{id}` | `GetAsync` | `CaseEvaluation.Appointments` (Default) |
| `POST /api/app/appointments` | `CreateAsync` | `CaseEvaluation.Appointments.Create` |
| `PUT /api/app/appointments/{id}` | `UpdateAsync` | `CaseEvaluation.Appointments.Edit` |
| `DELETE /api/app/appointments/{id}` | `DeleteAsync` | `CaseEvaluation.Appointments.Delete` |

Feature-specific custom methods (e.g., `AppointmentsAppService.UpdateStatusAsync`, `DoctorAvailabilitiesAppService.GenerateSlotsAsync`) may require additional permissions; refer to the feature's CLAUDE.md in `src/.../Domain/{Feature}/CLAUDE.md` for per-method details.

---

## Multi-tenancy Authorization Rules

> **This section previously listed Locations, States, WcabOffices, AppointmentTypes,
> AppointmentStatuses and AppointmentLanguages as host-only entities that "tenants read via the
> host database", and claimed a single sanctioned use each of the tenant-filter disable and of
> `CurrentTenant.Change`. All of that was wrong.** Corrected 2026-09-28 with counts.

**There are two host-only entities, and they are not reference data:** `OfficeBranding` and
`IntakeOfficeAssignment`. Everything else a maintainer is likely to touch is tenant-scoped,
including all six entities this section used to call host-only. Under database-per-office there is
no shared catalogue to read from: each office owns its own locations, states, appointment types
and languages. See [MULTI-TENANCY.md](../architecture/MULTI-TENANCY.md).

**Disabling the tenant filter is not rare.** There are **12 call sites in the Application layer**,
not one:

| Service | Sites | Why |
|---|---|---|
| `PatientsAppService` | 10 | Host and IT-Admin paths run with `CurrentTenant.Id == null`, where the filter generates `WHERE TenantId IS NULL` and excludes every office's rows |
| `DoctorsAppService` | 1 | Same reason, for doctors |
| `InternalUsersAppService` | 1 | `Disable<IMultiTenant>()` explicitly |

Both `PatientsAppService` and `DoctorsAppService` hold `IDataFilter<IMultiTenant>`, so their
bare `.Disable()` lifts the tenant filter only, not every filter. Two further sites disable
`ISoftDelete` instead, which is a different thing and not a tenancy concern.

Count them before trusting this table:

```bash
git grep -nE '_dataFilter\.Disable|DataFilter\.Disable' -- 'src/HealthcareSupport.CaseEvaluation.Application/*.cs'
```

That command prints 16 lines, not 14. Two of them are comments (`PatientsAppService.cs:43` and
`ExternalSignupAppService.cs:82`), and the other two non-table lines are the `ISoftDelete` sites.

**`CurrentTenant.Change` is common, not exceptional.** There are **65 call sites across 34 files**
in the Application layer alone, and more elsewhere. 28 of them, in 6 files, call the base-class
`CurrentTenant.Change(`; the other 37 call the same method through an injected field,
`_currentTenant.Change(`, and 28 of those are in `Notifications/`. Count code lines only, because
the plain grep also matches comments and XML docs (8 of its 73 lines):

```bash
git grep -nE '[Cc]urrentTenant\.Change\(' -- 'src/HealthcareSupport.CaseEvaluation.Application/*.cs' \
  | grep -vE '^[^:]+:[0-9]+:[[:space:]]*(//|/\*|\*)'
```

`DoctorTenantAppService` has exactly **one**,
and it is `CurrentTenant.Change(null)` -- opening host context, the opposite of the
`Change(tenantId)` this section described. Any background job that touches every office uses the
pattern by design, through `TenantWorkRunner`.

**What actually constrains cross-office access** is not a convention about these two APIs. It is
that each office has its own database, so a query on an office connection cannot reach another
office's rows at all, and that the resolver chain refuses caller-supplied tenant selection. See
[TENANCY-AND-ISOLATION.md](../architecture/TENANCY-AND-ISOLATION.md).

See [DATA-FLOWS.md](DATA-FLOWS.md) for SSN egress rules.

---

## Data-level authorization: appointment accessors (per-row)

Beyond the permission matrix above, the three accessor-mutation endpoints enforce a **per-row**
rule the ABP permission system does not express. `AppointmentAccessorsAppService` `CreateAsync`
/ `UpdateAsync` / `DeleteAsync` call `AppointmentReadAccessGuard.EnsureCanManageAccessorsAsync`,
which composes the pure `AppointmentAccessRules.CanManageAccessors` rule (Workstream B,
2026-06-10):

> A caller may add / edit / remove an appointment's accessors only if they are an **internal
> user**, OR they **created the appointment AND hold an authorized accessor-managing external
> role** (Applicant Attorney / Defense Attorney today).

The authorized external-role set lives in `BookingFlowRoles.ExternalAccessorManagerRoles`,
neutrally named so the paralegal-on-behalf-of-attorney feature can append `Paralegal` as a
one-line extension. Angular hides the "Add" control for callers who fail this rule, but the
server gate is authoritative (deny-by-default): a forced POST from an unauthorized caller
returns the localized `Appointment:AccessDenied`.

**Deliberate tightening (product decision, not OLD parity).** This rule is STRICTER than the
appointment edit-access rule (`AppointmentAccessRules.CanEdit`): it drops the Edit-accessor
pathway, so an Edit-accessor can still complete/edit the appointment form and submit
cancel/reschedule change-requests, but can no longer self-propagate accessors. A Patient or
Claim-Examiner creator is likewise denied. The change-request flow
(`AppointmentChangeRequestsAppService`) deliberately keeps the looser `CanEditAsync` gate, so
external cancel/reschedule is unaffected.

---

## Data-level authorization: appointment row-level visibility (email + role)

Firm-based AA/DA work (2026-06-12) made per-row appointment **visibility** role-gated, and the list
query and the per-appointment read guard now share one rule so they always agree (a row shown in the
list never 403s on click, and a hidden row is never openable by deep link).

An external caller may see / open an appointment only if ANY of:

> 1. they are the appointment **creator** (`CreatorId`); OR
> 2. they hold an explicit **AppointmentAccessor** grant on it; OR
> 3. they are the **patient identity** on the row (`Patient.IdentityUserId`); OR
> 4. **email + role**: one of the appointment's denormalized party-email columns equals the caller's
>    email AND the caller holds that column's role -- `PatientEmail`->Patient,
>    `ApplicantAttorneyEmail`->Applicant Attorney, `DefenseAttorneyEmail`->Defense Attorney,
>    `ClaimExaminerEmail`->Claim Examiner.

- List query: `AppointmentsAppService.ComputeExternalPartyVisibilityAsync`. Read guard:
  `AppointmentReadAccessGuard.EnsureCanReadAsync`. Both call the pure rule
  `AppointmentAccessRules.IsAppointmentEmailRoleVisible`.
- The earlier **role-agnostic** email match and the bare **id-based** AA/DA link pathways were
  REMOVED: with registration auto-link keying by email, those would surface a party column to a user
  who lacks that column's role (cross-role leak). Internal-role callers bypass narrowing entirely.

**Role accumulation (D9).** Adding an external account as an accessor under a role it does not yet
hold now **grants that role** (`AppointmentAccessorRules.ResolveOutcome` returns `GrantRoleAndLink`
instead of the former `RoleMismatch`). This is how a firm accumulates Applicant + Defense Attorney
and thereby sees both sides; it is safe because visibility stays gated by role (the grant only reveals
the newly-held role's own-side appointments). The grant is gated upstream by `CanManageAccessors`
(internal staff or the creator who holds AA/DA), so it cannot be self-initiated.

---

## The authorization surface is measured, and you should read the measurement

The repository generates an approval snapshot of every public application-service method and the
guard on it:

```text
test/HealthcareSupport.CaseEvaluation.Application.Tests/Authorization/authorization-surface.approved.txt
```

Each line reads `Namespace.Service.Method(args) -> class=<guard> method=<permission or ->`. There
are 358 entries. **This file, not this page, is the authority on what is guarded.**

What it currently records:

| Shape | Count | Meaning |
|---|---|---|
| `class=(authenticated)` | 181 | Class-level bare `[Authorize]`: signed in, no permission required at the class |
| `method=-` under `class=(authenticated)` | 20 | **No permission anywhere.** Any signed-in caller reaches these |
| `class=(anonymous)` | 2 | Deliberate: the public change-request consent endpoints, gated by a single-use token |

`AuthorizationSurfaceInvariantTests` guards this well: it asserts every method declares some
authorization, that every anonymous method sits on a justified allow-list, that the allow-list
carries no stale entries, and that the surface exceeds 200 entries so the suite cannot pass
against an empty inventory.

**But note exactly what the first invariant asserts.** "Declares some authorization" is satisfied
by a bare `[Authorize]`. That is **authentication**, not authorisation: it proves a caller is
signed in and nothing about whether they should see the record they asked for. The 20 methods with
no permission at all pass that invariant. So the harness is a good tripwire for a forgotten
attribute and is not a check that access is correctly scoped.

Many methods need no permission by design -- a caller reading their own profile or their own
notifications, for example. Holding a permission is not access to a particular record either:
per-record access is decided in code, by guards such as `AppointmentReadAccessGuard` (see the
data-level sections above), and the snapshot does not show those guards.

Also worth knowing: a method-level `[RemoteService(IsEnabled = false)]` removes a method from the
HTTP surface entirely, so an entry in that snapshot is not by itself evidence of a reachable
endpoint. Check the hand-written controller before drawing a conclusion.

## Enforcement Gaps (to be audited)

1. **Permission held is not access granted.** The most common mistake in this codebase. Holding
   `Appointments.Edit` does not mean you may edit a PARTICULAR appointment. Per-record access is
   `AppointmentReadAccessGuard` over `AppointmentAccessRules`. A service that checks only a
   permission has not checked access.
2. **Anonymous endpoints** are inventoried, not "should be": the allow-list and its staleness
   check are in `AuthorizationSurfaceInvariantTests`.
3. **Automated lint for a missing `[Authorize]` exists** -- it is the invariant test above. What
   does not exist is a check that a guard is the RIGHT strength.
4. **Controllers do not re-check.** Still true: controllers delegate, so a gap in an application
   service is a gap in the endpoint.

---

## Related Documents

- [backend/PERMISSIONS.md](../backend/PERMISSIONS.md) -- permission implementation details, definition provider
- [THREAT-MODEL.md](THREAT-MODEL.md) -- elevation-of-privilege analysis
- [api/AUTHENTICATION-FLOW.md](../api/AUTHENTICATION-FLOW.md) -- how users obtain tokens that carry permission claims
- [architecture/MULTI-TENANCY.md](../architecture/MULTI-TENANCY.md) -- IMultiTenant filter behavior
