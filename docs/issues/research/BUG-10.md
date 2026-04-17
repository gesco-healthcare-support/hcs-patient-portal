[Home](../../INDEX.md) > [Issues](../) > Research > BUG-10

# BUG-10: fromTime > toTime Accepted on Slot Creation -- Research

**Severity**: Medium
**Status**: Open (verified 2026-04-17, E2E tests B7.4.2 + E7)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` `CreateAsync` lines 136-145
- `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/DoctorAvailabilityManager.cs` `CreateAsync` lines 23-30
- `DoctorAvailability.cs` entity constructor (no validation)

---

## Current state (verified 2026-04-17)

`DoctorAvailabilitiesAppService.CreateAsync` validates only `LocationId != default`. `DoctorAvailabilityManager.CreateAsync` only does `Check.NotNull` on inputs:

```csharp
public virtual async Task<DoctorAvailability> CreateAsync(Guid locationId, Guid? appointmentTypeId, DateTime availableDate, TimeOnly fromTime, TimeOnly toTime, BookingStatus bookingStatusId)
{
    Check.NotNull(locationId, nameof(locationId));
    Check.NotNull(availableDate, nameof(availableDate));
    Check.NotNull(bookingStatusId, nameof(bookingStatusId));
    var doctorAvailability = new DoctorAvailability(...);
    return await _doctorAvailabilityRepository.InsertAsync(doctorAvailability);
}
```

`GeneratePreviewAsync` DOES validate (`item.ToTime <= item.FromTime`) at line 184, creating a path-dependent invariant -- the classic anti-pattern DDD warns about.

---

## Official documentation

- [ABP Domain Services best practices](https://abp.io/docs/latest/framework/architecture/best-practices/domain-services) -- invariants belong in the domain service or entity, not AppService; use Manager suffix; throw `BusinessException`.
- [ABP Domain Services](https://abp.io/docs/en/abp/latest/Domain-Services) -- logic spanning entity + external services belongs here.
- [ABP Exception Handling](https://abp.io/docs/en/abp/latest/Exception-Handling) -- `BusinessException` with error codes is canonical for domain invariant failures.
- [.NET `TimeOnly.CompareTo`](https://learn.microsoft.com/en-us/dotnet/api/system.timeonly.compareto?view=net-10.0) -- `<`, `<=`, `>`, `>=` operators supported directly; single-day tick comparison, no epsilon needed.
- [.NET date/time type guidance](https://learn.microsoft.com/en-us/dotnet/standard/datetime/choosing-between-datetime) -- `TimeOnly` is wall-clock time-of-day, not elapsed span; do not subtract to DateTime.

## Community findings

- [Exception Not Found -- DateOnly and TimeOnly](https://exceptionnotfound.net/bite-size-dotnet-6-dateonly-and-timeonly/) -- designed for exact wall-clock comparisons.
- [Medium -- TimeOnly in C#](https://info2502.medium.com/timeonly-in-c-aec719a45d96) -- operator examples.
- [C# Tutorial -- TimeOnly](https://www.csharptutorial.net/csharp-tutorial/csharp-timeonly/) -- confirms `>` and `<` are direct and safe.
- [DDD Consistency Boundary (Hickey)](https://www.jamesmichaelhickey.com/consistency-boundary/) + [Vernon's Effective Aggregate Design Part II](https://www.dddcommunity.org/wp-content/uploads/files/pdf_articles/Vernon_2011_2.pdf) -- invariants must hold in a single transaction and a single place.

## Recommended approach

1. Enforce `FromTime < ToTime` inside `DoctorAvailability`'s constructor/factory (the entity) so every code path -- `CreateAsync`, `UpdateAsync`, `GeneratePreviewAsync` -- is gated. Throw `BusinessException("CaseEvaluation:DoctorAvailability.InvalidTimeRange")`.
2. Move the redundant check from `GeneratePreviewAsync` to `DoctorAvailabilityManager.CreateAsync` as defence-in-depth; keep Manager validation for multi-entity rules (future overlap checks).
3. `<` is safe and correct for `TimeOnly`. Single-day ticks, no epsilon required.

## Gotchas / blockers

- `TimeOnly` wraps at midnight (`23:59 < 00:30`); a shift crossing midnight requires explicit modelling (two ranges or a `CrossesMidnight` flag). Confirm the business does NOT support overnight IME slots before enforcing strict `<`. INFERENCE from .NET `TimeOnly` semantics.
- `GeneratePreviewAsync` uses `item.ToTime <= item.FromTime` (inclusive) -- once hoisted, decide whether 0-minute slots are invalid. Most likely yes.
- If Mapperly maps DTO -> entity bypassing the constructor, entity guard is skipped. Mapperly default uses property-setter assignment; verify mapper strategy or switch to constructor/factory mapping. INFERENCE.

## Open questions

- **Product**: do overnight blocks exist (e.g. 22:00-02:00)? Determines whether `<` is the right operator.
- **Product**: are zero-minute slots rejected outright? Preview uses `<=`, so yes -- confirm.
- **Product**: minimum block length (e.g. >= 30 minutes)? Not inferable from source.

## Related

- [BUG-09](BUG-09.md) -- sibling "missing invariant in AppService" issue
- [BUG-01](BUG-01.md) -- slot conflict logic; fix together
- [ARC-02](ARC-02.md) -- broader layering concern
- [docs/issues/BUGS.md#bug-10](../BUGS.md#bug-10-fromtime--totime-accepted-on-slot-creation)
