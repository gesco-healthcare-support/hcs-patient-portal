# CLAUDE.md -- branch `feat/replicate-old-app`

Branch-scoped guidance for replicating the legacy Patient Portal onto
the new ABP stack. Supersedes `main`'s `CLAUDE.md` in this worktree.
Path-scoped `.claude/rules/*.md` win for the file paths they target.

---

## PRIMARY MISSION

Port the **correct, ground-truth behavior** of the legacy single-tenant
Patient Portal at `P:\PatientPortalOld` into this codebase, using the
NEW stack (.NET 10 / ABP Commercial 10.0.2 / Angular 20). Goal:
**logical and visual parity** with the OLD app on a modern stack.

- **Match:** entities, names, labels, schema shape, business rules,
  frontend logic, UI layout, role + permission model, document content,
  colors, fonts, spacing, and every small UI detail.
- **Do NOT match:** framework choice, library set, framework versions,
  AWS hosting, in-house custom packages. Use NEW framework components
  (LeptonX, Angular 20 Material) natively -- but apply OLD colors and
  fonts from `docs/design/_design-tokens.md` so the result is
  visually close to the OLD app without recreating its HTML/CSS structure.
- **Reports -- PDF replaces DOCX:** OLD generated `.docx` reports;
  NEW generates PDF. Reason: PDFs are immutable so recipients cannot
  edit them. All report business logic (data shown, role access,
  filters, column layout) must still match OLD exactly.
- **Out of scope until parity:** multi-tenancy. The OLD app is
  single-tenant; the NEW app's multi-tenant scaffolding stays wired up
  but the target through Phase 1 is one demo tenant = one doctor's
  office. Phase 2 (multi-tenant adaptation) starts only after one
  office has parity.

---

## Cross-reference: the OLD app

- **Canonical, read-only source of truth:** `P:\PatientPortalOld`,
  registered via `.claude/settings.local.json`
  (`{"additionalDirectories": ["P:\\PatientPortalOld"]}`, gitignored).
- **Never edit anything under `P:\PatientPortalOld`.**
- **OLD code layout:** `PatientAppointment.{Api, Domain, Models,
  DbEntities, Infrastructure, UnitOfWork, BoundedContext}` +
  `patientappointment-portal/` (Angular) + `packages/` (in-house NuGet
  -- replace, do not port) + `_local/`.
