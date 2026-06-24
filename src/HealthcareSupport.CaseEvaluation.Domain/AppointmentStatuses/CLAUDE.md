# AppointmentStatuses -- host-scoped status-label lookup

Thin host-scoped lookup. IMPORTANT: this entity is NOT the appointment lifecycle state machine --
that is the `AppointmentStatusType` enum in Domain.Shared; these rows are display-name metadata,
disconnected from the enum by design. Its remaining non-obvious facts are documented once in the
Domain layer CLAUDE.md, under "Thin host-scoped lookups", which loads alongside this file -- kept
there, not duplicated here, to avoid per-file drift.

## Related

- src/HealthcareSupport.CaseEvaluation.Domain/CLAUDE.md (Thin host-scoped lookups)
