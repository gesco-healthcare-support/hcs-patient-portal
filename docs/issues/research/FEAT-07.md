[Home](../../INDEX.md) > [Issues](../) > Research > FEAT-07

# FEAT-07: Near-Zero Test Coverage -- Research

**Severity**: Medium
**Status**: Open (verified 2026-04-17)
**Source files**:
- `test/HealthcareSupport.CaseEvaluation.Application.Tests/`
- `test/HealthcareSupport.CaseEvaluation.Domain.Tests/`
- `test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/`
- `test/HealthcareSupport.CaseEvaluation.HttpApi.Client.ConsoleTestApp/`
- `test/HealthcareSupport.CaseEvaluation.TestBase/`

---

## Current state (verified 2026-04-17)

Five test projects exist (xUnit + Shouldly + Autofac + SQLite in-memory). Coverage per repo CLAUDE.md: "Only Doctors, Books, and framework services have tests. Patients, Appointments, Locations, DoctorAvailabilities, and all others are untested."

~13 test methods total. Critical business paths (booking, slot generation, external signup, tenant provisioning) have zero automated safety net.

---

## Official documentation

- [ABP Automated Testing overview](https://abp.io/docs/latest/testing/overall) -- stack: xUnit + NSubstitute + Shouldly pre-configured. SQLite in-memory default for integration tests.
- [ABP Unit Tests](https://abp.io/docs/latest/testing/unit-tests) -- pure unit tests (no DI container) with NSubstitute for `IRepository`, `ICurrentUser`, etc.
- [ABP Integration Tests](https://abp.io/docs/latest/testing/integration-tests) -- AppService-level tests through Autofac, real SQLite; recommended for business logic touching DB.
- [ABP Data Seeding](https://abp.io/docs/latest/framework/infrastructure/data-seeding) -- `IDataSeedContributor` pattern; multiple contributors coexist; run order follows DI registration.
- [ABP Book Store tutorial Part 4 -- Integration Tests](https://abp.io/docs/latest/tutorials/book-store/part-04) -- canonical `AppService_Tests` pattern with `[Collection(...)]`.
- [MS Learn -- Unit testing code coverage](https://github.com/dotnet/docs/blob/main/docs/core/testing/unit-testing-code-coverage.md) -- official Coverlet + ReportGenerator wiring for .NET.
- [ReportGenerator GitHub Action](https://github.com/danielpalme/ReportGenerator/wiki/Integration) -- `danielpalme/ReportGenerator-GitHub-Action@v5`; Cobertura input; HTML/Markdown summary.

## Community findings

- [Engincan Veske -- Testing in ABP Framework](https://engincanv.github.io/abp/test/2022/10/13/testing-in-abp-framework.html) -- ABP team member walkthrough: `AfterAddApplication` override for mocking `ICurrentUser` as singleton.
- [ABP Support #6621 -- Mocking CurrentUser for tests](https://abp.io/support/questions/6621/Mocking-CurrentUser-for-tests) -- canonical `Substitute.For<ICurrentUser>()` + `services.AddSingleton(currentUser)`.
- [ABP Support #5839 -- Switch between ICurrentUser and roles](https://abp.io/support/questions/5839/How-to-switch-inbetween-ICurrentUser-and-their-roles-in-Unit-Tests) -- swapping identities mid-test via scoped DI overrides.
- [ABP GitHub #7550 -- Base property of AppService/DomainService not mockable](https://github.com/abpframework/abp/issues/7550) -- **IMPORTANT** ABP base protected members (`LazyServiceProvider`, `CurrentUnitOfWork`) are not directly mockable -- pushes toward integration tests rather than pure unit tests for `AppointmentManager`.
- [Sean Killeen -- Beautiful .NET Test Reports Using GitHub Actions](https://seankilleen.com/2024/03/beautiful-net-test-reports-using-github-actions/) -- practical coverage pipeline with PR-comment summaries.
- [Josh Garverick -- Publish Code Coverage Summary to PR](https://josh-ops.com/posts/github-code-coverage/) -- end-to-end PR annotation from Coverlet.
- [ABP Support #5710 -- SQLite can't seed due to SQL Server-specific statements](https://abp.io/support/questions/5710/Unit-tests-are-getting-failing-because-Sqlite-cannot-seed-database-because-of-some-sql-statement-which-are-valid-for-sql-server-but-not-for-sqlite) -- test seed data must avoid SQL Server-specific DDL.

## Recommended approach

**Priority order**:
1. `AppointmentManager` create/update invariants (domain tests, no DB)
2. `AppointmentsAppService` happy-path + slot-booking side-effect (integration test with SQLite)
3. `DoctorAvailabilitiesAppService` bulk slot generation (integration)
4. `PatientsAppService` get-or-create flow (integration)

Skip status-transition tests until [FEAT-01](FEAT-01.md) introduces the state machine (tests would encode current free-for-all).

**Seed strategy**: per-feature `IDataSeedContributor` alongside `DoctorsDataSeedContributor` for each new class group. Hardcoded GUIDs so assertions are stable.

**Coverage gating**: wire Coverlet's `collect:"XPlat Code Coverage"` into existing GitHub Actions CI; ReportGenerator on Cobertura output; publish markdown summary as PR comment. Surface the number first, gate later once baseline is known.

## Gotchas / blockers

- SQLite in-memory doesn't support every T-SQL idiom; seed data must avoid SQL Server-specific DDL.
- Per CLAUDE.md, EF Core tests must be `[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]` to share a seeded DB across tests in a class.
- INFERENCE: `IDistributedLockProvider` used by ABP's permission seeding; in SQLite tests it must be stubbed or replaced with `LocalDistributedLockProvider` to avoid Redis/SQL Server dep. Check `AbpEntityFrameworkCoreSqliteModule` internals if a test hangs on startup.
- Integration tests inherit real Autofac container; mock overrides must happen in `AfterAddApplication` or they're overwritten by ABP's registration.

## Open questions

- Does existing GitHub Actions pipeline publish test results as artifacts? If yes, Coverlet can piggy-back; if no, a new job step is needed.
- Is there a business-side coverage target, or is this engineering-driven? Affects whether PR-comment is informational or gating.
- Should `HttpApi.Client.ConsoleTestApp` be converted to an integration smoke test in CI, or left as an ad-hoc developer tool?

## Related

- [FEAT-01](FEAT-01.md) -- state-machine tests depend on this
- [ARC-02](ARC-02.md) -- hoisting logic into Manager enables simpler tests
- [docs/issues/INCOMPLETE-FEATURES.md#feat-07](../INCOMPLETE-FEATURES.md#feat-07-near-zero-test-coverage)
