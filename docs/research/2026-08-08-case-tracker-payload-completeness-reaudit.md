---
feature: Case Tracker payload completeness (epic phase 6) -- RE-AUDIT
date: 2026-08-08
status: research
base-branch: main
related-issues: []
---

# Phase 6 re-audit against current `main`

The phase 6 plan lives ONLY on branch `feat/case-tracker-payload-completeness`
(`docs/plans/2026-08-05-case-tracker-payload-completeness.md`, 673 lines, 10 tasks). It was written
against `main` at `2ce2ef3f` and is now **five merges stale**: 4d (#430), 4e (#431), the employer fix
(#432), phase 5 (#433) and the phase 5 follow-up (#436) have all landed since. `main` is `ee4f11a2`.

This packet re-checks every task against the code as it stands. It does NOT rewrite the plan.

## Task-by-task state

| Task                                | Plan says                                                             | Actual state on `ee4f11a2`                                                                                                                                                                                                                                                                       |
| ----------------------------------- | --------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| T1 `DecidedAt` + `MarkDecided`      | ADD the column and a domain method                                    | **COLLIDES.** 4d already shipped `DecidedAt` (`AppointmentChangeRequest.cs:118`), same entity, same name. It has a PUBLIC setter and is stamped in `PersistChangeRequestAsync` with `??=`, NOT the plan's `protected set` + `MarkDecided(outcome, decidedById, nowUtc)`.                         |
| T2 EF config + dual migrations      | ADD `Added_ChangeRequestDecidedAt` to both sets                       | **COLLIDES.** Both exist and are applied: `20260806172525` (host), `20260806172601` (tenant). **The BACKFILL was never written** -- that half is still real work and needs its OWN new migration, since the merged one is already applied.                                                       |
| T3 patient address on the DTO       | ADD `Address, Street, City, State, ZipCode` to `IntakePatientSection` | **UNTOUCHED and still correct.** `IntakePatientSection` has no address fields at all (`IntakePayload.cs`). Caution: `IntakeLocationSection` DOES have `Address`/`City`/`ZipCode` -- those are the CLINIC's, easy to mistake for the patient's.                                                   |
| T4 resolve address + state name     | Resolve from the patient, state as a NAME                             | **UNTOUCHED, and the precedent already exists.** `Patient` carries `Address:51`, `City:54`, `ZipCode:57`, `Street:65`, `StateId:76`. `PartyDetailResolver.StateNameOrNull(:94-95)` already turns a `StateId` into a name for the attorney sections -- reuse it rather than writing a second one. |
| T5 `ChangeRequestSideWire`          | CREATE the wire enum                                                  | **PARTLY DONE ALREADY.** The DOMAIN concept exists: `ChangeRequestSide { SideA = 1, SideB = 2 }` and `AppointmentChangeRequest.RequestingSide:127`. Only the WIRE mapping file is missing. Side A = Patient + Applicant Attorney; Side B = Defense Attorney + Claim Examiner.                    |
| T6 attribution + timestamps on DTO  | ADD `changeRequestedBy`, `changeFinalizedAtUtc`, etc.                 | **UNTOUCHED.** No such field anywhere in `Payload/`.                                                                                                                                                                                                                                             |
| T7 resolve attribution + timestamps | Resolve them                                                          | **UNTOUCHED, and every input exists.** `SubmittedByUserId:133`, `ApprovedById:73`, `RejectedById:71`, `DecidedAt:118`, `RequestingSide:127`.                                                                                                                                                     |
| T8 contract updates                 | Amend sections A / E2                                                 | **NEEDS RE-ANCHORING.** 4e rewrote sections A and E2; phase 5 added section K and restruck the NEVER-sent list. The plan quotes text that no longer exists.                                                                                                                                      |
| T9 correct two stale documents      | Fix `reply-4-sent.md` marker + `case-tracker-open-items.md`           | Untouched, small. NOTE: `reply-4-sent.md` records that `CaseTracker:IntegrationToken` is STILL EMPTY in production (item I7) -- independently true and worth acting on.                                                                                                                          |
| T10 epic roadmap row                | Add the phase 6 row                                                   | **ALREADY DONE** -- added during phase 5's T9.                                                                                                                                                                                                                                                   |

## Resolved: the D2 question that was left open

The earlier handoff flagged as UNVERIFIED that phase 6 assigned D2 (every push is a full snapshot),
D3 (status list five to seven) and D4 (reschedule link semantics) to 4e.

**D2 is NOT documented.** Verified by search: the contract contains no statement that every push
carries a complete snapshot. 4e delivered D4 and part of D3; D2 was dropped. It therefore returns to
phase 6's scope, or needs explicitly abandoning.

## What actually remains

Real, unbuilt work: **T3, T4, T6, T7** (the payload additions), the **backfill half of T2**, the
**wire mapping half of T5**, plus **T8/T9** documentation.

Already done or superseded: T1, the migration half of T2, the domain half of T5, T10.

The plan's own sequencing note that "T8 runs last behind a rebase because 4e is rewriting the same
contract file" is SPENT -- 4e is merged.

## Open decisions (for the design step)

1. **`MarkDecided` -- adopt or abandon?** The plan's design (status, actor and timestamp set
   together behind a protected setter so they cannot drift) is arguably better than what 4d shipped
   (public setter, stamped with `??=` in one seam). Refactoring toward it is a CHOICE: the shipped
   version is merged and live-gated. Adopting it means touching a merged 4d seam.
2. **The `DecidedAt` backfill -- ship it, and with what cutoff?** Their SQL is
   `UPDATE AppAppointmentChangeRequests SET DecidedAt = LastModificationTime WHERE RequestStatus <> 25
AND DecidedAt IS NULL AND LastModificationTime IS NOT NULL` (25 = Pending; the enum is
   non-contiguous and OLD-faithful). `LastModificationTime` bumps on ANY write, so for a request
   touched after its decision the backfilled value is WRONG-but-plausible. Decide whether an
   approximate historical timestamp beats a null.
3. **D2 -- document "every push is a full snapshot", or drop it?** It never landed in 4e.
4. **Patient address -- what does `Address` vs `Street` mean?** Both are separate nullable columns
   with no recorded convention for which is line 1, and there is NO unit/apartment field. The
   contract must say so rather than implying a structure that does not exist.
5. **Scope vs the deploy.** Five phases sit merged and undeployed. Phase 6 adds payload FIELDS,
   which is exactly the kind of change Case Tracker's receiver must be ready for -- worth deciding
   whether it ships with the current train or after it.

## Known open issue carried in from phase 5

Reval rejections lose their localized message on `POST /api/app/appointments/create-reval/{cn}`:
the right error CODE comes back but the body says "An internal error occurred during your request!".
PRE-EXISTING (verified with a control against the older `RevalSourceNotApproved` code), so not
phase 6's to fix, but it means the distinct advice those codes carry never reaches a user.
