# States -- host-scoped US state lookup

Thin single-field host-scoped lookup with no feature-specific AppService complexity. Its
non-obvious facts (inbound-FK delete behavior, missing length constraint, a known mapper
deviation) are documented once in the Domain layer CLAUDE.md, under "Thin host-scoped lookups",
which loads alongside this file -- kept there, not duplicated here, to avoid per-file drift.

## Related

- src/HealthcareSupport.CaseEvaluation.Domain/CLAUDE.md (Thin host-scoped lookups)
