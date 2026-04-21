# 01 — Current State: what this repository looks like today

**Date of snapshot:** 2026-04-20
**Main HEAD at snapshot:** `ab7461ad2ce6b48264d09bd32277c982ba3746d1` (pre-PR#99 merge)
**Development HEAD at snapshot:** `a9997e6` (one commit ahead of main, a sync-promote)

Every fact below is a directly-observed signal. Exact paths, commit SHAs, PR numbers, and quoted text included so you can re-verify each one from your clone.

## 1. Branches that exist

On `origin`:

- `origin/main`
- `origin/development`
- `origin/staging` (existence to verify via `gh api`)
- `origin/production` (existence to verify via `gh api`)

To confirm, the handoff session should run:

```bash
gh api "repos/gesco-healthcare-support/hcs-patient-portal/branches" --jq '.[].name' | sort
```

and save the result under `evidence/branches.txt`.

## 2. The auto-promotion workflow — the central signal

A GitHub Actions workflow named `auto-pr-dev.yml` exists at `.github/workflows/auto-pr-dev.yml`. Its trigger and semantic direction are what this handoff must resolve.

Fetch and save to `evidence/`:

```bash
cat .github/workflows/auto-pr-dev.yml > evidence/auto-pr-dev.yml.txt
```

**PRs the workflow has produced (observed via `gh pr list --state all`):**

| PR # | Title | Closed state | Noted in |
|---|---|---|---|
| #77 | `ci(sync): promote main to development` | Closed (merged? verify) | `docs/verification/PHASE-B-CONTINUATION.md` line 33 |
| #96 | `ci(sync): promote main to development` | Closed (merged? verify) | observed in `git log origin/development --oneline -n 3` |

**The contradiction:** PR titles read "promote main to development". This phrasing in standard release-engineering terminology means "take the state of main and apply it to development" — implying **main is upstream, development is downstream**.

Handoff session: run

```bash
gh pr view 77 --json title,body,baseRefName,headRefName,state,mergedAt,mergedBy
gh pr view 96 --json title,body,baseRefName,headRefName,state,mergedAt,mergedBy
```

and save to `evidence/pr-77.json` + `evidence/pr-96.json`. The `baseRefName` tells you which branch was merged INTO; `headRefName` tells you what was merged FROM. This is the single most dispositive fact in the whole investigation.

## 3. Recent commit history on main (phase-B evidence)

Running `git log origin/main -n 10 --oneline` as of 2026-04-20:

```
ab7461a fix(security): resolve 4 SonarCloud hotspots (Phase-B closure) (#95)
a364809 docs(plans): add Phase B-6 test-coverage kickoff (B closure) (#92)
2f4f4b2 fix(sonar): close 1 bug + ~30 misc findings across 7 languages (B-5d) (#91)
8df93d2 refactor(sonar): close 2 S3776, defer 1, extend ABP baseline (B-5c-3b) (#90)
bd6bae3 fix(sonar): close 41 mechanical C# findings (B-5c-3a) (#89)
```

All of these are feature / fix PRs that landed **on `main` directly** — not on `development`. If the intended flow were "feature → development → main", the feature SHAs would have appeared on development first and then been merged up. The absence of that pattern is a strong signal that features merge directly to main in this repo's current wiring.

Handoff session: cross-verify by running for each PR above:

```bash
for pr in 89 90 91 92 95; do
  gh pr view $pr --json number,title,baseRefName,headRefName,state,mergedAt
done > evidence/phase-b-pr-targets.json
```

Save the output. If every `baseRefName` is `main`, the "feature-to-main" pattern is confirmed for the Phase B work.

## 4. The text of `~/.claude/rules/code-standards.md` that Adrian's global CLAUDE config loads

Exact quote from `~/.claude/rules/code-standards.md` under the "Git Workflow" section (paths-scoped to all projects):

> "Branch structure per project: main, development, staging, production + feature/fix branches.
> GitHub Flow: branch from development, PR back into development, promote through staging to production.
> Squash merge feature branches for clean history (PR title becomes the commit subject)."

**Problems with this rule text:**

1. It says "GitHub Flow" but describes a **multi-branch environment model**, which is NOT GitHub Flow (GitHub Flow is strictly `main` + feature branches, no environment branches). This is GitLab Flow (environment-branch variant) or a GitLab-Flow-like custom model.
2. It prescribes "branch from development, PR back into development" — but the observed auto-PR direction (main → development) suggests development is downstream of main, not the integration point.
3. The "promote through staging to production" phrase is ambiguous about whether `main` is upstream of `staging` or whether `development` is the start of the promotion chain.

Handoff session: quote this exact text verbatim in your `VERDICT.md`. It is one of three internal contradictions you must reconcile.

## 5. The text of `docs/plans/PHASE-B6-TEST-COVERAGE-KICKOFF.md` that mentions branching

From that kickoff doc (written on 2026-04-20 by a prior Claude session based on Adrian's approval):

- Line 720-721 (Section 14.5 Branch protection):

  > "`main`: required status checks `Backend: Build`, `Frontend: Build`. 1 required PR approval. `enforce_admins=false`. `dismiss_stale_reviews=true`. `required_linear_history=false`."

- Line 722:

  > "Solo-dev workflow: Adrian `--admin` merges after CI is green; self-approval is impossible (ABP commercial licence owner is the only human on the repo)."

The kickoff documents `main` having the strict branch-protection profile. If `main` is the upstream trunk (which the Phase-B commit history suggests), that is expected. If `main` is a downstream release tag, the strict protection is less typical.

Handoff session: fetch live branch-protection config:

```bash
gh api "repos/gesco-healthcare-support/hcs-patient-portal/branches/main/protection" > evidence/main-protection.json
gh api "repos/gesco-healthcare-support/hcs-patient-portal/branches/development/protection" > evidence/development-protection.json
gh api "repos/gesco-healthcare-support/hcs-patient-portal/branches/staging/protection" > evidence/staging-protection.json
gh api "repos/gesco-healthcare-support/hcs-patient-portal/branches/production/protection" > evidence/production-protection.json
```

Compare to what a canonical GitLab Flow configuration prescribes (see `02-RESEARCH-PLAN.md`).

## 6. Memory notes in Adrian's global config

From `C:\Users\RajeevG\.claude\projects\p--Patient-Appointment-Portal-hcs-case-evaluation-portal\memory\MEMORY.md`, relevant entries:

- `project_branch_protection.md` — "Promotion PRs use --merge (never --rebase); linear_history disabled on all branches; enforce_admins off for solo dev"
- `project_cicd_pipeline_model.md` — "4-layer GitLab Flow pipeline (Pattern 3): same static checks everywhere, env-specific stages downstream; Layer 1-2 first, 3-4 deferred"

The phrase "env-specific stages downstream" in the pipeline model note implies the environment branches are downstream of something. Downstream of what? If downstream of main, the "promote main to X" PR direction is correct. If downstream of development, the auto-PR names should have been "promote development to X" — but they read main→development.

Handoff session: this is another data point for your analysis. Read the Pattern-3 source (Martin Fowler's Deployment Pipeline article) to calibrate what "downstream env branches" means.

## 7. PR #99 (PR-0 of Phase B-6 Tier 1, merged just before this handoff was written)

- Branch: `feat/b6-tier1-pr0-infrastructure`
- Base: `development` ← **this is the choice the session is asking the handoff to verify**
- Target: `development`
- Merged: yes, via `gh pr merge --admin --squash` (per Adrian)
- URL: https://github.com/gesco-healthcare-support/hcs-patient-portal/pull/99

If the "feature → development" pattern is correct per canonical GitLab Flow, PR #99 was targeted correctly. If the canonical pattern is "feature → main", PR #99 should have targeted `main`, and the mistake will need to be documented (but does not need to be fixed retroactively — the squash commit simply lives on `development` until the next main→development sync brings it back).

Handoff session: mention PR #99 in your `VERDICT.md` as "the specific PR whose target branch is the concrete manifestation of the question."

## 8. Four-branch-protection model as a Gesco convention

Per Adrian's global CLAUDE (`C:\Users\RajeevG\.claude\CLAUDE.md`, "Projects" section):

> "Currently focused on Patient Portal, one project at a time. Pipeline: Digital Forms -> Patient Portal -> Case Tracking -> MRR AI."

And the code-standards rule:

> "Branch structure per project: main, development, staging, production + feature/fix branches."

This is a **cross-project standard**. Getting this right in Patient Portal matters because whatever model is declared correct will be replicated in Case Tracking, MRR AI, and Digital Forms as they come into documentation scope. A mistake replicated 4x is more expensive than a mistake contained in one.

Handoff session: note this "replication scope" in your `VERDICT.md` executive summary. The fix, if any, should be written to apply cross-project.

## 9. What a correctly-working GitLab Flow looks like in practice (for calibration)

You will verify this from external research, but these are common patterns reported in industry. Include your own verified version in `VERDICT.md`:

- **Pattern 1: Environment-branches upstream = trunk.** `main` (or `master`, or `develop`) is the integration trunk. Features merge to trunk. Then trunk is promoted downstream: trunk → staging → production. Each downstream env branch is a "frozen snapshot" of trunk at deploy time. Sync-back occurs if hotfixes land on production (back-propagates to main).

- **Pattern 2: Environment-branches downstream = trunk.** `production` is the stable release branch. `staging` is pre-prod. `development` (or `pre-production`) is the integration trunk. Features merge to development. Promotion flows development → staging → production. This is what `~/.claude/rules/code-standards.md` describes.

- **Pattern 3: Upstream-first (GitLab's own whitepaper variant).** Features merge to `main` (or `master`). Environment branches (`pre-production`, `production`) are downstream and advance via promotion PRs. No `development` branch in this variant.

- **Pattern 4: Release-branches.** `main` is the integration branch; `release-*` branches are cut per release for stabilisation; no env-specific persistent branches.

Which of these four (or a fifth you discover) is Gesco's intended model? That is the crux.

## 10. Specific contradictions you must resolve

Compile these into a table in `VERDICT.md`:

| Signal | Implies upstream is... | Evidence |
|---|---|---|
| Phase-B PRs #89-#95 merged directly to `main` | main | `git log origin/main` |
| Auto-PR named "promote main to development" | main | PR #77, #96 titles |
| `~/.claude/rules/code-standards.md` "branch from development" | development | rule text |
| `main` has strict branch protection per kickoff §14.5 | main | verify live |
| Memory note "env-specific stages downstream" | ambiguous | MEMORY.md |
| PR #99 targeted development and was approved by Adrian | development (by practice) | PR #99 |

Your analysis must reconcile all six signals against the canonical GitLab Flow definition.

## 11. Evidence-collection checklist (before you start research)

Run and save all of these to `docs/handoffs/2026-04-20-gitlab-flow-branching-model-verification/evidence/`:

```bash
EVIDENCE="docs/handoffs/2026-04-20-gitlab-flow-branching-model-verification/evidence"
mkdir -p "$EVIDENCE"

# Branches
gh api "repos/gesco-healthcare-support/hcs-patient-portal/branches" > "$EVIDENCE/branches.json"

# Auto-promote workflow
cp .github/workflows/auto-pr-dev.yml "$EVIDENCE/auto-pr-dev.yml"
# Any other auto-promote workflows:
ls .github/workflows/ | grep -iE '(auto|promote|sync)' | xargs -I {} cp .github/workflows/{} "$EVIDENCE/"

# The two historical auto-PRs
gh pr view 77 --json title,body,baseRefName,headRefName,state,mergedAt,author > "$EVIDENCE/pr-77.json"
gh pr view 96 --json title,body,baseRefName,headRefName,state,mergedAt,author > "$EVIDENCE/pr-96.json"

# Phase-B PR targets
for pr in 73 76 78 79 80 81 82 83 84 85 86 87 89 90 91 92 95 99; do
  gh pr view $pr --json number,title,baseRefName,headRefName,state,mergedAt 2>/dev/null
done > "$EVIDENCE/phase-b-pr-targets.json"

# Branch protection on all four env branches
for br in main development staging production; do
  gh api "repos/gesco-healthcare-support/hcs-patient-portal/branches/$br/protection" 2>&1 > "$EVIDENCE/$br-protection.json" || echo "branch $br may not have protection" > "$EVIDENCE/$br-protection.err"
done

# Repository settings (might include default branch)
gh api "repos/gesco-healthcare-support/hcs-patient-portal" > "$EVIDENCE/repo-settings.json"
```

Do this BEFORE reading external sources. Primary-source state of the repo first, then compare to canonical.
