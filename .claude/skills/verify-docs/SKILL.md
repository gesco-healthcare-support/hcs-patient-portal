---
name: verify-docs
description: Audits all docs/ files for accuracy (comparing claims against source code), fixes broken links, stale content, and navigation gaps, then reports what was fixed vs. what needs manual review
argument-hint: "[optional subfolder path, e.g. docs/features/appointments]"
---

# verify-docs

Verifies every doc in `docs/` against the actual codebase, fixes what can be
fixed automatically, and produces a categorized report of remaining issues.

**Scope:** Runs on all of `docs/` by default. If `$ARGUMENTS` is provided
(e.g. `docs/features/appointments` or `docs/backend`), restrict all steps
to files within that subtree only — but still check links that point outward.

---

## Step 1 — INVENTORY

### 1a. Discover all docs

Glob `docs/**/*.md` recursively. This inventory **must include** the
following subdirectories introduced by the documentation augmentation:
`docs/security/`, `docs/decisions/`, `docs/runbooks/`, `docs/repo-map/`,
and `docs/verification/`. All are Type B (no `Last synced from` header) --
they make verifiable claims about the codebase and must be spot-checked.

For each file record:
- Relative path from repo root
- File size (line count)
- Whether the first line matches `<!-- Last synced from ... on YYYY-MM-DD -->` — if yes, classify as **Type A** and extract:
  - `source_claude_md`: the path after "from" (e.g. `src/.../Domain/Appointments/CLAUDE.md`)
  - `last_synced`: the date
- Otherwise classify as **Type B**

### 1b. Build link map

For every markdown file found, scan for all markdown links using the pattern
`[...](target)`. For each link record:
- `source_file`: the file containing the link
- `link_text`: the display text
- `target_raw`: the raw href value
- `target_resolved`: the absolute path after resolving relative to `source_file`'s directory
- `exists`: whether the resolved target file exists on disk (ignore anchor fragments for existence check; just check the file portion)

### 1c. Classify link health

From the link map, produce three lists:

1. **Broken links** — `target_resolved` does not exist on disk
2. **Orphaned files** — files under `docs/` that have zero inbound links from any other `docs/` file
3. **Unreachable from INDEX** — files not reachable by following links transitively from `docs/INDEX.md` (breadth-first traversal of the link graph starting at INDEX.md)

Hold all of this in working memory. Do not write anything yet.

### 1d. Check Context Loading table completeness

Read the root CLAUDE.md's `## Context Loading` → `### Feature CLAUDE.md Index` table.
For each row:
- `feature_name`: the Feature column value
- `status`: "documented" if the CLAUDE.md column has a path, "not yet documented" otherwise
- `claude_md_exists`: whether the referenced CLAUDE.md file actually exists on disk
- `docs_link`: the docs/ column value
- `docs_exists`: whether the linked overview.md actually exists on disk

Also glob `src/HealthcareSupport.CaseEvaluation.Domain/*/` and check that every
business feature folder (excluding Identity, OpenIddict, Saas, Settings) has a row
in the table. Flag missing rows.

---

## Step 2 — VERIFY ACCURACY (Type A: Feature Docs)

For each Type A file (files with `<!-- Last synced ... -->` header):

### 2a. Check source CLAUDE.md exists

Read the `source_claude_md` path extracted in Step 1a.
- If the file does not exist: flag the doc as **UNVERIFIABLE** with reason "source CLAUDE.md missing at {path}" and skip to next file.

Staleness pre-check:
- Read the `<!-- Last synced from ... on YYYY-MM-DD -->` header from the doc file
- Compare that date to any date comment in the source CLAUDE.md that indicates when it was
  last regenerated (e.g., a SKILL-ASSESSMENT date, or the generate date in comments)
- If source CLAUDE.md was updated AFTER the last-synced date: flag this doc as **POTENTIALLY_STALE**
  and add a note in the report: "Source CLAUDE.md updated after last sync — full re-sync recommended"
