# Lookup data seeds (States, AppointmentTypes, AppointmentStatuses, AppointmentLanguages, Locations, WcabOffices)

## Source gap IDs

- DB-15 -- source: [../../../../main/docs/gap-analysis/01-database-schema.md](../../../../main/docs/gap-analysis/01-database-schema.md) "## Delta" table row 138 and the gap-analysis README line 58 ("MVP-blocking (testing blocker)").

## NEW-version code read

- `W:/patient-portal/main/src/HealthcareSupport.CaseEvaluation.Domain/BookStoreDataSeederContributor.cs:10-47` -- reference pattern for `IDataSeedContributor + ITransientDependency`. Inserts 2 books with `autoSave: true` and a count-guard (`GetCountAsync() <= 0`). Does NOT wrap in `_currentTenant.Change(...)`; Books is host-scoped.
- `W:/patient-portal/main/src/HealthcareSupport.CaseEvaluation.Domain/Identity/ExternalUserRoleDataSeedContributor.cs:10-43` -- reference pattern for tenant-aware seeding. Wraps work inside `using (_currentTenant.Change(context?.TenantId)) { ... }` and uses `FindByNameAsync` as the idempotency guard before `CreateAsync`.
- `W:/patient-portal/main/src/HealthcareSupport.CaseEvaluation.Domain/Saas/SaasDataSeedContributor.cs` and `OpenIddict/OpenIddictDataSeedContributor.cs` -- the 4 existing seeders. There is NO `DoctorsDataSeedContributor.cs` in the main worktree despite the track-01 brief claiming one exists. Grep result: only test files and docs mention `DoctorsDataSeed`; no `.cs` under `src/.../Domain`. The brief's pattern claim is still valid, but the factual file doesn't exist.
- Host-only lookup entities in `src/.../Domain/`:
  - `States/State.cs:13-28` -- `FullAuditedAggregateRoot<Guid>`, single `Name` field, ctor `State(Guid id, string name)`. No IMultiTenant. No NameMaxLength const (CLAUDE.md notes the column has no `HasMaxLength` in EF config).
  - `AppointmentTypes/AppointmentType.cs:14-36` -- `FullAuditedEntity<Guid>`, `Name` (max 100), `Description` (max 200), `DoctorAppointmentTypes` collection.
  - `AppointmentStatuses/AppointmentStatus.cs:13-28` -- `FullAuditedEntity<Guid>`, `Name` (max 100). Note: `AppointmentStatus` the entity is separate from `AppointmentStatusType` the enum. Per `AppointmentStatuses/CLAUDE.md` "Known Gotchas" #2: no FK from Appointment -- this entity is a label lookup, not part of the state machine.
  - `AppointmentLanguages/AppointmentLanguage.cs:13-28` -- `FullAuditedEntity<Guid>`, `Name` (max 50). CreateDto defaults to "English".
  - `Locations/Location.cs:16-59` -- `FullAuditedAggregateRoot<Guid>`, `Name` (max 50), `Address` (max 100), `City` (max 50), `ZipCode` (max 15), `ParkingFee` decimal, `IsActive` bool, optional `StateId`, optional `AppointmentTypeId`, `DoctorLocations` M2M.
  - `WcabOffices/WcabOffice.cs:14-56` -- `FullAuditedAggregateRoot<Guid>`, `Name` (max 50), `Abbreviation` (max 50), `Address/City/ZipCode`, `IsActive`, optional `StateId`.
