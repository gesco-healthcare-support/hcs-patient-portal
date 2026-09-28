# States -- per-office US state lookup

Thin single-field per-office lookup (`IMultiTenant`: each office has its own list in its own
database) with no feature-specific AppService complexity. Its non-obvious facts (inbound-FK delete
behavior, missing length constraint, the mapping call path) are documented once in the Domain layer
CLAUDE.md, under "Thin per-office lookups", which loads alongside this file -- kept there, not
duplicated here, to avoid per-file drift.

## Related

- src/HealthcareSupport.CaseEvaluation.Domain/CLAUDE.md (Thin per-office lookups)
