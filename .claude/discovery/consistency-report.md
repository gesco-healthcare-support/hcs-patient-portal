# Consistency Verification Report
Project: Patient Appointment Portal
Date: 2026-04-08

## Summary
| Severity | Count |
|----------|-------|
| CRITICAL | 0 |
| MODERATE | 1 |
| MINOR | 2 |
| Total checks | 47 |
| Checks passed | 44 |
| Health score | 94% |

## CRITICAL Issues
None.

## MODERATE Issues

### 1. Sync dates stale on 13 feature docs
- **Location:** `docs/features/*/overview.md` (all except states and locations)
- **What:** 13 overview.md files have `Last synced ... on 2026-04-03` but the CLAUDE.md files were verified accurate on 2026-04-07. Content is correct but the sync dates suggest they are outdated.
- **Impact:** Low — content is verified accurate, only the date header is stale
- **Fix:** Update sync dates to 2026-04-08 (safe auto-fix since content hasn't changed)

## MINOR Issues

### 1. ExternalSignups not in root CLAUDE.md Context Loading table
- **Location:** Root `CLAUDE.md` Context Loading table
- **What:** ExternalSignups has a CLAUDE.md at `src/.../Application/ExternalSignups/CLAUDE.md` but is not listed in the Context Loading table because the table only covers Domain/ features
- **Impact:** Very low — ExternalSignups is a cross-cutting module, not a standard entity feature
- **Fix:** Add a note below the table mentioning ExternalSignups (manual, not auto-fix — affects root CLAUDE.md)

### 2. Patients CLAUDE.md File Map row mentions "IMultiTenant" inconsistently
- **Location:** `src/.../Domain/Patients/CLAUDE.md` line 12
- **What:** File Map says "IMultiTenant" but Entity Shape correctly says "NO IMultiTenant — has TenantId property but not the interface". The authoritative sections (Entity Shape, Multi-tenancy) are correct.
- **Impact:** Cosmetic — could confuse someone scanning only the File Map
- **Fix:** Update File Map row to say "has TenantId but NOT IMultiTenant" (safe auto-fix)

## Auto-Fixed Issues
None applied yet. Safe to auto-fix:
1. Update 13 sync dates in docs/features/*/overview.md
2. Fix Patients CLAUDE.md File Map IMultiTenant inconsistency

## Spot-Check Results
| Category | Checks | Verified | Inaccurate | Unverifiable |
|----------|--------|----------|------------|--------------|
| Architecture (file paths) | 5 | 5 | 0 | 0 |
| API (endpoint routes) | 10 | 10 | 0 | 0 |
| Features (entity classes) | 16 | 16 | 0 | 0 |
| Database (config) | 3 | 3 | 0 | 0 |
| Onboarding (ports) | 3 | 3 | 0 | 0 |
| Permissions | 5 | 5 | 0 | 0 |
| Cross-references (links) | 5 | 5 | 0 | 0 |
| **Total** | **47** | **47** | **0** | **0** |

## Link Health
- Total markdown links checked: 78 docs files scanned
- Broken links: 0
- Orphaned files: 0
- INDEX.md coverage: 15/15 features listed

## Layer Consistency
| Check | Result |
|-------|--------|
| Feature CLAUDE.md → docs/ (15 features) | All present |
| docs/ → INDEX.md entries | All present |
| Root CLAUDE.md table → CLAUDE.md files | 15/16 match (ExternalSignups excluded — see MINOR #1) |
| Root CLAUDE.md line count | 193 (under 200 limit) |
