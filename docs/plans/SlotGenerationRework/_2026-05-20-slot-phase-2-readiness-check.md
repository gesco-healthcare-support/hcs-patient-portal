---
status: resolved
issue: slot-phase-2-readiness-check
owner: AdrianG
created: 2026-05-20
resolved: 2026-05-20 (Q1=A, Q2=A, Q3=A, Q5=A locked; D1-D6 baked
  into plan body, including D2 CRITICAL fix for
  ReleaseSlotIfReservedAsync removal)
approach: code (research-only; updates plan body in a follow-up)
sequence: drift check for `2026-05-15-slot-rework-phase-2-domain-logic.md`
depends-on: _2026-05-20-slot-phase-1-readiness-check.md (Phase 1
  schema must ship first; this plan's `AppointmentTypes` collection
  read assumes Phase 1's M2M is live)
---

# Phase 2 (domain logic) readiness check

## Goal

Compare the Phase 2 plan against the current source on
`feat/replicate-old-app` and surface drift, contradictions, or
new questions. Output: a punch list of plan-body edits and a
small set of decisions to lock before implementation starts.

This check does NOT modify the plan body. Once the questions
below are answered, a follow-up edit pass updates the plan.

## What was verified

| Cited location | Status | Notes |
|---|---|---|
| `src/.../Domain.Shared/Enums/AppointmentStatusType.cs` | MATCHES | 13 values, ints `Pending=1 ... CancellationRequested=13`. SQL literals `3,5,6,7,8` in Phase21 migration align with `Rejected, CancelledNoBill, CancelledLate, RescheduledNoBill, RescheduledLate`. |
| `src/.../Domain.Shared/Enums/BookingStatus.cs` | MATCHES | `Available=8, Booked=9, Reserved=10`. Phase21 SQL literals (`= 8 from = 10`, `= 8 from = 9`) align. |
| `src/.../Domain.Shared/CaseEvaluationDomainErrorCodes.cs` (607 lines) | OK to extend | Three new const strings (`AppointmentBookingSlotFull`, `AppointmentBookingSlotClosed`, `AppointmentBookingSlotTypeMismatch`) do not collide with existing codes. |
| `src/.../Domain/Appointments/IAppointmentRepository.cs` (35 lines) | OK to extend | Adding `GetActiveCountForSlotAsync` + `GetActiveCountsForSlotsAsync` does not collide; existing methods are `GetWithNavigationPropertiesAsync`, `GetListWithNavigationPropertiesAsync`, `GetListAsync`, `GetCountAsync`, `FindByConfirmationNumberAsync`. |
| `src/.../EntityFrameworkCore/Appointments/EfCoreAppointmentRepository.cs` (306 lines) | OK to extend | New method implementations land alongside existing ones. Appointment entity has `AppointmentStatus : AppointmentStatusType` (confirmed at `Appointment.cs:42`). |
| `src/.../Application/Appointments/AppointmentsAppService.cs` `ValidateDoctorAvailabilityForBooking` at line 808-835 | DRIFT | Plan says "line 808-..." but cites preserved arms at "line 813-828". Reality: Location at 815-818, Date at 825-828, Time at 830-834. Plan also says "Arms 4 + 5 (location match + date-component match)" but there are THREE preserved arms (Location, Date, Time). See D1. |
| `src/.../Application/Appointments/AppointmentsAppService.cs` `CreateAsync` call site at line 643 | MATCHES | `ValidateDoctorAvailabilityForBooking(input, doctorAvailability);` -- non-async today; plan correctly states the call becomes `await`. |
| `src/.../Application/Appointments/AppointmentsAppService.cs` slot load at line 637 | MATCHES | `_doctorAvailabilityRepository.FindAsync(input.DoctorAvailabilityId)` -- plan replaces with `WithDetailsAsync(x => x.AppointmentTypes)` + `AsyncExecuter.FirstOrDefaultAsync`. |
| `src/.../Domain/Appointments/Handlers/SlotCascadeHandler.cs` (159 lines) | MATCHES `from` state | Full 14-state mapping at lines 133-158 + reschedule-swap branch at lines 70-82. Plan reduces this to a log-only stub. Note: the file is 159 lines, NOT "6 lines" -- the plan describes the reduction correctly, but the math is "delete ~95% of the file." |
| `src/.../Application/AppointmentChangeRequests/AppointmentChangeRequestsAppService.Approval.cs` lines 316, 385, 458-470 (`ReleaseSlotIfReservedAsync`) | DRIFT | Plan does NOT mention this helper. Under the NEW model where `Reserved = manually closed`, this auto-release becomes semantically wrong. See D2 -- CRITICAL. |
| `src/.../Application/Appointments/AppointmentsAppService.cs` line 810 (BookingStatusId == Available gate) | MATCHES | Plan replaces this single-status gate with the 3-arm capacity-aware predicate. |
| `src/.../Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` lines 167, 205 (`HasInFlightStatus` calls) | MATCHES | Plan acknowledges these stay; the semantic that "Reserved blocks edit/delete" is preserved. |
| `src/.../Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` line 420-423 (`HasInFlightStatus` helper) | DRIFT (latent) | Helper returns true for BOTH `Reserved` AND `Booked`. Under the new model `Booked` is unused after Phase21 backfill, so the `Booked` arm becomes dead code. See D3. |
| `src/.../Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` lines 337-338 (conflict-detection BookingStatus check) | MATCHES | Plan section 12 updates this. |
| `src/.../Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` line 389 (lookup filter on Available) | MATCHES | Plan section 8 preserves this. |
| `src/.../Domain/Appointments/CLAUDE.md` Business Rule #4 | STALE | Claims `AppointmentsAppService.CreateAsync` sets the slot to `Booked`. Actual source has NO such write (grep returned only the line-810 READ). The flip lives entirely in `SlotCascadeHandler` via `MapToSlotStatus`. CLAUDE.md drift, not plan drift, but useful context. See D4. |
| `src/.../Domain/Notifications/Jobs/JointDeclarationAutoCancelJob.cs` lines 158-161 | DRIFT (comment-only) | Comment says "drives Session A's existing SlotCascadeHandler so the slot transitions Booked -> Available." Under the new model, that transition stops happening. Comment becomes stale; behaviour stays correct (no slot mutation is needed because Booked is unused). See D5. |
| Tests folder | NO BLOCKER | No existing test references `SlotCascadeHandler` directly. New tests in the plan are additive. |

## Drift items

### D1 -- Line-number drift and arm-count miscount in `ValidateDoctorAvailabilityForBooking`

**Plan body (section 6, line 302-303):** "Arms 4 + 5 (location
match + date-component match) stay from the OLD code at line
813-828; preserve them verbatim."

