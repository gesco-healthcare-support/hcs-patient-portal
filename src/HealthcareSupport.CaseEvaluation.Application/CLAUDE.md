# Application Layer

Application services (use cases) that orchestrate domain logic and expose DTOs to the HTTP API. Every feature under `Domain/` has a corresponding AppService here.

## What Lives Here

- **17 feature folders** that mirror `Domain/`: Appointments, Doctors, Patients, DoctorAvailabilities, ApplicantAttorneys, AppointmentAccessors, AppointmentApplicantAttorneys, AppointmentEmployerDetails, AppointmentLanguages, AppointmentStatuses, AppointmentTypes, Books, Locations, States, WcabOffices, ExternalSignups, Users
- **Cross-cutting files** at the project root:
  - `CaseEvaluationApplicationMappers.cs` -- all Riok.Mapperly mapper classes
  - `CaseEvaluationApplicationModule.cs` -- ABP module definition
  - `CaseEvaluationAppService.cs` -- base class for all AppServices in this project

## Conventions

1. **Extend `CaseEvaluationAppService`**, not `ApplicationService` directly. The base wires up localization and permission helpers.
2. **Always add `[RemoteService(IsEnabled = false)]`** to every AppService class. Without it, ABP auto-generates duplicate routes that clash with the manual controllers in `HttpApi/`.
3. **Use Riok.Mapperly, not AutoMapper.** Add a `partial class` decorated with `[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]` to `CaseEvaluationApplicationMappers.cs`. Do not call `ObjectMapper.Map<>` for new mappers -- see [ADR-001](../../docs/decisions/001-mapperly-over-automapper.md).
4. **Permissions are enforced here**, not in controllers. Apply `[Authorize(CaseEvaluationPermissions.{Entity}.Default)]` at class level and override with `.Create` / `.Edit` / `.Delete` on specific methods.
5. **Business rules belong in the domain layer.** AppServices orchestrate (validate input, map DTOs, call domain services / repositories, return DTOs). Invariants and multi-step business logic live in domain services like `AppointmentManager`.

## Key Files

| File | Purpose |
|------|---------|
| `CaseEvaluationAppService.cs` | Base class every AppService extends |
| `CaseEvaluationApplicationMappers.cs` | All Mapperly mapper classes in one file |
| `CaseEvaluationApplicationModule.cs` | ABP module registration, DI wiring |
| `ExternalSignups/` | Cross-cutting: multi-step tenant provisioning flow (not an entity CRUD feature) |
| `Users/` | Extends ABP Identity user flows (not generated from a Domain entity) |

## Related Docs

- [Root CLAUDE.md](../../CLAUDE.md) -- global ABP conventions and constraints
- [docs/backend/APPLICATION-SERVICES.md](../../docs/backend/APPLICATION-SERVICES.md)
- [docs/security/AUTHORIZATION.md](../../docs/security/AUTHORIZATION.md)
- [ADR-001: Mapperly over AutoMapper](../../docs/decisions/001-mapperly-over-automapper.md)
- [ADR-002: Manual controllers](../../docs/decisions/002-manual-controllers-not-auto.md)
