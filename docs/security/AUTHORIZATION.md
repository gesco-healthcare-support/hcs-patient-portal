[Home](../INDEX.md) > Security > Authorization

# Authorization & Permission Matrix

> For known security vulnerabilities and remediation status, see [Security Issues](../issues/SECURITY.md).

This document summarizes the permission surface and its mapping to roles, entities, and API endpoints. For permission implementation details (definition provider, localization, child-permission registration), see [backend/PERMISSIONS.md](../backend/PERMISSIONS.md).

**Source of truth:** `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs`

**Last verified:** 2026-04-13

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
| Patients | yes | yes | yes | yes | Both (Patient lacks IMultiTenant) |
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

**Mixed (cautionary):** Patients. Has `TenantId` but does not implement `IMultiTenant`. Application code must apply tenant scoping manually on every query. See [DATA-FLOWS.md](DATA-FLOWS.md#cross-tenant-phi-risk-critical).

**Cross-tenant escape hatch:** `DoctorsAppService` uses `IDataFilter.Disable<IMultiTenant>()` to enumerate doctors across tenants from host context. This is the only sanctioned use of the disable pattern in the Application layer. Any new code disabling `IMultiTenant` filter requires explicit ADR.

**Explicit tenant switch:** `DoctorTenantAppService` uses `CurrentTenant.Change(tenantId)` during tenant provisioning. This is the only sanctioned use of `CurrentTenant.Change` outside of ABP framework code.

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
