---
name: verify-structure
description: Run structural verification on the documentation system. Checks file existence, CLAUDE.md coverage, link validity, and cross-reference consistency. Use before releases, after restructures, or when the user reports missing navigation.
argument-hint: (none)
---

# verify-structure

Runs the two automated structural checks and reports results. This skill does
not modify documentation -- it reports what needs attention.

## Step 1 -- Run structural checks

```bash
python .claude/scripts/verify_structure.py
```

The script prints `PASS`, `WARN`, `FAIL` lines and a summary. Exit code 0 if no
FAILs, exit 1 if any FAIL.

## Step 2 -- Run link checks

```bash
python .claude/scripts/check-links.py
```

Scans all Markdown files under `docs/` and `.claude/` (plus all CLAUDE.md) for
broken relative links. Template placeholders (`{foo}`), external links, and
anchor-only references are skipped.

## Step 3 -- Triage failures

For each `FAIL` from verify_structure:

- Missing required file -- check whether the file was recently renamed or moved.
  Suggest the exact Write operation to create it.
- Feature missing CLAUDE.md -- suggest `/generate-feature-doc {FeatureName}`.
- Layer CLAUDE.md missing -- check the plan in `C:\Users\RajeevG\.claude\plans\`
  for the expected content.

For each `FAIL` from check-links:

- Pre-existing broken links (e.g. DOCKER-AND-DEPLOYMENT.md) -- leave unchanged
  unless scope allows; add to `docs/verification/BASELINE.md` open-gaps list.
- Broken links introduced by recent edits -- fix immediately by correcting the
  relative path.
- Forward references to files that do not yet exist -- confirm with user before
  creating a placeholder.

## Step 4 -- Update BASELINE.md

After triage, update `docs/verification/BASELINE.md`:

- Set `Last verified` to today's date
- Update the structural check summary row with the current PASS/WARN/FAIL counts
- Update the link health summary row with the current validated/failures counts
- Add any new `OPEN` gaps to the "Known Gaps" table

## Step 5 -- Report to user

Produce a summary under 150 words:

- Overall structural status (healthy / needs attention)
- Counts: PASS, WARN, FAIL from verify_structure; links validated / failures
  from check-links
- Top 3 things needing attention, with suggested fix for each
- Link to the updated BASELINE.md

Do not re-print script output verbatim. Summarize.
