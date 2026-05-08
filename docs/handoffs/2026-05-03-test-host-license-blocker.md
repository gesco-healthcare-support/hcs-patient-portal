---
type: handoff
date: 2026-05-03
audience: Adrian
priority: medium
related-task: Phase 4 -- pre-existing test-infrastructure blocker
---

# Test-host process crashes on Pro license validation

## TL;DR

`dotnet test test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/`
exits with no test output and `exitcode: -42` because ABP Pro's
`Volo.Abp.Commercial.Core.LicenseChecker` calls
`Environment.Exit(-42)` during `OnApplicationInitializationAsync` when
`AbpLicenseCode` is the placeholder `PASTE_YOUR_ABP_LICENSE_CODE_HERE`.

**Fix:** paste a real license code into
`test/HealthcareSupport.CaseEvaluation.TestBase/appsettings.secrets.json`
(file is gitignored). License code lives in your ABP account portal at
<https://abp.io/my-organizations> -> "License Code".

After that, `dotnet test test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/`
should run all 60+ tests (Doctors, States, Books, AppointmentTypes,
WcabOffices, SystemParameters, etc.).

## Diagnosis trail

1. `dotnet test --blame` reported `exitcode: -42` with empty stderr.
2. Reproduced via a 30-line repro app
   (`AbpApplicationFactory.CreateAsync<CaseEvaluationEntityFrameworkCoreTestModule>`).
3. First failure mode: `NullReferenceException` in
   `Volo.Abp.BackgroundWorkers.BackgroundWorkerBase.get_Logger()` thrown
   during `AbpIdentityProDomainModule.OnApplicationInitializationAsync`.
   ABP's worker base resolves its `Logger` lazily through
   `LazyServiceProvider`, but `LazyServiceProvider` is not yet attached
   when `BackgroundWorkerManager.AddAsync` calls `worker.StartAsync`
   in the testhost lifecycle.
4. **Defensive fix applied:** added
   `Configure<AbpBackgroundWorkerOptions>(o => o.IsEnabled = false)` in
   `CaseEvaluationEntityFrameworkCoreTestModule.ConfigureServices`. This
   keeps `BackgroundWorkerManager.IsRunning = false`, so `AddAsync` does
   not start the worker (verified via Mono.Cecil decompilation of
   `BackgroundWorkerManager.AddAsync`).
5. Second failure mode (root cause): with workers disabled,
   `Environment.ExitCode = -42` is still being set, the `ProcessExit`
   handler runs, and the process terminates silently. Stack tracing
   shows `Volo.Abp.Commercial.Core.LicenseChecker.Check(...)` is invoked
   directly by the obfuscated initialization path -- not via the worker
   manager. The `LicenseChecker` is in
   `Volo.Abp.Commercial.Core.dll`, has no public interface, and its
   relevant methods (`AssertValidLicense`, `iTwnKFlRd`, etc.) are
   obfuscated -- not DI-substitutable without writing a stub assembly.

## Why placeholder license fails

`appsettings.secrets.json` ships gitignored with a placeholder so the
project builds. ABP Pro's license check reads the value, fails to parse
or fails the remote-side check, and calls `Environment.Exit(-42)`.

The correct value is the license string from your ABP organization's
portal. The CLI alone (`abp login` -> `abp login-info`) only fetches
the NuGet API key for downloading Pro packages -- the runtime license
code is separate and must be pasted into config.

## How to verify the fix works

1. Edit `test/HealthcareSupport.CaseEvaluation.TestBase/appsettings.secrets.json`:
   ```json
   { "AbpLicenseCode": "<paste_real_license_here>" }
   ```
2. `dotnet test test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/ --filter "FullyQualifiedName~Should_Get_List_Of_Books"`
3. Should print `Passed!` and `Total: 1` instead of `Test host process crashed`.

## What was kept and what was reverted

- Kept: `Configure<AbpBackgroundWorkerOptions>(o => o.IsEnabled = false)`
  in `CaseEvaluationEntityFrameworkCoreTestModule.cs`. Defensive --
  prevents the secondary NPE even if a future test scenario re-enables
  one of the Pro modules' background workers without a valid license.
- Reverted: nothing else. Source code (Phase 1+2+3) and tests are
  unchanged from the four atomic commits on `feat/replicate-old-app`.

## Phase-4 implication

With the test host blocked, Phase 4 verification falls back to the same
unit-test pattern used in Phase 3:
`InternalsVisibleTo("HealthcareSupport.CaseEvaluation.Application.Tests")`
plus pure-unit `xUnit + Shouldly` tests against the AppService's static
helpers. Integration-tier tests are written but will not execute until
the license blocker is cleared.
