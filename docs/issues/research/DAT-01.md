[Home](../../INDEX.md) > [Issues](../) > Research > DAT-01

# DAT-01: Race Condition on Slot Booking -- Research

**Severity**: Critical
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs` lines 213-249
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` lines 214-228 (Medallion/Redis wired but unused)

---

## Current state (verified 2026-04-17)

```csharp
// Line 219-222: check
if (doctorAvailability.BookingStatusId != BookingStatus.Available)
    throw new UserFriendlyException(L["The selected availability slot is no longer available."]);
// ... build appointment ...
// Line 248-249: act
doctorAvailability.BookingStatusId = BookingStatus.Booked;
await _doctorAvailabilityRepository.UpdateAsync(doctorAvailability);
```

Facts:

- Classic check-then-act race: two concurrent callers can both observe `Available` before either writes.
- `DoctorAvailability` has **no** `ConcurrencyStamp` / rowversion (`Appointment` does; `DoctorAvailability` does not).
- `Medallion.Threading.Redis.RedisDistributedSynchronizationProvider` is registered as `IDistributedLockProvider` singleton in `ConfigureDistributedLocking` -- available but unused by the booking path.
- The existing E2E test (B15.1.1) did not reproduce double-booking because EF Core transaction happened to serialize; this is not a fix, just happenstance.

---

## Official documentation

