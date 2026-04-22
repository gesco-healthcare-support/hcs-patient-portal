# 03 — Deliverable specification

**Read after:** `README.md` + `01-CURRENT-STATE.md` + `02-RESEARCH-PLAN.md`
**What this file defines:** the exact structure of `VERDICT.md` that you will produce. Do not deviate. Adrian will read this; structure consistency matters.

## File path

`docs/handoffs/2026-04-20-gitlab-flow-branching-model-verification/VERDICT.md`

## Required sections (in this order)

### Section 0: Frontmatter

```yaml
---
handoff: gitlab-flow-branching-model-verification
verdict-date: YYYY-MM-DD
verdict-author: Claude (session <short-id>)
main-head-at-verdict: <git SHA>
confidence-overall: HIGH | MEDIUM | LOW
---
```

### Section 1: Executive summary (under 200 words)

Answer the one question the handoff exists to resolve: **"In Gesco's repos, which branch should in-flight feature branches target, and in which direction do promotion PRs flow between main / development / staging / production?"**

Structure:

- **Verdict:** one sentence stating the answer.
- **Intended Gesco model:** one sentence naming the canonical GitLab Flow variant Gesco should implement (e.g., "GitLab Flow environment-branches variant, `main` as integration trunk").
- **Current state vs intended:** one sentence summarising whether Gesco matches, partially matches, or diverges.
- **Action required:** one sentence summarising the minimum fix (or "no action; current state is correct").
- **Confidence:** HIGH / MEDIUM / LOW + one sentence reason.

A reader who reads only Section 1 must walk away with a clear picture. Everything else is evidence.

### Section 2: Canonical GitLab Flow (per your research)

- 2.1 — What GitLab's own documentation defines GitLab Flow as, quoted verbatim with source URL + date accessed. Confidence: HIGH (primary source).
- 2.2 — The documented variants (production-branch, environment-branches, release-branches, upstream-first). For each, quote a defining sentence from the primary source.
- 2.3 — For the environment-branches variant specifically: which branch is the integration trunk, which are downstream, what's the direction of promotion? Cite primary source + 2 secondary cross-checks.
- 2.4 — Any evolution of the canonical model over time. (If GitLab's whitepaper was rewritten in 2022, note it.)
- 2.5 — Disagreements between sources, if any. Primary wins, but disagreements are flagged.

### Section 3: Gesco's observed state

- 3.1 — Inventory of branches that exist on `origin`. Confidence: HIGH (git command).
- 3.2 — Auto-promotion workflow file content (paste the `auto-pr-dev.yml` verbatim from `evidence/`).
- 3.3 — Auto-PR direction (the `baseRefName` / `headRefName` extracted from `gh pr view 77` and `gh pr view 96`). Confidence: HIGH (API).
- 3.4 — Phase-B PR targets. Confidence: HIGH (API).
- 3.5 — Branch protection configuration per branch. Confidence: HIGH (API). Flag any branches that don't have protection.
- 3.6 — The text of `~/.claude/rules/code-standards.md` "Git Workflow" section, quoted verbatim.
- 3.7 — PR #99's target (development) + Adrian's explicit approval of that target.

Each sub-section must cite the specific evidence file in `evidence/` that supports the claim.

### Section 4: Reconciliation — which model does Gesco actually implement?

A formal decision table. For each signal in §3, classify it as supporting Pattern A (feature → main, env branches downstream), Pattern B (feature → development, main as release), or Pattern C (other). Then count votes.

| # | Signal | Supports pattern | Confidence | Source |
|---|---|---|---|---|
| 1 | Auto-PR direction main → development | A | HIGH | `evidence/pr-77.json` + `evidence/pr-96.json` |
| 2 | Phase-B PRs #89-#95 merged to main | A | HIGH | `evidence/phase-b-pr-targets.json` |
| 3 | `code-standards.md` says "branch from development" | B | HIGH (quote) / LOW (as current-state evidence) | rule file |
| 4 | `main` has strict branch protection | A (usually) | MEDIUM | `evidence/main-protection.json` |
| 5 | Memory note "env-specific stages downstream" | A | LOW (ambiguous) | MEMORY.md |
| 6 | PR #99 targeted development, Adrian approved | B | MEDIUM (interpretive) | PR #99 |

Weight: HIGH signals outweigh LOW. Operational signals (workflow behavior, PR history) outweigh documentation text. Verdict: Pattern A or B.

### Section 5: Verdict with detailed reasoning

A full prose argument walking a reader through why your verdict follows from the signals + canonical research. At least 500 words, no more than 1500. Should include:

- Why the operational signals point to one pattern.
- Why the documentation text appears to point elsewhere.
- Which is authoritative in the "what Gesco is doing" sense.
- Which is authoritative in the "what Gesco intends" sense, based on Adrian's statement ("I definitely want to maintain the GitLab flow style implementation").
- Whether what Gesco intends matches canonical GitLab Flow.
- Final verdict on which model is correct and which file / signal needs to change.

### Section 6: Fix plan

Only present if your verdict is "mis-wire exists."

For each file / configuration item that needs changing, provide:

- **File path** (absolute).
- **Current content** (quoted block).
- **Intended content** (quoted block or diff).
- **Rationale** (one sentence linking to the verdict).
- **Risk** (low / medium / high + explanation).
- **Specific shell command** Adrian can run to make the change.

Example format:

```
### 6.1 `~/.claude/rules/code-standards.md`

Current:
> Branch structure per project: main, development, staging, production + feature/fix branches.
> GitHub Flow: branch from development, PR back into development, promote through staging to production.

Intended:
> Branch structure per project: main (integration trunk), development, staging, production (env-tracking branches).
> GitLab Flow environment-branches variant: branch from main, PR back into main; auto-promote to development on merge; promote dev -> staging -> production via manual PRs.

Rationale: aligns global rule with actual repo workflow (`auto-pr-dev.yml` direction + Phase-B PR targets).
Risk: low; documentation-only; does not change any running system.
Command: manual edit (Adrian).
```

### Section 7: Migration for in-flight work

Specifically address: PR #99 merged into development instead of main. What happens to PR #99's content?

- Does a dev → main sync workflow exist? If yes, PR #99's squash commit will reach main automatically on the next trigger. Verify this.
- If no dev → main sync, PR #99 content must be manually cherry-picked to main OR moved by opening a second PR from development into main.
- Recommend the safest path with specific commands.

### Section 8: Downstream Gesco projects

Remind Adrian: the verdict here replicates to Case Tracking, MRR AI, and Digital Forms when they come into documentation scope.

- List each project + its repo path.
- Recommended copy-paste rule update for each.
- Note any per-project customisation expected (e.g., MRR AI is Python; branching model can still be identical).

### Section 9: Open questions + things Claude did NOT research

Explicit list of anything you didn't verify, with the reason. Candidates:

- Whether Gesco's `staging` and `production` branches have been used in practice yet (per handoff context, they're placeholders).
- Whether the auto-promotion workflow should sync main → development on every main commit or only on a schedule.
- Whether branch protection should be tightened on development now that it's the (apparent) Pattern-A downstream env branch.
- Whether Conventional Commit squash-merge behavior interacts with the branching choice in any way.

