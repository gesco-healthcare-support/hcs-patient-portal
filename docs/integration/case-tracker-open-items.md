# Case Tracker integration -- open items

What we owe the Case Tracker team, as of 2026-08-05. Derived from the correspondence in
`case-tracker-correspondence/` and the code facts in `case-tracker-verified-findings.md`.

SCOPE: this file EXCLUDES the reschedule/cancel/calendar epic phases, which are tracked in
`docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md` and worked by a separate
session. Items below are either unphased or already shipped-pending-deploy.

## Epic status for context (not this file's work)

This file used to carry its own copy of the phase table and it went stale -- it still showed 4c as
"plan being written" and 4d, 4e and 5 as TODO long after all three had merged. The copy is DELETED
rather than corrected, because two tables drift and one does not.

**Source of truth: `docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md`.** As of
2026-08-12 all six phases are merged; phases 1-5 are deployed and phase 6 is not.

## 1. DEPLOY -- RESOLVED 2026-08-11

The four things Levon had been waiting on are now LIVE, verified against git rather than assumed
(each commit confirmed an ancestor of the deployed `2c82c358`):

- `data.doctor.id` (PR #413, `d658382b`) -- DEPLOYED. His staff no longer have to pick the doctor by
  hand, which was a live daily cost while it sat merged-but-undeployed.
- Volume cap, 100/office/hour (PR #413, `d658382b`) -- DEPLOYED.
- `cancellationReason` (PR #414, `baa1fee6`) -- DEPLOYED.
- `billingStatus` = `NO_BILL` / `LATE` / `NONE` (PR #414, `baa1fee6`) -- DEPLOYED.

Two consecutive emails promised these "on our next deployment" and that deployment happened. TELL HIM
they are live: he has no way to know, and his staff are the ones absorbing the manual work.

Still undeployed: phase 6 (`86ab3e80`), which is the only commit on `main` and not on `development`.

## 2. CODE -- new, small, in no epic phase

| #   | Item                                                  | Status | Why                                                                                                                                                                                                                                                                       |
| --- | ----------------------------------------------------- | --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| N1  | Who cancelled: party type at minimum, name where held | **PARTLY DELIVERED, phase 6 (2026-08-08)** -- `changeRequestedBySide` gives the party TYPE (Side A / Side B). The NAME is still not sent.                                                                                                                                 | The one remaining gap of his three attribution asks. ORPHANED -- Phase 2 shipped and closed without it. Needed for his proof-of-service and billing attribution. |
| N2  | Explicit AME auto-cancel flag                         | **CLOSED BY REMOVAL 2026-08-08.** There will be no auto-cancels to flag: the nightly job no longer cancels anything. It stamps an overdue marker and emails internal staff, and a human decides. Levon must be told the flag is moot rather than pending -- see update-5. | He has asked for a boolean rather than matching the reason constant. Small; pairs naturally with N1.                                                             |
| N3  | Patient postal address on the intake payload          | **DELIVERED, phase 6 (2026-08-08)** -- `street` / `unit` / `city` / `state` / `zipCode`. NOTE this row was RIGHT that we already hold all five; the phase 6 draft plan wrongly claimed the columns were ambiguous with no unit field.                                     | The ONLY address gap. We already hold street/unit/city/state/zip; `IntakePatientSection` simply has no address fields. Blocks his proof-of-service document.     |
| N4  | Requested-vs-finalized timestamps, UTC + local zone   | **DELIVERED (UTC), phase 6 (2026-08-08)** -- `changeRequestedAtUtc` + `changeFinalizedAtUtc`. Local-zone variants were NOT added; the contract carries a single IANA `timeZone` for the appointment.                                                                      | A notice period measured from the wrong end over-bills someone who gave timely notice.                                                                           |

## 3. CODE -- requested, NOT promised

| #   | Item                                | Note                                                                                                                                                                                            |
| --- | ----------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| R1  | Reschedule sequence / count         | For an appointment moved more than once. Noted only.                                                                                                                                            |
| R2  | Type-change link or marker          | A type change after approval is cancel-and-rebook; the workflow KNOWS the new booking replaces the old, unlike a general rebooking. His argument is sound.                                      |
| R3  | Reason on a rejected change request | Found while answering his Q3. `cancellationReason` is populated only for real cancellations, so a rejected reschedule shows the status revert with no explanation. He has not asked for it yet. |

## 4. DOCS

- **D1. STILL OPEN** -- phase 6 did not write this down; it remains inferred rather than stated.
  Contract: a non-200 reconcile response carries NO document information and must never drive
  pruning. He has ACCEPTED this and is not asking us to weaken the 404 ambiguity, so it is the agreed
  fix -- write it down rather than leave it inferred.
- **D2. DONE, phase 6 (2026-08-08)** -- now stated in contract section A. It had been assigned to 4e,
  which shipped without it. Contract: record that every push is a FULL SNAPSHOT and never slim. This is what makes his
  null-writing upsert safe, and it is currently only true by construction rather than by statement.
- **D3.** Contract: status list five -> seven when the `Rescheduled*` values become reachable (4e).
- **D4.** Epic roadmap: the reschedule link is load-bearing for PATIENT FILING, not cosmetic -- because
  we send no patient identifier, following the link is the only way they can read their own patient id
  for a rescheduled appointment.

## 5. DATA -- no code

- **A1.** Populate `Location.FacilityId` on both production clinics. Empty today; his staff type it
  manually on every intake.

## 6. INFRA -- verified NOT started on the server, 2026-08-03/05

Strict order: I3 -> I4 -> I5. I2 is pointless before I1.

| #   | Item                                                                                                         | State                                                                                                                                                                                                                           |
| --- | ------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| I1  | Publish MinIO outside the container network                                                                  | No `ports:` mapping at all in `docker-compose.prod.yml`                                                                                                                                                                         |
| I2  | MinIO route in the reverse proxy                                                                             | No minio/9000/9001 reference in the nginx template. UNBLOCKED: Levon confirmed `.35` is his box making the OUTBOUND connection, so a hostname route works and the existing wildcard covers it -- no bare-IP certificate problem |
| I3  | Create `case-tracker-documents` bucket                                                                       | Only `case-evaluation-documents` exists                                                                                                                                                                                         |
| I4  | Scoped MinIO policy: read-only on `case-evaluation-documents`, read/write+delete on `case-tracker-documents` | Zero custom policies; only MinIO's five built-ins. BLOCKED BY I3                                                                                                                                                                |
| I5  | MinIO user/key for that policy, secret out of band                                                           | Zero non-root users. BLOCKED BY I4                                                                                                                                                                                              |
| I6  | Joint DNS request with their IT (Rod)                                                                        | Not visible on the server. **GATES THEIR DEPLOY** -- highest leverage of the six                                                                                                                                                |
| I7  | Issue `CaseTracker:IntegrationToken`                                                                         | Still EMPTY in production, failing closed, so the reconcile GET rejects everything. His whole reconcile section concerns an endpoint he cannot currently call                                                                   |

## 7. COORDINATION

- **X1.** Delete synthetic `A00005` from their live Pending Intakes queue. Agreed with him; a synthetic
  appointment in a live queue invites someone to confirm it and create a junk case and patient folder.
- **X2.** Compare notice recipient lists (his Q15). The portal ALREADY notifies on cancellation via
  `ClinicalStaffCancellationEmailHandler`, `JdfAutoCancelledEmailHandler` and
  `StatusChangeEmailHandler`, so his paperwork may duplicate our email. He suggested a call.

## 8. DROPPED

- Unconditional document re-send on the feed after any intake. He declined it: a superseding push
  already carries the complete set and his push path never prunes.

## Known gaps in our own testing

- `CountSentSinceAsync` (the volume cap's EF query) has NO test. Unit tests substitute the repository,
  so the `Status == Sent` filter, the `SentAt >= sinceUtc` comparison and the tenant filter are all
  unverified. Its two failure modes are silent and opposite: never trip (no protection) or never
  release (delivery stuck). A coverage gate flagged this and it was overridden deliberately.
  `EfCoreIntegrationOutboxRepositoryTests` exists to mirror.
