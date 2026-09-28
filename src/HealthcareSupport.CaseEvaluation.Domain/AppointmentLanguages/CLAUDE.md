# AppointmentLanguages -- per-office language lookup

Thin per-office lookup (`IMultiTenant`: each office has its own list in its own database). Its
non-obvious facts (dual-DbContext configuration, the per-office seed, the optional FK on Patient) are
documented once in the Domain layer CLAUDE.md, under "Thin per-office lookups", which loads alongside
this file -- kept there, not duplicated here, to avoid per-file drift.

## Related

- src/HealthcareSupport.CaseEvaluation.Domain/CLAUDE.md (Thin per-office lookups)
