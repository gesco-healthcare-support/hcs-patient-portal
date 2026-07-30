# Integration -- outbound delivery to external Gesco systems

Everything the portal sends OUT to another application. Today that is one target, the Case
Tracker: when staff approve an appointment it becomes a case over there, and this folder owns
getting the appointment's data across reliably. The wire contract is agreed and frozen in
`docs/integration/case-tracker-api-contract.md` -- treat that document, not this code, as the
source of truth for field names and semantics.

## What lives here

| File | Purpose |
|---|---|
| `CaseTracker/IntegrationOutboxItem.cs` | Durable per-office message ledger. `TryClaim` leases a row, `MarkSent` is idempotent, `MarkFailed` reschedules or dead-letters at the cap, and `MarkFatal` dead-letters immediately for a response a retry can never fix. |
| `CaseTracker/IntegrationOutboxManager.cs` | Idempotent enqueue (SHA-256 key over message type + appointment + version) and the atomic due-batch claim. |
| `CaseTracker/IIntegrationOutboxRepository.cs` | Adds `TryLeaseAsync`; the EF implementation lives in the EntityFrameworkCore layer. |
| `CaseTracker/IntegrationOutboxDrainService.cs` | Sends due rows: gates on the enabled setting, then applies the status matrix to each result. |
| `CaseTracker/CaseTrackerClient.cs` + `ICaseTrackerClient.cs` | Typed HttpClient. Sends `X-Intake-Token` and `application/json`; never logs the token or the payload. |
| `CaseTracker/CaseTrackerPushResult.cs` | Pure classifier: 2xx succeeds, 404/408/429/5xx retry, every other 4xx is fatal. |
| `CaseTracker/CaseTrackerIntakeQueue.cs` | Shared enqueue path used by BOTH the approval trigger and the manual push, so they cannot drift. |
| `CaseTracker/CaseTrackerEndpoints.cs` | Relative paths on the Case Tracker API. |
| `CaseTracker/IntakePayloadSerializer.cs` | The one place integration JSON is produced (camelCase, nulls kept). |
| `CaseTracker/Jobs/` | `IntegrationOutboxDrainJob` (one office) and `CaseTrackerReconciliationJob` (15-min sweep across offices). |
| `CaseTracker/Payload/` | `IIntakePayloadBuilder` facade over four focused resolvers, plus the DTOs and the pure helpers (`ObjectKeyBuilder`, `DocumentEntryMapper`, `IntegrationTimestamp`, `EvaluationKindWire`). |

## Conventions

- **Nothing leaves the portal unless an office opts in.** The drain gates on
  `CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled`, which defaults to false.
  When off, due rows stay Pending with no failed-attempt cost and resume once enabled.
- **Fail fast, then tell a human.** `MaxAttempts` is 3 with a flat 5-minute backoff (~10 minutes
  to dead-letter), unlike the email outbox's 5. Case timelines are legally significant, so a
  stuck case must surface quickly rather than retry quietly for hours.
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
