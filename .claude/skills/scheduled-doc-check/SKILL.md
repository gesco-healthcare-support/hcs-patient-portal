---
name: scheduled-doc-check
description: "Run documentation health check across all features. Reports staleness, broken links, and sync status. Designed for /loop or scheduled tasks."
---

# scheduled-doc-check

Lightweight documentation health check. Reports which features have stale docs
and which navigation issues exist. Does NOT auto-fix — only reports.

Designed to be invoked weekly via `/loop 7d /scheduled-doc-check`, cloud scheduled
tasks, or manually when checking overall doc health.

---

## Step 1 — INVENTORY

1. Read `.claude/discovery/module-map.md` to get the full feature list
2. For each feature with ROI >= 3, check:
   - Does `src/HealthcareSupport.CaseEvaluation.Domain/{Feature}/CLAUDE.md` exist?
   - Does `docs/features/{feature-kebab}/overview.md` exist?
   - What is the `<!-- Last synced from ... on YYYY-MM-DD -->` date in overview.md?
3. Check repo-map freshness: read `docs/repo-map/index.json` and extract `generated_at`.
   If the file is missing or older than 30 days, flag as `STALE` in the report and
   suggest running `/generate-repo-map`.
4. Check that navigation for the augmented documentation is intact. Specifically,
   verify these directories exist and have at least one `.md` file each:
   `docs/security/`, `docs/decisions/`, `docs/runbooks/`, `docs/repo-map/`,
   `docs/verification/`. If any are missing or empty, flag as `GAP` in the report.

---

## Step 2 — STALENESS CHECK

For each feature with a CLAUDE.md:
1. Get the list of source files from the CLAUDE.md's File Map section
2. For each source file listed, check if it was modified after the last sync date:
   ```bash
   git log -1 --format="%ai" -- {file_path}
   ```
3. If ANY source file was modified after the last sync date: mark feature as **STALE**
4. If no sync date exists in docs (overview.md missing or no header): mark as **UNSYNCED**
5. If all source files are older than the sync date: mark as **CURRENT**

---

## Step 3 — NAVIGATION CHECK

Quick link health check (subset of verify-docs):
1. Read `docs/INDEX.md`
2. For each link in INDEX.md, verify the target file exists
3. Count broken links
4. Check that every `docs/features/*/overview.md` is linked from INDEX.md

---

## Step 4 — REPORT

Print:

```
Weekly Doc Health Check — Patient Portal — YYYY-MM-DD

Feature Documentation Status:
  Total features: {N}
  Documented (CLAUDE.md): {N}
  Synced to docs/: {N}
  Current: {N}
  Stale (code changed since sync): {N}
  Unsynced (no docs/ entry): {N}

{If stale features:}
Stale Features (code changed since last doc sync):
  - {Feature1}: last synced YYYY-MM-DD, code changed YYYY-MM-DD
  - {Feature2}: last synced YYYY-MM-DD, code changed YYYY-MM-DD

Navigation:
  INDEX.md links: {N} total, {N} broken
  Feature docs linked from INDEX: {N}/{N}

Recommended Actions:
  {If stale:}  /update-docs {feature1}
  {If stale:}  /update-docs {feature2}
  {If broken:} /verify-docs all
  {Always:}    /sync-to-vault
```

---

## Constraints

- **Read-only** — this skill does NOT modify any files
- **Lightweight** — uses git log and file existence checks, not deep content verification
- **For deep verification** — recommend running `/verify-docs all` instead
