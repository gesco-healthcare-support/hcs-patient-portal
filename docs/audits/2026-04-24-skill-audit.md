# Doc-Maintenance Skill Audit -- 2026-04-24

## Context

This audit reviews the four doc-maintenance skills in `.claude/skills/` (`generate-feature-doc`, `sync-feature-to-docs`, `update-docs`, `verify-docs`) ahead of PR-3, in which the improved skills will be run against all 15 entity-level CLAUDE.md files. PR-2's job is to apply every actionable change identified here so that PR-3 produces high-fidelity output across MEDIUM/SIMPLE/COMPLEX features and across host-scoped vs tenant-scoped entities. The findings below are scoped to skill-body content only -- they do not propose refactors to the skills' YAML frontmatter, file layout, or invocation pattern. The aggregate task list at the bottom is the canonical input for PR-2's plan file.

## Summary table

| Skill | Lines | High-priority issues | Medium issues | Low issues |
| --- | --- | --- | --- | --- |
| generate-feature-doc | 378 | 4 | 5 | 3 |
| sync-feature-to-docs | 268 | 3 | 4 | 2 |
| update-docs | 136 | 2 | 3 | 2 |
| verify-docs | 426 | 3 | 5 | 2 |

## Per-skill findings

### generate-feature-doc (.claude/skills/generate-feature-doc/SKILL.md)

#### Purpose (1 sentence)
Reads every layer for one feature (Domain.Shared, Domain, Application.Contracts, Application, EFCore, HttpApi, Angular) and writes a feature-level CLAUDE.md at `src/.../Domain/{Feature}/CLAUDE.md` covering file map, entity shape, mapper config, permissions, business rules, Angular UI, inbound FKs, and known gotchas.

#### High-priority issues (PR-2 must fix)

- **ASCII violations throughout the skill body** (lines 15, 19, 24-26, 33-34, 36, 38-40, 50-52, 65, 75, 95, 119, 192, 205, 230, 246-249, 259-267, 273, 280-282, 285-287, 294-300, 304-310, 315-321, 333). The skill uses em dashes (`--` written as the U+2014 character) and Unicode right-arrows (`->` written as U+2192) in instructions, the output template, and example state diagrams. This propagates downstream: every CLAUDE.md generated from this template inherits non-ASCII characters, which violates `code-standards.md` (ASCII-only) and triggers the PHI/style scanner. Fix: search-and-replace U+2014 with the two-character ASCII string `--` and U+2192 with the two-character ASCII string `->` across the entire skill file. The search-and-replace must cover both prose and the fenced code block at lines 213-322 because that template is copied verbatim into entity CLAUDE.md files.
- **Reference Pattern hyperlink fragment is wrong** (line 316). The Markdown link `[CLAUDE.md#reference-pattern--appointments]` uses two consecutive hyphens to encode the em-dash from a heading like "Reference Pattern -- Appointments". GitHub's slug algorithm collapses repeated separators, so the resolved fragment becomes `#reference-pattern-appointments` (single hyphen) and the link 404s. Fix: change the fragment to `#reference-pattern-appointments` (single hyphen between `pattern` and `appointments`). Verify against the actual root CLAUDE.md heading after fixing its ASCII violations.
- **Step 5 root-index update assumes existing row format** (lines 326-340). The instruction to "find the row for `{Feature}` in the table (it should already exist with `(not yet documented)`)" silently skips the case where the table uses a different placeholder string (e.g. "TBD", "pending"), the row is misspelled, or the table column count changes. PR-3 will hit features whose table state is inconsistent. Fix: replace the bullet with explicit fallback logic: "Search the table for a row whose Feature column matches `{Feature}` exactly. If no exact match exists, search for a case-insensitive match and warn. If still no match, insert a new row in alphabetical order. Never silently create a duplicate row."
- **Angular UI section assumes ABP Suite abstract/concrete pattern is the only valid pattern** (lines 281-282, 283-296). The skill labels deviations as "custom" but provides no instruction for what to capture when the feature uses inline standalone components, dialog-only flows, or Angular Material primitives without ABP UI. For 2026-vintage Angular 20 standalone components (which this codebase is on), the abstract/concrete pattern is the legacy ABP Suite scaffold, not the default. Fix: split the Angular UI Surface section into two sub-paths -- "ABP Suite scaffold (abstract/concrete)" and "Standalone Angular component" -- and instruct the runner to detect which by checking the component file for `standalone: true` in the `@Component` decorator.

