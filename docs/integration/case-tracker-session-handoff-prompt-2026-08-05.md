# Session-start prompt -- Case Tracker integration continuation

Paste the block below into the new session as the first message.

---

You are picking up in-flight work on the Gesco Appointment Portal at
`C:\src\patient-portal\main` (Angular 20 / .NET 10 / ABP Commercial 10.0.2, SQL Server, EF Core).
I am Adrian, the sole developer. A previous session on a different Claude account did the analysis
and wrote it all down; you are continuing from that, not restarting it.

**First action, before anything else including clarifying questions:** read
`docs/integration/case-tracker-session-handoff-2026-08-05.md` in full. It is self-contained and
written for a reader with no transcript and no memory of prior sessions. Everything below assumes
you have read it.

## The work

The Appointment Portal pushes approved appointments to the Case Tracker, a separate Gesco system.
Levon is their developer. Over a long email exchange he asked for a set of additions; some shipped,
some did not. A separate multi-phase epic (reschedule / cancel / calendar) is owned by OTHER
sessions and is not yours. Your scope is the items that sit outside that epic.

The previous session established what is outstanding, verified each claim against the code, found
two places where what we told Levon does not match what the code does, and recommended where each
outstanding item belongs. Sections 4, 5 and 6 of the handoff carry that.

## Your objective

Get the outstanding non-epic work to the point where it can be built: resolve the open decisions,
then produce an executable plan for one new independent phase covering N1 (who cancelled), N2 (AME
auto-cancel flag), N3 (patient postal address), N4 (requested-vs-finalized timestamps) and D1 (the
contract note about non-200 responses never driving document pruning).

## Constraints, and why each one is there

- **Do not deploy anything, and do not merge the `main` -> `development` cascade PR #410.** I
  explicitly declined a deploy. Handoff section 8 exists so nobody re-derives those findings, not
  as a task list. Do not raise deploying unless I raise it first.
- **Do not touch epic phases 4d, 4e or 5.** Other sessions own them; parallel edits to the same
  plan files have already caused one recovery.
- **This worktree is shared and its branch moves underneath you.** Run `git branch --show-current`
  and `git status --short` immediately before any commit, commit by explicit pathspec, and never
  use `git add -A` or a bare `git commit` -- a previous session swept another session's file into
  the wrong commit that way. Files you did not create belong to someone else; leave them. Do not
  create a git worktree.
- **Verify before asserting.** Every factual claim in the handoff carries a `file:line` or a
  command; re-run the check before repeating it to me or putting it in a plan. Two wrong answers
  nearly reached Levon because a fact came from memory or a truncated grep. A truncated grep is not
  evidence, and code moves.
- **ASCII only** in output, files and commits -- no em dashes, smart quotes or emoji, because a
  commit hook and a PR hook both reject non-ASCII.
- **The repo root `CLAUDE.md` is scoped to a legacy-parity mission that is NOT this work.** Do not
  let it redirect Case Tracker work toward porting the old app.
- Ask via the AskUserQuestion modal rather than inline prose, and recommend one option rather than
  surveying all of them.

## Stop and ask me before proceeding

Handoff section 7 lists three open decisions. Put them to me together in ONE modal, each with your
recommendation, before writing any plan:

1. Whether `reply-4-sent.md` was actually sent (its filename and its first line disagree, and if it
   was not sent, Levon does not know about several things we believe he knows).
2. Whether a type-change cancel-and-rebook procedure actually exists -- the code says a type change
   is an in-place edit, which contradicts what we told Levon.
3. What "requested at" should mean for N4 now that phase 4c introduced consent rounds.

These are judgment calls about what a third party has been told and what our own workflow does; you
cannot settle them from the code alone, and guessing wrong sends a wrong answer to an external
team.

## Definition of done for this stint

A plan file at `docs/plans/YYYY-MM-DD-case-tracker-payload-completeness.md` that:

- follows the structure of `docs/plans/2026-08-05-reschedule-consent-rounds.md`;
- contains zero open questions, TBDs or placeholders -- every decision resolved with me first,
  because an unresolved decision becomes an empty variable at build time;
- anchors every task to an exact `file:line` plus the existing pattern it mirrors;
- states acceptance in EARS form ("WHEN <trigger>, THE SYSTEM SHALL <behavior>");
- ends with a validation loop of exact runnable commands covering every layer the change touches,
  not just a build -- a build proves it compiles, only the test command proves nothing broke.

Do not start implementing until I have approved the plan.

## How to work

You choose the research approach and the task decomposition. Read what you need. Where the handoff
and the code disagree, the code wins and the handoff is wrong -- tell me when you find that.
