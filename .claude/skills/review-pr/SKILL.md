---
name: review-pr
description: "Review a pull request for code quality, documentation completeness, and HIPAA compliance. Designed for team PR workflow."
argument-hint: "<PR number> or 'current' for current branch diff"
---

# review-pr

Reviews a pull request against the project's conventions, documentation requirements,
and HIPAA compliance standards.

---

## Step 1 — GET THE DIFF

Parse `$ARGUMENTS`:

- If a **PR number** (e.g. `42`): run `gh pr diff 42`
- If **`current`** or no argument: detect the base branch and diff against it:
  ```bash
  BASE=$(git symbolic-ref refs/remotes/origin/HEAD 2>/dev/null | sed 's@refs/remotes/origin/@@' || echo "develop")
  git diff $BASE...HEAD
  ```
- If a **branch name**: `git diff main...$ARGUMENTS`

Also get the PR title and description if available:
```bash
gh pr view [number] --json title,body,files 2>/dev/null
```

---

## Step 2 — CLASSIFY CHANGES

For each changed file in the diff, identify:

1. **Feature name:** Map the file path to a feature:
   - `src/.../Domain/Appointments/...` → Appointments
   - `src/.../Application/Doctors/...` → Doctors
   - `src/.../HttpApi/Controllers/Locations/...` → Locations
   - `angular/src/app/appointments/...` → Appointments
   - `angular/src/app/proxy/...` → (auto-generated — flag if manually edited)
   - `src/.../EntityFrameworkCore/Migrations/...` → (migration — note which entities)
   - `src/.../Domain.Shared/Enums/...` → (shared — identify which feature uses it)
   - Cross-cutting files → note as "cross-cutting"

2. **Change type:** new file | modified | deleted | renamed

3. **Layer:** Domain.Shared | Domain | Contracts | Application | EF Core | HttpApi | Angular | Config | Test | Docs

---

## Step 3 — CODE QUALITY CHECK

Read `.claude/discovery/conventions.md` and check the diff against each convention:

### Naming
- [ ] Entity names are PascalCase singular
- [ ] DTO names follow `{Entity}CreateDto` / `{Entity}UpdateDto` / `{Entity}Dto` pattern
- [ ] AppService extends `CaseEvaluationAppService`, not `ApplicationService`
- [ ] Controller route follows `[Route("api/app/{entity-plural}")]`
- [ ] No `CreateUpdate{Entity}Dto` pattern used

### Architecture
- [ ] AppService has `[RemoteService(IsEnabled = false)]` attribute
- [ ] Controller manually delegates to AppService (not auto-wired)
- [ ] Mapper uses Riok.Mapperly `[Mapper]` pattern, NOT AutoMapper
- [ ] If entity has a DomainManager: AppService delegates to it for create/update
- [ ] No proxy files (`angular/src/app/proxy/`) manually edited
- [ ] No `ng serve` or `yarn start` in any script or config

### Multi-tenancy
- [ ] New entity correctly uses or omits `IMultiTenant` based on scoping rules
- [ ] Host-scoped entities configured inside `if (builder.IsHostDatabase())` guard
- [ ] No manual `WHERE TenantId = X` queries (ABP handles this automatically)

### EF Core
- [ ] New migration included if entity shape changed
- [ ] Both `CaseEvaluationDbContext` and `CaseEvaluationTenantDbContext` updated if needed
- [ ] FK delete behaviors explicitly set (NoAction for most, SetNull for optional)

---

## Step 4 — DOCUMENTATION CHECK

For each feature affected by the PR:

1. **Feature CLAUDE.md:** Does the feature's `src/.../Domain/{Feature}/CLAUDE.md` exist?
   - If the PR adds new fields, methods, or relationships: is the CLAUDE.md updated?
   - If not updated: flag as WARN with suggestion to run `/generate-feature-doc {Feature}`

2. **docs/ sync:** If the feature CLAUDE.md was updated, was `docs/features/{feature-kebab}/overview.md` also updated?
   - If not: flag as WARN with suggestion to run `/sync-feature-to-docs {Feature}`

3. **Root CLAUDE.md:** If a new feature was added, is it in the Context Loading table?