#### Medium-priority issues (PR-2 should fix)

- **Step 1 glob list is missing `Domain.Shared/Localization/`** (lines 23-35). Permissions and business-rule labels live under `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`, and several features have feature-specific keys there. Add a glob for the localization JSON so the runner pulls user-facing string keys into the doc.
- **Step 2 base-class enumeration omits `BasicAggregateRoot<Guid>` and `Entity<Guid>`** (line 76). Some lookup entities (and any entity that intentionally opts out of audit fields) extend lighter base classes. Listing only `FullAuditedAggregateRoot<Guid>`, `FullAuditedEntity<Guid>`, etc. encourages the runner to misclassify. Add `BasicAggregateRoot<Guid>` and `Entity<Guid>` to the list, and add a fallback instruction: "If the base class is none of the above, record the literal class declaration verbatim."
- **AfterMap detection regex is implicit, not specified** (lines 115-121). The instruction says "search for AfterMap in the same class body" without telling the runner how to scope `same class body` (Riok.Mapperly classes are `partial` and may span multiple files). Fix: specify the search procedure: "Grep for `class {Entity}To{Dto}Mapper` in `src/HealthcareSupport.CaseEvaluation.Application/`. For each file containing that class, read the entire `partial class` body (from the opening brace to the matching closing brace) and look for `protected override void AfterMap`."
- **Inbound FK regex not robust to formatting** (lines 164-167). The pattern `HasForeignKey\(x => x\.{EntitySingular}Id\b` assumes the lambda parameter is always `x` and the spacing is exact. EF configurations regularly use `e =>`, `b =>`, `entity =>`, multi-line lambdas, and `nameof(...)` style. Fix: replace the regex with a two-pass search: first grep for `HasForeignKey` lines, then for each match, parse the property name with a more permissive pattern like `HasForeignKey\([^)]*\.{EntitySingular}Id\b`.
- **No HIPAA / synthetic-data guard in the output template** (lines 213-322). The template never tells the runner not to copy real user-facing strings, sample data, or seed data into the generated CLAUDE.md. Some entities (Patients, Doctors) have seed data in `*DataSeedContributor.cs` files that may include test names that look real. Fix: add a `## Step 0 -- Safety check` before Step 1 that says: "Do not copy any string literal that looks like a real person's name, SSN, DOB, address, or phone. Use the placeholder `<example>` instead. If unsure, omit the value."

#### Low-priority issues (PR-2 may defer)

- **"max 10-word summary" in Step 5 is unenforceable** (line 335). No guidance on how to truncate; runner produces inconsistent lengths. Soften to "one-line summary, target 8-12 words."
- **File Map row-count guidance is ambiguous for Angular** (lines 222-225). "Group by component" vs "list abstract components and services individually" is not a clear rule. Worth a one-sentence clarification with an example.
- **SKILL-ASSESSMENT block is dated 2026-04-03** (line 357). The block is HTML-commented so it does not affect output, but PR-2 can refresh the date and findings to 2026-04-24 to keep the audit trail accurate.

#### Open questions (need Adrian or future session)

- Should the generated CLAUDE.md include a `## Localization Keys` section when `en.json` has feature-specific keys? Pro: completeness; con: line-count bloat against the 400-line cap.
- Standalone-component detection: does the codebase have any current standalone Angular 20 components, or is everything still on the ABP Suite scaffold? Affects priority of the Angular UI High-priority fix.

