---
status: draft
title: CLAUDE.md drift-proofing pass
date: 2026-06-01
branch: docs/claude-md-and-docs-overhaul (extends PR #276)
author: Adrian (with Claude)
---

# CLAUDE.md drift-proofing pass

<!-- Follow-up to the CLAUDE.md/docs overhaul (PR #276). An independent audit against fresh
     power-user research found the structure sound but the content partly drift-prone: exact
     counts, source line numbers, embedded code/entity blocks, and a few duplicated rules that
     will rot as code changes. This pass removes the rot and keeps 100% of the non-obvious
     knowledge. Docs-only. Plan-first; execute only after Adrian approves. -->

## Goal

Make the 32 CLAUDE.md files durable: strip content that goes stale on the next refactor and
de-duplicate across the hierarchy, while preserving every gotcha, business rule, and non-obvious
constraint. Expected effect: ~20-30% leaner, materially lower maintenance drift, zero loss of
the knowledge that makes the files worth loading. Grounding: Anthropic memory/best-practices docs
+ power-user consensus (prefer file:line pointers over code copies; document only what cannot be
inferred; nested files additive only).

## The 5 transformation rules (apply uniformly)

1. **Counts -> concepts.** Drop mechanical counts that rot ("9 Hangfire jobs", "55-field form",
   "35 DbSets", "59 templates / 41 HTML", "14 custom repos", "~1,594 lines"). State the concept
   + a pointer instead. KEEP a number only when the number IS the gotcha (e.g. "proxy sends 18
   params, server reads 7"; non-sequential enum values like `View=23/Edit=24`; `Capacity` default 3
   as a business rule; the 4-vs-7 constructor-field split).
2. **Snippets / entity-shape blocks -> pointers.** Remove fenced C#/HTML/`dotnet ef` blocks and
   entity-field dumps; replace with `see <File.cs>` / a symbol or search hint, keeping the prose
   "why". Allow at most one tiny pattern illustration where the pattern itself is the lesson
   (e.g. blob-download), and shrink it.
3. **Line numbers -> symbols.** Zero source line numbers in any CLAUDE.md (`line 245`, `line 1546`,
   `lines 79-87`). Reference a symbol name or a searchable string. Re-word the `console.log` ref to
   "remove the 'Date check:' debug log before release".
4. **De-duplicate parent/child.** Where a rule already lives in the layer/root file, the feature
   file points to it rather than restating (SSN never-clear, `[RemoteService(IsEnabled=false)]`,
   state-machine "never set status directly", `performFullLogout`).
5. **Collapse the over-documented.** `Domain/Books` -> ~10-line "vestigial; do not extend" note
   (move the one real signal, the banned `CreateUpdateBookDto` pattern, to Application.Contracts).
   `Application/ExternalSignups` -> drop the inferable cross-layer file-map table, keep the gotchas.
   Angular layer file -> replace the 22-dir enumeration with a pointer; drop the `shared/` inventory
   the `shared/` spoke already owns. Trim files to <=2 `IMPORTANT` markers.

## Files to change (others already clean -- leave as-is)

| Fix focus | Files |
| --- | --- |
| Counts + snippets + line numbers | `Domain/Appointments`, `HttpApi.Host`, `Domain/CLAUDE.md`, `Domain/Patients`, `Domain/Locations`, `Domain/DoctorAvailabilities`, `Domain/AppointmentDocuments`, `Domain/NotificationTemplates`, `Domain.Shared`, `EntityFrameworkCore`, `DbMigrator`, `Application`, `Application.Contracts`, `HttpApi`, `test` |
| Line numbers / snippets | `Domain/ApplicantAttorneys`, `Domain/AppointmentApplicantAttorneys`, `Domain/AppointmentEmployerDetails`, `Domain/Doctors` |
| Snippets + counts + dedupe | `angular/src/app/CLAUDE.md`, `angular/.../appointments`, `angular/.../doctor-availabilities`, `angular/.../patients`, `angular/.../shared` |
| Collapse | `Domain/Books` (->~10 lines), `Application/ExternalSignups` (trim file-map) |
| Already clean (no change) | root hub, `HttpApi.Client`, `Domain/Notifications`, `Domain/AppointmentChangeRequests`, `AuthServer`, `Domain/AppointmentAccessors` |

## Preserved (non-negotiable)

Every genuine gotcha, business rule, non-obvious constraint, PHI/HIPAA guidance, and load-bearing
number stays. All "Related" links keep resolving. No gotcha is removed to hit a length target.
A line is cut ONLY if a refactor would invalidate it AND it carries no non-obvious meaning.

## Execution

- Parallel agents, one per file-group, each applying the 5 rules to its files with an explicit
  "preserve every gotcha / non-obvious fact" guardrail; I review each result.
- Stage ONLY CLAUDE.md files. There is active unrelated uncommitted work in this worktree
  ("fax-optional" Angular edits + a same-day plan) -- do not stage, commit, or touch it.
- Commit to `docs/claude-md-and-docs-overhaul` (updates open PR #276); 1-2 conventional commits.

## Verification

- Every CLAUDE.md still < 200 lines (will shrink).
- `grep` confirms: no fenced code blocks of impl, no `line NNN` source refs remain (except code-free
  command refs moved to docs), no `@import`.
- No dead "Related" links (re-run the docs link scan).
- Spot-check >=6 files: every gotcha/business rule present before the pass is still present.
- No new contradictions across root/layer/feature.
