# Plan: OLD vs NEW Patient Portal -- Comprehensive Gap Analysis

> **Companion file:** [`00-KICKOFF-PROMPT.md`](./00-KICKOFF-PROMPT.md) -- the self-contained prompt to paste into a new Claude Code session to execute this plan.

## Context

Adrian has two Patient Portal codebases running on his machine:

- **OLD:** `P:\PatientPortalOld` -- legacy .NET Framework 4.8 + ASP.NET Core 2.0 + Angular 7.1.3 (~2018). Running on `http://localhost:4201` (Angular) and `http://localhost:59741` (API). 48 `dbo` tables + 84 `spm` view-tables + 40 stub stored procs + 7 seeded users in LocalDB `PatientPortalOld_Main`. The just-completed bring-up in the prior session left this app fully walkable end-to-end; the Book Appointment flow, every role login, all navigation works.
- **NEW:** `W:\patient-portal\development\` (the active branch of the ABP Commercial 10.0.2 + Angular 20 rewrite). Running on `http://localhost:4200` (Angular), `https://localhost:44327` (HttpApi.Host + Swagger), `https://localhost:44368` (AuthServer / OpenIddict). Partially built -- Foundation is complete, feature coverage is a subset of OLD.

Adrian's scoping problem: he has to finalize MVP for management and then build up NEW until it matches MVP. OLD is the feature reference -- any user-visible capability that exists in OLD and is absent in NEW is a candidate MVP gap. But OLD also contains legacy cruft, tenant-per-database plumbing, stored-procedure-heavy data access, and other decisions that were intentionally replaced in NEW. The gap analysis must distinguish **missing capability** from **intentional architectural difference**, otherwise the MVP scope gets polluted with noise.

