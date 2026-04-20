[Home](../../INDEX.md) > [Issues](../) > Research > DAT-05

# DAT-05: Disconnected Status Representations -- Research

**Severity**: High
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/AppointmentStatusType.cs` (13-value enum)
- `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentStatuses/AppointmentStatus.cs` (separate CRUD entity)
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Appointment.cs` (stores enum, no FK to entity)

---

## Current state (verified 2026-04-17)

Two parallel representations:

1. **`AppointmentStatusType` enum** (values 1-13: Pending..CancellationRequested) stored directly on `Appointment.AppointmentStatus`.
2. **`AppointmentStatus` entity** -- full domain entity with Guid Id, `Name` field, CRUD AppService, Angular module at `/appointment-statuses`.

Zero linkage: `Appointment` has no FK to the `AppointmentStatus` table. Admin UI edits rows that nothing reads. Classic "enum vs lookup table" anti-pattern with both present and neither authoritative. Confirmed in E2E tests B10.1 and B11.1.1.

---

## Official documentation

- [EF Core -- Value Conversions](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions) -- `HasConversion`; required if the enum is kept but projected to a lookup row at query time.
- [Mapperly -- Enum mappings](https://mapperly.riok.app/docs/configuration/enum/) -- ByValue/ByName strategies. Confirms Mapperly maps enum <-> primitive but does NOT auto-join to a lookup table row.
- [Mapperly MapEnumAttribute](https://mapperly.riok.app/docs/api/riok.mapperly.abstractions.mapenumattribute/) + [MapEnumValue issue #1476](https://github.com/riok/mapperly/issues/1476) -- explicit per-value mapping if numeric enum values change.
- [EnumMappingStrategy](https://mapperly.riok.app/docs/api/riok.mapperly.abstractions.enummappingstrategy/) -- ByValue default, ByName, ByValueCheckDefined.
- [Ardalis -- Enums and Lookup Tables](https://ardalis.com/enums-and-lookup-tables/) -- canonical .NET framing. Argues for a unit test asserting enum count == seeded lookup-row count when both are kept.
- [Ardalis -- SmartEnum repo](https://github.com/ardalis/SmartEnum) + [NimblePros -- Persisting a Smart Enum with EF Core](https://blog.nimblepros.com/blogs/persisting-a-smart-enum-with-entity-framework-core/) -- middle path: class-based enum with behaviour, persisted as int via value converter.
- [ABP Module Entity Extensions](https://docs.abp.io/en/abp/latest/Module-Entity-Extensions) -- `UI.Lookup.Url` consumes a lookup endpoint via FK if going the table route.
- [ABP Support #4475 -- Adding a Lookup table for a new field](https://abp.io/support/questions/4475/Adding-Lookup-table-for-a-new-field) -- Volo support: "using an Enum is easier than a lookup table" for static lists.
- [ABP Support #10576 -- Angular enum dropdown list](https://abp.io/support/questions/10576/Angular---Enum---dropdown-list) -- ABP-sanctioned way to render enum selects without a lookup table.

## Community findings

- [CYBERTEC -- Lookup table or enum type?](https://www.cybertec-postgresql.com/en/lookup-table-or-enum-type/) -- lookup tables when values may change; enums when truly immutable.
- [Hacker News discussion](https://news.ycombinator.com/item?id=46154892) -- practitioner consensus: "lookup table unless you have a strong reason."
- [Gregory Beamer -- Why enums are not a good replacement for lookup tables](https://gregorybeamer.wordpress.com/2008/06/09/why-enums-are-not-a-good-replacement-for-lookup-tables/) -- older but still cited: "enums and tables coexist without FK, they drift." Exact DAT-05 situation.
- [JustSimplyCode -- Enums as lookup tables in DB vs code](https://justsimplycode.com/2018/10/20/keeping-enums-as-look-up-tables-in-db-vs-keeping-them-in-code/) -- enum in code + seed lookup table + unit test asserts parity. Most practical hybrid.
- [CoveMountainSoftware -- Enums and lookup tables](https://covemountainsoftware.com/2022/06/18/code-maintenance-enums-and-lookup-tables/) -- drift problem + build-time parity check.
- [Vladimir Khorikov -- Is Enum an Entity or a Value Object?](https://khorikov.org/posts/2021-09-06-enum-entity-value-object/) -- relevant framing: "we accidentally modelled a value object as an entity" -- precisely the DAT-05 diagnosis.
- [Meziantou -- Smart/type-safe enums in .NET](https://www.meziantou.net/smart-enums-type-safe-enums-in-dotnet.htm) + [Thinktecture -- Smart Enums](https://www.thinktecture.com/en/net/smart-enums-adding-domain-logic-to-enumerations-in-dotnet/) -- middle-ground, class-based with EF value converter.
- [dotnet/efcore #12248 -- Enum as Lookup Table](https://github.com/dotnet/efcore/issues/12248) -- EF Core team closed without native support; community points to SmartEnum.
- [Ardalis -- Persisting type-safe enum with EF 6](https://ardalis.com/persisting-the-type-safe-enum-pattern-with-ef-6/) -- EF Core plumbing nearly identical.

## Recommended approach

**Product decision required first**: will statuses ever change post-deploy?

- **Option A (preferred for this domain)** -- keep enum only, delete entity.
  - Workers' comp IME states are regulatory-adjacent; they rarely change.
  - Delete `AppointmentStatus` entity + its AppService + Angular module + its DB migration table.
  - Move display labels to ABP localisation JSON using ABP's `Enum:AppointmentStatusType.Pending` key convention ([ABP enum localization](https://abp.io/support/questions/3944/Angular-enums-and-labels)). Non-developers can edit labels without code changes.
  - Run `abp generate-proxy` to regenerate Angular proxies.

- **Option B** -- FK-based lookup. Drop the enum, add `AppointmentStatusId` (Guid) FK on `Appointment`, seed the 13 values via `DataSeedContributor` with stable Guids, manage labels through admin UI.
  - Mapperly can map `entity.AppointmentStatus.Code` -> DTO string, but cannot join back from a string to an entity -- repository work needed.
  - Requires EF migration + data migration for existing rows.
  - Pick this only if tenants need to add new statuses without a deploy.

- **Hybrid (JustSimplyCode pattern)** -- keep enum authoritative in code, seed the lookup table read-only, add a build-time unit test asserting parity. Gives an admin-visible list without runtime editability.

## Gotchas / blockers

- Dropping the lookup entity removes the `appointment-statuses` Angular module -- any menu items referencing that route must go.
- If any tenant has already edited rows in the `AppointmentStatus` table (renamed Pending to something custom), dropping loses that customisation -- check first.
- Mapperly's `EnumMappingStrategy.ByName` is **case-sensitive** by default; camelCase/PascalCase mismatch silently falls to numeric (see [Mapperly #1470](https://github.com/riok/mapperly/issues/1470), [#1079](https://github.com/riok/mapperly/issues/1079)).
- ABP's auto-generated Angular proxy already exposes the enum as a `const enum` -- Angular-side duplicate shape exists.

## Open questions

- Does any tenant rely on the editable lookup table today? Any rows renamed?
- Will the portal need new statuses post-deploy, tenant-configurable?
- Does the DWC localisation team need to edit status labels without deploying? ABP localisation supports that on enums.

## Related

- [BUG-02](BUG-02.md) -- status changes never persisted; DAT-05 must be resolved before designing the fix
- [FEAT-01](FEAT-01.md) -- status workflow needs a canonical representation first
- Q2 in [TECHNICAL-OPEN-QUESTIONS.md](../TECHNICAL-OPEN-QUESTIONS.md#q2-is-the-appointmentstatus-lookup-table-intentional-or-a-design-mistake)
- [docs/issues/DATA-INTEGRITY.md#dat-05](../DATA-INTEGRITY.md#dat-05-disconnected-status-representations)
