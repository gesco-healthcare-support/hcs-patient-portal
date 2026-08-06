# Prompt for the incoming session

Copy everything between the rules below into the first message of the new session.

---

You are picking up an in-flight epic in the Patient Portal codebase from a previous session on a
different account. That session's conversation is gone; everything you need is on disk.

Repo: `C:/src/patient-portal/main` (the main checkout -- **NEVER create a git worktree**).
Stack: Angular 20.3.19 + .NET 10 / ABP Commercial 10.0.2, SQL Server, EF Core, OpenIddict,
subdomain multi-tenant DATABASE-PER-OFFICE. Ports: AuthServer 44368, API 44327, Angular 4200.

=========================================================================
STEP 1 -- READ THESE THREE FILES BEFORE DOING ANYTHING ELSE
=========================================================================

1. `docs/handoff/2026-08-05-session-handoff.md`
   THE HANDOFF. Where the work stands, what shipped, what is outstanding, every working rule in
   force, the environment traps, and the live-verification credentials. Read it in full.
2. `docs/research/2026-08-05-reschedule-creates-new-appointment.md`
   THE PHASE 4D RESEARCH PACKET. Verified `file:line` anchors, the context packet, and all six
   decisions already resolved with the user. This is your input to design.
3. `docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md`
   THE LIVING EPIC TRACKER. Phase table, locked decisions, cross-phase findings, and the
   "Learnings carried forward" section -- read that section carefully, it is where the expensive
   mistakes of phases 1-4c are recorded so you do not repeat them.

Also useful once oriented: `docs/plans/2026-08-05-reschedule-consent-rounds.md` (the phase 4c
plan, including the four corrections made to it during the build).

=========================================================================
STEP 2 -- TWO TRAPS THAT WILL MISLEAD YOU IF YOU MISS THEM
=========================================================================

1. The repo's root `CLAUDE.md` auto-loads and is titled "branch `feat/replicate-old-app`". It
   describes a DIFFERENT mission (porting a legacy app from `P:\PatientPortalOld`). That is NOT
   this work. This work is the reschedule/cancel/calendar epic on `main`, governed by the tracker
   above. The `.claude/rules/*.md` files DO apply.
2. The worktree is shared with other sessions. Another session can switch the branch under you.
   Re-check `git rev-parse --abbrev-ref HEAD` IMMEDIATELY before every commit, and commit BY
   PATHSPEC (`git commit -F - -- <explicit paths>`), never a bare `git commit`.

=========================================================================
STATE
=========================================================================

`main` is at `2ce2ef3f`. Phases 1, 2, 3, 4a, 4b, 4c are DONE and merged. Phase 4d is RESEARCHED
with all decisions resolved, but NOT designed and NOT built. 4a-4e are strictly sequential.

**Most urgent standing item:** phases 4b and 4c are merged but NOT DEPLOYED, and they must deploy
TOGETHER as a single server release -- neither is safe alone. Do not deploy without an explicit
fresh go from the user.

=========================================================================
YOUR TASK
=========================================================================

Run `/feature-design` for phase 4d and write the plan to
`docs/plans/2026-08-XX-reschedule-creates-new-appointment.md`.

All six design decisions are ALREADY RESOLVED -- they are in section 4 of the research packet. Do
not re-litigate them. Do not re-run research; it is done. Go straight to writing a concrete,
one-pass-executable plan: every task anchored to an exact `file:line` plus the pattern to mirror,
an approach flag per task (`tdd` / `test-after` / `code`), EARS acceptance criteria
("WHEN <trigger>, THE SYSTEM SHALL <behavior>"), a validation loop covering every layer the diff
touches, and a live gate.

Two things the plan must handle explicitly:

- **Amend the epic tracker's locked-decision list.** One locked decision ("reuse the create
  pipeline") was found not to hold -- see section 2 of the research packet. That correction is
  part of 4d's scope.
- **Say what 4d pushes to Case Tracker and what it defers to 4e.** 4d creates a second appointment
  row where the CT contract currently promises there will never be one
  (`docs/integration/case-tracker-api-contract.md` section E2).

The highest risk in this phase is the child-entity copier: it is the same work that caused bug
F18, which silently dropped 2 of 8 child groups. The agreed mitigation is an explicit audit of all
nine groups with ONE TEST PER GROUP. Treat that as non-negotiable in the plan.

=========================================================================
HOW THE USER WANTS YOU TO WORK
=========================================================================

Section 6 of the handoff file has the full list. The ones that bite hardest:

- **Surface EVERY question or decision through the AskUserQuestion modal**, never as inline prose.
  This is explicit and applies to all decisions.
- Follow RPE: research -> design -> build -> ship, scaled to the size of the change.
- If the plan turns out to be wrong mid-build: STOP, say so, update the plan, resume from the
  corrected task. Never silently work around it. This happened four times in phase 4c and catching
  it each time is why 4c shipped correct.
- Mutation-check every new test: deliberately break the code, confirm the intended test fails,
  then revert. A test that has never failed proves nothing.
- Branch off `main` with a descriptive name (never phase letters). Squash-merge. On green,
  `gh pr merge --squash --admin` without asking -- there is no second reviewer. NEVER delete
  branches. Never push to `development`. Leave PRs #410 and #384 and all Dependabot PRs alone.
- The validation loop must cover every layer the diff touches -- backend format/build/test, BOTH
  migration contexts, and `npx ng build` + `npx ng test` when Angular changes.
- Do not launch subagents or Workflows without stating the scale and getting a yes.
- HIPAA: synthetic data only, everywhere. ASCII only. No ADRs -- fold decisions into the plan.

Start by reading the three files, then tell me your understanding of where things stand and what
4d involves before you write the plan.