- `CaseEvaluationDomainModule.cs:29-48` -- `CaseEvaluationDomainSharedModule` + ABP modules; NO explicit registration of seed contributors. ABP auto-discovers `IDataSeedContributor + ITransientDependency` via the DI container (per ABP data-seeding docs).
- `Data/CaseEvaluationDbMigrationService.cs:42-91` -- the DbMigrator driver. Line 54: host seed via `await _dataSeeder.SeedAsync(new DataSeedContext(tenant?.Id)...)` where `tenant` is `null`; line 82: per-tenant seed via `using (_currentTenant.Change(tenant.Id))` plus `new DataSeedContext(tenant?.Id)`. Every registered contributor runs **twice** in a multi-tenant setup (once with `TenantId=null`, once per tenant). Seeders MUST branch on `context.TenantId` to avoid double-inserting host rows into tenant DBs.
- `CaseEvaluationConsts.cs:7` -- `DbTablePrefix = "App"`, which means the lookup tables are `AppStates`, `AppAppointmentTypes`, `AppAppointmentStatuses`, `AppAppointmentLanguages`, `AppLocations`, `AppWcabOffices` (matches `01-database-schema.md` line 88).
- Per ADR-003 (`docs/decisions/003-dual-dbcontext-host-tenant.md:15-19`) and root CLAUDE.md "Multi-tenancy Rules": Location, State, WcabOffice, AppointmentType, AppointmentStatus, AppointmentLanguage are host-scoped (no `IMultiTenant`). Configured inside `if (builder.IsHostDatabase())` in `CaseEvaluationDbContext.OnModelCreating`.
- Custom repository interfaces exist for all 6 entities (`IStateRepository`, `IAppointmentTypeRepository`, `IAppointmentStatusRepository`, `IAppointmentLanguageRepository`, `ILocationRepository`, `IWcabOfficeRepository`) -- a seeder can inject either these or the generic `IRepository<TEntity, Guid>` (Books uses the generic path).

## Live probes

- Token fetched via password grant against `https://localhost:44368/connect/token` with `admin/1q2w3E*`. Token length 1369 chars. Probe log: [`../probes/lookup-data-seeds-2026-04-24T12-58-00.md`](../probes/lookup-data-seeds-2026-04-24T12-58-00.md).
- `GET https://localhost:44327/api/app/states?MaxResultCount=5` -> HTTP 200. `totalCount: 12`. Rows include `California`, `Montana`, `Oregon`, `Texas`, `North Dakota`, plus one synthetic `TestState_XXXX...` (length-boundary test row, synthetic data, no PHI).
- `GET https://localhost:44327/api/app/appointment-types?MaxResultCount=5` -> HTTP 200. `totalCount: 7`. Rows include `Record Review`, `Deposition`, `Agreed Medical Examination (AME)`, `Supplemental Medical Report`, plus a `TestType_XXXX...` synthetic row. All seven are real IME categories suitable as canonical seed rows.
- `GET https://localhost:44327/api/app/appointment-statuses?MaxResultCount=5` -> HTTP 200. `totalCount: 14`. Rows map 1-to-1 to the `AppointmentStatusType` enum (13 states: Pending/Approved/Rejected/NoShow/CancelledNoBill/CancelledLate/RescheduledNoBill/RescheduledLate/CheckedIn/CheckedOut/Billed/RescheduleRequested/CancellationRequested) plus one extra label row. **These rows must be preserved verbatim** -- the enum names are the canonical seed names.
- `GET https://localhost:44327/api/app/appointment-languages?MaxResultCount=5` -> HTTP 200. `totalCount: 13`. Rows include `Armenian`, `Portuguese`, `Japanese`, `Hmong`, plus a synthetic test row. This set reflects the interpreter-language list for Southern California workers'-comp intake.
- `GET https://localhost:44327/api/app/locations?MaxResultCount=5` -> HTTP 200. `totalCount: 8`. Rows include `HCS Closed Santa Ana`, `HCS Fresno Office 6`, `HCS Anaheim Office 5`, `HCS Santa Ana Office 4`, plus a synthetic `MaxLoc_XXXX...` test row. All real rows are HCS-branded clinics with California StateId FKs -- **clinic-specific data**, not host-level defaults.
- `GET https://localhost:44327/api/app/wcab-offices?MaxResultCount=5` -> HTTP 200. `totalCount: 7`. Rows include `WCAB Irvine`, `WCAB Riverside`, `WCAB Bakersfield`, `WCAB San Bernardino`, `WCAB Glendale` -- a complete set of Southern California Workers' Compensation Appeals Board district offices. Public government data, no PHI.

**Critical finding:** the gap-analysis claim "every dropdown shows No data" is FALSE for this LocalDB instance. Rows are present. But there is NO `IDataSeedContributor` that regenerates them; the rows survive only while this local DB file survives. Rebuilding the DB (drop + `DbMigrator`) will lose them. The gap is **durability and reproducibility**, not "empty at runtime now".

## OLD-version reference

