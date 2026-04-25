# NEW-SEC-03: DoctorTenantAppService.CreateAsync non-transactional

## Source gap IDs

- [NEW-SEC-03](../gap-analysis/10-deep-dive-findings.md#new-sec-03----doctortenantappservicecreateasync-is-non-transactional-partial-failure-leaves-orphaned-tenants-mvp-blocking) -- non-transactional UoW around SaaS tenant creation plus IdentityUser / Doctor / Role creation; partial failure leaves an orphan `SaasTenant` row.
- Sub-defects tracked under the same ID in the gap doc:
  - Hardcoded `Gender.Male` at `DoctorTenantAppService.cs:141` for every tenant-provisioned Doctor.
  - Hardcoded empty `LastName` (`""`) at `DoctorTenantAppService.cs:139` for every tenant-provisioned Doctor.

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorTenantAppService.cs:49-71` -- `CreateAsync` override. Opens an explicit UoW with `_unitOfWorkManager.Begin(requiresNew: true, isTransactional: false)` at line 57, calls `base.CreateAsync(input)` inside it (creates the `SaasTenant` row in the host DB), completes that UoW, then switches to `CurrentTenant.Change(tenant.Id)` at line 62 and calls `CreateDoctorUserAsync`, `CreateDoctorProfileAsync`, and `EnsureRoleAsync` outside the UoW.
- `DoctorTenantAppService.cs:57` -- the defect. `isTransactional: false` means `base.CreateAsync` writes the `SaasTenant` row through the ambient EF Core connection with no transaction wrapping. When `uow.CompleteAsync()` at line 60 returns, the row is committed. Any subsequent throw in lines 62-69 cannot roll that row back.
- `DoctorTenantAppService.cs:64` -- `CreateDoctorUserAsync` calls `IdentityUserManager.CreateAsync(adminUser, input.AdminPassword)`. ASP.NET Identity's password validators run here (length, required chars, uniqueness). A weak password or a clashing email throws `UserFriendlyException` at line 102, leaving the SaasTenant orphaned.
- `DoctorTenantAppService.cs:66` -- `CreateDoctorProfileAsync` constructs a `Doctor` via the aggregate constructor. That constructor enforces `Check.NotNull(lastName, ...)` and `Check.Length(lastName, ..., DoctorConsts.LastNameMaxLength, 0)` -- passing `""` succeeds today only because the Check helpers treat empty string as valid for length 0. If `LastNameMaxLength` or the Check contract ever tighten (e.g. `NotNullOrWhiteSpace`), this call throws and orphans the tenant + IdentityUser.
- `DoctorTenantAppService.cs:135-142` -- `new Doctor(..., firstName: input.Name, lastName: "", email: user.Email, gender: Gender.Male)`. Two independent defects:
  - Line 138: `firstName: input.Name` overloads the tenant name as the doctor's first name. Tenant Name = "Acme Clinic" then seeds a Doctor named "Acme Clinic" / "". Correct semantic separation is missing.
  - Line 139: `lastName: ""` hardcoded. Operator has to edit the DB or re-save the Doctor through `DoctorsAppService.UpdateAsync` after onboarding.
  - Line 141: `gender: Gender.Male` hardcoded. Every female or other-gender doctor is provisioned with the wrong enum value.
- `SaasTenantCreateDto` (Volo SaaS) ships with `Name`, `AdminEmailAddress`, `AdminPassword`, `EditionId?`, and an `ExtraProperties` bag. There is no Gender / LastName / FirstName on it. Either `SaasTenantCreateDto` must be extended (new derived DTO on the NEW side) or the Doctor fields must go through a separate post-provisioning collection step.
- `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/Doctor.cs:40-56` -- aggregate constructor validates FirstName, LastName, Email, and Gender. LastName accepts length 0 but requires non-null, so the current `""` value passes `Check.NotNull` + `Check.Length(lastName, ..., 50, 0)`. That is fragile: `DoctorManager.CreateAsync` at `src/.../Domain/Doctors/DoctorManager.cs:33-34` uses `Check.NotNullOrWhiteSpace(lastName, ...)`. The tenant path bypasses `DoctorManager` and uses the raw constructor, so it slips past the stricter contract used by the standard CRUD path.
- `src/HealthcareSupport.CaseEvaluation.Domain/Data/CaseEvaluationTenantDatabaseMigrationHandler.cs:44-50` -- a distributed event `TenantCreatedEto` handler. When `SaasTenant` inserts, ABP publishes this event AFTER the outer UoW commits. This handler runs tenant-DB migrations and seeds the default admin. Rolling the outer UoW back via `isTransactional: true` prevents the `TenantCreatedEto` from being published in the first place (ABP's `IDistributedEventBus` default is outbox-style publish-on-UoW-complete), so the tenant DB is never created on failure. This is the behaviour we want.
- `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/Doctor.cs:15` -- `Doctor : FullAuditedAggregateRoot<Guid>, IMultiTenant`. The Doctor row lives in the tenant DB after `CurrentTenant.Change(tenant.Id)`. A single host-level transaction CANNOT roll back both the host-DB `SaasTenant` row and a tenant-DB `Doctor` row (cross-database transaction would require MSDTC; ABP does not orchestrate that). Practical rollback must therefore abort BEFORE the tenant DB is touched (i.e. before `CurrentTenant.Change`) OR rely on the handler pattern where tenant-DB creation is driven by the `TenantCreatedEto` which only fires on host-DB commit.

## Live probes

- Live Verification Protocol in `docs/implementation-research/README.md:247-261` explicitly forbids state-mutating probes for SaaS tenant creation ("Never probe SaaS tenant creation ... persistent state manual cleanup might miss"). No mutating probe is run for this brief.
- Read-only probe from Phase 1.5 reproduced the missing multi-tenancy management endpoint: `GET /api/multi-tenancy/tenants` returns **HTTP 404** (see `probes/service-status.md:28-30`). This confirms the tenant management module is not exposed over HTTP in the NEW build, so even if a mutating probe were allowed, the only call path is the internal `DoctorTenantAppService` via the `api/app/doctor-tenant/*` route. See probe log at `../probes/new-sec-03-transactional-tenant-provisioning-2026-04-24T1550.md` for the recorded static analysis + the cited read-only probe reference.

## OLD-version reference

Not applicable. NEW-SEC-03 is a defect introduced on the NEW side; the OLD codebase does not use ABP SaaS tenant provisioning (OLD is PHP/Laravel-based per the Gesco project pipeline). No OLD-version `path:line` citation exists.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, OpenIddict, Volo.Saas module (reference pattern: `DoctorTenantAppService extends Volo.Saas.Host.TenantAppService`).
- Row-level `IMultiTenant` (ADR-004 at `../decisions/004-doctor-per-tenant-model.md`): the Doctor IS the tenant. Cross-database rollback with MSDTC is not available; rollback must be confined to the host DB phase.
- Mapperly (ADR-001), manual controllers (ADR-002), dual DbContext (ADR-003): a new DTO extension (e.g. `CaseEvaluationSaasTenantCreateDto : SaasTenantCreateDto`) must add its mapper in `CaseEvaluationApplicationMappers.cs` and its controller wiring in `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Doctors/DoctorTenantController.cs`.
- HIPAA applicability: onboarding a Doctor is the root of all PHI access for a tenant. A half-provisioned tenant with an orphan SaasTenant row can be picked up by the `TenantCreatedEto` handler and get its tenant DB migrated even though no Doctor exists -- an auditable and operationally fragile state. Transactional provisioning eliminates that class of failure.
- No `ng serve` / ADR-005. Angular-side changes (tenant-create form additions for Gender / LastName) must be rebuilt via `ng build` + `npx serve`.
- Doctor aggregate constructor at `Doctor.cs:40` enforces FirstName/LastName/Email/Gender invariants; any DTO extension must supply real values that pass those checks. `DoctorManager.CreateAsync` enforces `NotNullOrWhiteSpace` on LastName, so once the DTO surface widens, the tenant path should migrate to `DoctorManager.CreateAsync` rather than the raw constructor, to share the same invariant with the standard CRUD path.

## Research sources consulted

- [ABP Unit of Work -- official docs](https://abp.io/docs/en/abp/latest/Unit-Of-Work) -- accessed 2026-04-24. Confirms `Begin(requiresNew, isTransactional)` semantics: `requiresNew: true` ignores the ambient UoW and starts a fresh one; `isTransactional: true` wraps EF Core operations in a real transaction that rolls back on exception before `CompleteAsync`. Default `isTransactional` is `false`.
- [ABP DDD Unit of Work](https://abp.io/docs/latest/framework/architecture/domain-driven-design/unit-of-work) -- accessed 2026-04-24. States "rolled back (on exception) all together" for transactional UoWs before `CompleteAsync`.
- [ABP SaaS module overview](https://abp.io/docs/latest/modules/saas) -- accessed 2026-04-24. Documents shared-vs-separate DB tenant configuration. Confirms that tenant DB creation is NOT part of `TenantAppService.CreateAsync`; it is triggered by the `TenantCreatedEto` distributed event handler AFTER the host-DB transaction commits. Host-DB rollback before event publication prevents the tenant DB from being provisioned in the first place.
- [ABP `UnitOfWorkAttribute` source / API reference](https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.Uow/Volo/Abp/Uow/UnitOfWorkAttribute.cs) -- accessed 2026-04-24 (via WebFetch; exact file path stable across 8.x/9.x/10.x). Confirms named attribute usage (`[UnitOfWork(IsTransactional = true)]`) vs programmatic `_unitOfWorkManager.Begin(requiresNew, isTransactional)` equivalence.
- [ABP distributed events + outbox pattern](https://abp.io/docs/latest/framework/infrastructure/event-bus/distributed) -- accessed 2026-04-24. Confirms `IDistributedEventBus.PublishAsync` inside a UoW defers publication to `OnCompleted`, so a rollback suppresses the event.
- Repo reference for existing `isTransactional: true` precedent: `src/HealthcareSupport.CaseEvaluation.Domain/Data/CaseEvaluationTenantDatabaseMigrationHandler.cs:113` -- already demonstrates a nested `Begin(requiresNew: true, isTransactional: true)` around `_dataSeeder.SeedAsync`. The same pattern applies here.

## Alternatives considered

- **A. Single `isTransactional: true` UoW wrapping base tenant creation + inner identity/doctor/role steps (chosen).**
  One call to `_unitOfWorkManager.Begin(requiresNew: true, isTransactional: true)` around the entire body of `CreateAsync`. The `SaasTenant` write and the subsequent IdentityUser / Doctor / Role writes participate in the same host-DB transaction for host-DB-bound writes. The `IdentityUser` and `Doctor` rows are tenant-DB-bound after `CurrentTenant.Change`, but per the ABP SaaS module the tenant DB is not created inside `CreateAsync` -- it is created by the `TenantCreatedEto` handler on UoW commit. An exception inside the UoW scope prevents the event from publishing, so the tenant DB is never created and no orphaned cross-DB state exists.
  Trade-off: the IdentityUser and Doctor writes live in the tenant DB, so when the separate-DB mode is configured they happen through a distinct `DbContext`. ABP's `AbpUnitOfWorkDbContextProvider` opens each new `DbContext` under the same ambient transaction when the same UoW is active, so rollback covers them. The cross-database coverage depends on SQL Server being the backing store and ABP's `IUnitOfWork.SetShareSameTransaction` behaviour (default for SQL Server in ABP 10.x).

- **B. Two nested UoWs -- outer `isTransactional: true` for host DB, inner `isTransactional: true` for tenant DB (rejected).**
  More explicit but over-engineered. The inner UoW cannot coordinate with the outer via a distributed transaction on SQL Server without MSDTC, and ABP 10.x default behaviour already shares the ambient transaction within a single UoW across multiple `DbContext`s. Adds code without adding safety.

- **C. Try/catch with manual rollback step (rejected).**
  Wrap base.CreateAsync in a try/catch; on catch, call `_tenantRepository.DeleteAsync(tenantId)` to undo. Rejected because (a) ABP's `IDistributedEventBus` may already have published `TenantCreatedEto` before we catch, triggering tenant-DB creation; (b) the compensating delete can itself throw and leave the system in an inconsistent state; (c) transactional UoW is the idiomatic ABP pattern and matches the existing precedent at `CaseEvaluationTenantDatabaseMigrationHandler.cs:113`.

- **D. `IDomainEventHandler<EntityCreatedEventData<SaasTenant>>` handling the post-steps asynchronously (rejected).**
  Move IdentityUser / Doctor / Role creation into a handler that fires after the SaasTenant commits. This is how `CaseEvaluationTenantDatabaseMigrationHandler` already migrates the tenant DB. Rejected as the main remediation because (a) the user-facing `POST /api/app/doctor-tenant` call needs to report IdentityUser / Doctor creation failures synchronously -- a handler pattern would surface failures only through logs; (b) the handler still needs its own transactional UoW, so the transactional fix at A is a prerequisite regardless.

- **E. Keep `isTransactional: false` and rely on manual operator cleanup (rejected).**
  Status quo. Documented MVP-blocker; ruled out by the defect definition.

## Recommended solution for this MVP

**WHAT:** Make `DoctorTenantAppService.CreateAsync` atomic and remove the hardcoded Doctor identity values.

**WHERE:**

1. `src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorTenantAppService.cs:57` -- change the flag: `_unitOfWorkManager.Begin(requiresNew: true, isTransactional: true)`. Keep the entire body inside that single UoW (move `CurrentTenant.Change` block and its three inner calls inside the `using` block; remove the intermediate `uow.CompleteAsync()` so there is exactly one `CompleteAsync` at the end). Alternative expression as an attribute: replace the programmatic `Begin` with `[UnitOfWork(IsTransactional = true, RequiresNew = true)]` on `CreateAsync` -- functionally identical; pick whichever is consistent with surrounding style (programmatic `Begin` is used across the repo).

2. `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Doctors/CaseEvaluationSaasTenantCreateDto.cs` (new file) -- derive from `Volo.Saas.Host.Dtos.SaasTenantCreateDto` and add `FirstName`, `LastName`, `Email` (if not already mapped from AdminEmailAddress), `Gender` properties with `[Required]` attributes. This supersedes the hardcoded `Gender.Male` and `lastName: ""` at lines 137-142. Angular tenant-onboarding form must collect these values (see Angular-side note below).

3. `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Doctors/IDoctorTenantAppService.cs` -- widen the `CreateAsync` signature to accept the derived DTO, OR override a second method like `CreateAsync(CaseEvaluationSaasTenantCreateDto)` while keeping the base signature. The second pattern is safer for ABP proxy regeneration and does not fight the Volo.Saas base.

4. `src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorTenantAppService.cs:135-142` -- replace the raw `new Doctor(...)` construction with `_doctorManager.CreateAsync(appointmentTypeIds: new(), locationIds: new(), identityUserId: user.Id, firstName: input.FirstName, lastName: input.LastName, email: user.Email, gender: input.Gender)`. This shares the invariants used by the standard CRUD path (`Check.NotNullOrWhiteSpace(lastName)`).

5. Angular: `angular/src/app/doctors/doctor-tenant/...` -- add FirstName / LastName / Gender form controls to the tenant creation form. Rebuild via `ng build --configuration development` + `npx serve` (ADR-005 prohibits `ng serve`).

6. Tests: add to `test/HealthcareSupport.CaseEvaluation.Application.Tests/Doctors/` a new `DoctorTenantAppService_Tests.cs` covering:
   - `CreateAsync_Rolls_Back_SaasTenant_When_IdentityUser_Creation_Fails` -- seed a conflicting IdentityUser (same email, different tenant) so `CreateDoctorUserAsync` throws, assert no `SaasTenant` row persists.
   - `CreateAsync_Persists_All_Aggregates_On_Success` -- happy path, assert tenant + IdentityUser + Doctor + Doctor role all present.
   - `CreateAsync_Honors_Supplied_Gender_And_LastName` -- assert the Doctor row reflects the DTO values, not `Gender.Male` + `""`.

**SHAPE:** DTO + Mapperly mapper -> Application (`DoctorTenantAppService.CreateAsync`) -> Domain (`DoctorManager.CreateAsync`) -> EF Core + Volo.Saas -> HttpApi controller `DoctorTenantController` -> Angular proxy regeneration via `abp generate-proxy` (orchestrator, not subagent) -> Angular form.

**No migration required** because no schema change is introduced. `SaasTenantCreateDto` extensions live on the DTO level; Doctor table already has LastName / Gender columns.

## Why this solution beats the alternatives

- Transactional UoW is the idiomatic ABP pattern. The same `requiresNew: true, isTransactional: true` usage already exists at `CaseEvaluationTenantDatabaseMigrationHandler.cs:113`, so the fix matches existing code convention and ADR alignment.
- Single-UoW rollback covers host-DB commit + `TenantCreatedEto` publication in one gesture. Because the event is deferred to UoW commit by ABP's distributed event outbox, a rollback prevents downstream tenant-DB migration from ever firing, avoiding the cross-DB orphan class without needing MSDTC.
- Using `DoctorManager.CreateAsync` instead of the raw constructor aligns invariants between the tenant provisioning path and the standard CRUD path, so a single change to `LastName` validation (e.g. `NotNullOrWhiteSpace`) would apply uniformly.
- Adding real Gender / LastName collection in the signup form is a one-time UI touch that removes every downstream data-quality remediation ticket tied to "Why are all my doctors Male with no LastName".

## Effort (sanity-check vs inventory estimate)

Inventory says S (0.5 day). Analysis confirms S for the transactional flip + test additions, plus a small increment (still S, ~0.5-1 day) for the DTO extension + Angular form. Total envelope remains S.

Breakdown:
- Change line 57 + restructure the UoW scope: 15 min.
- Derive `CaseEvaluationSaasTenantCreateDto` + mapper + controller route: 1 hr.
- Angular form update + rebuild: 1 hr.
- Two xUnit tests (tenant rollback + happy path): 2-3 hr.

## Dependencies

- **Blocks:** nothing. Tenant onboarding is a bootstrap step; other capabilities consume the outcome but do not depend on the wiring of this change.
- **Blocked by:** none. No capability needs to land first; this is a self-contained fix.
- **Blocked by open question:** none. Neither Adrian's 32 open questions nor the gap-analysis README flag scope ambiguity here. Remediation is unambiguous.

## Risk and rollback

- **Blast radius:** `POST /api/app/doctor-tenant` and the tenant-create Angular form. No other endpoints are impacted. Existing tenants are untouched. Downstream `TenantCreatedEto` handler at `CaseEvaluationTenantDatabaseMigrationHandler.cs:44` continues to behave identically on success; on failure it simply never fires (correct behaviour).
- **Rollback:** revert the commit on `main`; no schema migration to unwind. If the DTO extension was merged with Angular changes, revert both together. `git revert <sha>` is the full procedure.

## Open sub-questions surfaced by research

- **Q-SEC03-1:** Does the NEW codebase currently configure separate-DB-per-tenant mode, or is it shared-DB mode only? `appsettings.json:10` declares one `Default` connection string and no per-tenant override; this suggests shared-DB by default. In shared-DB mode, all rows (SaasTenant + IdentityUser + Doctor + Role) live in the same physical DB and a single host transaction definitely covers the lot. Worth confirming before shipping.
- **Q-SEC03-2:** Should the tenant-create form also collect Email separately from `AdminEmailAddress`? Today the Doctor's email is mirrored from the admin user's email. That is acceptable for MVP (one doctor per tenant, one account, one email), but note for post-MVP when operational email and login email might diverge.
- **Q-SEC03-3:** Should `CaseEvaluationSaasTenantCreateDto` also capture AppointmentType / Location assignments so the Doctor is usable immediately, or is the current post-onboarding "Add appointment types / locations" step good enough? Deferring to MVP scope -- not required for NEW-SEC-03 itself.
