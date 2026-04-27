# Critical-path test coverage (tenant provisioning + permissions + external signup + tenant filter)

## Source gap IDs

- NEW-QUAL-01 -- [../../gap-analysis/10-deep-dive-findings.md](../../gap-analysis/10-deep-dive-findings.md) Part 2 lines 98-109.

## NEW-version code read

- Test projects under `test/`: `TestBase`, `Domain.Tests`, `Application.Tests`, `EntityFrameworkCore.Tests`, `HttpApi.Client.ConsoleTestApp` (`test/CLAUDE.md:5-11`).
- Test-class count via `find test -name "*Tests.cs"` is **23**, correcting deep-dive's "17" at `10-deep-dive-findings.md:100`.
- Base-class chain: concrete -> `CaseEvaluationApplicationTestBase<TStartupModule>` -> `CaseEvaluationTestBase<TStartupModule>` -> `AbpIntegratedTest<TStartupModule>` (`test/.../Application.Tests/CaseEvaluationApplicationTestBase.cs:5-9`).
- SQLite in-memory supports real transactions + rollback (`test/CLAUDE.md:22`) -- suitable for NEW-SEC-03 orphan-tenant current-behaviour test.
- Seed orchestrator: single `CaseEvaluationIntegrationTestSeedContributor` runs in strict FK order: Tenant -> IdentityUser -> Location/State/AppointmentType -> Doctor -> Patient -> ApplicantAttorney (`test/.../TestBase/Data/CaseEvaluationIntegrationTestSeedContributor.cs:82-96`).
- Two tenants seeded (`TenantARef`, `TenantBRef`); cross-tenant isolation tests feasible without seed changes.
- Principal swap primitive: `FakeCurrentPrincipalAccessor : ThreadCurrentPrincipalAccessor` hardcoded to admin UserId (`test/.../TestBase/Security/FakeCurrentPrincipalAccessor.cs:9-25`). Per-test override via AsyncLocal is the minimal extension.
- Canonical Skip pattern in place: `AppointmentsAppServiceTests.cs:215-253` uses `[Fact(Skip = "KNOWN GAP: ...")]` for permission + state-machine gaps. Extend, don't replace.
- Production targets with zero coverage: `DoctorTenantAppService.CreateAsync`, `ExternalSignupAppService.RegisterAsync`, `OpenIddictDataSeedContributor`, permission enforcement (15 AppServices), multi-tenancy filter effectiveness, `AppointmentAccessors` + `AppointmentApplicantAttorneys` CRUD, Patient-without-IMultiTenant intentional leak.

## Live probes

N/A. Capability is pure test-coverage over existing code. Services confirmed running in `../probes/service-status.md` (Phase 1.5).

## OLD-version reference

N/A. NEW-QUAL-01 is a NEW-side quality gap; OLD stack has no equivalent coverage.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, xUnit + Shouldly + Autofac, SQLite in-memory.
- Row-level IMultiTenant (ADR-004); 7 IMultiTenant entities; Patient uniquely has `TenantId` without `IMultiTenant` (root CLAUDE.md). Tests must respect this distinction.
- HIPAA: synthetic test data only; `Bogus` + deterministic seed preferred.
- **Scope-constrained PR rule** (`memory/feedback_encode_gaps_in_tests.md`): coverage-only PR MUST NOT patch production code. Encode defects via `[Fact(Skip="KNOWN GAP: ...")]` or current-behaviour assertions. Production fix is a separate PR that inverts Skip/assertion.
- Test base-class chain + `[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]` required for EF tests (`test/CLAUDE.md:16-21`).

## Research sources consulted

- ABP Automated Testing -- https://abp.io/docs/latest/testing/overall (accessed 2026-04-24)
- ABP Integration Tests -- https://abp.io/docs/latest/testing/integration-tests (accessed 2026-04-24)
- ABP Multi-Tenancy -- https://abp.io/docs/latest/framework/architecture/multi-tenancy (accessed 2026-04-24)
- ABP Data Filtering -- https://abp.io/docs/latest/framework/infrastructure/data-filtering (accessed 2026-04-24)
- ABP Authorization -- https://docs.abp.io/en/abp/latest/Authorization (accessed 2026-04-24)
- ABP UoW Transactions (community) -- https://abp.io/community/articles/understanding-transactions-in-abp-unit-of-work-0r248xsr (accessed 2026-04-24)
- ABP Q&A #5839 -- ICurrentUser in unit tests (accessed 2026-04-24)

## Alternatives considered

A. **Add tests for all four critical paths; encode NEW-SEC-01..04 defects via Skip + current-behaviour assertions; zero production patches.** Chosen. Matches capability scope; honours feedback memory.
B. **Defer test coverage until after NEW-SEC-02..04 fixes land.** Rejected -- HIPAA + silent-regression risk are what make NEW-QUAL-01 MVP-blocking. Loses chance to encode current defect shape as regression fence.
C. **Rely on Playwright E2E as regression fence.** Conditional -- E2E complements but cannot prove `IMultiTenant` filter at repository level.
D. **Write only the transactional-rollback test (NEW-SEC-03) and defer three paths.** Rejected -- leaves 3 of 4 critical paths uncovered.