### sync-feature-to-docs (.claude/skills/sync-feature-to-docs/SKILL.md)

#### Purpose (1 sentence)
Reads a feature CLAUDE.md and writes/updates `docs/features/{feature-kebab}/overview.md` (plus optional `api.md` and `ui.md`), preserving human-written prose via section-by-section length comparison and `<!-- DOCS:MANUAL -->` markers.

#### High-priority issues (PR-2 must fix)

- **ASCII violations throughout** (lines 36-40, 45, 56-65, 67-68, 73-74, 81, 84, 87-99, 102-105, 108-114, 119-141, 143-159, 161-177, 181-204, 207, 210-216, 219-232, 234-246). Same em-dash and Unicode-arrow problem as `generate-feature-doc`. Same fix: replace U+2014 with `--` and U+2192 with `->` throughout, including the fenced output templates which become file content downstream.
- **Two sections labelled `Step 4.5`** (lines 181-204 and 234-246). The first `Step 4.5` ("ADD CROSS-FEATURE LINKS") sits between Step 4 and Step 5; the second `Step 4.5` ("ADR cross-linking") sits AFTER `Step 5.5`. This is a copy-paste error. Symptoms downstream: an ambiguous order of operations means the runner may execute one and skip the other. Fix: rename the trailing `## Step 4.5 -- ADR cross-linking` to `## Step 5.6 -- ADR cross-linking` and move it to immediately follow Step 5.5, OR merge its body into Step 4.5 as a sub-step (4.5b). Recommend the latter because both sub-steps are about cross-linking and run on the same target file.
- **`api.md` creation logic is internally contradictory** (lines 75-86 vs lines 88-105). Lines 75-86 say create `api.md` only when one of four conditions is met (>= 6 endpoints, >= 3 DTOs, non-CRUD methods, > 15 lines of API content). Lines 88-105 say "if it exists or content is substantial" without re-stating the conditions. The runner reads top-down and may apply only the looser test. Fix: replace the heading at line 75 with `### Sections routed to api.md (creation rule below)` and consolidate the conditions in one place; remove the conflicting "if it exists or content is substantial" hedge at line 75.

#### Medium-priority issues (PR-2 should fix)

- **`ui.md` "ALWAYS create" rule is too eager** (lines 88-105). The skill says "ALWAYS create ui.md regardless of content length" if a UI section exists -- but for SIMPLE lookup features (e.g. AppointmentTypes) the Angular surface is two lines and a `ui.md` would be padding. Fix: keep the "always create" rule but cap the floor at the same minimum-viable list (table of components + ABP pattern + permission guards + injected services) and instruct the runner to skip ui.md when fewer than two of those four items have content.
- **Section-merge rule "if same length (+/- 10%): prefer CLAUDE.md (fresher from code)"** (line 64). 10% character-count tolerance is brittle for short sections (a 30-char section has +/- 3 chars of headroom). Switch to a token/word-count tolerance, or a fixed minimum-difference threshold like "50 characters AND 10%."
- **No instruction to update `<!-- Last synced from ... -->` header on partial updates** (lines 122, 146, 164). The header is written at file creation but the skill never says to refresh the date when a section is updated. Fix: add an explicit step in Step 4: "If any section was updated or added, set the header date to today (UTC)."
- **Step 4.5 cross-link procedure does not handle missing `## Relationships` section** (lines 183-204). If the source CLAUDE.md has no `## Relationships` (e.g. for a leaf lookup with no FKs), the procedure silently does nothing. Fix: state explicitly "If no `## Relationships` section is found, skip Step 4.5 entirely and log a single line: `No relationships -- no cross-links added.`"

#### Low-priority issues (PR-2 may defer)

- **SKILL-ASSESSMENT block at lines 248-268 is dated 2026-04-03** -- refresh to 2026-04-24 with PR-3 results once available.
- **Conversion table for kebab-case is inline (lines 36-40)** -- the same mapping appears in `update-docs` and `verify-docs`. Pull into a shared note, or duplicate the four bullet examples consistently across all three skills so they stay in sync.

