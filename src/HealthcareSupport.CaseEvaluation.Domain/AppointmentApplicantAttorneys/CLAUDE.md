# AppointmentApplicantAttorneys

Join entity linking an Appointment to an ApplicantAttorney and an IdentityUser. Created during the appointment booking flow when an attorney is associated with an appointment. No standalone UI; managed via the Appointments feature's attorney upsert endpoint.

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/.../Domain.Shared/AppointmentApplicantAttorneys/AppointmentApplicantAttorneyConsts.cs` | Default sort (Id asc) |
| Domain | `src/.../Domain/AppointmentApplicantAttorneys/AppointmentApplicantAttorney.cs` | Aggregate root — IMultiTenant, 3 required FKs |
| Domain | `src/.../Domain/AppointmentApplicantAttorneys/AppointmentApplicantAttorneyManager.cs` | DomainService — create/update |
| Domain | `src/.../Domain/AppointmentApplicantAttorneys/IAppointmentApplicantAttorneyRepository.cs` | Custom repo interface |
| Contracts | `src/.../Application.Contracts/AppointmentApplicantAttorneys/` | DTOs, service interface |
| Application | `src/.../Application/AppointmentApplicantAttorneys/AppointmentApplicantAttorneysAppService.cs` | CRUD with proper permission enforcement |
| HttpApi | `src/.../HttpApi/Controllers/AppointmentApplicantAttorneys/AppointmentApplicantAttorneyController.cs` | 9 endpoints at `api/app/appointment-applicant-attorneys` |

## Entity Shape

```
AppointmentApplicantAttorney : FullAuditedAggregateRoot<Guid>, IMultiTenant
├── TenantId            : Guid?  (tenant isolation)
├── AppointmentId       : Guid   (FK → Appointment, required)
├── ApplicantAttorneyId : Guid   (FK → ApplicantAttorney, required)
└── IdentityUserId      : Guid   (FK → IdentityUser, required)
```

## Relationships

| FK Property | Target Entity | Delete Behavior | Notes |
|---|---|---|---|
| `AppointmentId` | Appointment | NoAction | Required |
| `ApplicantAttorneyId` | ApplicantAttorney | NoAction | Required |
| `IdentityUserId` | IdentityUser | NoAction | Required |

## Multi-tenancy

**IMultiTenant: Yes.** DbContext config outside `IsHostDatabase()` — both contexts.

## Permissions

```
CaseEvaluation.AppointmentApplicantAttorneys          (Default — class-level auth)
CaseEvaluation.AppointmentApplicantAttorneys.Create
CaseEvaluation.AppointmentApplicantAttorneys.Edit
CaseEvaluation.AppointmentApplicantAttorneys.Delete
```

All properly enforced on AppService methods.

## Business Rules

Standard CRUD — no special business rules. Default sort is by `Id asc` (unusual — most entities sort by CreationTime desc).

## Angular UI Surface

No Angular UI — this entity is managed via API only during the appointment booking flow.

## Known Gotchas

1. **No Angular UI** — managed via API during appointment booking
2. **No tests**

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
