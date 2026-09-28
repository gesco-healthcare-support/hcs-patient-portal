# AppointmentTypes -- per-office IME-type lookup

Thin per-office lookup (`IMultiTenant`: each office has its own list in its own database). M2M with
Doctor; drives slot type-matching in the booking gate. Its non-obvious facts are documented once in
the Domain layer CLAUDE.md, under "Thin per-office lookups", which loads alongside this file -- kept
there, not duplicated here, to avoid per-file drift.

## Related

- src/HealthcareSupport.CaseEvaluation.Domain/CLAUDE.md (Thin per-office lookups)