## Recommended solution for this MVP

Add four test files + one helper under existing `Application.Tests` and `EntityFrameworkCore.Tests` projects. All production defects encoded via `[Fact(Skip = "KNOWN GAP: ...")]` or current-behaviour assertions. No production code modified.

1. **`test/.../Application.Tests/SaasTenants/DoctorTenantAppServiceTests.cs`** (abstract) + EfCore concrete:
   - `CreateAsync_HappyPath` -- `[Fact(Skip = "KNOWN GAP: hardcodes Gender.Male and empty LastName; NEW-SEC-03 follow-on")]`.
   - `CreateAsync_WhenInnerStepFails_TenantRowIsOrphaned_CurrentBehaviour` -- active current-behaviour assertion; flips when NEW-SEC-03 fix lands.
   - `CreateAsync_WithoutSaasTenantsCreatePermission_ShouldThrow` -- `[Fact(Skip="KNOWN GAP: NEW-SEC-02")]`.
2. **`test/.../Application.Tests/ExternalSignups/ExternalSignupAppServiceTests.cs`**:
   - `RegisterAsync_AsPatient_CreatesUserAndPatient` -- green.
   - `RegisterAsync_AsPatient_SetsHardcodedDefaults_CurrentBehaviour` -- assert Gender.Male / today-DOB / Home phone; flips when NEW-SEC-04 lands.
   - Duplicate-email / missing-tenant / attorney-does-not-create-patient -- green.
3. **`test/.../Application.Tests/Permissions/{Entity}MethodAuthorizationTests.cs`** -- one file per AppService missing method-level `[Authorize(...Create/Edit/Delete)]` per NEW-SEC-02. 15 files x 3 Skip facts = 45 Skip-tests. Plus helper `test/.../TestBase/Security/TestCurrentPrincipalScope.cs` to scope an override principal per test.
4. **`test/.../EntityFrameworkCore.Tests/MultiTenancy/TenantFilterTests.cs`**:
   - `AppointmentsRepository_TenantB_CannotReadTenantA_Data` -- seed one Appointment in TenantA, switch to TenantB, assert `FindAsync` returns null.
   - `DoctorsRepository_TenantB_CannotReadTenantA_Data`, `ApplicantAttorneysRepository_TenantIsolation_IsAutomatic` -- green today.
   - `PatientsRepository_TenantId_DoesNotIsolate_CurrentBehaviour` -- current-behaviour assertion proving intentional Patient leak.
   - `HostDataFilterDisable_AllowsCrossTenantRead_HappyPath` -- canary via `IDataFilter.Disable<IMultiTenant>()`.

## Why this solution beats the alternatives

- Encodes all four critical paths without patching production code -- respects scope-constrained-PR rule (`memory/feedback_encode_gaps_in_tests.md`).
- Reuses existing orchestrator seed + base-class chain -- no new projects, no new NuGet.
- Skip-tests + current-behaviour assertions are green today, name the defect; NEW-SEC-02/03/04 fix PRs find ready-made inversion targets.
- SQLite in-memory supports transactional rollback -- feasible for NEW-SEC-03 orphan-tenant test.

## Effort (sanity-check vs inventory estimate)

Inventory: **M (3-5 days)**. Analysis confirms **M, lean (~3 days)**. 4 test files (~400-500 LOC aggregate), one helper (~40 LOC), ~45 Skip stubs (~5 LOC each).

## Dependencies

- **Blocks:** nothing. Tests are a fence, not a prerequisite.
- **Blocked by (logically, not strictly):** `new-sec-02-method-level-authorize`, `new-sec-03-transactional-tenant-provisioning`, `new-sec-04-external-signup-real-defaults`. Coverage lands BEFORE fixes; each fix PR flips Skip / inverts assertion.
- **Blocked by open question:** none.

## Risk and rollback

- **Blast radius:** test-only. No production code changes.
- **Rollback:** `git revert` the feature commit. Delete 4 new files + helper.

## Open sub-questions surfaced by research

1. Does `Volo.Saas.Host.TenantAppService` already carry class-level `[Authorize(SaasHostPermissions.Tenants.Create)]`? If yes, the permission Skip-test for `DoctorTenantAppService.CreateAsync` may be a no-op.
2. File shape for permission tests: one file with 45 `[Theory]` cases, or 15 per-entity files with 3 `[Fact]` each? Recommend 15-file structure for single-file inversion per fix.
3. Seeding one Appointment for `TenantFilterTests` -- confirm with any pending Phase-B6 orchestrator extension that test-local seeds are acceptable.