4. **Localization:** If new permission or UI text was added, are the localization JSON files updated?
   - Check `src/.../Domain.Shared/Localization/CaseEvaluation/en.json`

---

## Step 5 — HIPAA CHECK

Scan the diff for PHI compliance:

1. **No real patient data:** Search for patterns that look like real names, SSNs (###-##-####),
   dates of birth, medical record numbers, real email addresses, real phone numbers
   - Synthetic data patterns are OK: hex strings, `@example.com`, `555-xxxx`

2. **No PHI logging:** Check for `Console.Write`, `_logger.Log`, `Serilog` calls that could
   capture request/response bodies containing patient data

3. **Test data:** If test files are modified, verify test data uses synthetic values
   (random hex strings like existing Doctor tests, not realistic names)

4. **DTO exposure:** If new DTOs are added, check they don't expose more PHI than needed
   (e.g., a lookup DTO shouldn't include patient address)

5. **Access control:** If new endpoints are added, verify they have `[Authorize]` with
   appropriate permission attributes

6. **Security documentation impact:** If the PR adds a new entity with PHI fields, modifies
   multi-tenancy scope, or changes an authorization gate, flag that the following may need
   updates:
   - `docs/security/DATA-FLOWS.md` -- add entity to PHI inventory
   - `docs/security/AUTHORIZATION.md` -- add new permissions to the matrix
   - `docs/security/THREAT-MODEL.md` -- update STRIDE table for affected component
   If the PR directly edits files in `docs/security/`, spot-check two specific claims
   against source code (read the referenced .cs / .ts file and verify the claim holds).

---

## Step 6 — OUTPUT REVIEW

```markdown
# PR Review: {title or branch name}

## Summary
{2-3 sentences: what this PR does, which features it affects}

## Files Changed
{N} files across {N} features: {list feature names}

## Code Quality
| Check | Status | Notes |
|-------|--------|-------|
| Naming conventions | PASS/WARN/FAIL | {details} |
| AppService decoration | PASS/WARN/FAIL | {[RemoteService] present?} |
| Controller delegation | PASS/WARN/FAIL | {manual delegation correct?} |
| Mapper pattern | PASS/WARN/FAIL | {Riok.Mapperly, not AutoMapper?} |
| Multi-tenancy | PASS/WARN/FAIL | {IMultiTenant correct? IsHostDatabase guard?} |
| EF Core migration | PASS/WARN/FAIL | {migration included if entity changed?} |
| Proxy files untouched | PASS/WARN/FAIL | {no manual proxy edits?} |

## Documentation
| Check | Status | Notes |
|-------|--------|-------|
| Feature CLAUDE.md updated | PASS/WARN/N/A | {which features} |
| docs/ synced | PASS/WARN/N/A | {details} |
| Root CLAUDE.md current | PASS/WARN/N/A | {Context Loading table} |
| Localization updated | PASS/WARN/N/A | {if new permissions/text} |

## HIPAA Compliance
| Check | Status | Notes |
|-------|--------|-------|
| No real PHI in diff | PASS/FAIL | {details if FAIL — STOP and flag} |
| Logging safe | PASS/WARN | {no PHI logging?} |
| Test data synthetic | PASS/FAIL | {details if FAIL} |
| DTO exposure minimal | PASS/WARN/N/A | {no over-exposure?} |
| Access control | PASS/WARN/N/A | {[Authorize] on new endpoints?} |

## Verdict: {APPROVE / REQUEST CHANGES}

{If APPROVE:}
No blocking issues found. Consider the WARN items before merging.

{If REQUEST CHANGES:}
### Required Changes
1. {specific change needed with file path}
2. {specific change needed with file path}

### Suggested Improvements (non-blocking)
1. {optional improvement}
```

---

## Constraints

- **HIPAA FAIL is always a blocker** — any real PHI in the diff means REQUEST CHANGES
- **Missing CLAUDE.md update is WARN, not FAIL** — documentation can be updated after merge
- **Never approve manual proxy file edits** — always flag as FAIL
- **Check the feature's Known Gotchas** — if the PR touches a gotcha area, verify the fix
  doesn't reintroduce the issue
- **Read-only** — this skill does not modify any files, only reports findings
