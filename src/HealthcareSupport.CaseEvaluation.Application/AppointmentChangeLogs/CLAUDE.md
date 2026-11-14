# AppointmentChangeLogs -- change-log over ABP audit (Group K)

Read-only change-log view + intake-changed email for appointment intake entities
(G-02-01/02/03, G-10-05). NOT a CRUD entity: there is no `AppointmentChangeLog` table -- this
feature reads ABP audit (`EntityChange`) and diffs in-memory at update time. See ADR-012.

## What lives here

| File | Purpose |
|---|---|
| `AppointmentChangeLogsAppService.cs` | Aggregates ABP `EntityChange` for an appointment + its child entities; redacts via the policy; exposes per-appointment + global filtered/paged list. `[RemoteService(false)]`, `[Authorize(AppointmentChangeLogs.Default)]`. |
| `AppointmentChangeLogBuilder.cs` | Pure: explodes raw entity changes to per-field redacted DTO rows. Unit-tested. |
| `AppointmentAuditedEntities.cs` | Audited entity FQNs + friendly labels. `All` = the 5 intake types the global list scans; `PerCase` = every audited entity under one appointment (adds employer, party links, authorized users, documents, packets, info requests, change requests, consent rounds, change-request documents). |
| `AppointmentCaseEntityLocator.cs` | Finds the ids of every audited row under ONE appointment (soft-deleted rows INCLUDED, tenant filter kept, capped per type). Resolves repositories from the provider, so it stays at three dependencies. |
| `AppointmentChangeActionLabeler.cs` | Pure: one readable action per audited change ("Booked", "Approved", "Document rejected", "Consent agreed (side A)", ...). Built from enum states only, never from values, so a label cannot carry PHI. |
| `ChangeAuthor.cs`, `ChangeAuthorResolver.cs` | The "who" rules over two seams: `IChangeAuthorSource` (audit log rows, `AuditLogChangeAuthorSource`) and `IChangeAuthorDirectory` (users, `IdentityChangeAuthorDirectory`). Pure resolver, unit-tested. |
| `RawEntityChange.cs` | Framework-agnostic projection of ABP `EntityChange`, so the builder stays DB-free + testable. Declares TWO records: `RawEntityChange` and `RawPropertyChange` (one property's name, original and new value). Both are `sealed record` because ABP's own audit entities have protected setters and cannot be constructed freely in tests. |

Shared engines under `Appointments/Auditing/`: `AuditFieldPolicy` (PHI allowlist),
`AuditFieldDiff` (redacted row builder), `AppointmentIntakeDiff` (update-time diff).

## Key facts

- **No custom table.** View = ABP `EntityChange`; email = in-memory diff at
  `AppointmentsAppService.UpdateAsync`. One email per save -- no `IsMailSent` latch.
- **PHI redaction is deny-by-default** (`AuditFieldPolicy`): only allowlisted non-sensitive
  fields show old/new values; everything else is masked. Applied on BOTH the view and the email.
- **Child entities:** injury details FK to `AppointmentId`; body parts / claim examiners /
  primary insurance FK to `AppointmentInjuryDetailId`. The AppService resolves those ids and
  queries `EntityChange` per id (+ per type for the global list).
- **Email:** `AppointmentIntakeChangedEto` (carries already-redacted fields) ->
  `IntakeChangedEmailHandler`. A panel-number-only or due-date-only save sends the
  `AppointmentChangeLogs` diff table to the office inbox ONLY (2026-11-03). A save that moves the
  date sends ONE `AppointmentRescheduleRequestByAdmin` mail to the parties, the inbox and the active
  Intake Staff (the patient only once approved, or when the patient submitted), with no change log
  for that save even if the panel number changed too.
- **Who + whole case (2026-11):** the per-case page now covers every audited entity under the
  appointment, not only the intake five, and every row carries `ActionLabel`, `ChangedByName` and
  `ChangedByKind` (Staff, HostStaff, Outside, System, CaseTracker, Party). The global list gets the same
  fields (it still scans the intake five). No new table, tab or permission.
- **Who rules, in order:** an audit-log impersonator wins (a host operator acts through an office SHADOW
  login, so the log names the shadow and carries the operator as impersonator; the operator is shown, by
  one host-scope lookup per distinct operator). A shadow with no impersonator is mapped through its
  `HostOperatorUserId` stamp. Otherwise the office user: Staff for admin / IT Admin / Staff Supervisor /
  Intake Staff, else Outside. No user: `/api/integration/` is Case Tracker, the anonymous consent and
  upload-link pages are Party, anything else (background work, no audit log) is System.
- **Authors are resolved only for the rows returned** (the page, or the whole case), with one batched
  audit-log query and one lookup per distinct person.
- **Events with no visible field** (an authorized user added, a document uploaded) still yield one row
  with an empty `PropertyName`; generic "... updated" / "Details edited" changes with nothing left after
  redaction do not.
- **Angular:** the per-appointment viewer (`appointments/appointment-change-logs/`) GETs
  `by-appointment/{id}` and the shared `<app-change-log-timeline>` renders the label and the who (the
  generated proxy model predates the new fields, so `ChangeLogRow` in `clg-log.util.ts` widens it); the global list page (`appointment-change-logs/`) uses `getList`.

## Gotchas

- ABP `EntityChange.EntityId` is a STRING; `EntityChangeType` is in `Volo.Abp.Auditing`
  (NOT `Volo.Abp.AuditLogging`); controllers need `using Asp.Versioning;` for `[ControllerName]`.
- The audit-query glue (`IAuditLogRepository`) is verified live, not unit-tested -- ABP writes
  `EntityChange` rows itself, so seeding them in SQLite is impractical. The pure builder +
  policy + intake diff ARE unit-tested.
- History only exists from the day an entity became `[Audited]`. Not audited today (no history, so no
  rows): `AppointmentEmailStop` / global stop, `DoctorAvailability` (slot capacity), `IntakeOfficeAssignment`,
  `Location`, `SystemParameter`, and `AppointmentDocumentVersion`. Anonymous consent decisions are
  recorded on the audited consent round / change request, with the actor shown as "Party (email link)".
- `IChangeAuthorSource` is verified with a real `AuditLog` row in the EF rig; the impersonator / shadow
  rules run in unit tests over fakes. What only a live case proves is that ABP fills the impersonator
  columns for a host operator acting in an office.
- The email diff covers date/time, panel #, due date; FK-field name resolution is a refinement.

## Related

- docs/decisions/012-audit-change-log-redaction.md
- docs/parity-research/G-02-01.md, G-02-02.md, G-02-03.md, G-10-05.md
