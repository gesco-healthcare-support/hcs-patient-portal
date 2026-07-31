[Home](../INDEX.md) > Security > PHI Data Flows

# PHI Data Flows

> Purpose: Map where PHI lives, how it moves through the system, and every persistence/logging point. Audience: security auditor, backend developer. Last verified: 2026-06-01 vs main.

This document maps where Protected Health Information (PHI) lives, how it moves through the system, and every place it may be persisted or logged. Required for HIPAA technical safeguard analysis.

**Last verified:** 2026-06-01
**Method:** code-inspect on entity definitions + module configuration

---

## PHI-Containing Entities

Three entities contain PHI directly. Others (Doctor, Location, etc.) are non-PHI master data.

| Entity | PHI Fields | IMultiTenant? | Source |
|---|---|---|---|
| `Patient` | Name (first/last), DOB, SSN, contact info, address, gender, marital status | **Yes** (FEAT-09, ADR-006 T4, 2026-05-05: implements `IMultiTenant`; ABP auto-filter scopes reads by `CurrentTenant.Id`; host/admin paths disable the filter via `IDataFilter<IMultiTenant>.Disable()` for cross-tenant reads) | `src/.../Domain/Patients/Patient.cs` |
| `Appointment` | Patient link, doctor link, claim number, DOI (date of injury), medical context | Yes | `src/.../Domain/Appointments/Appointment.cs` |
| `AppointmentEmployerDetail` | Employer name, occupation, employer address | Yes | `src/.../Domain/AppointmentEmployerDetails/AppointmentEmployerDetail.cs` |

Supporting entities (`AppointmentAccessor`, `AppointmentApplicantAttorney`) link PHI records to identity users but do not contain PHI fields themselves. Access to them still requires authorization because they reveal relationships between PHI records and people.

---

## Create Appointment (typical PHI flow)

```mermaid
sequenceDiagram
    actor Staff as HCS Staff (browser)
    participant NG as Angular SPA
    participant API as HttpApi.Host
    participant Svc as AppointmentsAppService
    participant Mgr as AppointmentManager<br/>(DomainService)
    participant Repo as Repository
    participant DB as SQL Server
    participant Log as Serilog/Audit

    Staff->>NG: Fill appointment form<br/>(patient, doctor, slot)
    NG->>API: POST /api/app/appointments<br/>(JWT bearer; AppointmentCreateDto)
    API->>Svc: CreateAsync(dto)
    Note over Svc: [Authorize(Appointments.Create)]
    Svc->>Mgr: CreateAsync(...) -- business rules
    Mgr->>Repo: InsertAsync(appointment)
    Repo->>DB: INSERT ... TenantId = current tenant
    DB-->>Repo: OK
    Repo-->>Mgr: entity
    Mgr-->>Svc: entity
    Svc->>Log: ABP audit log entry<br/>(user, action, entity id)
    Svc-->>API: AppointmentDto
    API-->>NG: 200 OK + DTO
    NG-->>Staff: Booking confirmation
```

---

## PHI Persistence Locations

Each location where PHI can land, with the implication for HIPAA analysis:

| Location | What PHI lands here | Risk | Mitigation |
|---|---|---|---|
| SQL Server (primary) | All PHI fields in their full form | Highest | Tenant filter (all PHI entities including Patient as of FEAT-09), permission-gated access, audit logs |
| EF Core change tracker (memory) | Current request's PHI during processing | Transient | Scoped per-request |
| Redis cache | Permission grants, distributed cache entries, data protection keys | Low-medium | No PHI keys cached by default; verify no AppService uses `ICacheManager.Get<Appointment>` patterns |
| ABP audit log table | Entity snapshots on Create/Update when enabled | High | Stored in DB; retention policy undocumented |
| Serilog file sink (`Logs/`) | Exception messages that may embed PHI; with SEC-02 active, full JWT claims and PII | High | Active gap: SEC-02 logs full PII unless `App:DisablePII=true` |
| HTTP response body (transit) | DTOs returned to the browser | Medium | HTTPS required; CORS limited to known origins |
| Browser localStorage | Access/refresh tokens (not PHI directly, but grants access) | Medium | Token location needs audit (localStorage vs httpOnly cookie) |
| Browser memory | Currently-viewed PHI in Angular state | Transient | Cleared on tab close; no IndexedDB persistence in feature modules |

---

## Cross-Tenant PHI Isolation (closed -- FEAT-09)

**`Patient` now implements `IMultiTenant` (FEAT-09, ADR-006 T4, 2026-05-05).** ABP's
automatic tenant filter scopes all `Patient` queries by `CurrentTenant.Id`. Manual
`TenantId` predicates in repository methods are no longer required for correctness --
the framework filter handles them.

Host-context callers (admin / IT-Admin) run with `CurrentTenant.Id == null`, which would
cause ABP to emit `WHERE TenantId IS NULL` and exclude every tenant-scoped row. To allow
cross-tenant reads in those paths, `PatientsAppService` wraps each host-context call with:

```csharp
// _dataFilter is IDataFilter<IMultiTenant>, so .Disable() scopes to that filter
using (_dataFilter.Disable()) { ... }
```

This pattern mirrors `DoctorsAppService` (Doctor is also `IMultiTenant`). Tenant-scoped
callers (booking flow, patient self-service) run inside an OAuth-resolved tenant context;
the filter applies and scopes correctly without any disable call.

The previous risk -- any caller with the `Patients` permission could read every tenant's
patients -- is closed. See [docs/architecture/MULTI-TENANCY.md](../architecture/MULTI-TENANCY.md)
for the broader tenant isolation design and the [Patient feature CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/Patients/CLAUDE.md)
for entity-level details.

---

## Egress Paths

Places PHI can leave the system:

| Path | Destination | Control |
|---|---|---|
| HTTPS API response (standard) | Authenticated browser | JWT + permission check; SSN masked to last 4 via `SsnVisibility.MaskToLast4` on every `PatientDto` / `PatientWithNavigationPropertiesDto` exit |
| `GET api/app/patients/{id}/ssn` (SSN reveal) | Authenticated browser | `Patients.RevealSsn` permission (declarative) + `SsnRevealAccess.CanReveal` internal-or-owner check (imperative); returns full `SocialSecurityNumber`; every call recorded in ABP HTTP audit log (caller + patient id) |
| CORS preflight | Allowed origins only | Configured in `CaseEvaluationHttpApiHostModule.cs` ConfigureCors |
| Error pages / Swagger | Development only | Disabled in production builds |
| Log files | Local disk | No log shipping configured; logs remain on host |
| Database backups | Wherever SA takes them | Not configured in repo; operator responsibility |
| Excel export | Authenticated browser download | MiniExcel library; verify permission on each export endpoint |

No email sending, no SMS, no third-party data sharing integrations are configured in the current codebase.

---

## Related Documents

- [Threat Model](THREAT-MODEL.md) -- STRIDE analysis of the same components
- [Authorization](AUTHORIZATION.md) -- permission gates controlling PHI access
- [HIPAA Compliance](HIPAA-COMPLIANCE.md) -- technical safeguard inventory
- [Patient Domain CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/Patients/CLAUDE.md) -- Patient entity details, SSN rules, fuzzy match
- [Multi-Tenancy Architecture](../architecture/MULTI-TENANCY.md) -- tenant isolation design
