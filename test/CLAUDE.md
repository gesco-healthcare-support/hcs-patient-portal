# Test Suite

Five test projects that cover the domain, application, EF Core, and a console test app for the HTTP API client. All use xUnit + Shouldly + Autofac DI. For counts and coverage, run the commands in [docs/testing/coverage-status.md](../docs/testing/coverage-status.md); this file stores no figures.

## What Lives Here

- **`HealthcareSupport.CaseEvaluation.TestBase/`** -- shared infrastructure, base classes, seed contributors, localization test data
- **`HealthcareSupport.CaseEvaluation.Domain.Tests/`** -- domain entity and domain service unit tests
- **`HealthcareSupport.CaseEvaluation.Application.Tests/`** -- AppService tests, uses in-memory SQLite
- **`HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/`** -- repository tests, uses in-memory SQLite
- **`HealthcareSupport.CaseEvaluation.HttpApi.Client.ConsoleTestApp/`** -- manual console app for ad-hoc HTTP API exploration (not automated)

## Conventions

1. **Base class chain:** Concrete tests inherit from `CaseEvaluationApplicationTestBase` or `CaseEvaluationDomainTestBase`, which in turn inherit from `CaseEvaluationTestBase<TModule>`. Do not skip the chain -- it wires up Autofac, `appsettings.json`, and test data seed contributors.

2. **EF-backed test classes take NO `[Collection]` attribute.** Each test builds its own ABP application and its own in-memory SQLite database, so xUnit runs the classes in parallel, one collection per class (its default), and a run uses every core (#1034). Only three named collections are allowed: MultiOffice and RealAuthorization, because their NAMED shared-cache databases outlive a single test and must be used by one test at a time, and `SqlServerCollection`, which shares one SQL Server container. A new `[Collection]` needs a stated reason and an entry in the allowlist of `TestCollectionAllowlistTests`, which fails on any other collection. The old shared name, `CaseEvaluationTestConsts.CollectionDefinitionName`, is `[Obsolete(error: true)]` so a leftover attribute fails to compile. Anything static that a seed or test writes is shared by classes running at the same time: keep test data in fixed values (see `TenantsTestData`), never in state one application writes and another reads.

3. **SQLite in-memory, with two exceptions.** The test infrastructure uses SQLite in-memory, so almost every test runs fast and needs no SQL Server. `CaseTrackerFeedSqlServerTests` and `SqlAppLockTests` start a real SQL Server container (`SqlServerFeedFixture`), so they need Docker running.

4. **Autofac, not default .NET DI.** Replace / substitute dependencies with `application.ServiceProvider.GetRequiredService<T>()` or via Autofac `IContainer` overrides inside the module override pattern.

5. **Test data via the integration seeder.** Shared test data is seeded by `CaseEvaluationIntegrationTestSeedContributor`, with fixed ids in the `Data/*TestData.cs` classes that tests assert against. Add shared data there rather than creating a second contributor, because ABP does not order contributors.

6. **HIPAA: never use real patient data.** All test fixtures must use synthetic data (fake names, fake DOBs, fake SSNs). This is enforced by `.claude/rules/hipaa-data.md` and `.claude/rules/test-data.md`.

7. **Where coverage stands:** read it from a run (`docs/testing/coverage-status.md`), not from a list here. New behaviour gets a test that is seen to fail first.

## Key Files

| File | Purpose |
|------|---------|
| `HealthcareSupport.CaseEvaluation.TestBase/CaseEvaluationTestBase.cs` | Generic base for all tests |
| `HealthcareSupport.CaseEvaluation.TestBase/CaseEvaluationTestConsts.cs` | Shared constants; the old collection name is kept only as a compile-time error |
| `HealthcareSupport.CaseEvaluation.TestBase/Data/CaseEvaluationIntegrationTestSeedContributor.cs` and `Data/*TestData.cs` | The single integration-test seeder, and the fixed ids tests assert against |
| `HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/EntityFrameworkCore/CaseEvaluationEntityFrameworkCoreTestBase.cs` | Base for repo tests (SQLite) |
| `HealthcareSupport.CaseEvaluation.Application.Tests/CaseEvaluationApplicationTestBase.cs` | Base for AppService tests |

## Running Tests

See docs/devops/TESTING-STRATEGY.md for the full test strategy. To run: `dotnet test` (all projects), `dotnet test test/<ProjectName>` (one project), or `dotnet test --filter "FullyQualifiedName~MethodName"` (single test).

## Related Docs

- [Root CLAUDE.md](../CLAUDE.md) -- Testing section
- [docs/devops/TESTING-STRATEGY.md](../docs/devops/TESTING-STRATEGY.md)
- [Project HIPAA Rules](../.claude/rules/hipaa-data.md)
- [Project Test Data Rules](../.claude/rules/test-data.md)