- Still perform the full section comparison and spot-checks even if POTENTIALLY_STALE

### 2b. Section-by-section comparison

Parse both the source CLAUDE.md and the Type A doc into sections split on `## ` headings. For each section present in the CLAUDE.md:
- If the section does NOT exist in the doc → flag as **STALE** ("missing section: {heading}")
- If the section exists in both → compare content character-by-character (ignoring whitespace normalization). If they differ materially (not just whitespace), flag as **STALE** ("section '{heading}' differs from CLAUDE.md")

Skip sections that exist only in the doc (human additions) — these are fine.

Before flagging any section as MISSING or STALE:
1. Search the ENTIRE doc file (not just the corresponding section) for the concept or entity name
2. If the concept appears ANYWHERE in the file with correct information, do NOT flag it as missing
3. Only flag as STALE if: the specific section heading is absent AND the concept is absent
   from the whole file
4. Only flag as INACCURATE if: the value present in the file contradicts the source CLAUDE.md

This prevents false positives where a concept is documented under a different heading than expected.

### 2c. Spot-check 3 claims against source code

From the source CLAUDE.md, pick exactly 3 **specific, verifiable claims** — prioritize these types in order:
1. A class name or base class (e.g. "Appointment : FullAuditedAggregateRoot<Guid>") → Read the entity .cs file and verify the class declaration
2. A permission string (e.g. "CaseEvaluation.Appointments.Create") → Grep `CaseEvaluationPermissions.cs` for it
3. A field name and type (e.g. "ConfirmationNumber : string, max 20") → Read the entity file and check the property exists with that type
4. A relationship claim (e.g. "FK to Doctor via DoctorId") → Read the entity and verify the Guid property exists

For each claim, record:
- `claim`: what the CLAUDE.md says
- `source_file`: where you looked
- `result`: **VERIFIED** (matches), **INACCURATE** (contradicts source — record what the source actually says), or **UNVERIFIABLE** (source file not found)

### 2d. Cross-feature FK bidirectionality check

For the Type A file being verified, read its `## Relationships` section (or `## Inbound FKs`
section if present). For each FK relationship found (in either direction):
  a. Identify the related entity name and locate its CLAUDE.md
  b. Read the related CLAUDE.md's `## Relationships` or `## Inbound FKs` section
  c. Check: does the related CLAUDE.md acknowledge the same FK from the other direction?
  d. Also check: does the related `docs/features/` overview.md have a `## Related Features`
     link back to this feature?

Flag as MODERATE if a bidirectional FK is documented in only one direction.
Do NOT flag as MODERATE if:
  - The related entity has no CLAUDE.md yet (can't document what doesn't exist)
  - The FK is already present in a different section of the file — search the ENTIRE file
    before flagging (not just the expected section)
  - The relationship is described in prose but not in a formal table

### 2e. Overall verdict per Type A file

- **ACCURATE** — no stale sections AND all 3 spot-checks verified
- **STALE** — one or more sections differ from CLAUDE.md but spot-checks pass
- **INACCURATE** — one or more spot-checks found contradictions with source code
- **UNVERIFIABLE** — source CLAUDE.md missing

---

## Step 3 — VERIFY ACCURACY (Type B: Reference Docs)

For each Type B file:

### 3a. Extract testable claims

Read the file and extract every statement that makes a specific factual claim about the codebase. A testable claim is one that names:
- A file path (e.g. "located at `src/.../Domain/Appointments/Appointment.cs`")
- A class, interface, or method name (e.g. "extends `CaseEvaluationAppService`")
- A port number or URL (e.g. "AuthServer runs on port 44368")
- A command (e.g. "`dotnet ef migrations add`")
- An enum value (e.g. "`AppointmentStatusType.Pending`")
- A config key or connection string pattern
- A count (e.g. "15 entities", "13 states")
- A field name, type, or max-length constant

Do NOT extract subjective statements, descriptions of purpose, or architectural opinions.

### 3b. Verify claims (sample-based for large files)