**Reality:** the current method spans lines 808-835. Three arms
are preserved (not two):

- Location (lines 815-818)
- Date (lines 825-828)
- Time-range (lines 830-834)

The plan's NEW method body correctly includes all THREE arms,
but the prose miscounts.

**Fix:** in the plan body, change "Arms 4 + 5" to "Arms 4, 5,
and 6" and the cited line range to "line 815-834". The code
snippet stays correct.

**Risk:** zero -- documentation accuracy.

### D2 -- `ReleaseSlotIfReservedAsync` is incompatible with the new `Reserved` semantic (CRITICAL)

**Plan body decision #2:** "`Reserved` is manual-close-only. The
`SlotCascadeHandler` MUST NOT write `Reserved`."

**Reality:** `AppointmentChangeRequestsAppService.Approval.cs`
lines 458-470 has a private helper:

```csharp
private async Task ReleaseSlotIfReservedAsync(Guid slotId)
{
    var slot = await _doctorAvailabilityRepository.FindAsync(slotId);
    if (slot == null) return;
    if (slot.BookingStatusId == BookingStatus.Reserved)
    {
        slot.BookingStatusId = BookingStatus.Available;
        await _doctorAvailabilityRepository.UpdateAsync(slot, autoSave: true);
    }
}
```

Called from two sites (lines 316, 385) on admin-override paths
of the change-request approval flow. The comment at line 310-313
explains: "the user-picked slot was held in Reserved by Phase
16's submit. The supervisor abandoned it -- release back to
Available."

