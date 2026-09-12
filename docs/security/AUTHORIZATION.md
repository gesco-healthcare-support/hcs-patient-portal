[Home](../INDEX.md) > Security > Authorization

# Authorization & Permission Matrix

> Purpose: Document the permission surface, role mapping, and multi-tenancy enforcement rules. Audience: backend developers, security reviewers. Last verified: 2026-06-01 vs main.

> For known security vulnerabilities and remediation status, see [Security Issues](THREAT-MODEL.md).

This document summarizes the permission surface and its mapping to roles, entities, and API endpoints. For permission implementation details (definition provider, localization, child-permission registration), see [backend/PERMISSIONS.md](../backend/PERMISSIONS.md).

**Source of truth:** `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs`

**Last verified:** 2026-06-01

---

## Permission Groups

The root group is `CaseEvaluation`. All permissions are nested under it.

| Group | Default | Create | Edit | Delete | Multi-tenancy side |
|---|---|---|---|---|---|
| Dashboard | -- | -- | -- | -- | Host / Tenant (two vars) |
| Books | yes | yes | yes | yes | Both |
| States | yes | yes | yes | yes | Host |
| AppointmentTypes | yes | yes | yes | yes | Host |
| AppointmentStatuses | yes | yes | yes | yes | Host |
| AppointmentLanguages | yes | yes | yes | yes | Host |
| Locations | yes | yes | yes | yes | Host |
| WcabOffices | yes | yes | yes | yes | Host |
| Doctors | yes | yes | yes | yes | Tenant |
| DoctorAvailabilities | yes | yes | yes | yes | Tenant |
| Patients | yes | yes | yes | yes | Both |
| Patients.RevealSsn | yes | -- | -- | -- | Both |
| Appointments | yes | yes | yes | yes | Tenant |
| AppointmentEmployerDetails | yes | yes | yes | yes | Tenant |
| AppointmentAccessors | yes | yes | yes | yes | Tenant |
| ApplicantAttorneys | yes | yes | yes | yes | Tenant |
| AppointmentApplicantAttorneys | yes | yes | yes | yes | Tenant |

**Dashboard permissions:** `CaseEvaluation.Dashboard.Host` and `CaseEvaluation.Dashboard.Tenant`. These gate the dashboard widgets by multi-tenancy side. The host dashboard aggregates across tenants; the tenant dashboard is scoped to the current tenant.

**Pattern:** Every CRUD-capable entity follows `GroupName.{Entity}` with child permissions `.Create`, `.Edit`, `.Delete`. `Default` grants view / list access.

---

## Roles (ABP Identity defaults)

ABP seeds two default roles; this project does not define custom roles in code.

| Role | Scope | Intended Permissions |
|---|---|---|
| `admin` | Host + Tenant | All permissions for their scope |
| (user's direct permissions) | Tenant | Individually granted per user |

**Gap:** The repository does not yet define role-based permission seeds beyond ABP defaults. New tenants get no custom role setup; all permissions must be granted per-user or via manual role creation in the admin UI.

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

**Host-only entities:** Locations, States, WcabOffices, AppointmentTypes, AppointmentStatuses, AppointmentLanguages. Only host-context users can mutate these. Tenants read them via the host database.

**Tenant-scoped entities:** Doctors, DoctorAvailabilities, Appointments, AppointmentEmployerDetails, AppointmentAccessors, ApplicantAttorneys, AppointmentApplicantAttorneys. Automatically filtered by `IMultiTenant` data filter.

**Tenant-scoped (auto-filtered):** Patients. Implements `IMultiTenant` since FEAT-09 (2026-05-05); ABP's data filter scopes all queries to `CurrentTenant.Id` automatically. Host or IT Admin paths that must enumerate across tenants must explicitly opt in via `IDataFilter.Disable<IMultiTenant>()`, matching the pattern used in `DoctorsAppService`. See [DATA-FLOWS.md](DATA-FLOWS.md) for SSN egress rules.

**Cross-tenant escape hatch:** `DoctorsAppService` uses `IDataFilter.Disable<IMultiTenant>()` to enumerate doctors across tenants from host context. This is the only sanctioned use of the disable pattern in the Application layer. Any new code disabling `IMultiTenant` filter requires explicit ADR.

**Explicit tenant switch:** `DoctorTenantAppService` uses `CurrentTenant.Change(tenantId)` during tenant provisioning. This is the only sanctioned use of `CurrentTenant.Change` outside of ABP framework code.

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

## Enforcement Gaps (to be audited)

1. **Controller layer:** Controllers delegate to AppServices. If an AppService method lacks `[Authorize]`, there is no fallback; the controller does not re-check. Gap: no automated lint for missing `[Authorize]`.
2. **Anonymous endpoints:** Some AppService methods may have `[AllowAnonymous]` (e.g., public signup flow in `ExternalSignups`). These should be manually inventoried.
3. **Permission check vs policy check:** All checks are permission-based, not policy-based. Data-level authorization (e.g., "only the appointment owner can edit") is inconsistent and implemented per-AppService when present.

---

## Related Documents

- [backend/PERMISSIONS.md](../backend/PERMISSIONS.md) -- permission implementation details, definition provider
- [THREAT-MODEL.md](THREAT-MODEL.md) -- elevation-of-privilege analysis
- [api/AUTHENTICATION-FLOW.md](../api/AUTHENTICATION-FLOW.md) -- how users obtain tokens that carry permission claims
- [architecture/MULTI-TENANCY.md](../architecture/MULTI-TENANCY.md) -- IMultiTenant filter behavior
