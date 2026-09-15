# .NET Environment Configuration

When **running or building** the app (`dotnet run`, `dotnet build`, `dotnet ef`), set
`DOTNET_ENVIRONMENT=Development` and `ASPNETCORE_ENVIRONMENT=Development` unless
explicitly told otherwise. This loads development-mode configuration
(`appsettings.Development.json`).

Example: `DOTNET_ENVIRONMENT=Development dotnet run --project src/HealthcareSupport.CaseEvaluation.HttpApi.Host`

## DO NOT set it for `dotnet test`

This rule previously said "dotnet test" as well. That was wrong, and it breaks the
EntityFrameworkCore test suite outright. Measured on 2026-09-13:

```text
DOTNET_ENVIRONMENT=Development dotnet test ...EntityFrameworkCore.Tests
  Failed: 408, Passed: 110, Skipped: 12, Total: 530

dotnet test ...EntityFrameworkCore.Tests          (no environment set)
  Failed:   0, Passed: 518, Skipped: 12, Total: 530
```

The second line is exactly what CI reports, because CI does not set the variable.

**Why.** The demo seed contributors gate themselves on the environment name, read
straight from the variable:

- `OfficeSeedDataContributor.cs:54` runs only in Development, and takes an
  `ITenantConnectionStringProvider` dependency (`:29`)
- `DemoExternalUsersDataSeedContributor.cs:232-236` reads
  `ASPNETCORE_ENVIRONMENT ?? DOTNET_ENVIRONMENT` directly
- `DemoPatientDataSeedContributor` likewise

In Development those seeders wake up inside the test harness. They resolve
`TenantConnectionStringProvider`, which needs `App:TenantDbTemplate` or
`ConnectionStrings:Default`, and
`test/HealthcareSupport.CaseEvaluation.TestBase/appsettings.json` deliberately
carries neither. Every test in the assembly then fails during module
initialisation with:

```text
Volo.Abp.AbpException : No base connection string is configured.
Set 'App:TenantDbTemplate' or ConnectionStrings:Default.
```

**Setting `App__TenantDbTemplate` does NOT fix it, and that is worth knowing** --
it is the obvious next move and it fails silently in a confusing way.
`CaseEvaluationTestBase.cs:22-24` builds its configuration from `appsettings.json`
plus `appsettings.secrets.json` and never calls `AddEnvironmentVariables()`, so the
variable is not read at all. The fix is to stop setting the environment, not to
supply a connection string.

The tests need **no SQL Server, no connection string and no Docker stack.**

## Summary

| command | environment |
| --- | --- |
| `dotnet run`, `dotnet ef` | `DOTNET_ENVIRONMENT=Development` |
| `dotnet build` | either; it does not read it |
| `dotnet test` | **leave unset** |
