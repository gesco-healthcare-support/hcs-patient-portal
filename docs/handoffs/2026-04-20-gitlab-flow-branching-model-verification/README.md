# Handoff: GitLab Flow branching model verification for Gesco Patient Portal

**Created:** 2026-04-20
**Created by:** Claude session executing Phase B-6 Tier-1 PR-0 test infrastructure
**Owner:** Adrian (AdrianG@gesco.com)
**Priority:** BLOCKING for future branch-protection / environment-promotion automation decisions. NON-BLOCKING for the in-flight Phase B-6 Tier-1 test-coverage work (which deliberately continues on the current pattern while this is being verified).

## Why this handoff exists

During Phase B-6 PR-0 execution, the executing Claude session noticed an internal contradiction in how this repository's branching model is described versus how it appears to behave in practice. The session asked Adrian to confirm the intended model; Adrian replied that Gesco explicitly wants **GitLab Flow**, that the model was researched when first set up, but that something may have gotten swapped during implementation.

Rather than guess and compound a potential mistake, Adrian is deferring verification to a **fresh, dedicated Claude session running against an identical clone of this repository**. That is you.

The in-flight Phase B-6 Tier-1 work continues in parallel on the current (possibly-mistaken) pattern because the answer here does not change any individual feature-PR's content — only which branch it targets. Once you produce a verdict, any future PRs will adopt the correct pattern, and a one-time migration commit (if needed) will align existing branches.

## What "correct" means for this handoff

Adrian's bar (direct quote):

> "this has to be very thorough and detailed and there should be no doubt that this is the correct and accurate way to implement the GitLab Flow style repository and CI/CD. If we have made mistakes I want to know. Your hand-off folder should contain detailed information of multiple types of contradictory research to determine what is the truth, and multiple different sets of resources and methods to research to ensure that we not only read the official documentation but also other experienced developer recommendations, community reviews and ideas, etc."

Concretely: a verdict that reads "maybe" or "either model works" is a failed deliverable. The expected output is a single unambiguous answer, with an evidence trail that cites primary official documentation AND cross-references multiple independent expert/community sources.

## The specific question to answer

**In a correctly-implemented GitLab Flow repository with `main`, `development`, `staging`, and `production` branches plus feature branches, which branch does an in-flight feature branch merge into, and in which direction do promotion PRs flow between the four environment branches?**

Sub-questions, all of which the deliverable must answer authoritatively:

1. Is "GitLab Flow" a single canonical model, or multiple variants? If multiple, which variant did GitLab's own documentation describe first / describe canonically / recommend for teams with our shape (solo dev, 4 environments, ABP commercial stack)?
2. In GitLab Flow's "environment branches" variant, which branch is the **upstream trunk** (where features integrate) and which are downstream (deploy-tracking)?
3. Is the upstream trunk conventionally named `main`, `master`, `develop`, `development`, or `production`? Has the canonical naming changed since GitLab first published the Flow whitepaper?
4. What direction do promotion PRs flow? Is "promote main to development" standard terminology, an inversion, or a sync-back for hotfixes that lands on the trunk?
5. Given this repo's current observed signals (see `01-CURRENT-STATE.md`), which of the canonical GitLab Flow variants does it actually implement? If none, what's the mis-wire?
6. If a mis-wire exists, what is the minimum-effort, non-destructive correction? (Renaming branches? Inverting the auto-PR direction? Updating documentation? Reversing direction of feature targets?)
7. What does branch protection configuration look like in canonical GitLab Flow? Does this repo's current configuration match?

## Expected deliverable

A single markdown file at this path, written by you:

`docs/handoffs/2026-04-20-gitlab-flow-branching-model-verification/VERDICT.md`

Structure specified in `03-DELIVERABLE-SPEC.md`. Do not deviate from that structure.

## Constraints on your work

