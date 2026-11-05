# EmailStops -- staff stop emails to an external person

## What lives here

Office decisions of 2026-11-05. Staff can stop every email the portal sends an external person, for one
case or for all cases at every practice. A stop silences everything that person would get:

- case emails;
- appointment letters (packets);
- Agree/Decline links;
- reminders;
- CC lines.

Self-service account mail still goes out (`EmailStopPolicy.NeverStoppedTemplateCodes`, pinned by
`EmailStopGuardTests`).

| File | Purpose |
|---|---|
| `EmailStopPolicy.cs` | THE one decision point: loads the stops for a send or a drain batch (one host and one office query) |
| `IEmailStopPolicy.cs` | Seam for the policy, so the sending paths unit-test without a database |
| `EmailStopLookup.cs` | The loaded answer per address, plus `EmailStopAddressing` (a stopped To hands the email to the first remaining CC) |
| `EmailStopAddress.cs` | How an address is normalized for matching (log lines never carry an address) |
| `GlobalEmailStop.cs` | "All cases" stop: HOST table `AppEmailStops`, read with `CurrentTenant.Change(null)` |
| `AppointmentEmailStop.cs` | "This case only" stop: office table `AppAppointmentEmailStops`, never a column on the appointment |

## Rules

- Every sending path asks `EmailStopPolicy`:
  - the notification dispatcher, before an outbox row is written;
  - the outbox drain, for rows written before the stop;
  - the two reminder jobs that enqueue the send job directly.
- Every dispatch names its case (`NotificationCase.Of(...)`). `EmailStopGuardTests` fails a new call that
  passes `NotificationCase.None` from a file not on its reviewed list.
- Turning emails back on is a soft delete, so who stopped it, when and why stay on record.
- Only external addresses can be stopped. The staff API (`Application/EmailStops`) refuses the office inbox,
  the technical alert list, host accounts and office staff-role accounts.
