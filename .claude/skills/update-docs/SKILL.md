---
name: update-docs
description: "Workflow: regenerate feature CLAUDE.md, sync to docs/, verify accuracy, and sync to vault. Use after code changes to keep all documentation layers current."
argument-hint: "<FeatureName> or 'all' or 'modified'"
---

# update-docs

End-to-end documentation update workflow. Orchestrates generate-feature-doc,
sync-feature-to-docs, verify-docs, and sync-to-vault in the correct order.

---

## Step 1 -- PARSE ARGUMENT

Given `$ARGUMENTS`, determine scope:

- **Specific feature name** (e.g. `Appointments`, `DoctorAvailabilities`):
  Process only that feature. The name must be PascalCase matching a Domain folder.

- **`all`**:
  Process every feature listed in `.claude/discovery/module-map.md` with ROI >= 3.
  Read the module map and extract feature names from the table.
  If `.claude/discovery/module-map.md` does not exist, glob
  `src/HealthcareSupport.CaseEvaluation.Domain/*/CLAUDE.md` and use the parent
  folder names as the feature list.

- **`modified`**:
  Use `git diff --name-only $(git merge-base HEAD origin/main)..HEAD` to find
  all changed source files on the current branch since it diverged from
  `origin/main` (captures every commit on the branch, not just the last one).
  Map each changed file to its feature by extracting the feature folder name:
  - `src/.../Domain/Appointments/...` -> `Appointments`
  - `src/.../Application/Doctors/...` -> `Doctors`
  - `src/.../HttpApi/Controllers/Locations/...` -> `Locations`
  - `angular/src/app/appointments/...` -> `Appointments` (kebab -> PascalCase)
  - `angular/src/app/doctor-availabilities/...` -> `DoctorAvailabilities`
  Deduplicate the list. If a cross-cutting file changed (e.g. `CaseEvaluationApplicationMappers.cs`,
  `CaseEvaluationPermissions.cs`, `CaseEvaluationDbContext.cs`), process ALL features.

- **No argument provided**:
  Ask the user which mode to use:
  > "Which features should I update documentation for?
  > - A specific feature name (e.g. `Appointments`)
  > - `all` -- every documented feature
  > - `modified` -- only features with code changes since last commit"

---

## Step 2 -- GENERATE

For each feature in scope:

1. Invoke the `generate-feature-doc` skill via the Skill tool with argument `{Feature}`.
   - This reads all source code files and regenerates the feature's CLAUDE.md
   - Wait for completion before proceeding to sync

2. Invoke the `sync-feature-to-docs` skill via the Skill tool with argument `{Feature}`.
   - This syncs the CLAUDE.md content into `docs/features/{feature-kebab}/`
   - Uses section-by-section merge to preserve human-written prose in docs

3. After each successful feature, suggest a commit but do NOT auto-commit. The user
   decides whether to checkpoint (a mid-process failure across 15 features otherwise
   leaves a half-updated tree with no rollback point).

If processing multiple features, run them **sequentially** (not in parallel) to avoid
file conflicts in cross-cutting files like `CLAUDE.md` root table updates.

Print progress:
```
[1/N] Generating CLAUDE.md for {Feature}...
[1/N] Syncing to docs/features/{feature-kebab}/...
[2/N] Generating CLAUDE.md for {Feature}...
...
```

---

## Step 3 -- VERIFY

After ALL features have been generated and synced:

1. Invoke the `verify-docs` skill via the Skill tool with no arguments.
   - This audits docs/ for accuracy, broken links, and navigation gaps
   - It prints a summary with CRITICAL/MODERATE/MINOR counts

2. If verify reports **CRITICAL** issues:
   - Print the CRITICAL issues clearly
   - Ask the user: "Critical documentation issues found. Fix them now, or continue?"
   - If "fix": the verify skill's auto-fix will handle navigation issues;
     for accuracy issues, re-invoke `generate-feature-doc` on the affected feature

3. If verify reports only MODERATE or MINOR:
   - Print the summary but continue automatically

---

## Step 4 -- VAULT SYNC

Check if the Obsidian MCP tools are available by looking for `obsidian_read_note`
in the available tools.

- If available: Invoke the `sync-to-vault` skill via the Skill tool with no arguments.
- If not available: Print "Vault sync skipped -- Obsidian MCP not connected"

---

## Step 5 -- REPORT

Print a completion summary:

```
Documentation update complete.

Features processed: {N}
  {Feature1}: CLAUDE.md regenerated, docs/ synced
  {Feature2}: CLAUDE.md regenerated, docs/ synced
  ...

CLAUDE.md files: {N} generated/updated
docs/ files: {N} created, {N} updated, {N} preserved (human-written)

Verification:
  CRITICAL: {N}
  MODERATE: {N}
  MINOR: {N}

Vault: {synced / skipped / unavailable}

{If any issues remain:}
Remaining issues (run /verify-docs for details):
  - {file}: {issue summary}
```

---

## Constraints

- **Sequential feature processing** -- generate + sync one feature at a time to avoid
  conflicts in shared files (root CLAUDE.md table, CaseEvaluationApplicationMappers.cs notes)
- **Never skip verify** -- always run verification after all features are processed
- **Preserve human prose** -- the sync step handles this via section-by-section merge;
  this workflow does NOT override that behavior
- **HIPAA** -- if any generated CLAUDE.md contains what looks like real patient data
  (not synthetic test data), STOP and alert immediately
