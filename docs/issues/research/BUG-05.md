[Home](../../INDEX.md) > [Issues](../) > Research > BUG-05

# BUG-05: Slot Save Fires N+1 Individual POSTs -- Research

**Severity**: Medium
**Status**: Open (verified 2026-04-17)
**Source files**:
- `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability-generate.component.ts` lines 226-254

---

## Current state (verified 2026-04-17)

```typescript
const requests = slots.map((slot) =>
  this.service.create({
    availableDate: slot.availableDate,
    fromTime: slot.fromTime,
    toTime: slot.toTime,
    bookingStatusId: slot.bookingStatusId,
    locationId: slot.locationId,
    appointmentTypeId: slot.appointmentTypeId ?? null,
  }),
);

this.isSubmitting = true;
forkJoin(requests.length ? requests : [of(null)])
  .pipe(finalize(() => (this.isSubmitting = false)))
  .subscribe(() => this.goBack());
```

32 slots = 32 parallel POSTs. No bulk endpoint. No transactional atomicity -- each POST is its own UoW; a partial failure leaves DB inconsistent and user with no indication of which slots failed.

---

## Official documentation

- [ABP Application Services](https://abp.io/docs/latest/framework/architecture/domain-driven-design/application-services) -- AppService methods may accept collection DTOs; no canonical naming for bulk methods.
- [ABP Unit of Work](https://abp.io/docs/latest/framework/architecture/domain-driven-design/unit-of-work) -- each async AppService method auto-wrapped in single UoW/transaction; all repo calls inside share one commit.
- [ABP Repositories (InsertManyAsync)](https://abp.io/docs/latest/framework/architecture/domain-driven-design/repositories) -- `InsertManyAsync(IEnumerable<TEntity>)` on every IRepository, uses EF Core batched inserts.
- [ABP EF Core integration -- IEfCoreBulkOperationProvider](https://abp.io/docs/latest/framework/data/entity-framework-core) -- extension point for overriding default batched insert.
- [EF Core SaveChanges is transactional](https://learn.microsoft.com/en-us/ef/core/saving/basic) -- "all operations either succeed or fail and never left partially applied."

## Community findings

- [ABP Community -- Bulk Operations with EF Core 7.0](https://abp.io/community/articles/bulk-operations-with-entity-framework-core-7.0-zvr01mtn) -- `InsertManyAsync` from an AppService leverages EF Core 7+ batched inserts; no third-party library needed.
- [ABP #8613 -- Bulk Insert performance](https://github.com/abpframework/abp/issues/8613) -- batching semantics and limits.
- [ABP #12185 -- Bulk operations and optimistic concurrency](https://github.com/abpframework/abp/issues/12185) -- known interaction worth reviewing.
- [ABP support #7048 -- Query Related to Bulk User Insert](https://abp.io/support/questions/7048/Query-Related-to-Bulk-User-Insert) -- pattern: inject `IRepository<T>`, call `InsertManyAsync`, optionally inside explicit `[UnitOfWork]`.

## Recommended approach

1. Add single AppService method `CreateManyAsync(List<DoctorAvailabilityCreateDto>)` + manual controller action at `POST api/app/doctor-availabilities/bulk`.
2. Internally: map DTOs -> entities, call `_repository.InsertManyAsync(entities, autoSave: true)`, rely on auto-UoW for atomicity.
3. Angular: replace `forkJoin(slots.map(create))` with one HTTP call returning `List<Dto>`.
4. Optional future: move slot-generation rules into a domain-service method so conflict validation runs once per request, not per-slot.

## Gotchas / blockers

- `ExecuteUpdateAsync`/`ExecuteDeleteAsync` bypass ABP soft-delete and auditing; do NOT swap them in for inserts.
- Angular proxy must be regenerated (`abp generate-proxy`) after adding the method; hand-edits to `proxy/` are reverted.
- If slot generation runs per-slot overlap/conflict checks today, those need to be rewritten as set-based SQL or loop-inside-transaction to stay O(1) round-trip.
- Permission check runs once for bulk call rather than 32 times -- desirable but changes authz log volume.

## Open questions

- Is per-slot conflict validation needed at domain level, or can SQL uniqueness/exclusion constraints enforce it?
- Fail-fast (first conflict rejects all) or upsert-like (skip duplicates)? Affects transaction semantics and 2xx vs 207 responses.
- Does the client need per-slot error detail ("which slot failed"), or is all-or-nothing acceptable UX?

## Related

- [BUG-01](BUG-01.md) -- slot conflict detection; same feature area
- [docs/issues/BUGS.md#bug-05](../BUGS.md#bug-05-slot-save-fires-n1-individual-http-posts)
