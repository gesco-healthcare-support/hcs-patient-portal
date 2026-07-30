---
status: draft
date: 2026-07-30
---

# Defer the intake push until packets settle

## Goal

Send the Case Tracker ONE complete intake per approval, carrying the generated packets and any
already-accepted uploaded documents, instead of the current two pushes ten seconds apart.

## Why: measured, not theorised

First live approval in production (falkinstein A00004, appointment
`df9c4239-56cd-82e6-6f58-3a22c6ad1093`, 2026-07-30) queued TWO intake rows:

| Row | Queued | Payload `updatedAt` | Size | Contains |
| --- | --- | --- | --- | --- |
| `748E4AD0..` | 19:55:01.735 | 19:55:01.307Z | 2650 | no packets |
| `2D77F7D1..` | 19:55:11.102 | 19:55:02.317Z | 4298 | packets |

Sequence: `CaseTrackerIntakeOnApprovedHandler` enqueued on approval; one second later packet
generation modified the appointment; `AppointmentChangedHandler` then re-pushed with the newer
`UpdatedAt`, so the idempotency key legitimately did NOT collapse them.

**This is not a dedup bug.** The key is versioned by `UpdatedAt` and the two states genuinely
differed. It is the designed re-push-on-any-change behaviour, with packet generation as the change.
The waste is that the first message is superseded within seconds of being sent.

The older row was marked `Resolved` on 2026-07-30 (`LastError` records why) so only the complete
payload remains queued. falkinstein's push is still DISABLED; nothing has been sent.

## Decision (Adrian, 2026-07-30) -- REVERSES a locked contract decision

Contract §H currently states intake pushes IMMEDIATELY on approval, packets stream in afterwards,
"No all-3 aggregator". `CaseTrackerIntakeOnApprovedHandler`'s doc comment gives the original
reasoning: waiting "would need a stateful all-three-generated aggregator plus a timeout path for a
permanently failed packet, and would buy the receiver nothing it needs."

That reasoning is now overridden, on evidence: the receiver DOES get something from waiting -- one
message instead of two, the first of which is immediately stale. Accepted trade-off: the Case
Tracker learns of an approval minutes later rather than seconds later.

## Context / anchors

- Trigger to move: `Application/Integration/CaseTracker/Handlers/CaseTrackerIntakeOnApprovedHandler.cs`
  (subscribes `AppointmentApprovedEto`; lives in Application because that ETO is in
  Application.Contracts and Domain cannot reference upward).
- Re-push on change: `Domain/Integration/CaseTracker/Handlers/AppointmentChangedHandler.cs:50` and
  `:80`. This is what fires the second push and must NOT be removed -- it is the mechanism for
  genuine later edits.
- REUSE, do not reinvent: `Domain/Integration/CaseTracker/PacketSetPolicy.cs` already implements
  `AllKinds`, `IsComplete(packets)` and `ShouldRelease(packets, cutoffUtc)` -- i.e. "all kinds
  generated OR past a cutoff". `CaseTrackerReconciliationJob` already applies it with
  `PacketReleaseAfterMinutes = 30` / `PacketReleaseBatchSize = 50` for the document feed.
- Packets-complete path already exists: `Domain/Integration/CaseTracker/Handlers/PacketsCompleteHandler.cs`.
- Enqueue path: `Domain/Integration/CaseTracker/CaseTrackerIntakeQueue.cs` -- note `ScheduleDrain`
  defers the Hangfire enqueue to `CurrentUnitOfWork.OnCompleted`; any new trigger MUST keep that or
  the worker can dequeue before the row commits.
- Publish guard: `CaseTrackerPublishPolicy.IsPublished` -- excludes only Pending / Rejected /
  InfoRequested.

## Open decisions -- RESOLVE WITH ADRIAN BEFORE WRITING CODE

1. **Settle rule.** Reuse `ShouldRelease` with a cutoff (send whatever exists once all kinds are
   generated OR the cutoff passes), or wait strictly for `IsComplete` with no timeout? A strict wait
   means a permanently failed packet blocks the intake forever, which is why the cutoff exists.
   Recommendation: reuse `ShouldRelease`.
2. **Cutoff value.** The document feed uses 30 minutes. Same for intake, or shorter given an
   approved appointment nobody has been told about is worse than a slightly incomplete payload?
3. **Does the intake payload already include accepted uploaded documents?** UNVERIFIED -- A00004 had
   no uploads so tonight's data cannot tell us. Check `IntakePayloadBuilder` / the documents resolver
   before assuming this half needs work.
4. **Does the approval still enqueue anything at all?** If the intake now waits, the Case Tracker has
   no record between approval and packet settle. Acceptable, or does something lighter need to go
   immediately?

## Tasks (draft -- do not execute until the decisions above are closed)

### T1 -- move the trigger (approach: code)

Stop `CaseTrackerIntakeOnApprovedHandler` enqueueing directly. Enqueue when the packet set settles,
gated by `PacketSetPolicy`. Likely home: extend `PacketsCompleteHandler`, plus a sweep pass for the
cutoff case (mirroring `CaseTrackerReconciliationJob.ReleaseStalledPacketSetsAsync`). Keep the
`OnCompleted` deferral.

### T2 -- keep later edits working (approach: code)

`AppointmentChangedHandler` must still re-push genuine post-approval edits. Verify it cannot fire
BEFORE the first intake has been queued, or an edit during packet generation would push an intake
that the settle path then duplicates.

### T3 -- payload completeness (approach: code)

Confirm/ensure accepted uploaded documents appear in the intake `documents[]` alongside packets.

### T4 -- tests (approach: tdd)

Domain tests: settle-when-complete; release-at-cutoff with a failed kind; no push before settle; a
post-settle edit still re-pushes; exactly ONE intake row per approval in the happy path. The last is
the regression test for tonight's finding.

### T5 -- contract + Levon (approach: code)

Revise §H with a dated block: one intake per approval, sent once packets settle, carrying packets and
accepted documents. State the latency change. Tell Levon -- he was told the opposite (empty
`documents` on approval, packets via the document feed).

## Acceptance (EARS)

- WHEN an appointment is approved, THE SYSTEM SHALL NOT enqueue an intake push until the packet set
  is complete or the cutoff has elapsed.
- WHEN the packet set settles, THE SYSTEM SHALL enqueue EXACTLY ONE intake row carrying the generated
  packets and every accepted uploaded document.
- WHEN one packet kind remains Failed past the cutoff, THE SYSTEM SHALL push with the packets that do
  exist rather than withhold indefinitely.
- WHEN a published appointment is edited after its intake has been queued, THE SYSTEM SHALL still
  re-push.

## Validation loop

    dotnet format HealthcareSupport.CaseEvaluation.slnx --verify-no-changes
    dotnet build HealthcareSupport.CaseEvaluation.slnx -c Release -warnaserror
    dotnet test HealthcareSupport.CaseEvaluation.slnx
    python .claude/scripts/verify_structure.py

Then live: approve a synthetic appointment in falkinstein and confirm EXACTLY ONE Pending intake row
appears, containing packets. Adrian gates enabling the clinic and sending.

## State this leaves behind

- A00004 is Approved in falkinstein with one Pending intake row (4298 chars, packets included).
- falkinstein push is DISABLED. Nothing has been sent to the Case Tracker.
- Their token is verified working against `GET /api/intake/health` (all three states).
- `secrets/env.prod` on the box carries `CASE_TRACKER_BASE_URL` + `CASE_TRACKER_INTAKE_TOKEN`;
  `CASE_TRACKER_INTEGRATION_TOKEN` is deliberately empty so reconcile fails closed.
