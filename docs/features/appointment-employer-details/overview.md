<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/AppointmentEmployerDetails/CLAUDE.md on 2026-04-08 -->

# Appointment Employer Details

Employer information associated with an appointment -- the patient's employer at the time of the workers' comp claim. One employer detail record per appointment. Created during the appointment booking flow. No standalone UI.

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

**IMultiTenant: Yes.** DbContext config outside `IsHostDatabase()` -- both contexts.

## Permissions

```
CaseEvaluation.AppointmentEmployerDetails          (Default)
CaseEvaluation.AppointmentEmployerDetails.Create
CaseEvaluation.AppointmentEmployerDetails.Edit
CaseEvaluation.AppointmentEmployerDetails.Delete
```

**Note:** CreateAsync and UpdateAsync use generic `[Authorize]` instead of Create/Edit permissions. Only DeleteAsync uses the specific permission.

## Business Rules

1. **EmployerName and Occupation are required** -- the only two required string fields.

## Known Gotchas

1. **No Angular UI** -- managed via API during appointment booking
2. **Mixed auth** -- Delete requires specific permission; Create/Update only require generic `[Authorize]`
3. **No tests**

## Related Features

- [Appointments](../appointments/overview.md) -- `AppointmentId` FK references Appointment
- [States](../states/overview.md) -- `StateId` FK references State (optional, SetNull)

## Links

- Feature CLAUDE.md: `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentEmployerDetails/CLAUDE.md`
- Root architecture: [CLAUDE.md](../../../CLAUDE.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