- **OLD docs:** `P:\PatientPortalOld\Documents_and_Diagrams\` --
  `Architecture/`, `ER Digram/`, `Postman Collection/`, `Swagger/`,
  `Workflow/`, `Readme.txt`. Built 2017-2023 with formal SDE practice;
  more accurate than any doc currently in this codebase.
- **Find first, ask second:** before porting a feature, read the OLD
  code AND its `Documents_and_Diagrams/` entry. Read originals directly
  from `P:\PatientPortalOld\Documents_and_Diagrams\` -- the
  old-extracted copies have been removed from this repo. Ignore AWS
  infrastructure; copy all business logic. OLD DOCX report output ->
  NEW PDF output (see Primary Mission above).

---

## Precedence (highest to lowest)

1. OLD app code at `P:\PatientPortalOld\<project>\`.
2. OLD app docs at `P:\PatientPortalOld\Documents_and_Diagrams\`.
3. This file.
4. `.claude/rules/*.md` -- path-scoped infra rules (Angular, dotnet,
   dotnet-env, hipaa-data, test-data).
5. Adrian's user-level rules at `C:\Users\RajeevG\.claude\`.
6. The new app's existing source -- starting point only, never a target.

---

## Descriptive vs prescriptive

`docs/gap-analysis/`, `docs/implementation-research/`,
`docs/architecture/`, `docs/api/`, `docs/backend/`, `docs/frontend/`,
`docs/database/`, and the layer-level `CLAUDE.md` files under
`src/.../<layer>/` and `angular/src/app/` describe the **current crude
state** of the new app. Use them as a navigation aid to understand what
already exists -- do NOT preserve their logic, naming, or UI choices
when replicating the OLD app. Binding sources are: the OLD app code and
docs at `P:\PatientPortalOld`, this file, `.claude/rules/*.md`, and
Adrian's user-level rules.

---

## Bug and deviation policy

When OLD code contains something that looks wrong, apply this rule:

- **Clear bug -- fix it.** Wrong data that cannot be intentional (e.g.
  hardcoded `UserId=1` in scheduler jobs, Twilio country code `+91`
  for a US app, a null-ref that always throws). Fix silently; the NEW
  app must not inherit broken behavior.
- **Ambiguous -- replicate and flag it.** When it is genuinely unclear
  whether a pattern is a bug or an intentional design choice, replicate
  the OLD behavior verbatim AND mark it for explicit testing:
  1. Add a `// PARITY-FLAG: <description> (OLD source: <file>:<line>)`
     comment on the relevant C# or TypeScript line.
  2. Add a row to `docs/parity/_parity-flags.md` (create if absent)
     with: feature, OLD source citation, description, status `needs-test`.
  3. Adrian manually tests every flagged behavior after implementation
     to determine bug vs. design. Once resolved, remove the flag and
     update the row to `resolved`.

Specific decisions already made (do not re-litigate):

- Template code name typos: see
  `docs/parity/it-admin-notification-templates.md` section
  "Notification template codes" for which are fixed vs. kept.

---

## Unaudited features protocol

Every feature must have a parity audit doc in `docs/parity/` before
implementation starts. The 18 existing audit docs cover the priority
flows. For any feature not yet audited:

1. Read the OLD code (`P:\PatientPortalOld\<project>\`).
2. Read the OLD docs (`P:\PatientPortalOld\Documents_and_Diagrams\`).
3. Write a parity audit doc to `docs/parity/<feature-slug>.md`
   following the structure of existing audit docs (gap table, OLD code
   map, UI field inventory, business rules, role matrix).
4. Present the doc to Adrian for review before writing any code.

Do NOT implement from OLD code alone without a parity doc. The parity
doc is the contract; the implementation satisfies it.

---

## Stack and versions

.NET 10.0 (C# `latest`). ABP Commercial 10.0.2 (Volo -- OpenIddict,
LeptonX, SaaS). Angular ~20.0 (standalone, esbuild, **NO Vite dev
server**). SQL Server (LocalDB / Docker, dev). EF Core code-first.
Riok.Mapperly source-gen mapper (NOT AutoMapper). OpenIddict OAuth.
xUnit + Shouldly with Autofac DI. Ports: AuthServer 44368, API 44327,
Angular 4200.

---

## Critical constraints (binding)

- **Path length on Windows.** > ~200 chars triggers SNI.dll failure
  (260-char native limit). Worktree at
  `C:\src\patient-portal\replicate-old-app\` is fine.
- **Never `ng serve`, `yarn start`, `ng build --watch`.** Vite
  duplicates `CORE_OPTIONS` `InjectionToken` and breaks ABP DI
  (`NullInjectorError`). Use:
  `npx ng build --configuration development` then
  `npx serve -s dist/CaseEvaluation/browser -p 4200`.
- **Service start order:** SQL -> AuthServer -> HttpApi.Host -> Angular.
  Out-of-order cold starts break permission seeding + JWT validation.
- **Never edit `angular/src/app/proxy/`** -- auto-generated; regenerate
  via `abp generate-proxy` after backend DTO/service changes.
- **`appsettings.secrets.json`** holds ABP license + SMTP creds; treat
  as sensitive even when not gitignored.
- Docker compose at worktree root is the supported one-shot path
  (`docker compose up -d --build`).

---

## OLD-to-NEW translation map

NEW layout: `src/Domain.Shared` (enums, localization), `Domain`
(entities, IRepository, domain services), `Application.Contracts`
(DTOs, IAppService, Permissions), `Application` (impls + mappers),
`EntityFrameworkCore` (DbContext, migrations, repo impls), `HttpApi`
(manual `AbpController` -> `IAppService`), `HttpApi.Host`, `AuthServer`,
`DbMigrator`. Frontend: `angular/src/app/<feature>/` (with
auto-generated `proxy/`).

- OLD `DbEntities` + `Domain` -> `src/.../Domain/<Feature>/<Entity>.cs`.
- OLD `Models` -> `src/.../Application.Contracts/<Feature>/*Dto.cs`.
- OLD `Api` controllers -> `src/.../HttpApi/Controllers/<Feature>/`
  PLUS a paired `IAppService` + impl per ABP convention.
- OLD `Infrastructure` repos -> `src/.../EntityFrameworkCore/<Feature>/`.
- OLD `patientappointment-portal/` -> `angular/src/app/<feature>/`.

Two DbContexts: `CaseEvaluationDbContext` (`MultiTenancySides.Both`)
and `CaseEvaluationTenantDbContext`. Host-only configs guarded with
`if (builder.IsHostDatabase())`. Many existing tests encode the current
crude behavior -- expect to rewrite or delete; never preserve a failing
test by altering production code. Synthetic-data conventions:
`.claude/rules/{hipaa-data,test-data}.md`.

---

## ABP conventions (HOW to wire, not WHAT to build)

- **DTO naming:** `{Entity}CreateDto`, `{Entity}UpdateDto`,
  `{Entity}Dto`, `{Entity}WithNavigationPropertiesDto`,
  `Get{Entities}Input`. Never `CreateUpdate{Entity}Dto`.
- **AppService:** extend `CaseEvaluationAppService`. Always add
  `[RemoteService(IsEnabled = false)]` -- otherwise ABP exposes
  duplicate routes alongside the manual controller.
- **Mappers:** Riok.Mapperly only. `partial class` with
  `[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]`
  extending `MapperBase<TSource, TDest>` in
  `CaseEvaluationApplicationMappers.cs`. Never `ObjectMapper.Map<>`,
  never AutoMapper.
- **Permissions:** nested static class in `CaseEvaluationPermissions.cs`
  AND register in `CaseEvaluationPermissionDefinitionProvider.cs`.
  Pattern: `Default` parent, `Create / Edit / Delete` children.
- **Controllers:** every AppService needs a manual controller
  `extends AbpController, I{Entity}AppService`. Route:
  `[Route("api/app/{entity-plural}")]`.
- **Localization:** JSON in
  `src/.../Domain.Shared/Localization/CaseEvaluation/`. `L("Key")` in
  C#, `| abpLocalization` in Angular templates. Keys must exist in
  `en.json` before they are referenced.

---

## What never to do

- Edit `angular/src/app/proxy/` (regenerate instead).
- `ng serve`, `yarn start`, `ng build --watch`.
- Run dotnet from a path > ~200 chars.
- Omit `[RemoteService(IsEnabled = false)]` on a new AppService.
- Use AutoMapper or `ObjectMapper.Map<>` for new mappers.
- Edit anything under `P:\PatientPortalOld`.
- Skip the domain service (e.g. `AppointmentManager`) on create/update
  flows -- business rules belong in domain services, not AppServices.
- Treat any feature CLAUDE.md, `docs/features/*`, `docs/product/*`, or
  ADR as the target for parity work. They describe current state.
- Use real patient data anywhere (see `.claude/rules/hipaa-data.md`,
  `.claude/rules/test-data.md`).
- Add code that depends on cross-tenant queries during Phase 1.

---

## Vocabulary

- **Promotion cascade:** `main -> development -> staging -> production`,
  one direction only. `feat/replicate-old-app` is a feature branch off
  `main`; merges to `main` only when a parity slice is reviewed.
- **The OLD app:** `P:\PatientPortalOld`.
- **The new stack / new app:** this codebase.
- **Parity:** new stack reproduces OLD user-visible + business-rule
  behavior to the maximum allowed by the modern framework set.

---

## Session-start protocol

1. This file auto-loads.
2. Read the prompt + any `@<path>` references the user gave.
3. **Before claiming a feature is "ported":** read the OLD code under
   `P:\PatientPortalOld\<project>\`, read its doc under
   `Documents_and_Diagrams/`, then read what the new stack currently
   has and propose the delta.
4. Make changes in this worktree only. Commit on `feat/replicate-old-app`.
5. New sub-branches check out from `feat/replicate-old-app`, not `main`.
6. Do NOT push `feat/replicate-old-app` to `origin` until Adrian asks.
