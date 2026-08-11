---
feature: Replace the JDF auto-cancel with a staff alert
date: 2026-08-08
status: draft
base-branch: main
related-issues: []
---

# Stop auto-cancelling AME appointments; alert staff instead

Closes open item N2 (`docs/integration/case-tracker-open-items.md`) by REMOVING the behaviour rather
than adding the flag Levon asked for. Adrian, 2026-08-08: "auto-cancel without staff or anyone's
involvement seems like a risky thing and we should not do that."

## What happens today

`JointDeclarationAutoCancelJob` (`Domain/Notifications/Jobs/`, cron `0 6 * * *`, id
`appt-jdf-auto-cancel`) runs daily across every office. For each **Approved AME** appointment with no
uploaded Joint Declaration Form whose due date is at or past
`SystemParameter.JointDeclarationUploadCutoffDays`, it:

1. sets `AppointmentStatus = CancelledNoBill` **directly**, bypassing the state machine (a documented
   strict-parity exception -- there is no `Approved -> Cancelled*` edge);
2. writes `CancellationReason = "The Joint Declaration Form was not uploaded before the required
   deadline."`;
3. publishes `AppointmentStatusChangedEto` (drives notifications + audit) and
   `AppointmentAutoCancelledEto` (drives `JdfAutoCancelledEmailHandler`, which mails stakeholders).

No human is involved at any point. That is what is being removed.

## Decisions (Adrian, 2026-08-08, via modal)

- **Decision 1 -- BOTH an email and an in-app flag.** Email reaches someone who is not looking at the
  portal; the flag persists so it is still visible days later and carries state. Email alone gets
  buried and leaves no way to see which appointments are still waiting; a flag alone is invisible to
  anyone not already in that screen. Once nothing cancels automatically the appointment sits
  indefinitely, so the signal must survive being missed once.
- **Decision 2 -- the appointment STAYS `Approved`, carrying an overdue marker.** No status changes
  without a human. Consequence worth stating: Case Tracker sees no status change at all, so the
  integration needs no contract change and Levon needs no code change.

## Consequences to state plainly

- **Case Tracker STOPS receiving these cancellations.** Today an auto-cancel pushes
  `CancelledNoBill` with the reason text. After this, nothing is pushed until a human cancels. This is
  a behaviour change on their side even though no field changes -- it belongs in the update email
  (`update-5-DRAFT.md` section 8 already flags it as coming).
- **N2 as Levon framed it is MOOT.** He asked for a boolean marking an auto-cancel; there will be no
  auto-cancels to mark. Tell him rather than silently closing it.
- Appointments that would previously have been cancelled will now accumulate as Approved-but-overdue.
  Nobody has seen that backlog before; the flag is what makes it visible.

## All needed context

| Need | Anchor |
| ---- | ------ |
| The job to rewrite | `Domain/Notifications/Jobs/JointDeclarationAutoCancelJob.cs` |
| Cutoff predicate (already pure + unit-tested) | `Domain/AppointmentDocuments/JointDeclarationCutoff.IsAtOrPastCutoff` |
| Existing stakeholder mail handler | `JdfAutoCancelledEmailHandler` (filters on `AutoCancelReason`, ordinal compare) |
| Staff-email precedent | `StatusChangeEmailHandler` -- the no-show staff notice it already sends |
| Dual-context migration pairing | 4d's `Added_ChangeRequestDecidedAt` in both migration sets |
| Row + detail rendering | `internal-appointments.component.ts`, `internal-appointment-detail.component.ts` |

### Gotchas

- `Appointment` is `IMultiTenant` and mapped in BOTH `CaseEvaluationDbContext` and
  `CaseEvaluationTenantDbContext`. A new column needs a migration in BOTH sets or offices break.
- `AutoCancelReason` (`"JDF-not-uploaded"`) is a ROUTING DISCRIMINATOR, not a message.
  `JdfAutoCancelledEmailHandler` ordinal-compares it. Changing or removing it silently stops that
  email firing -- decide deliberately whether that handler survives at all.
- The job currently bypasses the state machine on purpose. Removing the status write removes the need
  for that exception entirely, which is a simplification, not a regression.
- The job is per-office and iterates tenants; the flag write must stay inside the office scope.

## Tasks

### T1 -- the overdue marker on the entity