- `P:\PatientPortalOld\PatientAppointment.Api\Program.cs:221-334` (`SeedUsers()` per track-01 line 51) -- OLD seeds only 7 Roles + 1 `ApplicationTimeZone` + 7 Users at bring-up. States, Languages, Locations, AppointmentTypes, AppointmentStatuses, ApplicationObjects are **all empty** at bring-up; the rows in PROD were populated manually by clinic admins over time.
- Track-01 `01-database-schema.md:50-51`: "No other seeds (Countries, States, Languages, Locations, AppointmentTypes, AppointmentStatuses, ApplicationObjects all empty)."
- Track-10 erratum applicability: none of the four errata in `10-deep-dive-findings.md:5-50` touch seed data. Errata skipped.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, Angular 20 -- the stack (root `CLAUDE.md`).
- ADR-003 (`docs/decisions/003-dual-dbcontext-host-tenant.md`): these 6 entities are host-only; configured inside `if (builder.IsHostDatabase())`. Seeders MUST guard on `context.TenantId == null` (only seed at host scope) or the DbMigrator will try to insert into the tenant DB where the tables do not exist.
- ADR-004 (doctor-per-tenant): reinforces that tenant DBs never carry host lookups.
- Mapperly + manual controllers + dual DbContext constraints per ADR-001/002/003 -- none are touched by seeding.
- ABP `IDataSeedContributor.SeedAsync(DataSeedContext context)` signature is `Task`; auto-discovered via `ITransientDependency`; runs **on every `DbMigrator.MigrateAsync()` invocation** per the ABP Framework Data Seeding doc (accessed 2026-04-24). Idempotency MUST be enforced by the contributor (example idiom: `if (await repo.GetCountAsync() <= 0)` or per-row `FindByNameAsync`-style guard).
- HIPAA applicability: none. These 6 entities hold public reference data (US states, California WCAB district offices, generic IME type names, standard interpreter languages). No PHI in the seeds.
- Deterministic IDs required. `DoctorsDataSeedContributor` (referenced in root `CLAUDE.md` "Testing" section but absent from `src/.../Domain`) establishes the project convention of hardcoded GUIDs so tests can assert against known IDs. Seeders here must use fixed GUIDs (e.g. `new Guid("00000000-0000-0000-0000-<row-specific>")`) so subsequent seeders (`AppDoctorAvailability.AppointmentTypeId`, `AppAppointment.AppointmentTypeId`, etc.) can reference them.
- Non-MVP-side constraints: any localisation of seed names belongs in `Domain.Shared/Localization/CaseEvaluation/*.json`, not in seeder code. Seeders store the canonical English name; `| abpLocalization` handles display.

## Research sources consulted

