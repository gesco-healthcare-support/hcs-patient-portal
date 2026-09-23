# Integration -- outbound delivery to external Gesco systems

Everything the portal sends OUT to another application. Today that is one target, the Case
Tracker: when staff approve an appointment it becomes a case over there, and this folder owns
getting the appointment's data across reliably. The wire contract is agreed and frozen in
`docs/integration/case-tracker-api-contract.md` -- treat that document, not this code, as the
source of truth for field names and semantics.

## What lives here

| File | Purpose |
|---|---|
| `CaseTracker/IntegrationOutboxItem.cs` | Durable per-office message ledger. `TryClaim` leases a row, `MarkSent` is idempotent, `MarkFailed` reschedules on growing waits (5, 10, 20, then 30 minutes) until 24 hours after the FIRST failure, then dead-letters (#917), and `MarkFatal` dead-letters immediately for a response a retry can never fix. |
| `CaseTracker/IntegrationOutboxManager.cs` | Enqueue with a content key (SHA-256 over message type + appointment + version) and the atomic one-row claim (`ClaimNextDueAsync`, #917). Same content collapses only onto a Pending or Sent row (never Failed/Resolved); an intake is compared with the NEWEST intake row only (so A -> B -> A is sent), a document update with any earlier row (#915). A repeat of content takes a generation key. |
| `CaseTracker/IIntegrationOutboxRepository.cs` | Adds `TryLeaseAsync`, `GetDueIdsAsync`, `CountSentSinceAsync`, `HasIntakeAsync`, `GetUnwarnedRetryingAsync`, `StampEarlyWarnedAsync` and `AcquireAppointmentLockAsync`; the EF implementation lives in the EntityFrameworkCore layer. |
| `CaseTracker/CaseTrackerDocumentQueue.cs` | The ONLY writer of document-update rows. Writes nothing for an appointment with no intake row yet (#931), so an update can never sit ahead of its own intake. |
| `CaseTracker/CaseTrackerDeadLetterRequeuer.cs` + `DeadLetterRetryOutcome.cs` | The manual dead-letter retry (#961): rebuilds an intake as an intake and a document update as a document update from each listed document's CURRENT state (a retried deletion must be a deletion; an intake cannot remove a document), then JUDGES what came back instead of trusting it. Refuses, leaving the dead letter listed, unless a row is Pending or everything is already Sent. |
| `CaseTracker/IntegrationOutboxDrainService.cs` | Sends due rows ONE AT A TIME: gates on the enabled setting, then per row claims in a committed transaction, sends with NO transaction open, and records the result in a second short transaction (#917). Its caller must not hold a unit of work. |
| `CaseTracker/CaseTrackerClient.cs` + `ICaseTrackerClient.cs` | Typed HttpClient. Sends `X-Intake-Token` and `application/json`; never logs the token or the payload. |
| `CaseTracker/CaseTrackerPushResult.cs` | Pure classifier: 2xx succeeds, 403/404/408/429/5xx retry (403 since #917), every other 4xx is fatal. |
| `CaseTracker/CaseTrackerIntakeQueue.cs` | Shared enqueue path used by BOTH the approval trigger and the manual push, so they cannot drift. |
| `CaseTracker/CaseTrackerEndpoints.cs` | Relative paths on the Case Tracker API. |
| `CaseTracker/IntakePayloadSerializer.cs` | The one place integration JSON is produced (camelCase, nulls kept). |
| `CaseTracker/Jobs/` | `IntegrationOutboxDrainJob` (one office; skips if that office's drain lock is held), `CaseTrackerDrainKickJob` (5-min kick of every office's drain, #917), `CaseTrackerReconciliationJob` (15-min sweep across offices) and `CaseTrackerFailureAlertJob` (dead-letter alert plus the #917 early warning). |
| `CaseTracker/Payload/` | `IIntakePayloadBuilder` facade over four focused resolvers, plus the DTOs and the pure helpers (`ObjectKeyBuilder`, `DocumentEntryMapper`, `IntegrationTimestamp`, `EvaluationKindWire`). |

## Conventions

- **Nothing leaves the portal unless an office opts in.** The drain gates on
  `CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled`, which defaults to false.
  When off, due rows stay Pending with no failed-attempt cost and resume once enabled.
- **Retry for a day, and tell a human early.** Since #917 a retryable failure keeps retrying for 24
  hours from its FIRST failure, on waits of 5, 10, 20, then 30 minutes; `MaxAttempts` (100) is only
  a backstop, and rows queued before #917 keep their stored 3. Internal staff get a one-off early
  warning once a push has failed twice and is still retrying (`EarlyWarnedAt` throttles it), and
  the dead-letter email when it gives up. Fatal responses still dead-letter at once.
- **An appointment's status does not say whether its intake has gone.** Since 2026-07-30 the
  intake waits for the packet set, so an approved appointment can have no intake row for a while.
  `CaseTrackerPublishPolicy` is a status test only; "has an intake been queued" is
  `IIntegrationOutboxRepository.HasIntakeAsync`, and it counts every status. It relies on outbox
  rows never being deleted -- a purge job would silently suppress later document updates.
- **Intake and document enqueues for one appointment are serialized.** Both take
  `AcquireAppointmentLockAsync` (a transaction-owned SQL Server application lock) BEFORE they read.
  Without it, a document accepted while an intake is being built is suppressed by the document gate
  and missing from the intake. Any new path that writes Intake or DocumentUpdate rows must take the
  lock first. It is a no-op on the SQLite test provider, so tests prove order, not blocking.
- **PHI discipline.** `IntegrationOutboxItem.Payload` is a rendered intake body and DOES contain
  PHI. Never log it, never echo it into an alert or an exception message. Log lines carry
  appointment ids, target paths and status codes only.
- **No patient identifier is ever sent.** The portal is database-per-office and has no
  cross-office patient identity, and CalMed mints a new patient id per claim, so anything we sent
  would look authoritative and not be. The only linking facts are `previousAppointmentId`
  (machine) and `previousConfirmationNumber` (human aid).
- **Object keys are opaque.** A row's `BlobName` is only the logical key; `ObjectKeyBuilder` adds
  the `tenants/{tenantId}/` scope segment that ABP's MinIO provider wrote. Consumers use the
  result verbatim -- never parse or rebuild it.
- **Timestamps are pre-formatted UTC strings**, not `DateTime`. EF returns `Unspecified` kinds,
  which would serialize without the `Z` the contract requires. See `IntegrationTimestamp`.
- Event handlers that subscribe to Application.Contracts events (e.g. `AppointmentApprovedEto`)
  live in the Application layer, not here -- Domain cannot reference upward. The reusable work
  stays here; only the subscription lives up there.
