---
feature: record-locking
old-source:
  - P:\PatientPortalOld\PatientAppointment.DbEntities\Models\LockRecord.cs
  - P:\PatientPortalOld\PatientAppointment.DbEntities\Models\GlobalSetting.cs (RecordLock flag)
old-docs:
  - data-dictionary-table.md (LockRecord, GlobalSettings.RecordLock)
audited: 2026-05-01
status: audit-only
priority: 4
strict-parity: true
internal-user-role: any internal user (system-managed)
depends-on: []
required-by: []
phase: 2
---

# Record locking

## Purpose

When `GlobalSettings.RecordLock = true`, OLD locks records being edited to prevent concurrent modifications by multiple users. A user opens a record, system creates a `LockRecord` row; another user opening the same record sees a "locked by X for N minutes" message.

**Phase 2 -- defer.** Not in critical workflow scope.

## OLD behavior (binding, abbreviated)

### Schema (`LockRecords` table)

Standard locking table with TableName + RecordId + UserId + LockedAt + LockDuration.

### Critical OLD behaviors

- **Toggleable via `GlobalSettings.RecordLock`** -- default off in spec, configurable.
- **`GlobalSettings.LockDuration`** controls auto-release timer.
- **Optimistic vs pessimistic** -- LockRecords is a pessimistic lock pattern. ABP supports optimistic concurrency via concurrency stamps (`IHasConcurrencyStamp`) which is the modern equivalent.

## NEW current state

- ABP supports `IHasConcurrencyStamp` on entities -- optimistic concurrency check on update.
- No pessimistic lock table in NEW.

## Gap analysis (strict parity, deferred)

**Phase 2 decision:** OLD's pessimistic lock pattern is uncommon in modern web apps. Modern UX uses optimistic concurrency (concurrency stamps + 409 conflict on save) plus optional UI hints (e.g., "X is currently editing"). 

For Phase 1 strict parity: **defer the feature**. Most OLD installations likely run with `RecordLock = false` (per OLD spec implication). When Phase 2 begins:

- Option A: Implement OLD's pessimistic LockRecord table verbatim.
- Option B: Use ABP's optimistic concurrency stamp + a separate "presence" indicator (e.g., real-time channel showing who has the page open).

Decision deferred to Phase 2.

## Replication notes

For Phase 1: **omit**. Add a setting key `CaseEvaluation.RecordLockEnabled` defaulted `false`. Document the deferral.

## Verification

Phase 1: setting exists; feature not active. Concurrent edits result in standard ABP optimistic-concurrency conflict (last-writer-wins or 409).

Phase 2: implement per chosen option.