#### Open questions (need Adrian or future session)

- Should `## Related Features` links be bidirectional (this skill writes both directions, line 196 vs 199)? The current text writes both, which means re-running sync on feature B after running on feature A could undo or duplicate the link. Confirm intended idempotence.

### update-docs (.claude/skills/update-docs/SKILL.md)

#### Purpose (1 sentence)
Orchestrator: parses an argument (`<FeatureName>`, `all`, or `modified`), then sequentially runs `/generate-feature-doc` -> `/sync-feature-to-docs` -> `/verify-docs` -> `/sync-to-vault` for the in-scope features.

#### High-priority issues (PR-2 must fix)

- **ASCII violations** (lines 17-32, 38-40, 49-50, 60, 75-77, 87, 105, 113-117, 130-136). Em dashes and Unicode arrows scattered through the prose, the printed-progress template, and the report block. Same fix as the others: U+2014 -> `--`, U+2192 -> `->`. Note line 30 has an actual right-arrow in `src/.../Domain/Appointments/...` -> `Appointments` -- this is template content that gets echoed to the user, so it must be ASCII.
- **`/sync-to-vault` invocation is referenced but the skill is registered, not the slash command** (lines 89-95). The skill list shows `sync-to-vault` (no leading slash) is the registered name. The skill body says `Run /sync-to-vault` which works in the user-facing harness but in skill-orchestration context the skill must be invoked via the Skill tool, not as a slash command. Fix: replace "Run `/sync-to-vault`" with explicit instruction "Invoke the `sync-to-vault` skill via the Skill tool with no arguments." Same edit applies to lines 49 (`/generate-feature-doc`), 51 (`/sync-feature-to-docs`), and 74 (`/verify-docs`).

#### Medium-priority issues (PR-2 should fix)

- **`modified` mode uses `git diff --name-only HEAD~1`** (line 27). For a multi-commit feature branch, `HEAD~1` only catches the last commit. Most PRs will have several commits. Fix: change to `git diff --name-only $(git merge-base HEAD origin/main)..HEAD` so it captures all branch-local changes.
- **`all` mode reads `.claude/discovery/module-map.md`** (lines 22-24). The path may not exist on every worktree (it is gitignored on at least one branch). Fix: add a fallback: "If `.claude/discovery/module-map.md` does not exist, glob `src/HealthcareSupport.CaseEvaluation.Domain/*/CLAUDE.md` and use the parent folder names as the feature list."
- **No instruction to commit between feature processings** (lines 56-58). For 15 features with sequential runs, a mid-process failure leaves a half-updated tree with no checkpointing. Fix: add a one-line note: "After each successful feature, suggest a commit but do not auto-commit. The user decides whether to checkpoint."

#### Low-priority issues (PR-2 may defer)

- **Step 5 report block uses fixed-width column layout that breaks on long file paths** (lines 102-124). Cosmetic.
- **No mention of how the orchestrator behaves when a sub-skill fails** (lines 49-95). Add one bullet on error semantics.

#### Open questions (need Adrian or future session)

- Should `update-docs` accept multiple feature names in one invocation (e.g. `Appointments,Doctors`)? Current design forces N invocations.

### verify-docs (.claude/skills/verify-docs/SKILL.md)

#### Purpose (1 sentence)
Audits all of `docs/` against the source code: classifies files as Type A (synced from a CLAUDE.md) vs Type B (free-form reference), spot-checks claims, builds a link graph, and either auto-fixes or reports navigation, accuracy, structure, and consistency issues.

#### High-priority issues (PR-2 must fix)

