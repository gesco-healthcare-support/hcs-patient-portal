# AppointmentEmployerDetails

Employer information associated with an appointment — the patient's employer at the time of the workers' comp claim. One employer detail record per appointment. Created during the appointment booking flow. No standalone UI.

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/.../Domain.Shared/AppointmentEmployerDetails/AppointmentEmployerDetailConsts.cs` | Max lengths (EmployerName=255, Occupation=255, PhoneNumber=12, Street=255, City=255, ZipCode=10) |
| Domain | `src/.../Domain/AppointmentEmployerDetails/AppointmentEmployerDetail.cs` | Aggregate root — IMultiTenant, 2 FKs |
| Domain | `src/.../Domain/AppointmentEmployerDetails/AppointmentEmployerDetailManager.cs` | DomainService — create/update |
| Domain | `src/.../Domain/AppointmentEmployerDetails/IAppointmentEmployerDetailRepository.cs` | Custom repo interface |
| Contracts | `src/.../Application.Contracts/AppointmentEmployerDetails/` | DTOs, service interface |
| Application | `src/.../Application/AppointmentEmployerDetails/AppointmentEmployerDetailsAppService.cs` | CRUD — mixed auth (some generic `[Authorize]`, some permission-gated) |
| HttpApi | `src/.../HttpApi/Controllers/AppointmentEmployerDetails/AppointmentEmployerDetailController.cs` | 8 endpoints at `api/app/appointment-employer-details` |

## Entity Shape

```
AppointmentEmployerDetail : FullAuditedAggregateRoot<Guid>, IMultiTenant
├── TenantId      : Guid?              (tenant isolation)
├── EmployerName  : string [max 255, required]
├── Occupation    : string [max 255, required]
├── PhoneNumber   : string? [max 12]
├── Street        : string? [max 255]
├── City          : string? [max 255]
├── ZipCode       : string? [max 10]
├── AppointmentId : Guid               (FK → Appointment, required)
└── StateId       : Guid?              (FK → State, optional)
```

## Relationships

| FK Property | Target Entity | Delete Behavior | Notes |
|---|---|---|---|
| `AppointmentId` | Appointment | NoAction | Required |
| `StateId` | State | SetNull | Optional. Host-scoped |

## Multi-tenancy

**IMultiTenant: Yes.** DbContext config outside `IsHostDatabase()` — both contexts.

## Permissions

```
CaseEvaluation.AppointmentEmployerDetails          (Default)
CaseEvaluation.AppointmentEmployerDetails.Create
CaseEvaluation.AppointmentEmployerDetails.Edit
CaseEvaluation.AppointmentEmployerDetails.Delete
```

**Gotcha:** CreateAsync and UpdateAsync use generic `[Authorize]` instead of Create/Edit permissions. Only DeleteAsync uses the specific permission.

## Business Rules

1. **EmployerName and Occupation are required** — the only two required string fields.

## Angular UI Surface

No Angular UI — this entity is managed via API only during the appointment booking flow.

## Known Gotchas

1. **No Angular UI** — managed via API during appointment booking
2. **Mixed auth** — Delete requires specific permission; Create/Update only require generic `[Authorize]`
3. **No tests**

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