1. **No code changes in this session.** You are producing `VERDICT.md` only. Do not touch branches, workflows, settings, or any production file. Commits in the repository should be limited to the `VERDICT.md` file itself and any supporting artifacts you choose to save (screenshots, JSON dumps of `gh api` responses, etc.) — all under `docs/handoffs/2026-04-20-gitlab-flow-branching-model-verification/`.
2. **Zero-trust verification per `~/.claude/rules/zero-trust-verification.md`.** Every technical claim in your deliverable must be independently verified against two or more external sources at research time. Do not rely on Claude's training data. Date-check every source (is the linked article from 2019 or 2026?).
3. **Read the full handoff before starting research.** `01-CURRENT-STATE.md` has the observed evidence. `02-RESEARCH-PLAN.md` has the exact question list and source rubric. `03-DELIVERABLE-SPEC.md` has the output format.
4. **Minimum source count per claim.** 1 primary (GitLab official docs, their Flow whitepaper, or equivalent) + 2 secondary (community/expert/industry) for every technical claim. If you cannot meet this bar on a specific claim, flag it LOW-confidence in the deliverable.
5. **Confidence labels required.** Every claim in `VERDICT.md` must carry HIGH / MEDIUM / LOW confidence, as defined in `~/.claude/rules/communication.md`. No unlabeled claims.
6. **Do NOT read the Phase B-6 Tier-1 plan** (`docs/plans/2026-04-20-phase-b6-tier1.md`) or any Tier-1 progress artifacts. That work is deliberately isolated from this verification so it doesn't bias the answer.
7. **Do NOT modify `~/.claude/rules/code-standards.md`**. If your verdict recommends changing it, document that recommendation in `VERDICT.md` — Adrian makes the change himself after reviewing.

## Files in this folder

- `README.md` (this file) — entry point, goal, constraints.
- `01-CURRENT-STATE.md` — every observed signal in this repo that bears on the question, with exact file paths, PR numbers, and quoted text. Read this second.
- `02-RESEARCH-PLAN.md` — the specific rubric of questions, authoritative source list, methodology, and how to conduct the research. Read this third.
- `03-DELIVERABLE-SPEC.md` — exact structure of `VERDICT.md` that you will produce. Read this fourth.
- `VERDICT.md` — your deliverable. Does not exist yet; you will create it.

## Starting instructions

If you are the Claude session dispatched to handle this handoff, your first actions are:

1. Read all four files in this folder end-to-end, in order (README.md -> 01 -> 02 -> 03).
2. Use `gh api` + `git log --all --oneline --graph` + `git remote show origin` to capture the current state of all four branches and all auto-promotion workflows. Save raw output to `evidence/` subfolder for traceability.
3. Start external research per `02-RESEARCH-PLAN.md`. Do NOT open `docs/plans/*` or `.claude/rules/code-standards.md` except to quote them into `VERDICT.md` where the deliverable requires it.
4. Produce `VERDICT.md` per `03-DELIVERABLE-SPEC.md`.
5. Stop. Do not push code. Do not open PRs. Do not modify branches. Commit only the files under this handoff folder.

If the handoff prompt is unclear at any point, stop and ask Adrian directly rather than guessing.

## Context for this handoff that you may not otherwise have

- **Project:** Gesco Patient Portal, `hcs-patient-portal` on GitHub under org `gesco-healthcare-support`. Repo path on Adrian's machine: `p:\Patient Appointment Portal\hcs-case-evaluation-portal`.
- **Stack:** .NET 10 / ABP Commercial 10.0.2 / Angular 20 / SQL Server. Solo dev (Adrian). No CI/CD to production yet — staging + production branches are placeholders per `docs/verification/PHASE-B-CONTINUATION.md`.
- **4-branch environment model** is an explicit Gesco convention across all projects (per `~/.claude/CLAUDE.md`). Verifying this one repo's wiring informs the other three Gesco repos (Case Tracking, MRR AI, Digital Forms).
- **Phase-B hardening is complete**, Phase-B-6 test coverage is in flight. No unrelated infrastructure refactor is blocking; this handoff is pure CI/CD pattern verification.
