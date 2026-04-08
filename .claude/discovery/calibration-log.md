# Calibration Log

## Calibration 1: States — Simple
Date: 2026-04-07
Session: 1 of 4

### Issues Found
- Previous CLAUDE.md was missing `IStateRepository`, `EfCoreStateRepository`, proxy files, individual Contracts files from File Map
- Missing `## Relationships` section (outbound: none, but should be explicit)
- Missing `## Business Rules` section
- Route was listed as `/states` but actual route is `/configurations/states` (from base routes)
- Self-contradicting gotcha #4 in first draft (caught during self-review, removed before presenting)

### Skill Changes Made
- generate-feature-doc: No SKILL.md changes needed — the skill instructions already cover all these cases. The issues were execution gaps in the prior manual run, not skill design gaps.

### Before/After
Before: 87 lines, missing 4 sections (Relationships, Business Rules, individual Contracts files, correct route)
After: 141 lines, all standardized sections present, correct route `/configurations/states`, host-only vs both-contexts distinction in Inbound FKs

### Verdict
Approved by human (manual verification confirmed all sections correct)

## Calibration 3: Appointments — Complex
Date: 2026-04-07
Session: 3 of 4

### Issues Found
- None. The existing 187-line CLAUDE.md was already the most comprehensive feature doc in the project.
- 3 spot-checks against source code all verified:
  - Entity declaration matches (FullAuditedAggregateRoot + IMultiTenant)
  - Auto-generated confirmation numbers verified in AppService
  - All 3 inbound FKs (AppointmentEmployerDetail, AppointmentAccessor, AppointmentApplicantAttorney) verified with NoAction delete

### Skill Changes Made
- generate-feature-doc: No changes needed — complex features with state machines, 5 FKs, 3 inbound FKs, 3 Angular pages, and business rules are already well-handled
- sync-feature-to-docs: No changes needed

### Before/After
No changes — existing CLAUDE.md was accurate

### Verdict
Approved (spot-check verification passed, no regeneration needed)

## Calibration 4: ExternalSignups — Unusual
Date: 2026-04-07
Session: 4 of 4

### Issues Found
- No existing CLAUDE.md — first-ever documentation for this module
- Non-standard feature: no Domain entity, no repository, no EF Core config
- Two controllers in different directories delegate to the same AppService
- Route deviates from convention: `api/public/external-signup` instead of `api/app/`
- Missing `[RemoteService(IsEnabled = false)]` on AppService
- Potentially unprotected endpoint (GetExternalUserLookupAsync)
- HIPAA concerns: public PII collection without rate limiting

### Skill Changes Made
- generate-feature-doc: No SKILL.md changes — but this calibration revealed that the skill's section structure needed adaptation for non-entity modules. The "Entity Shape" section was replaced with "Service Shape", and "Angular UI Surface" was replaced with "Angular Integration". These are content adaptations, not skill instruction changes — the skill already says "If a folder does not exist for a layer, note it."
- sync-feature-to-docs: Not applicable — ExternalSignups doesn't have a docs/features/ entry yet (cross-cutting module)

### Before/After
Before: No documentation existed
After: 123 lines with adapted section structure, 6 security-relevant gotchas, 6 business rules, and cross-references to Patients and Appointments

### Verdict
Approved by human

## Calibration 2: Locations — Medium
Date: 2026-04-07
Session: 2 of 4

### Issues Found
- Existing CLAUDE.md was already comprehensive (146 lines, all sections present)
- One factual correction: DoctorLocation join table DbContext placement was inaccurate (said "outside IsHostDatabase" but it's inside in CaseEvaluationDbContext, and at top level in TenantDbContext)
- No skill design gaps — the existing skill instructions already covered M2M relationships, inbound FKs, bulk operations, and lookup filtering

### Skill Changes Made
- generate-feature-doc: No changes needed — the skill correctly handles medium-complexity features with M2M joins, multiple FKs, and bulk operations
- sync-feature-to-docs: No changes needed

### Before/After
Before: 146 lines, one incorrect Multi-tenancy claim about DoctorLocation placement
After: 146 lines, corrected DoctorLocation dual-context description

### Verdict
Approved by human (manual verification confirmed all sections correct)