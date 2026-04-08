<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/AppointmentApplicantAttorneys/CLAUDE.md on 2026-04-08 -->

# Appointment Applicant Attorneys

Join entity linking an Appointment to an ApplicantAttorney and an IdentityUser. Created during the appointment booking flow when an attorney is associated with an appointment. No standalone UI; managed via the Appointments feature's attorney upsert endpoint.

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

**IMultiTenant: Yes.** DbContext config outside `IsHostDatabase()` -- both contexts.

## Permissions

```
CaseEvaluation.AppointmentApplicantAttorneys          (Default — class-level auth)
CaseEvaluation.AppointmentApplicantAttorneys.Create
CaseEvaluation.AppointmentApplicantAttorneys.Edit
CaseEvaluation.AppointmentApplicantAttorneys.Delete
```

All properly enforced on AppService methods.

## Business Rules

1. **Default sort is by `Id asc`** -- unusual; most entities sort by CreationTime desc.

## Known Gotchas

1. **No Angular UI** -- managed via API during appointment booking
2. **No tests**

## Related Features

- [Appointments](../appointments/overview.md) -- `AppointmentId` FK references Appointment
- [Applicant Attorneys](../applicant-attorneys/overview.md) -- `ApplicantAttorneyId` FK references ApplicantAttorney

## Links

- Feature CLAUDE.md: `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentApplicantAttorneys/CLAUDE.md`
- Root architecture: [CLAUDE.md](../../../CLAUDE.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