- **ASCII violations throughout** (lines 26-27, 31, 39, 51, 67, 84, 89-95, 99-100, 102, 117, 124, 130, 138-141, 156, 162-165, 173-184, 192-227, 233-251, 261-280, 296, 312-314, 322-330, 338, 344, 357-383, 388-396, 401-415, 417-425). Same fix as the others. Especially critical here because the auto-fix path at Step 6 writes `<!-- verified against ... on YYYY-MM-DD -->` comments into doc files; the comment template at line 339 is ASCII-clean, but the `--` separator used in the printed report (line 234, line 248, etc.) is an em dash that becomes content in the user's terminal output and is logged.
- **Step 1d "business feature folder (excluding Identity, OpenIddict, Saas, Settings)"** (lines 67-68). The exclusion list is hardcoded but the project may add other ABP-internal folders (e.g. `Tenants` under multi-tenant configurations, `BackgroundJobs`, `AuditLogs`). PR-3 against 15 features will likely surface a folder the skill flags as "missing row" when it should be excluded. Fix: parameterize the exclusion list at the top of Step 1d as a comment-block: `EXCLUDE: Identity, OpenIddict, Saas, Settings, Tenants, BackgroundJobs, AuditLogs, FeatureManagement` and instruct the runner to verify against the actual folder list before flagging.
- **Step 2c spot-check selection is non-deterministic** (lines 106-117). The skill says "pick exactly 3 specific, verifiable claims, prioritise these types in order: 1) class name, 2) permission, 3) field name, 4) relationship." Listing four priorities for picking three creates ambiguity (does the runner pick 1+2+3, or 1+2+4 if 3 is missing?). Fix: rewrite as: "Pick the 3 highest-priority claims available. If priority 1 is unavailable, fall back to priority 2, 3, 4 in order. If fewer than 3 priorities are available, spot-check fewer claims and record the count."

#### Medium-priority issues (PR-2 should fix)

- **Step 2d cross-feature FK bidirectionality check** (lines 119-135) duplicates work that `sync-feature-to-docs` Step 4.5 already does. The two skills will compete on the same `## Related Features` section. Fix: define the source of truth: "verify-docs flags missing bidirectional FKs but does NOT auto-add them. Only sync-feature-to-docs adds `## Related Features` links."
- **Step 3b sample-size logic is unstable** (lines 162-168). "Verify all 10, plus a random sample of 5 more (15 total max per file)" -- "random sample" makes the audit non-reproducible. Fix: replace with deterministic selection -- "the first 10, then every Nth claim where N = total / 5, capped at 15."
- **Step 5d auto-add back-link to INDEX.md** (lines 291-300) appends to every file. For files already inside a section that links to INDEX.md higher up the tree (e.g. `docs/features/{x}/api.md` whose sibling `overview.md` links back), this creates redundant links. Fix: check transitive reachability before appending; only add back-link if no inbound link exists from any other docs file.
- **Step 6c "mark unverifiable claims" with inline HTML comments** (lines 342-346) accumulates `<!-- UNVERIFIED -->` markers that are never cleaned up on subsequent runs. Fix: add Step 6c.1: "Before adding a new UNVERIFIED comment, search the line for an existing one and replace it; do not stack."
- **No HIPAA / synthetic-data guard during fix passes** (Step 6, lines 322-353). The skill modifies docs but never re-checks that the corrected value is not real PHI. Unlikely in practice but the safety net should exist. Fix: add a final guard at Step 6d: "Before writing any corrected value, verify the value is structural (class name, port, file path, count) and not data-shaped (no string that resembles a name, SSN, DOB, phone, address). If unsure, write `<example>` instead."

#### Low-priority issues (PR-2 may defer)

- **SKILL-ASSESSMENT block dated 2026-04-03** (line 401) -- refresh after PR-3.
- **Step 1c "Orphaned files" definition** (line 49) does not say whether INDEX.md itself counts as orphaned. It cannot be (it is the root) but the rule should state it explicitly.

#### Open questions (need Adrian or future session)

- Should verify-docs also audit `.claude/skills/` and `.claude/discovery/` for staleness, or strictly stay in `docs/`? Current scope is `docs/` only (line 13) but PR-2 changes will create skill-content drift that nothing audits.

