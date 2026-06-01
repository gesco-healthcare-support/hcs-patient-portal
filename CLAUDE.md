# HCS Patient Portal -- CLAUDE.md (repo hub)

Workers'-comp Independent Medical Examination (IME) scheduling portal. .NET 10 / ABP
Commercial 10.0.2 (C#), Angular 20 standalone SPA, SQL Server + EF Core (code-first),
Riok.Mapperly source-gen mappers, OpenIddict. Root namespace
`HealthcareSupport.CaseEvaluation` (projects at `src/HealthcareSupport.CaseEvaluation.<Layer>/`).
Multi-tenant scaffolding is wired; the Phase-1 target is one demo tenant = one doctor's office.

<!-- Hub file: global, cross-cutting rules only, plus the Map below. Layer/feature detail
     lives in nested CLAUDE.md (loaded lazily when Claude reads that directory) and in docs/
     (loaded on demand by plain path). Keep this file lean -- only the repo-root CLAUDE.md is
     re-injected after /compact, so anything global must live here. Reference docs by path,
     never @import (imports load at launch and save no context). -->

## Build + run (dev)

- IMPORTANT: dev runs in Docker only. Do NOT `dotnet run` the AuthServer or HttpApi.Host, and
  do NOT `ng serve` -- HSTS redirects and Vite `CORE_OPTIONS` duplication break the stack.
  One-shot: `docker compose up -d --build` at the worktree root.
- Angular outside Docker: `npx ng build --configuration development` then
  `npx serve -s dist/CaseEvaluation/browser -p 4200`. Never `ng serve` / `yarn start` /
  `ng build --watch` (Vite duplicates the `CORE_OPTIONS` InjectionToken -> ABP DI `NullInjector`).
- Cold-start order: SQL -> AuthServer -> HttpApi.Host -> Angular. Out of order breaks
  permission seeding and JWT validation.
- Ports: AuthServer 44368, API 44327, Angular 4200. dotnet CLI needs
  `DOTNET_ENVIRONMENT=Development` + `ASPNETCORE_ENVIRONMENT=Development`.
- Windows path limit ~260 chars (SNI.dll failure) -- keep worktrees short
  (`C:\src\patient-portal\...`).

## Binding conventions (the layer files below elaborate each)

- AppServices extend `CaseEvaluationAppService` and carry `[RemoteService(IsEnabled=false)]`;
  a manual controller (`AbpController`, implements `I<Entity>AppService`, route
  `api/app/<entity-plural>`) is the only HTTP surface. Anonymous surfaces use `api/public/*`.
- Mappers: Riok.Mapperly only -- `partial class [Mapper] : MapperBase<,>` in
  `CaseEvaluationApplicationMappers.cs` (split across 6 partial files). Never AutoMapper,
  never `ObjectMapper.Map<>`.
- Permissions: nested static class in `CaseEvaluationPermissions.cs` AND registered in the
  `DefinitionProvider`. Localization: a key must exist in Domain.Shared `en.json` before any
  `L()` / `| abpLocalization` reference.
- Two DbContexts: `CaseEvaluationDbContext` (host; `IsHostDatabase()` guards host-only tables)
  and `CaseEvaluationTenantDbContext`. IMPORTANT: `Patient` is NOT `IMultiTenant` -- every
  Patient query must filter `TenantId` by hand; this is the PHI cross-tenant guard.
- Business rules + invariants live in domain managers (e.g. `AppointmentManager`); AppServices
  orchestrate only. Never edit `angular/src/app/proxy/` -- regenerate via `abp generate-proxy`.
- `appsettings.secrets.json` holds the ABP license + SMTP creds -- treat as sensitive even
  when not gitignored.

## HIPAA

Synthetic data only in code, tests, fixtures, examples (emails `@gesco.com` / `@example.test`,
`555` phones, no real SSN/MRN/DOB). Redact identifiers at log boundaries. See
`.claude/rules/hipaa-data.md`.

## Map (load on demand -- plain paths, never @import)

Layers:
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CLAUDE.md` -- enums, consts, localization, ETOs
- `src/HealthcareSupport.CaseEvaluation.Domain/CLAUDE.md` -- entities, managers, repos (richest layer)
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/CLAUDE.md` -- DTOs, IAppService, permissions
- `src/HealthcareSupport.CaseEvaluation.Application/CLAUDE.md` -- AppService impls + Mapperly mappers
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/CLAUDE.md` -- two DbContexts, configs, migrations
- `src/HealthcareSupport.CaseEvaluation.HttpApi/CLAUDE.md` -- manual controllers
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CLAUDE.md` -- runnable API host, Hangfire jobs, rate limit
- `src/HealthcareSupport.CaseEvaluation.AuthServer/CLAUDE.md` -- OpenIddict + Razor auth UI
- `src/HealthcareSupport.CaseEvaluation.DbMigrator/CLAUDE.md` -- one-shot migrate + seed
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Client/CLAUDE.md` -- generated client proxies
- `angular/src/app/CLAUDE.md` -- Angular SPA shell + feature conventions
- `test/CLAUDE.md` -- test stack + coverage gaps

Key docs:
- `docs/INDEX.md` -- documentation map (start here)
- `docs/decisions/` -- ADRs (why we chose X)
- `docs/architecture/`, `docs/api/`, `docs/backend/`, `docs/database/` -- descriptive references
- `docs/security/` -- PHI data flows, threat model, authorization matrix
- `docs/runbooks/LOCAL-DEV.md`, `docs/runbooks/DOCKER-DEV.md` -- dev/ops procedures
- `docs/parity-v2/` -- legacy-parity gap analysis + `docs/parity-review-log.csv` decision log
- `docs/design/_design-tokens.md` -- legacy colors/fonts for visual parity

## Legacy parity

This app reproduces the behavior of the legacy single-tenant Patient Portal on the modern
stack. The gap analysis and decision register live in `docs/parity-v2/`; intentional
deviations are logged in `docs/parity/_parity-flags.md`. Active replication work happens on
branch `feat/replicate-old-app`. Ground-truth legacy source: `P:\PatientPortalOld`
(read-only; never edit).

## What never to do

- `ng serve` / `yarn start` / `ng build --watch`; `dotnet run` the AuthServer or HttpApi.Host in dev.
- Edit `angular/src/app/proxy/` (regenerate instead) or anything under `P:\PatientPortalOld`.
- Add an AppService without `[RemoteService(IsEnabled=false)]`; use AutoMapper or `ObjectMapper.Map<>`.
- Put real patient data anywhere (see HIPAA above).
