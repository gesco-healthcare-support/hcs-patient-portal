[Home](../../INDEX.md) > [Issues](../) > Research > BUG-04

# BUG-04: Slot Preview Uses Only First Input's Location Label -- Research

**Severity**: Medium
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` line 233

---

## Current state (verified 2026-04-17)

```csharp
// Line 233
var location = await _locationRepository.FindAsync(input.First().LocationId);
// ...
// Line 246
LocationName = location?.Name,
```

For batches that span multiple locations, all slots show the first input's location name. User cannot visually distinguish which slot belongs to which location.

---

## Official documentation

- [Enumerable.GroupBy (MS Learn)](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.groupby?view=net-9.0) -- canonical API for grouping inputs by `LocationId`.
- [Grouping Data -- C# LINQ](https://learn.microsoft.com/en-us/dotnet/csharp/linq/standard-query-operators/grouping-data) -- official LINQ grouping guide with `ToDictionary` patterns.
- [LINQ ToDictionary Method](https://dotnettutorials.net/lesson/todictionary-method/) -- `source.ToDictionary(x => x.Id, x => x)` pattern.
- [EF Core: ToList/ToArray/ToDictionary/ToLookup](https://gavilan.blog/2018/04/22/entity-framework-tolist-toarray-todictionary-tolookup-groupby/) -- trade-offs between `ToDictionary` and `ToLookup` for id-keyed lookups.

## Community findings

- [LINQ Group By to Dictionary (End Your If)](https://www.endyourif.com/linq-group-by-into-dictionary-object/) -- canonical pattern: `entities.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First())`.
- [ExtensionMethod.NET -- ToDictionary for IGrouping](https://www.extensionmethod.net/csharp/igrouping/todictionary-for-enumerations-of-groupings) -- dictionary-from-grouping helper.
- [Code Ninja -- Never call GroupBy().ToDictionary()](http://code-ninja.org/blog/2014/07/24/entity-framework-never-call-groupby-todictionary/) -- perform GroupBy in memory, not SQL, for this size.

## Recommended approach

1. Extract distinct `LocationId`s from `input`.
2. Fetch all matching locations in one repository call (`_locationRepository.GetListAsync(x => ids.Contains(x.Id))`).
3. Materialise a `Dictionary<Guid, string>` of id -> name.
4. Look up the name per slot when constructing each preview row (fall back gracefully if a location is missing).
5. Add an xUnit + Shouldly test fixture: seed 2 locations + 2 inputs referencing each; assert each preview row carries the correct `LocationName`.

## Gotchas / blockers

- `IRepository<Location, Guid>` LINQ queries require `AsyncExecuter` or the `IRepository` extension (`WhereIfAsync`, `GetListAsync(predicate)`). ABP convention in this repo: prefer `GetListAsync(predicate)`.
- Any missing LocationId in the dictionary (stale/deleted location) must fall back gracefully; mirror existing `location?.Name` null-safety.
- Confirm line 233 is still live code; recent refactors may have moved it. Grep the method name to verify.

## Open questions

- Are inputs validated up front so all `LocationId`s exist before this point? If yes, the fallback-null case is dead code (still keep defensive check).
- Is the "batch across multiple locations" scenario a real user flow or a theoretical API-abuse concern? Business-logic check.

## Related

- [BUG-01](BUG-01.md) -- same file, sibling issue
- [FEAT-07](FEAT-07.md) -- this test fixture belongs in the new DoctorAvailabilities test suite
- [docs/issues/BUGS.md#bug-04](../BUGS.md#bug-04-slot-preview-uses-only-the-first-inputs-location-label)