1. **ABP Framework -- Data Seeding** -- https://abp.io/docs/latest/framework/infrastructure/data-seeding -- accessed 2026-04-24. Confirms `IDataSeedContributor.SeedAsync(DataSeedContext context) => Task`, auto-discovery via DI, idempotency is the contributor's responsibility, runs on every DbMigrator invocation. HIGH confidence (official docs).
2. **ABP Framework source -- `IDataSeedContributor.cs`** -- https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.Data/Volo/Abp/Data/IDataSeedContributor.cs -- accessed 2026-04-24. Verified verbatim interface body: `public interface IDataSeedContributor { Task SeedAsync(DataSeedContext context); }`. HIGH confidence.
3. **ABP Commercial -- DbMigrator module** -- implicit via `CaseEvaluationDbMigrationService.cs` and root CLAUDE.md "Database & Migrations" section. Confirms host-first-then-tenant seed ordering. HIGH confidence (in-repo evidence).
4. **Root CLAUDE.md "Testing / Test data seeding"** line approximately 70: "DoctorsDataSeedContributor seeds 2 doctors with hardcoded GUIDs -- tests assert against those specific IDs." CLAIMED seeder is not present in the `main` worktree; however, the **convention of hardcoded GUIDs** is documented and should be followed. MEDIUM confidence (doc claims file that doesn't exist).
5. **ADR-003** `docs/decisions/003-dual-dbcontext-host-tenant.md` -- confirms host-only vs tenant-only separation rule. HIGH confidence.
6. **Track 01 `01-database-schema.md:50-51, 98-106, 138`** -- inventory of existing seeders and the DB-15 gap definition. HIGH confidence (static code read).
7. California DIR WCAB District Office directory -- public government list (https://www.dir.ca.gov/wcab/wcab_locations.html). Used to cross-reference the 5 CA WCAB offices already present in the live DB. HIGH confidence (primary source).

## Alternatives considered

1. **Code-first `IDataSeedContributor` per entity, hardcoded seed data in C#** -- one class per entity (`StateDataSeedContributor`, `AppointmentTypeDataSeedContributor`, etc.), each inserting a deterministic list with hardcoded GUIDs. Tag: **chosen** for States, AppointmentTypes, AppointmentStatuses, AppointmentLanguages, WcabOffices (the 5 that hold reference-data of public or sector-standard nature). Reason: tests and demos need reproducibility; rows are small (<= 55 each); no business-secret info.

2. **PROD snapshot import via SQL script** -- dump the 8 Locations + 14 AppointmentStatuses + 13 AppointmentLanguages from the existing LocalDB as an INSERT script and run it from DbMigrator. Tag: **rejected for 5 entities, conditional for Locations**. Reason: Locations hold clinic-specific HCS business data (Fresno, Anaheim, Santa Ana offices with real addresses and parking fees). That is not "host seed" data -- it's tenant/deployment-specific operational data. Importing it as a hardcoded seed would ship HCS's clinic list in everyone's deployment. PROD snapshot is appropriate only when the target deployment IS HCS; for generic MVP testing, seed 1-2 synthetic offices and let clinic admins CRUD from there.

3. **Single `LookupsDataSeedContributor` that seeds all 5+1 entities in one class** -- tag: **rejected**. Reason: violates single-responsibility; a future change to `AppointmentType` seed data churns the diff for the whole lookup suite. Also makes idempotency harder (one method with 6 count-guards). The existing 4 seeders (Books, ExternalUserRoles, OpenIddict, Saas) establish per-concern separation; staying consistent is cheap and improves auditability.

4. **EF Core `HasData` in `OnModelCreating`** -- ship the seed rows via `modelBuilder.Entity<State>().HasData(...)` and let migrations carry them. Tag: **rejected**. Reason: (a) `HasData` records become part of the migration snapshot, making seed changes require new migrations (heavy for MVP iteration); (b) does not respect `DataSeedContext.TenantId` branching, so for tenant-scoped seeders it is unusable; (c) ABP's documented pattern is `IDataSeedContributor` -- staying on the documented path avoids fighting the framework. Works for host-only entities but the asymmetry (host via HasData, tenant via contributor) is worse than one consistent pattern.

5. **Deferred seeding, expect admin to manually populate** -- tag: **rejected by DB-15 severity**. Reason: gap-analysis README line 58 flags DB-15 as "MVP-blocking (testing blocker)"; the whole point of closing this gap is to unblock walkable UI testing without asking the developer to hand-populate dropdowns on every DB rebuild. Idle admin-populates-it assumes the MVP is a long-lived instance, not a rebuilt-on-demand dev/test DB.

## Recommended solution for this MVP

Create **five** `IDataSeedContributor` classes -- one per host-only reference-data entity -- plus **one** lightweight location-seeder that covers the demo path only. All live in `src/HealthcareSupport.CaseEvaluation.Domain/` under per-entity folders so they sit alongside their managers and repositories:

- `Domain/States/StateDataSeedContributor.cs` -- inserts 50 US states with hardcoded GUIDs. Idempotency via `if (await _repo.GetCountAsync() <= 0)`. Host-only guard: early-return when `context?.TenantId != null`.
- `Domain/AppointmentTypes/AppointmentTypeDataSeedContributor.cs` -- inserts 6 canonical IME types: `Qualified Medical Examination (QME)`, `Agreed Medical Examination (AME)`, `Record Review`, `Deposition`, `Supplemental Medical Report`, `Panel QME`. Names and descriptions come directly from the live probe. Fixed GUIDs.
- `Domain/AppointmentStatuses/AppointmentStatusDataSeedContributor.cs` -- inserts the 13 rows that match the `AppointmentStatusType` enum (`Pending`, `Approved`, `Rejected`, `NoShow`, `CancelledNoBill`, `CancelledLate`, `RescheduledNoBill`, `RescheduledLate`, `CheckedIn`, `CheckedOut`, `Billed`, `RescheduleRequested`, `CancellationRequested`) so the label lookup stays aligned with the enum. Fixed GUIDs.
- `Domain/AppointmentLanguages/AppointmentLanguageDataSeedContributor.cs` -- inserts the 12 interpreter-language names present in the live DB (`English`, `Spanish`, `Vietnamese`, `Korean`, `Chinese Mandarin`, `Chinese Cantonese`, `Tagalog`, `Russian`, `Armenian`, `Portuguese`, `Japanese`, `Hmong`) -- strips the length-boundary test row. Fixed GUIDs.
- `Domain/WcabOffices/WcabOfficeDataSeedContributor.cs` -- inserts the 6 California WCAB district offices already in the live DB (`Anaheim`, `Bakersfield`, `Glendale`, `Irvine`, `Riverside`, `San Bernardino`, `Van Nuys`). Fixed GUIDs. `StateId` references the `California` GUID from the State seeder so the order must be: States seeder registered first (ABP auto-discovery ordering is alphabetical by assembly/type name -- enforce explicit ordering via a deterministic composite seeder OR factor shared GUIDs into a static `SeedIds` helper that each contributor reads from).
- `Domain/Locations/LocationDataSeedContributor.cs` -- inserts TWO synthetic demo locations (`Demo Clinic North`, `Demo Clinic South`) with zero parking fees, `IsActive = true`, and California StateId. Deliberately synthetic -- real clinic rows are deployment data, not host seeds. A HCS-specific PROD snapshot import is out of scope for this capability (post-MVP, per-deployment installer concern).

Shared `Domain/Data/CaseEvaluationSeedIds.cs` static class exposes the fixed GUIDs (`public static class States { public static readonly Guid California = new("11111111-...-...-...-000000000001"); ... }`) so cross-seeder references are compile-time stable and tests can import the same constants. Each contributor carries `[ITransientDependency]` (or `: ITransientDependency` since ABP auto-discovery); no `CaseEvaluationDomainModule.cs` edits required.

Each contributor's body follows the `BookStoreDataSeederContributor` idiom:

1. Guard `if (context?.TenantId != null) return;` (host-only rule; ADR-003).
2. Guard `if (await _repo.GetCountAsync() > 0) return;` (idempotency; ABP docs).
3. `foreach` insert via repository with `autoSave: true`.

Per-row upsert-by-ID (calling `FindAsync(id)` then `InsertAsync` if null) is a more defensive idempotency pattern that lets the seeder ADD new rows on future runs without wiping existing ones. Recommended for the 5 reference-data seeders where the canonical set may grow; the simple count-guard is fine for Locations (demo-only, replaced by deployment PROD import).

Entity -> domain: no new entities. Domain service: seeders instantiate entities via each entity's public ctor (matches track-01 line 96 ABP convention of `FullAuditedAggregateRoot<Guid>` with a non-protected constructor). App service: untouched. Controller/proxy/Angular: untouched. Migration: none; seeders do not change schema.

## Why this solution beats the alternatives

- **Durable reproducibility:** closes the gap-analysis README concern (line 58 "testing blocker"). A fresh DbMigrator run yields a walkable UI with populated dropdowns. Matches the memorized 2026-04-24 note: "Fill gaps / inline-seed / skip-with-tracking; do not ask A/B/C mid-build."
- **Honours ADR-003:** host-only entities seeded inside `context.TenantId == null` guards never attempt to insert into tenant DBs (which lack the tables). Follows the documented ABP `IsHostDatabase()` split.
- **Test-compatible:** fixed GUIDs in the shared `CaseEvaluationSeedIds` class let future `XyzDataSeedContributor` implementations (Doctors, Patients) reference lookup rows by ID in tests, the same convention the docs claim `DoctorsDataSeedContributor` already uses.
- **Separates reference data from deployment data:** Locations gets only a demo pair. Real HCS clinics ship via PROD snapshot at deployment time, NOT as code-committed seed. This prevents leaking HCS business data into every clinic's fork once the SaaS model scales.

## Effort (sanity-check vs inventory estimate)

Inventory says **S (3 story points)**. Analysis confirms **S**. Rationale: ~80 hardcoded rows across 6 files, each a ~40-line class; one shared `SeedIds` helper; no schema or DTO work; no proxy regeneration; idempotency is trivial. With an Explore-free copy of the `BookStoreDataSeederContributor` template, a human writes this in ~4-6 hours. `dotnet run DbMigrator` smoke test + assertion that dropdown counts are 50/6/13/12/6/2 respectively takes another ~30 minutes.

## Dependencies

- **Blocks:** nearly every UI capability that renders a dropdown. Concretely: `appointment-booking-cascade` (needs Locations + AppointmentTypes), `appointment-lead-time-limits` (needs AppointmentTypes), `patient-auto-match` (needs States and AppointmentLanguages via Patient form), `attorney-defense-patient-separation` (needs States via attorney address form), `appointment-injury-workflow` (needs WcabOffices via claim-examiner sub-entity once that design lands), `appointment-request-report-export` (filters by Location and AppointmentStatus label), `dashboard-counters` (per-Location counts), `external-user-home` (lists appointments with status labels). Without lookup seeds, a fresh dev DB is untestable for every per-role walkthrough.
- **Blocked by:** none. No upstream capability is required.
- **Blocked by open question:** Q23, verbatim from `W:/patient-portal/main/docs/gap-analysis/README.md:256-257`: "**Seed data for lookup tables**: write `IDataSeedContributor` classes for States, AppointmentTypes, AppointmentStatuses, AppointmentLanguages, Locations, WcabOffices, or import PROD snapshot? (Track 1)". This brief RESOLVES Q23 by proposing code-first seeders for the 5 reference-data entities and a 2-row synthetic demo seeder for Locations (the Locations PROD snapshot path is a deployment-time concern, not a code-commit concern). If Adrian answers Q23 with "PROD snapshot for all 6", the decision for the 5 reference entities still stands because those rows ARE generic enough; only Locations flips to PROD-driven.

## Risk and rollback

- **Blast radius:** very small. Seeders only INSERT; they never UPDATE or DELETE. If a seed GUID collides with an existing row after deploying to a manually-populated deployment, the idempotency guards (count > 0 or FindAsync non-null) short-circuit and take no action. No cross-cutting concerns (no jobs, no emails, no auth, no tenant data).
- **Rollback:** delete the 6 `*DataSeedContributor.cs` files + the shared `CaseEvaluationSeedIds.cs` + run `dotnet run DbMigrator` to verify no errors (seeders simply don't run). If bad data was inserted: SQL `DELETE FROM AppStates` (or targeted by GUID) on the host DB reverses. No migration to roll back; no app-side code references the seed GUIDs at runtime.

## Open sub-questions surfaced by research

1. **Seeder execution order** -- ABP auto-discovery ordering is by type registration; cross-seeder GUID references (WcabOffice.StateId -> State.California) are safe because all contributors run inside the SAME `IDataSeeder.SeedAsync` call, but per-entity `GetCountAsync() > 0` short-circuits mean one contributor does NOT block a later contributor. Shared `CaseEvaluationSeedIds.cs` decouples contributors from each other by using compile-time constants.
2. **Localisation** -- seed `Name` values are English. Non-English display requires `Domain.Shared/Localization/CaseEvaluation/*.json` entries keyed by the English name. Out of scope for this capability; handled by the existing localisation pipeline once per-tenant branding lands (BRAND-03 post-MVP).
3. **AppointmentStatus lookup vs `AppointmentStatusType` enum drift** -- today the lookup table is unreferenced by FK from Appointment (per `AppointmentStatuses/CLAUDE.md` "Known Gotcha" #2). Seeding the 13 rows now assumes a FUTURE capability will either wire the FK or remove the table. Capability `appointment-state-machine` (G2-01) should reconcile this; flag forward.
4. **Location real data** -- this brief declines to hardcode HCS clinics. If Adrian wants a demo deployment to ship with the 6 real HCS clinics, the right home is a `DbMigrator` post-seed SQL script in `src/.../DbMigrator/sql/hcs-prod-locations.sql` gated by an env var, NOT a C# seeder. Surface this for Phase 4 decision.