**Outcome this plan produces:** 9 per-dimension gap documents + a master README aggregating findings, written to `W:\patient-portal\development\docs\gap-analysis\`. Gap docs only -- no implementation plan, no code changes. Adrian will drive gap-by-gap research in follow-up sessions once he has the inventory in hand.

**Why today:** Adrian needs to hand this to management for MVP sign-off this week; the rest of this week depends on having the inventory in hand tomorrow morning.

## Goal

Produce, in one execution session run against both live instances, a complete inventory of what OLD has that NEW does not, organized into 9 per-dimension documents plus a master index. Every finding cites file paths / URLs / screenshots. Every difference is classified as `MVP-gap | non-MVP-gap | intentional-architectural-difference | extra-in-new`.

## Approach -- 9 parallel tracks

Each track produces one markdown document at `W:\patient-portal\development\docs\gap-analysis\NN-<slug>.md`. Tracks 1-8 run fully in parallel via `Agent` dispatch (subagent_type per track below). Track 9 runs in parallel too but takes longer because it drives a browser; it is split into 7 role-sub-passes within a single subagent rather than parallelized further, to keep cookie state coherent.

| # | Track | Subagent type | Depends on | Estimated wall-time |
|---|---|---|---|---|
| 1 | Database schema + seeds | Explore | -- | 30 min |
| 2 | Domain entities + services | Explore | -- | 40 min |
| 3 | Application services + DTOs | Explore | -- | 40 min |
| 4 | REST API endpoints | general-purpose | Swagger reachable on NEW | 40 min |
| 5 | Auth + authorization + permissions | general-purpose | Both login endpoints reachable | 30 min |
| 6 | Cross-cutting backend (logging, email, SMS, files, jobs) | Explore | -- | 30 min |
| 7 | Angular routes + modules | Explore | -- | 30 min |
| 8 | Angular proxy, services, models | Explore | -- | 30 min |
| 9 | UI screens per role (all 7 roles, all reachable screens) | general-purpose | Chrome DevTools MCP; old.UI running; new.UI running | 90 min |

All 9 can run in one dispatch. The orchestrator (parent Claude) then does a 10th synthesis pass -- reading all 9 docs -- to write the master `README.md`.

### Track 1 -- Database schema + seeds

- **Scope:** schema of `PatientPortalOld_Main` vs NEW's `CaseEvaluation` database: table inventory, column-level comparison (types, nullability, keys, indexes), views, stored procedures, FK graph, seed/lookup rows (Roles, Statuses, Languages, AppointmentTypes, Locations, States, etc.).
- **Method:**
  - OLD: query `sys.tables`, `sys.columns`, `sys.foreign_keys`, `sys.procedures`, `sys.views` on `(localdb)\MSSQLLocalDB` db `PatientPortalOld_Main`. Grep `[Table]`/`[Column]` attributes under `P:\PatientPortalOld\PatientAppointment.DbEntities\Models\`.
  - NEW: use `docs/database/SCHEMA-REFERENCE.md` as the authoritative source; cross-check against `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/` and actual `CaseEvaluation` DB if reachable.
- **Output:** `01-database-schema.md` following the shared template below. Includes an entity-by-entity side-by-side table.

### Track 2 -- Domain entities + services

- **Scope:** DDD entities (properties, relationships, invariants), domain services (`*Domain` classes in OLD, `*Manager` in NEW), enums, state machines, validation attributes.
- **Method:**
  - OLD: read `PatientAppointment.Domain\**\*Domain.cs` (UserAuthenticationDomain, AppointmentDomain, AppointmentDocumentDomain, SchedulerDomain, etc.). Note business rules embedded in domain logic.
  - NEW: read `src/HealthcareSupport.CaseEvaluation.Domain/**` + `docs/backend/DOMAIN-MODEL.md`, `docs/backend/DOMAIN-SERVICES.md`, `docs/business-domain/APPOINTMENT-LIFECYCLE.md`.
- **Focus:** the 13-state appointment lifecycle (confirm NEW's state machine matches OLD), doctor availability management, patient demographics, attorney linkage, accessor access grants.
- **Output:** `02-domain-entities-services.md`.

### Track 3 -- Application services + DTOs

- **Scope:** `IAppService` interfaces, DTOs (`*Dto`, `*CreateDto`, `*UpdateDto`, `Get*Input`), filter classes, search query shapes.
- **Method:**
  - OLD: read `PatientAppointment.Api\Controllers\Api\**\*.cs` for the list of operations; `PatientAppointment.Models\ViewModels\` for DTO shapes; the `*Domain.cs` for the business entry points.
  - NEW: read `src/HealthcareSupport.CaseEvaluation.Application/**` + `docs/backend/APPLICATION-SERVICES.md`.
- **Output:** `03-application-services-dtos.md`. Include a table per entity: operation (Create / GetById / GetList / Update / Delete / Search / Export) x presence in OLD x presence in NEW.

### Track 4 -- REST API endpoints

- **Scope:** full endpoint inventory. URL, HTTP method, auth required, permission required, request shape, response shape, supported query parameters.
- **Method:**
  - NEW: pull Swagger JSON from `https://localhost:44327/swagger/v1/swagger.json` (may need `-k` for self-signed cert). Parse.
  - OLD: recursively grep for `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpDelete]` under `P:\PatientPortalOld\PatientAppointment.Api\Controllers\`. Build the same shape manually.
- **Deliverable:** side-by-side endpoint list grouped by feature area. Flag every OLD endpoint that has no NEW counterpart.
- **Output:** `04-rest-api-endpoints.md`.

### Track 5 -- Auth + authorization + permissions

- **Scope:** login flow, token shape, roles, permission tree, guard logic, multi-tenancy runtime behavior.
- **Method:**
  - OLD: trace `POST /api/userauthentication/login` -> `UserAuthenticationDomain.PostLogin` -> JWT shape -> `/api/userauthorization` -> `UserAuthorization.GetAccessModules` -> `dbo.spPermissions`. Document every role's permission tree (7 roles; currently all get the grant-everything stub, but document what the ORIGINAL design was per OLD code).
  - NEW: OpenIddict flow via `AuthServer:44368`. Pull `.well-known/openid-configuration`, document OAuth scopes, permission classes in `docs/backend/PERMISSIONS.md`, ABP role model, host vs tenant split.
  - Include `docs/architecture/MULTI-TENANCY.md` findings.
- **Deliverable:** two permission matrices (OLD roles x access items; NEW roles x ABP permissions), plus a mapping table.
- **Output:** `05-auth-authorization.md`.

### Track 6 -- Cross-cutting backend

- **Scope:** logging, error handling, file storage, email, SMS, background jobs, caching, validation.
- **Method:**
  - OLD: read `Infrastructure\ExceptionLogs\`, `Infrastructure\AmazonService\`, `Infrastructure\Utilities\SendMail.cs`, `Infrastructure\TwilioSmsService.cs`, `Domain\Core\SchedulerDomain.cs`, `UnitOfWork\AuditLogs\`. Look at `appsettings*.json` / `server-settings.json` for what's configured.
  - NEW: read the equivalent ABP modules in `src/HealthcareSupport.CaseEvaluation.Domain/` and HttpApi.Host. Check the 5 `docs/decisions/*.md` ADRs.
- **Specifically call out:** S3 usage (OLD has it; NEW probably doesn't yet), SMTP via AWS SES (OLD) vs ABP emailing (NEW), Twilio SMS (OLD) vs NEW, scheduler jobs (OLD has 9 notification schedulers in `SchedulerDomain`).
- **Output:** `06-cross-cutting-backend.md`.

### Track 7 -- Angular routes + modules

- **Scope:** URL map, lazy-loaded modules, route guards, route data (`applicationModuleId`, `accessItem`, `rootModuleId`, `anonymous`).
- **Method:**
  - OLD: read `P:\PatientPortalOld\patientappointment-portal\src\app\app-routing.module.ts` plus every `*-routing.module.ts` under `src/app/components/**`.
  - NEW: read `W:\patient-portal\development\angular\src\app\app.routes.ts` plus per-feature route files (Angular 20 standalone components pattern).
- **Deliverable:** URL inventory table. Flag OLD routes that do not exist in NEW.
- **Output:** `07-angular-routes-modules.md`.

### Track 8 -- Angular proxy, services, models

- **Scope:** auto-generated proxy surface in NEW (`angular/src/app/proxy/`) vs handwritten API services in OLD (`src/app/domain/**/*.service.ts`), typed models, shared components, shared directives.
- **Method:**
  - OLD: inventory services under `src/app/domain/`, shared components under `src/app/components/shared/`, and database models under `src/app/database-models/`.
  - NEW: inventory `angular/src/app/proxy/**` (DO NOT EDIT these, per NEW's CLAUDE.md), plus any handwritten services under `angular/src/app/shared/` and feature folders.
- **Output:** `08-angular-proxy-services-models.md`.

### Track 9 -- UI screens per role (full coverage)

- **Scope:** Every reachable screen for every one of the 7 roles, on OLD and on NEW. Captures: URL, page title, list of top-level UI elements (forms, tables, buttons, dashboards), screenshot. Notes behavior differences when the same page exists in both (e.g., field-level differences).
- **Method:**
  - Uses Chrome DevTools MCP with two browser contexts (`http://localhost:4201` for OLD, `http://localhost:4200` for NEW).
  - Executes 7 sub-passes, one per role. Per role: login -> traverse every navigation link -> capture snapshot + screenshot -> logout -> next role.
  - OLD credentials (already seeded in `P:\PatientPortalOld\_local\CHANGELOG.md`):
    - `admin@local.test` / `Admin@123` (ItAdmin)
    - `supervisor@local.test` / `Admin@123` (StaffSupervisor)
    - `staff@local.test` / `Admin@123` (ClinicStaff)
    - `patient@local.test` / `Admin@123` (Patient)
    - `adjuster@local.test` / `Admin@123` (Adjuster)
    - `patatty@local.test` / `Admin@123` (PatientAttorney)
    - `defatty@local.test` / `Admin@123` (DefenseAttorney)
  - NEW credentials: ABP default `admin` / `1q2w3E*`. If NEW does not yet have separate role-scoped users equivalent to OLD's 7, note that as an MVP gap under track 5 (permissions) and capture only the admin-visible screens on NEW side.
  - Screenshots save to `W:\patient-portal\development\docs\gap-analysis\screenshots\<old|new>\<role>\<slug>.png`.
- **Output:** `09-ui-screens.md`. Per-role section listing every screen with side-by-side links. Flag each screen as `exists-in-both | old-only | new-only | behavior-differs`.

### Synthesis pass (after all 9 tracks complete)

The orchestrator reads the 9 track docs and writes `W:\patient-portal\development\docs\gap-analysis\README.md` containing:

- One-page summary: total gap count, breakdown by category, estimated MVP-critical gap count.
- Aggregated gap table (every row: `gap-id | feature | category | severity | evidence-links | status`).
- Open-questions list (every "Adrian, please clarify X" from every track doc, consolidated).
- Links to the 9 track docs.
- Cheat sheet of reproducible commands (sqlcmd, curl, etc.) used in the analysis.

## Shared template for each track doc

Every track doc follows this exact structure. The new session enforces it.

````markdown
# <NN> -- <Track Name>: Gap Analysis OLD vs NEW

## Summary
<2-3 sentences. Headline gap count. MVP risk rating: High | Medium | Low.>

## Method
<Which commands / files / URLs were queried. Reproducibility note. Timestamp.>

## OLD version state
<Bullets or tables with file:line citations for every claim.>

## NEW version state
<Same. When a claim comes from `docs/<path>.md`, cite that doc as the source; also verify against current source when doc is >30 days old.>

## Delta

### MVP-blocking gaps (capability present in OLD, absent in NEW)
<Table: gap-id | capability | evidence-old | evidence-new-absent | rough-effort-to-close>

### Non-MVP gaps (nice-to-have)
<Same format>

### Intentional architectural differences (NOT gaps)
<Table: topic | OLD approach | NEW approach | why NEW is different>

### Extras in NEW (not in OLD)
<Capabilities NEW has that OLD lacks.>

## Open questions
<List: "Adrian, please clarify X" -- feed into next-phase research.>
````

## Seeded intentional architectural differences -- do NOT flag these as gaps

These are architecture choices Adrian and the prior Gesco team made deliberately when rewriting. The new session loads these as "known intentional differences" and mentions them under the `Intentional architectural differences` section of whichever tracks they touch.

| Topic | OLD approach | NEW approach | Why different |
|---|---|---|---|
| Multi-tenancy | Tenant-per-database; `CompanyName` and `DbServer` encoded in JWT claims; `BaseDbContext.GetConnection` routes per-tenant | ABP row-level `IMultiTenant` with auto-filter on 7 entities; Location/State/WcabOffice/AppointmentType/Status/Language are host-scoped | ABP Commercial's standard model; simpler ops, less DB sprawl. Per `docs/architecture/MULTI-TENANCY.md` and `docs/decisions/004-doctor-per-tenant-model.md`. |
| Auth server | Custom JWT via `Rx.Core.Security.dll`; no refresh tokens visible in code; roles baked into `User.RoleId` enum | OpenIddict on dedicated port 44368, OAuth 2.0 / OIDC, ABP Identity Module with role records | Industry-standard OIDC, centralized auth, discoverable endpoints. |
| Permissions | Stored proc `dbo.spPermissions` returns hand-crafted JSON tree; frontend builds `userPermission[moduleId][accessItem]` | ABP `PermissionDefinitionProvider` + policy-based authorization; `[Authorize(CaseEvaluationPermissions.Foo.Create)]` | Declarative, code-first, analyzable without DB calls. Per `docs/backend/PERMISSIONS.md`. |
| Data access | Heavy stored procedures (40+ listed in `stub-procs.sql`) for listing/notification/permission logic; DbContext subcontexts share connections | ABP `IRepository<T>` + LINQ; EF Core migrations; Riok.Mapperly source-gen for DTO mapping (NOT AutoMapper) | Testable, refactorable, schema versioned. Per `docs/decisions/001-mapperly-over-automapper.md`. |
| Frontend build | Angular 7 with `ng serve` dev server and `@rx/*` in-tree monorepo | Angular 20 standalone components; auto-generated ABP proxy; build + `npx serve` only (never `ng serve` per NEW's CLAUDE.md, due to a known Vite/Angular 20 + ABP core-options issue) | Modern Angular tooling; codegen reduces hand-maintained client code. Per `docs/decisions/005-no-ng-serve-vite-workaround.md`. |
| Controller wiring | Manual MVC controllers in `Controllers/Api/**/*Controller.cs` | Manual controllers too, BUT every AppService has `[RemoteService(IsEnabled = false)]` and controller delegates to it | Per `docs/decisions/002-manual-controllers-not-auto.md` -- explicit routing is easier to review. |
| Mapping | Manual property copying in domain services | Riok.Mapperly `[Mapper]` partial classes, source-generated at compile time | Compile-time errors instead of runtime reflection. Per decision ADR 001. |

The new session surfaces these in each relevant track doc but does NOT count them as gaps in the tally.

## State handoff (the new session needs all of this to work)

**OLD version**

- Root path: `P:\PatientPortalOld`
- Angular: `http://localhost:4201` (running; `P:\PatientPortalOld\_local\ng-serve.log`)
- API: `http://localhost:59741` (running; `P:\PatientPortalOld\_local\api-run.log`; Kestrel on .NET 4.8)
- Database: `(localdb)\MSSQLLocalDB` -- `PatientPortalOld_Main`, `PatientPortalOld_Log`, `PatientPortalOld_Cache`
- Bring-up notes: `P:\PatientPortalOld\_local\CHANGELOG.md` (permission stubs, schema bootstrap caveats, limitations list)
- 7 seeded users, all with password `Admin@123`; emails `{admin|supervisor|staff|patient|adjuster|patatty|defatty}@local.test`