- what: ADD `JointDeclarationOverdueAt` (`DateTime?`) to `Appointment`. Null means not overdue. Set
  when the deadline passes; CLEARED when a JDF is later uploaded, so the flag tracks reality rather
  than latching forever. Map it in BOTH DbContexts.
- approach: tdd
- acceptance (EARS): WHEN the cutoff passes with no JDF, THE SYSTEM SHALL stamp the marker once and
  SHALL NOT overwrite an existing stamp on later runs. WHEN a JDF is subsequently uploaded, THE SYSTEM
  SHALL clear the marker.

### T2 -- paired migrations

- what: CREATE the migration in BOTH `Migrations/` and `TenantMigrations/`.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL have the column in both sets, and `has-pending-model-changes`
  SHALL report no drift on either context.

### T3 -- the job stops cancelling and starts flagging

- what: MODIFY `JointDeclarationAutoCancelJob` -- REMOVE the status write, the `CancellationReason`
  write, and the `AppointmentStatusChangedEto` publish. Stamp `JointDeclarationOverdueAt` instead.
  Keep the tenant loop, the cutoff predicate and the logging. Decide the fate of
  `AppointmentAutoCancelledEto` + `JdfAutoCancelledEmailHandler`: the event no longer describes a
  cancellation, so either retire the handler or repoint it -- do NOT leave it firing a
  "your appointment was cancelled" mail at stakeholders when nothing was cancelled.
  RENAME the job to match what it does; the id `appt-jdf-auto-cancel` is a persisted Hangfire
  recurring-job key, so a rename must remove the old registration or two jobs will run.
- approach: tdd
- acceptance (EARS): WHEN the cutoff passes, THE SYSTEM SHALL leave `AppointmentStatus` unchanged and
  SHALL NOT publish a status change. THE SYSTEM SHALL NOT send any stakeholder cancellation email.

### T4 -- the staff email

- what: A staff-facing notice listing the overdue appointments for that office, sent once per
  appointment (not daily forever). Mirror the no-show staff notice in `StatusChangeEmailHandler` for
  recipients and template shape.
- approach: test-after
- acceptance (EARS): WHEN an appointment first becomes overdue, THE SYSTEM SHALL notify office staff
  exactly once. WHERE it remains overdue on later runs, THE SYSTEM SHALL NOT re-notify.

### T5 -- the in-app flag

- what: Surface the marker on the appointment ROW and the appointment DETAIL for internal staff.
  Visible without opening a filter, and worded as an action ("Joint Declaration Form overdue"), not a
  status.
- approach: test-after
- acceptance (EARS): WHEN an appointment is overdue, THE SYSTEM SHALL show the marker in the list and
  on the detail. WHEN it is not, THE SYSTEM SHALL show nothing extra.

### T6 -- docs

- what: Epic/open-items note that N2 is CLOSED BY REMOVAL, and the update email gains the behaviour
  change for Levon.
- approach: code

## Validation loop

```
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
```

Frontend (T5 touches Angular):

```
export CHROME_BIN="/c/Program Files/Google/Chrome/Application/chrome.exe"
npx prettier --check <changed files>
npx eslint <changed files>
npx ng build
npx ng test --watch=false --browsers=ChromeHeadless
```

Migrations: `has-pending-model-changes` on BOTH contexts.

Mutation checks (required):

- Make the job set `CancelledNoBill` again; confirm the "status unchanged" test fails. That pair is
  the whole point of this change.
- Make the marker latch (never clear on upload); confirm the clear test fails.
- Make the staff email fire on every run; confirm the once-only test fails.

## Live gate

Needs a seeded AME appointment with no JDF past the cutoff. Assert: status still Approved, marker set,
one staff email, the flag visible in list and detail, and NO integration outbox row.

## Risk / rollback

Blast radius: one recurring job, one new nullable column, one email, and two UI surfaces.

1. **Appointments will now pile up.** The behaviour being removed was doing real work; the backlog it
   was silently absorbing becomes visible. That is the point, but the first run may surface more than
   expected -- worth looking at the count before enabling the email.
2. **A persisted Hangfire recurring-job id.** Renaming without removing the old registration leaves
   two jobs running.
3. Rollback: revert the merge. The column would remain until a follow-up migration drops it; harmless
   while unused.