Under the NEW model where `Reserved = manually closed by
doctor's-admin`, this helper will incorrectly flip an admin's
manually-closed slot to Available. The "user-picked slot held
in Reserved" workflow no longer happens because Phase 2's
SlotCascadeHandler stub will not write `Reserved`.

**Fix:** Phase 2 plan must DELETE the `ReleaseSlotIfReservedAsync`
helper and its two call sites (lines 316, 385). The user-picked
slot was never going into Reserved under the new model, so
there's nothing to release.

**Risk:** HIGH if missed -- silent bug where admins close a
slot, an unrelated change-request approval flips it back to
Available, and the next patient books into a "closed" slot.

### D3 -- `HasInFlightStatus` includes the dead `Booked` arm

**Plan body, non-goals:** "They already guard against `Reserved`
/ `Booked` (`HasInFlightStatus` helper). The semantics change:
`Reserved` now means 'manually closed' and must continue to
block delete; `Booked` is effectively a legacy value that the
bulk-delete loop will treat the same way it always did."

**Reality:** the helper at `DoctorAvailabilitiesAppService.cs`
line 420-423:

```csharp
internal static bool HasInFlightStatus(BookingStatus status)
{
    return status == BookingStatus.Reserved || status == BookingStatus.Booked;
}
```

After Phase21 backfill flips every `Booked` slot to `Available`
and the SlotCascadeHandler no longer writes `Booked`, no slot
will ever have `Booked` status again. The `Booked` arm is dead
code.

**Options:**