## Aggregate PR-2 task list (extracted)

Numbered list of EXACT changes PR-2 should make, ready to copy into the plan file:

1. **Skill: generate-feature-doc, all em-dash and Unicode-arrow occurrences -- ASCII conversion.** Search-and-replace U+2014 with the two-character ASCII string `--`, and U+2192 with the two-character ASCII string `->`, across the entire file including the fenced output template at lines 213-322. Reason: `code-standards.md` requires ASCII-only output, and the template is copied verbatim into entity CLAUDE.md files.
2. **Skill: generate-feature-doc, line 316 -- Reference Pattern fragment.** Change `[CLAUDE.md#reference-pattern--appointments]` to `[CLAUDE.md#reference-pattern-appointments]` (single hyphen). Reason: GitHub slug algorithm collapses `--` to `-`, so the double-hyphen fragment 404s.
3. **Skill: generate-feature-doc, lines 326-340 -- Step 5 root-index update.** Replace the row-find logic with explicit fallback: exact-match search, then case-insensitive search with warning, then alphabetical insert. Forbid silent duplicate-row creation. Reason: PR-3 hits 15 features with inconsistent table state.
4. **Skill: generate-feature-doc, lines 281-296 -- Angular UI section.** Split into "ABP Suite scaffold (abstract/concrete)" and "Standalone Angular component" sub-paths. Detection rule: check the component file for `standalone: true` in the `@Component` decorator. Reason: Angular 20 standalone components are now the default; current skill misclassifies them as "custom."
5. **Skill: generate-feature-doc, lines 23-35 -- Step 1 globs.** Add `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json` to the per-feature glob list (with note: grep for the feature name in the file). Reason: feature-specific localization keys are documentation-relevant.
6. **Skill: generate-feature-doc, line 76 -- base class enumeration.** Add `BasicAggregateRoot<Guid>` and `Entity<Guid>` to the listed base classes, plus the fallback "If the base class is none of the above, record the literal class declaration verbatim."
7. **Skill: generate-feature-doc, lines 115-121 -- AfterMap detection.** Specify the search procedure: grep for `class {Entity}To{Dto}Mapper`, read the partial class body across all matching files, and look for `protected override void AfterMap`.
8. **Skill: generate-feature-doc, lines 164-167 -- inbound FK regex.** Replace the `x =>` regex with the more permissive `HasForeignKey\([^)]*\.{EntitySingular}Id\b`. Reason: lambda parameter names vary across the codebase.
9. **Skill: generate-feature-doc, before Step 1 -- add Step 0 safety check.** Insert a new section: "Step 0 -- Safety check. Do not copy any string literal that looks like a real person's name, SSN, DOB, address, or phone. Use the placeholder `<example>` instead." Reason: HIPAA guard for seed data.
10. **Skill: generate-feature-doc, line 335 -- Step 5 summary length.** Change "max 10-word summary" to "one-line summary, target 8-12 words."
11. **Skill: generate-feature-doc, lines 222-225 -- File Map Angular grouping rule.** Add a one-sentence example to clarify when to group by component vs list individually.
12. **Skill: generate-feature-doc, line 357 -- SKILL-ASSESSMENT date.** Refresh date from `2026-04-03` to `2026-04-24` after PR-3 runs.
13. **Skill: sync-feature-to-docs, all em-dash and Unicode-arrow occurrences -- ASCII conversion.** Same global search-and-replace as task 1, scoped to this file.
14. **Skill: sync-feature-to-docs, lines 234-246 -- duplicate Step 4.5.** Merge the trailing `## Step 4.5 -- ADR cross-linking` into the existing Step 4.5 as sub-step 4.5b (cross-feature links + ADR links share the same target file). Remove the duplicate heading.
15. **Skill: sync-feature-to-docs, lines 75-105 -- api.md and ui.md creation rules.** Consolidate api.md conditions in one place, remove the conflicting "if it exists or content is substantial" hedge. Add a floor for ui.md: skip when fewer than two of (component table, ABP pattern, permission guards, injected services) have content.
16. **Skill: sync-feature-to-docs, line 64 -- section-merge tolerance.** Replace "+/- 10%" with "50 characters AND 10%" (both must hold for "same length").
17. **Skill: sync-feature-to-docs, Step 4 -- header refresh.** Add a bullet at the end of Step 4: "If any section was updated or added, set the `<!-- Last synced from ... -->` header date to today (UTC)."
18. **Skill: sync-feature-to-docs, lines 183-204 -- missing Relationships handling.** Add the explicit guard "If no `## Relationships` section is found, skip Step 4.5 and log `No relationships -- no cross-links added.`"
19. **Skill: sync-feature-to-docs, line 248 -- SKILL-ASSESSMENT date.** Refresh after PR-3.
20. **Skill: update-docs, all em-dash and Unicode-arrow occurrences -- ASCII conversion.** Same global search-and-replace as task 1.
21. **Skill: update-docs, lines 49-95 -- skill invocation syntax.** Replace `/generate-feature-doc`, `/sync-feature-to-docs`, `/verify-docs`, `/sync-to-vault` slash-style references with explicit "Invoke the `<skill-name>` skill via the Skill tool" wording so the orchestrator works in skill-context (not just user-context).
22. **Skill: update-docs, line 27 -- modified-mode git diff.** Change `git diff --name-only HEAD~1` to `git diff --name-only $(git merge-base HEAD origin/main)..HEAD`. Reason: capture all branch-local changes, not just the last commit.
23. **Skill: update-docs, lines 22-24 -- all-mode fallback.** Add: "If `.claude/discovery/module-map.md` does not exist, glob `src/HealthcareSupport.CaseEvaluation.Domain/*/CLAUDE.md` and use parent folder names as the feature list."
24. **Skill: update-docs, lines 56-58 -- mid-process checkpoint.** Add: "After each successful feature, suggest a commit but do not auto-commit."
25. **Skill: verify-docs, all em-dash and Unicode-arrow occurrences -- ASCII conversion.** Same global search-and-replace as task 1.
26. **Skill: verify-docs, lines 67-68 -- Step 1d exclusion list.** Expand from `Identity, OpenIddict, Saas, Settings` to `Identity, OpenIddict, Saas, Settings, Tenants, BackgroundJobs, AuditLogs, FeatureManagement`, and instruct the runner to verify against the actual folder list before flagging.
27. **Skill: verify-docs, lines 106-117 -- Step 2c spot-check selection.** Rewrite as: "Pick the 3 highest-priority claims available. Fall back through priorities 1->4 in order. If fewer than 3 are available, spot-check fewer and record the count." Reason: current four-priority-pick-three is ambiguous.
28. **Skill: verify-docs, lines 119-135 -- Step 2d ownership.** Add an explicit boundary: "verify-docs flags missing bidirectional FKs but does NOT auto-add them. Only sync-feature-to-docs adds `## Related Features` links." Reason: prevents skill conflict on the same file.
29. **Skill: verify-docs, lines 162-168 -- Step 3b sample size.** Replace "random sample of 5 more" with deterministic "every Nth claim where N = total / 5, capped at 15 total."
30. **Skill: verify-docs, lines 291-300 -- Step 5d back-link.** Add a transitive-reachability check before appending the INDEX back-link; only add when no inbound link exists from any other docs file.
31. **Skill: verify-docs, lines 342-346 -- Step 6c UNVERIFIED markers.** Add: "Before adding a new UNVERIFIED comment, search the line for an existing one and replace it; do not stack."
32. **Skill: verify-docs, Step 6d -- HIPAA guard.** Add: "Before writing any corrected value, verify the value is structural (class name, port, file path, count). If the value resembles personal data (name, SSN, DOB, phone, address), write `<example>` instead."
33. **Skill: verify-docs, line 401 -- SKILL-ASSESSMENT date.** Refresh after PR-3.
