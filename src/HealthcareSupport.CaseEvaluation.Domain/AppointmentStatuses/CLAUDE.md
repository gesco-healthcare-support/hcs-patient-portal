# AppointmentStatuses -- per-office status-label lookup

Thin per-office lookup (`IMultiTenant`: each office has its own list in its own database).
IMPORTANT: this entity is NOT the appointment lifecycle state machine -- that is the
`AppointmentStatusType` enum in Domain.Shared; these rows are display-name metadata, disconnected
from the enum by design. Its remaining non-obvious facts are documented once in the Domain layer
CLAUDE.md, under "Thin per-office lookups", which loads alongside this file -- kept there, not
duplicated here, to avoid per-file drift.

Types here: `AppointmentStatus` (the entity), `AppointmentStatusManager` (domain service), `IAppointmentStatusRepository` (repository) and `AppointmentStatusDataSeedContributor` (seeds each office's own list).

## Related

- src/HealthcareSupport.CaseEvaluation.Domain/CLAUDE.md (Thin per-office lookups)