Each open question must be actionable by Adrian (i.e., he can decide after reading) or deferrable to a future handoff.

### Section 10: Sources cited

A bibliography. Every URL / doc cited in the deliverable must appear here with:

- Full URL.
- Publication / last-modified date (as seen at research time).
- Author (if known).
- Primary / secondary classification.
- What claim it supports.

Group by source category (matching `02-RESEARCH-PLAN.md` §"Authoritative source list"). Minimum 8 unique sources total; minimum 2 primary + 3 secondary + 3 community/expert.

### Section 11: Reproducibility appendix

An appendix listing the exact shell commands any future session could run to re-verify your verdict. Should include:

- All `gh api` commands that collected `evidence/`.
- All `git log` / `git show` commands used.
- All `WebFetch` / `WebSearch` queries executed (paste the exact prompts used).

## Confidence labelling standard

Per `~/.claude/rules/communication.md`:

- **HIGH** = official primary documentation or direct source code / API response. Two-source concurrence where both are primary.
- **MEDIUM** = reputable industry blog / recognized expert / community consensus with 3+ corroborating voices. Or: primary source with one credible counter-voice.
- **LOW** = single community voice / inference from non-primary source / no independent corroboration. Or: contradictory sources with no clear winner.

Every claim gets one label. Unlabeled claims are defects in the deliverable and must be fixed before shipping.

## Length and style

- Executive summary: under 200 words. (Adrian reads this first.)
- Sections 2-5: as long as needed to carry the argument, but favour tables + quoted blocks over prose walls.
- Sections 6-11: as long as needed for correctness; trim fluff.
- ASCII only. No smart quotes, em dashes, or Unicode decoration. Use `-`, `--`, `->`, straight quotes.
- All file paths, branch names, SHAs rendered in `inline code` formatting.
- Markdown tables for decision matrices, lists for enumerations, code blocks for commands.
- Every section starts with a one-sentence summary of what that section argues, before the detail.

## Final checklist before marking the handoff done

Before you consider `VERDICT.md` complete:

- [ ] All 11 sections present in the specified order.
- [ ] Frontmatter filled in.
- [ ] Executive summary under 200 words and stands alone.
- [ ] All 10 research questions from `02-RESEARCH-PLAN.md` answered.
- [ ] At least 8 sources cited with dates.
- [ ] Every claim labeled HIGH / MEDIUM / LOW.
- [ ] Minimum 2 primary + 3 secondary + 3 community sources.
- [ ] `evidence/` folder populated and referenced.
- [ ] Fix plan (Section 6) is actionable (specific commands, not vague suggestions).
- [ ] Open questions (Section 9) are explicit about what was NOT researched.
- [ ] Reproducibility appendix lets a future session re-verify in under 30 minutes.
- [ ] Zero speculation. If uncertain, labeled LOW and flagged.
- [ ] Adrian's "zero-doubt" bar is met OR the unresolvable blocker is called out in §9.

If the checklist is complete, ship `VERDICT.md` to the same handoff folder and alert Adrian.