**NEW version**

- Root path: `W:\patient-portal\development`
- Angular: `http://localhost:4200` (running)
- HttpApi.Host: `https://localhost:44327` (running; self-signed cert, use `curl -k`)
- AuthServer: `https://localhost:44368` (running)
- CLAUDE.md: `W:\patient-portal\development\CLAUDE.md` (read first)
- Feature CLAUDE.md index: embedded in the root CLAUDE.md
- Documentation: `W:\patient-portal\development\docs\INDEX.md` hub
- ABP default admin: `admin` / `1q2w3E*` (if this fails, ask Adrian)

**Constraints**

- Plan mode vs auto mode: the new session runs in auto mode. The user does NOT want implementation, just docs. Honor that.
- Do NOT edit `W:\patient-portal\development\angular\src\app\proxy\**` -- auto-generated.
- Do NOT run `ng serve` against NEW -- known CORE_OPTIONS crash per NEW's CLAUDE.md.
- Do NOT modify OLD source either -- this is read-only analysis.

## Verification

Before the new session signals `done`, this must hold:

1. `W:\patient-portal\development\docs\gap-analysis\` contains:
   - `README.md`
   - `01-database-schema.md` through `09-ui-screens.md` (9 files; note: track 10 was dropped per Adrian 2026-04-23)
   - `screenshots/` subfolder with at least one screenshot per role per side (14 minimum) and ideally full coverage
   - no stub files -- every track doc is >= 300 lines when the gap is real, or explicitly says "no significant gaps" with evidence
2. Every file in the folder passes the "template lint" (sections present: Summary, Method, OLD state, NEW state, Delta with 4 subsections, Open questions).
3. `README.md` contains an aggregated gap table, a total MVP-gap count, and at least one open question per track.
4. At least one screenshot from each of the 7 OLD roles is saved under `screenshots/old/<role>/`.
5. Adrian can cold-read any track doc and reproduce every claim from the cited file paths or commands.

If any of these fails, the new session notes what's missing in a `STATUS.md` at the same folder and stops cleanly.
