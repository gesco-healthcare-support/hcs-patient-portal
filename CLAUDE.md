# CLAUDE.md

Repository guidance for `hcs-patient-portal` on `main`. Path-scoped `.claude/rules/*.md`
win for the file paths they target.

This file describes the app as it is. It is not a plan and not a target state.

---

## What this app is

The HCS Case Evaluation Portal: workers' compensation Independent Medical Examination
(IME) scheduling for Gesco. Healthcare support staff book patients with IME doctors at
specific locations and time slots, and track each appointment through its lifecycle.

**Multi-tenant, database per office.** Each doctor practice is one tenant with its own
database; the host organisation owns the shared management database. Multi-tenancy is
LIVE, not scaffolding.

**The repository is PUBLIC**, deliberately and permanently. Nothing committed here may
contain PHI, secrets, credentials, internal IP addresses or real patient data. Security
findings go to a draft repository security advisory, never to a GitHub Issue. See
`SECURITY.md`.

---

## Stack and versions

.NET 10.0 (C# `latest`). ABP Commercial 10.0.2 (Volo: OpenIddict, LeptonX, SaaS).
Angular ~20.0 (standalone components, esbuild application builder, **no Vite dev server**).
SQL Server (LocalDB or Docker in dev). EF Core code-first. Riok.Mapperly source-generated
mapping via `Volo.Abp.Mapperly` (NOT AutoMapper). OpenIddict OAuth 2.0 / OIDC.
xUnit + Shouldly with Autofac DI. Yarn 4.16.0 (`packageManager` + `yarnPath`; NOT Yarn 1).

Ports: AuthServer 44368, API 44327, Angular 4200.

---

## Layout

`src/` holds ten .NET projects in DDD layers:

- `Domain.Shared` -- enums, constants, localization, `TenantNaming`
- `Domain` -- entities, `IRepository` interfaces, domain services (`*Manager`), jobs
- `Application.Contracts` -- DTOs, `IAppService` interfaces, `Permissions`
- `Application` -- app service implementations, Mapperly mappers
- `EntityFrameworkCore` -- two DbContexts, two migration sets, repository implementations
- `HttpApi` -- hand-written controllers, the tenant resolve contributor
- `HttpApi.Client`, `HttpApi.Host` (:44327), `AuthServer` (:44368), `DbMigrator`

`angular/src/app/<feature>/` for the SPA, with an auto-generated `proxy/`.
`test/` holds four xUnit projects.

**Two DbContexts**: `CaseEvaluationDbContext` (`MultiTenancySides.Both`) and
`CaseEvaluationTenantDbContext`. Host-only configuration is guarded with
`if (builder.IsHostDatabase())`. **Two migration sets**: `Migrations/` and
`TenantMigrations/`, each with its own model snapshot. A change to an entity mapped in
both needs a migration in BOTH sets.

---

## Critical constraints (binding)

- **Path length on Windows.** Over roughly 200 characters triggers an `SNI.dll` load
  failure (260-char native limit). Keep worktrees under `C:\src\patient-portal\`.
- **Never `ng serve`, `yarn start`, or `ng build --watch`.** Angular 20's Vite
  pre-bundler splits `@abp/ng.core` across chunks and creates duplicate
  `InjectionToken` instances; ABP DI uses reference identity, so `CORE_OPTIONS`
  injection fails at runtime with `NullInjectorError`. Use
  `npx ng build --configuration development` then
  `npx serve -s dist/CaseEvaluation/browser -p 4200`. Note `angular.json` still defines
  a working `serve` target, which fails with that cryptic error rather than refusing.
  Context: `docs/decisions/005-no-ng-serve-vite-workaround.md`.
- **Service start order: SQL, then AuthServer, then HttpApi.Host, then Angular.**
  Out-of-order cold starts break permission seeding and JWT validation.
- **Never edit `angular/src/app/proxy/`.** It is generated; regenerate with
  `abp generate-proxy` after a backend DTO or service change.
- **`appsettings.secrets.json`** holds the ABP licence and SMTP credentials. Treat as
  sensitive even where it is not gitignored.
- **Every Compose command on the deployed server needs `--env-file secrets/env.prod`.**
  There is no `.env` there, so Compose loads nothing, every secret resolves to a blank
  string, and it warns and carries on. Use `scripts/hosting/dc.sh`, which refuses to run
  without it.
- **Force-recreate the reverse proxy after any backend rebuild.** nginx resolves upstream
  container names once at worker start and caches the addresses.

---

## Multi-tenancy: how a request becomes an office

- An office's subdomain slug IS its lowercased name.
  `TenantNaming.DeriveSlug` VALIDATES rather than transforms, because the resolver matches
  the slug back to the tenant's stored `Name`; a transformed slug would stop resolving.
- Database name is `CaseEvaluation_{slug}`; the host database is `CaseEvaluation`. The
  office connection string is derived from the single secret-managed `Default` by swapping
  the catalog, so SQL credentials are never duplicated into a second config key.
- **Two reserved slug sets, for different reasons.** `admin` maps to the Volo SaaS Host
  surface. `TenantNaming.ProxyReservedSlugs` = `{api, auth, minio, health, www}` are the
  single-label hosts the reverse proxy answers itself with an exact `server_name`, which
  nginx ranks above every wildcard.
- **COUPLING NOTHING ENFORCES**: adding an exact `server_name` block to
  `docker/nginx-proxy/default.conf.template` requires adding that slug to
  `ProxyReservedSlugs`. Miss it and an office by that name is accepted everywhere and then
  has no reachable front door, with no error explaining why.
- `HostAwareDomainTenantResolveContributor` resolves the office from the Host header
  against `App:TenantDomainFormat` (default `{0}.localhost`). A host naming no office is
  REFUSED, except `localhost` and `authserver`, which internal health checks use. It
  governs anonymous requests only; `CurrentUserTenantResolveContributor` runs first for
  authenticated ones.
- **The resolver chain is a security control.** `ConfigureMultiTenancy` calls
  `TenantResolvers.Clear()` and registers only those two, removing ABP's QueryString,
  Cookie, Header and Route contributors so a caller cannot select an office with a
  `__tenant` value. One `internal static` method serves both the AuthServer and the API.
  Its test needs a seeded decoy resolver or it passes with the `Clear()` deleted.

---

## ABP conventions (how to wire, not what to build)

- **DTO naming**: `{Entity}CreateDto`, `{Entity}UpdateDto`, `{Entity}Dto`,
  `{Entity}WithNavigationPropertiesDto`, `Get{Entities}Input`. Never
  `CreateUpdate{Entity}Dto`.
- **AppService**: extend `CaseEvaluationAppService` and add
  `[RemoteService(IsEnabled = false)]`, or ABP publishes a duplicate auto-generated route
  beside the hand-written controller.
- **Mapping**: Riok.Mapperly only. A `partial class` with `[Mapper]` extending
  `MapperBase<TSource, TDest>`, declared in `CaseEvaluationApplicationMappers*.cs`.
  **AutoMapper is banned.** `ObjectMapper.Map<>()` is NOT banned: `Volo.Abp.Mapperly`
  registers each `MapperBase<T,U>` as ABP's `IObjectMapper<T,U>`, so `ObjectMapper.Map<>`
  is the intended call path to the generated code and is used throughout the application
  layer. Post-mapping logic uses the `AfterMap` override. See
  `docs/decisions/001-mapperly-over-automapper.md`.
- **Permissions**: a nested static class in `CaseEvaluationPermissions.cs` AND a
  registration in `CaseEvaluationPermissionDefinitionProvider.cs`. Pattern is a `Default`
  parent with `Create` / `Edit` / `Delete` children.
- **Controllers**: every AppService gets a hand-written controller extending
  `AbpController` and implementing `I{Entity}AppService`, routed
  `[Route("api/app/{entity-plural}")]`.
- **Business rules belong in domain services** (`*Manager`), not in AppServices. Note the
  codebase does not hold this uniformly: several booking rules live in
  `Application/Appointments/*Validator` and `*Policy` classes as a deliberate port of the
  legacy layout, and `CustomField` has no Manager at all.
- **Per-appointment authorisation is NOT the permission.** Holding a permission is not
  holding access to a specific appointment. Compose
  `Application/Appointments/AppointmentReadAccessGuard` over
  `Domain/Appointments/AppointmentAccessRules` (seven pathways, first match wins). Any new
  appointment-scoped service must call the guard, not just declare `[Authorize]`.
- **Localization**: JSON in `Domain.Shared/Localization/CaseEvaluation/`. `L("Key")` in C#,
  `| abpLocalization` in Angular. Keys must exist in `en.json` before use. Only `en.json`
  is maintained (roughly 700 keys); the other 19 locale files are far behind it, most of
  them still ABP template stubs, and some carry leftover template keys `en.json` lacks.

---

## Deliberate oddities: do not "fix" these without asking

- **Three appointment states are DEAD**: `CheckedIn` (9), `CheckedOut` (10), `Billed`
  (11). The `AppointmentManager` transitions exist but nothing triggers them, so no
  appointment can reach them. Their email templates, status pills and dashboard counters
  are all present and never fire, and `DashboardAppService` hardcodes
  `BilledThisMonth = 0`. Retained for data compatibility pending a product decision.
  Tracked as PF-005. `NoShow` and `NotSeen` by contrast are LIVE but inbound-only from
  the Case Tracker.
- **`Email` is deliberately excluded from `AttorneySnapshot`.** Nine sibling fields are
  copied from the attorney master onto the appointment and `Email` is not, which reads
  like an oversight. It is load-bearing: `AppointmentAccessRules.IsAppointmentEmailRoleVisible`
  grants access by matching the caller's email against the appointment's party-email
  columns, so copying a caller-editable master email onto an appointment would let a
  caller grant themselves access. Do not add it.
- **`angular/src/app/doctors/` is dormant.** The office IS the doctor; nothing operational
  reads a `Doctor` row. Its route still resolves by direct URL.
- **Plans, handoffs, research notes and drafts are never committed.** `docs/plans/` is
  gitignored. A working artefact belongs there or in the vault, never in the repo.

---

## What never to do

- Edit `angular/src/app/proxy/` (regenerate instead).
- Run `ng serve`, `yarn start`, or `ng build --watch`.
- Run `dotnet` from a path longer than about 200 characters.
- Omit `[RemoteService(IsEnabled = false)]` on a new AppService.
- Use AutoMapper.
- Add an appointment-scoped write path without an ownership check.
- Commit PHI, secrets, credentials, internal IPs, or real patient data.
- File a security finding as a GitHub Issue on this public repository.
- Use `--no-verify` to get past a hook, or work around gitleaks.
- Wrap `git commit` in `timeout`: the pre-commit C# format step can exceed 85 seconds.

---

## The legacy app

The previous single-tenant Patient Portal is **archived and is no longer a reference**. It
was at `P:\PatientPortalOld`; that drive was emptied on 2026-08-21 and the contents moved
to `D:\Archives\P-drive-2026-08-21\` on `hcs-vc-server`. Do not write guidance that
depends on reading it.

Where legacy behaviour still matters it is recorded in `docs/parity/`, `docs/parity-v2/`
and in code comments citing OLD file and line. Ambiguous legacy behaviour that was
replicated rather than corrected carries a `// PARITY-FLAG:` comment and a row in
`docs/parity/_parity-flags.md`.

---

## Vocabulary

- **Office** = tenant = one doctor practice, with its own database.
- **Promotion cascade**: `main -> development -> staging -> production`, one direction.
  Features branch from `main` and squash-merge back to `main`.
- **Host context**: no current tenant. Reached on purpose only, via the `admin` slug or an
  internal host name.

---

## Where the documentation is

`docs/INDEX.md` is the map. `README.md` is the landing page. Per-feature `CLAUDE.md` files
sit beside the code they describe.

**Treat any document's counts and dates with suspicion until checked.** As of 2026-09-27 a
documentation reconciliation is in progress: several pages and code comments still describe
a tenancy model replaced on 2026-05-05, and the `Last verified` markers were applied once
in June and not maintained. Prefer the code, and prefer a dated code comment over a
document.
