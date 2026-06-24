# AppointmentDrafts -- server-persisted booking-wizard drafts

#15 (2026-06-22): a server-side, in-progress booking-wizard draft that replaces the
cosmetic localStorage-only autosave, so a partially-filled request survives
navigate-away and resumes on return. One active draft per (tenant, creator); the
self-scoped app service upserts it.

## What lives here

| File | Purpose |
|---|---|
| `AppointmentDraft.cs` | Aggregate root: `CreationAuditedAggregateRoot<Guid>`, `IMultiTenant`. The opaque form snapshot (`PayloadJson`) + `CurrentStep` + non-PHI `Label` + `LastSavedTime` |
| `Jobs/DraftCleanupJob.cs` | Hangfire daily TTL purge (`0 3 * * *`, `RetentionDays = 30`) that PHYSICALLY deletes stale drafts |

## Conventions

- **Base is `CreationAuditedAggregateRoot`, NOT FullAudited -- on purpose.** Drafts
  hold transient PHI (patient name/DOB/SSN/address inside `PayloadJson`), so discard
  and the TTL purge MUST physically delete the row. Soft-delete would leave the PHI
  payload at rest behind an `IsDeleted` flag, defeating retention minimization (#15 D5).
- **`PayloadJson` is an opaque PHI blob -- never log it.** It is the wizard's
  `form.getRawValue()`. Only counts/ids are safe to log.
- **`Label` is the only non-PHI string** (e.g. the appointment-type name) for a
  resume affordance; keep PHI out of it.

## Gotchas

1. **TTL purge disables the multi-tenant filter.** `DraftCleanupJob` runs
   `_dataFilter.Disable<IMultiTenant>()` so the daily sweep spans every tenant. This
   is one of the cross-tenant `Disable<IMultiTenant>` sites the db-per-tenant
   migration must revisit (under separate databases it would only see the host DB).
2. **One draft per (tenant, creator).** The app service upserts a single creator-
   scoped row; there is no draft list or history.
3. **`IMultiTenant` in both DbContexts.** Lives in host + tenant DBs.

## Related

- docs/business-domain/APPOINTMENT-LIFECYCLE.md
- The booking wizard (angular `appointments/wizard`) that produces the payload
