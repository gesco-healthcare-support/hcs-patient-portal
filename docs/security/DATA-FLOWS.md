[Home](../INDEX.md) > Security > PHI Data Flows

# PHI Data Flows

> For known security vulnerabilities and remediation status, see [Security Issues](../issues/SECURITY.md).

This document maps where Protected Health Information (PHI) lives, how it moves through the system, and every place it may be persisted or logged. Required for HIPAA technical safeguard analysis.

**Last verified:** 2026-04-13
**Method:** code-inspect on entity definitions + module configuration

---

## PHI-Containing Entities

Three entities contain PHI directly. Others (Doctor, Location, etc.) are non-PHI master data.

| Entity | PHI Fields | IMultiTenant? | Source |
|---|---|---|---|
| `Patient` | Name (first/last), DOB, SSN, contact info, address, gender, marital status | **No** (has TenantId property but filter not applied) | `src/.../Domain/Patients/Patient.cs` |
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
| SQL Server (primary) | All PHI fields in their full form | Highest | Tenant filter (except Patient), permission-gated access, audit logs |
| EF Core change tracker (memory) | Current request's PHI during processing | Transient | Scoped per-request |
| Redis cache | Permission grants, distributed cache entries, data protection keys | Low-medium | No PHI keys cached by default; verify no AppService uses `ICacheManager.Get<Appointment>` patterns |
| ABP audit log table | Entity snapshots on Create/Update when enabled | High | Stored in DB; retention policy undocumented |
| Serilog file sink (`Logs/`) | Exception messages that may embed PHI; with SEC-02 active, full JWT claims and PII | High | Active gap: SEC-02 logs full PII unless `App:DisablePII=true` |
| HTTP response body (transit) | DTOs returned to the browser | Medium | HTTPS required; CORS limited to known origins |
| Browser localStorage | Access/refresh tokens (not PHI directly, but grants access) | Medium | Token location needs audit (localStorage vs httpOnly cookie) |
| Browser memory | Currently-viewed PHI in Angular state | Transient | Cleared on tab close; no IndexedDB persistence in feature modules |

---

## Cross-Tenant PHI Risk (critical)

**Patient does not implement `IMultiTenant`.** ABP's automatic tenant filter does not scope Patient queries. A developer writing a query like:

```csharp
// Pattern used by PatientsRepository
var patient = await _patientRepository.FindAsync(p => p.Email == email);
```

will match patients across all tenants unless `TenantId` is manually included in the predicate:

```csharp
// Safe pattern
var patient = await _patientRepository.FindAsync(p =>
    p.Email == email && p.TenantId == CurrentTenant.Id);
```

This is documented in the [Patient feature CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/Patients/CLAUDE.md). Every Patient repository method must be audited for manual tenant scoping. See related discussion in [docs/architecture/MULTI-TENANCY.md](../architecture/MULTI-TENANCY.md).

---

## Egress Paths

Places PHI can leave the system:

| Path | Destination | Control |
|---|---|---|
| HTTPS API response | Authenticated browser | JWT + permission check |
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
- [Patient Feature Doc](../features/patients/overview.md) -- Patient entity details
- [Multi-Tenancy Architecture](../architecture/MULTI-TENANCY.md) -- tenant isolation design
