# AppointmentChangeLogs -- change-log over ABP audit (Group K)

Read-only change-log view + intake-changed email for appointment intake entities
(G-02-01/02/03, G-10-05). NOT a CRUD entity: there is no `AppointmentChangeLog` table -- this
feature reads ABP audit (`EntityChange`) and diffs in-memory at update time. See ADR-012.

## What lives here

| File | Purpose |
|---|---|
| `AppointmentChangeLogsAppService.cs` | Aggregates ABP `EntityChange` for an appointment + its child entities; redacts via the policy; exposes per-appointment + global filtered/paged list. `[RemoteService(false)]`, `[Authorize(AppointmentChangeLogs.Default)]`. |
| `AppointmentChangeLogBuilder.cs` | Pure: explodes raw entity changes to per-field redacted DTO rows. Unit-tested. |
| `AppointmentAuditedEntities.cs` | The 5 audited intake entity FQNs + friendly labels + the global-scan list. |
| `RawEntityChange.cs` | Framework-agnostic projection of ABP `EntityChange`, so the builder stays DB-free + testable. |

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
  `IntakeChangedEmailHandler` -> `AppointmentChangeLogs` template (diff table) to all
  stakeholders, plus `AppointmentRescheduleRequestByAdmin` one-shot when date/time changed.
- **Angular:** the per-appointment viewer (`appointments/appointment-change-logs/`) GETs
  `by-appointment/{id}`; the global list page (`appointment-change-logs/`) uses `getList`.

## Gotchas

- ABP `EntityChange.EntityId` is a STRING; `EntityChangeType` is in `Volo.Abp.Auditing`
  (NOT `Volo.Abp.AuditLogging`); controllers need `using Asp.Versioning;` for `[ControllerName]`.
- The audit-query glue (`IAuditLogRepository`) is verified live, not unit-tested -- ABP writes
  `EntityChange` rows itself, so seeding them in SQLite is impractical. The pure builder +
  policy + intake diff ARE unit-tested.
- The view omits a "Who" column (no username without an `AuditLog` join) -- a refinement.
- The email diff covers date/time, panel #, due date; FK-field name resolution is a refinement.

## Related

- docs/decisions/012-audit-change-log-redaction.md
- docs/parity-research/G-02-01.md, G-02-02.md, G-02-03.md, G-10-05.md