- [ABP Distributed Locking](https://abp.io/docs/latest/framework/infrastructure/distributed-locking) -- `IAbpDistributedLock.TryAcquireAsync(name, timeout, ct)` returns `IAsyncDisposable?` (null = not acquired). Supports `AbpDistributedLockOptions.KeyPrefix` for app-level scoping.
- [Medallion DistributedLock (GitHub README)](https://github.com/madelson/DistributedLock) -- `Acquire()` default timeout `Timeout.InfiniteTimeSpan`; `TryAcquire()` default `TimeSpan.Zero`. All implementations **non-reentrant since v2.0.0**. Handle released on disposal. `HandleLostToken` signals connection failure.
- [EF Core Concurrency Conflicts](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) -- `[Timestamp]` / `IsRowVersion()` vs `[ConcurrencyCheck]` / `IsConcurrencyToken()`. "In the normal ('optimistic') case the database reports one row affected; if a concurrent update occurred, the UPDATE fails to find any matching rows, and SaveChanges throws `DbUpdateConcurrencyException`."
- [SQL Server Table Hints](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table) -- `UPDLOCK` "specifies that update locks are to be taken and held until the transaction completes"; combine with `HOLDLOCK`/`ROWLOCK` to serialise check-then-act.
- [sp_getapplock](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql) -- `@Resource` + `@LockMode` (`Shared`/`Update`/`Exclusive`), `@LockOwner=Transaction` auto-releases on commit/rollback. Return `-1` timeout / `-3` deadlock victim.
- [EF Core Connection Resiliency caveat](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/work-with-data-in-asp-net-core-apps#entity-framework-core-for-relational-databases) -- when `EnableRetryOnFailure` is on, `BeginTransaction` breaks unless wrapped in `Database.CreateExecutionStrategy().ExecuteAsync(...)`.

## Community findings

- [Milan Jovanovic -- Solving Race Conditions with EF Core Optimistic Locking](https://www.milanjovanovic.tech/blog/solving-race-conditions-with-ef-core-optimistic-locking) -- canonical reservation-double-booking walkthrough using `IsRowVersion()` + catch `DbUpdateConcurrencyException`. Exact scenario.
- [HackerNoon -- How to Solve Race Conditions in a Booking System](https://hackernoon.com/how-to-solve-race-conditions-in-a-booking-system) -- pattern enumeration: pessimistic row locks, optimistic versioning, distributed locks, `SELECT FOR UPDATE`.
- [stevsharp -- Optimistic vs Pessimistic Concurrency in EF Core (with Table Hints)](https://dev.to/stevsharp/optimistic-vs-pessimistic-concurrency-in-ef-core-with-table-hints-37jk) -- recommends UPDLOCK/ROWLOCK via `FromSqlRaw` for high-contention; optimistic for read-heavy. EF Core has no LINQ support for table hints.
- [ABP Support #4249 -- Redis Distributed Lock exception](https://abp.io/support/QA/Questions/4249/Redis-Distributed-Lock-exception) -- real-world Redis config issues; confirms `IAbpDistributedLock` + Redis provider is the sanctioned ABP route.
- [ABP Support #2284 -- Concurrency handling clarification](https://abp.io/support/QA/Questions/2284/Concurrency-handling-clarification-question) -- ABP team's general guidance.
- [ABP Support #1018 -- AbpDbConcurrencyException](https://abp.io/support/QA/Questions/1018/AbpDbConcurrencyException-while-updating-in-multi-threading) -- how ABP surfaces ConcurrencyStamp mismatches; relevant because adding ConcurrencyStamp to DoctorAvailability is the minimum fix.
- [dotnet/efcore #26042](https://github.com/dotnet/efcore/issues/26042) -- open issue tracking native `SELECT FOR UPDATE`/UPDLOCK in LINQ. As of EF Core 10, still needs raw SQL.

## Recommended approach

1. **ABP-idiomatic fix (cheapest)**: use the already-wired `IAbpDistributedLock`. Inject `IAbpDistributedLock` into `AppointmentsAppService`. Wrap the check-then-act in `TryAcquireAsync($"slot:{CurrentTenant.Id}:{doctorAvailabilityId}", timeout: TimeSpan.FromSeconds(5))`. Include `TenantId` in the key to avoid cross-tenant false sharing.
2. **Defence in depth**: add a concurrency token on `DoctorAvailability` (convention in this repo is `ConcurrencyStamp` Guid via `IHasConcurrencyStamp`, matching the `Appointment` aggregate). EF migration converts `byte[] RowVersion` or adds a string `ConcurrencyStamp` column.
3. **Fallback tier if Redis is unavailable**: SQL Server `sp_getapplock` inside the UoW transaction (same `Transaction` lock owner auto-releases on commit/rollback). Pure-SQL, no extra infra.

## Gotchas / blockers

- Medallion 2.x is **non-reentrant** -- do not wrap recursive calls that grab the same lock.
- `TryAcquireAsync(name)` with default `TimeSpan.Zero` returns null immediately if contended -- either pass a timeout or translate null to a user-facing "slot just got taken" error.
- If `EnableRetryOnFailure` is ever flipped on, the distributed-lock block needs `CreateExecutionStrategy().ExecuteAsync(...)` wrapper.
- Redis outage behaviour: `HandleLostToken` signals connection loss; ABP docs "don't explicitly address Redis outages"; a Redis restart mid-acquire silently loses the handle. Plan a fallback or fail-closed policy.
- Adding a ConcurrencyStamp requires an EF migration + Angular proxy regen if the field is exposed in DTOs.
- Tenant filter scopes the query but the lock key should still include `TenantId` explicitly.

## Open questions

- What Redis timeout budget is acceptable for booking UX? Sub-second fail-fast with "try again"?
- Should `DoctorAvailability` use the string `ConcurrencyStamp` pattern (consistent with `Appointment`) or a raw `rowversion`? Convention check.
- Does production Redis have persistence enabled? If ephemeral and it restarts during acquire, the handle is silently lost.
- Is the fallback tier (sp_getapplock) worth implementing now, or only if Redis proves unreliable?

## Related

- [DAT-02](DAT-02.md) -- confirmation-number sequence uses the same read-then-write pattern; same lock strategy
- [DAT-03](DAT-03.md) -- reschedule coordination also needs this lock
- [docs/issues/DATA-INTEGRITY.md#dat-01](../DATA-INTEGRITY.md#dat-01-race-condition-on-slot-booking)