- If the file has **10 or fewer** testable claims: verify all of them
- If the file has **more than 10**: verify the first 10 plus a random sample of 5 more (15 total max per file)

For each claim:
- **File path claims**: use Glob to check the path exists
- **Class/method/field claims**: use Grep to search for the name in the expected file or directory
- **Port/URL claims**: check against `appsettings.json`, `launchSettings.json`, or `angular.json` as appropriate
- **Enum value claims**: Grep `Domain.Shared/Enums/` for the enum type and check the value exists
- **Count claims**: count the actual items (e.g., glob entity files, grep enum values) and compare
- **Command claims**: check that referenced project paths exist (don't run the command)

Record each as: **VERIFIED**, **STALE** (value changed — record old and new), or **UNVERIFIABLE** (no source to check against)

### 3c. Overall verdict per Type B file

- **ACCURATE** — all checked claims verified
- **PARTIALLY_STALE** — some claims stale, none inaccurate
- **INACCURATE** — one or more claims contradict current source code
- **UNVERIFIABLE** — majority of claims could not be checked

---

## Step 4 — BUILD THE ISSUE REPORT

Organize all findings from Steps 1-3 into four categories. For each issue, record the fields shown:

### Category 1: Accuracy Issues

| Field | Content |
|-------|---------|
| file | path to the doc |
| section | heading or line number |
| claim | what the doc says |
| actual | what the source code says (or "source missing") |
| severity | **CRITICAL** if following the doc would cause a build error, runtime failure, or wrong behavior; **MODERATE** if stale but not dangerous; **MINOR** if cosmetic |

### Category 2: Navigation Gaps

| Field | Content |
|-------|---------|
| issue_type | `broken_link`, `orphaned_file`, `unreachable_from_index`, `missing_cross_link` |
| file | path to the affected doc |
| detail | the broken link target, or why the file is orphaned |
| severity | **MODERATE** for broken links and unreachable files; **MINOR** for missing cross-links |

### Category 3: Structural Issues

| Field | Content |
|-------|---------|
| file | path |
| issue | content in the wrong file, duplicate content across files |
| suggested_action | move to {file}, or deduplicate by keeping in {file} |
| severity | **MINOR** unless it causes contradictions (then **MODERATE**) |

### Category 4: Consistency Issues

| Field | Content |
|-------|---------|
| files | list of files with conflicting statements |
| concept | the term or concept described differently |
| variants | the different descriptions found |
| glossary_term | matching GLOSSARY.md entry if one exists (or "missing") |
| severity | **MODERATE** if conflicting facts; **MINOR** if just terminology drift |

### Print summary to user

Before making any changes, print:

```
## Docs Audit Summary

### Counts
- Files scanned: {n}
- Type A (code-synced): {n} — {n} accurate, {n} stale, {n} inaccurate
- Type B (reference): {n} — {n} accurate, {n} partially stale, {n} inaccurate

### Issues by severity
- CRITICAL: {n}
- MODERATE: {n}
- MINOR: {n}

### CRITICAL issues (must fix):
1. {file}: {claim} — actual: {actual}
2. ...

### Proceeding to fix {n} issues automatically. {n} issues flagged for manual review.
```

Wait for user acknowledgment before proceeding to Steps 5-6. If the user says to skip fixes, stop here and output the full report only.

---

## Step 5 — FIX NAVIGATION AND STRUCTURE

### 5a. Fix broken links

For each broken link found in Step 1c:
- Search `docs/` for a file whose name matches the target filename (case-insensitive)
- If exactly one match: update the link to point to the correct relative path
- If zero or multiple matches: add to the manual-review report; do not change

### 5b. Add missing INDEX.md entries

For each file under `docs/` not linked from `docs/INDEX.md`:
- Determine which section heading it belongs under based on its directory:
  - `docs/architecture/` → "### Architecture"
  - `docs/backend/` → "### Backend"
  - `docs/api/` → "### API"
  - `docs/database/` → "### Database"
  - `docs/frontend/` → "### Frontend"
  - `docs/business-domain/` → "### Business Domain"
  - `docs/devops/` → "### DevOps"
  - `docs/issues/` → "### Issues & Technical Debt"
  - `docs/features/*/` → "### Features" (create this section if missing)
- Add a link entry in the format: `- [{Title from H1}]({relative-path}) -- {first sentence of file}`
- If the file has no H1 heading, derive title from filename (e.g. `SCHEMA-REFERENCE.md` → "Schema Reference")

### 5c. Add feature cross-links

For each feature in `docs/features/`:
- Read the feature's overview.md `## Relationships` section
- For each FK relationship mentioned (e.g. "FK to Doctor via DoctorId"):
  - Check if `docs/features/{related-feature-kebab}/` exists
  - If yes AND the related feature's overview.md does not already link back: add a "Related features" bullet with a relative link
- Only add cross-links where a real FK relationship is documented — never infer

### 5d. Ensure back-links to INDEX.md

For each `docs/` file that does not contain a link to `INDEX.md` (or `../INDEX.md` etc.):
- Add at the bottom, before any `<!-- DOCS:MANUAL:START -->` block:
  ```
  ---
  [Back to Documentation Index](../INDEX.md)
  ```
  Adjust the relative path depth based on the file's location.

### 5e. Flag orphaned files

For files with zero inbound links AND not added to INDEX.md in step 5b:
- Add to the report as "potentially redundant — no inbound links"
- Do NOT delete

### 5f. Fix Context Loading table

For each issue found in Step 1d:
- If a feature now has a CLAUDE.md but the table row still says "(not yet documented)":
  read the CLAUDE.md's first line summary and update the row with the real summary and path
- If a feature has a `docs/features/` folder but the table has `—` in the docs/ column:
  add the overview.md link
- If a row references a CLAUDE.md that no longer exists: flag for manual review (do not remove the row)
- If a Domain feature folder exists but has no row in the table: add a row with "(not yet documented)"

---

## Step 6 — FIX ACCURACY ISSUES

### 6a. Fix stale Type A docs

For each Type A file flagged as STALE in Step 2:
- Re-read the source CLAUDE.md
- Apply the same section-by-section merge logic defined in `sync-feature-to-docs`:
  - Section in CLAUDE.md but not in doc → ADD
  - Section in doc but not in CLAUDE.md → KEEP (human-written)
  - Section in both → if CLAUDE.md is longer, UPDATE; if doc is longer, KEEP; if similar length, prefer CLAUDE.md
- Preserve all content between `<!-- DOCS:MANUAL:START -->` and `<!-- DOCS:MANUAL:END -->` markers verbatim
- Update the `<!-- Last synced ... -->` header with today's date

### 6b. Fix inaccurate claims in Type B docs

For each STALE or INACCURATE claim verified in Step 3:
- Read the source file to get the current correct value
- In the doc, find the exact sentence or code block containing the wrong claim
- Replace ONLY the incorrect value with the correct one
- Do NOT rewrite surrounding prose — change the minimum text needed
- After the corrected value, add an inline HTML comment: `<!-- verified against {source_file} on {YYYY-MM-DD} -->`

### 6c. Mark unverifiable claims

For each UNVERIFIABLE claim in Step 3:
- Add an inline HTML comment immediately after the claim: `<!-- UNVERIFIED: {reason} -->`
- Reasons include: "source file not found at {path}", "enum type not found", "config key not found in appsettings"

### 6d. Protected content

NEVER modify:
- Content between `<!-- DOCS:MANUAL:START -->` and `<!-- DOCS:MANUAL:END -->`
- Content between `<!-- MANUAL:START -->` and `<!-- MANUAL:END -->`
- Files in `angular/src/app/proxy/`
- Any `.cs`, `.ts`, `.json`, or other source code file — this skill only modifies `docs/**/*.md` files

---

## Step 7 — FINAL REPORT

Print to the user:

```
## Docs Rebuild Report

### Files modified ({n} total)
- {file}: {what changed — e.g. "2 broken links fixed, 1 stale section re-synced"}
- ...

### Issues fixed automatically ({n})
| Severity | Category | File | Detail |
|----------|----------|------|--------|
| ... | ... | ... | ... |

### Issues requiring manual review ({n})
| Severity | Category | File | Detail | Suggested action |
|----------|----------|------|--------|-----------------|
| ... | ... | ... | ... | ... |

### Potentially redundant files (not deleted)
- {file}: {reason — e.g. "zero inbound links, not referenced from INDEX.md"}

### Health score
docs/ health: {n} critical fixed, {n} critical need manual review, {m} moderate fixed, {k} minor fixed
```

---

## Constraints

- **NEVER delete any file** — only flag for manual review
- **NEVER rewrite human-authored prose** — only fix specific factual claims (swap the wrong value for the correct one)
- **NEVER modify source code files** — only `docs/**/*.md` files
- **NEVER touch `<!-- DOCS:MANUAL:START/END -->` or `<!-- MANUAL:START/END -->` blocks**
- **Type A source of truth**: Domain `CLAUDE.md` (which derives from source code). If `CLAUDE.md` and `overview.md` conflict, trust `CLAUDE.md`.
- **Type B source of truth**: actual source code / config files. If a doc and source conflict, trust source.
- **Cross-links between features**: only add when a real FK relationship or direct dependency is documented in the source CLAUDE.md `## Relationships` section
- **INDEX.md is the navigation hub**: every docs file must be reachable from it, organized under section headings (not a flat list)
- **Parallelism**: use parallel Agent calls or parallel tool calls wherever steps are independent (e.g., verifying different files in Steps 2-3)

<!-- SKILL-ASSESSMENT:
Date: 2026-04-03
Runs: 4 (Appointments baseline, ApplicantAttorneys, DoctorAvailabilities checkpoint, full docs/ pass)
Average score: 9/9 across scored runs

Score breakdown by dimension:
- Detection Accuracy: 3/3 — caught the Patient IMultiTenant error (CRITICAL); zero false positives on verified content
- Fix Quality: 3/3 — fixes were surgical (changed specific claims, not surrounding prose)
- Navigation Repair: 3/3 — all links verified, INDEX.md and Context Loading table validated

Times improved: 0 — scores stayed at 9/9 throughout
Known limitations:
1. Freshly-generated docs always pass verification (same session = same source = no drift). The real value comes when verifying docs that have aged and drifted from source code.
2. The "pause after Step 4" design worked well — user could review before fixes were applied. In practice with standing approvals, the pause was skipped. Consider making the pause optional via a --auto-fix flag.
3. Step 1d (Context Loading table check) worked perfectly — caught zero issues because the table was well-maintained. Untested on a table with actual staleness.
4. Cross-feature consistency is NOT in the verify skill's scope — it was done as a separate Phase 3.2 check. Consider adding a Step 2.5 that reads all CLAUDE.md Relationships sections and checks for bidirectional FK documentation.
5. Type B verification (reference docs) found 15/15 claims verified with zero issues — excellent result, but the sample was only 5 files with 3 claims each. A full audit of all 46 Type B files would need more context budget.

What percentage of issues found were real vs false positives?
- 3 real issues found (Patient IMultiTenant x2, sync dates x2 = technically 4 fixes)
- 1 false positive (Appointment CLAUDE.md "missing" AppointmentAccessor reference — it was actually documented)
- Real issue rate: ~75%

Most common issue category: Accuracy (factual errors from incorrect interface claims)

Did verify catch issues that generate/sync missed?
YES — the Patient IMultiTenant error originated in the generate phase. The exploration agent incorrectly reported Patient as implementing IMultiTenant, and the generate skill propagated this. Verify caught it by grepping the actual source file. This validates the cross-skill feedback loop: verify findings improved the generate output (not the generate skill itself, since the error was in the agent's exploration, not the skill instructions).
-->
