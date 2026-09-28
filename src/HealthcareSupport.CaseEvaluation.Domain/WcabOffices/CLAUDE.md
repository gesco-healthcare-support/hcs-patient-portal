# WcabOffices -- per-office WCAB office lookup

Thin per-office lookup (`IMultiTenant`: each office has its own list in its own database), with
Excel export via the download-token pattern. Its non-obvious facts are documented once in the Domain
layer CLAUDE.md, under "Thin per-office lookups", which loads alongside this file -- kept there, not
duplicated here, to avoid per-file drift.

## Related

- src/HealthcareSupport.CaseEvaluation.Domain/CLAUDE.md (Thin per-office lookups)