- **A** -- Leave the helper as-is (plan's current text). Dead
  code is harmless; future readers see "Reserved or Booked" and
  understand the historical model.
- **B** -- Trim the helper to `status == BookingStatus.Reserved`.
  Cleaner code; matches the new model exactly.

**Recommendation:** **A**. Mixing the cleanup into the schema-
behaviour PR widens the diff and adds risk for zero functional
gain. A separate cleanup PR after Phase 2 ships can trim the
dead arm.

### D4 -- `Appointments/CLAUDE.md` Business Rule #4 is stale

**CLAUDE.md says:** "Slot booking is one-way. `CreateAsync` sets
`doctorAvailability.BookingStatusId = BookingStatus.Booked` and
saves the DoctorAvailability."

**Reality:** `grep BookingStatusId = ` on `AppointmentsAppService.cs`
returns NO writes. The flip lives entirely in `SlotCascadeHandler`
via `MapToSlotStatus`. CLAUDE.md is stale by ~2 weeks.

**Fix:** orthogonal to Phase 2 (CLAUDE.md drift, not plan drift)
but worth flagging. After Phase 2 ships and the cascade goes
log-only, the CLAUDE.md Business Rule #4 needs a full rewrite
to reflect "capacity-aware bookable predicate; slot status not
mutated on booking."

**Risk:** zero -- documentation only; corrected as part of the
Phase 2 PR's CLAUDE.md refresh.

### D5 -- `JointDeclarationAutoCancelJob` comment becomes stale

**File:** `src/.../Domain/Notifications/Jobs/JointDeclarationAutoCancelJob.cs`
lines 158-161:

```csharp
// - Publishing AppointmentStatusChangedEto manually
//     drives Session A's existing SlotCascadeHandler
//     so the slot transitions Booked -> Available
//     identically to the supervisor-cancel path.
```

Under Phase 2, the handler no longer transitions slots. The job
still publishes the ETO (other handlers still consume it for
email + audit), but the slot stays whatever status the admin
last set.

**Fix:** in the Phase 2 PR, update this comment to:

```csharp
// - Publishing AppointmentStatusChangedEto manually
//     fires the downstream notification + audit handlers
//     identically to the supervisor-cancel path.
//     Slot mutation no longer happens here; capacity-aware
//     booking treats the slot's BookingStatusId as a manual-
//     close override only.
```

**Risk:** zero -- comment-only.

### D6 -- Plan does not enumerate cross-file consumers of `MapToSlotStatus` / `ApplySlotStatusAsync`

**Plan body section 7:** "Remove the injected
`_availabilityRepository` and `_appointmentRepository` fields;
remove the helper methods `ApplySlotStatusAsync` and
`MapToSlotStatus`. The class is now log-only."

**Reality (verified via grep):** `MapToSlotStatus` and
`ApplySlotStatusAsync` are PRIVATE methods on `SlotCascadeHandler`.
No external consumers. The plan's deletion is safe.

**No drift; surfaced as positive confirmation.**

## Open questions (need decisions)

### Q1 -- What about the Phase 16 submit path that put slots into `Reserved`?

The change-request flow currently has a "Phase 16 submit" step
(per the comment at line 310-313) that holds a user-picked slot
in `Reserved` while the supervisor reviews. Phase 2 removes the
SlotCascadeHandler's `Pending -> Reserved` mapping, so the
submit no longer flips slots.

**Effect on UX:**

- Today (pre-Phase-2): a user picks a slot; submit flips it to
  Reserved; subsequent users see "this slot is held" and pick
  another.
- Phase 2 (post-rework): the slot stays Available; subsequent
  users CAN book it; the capacity gate handles overbooking
  contention via the active-count probe.

**Options:**

- **A** -- Accept the UX change (plan's implicit intent). The
  capacity model is the new source of truth; "slot held"
  becomes a UI hint computed from active-count, not a stored
  status.
- **B** -- Add an explicit "user-hold-while-pending" semantic
  back. Would require a NEW BookingStatus value (`Held`) or a
  separate hold table. Plan #3 doesn't budget for this.

**Recommendation:** **A**. Plan's intent is to treat
`BookingStatusId` as a manual-close override only. Concurrent
booking races are closed via the capacity gate's row-lock; the
"hold while pending" UX was an artifact of the single-row model.

### Q2 -- Phase21 down-migration is a no-op (forward-only)

**Plan body section 11:**

```csharp
protected override void Down(MigrationBuilder migrationBuilder)
{
    // No-op down -- the original Reserved/Booked partition is
    // not reconstructible without the SlotCascadeHandler running.
}
```

**Effect:** if Phase 2 is rolled back via `dotnet ef database
update <previous>`, the data migration is NOT reversed. All
slots stay on `Available`.

**Options:**

- **A** -- Accept the no-op down (plan's current text). The
  worst-case rollback is "every slot looks bookable"; the
  active-count probe (also reverted) returns 0; back to the
  pre-rework "Available means bookable" model.
- **B** -- Add a snapshot table approach: copy
  `BookingStatusId` pre-flip into a backup column, restore on
  Down. Adds a column + cleanup migration; complicates the PR.
- **C** -- Treat the migration as truly forward-only and refuse
  to merge until Phase 2 has been smoke-tested. Adds friction
  but keeps rollback simple (revert + database update to a
  previous migration).

**Recommendation:** **A**. The Down's effect is benign (one
slot snapshot per tenant, all set to Available), the active-
count probe is reverted alongside, and the rollback path is
"revert the commit" -- not "downgrade a production DB to an
older migration mid-feature." The plan's note that this is
worst-case "pre-rework single-row semantics" is accurate.

### Q3 -- `IUnitOfWorkManager` injection

**Plan body section 6:** "ABP provides `_unitOfWorkManager`
(inject if absent); the lock is EF-level via `FOR UPDATE`
equivalent on SQL Server (`WITH (UPDLOCK, HOLDLOCK)`)."

**Reality (verified):** `AppointmentsAppService.cs` does not
currently inject `IUnitOfWorkManager`. The class is large (1334
lines) with many existing injected dependencies. Adding one more
is fine, but the plan should make the DI change explicit so the
PR doesn't surprise the reviewer.

**Options:**

- **A** -- Add `IUnitOfWorkManager` to the constructor + readonly
  field (plan's intent, just not stated).
- **B** -- Use `[UnitOfWork(isTransactional: true)]` attribute on
  the `CreateAsync` method. ABP supports this declarative form
  and avoids the manual `Begin/Complete` boilerplate. However,
  the plan's row-lock pattern requires explicit `FromSqlRaw`
  which is independent of the UoW attribute.

**Recommendation:** **A** with an explicit "added 2026-05-20"
comment. The declarative `[UnitOfWork]` attribute does the same
thing under the hood but is less obvious to read.

### Q4 -- Does the row-lock `FromSqlRaw` pattern need a SQL Server EF Core 10 compatibility check?

**Plan body section 6:** "`FromSqlRaw` with `WITH (UPDLOCK,
HOLDLOCK)` is the standard pattern (Microsoft docs:
`docs.microsoft.com/en-us/ef/core/querying/raw-sql`)."

**Reality:** the project uses `dotnet 10.0.201` with EF Core 10
(per `Directory.Build.props`). The `FromSqlRaw` API is stable;
the hint syntax has not changed in T-SQL. Test concurrency on
real SQL Server (not LocalDB or SQLite); plan body acknowledges
this risk.

**No decision needed**; surfaced as sanity-check.

### Q5 -- `GetActiveCountsForSlotsAsync` bulk variant -- should it ship in this PR?

**Plan body section 8:** introduces a sibling bulk method
`GetActiveCountsForSlotsAsync(List<Guid> slotIds)` to avoid
N+1 round-trips on the lookup endpoint. The single-id variant
(`GetActiveCountForSlotAsync`) is also added.

**Effect:** two new methods, both used in this plan.

**Options:**

- **A** -- Ship both methods in Phase 2 (plan's current text).
  Single-id used in CreateAsync; bulk used in
  GetDoctorAvailabilityLookupAsync.
- **B** -- Ship only single-id in Phase 2; ship bulk in plan 5
  (booking UI) when the picker actually consumes
  `RemainingCapacity`. The lookup endpoint stays as-is in
  Phase 2, returning N round-trips.

**Recommendation:** **A**. The bulk method is small and the
lookup endpoint is the natural consumer; deferring just
fragments the rework.

## Decisions to lock before implementation

| # | Question | Recommendation |
|---|---|---|
| Q1 | Phase 16 "user-hold" UX | A -- accept; capacity gate replaces "slot held" semantics |
| Q2 | Phase21 down-migration | A -- no-op down is acceptable; revert-the-commit is the rollback path |
| Q3 | `IUnitOfWorkManager` injection | A -- explicit constructor injection with dated comment |
| Q5 | Bulk `GetActiveCountsForSlotsAsync` in this PR | A -- ship both methods together |

Q4 needs no decision; flagged as sanity-check.

## Risk re-rating

**Plan body lists three risks** (row-lock dialect-specific,
missed transition mapping, pre-rework Reserved data). All still
apply.

**New risks surfaced by this check:**

- **D2 (CRITICAL)**: `ReleaseSlotIfReservedAsync` and its two
  call sites must be removed in this PR. If missed, admins'
  manually-closed slots will be silently flipped back to
  Available by an unrelated change-request approval flow.
  Mitigation: add an explicit "files touched" line item in
  the plan body listing
  `src/.../Application/AppointmentChangeRequests/AppointmentChangeRequestsAppService.Approval.cs`.
- **D5 (low)**: `JointDeclarationAutoCancelJob` comment goes
  stale. Mitigation: update the comment in the same PR.

## Workflow

This readiness check informs (does not modify) the Phase 2 plan
body. Once decisions Q1, Q2, Q3, Q5 are locked and D1-D6 are
agreed, a follow-up edit pass updates
`2026-05-15-slot-rework-phase-2-domain-logic.md`. Then the plan
is ready to execute as soon as Phase 1 schema (Plan 2) ships
and the docker stack returns.

## How to apply

1. Read this doc.
2. Decide Q1, Q2, Q3, Q5 (recommendations: A, A, A, A).
3. Edit the Phase 2 plan body to incorporate D1-D6 + locked
   decisions. Specifically:
   - D1: fix line numbers + arm count in `ValidateDoctorAvailabilityForBooking` prose.
   - D2 (CRITICAL): add new section "files touched" item for
     `AppointmentChangeRequestsAppService.Approval.cs` -- delete
     `ReleaseSlotIfReservedAsync` + its two call sites.
   - D4 + D5: add a "CLAUDE.md + stale comment cleanup" item to
     the plan's files-touched list.
4. Mark this readiness check `status: resolved`.
