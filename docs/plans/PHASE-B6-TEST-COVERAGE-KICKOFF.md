# Phase B-6 — Test Coverage Expansion — Kickoff

**Status:** Ready to start
**Written:** 2026-04-20 (end of Phase-B closure, `main` at SHA recorded in Appendix A)
**Target:** SonarCloud overall coverage `>=60%` (from baseline `~6%`), new-code coverage `>=80%`, Quality Gate green on new code
**Reader:** the next Claude Code session that will execute Phase B-6

**This is a kickoff, not an execution plan.** It front-loads everything a fresh session needs to understand what the project is, where B-6 sits in the larger plan, what infrastructure already exists, and what to read *outside* this repo before writing the first test. The next session MUST still run its own research-plan-execute cycle (details in Section 0).

> Treat this document as a map, not a checklist. The entries below are clickable starting points — do not treat "I read this doc" as a substitute for reading the code it points to.

---

## Table of contents

0. [Process mandate — how the next session should run](#0-process-mandate-non-negotiable)
1. [Project context — what Gesco Patient Portal is](#1-project-context--what-gesco-patient-portal-is)
2. [Where we are in the larger plan — Layer 1/2/3/4 + Phase A/B/C](#2-where-we-are-in-the-larger-plan)
3. [Why test coverage now, and why not E2E + smoke](#3-why-test-coverage-now-and-why-not-e2e--smoke)
4. [Test infrastructure — what exists today](#4-test-infrastructure--what-exists-today)
5. [DataSeedContributor pattern — the reference to copy](#5-dataseedcontributor-pattern--the-reference-to-copy)
6. [Doctor reference tests dissected](#6-doctor-reference-tests-dissected)
7. [Multi-tenancy minefield](#7-multi-tenancy-minefield)
8. [HIPAA synthetic-data rules (non-negotiable)](#8-hipaa-synthetic-data-rules-non-negotiable)
9. [Coverage measurement pipeline](#9-coverage-measurement-pipeline)
10. [SQLite-vs-SQL-Server pitfalls](#10-sqlite-vs-sql-server-pitfalls)
11. [Entities still uncovered — priority matrix](#11-entities-still-uncovered--priority-matrix)
12. [Tier breakdown and per-PR deliverables](#12-tier-breakdown-and-per-pr-deliverables)
13. [Entity-specific gotchas to encode as tests or test-gap comments](#13-entity-specific-gotchas)
14. [CI / CD / pre-commit inventory (complete)](#14-ci-cd-pre-commit-inventory-complete)
15. [Code navigation starting points — how to understand a feature](#15-code-navigation-starting-points)
16. [External references — what to read outside this repo](#16-external-references)
17. [Appendix A — Phase-B criteria met at kickoff](#appendix-a-phase-b-criteria-met-at-kickoff)
18. [Appendix B — Phase-C backlog (NOT B-6 scope)](#appendix-b-phase-c-backlog)
19. [Appendix C — Do-not-do list](#appendix-c-do-not-do-list)

---

## 0. Process mandate (NON-NEGOTIABLE)

Phase B-6 is test coverage for 13 entities with complex FK dependencies, multi-tenant variants, domain services with business rules, and a 13-state appointment lifecycle that has no enforced transitions. One-shotting this is a recipe for a broken `main`.

The next session **MUST** follow this order:

1. **External research BEFORE code research.** Read Section 16 (External references) and genuinely study the cited official docs, expert articles, and similar open-source repos (>=500 stars preferred). Do not skip to the codebase.
2. **Then `/load-context` and `/feature-research`** (skills from `~/.claude/rules/rpe-workflow.md`). Produce a structured research summary covering Sections 4-13 in your own words, citing what you learned from Section 16 that either confirmed or contradicted what this doc claims. Flag every contradiction — this kickoff was written on 2026-04-20 against `main` at the SHA in Appendix A; some facts will age.
3. **Pause for Adrian's questions.** Do NOT skip to `/feature-design`. The research summary is a gate, not a speed bump.
4. **One tier per plan file.** `/feature-design` writes `docs/plans/YYYY-MM-DD-phase-b6-tier1.md`. Do not write the Tier 2 plan until Tier 1 is merged AND coverage delta is measured.
5. **One entity per PR inside each tier.** Tier 1 is Appointments + Patients + DoctorAvailabilities + Locations + ApplicantAttorneys — that is at least 5 PRs, not one. Each lands, CI confirms coverage delta, then next starts.
6. **TDD is NOT required for every test.** For *new test infrastructure* (seed contributors, abstract base classes) use `approach: code`. For *behavioural* tests that encode business rules (state transitions, slot release, booking conflict) use `approach: tdd` — write the failing test that encodes the spec, then verify or fix the production code.
7. **Every commit must build 0/0 with `-warnaserror`** and pass `dotnet test` for all discovered test classes. Regression in existing 13 tests = revert.

---

## 1. Project context — what Gesco Patient Portal is

### 1.1 The wider Gesco product pipeline

Gesco operates a four-product medical-evaluation workflow for California workers' compensation:

1. **Digital Forms** (PHP/Laravel — source not yet available). Public intake forms for injured workers, attorneys, and employers to initiate a case.
2. **Patient Portal (THIS REPO).** IME (Independent Medical Examination) scheduling. Staff book patients with QME/AME physicians at specific locations and times; tracks the appointment through a 13-state lifecycle; links applicant attorneys, employers, and accessors.
3. **Case Tracking** (Angular 17 + Spring Boot 2.5.3 + MySQL 8 — separate repo `P:/Case Tracking Portal/Case_Tracking_Source`). Post-appointment case progression.
4. **MRR AI** (Flask + OpenAI + Gemini + OCR — separate repo `P:/MRR_AI_Source/mrr-line_source`). Medical record review AI that ingests records and produces structured case summaries.

Integration today is **manual** — no API bridges between products. Patient Portal is self-contained; its outputs reach downstream products via manual data transfer. Do NOT assume any cross-product contracts exist.

**Source:** the 4-product pipeline is documented in Adrian's global `C:/Users/RajeevG/.claude/CLAUDE.md` (outside this repo). It is not in `docs/` here. If the next session needs the pipeline context, read the global CLAUDE or ask Adrian.

### 1.2 Patient Portal in one paragraph

Workers' compensation Independent Medical Examination scheduling platform for California WCAB proceedings. Healthcare staff book patients with certified medical evaluators (QME/AME doctors) at specific locations and time slots; applicant attorneys can book on behalf of their clients; tracks appointments through a 13-state lifecycle (Pending -> Approved -> CheckedIn -> Billed, plus reschedule/cancel/rejected paths). Multi-tenant SaaS: master data (Locations, States, AppointmentTypes, AppointmentStatuses, AppointmentLanguages, WcabOffices) is host-scoped; transactional data (Appointments, DoctorAvailabilities, ApplicantAttorneys) is tenant-scoped, with one tenant per doctor practice. Patient is a special case: it has a `TenantId` property but does NOT implement `IMultiTenant` — this anomaly is the single highest HIPAA risk in the codebase and must be tested explicitly.

### 1.3 Tech-stack summary

| Layer | Technology | Versions |
|---|---|---|
| Frontend | Angular (standalone components, esbuild — **NOT Vite**) | `~20.3.19` |
| Frontend tests | Karma + Jasmine; Protractor config present but not CI-wired | Karma `~6.4.x`, Jasmine `~3.6.0` |
| Backend | .NET + ABP Commercial (Volo) + OpenIddict + LeptonX theme | .NET `10.0`, ABP `10.0.2` |
| Mapper | Riok.Mapperly source generator (NOT AutoMapper) | Latest |
| ORM | EF Core with ABP repository abstractions | .NET 10 bundled |
| Auth | OpenIddict OAuth2 / OIDC; JWT bearer | Per ABP 10.0.2 bundle |
| Database (dev) | SQL Server LocalDB (`MSSQLLocalDB`) | 2022 |
| Database (tests) | SQLite in-memory via `AbpEntityFrameworkCoreSqliteModule` | Embedded |
| Tests | xUnit + Shouldly + Autofac DI container | Latest |
| CI/CD | GitHub Actions (17 workflows), SonarCloud, Scorecard, TruffleHog, CodeQL, Dependabot | See Section 14 |
| Infra | Docker Compose for local stack, Helm charts for k8s (deferred) | `etc/helm/caseevaluation/` |

### 1.4 Ports and services

| Service | Port | Role |
|---|---|---|
| AuthServer | 44368 | OpenIddict OAuth2 IdP |
| HttpApi.Host | 44327 | REST API + Swagger |
| Angular dev | 4200 | SPA (via `npx serve -s dist/CaseEvaluation/browser -p 4200`) |

Startup order is non-negotiable: **SQL Server -> AuthServer -> HttpApi.Host -> Angular**.  See the root [CLAUDE.md](../../CLAUDE.md) "Critical Constraints" section for why.

---

## 2. Where we are in the larger plan

This repo's CI/CD hardening work follows a 4-layer model derived from **Fowler's Deployment Pipeline (Pattern 3: same static checks everywhere, environment-specific stages downstream)**. Source: Adrian's notes in global memory `project_cicd_pipeline_model.md` — paraphrased here because that file lives outside the repo.

### 2.1 The four layers

| Layer | Scope | Status today (2026-04-20) |
|---|---|---|
| **Layer 1 — Local Git hooks** | Pre-filter on the developer's machine: formatting, secrets, commit-message format, quick build. Identical checks as Layer 2 but fast and offline. | **Active.** See [angular/.husky/](../../angular/.husky) — `pre-commit`, `commit-msg`, `pre-push` hooks. Also Section 14. |
| **Layer 2 — PR-stage CI** | Full static checks on every PR against every branch: build, test, lint, format, dependency review, docs structure. If it's about code correctness (not environment), it runs here. | **Mostly active.** See the 17 workflows in Section 14. Phase B-6 is the final missing piece (60% coverage). |
| **Layer 3 — Post-merge env-specific stages** | After code lands on `development` or `staging`, deploy and validate: smoke tests, E2E, integration with external services. Answers "does it work deployed?". | **Deferred.** Current `deploy-dev.yml` and `promote-staging.yml` are placeholders that re-run build+test. Real deploys + E2E are blocked on Docker/k8s infra being stood up. |
| **Layer 4 — Production gates** | Manual promotion staging -> production with approvals, deploy, health checks, observability. | **Deferred.** No automation; Adrian will gate manually initially. |

**Pattern 3 anti-pattern to avoid:** graduated gates like "main needs 2 checks, staging needs 7" are security theatre. Every static check is required at the earliest gate (Layer 2). Environment-specific variance lives only in Layer 3.

### 2.2 Phase A / B / C within Layer 2

Layer 2 itself has three sub-phases:

| Phase | Purpose | Status |
|---|---|---|
| **Phase A** | Baseline inventory. Get every check discovered, wired, and running (possibly as informational, not blocking). | **Merged** via PR #60 and the "Layer 2 Phase A baseline inventory" work in #64. |
| **Phase B** | Code cleanup + check enablement. Make `main` clean against every check (0 warnings, 0 vulnerabilities, Quality Gate green on new code, CodeQL=0, Dependabot=0, Scorecard running). Ship 8 sub-items (B-1 through B-6). | **7 of 8 sub-items closed today**; B-6 (this doc) is the residual. See Section 2.3. |
| **Phase C** | Hardening. Flip every informational check to blocking. Tighten branch protection. Close deferred alerts (Scorecard supply-chain, DoctorAvailabilities cognitive-41 refactor, Angular animations API migration). | **Backlog.** Starts AFTER B-6. See Appendix B. |

### 2.3 Phase B sub-items

From [docs/verification/PHASE-B-CONTINUATION.md](../verification/PHASE-B-CONTINUATION.md) and the Phase-B closure plan:

| ID | Scope | Status |
|---|---|---|
| B-1 | Dependabot security alerts cleanup | DONE — 0 alerts |
| B-2 | Build config (Scriban 7.1.0, Mapperly strategy, `Directory.Build.props` consolidation) | DONE — PR #76 |
| B-2.1 | Nullable enablement + warning cleanup (`Nullable=enable`, eliminated 480 warnings + 64 RMG012) | DONE — PR #80 |
| B-3 | CodeQL + SonarCloud config (dotnet clean, X-Frame-Options DENY on 5 web.config locations) | DONE — PR #79 |
| B-3.1 | Scanner-level ABP-false-positive suppressions (S6967 + S6853) | DONE — PR #82 |
| B-4 | Scorecard + TruffleHog pins; paths-filter root; karma lcov reporter | DONE — PR #81 |
| B-5a | TypeScript SonarCloud findings (36 -> 0) | DONE — PR #83 |
| B-5b | Helm + GitHub Actions vulnerabilities (10 -> 0) | DONE — PR #84 |
| B-5c-1 | C# mechanical: `== default` -> `== Guid.Empty` (34 findings) | DONE — PR #85 |
| B-5c-2 | C# mechanical: 35 S125/S108/S1118/CA1822/S2325/CA1860/S1481/S6562 | DONE — PR #86 |
| B-5c-3a | C# mechanical: 41 findings (S6966 async, S4136 adjacency, CA2263 Enum.GetValues, logger templates) | DONE — PR #89 |
| B-5c-3b | C# complexity + defer + extended ABP baseline suppressions | DONE — PR #90 |
| B-5d | 1 BUG + ~30 cross-language findings (Web, Shell, Python, CSS, JS, Docker, K8s, TS migration) | DONE — PR #91 |
| B-5f | ABP baseline suppressions (S107 + S1192 scoped to ABP DI patterns) | DONE — PR #87 |
| **B-6** | **Test coverage 6% -> 60%, this phase** | **NOT STARTED** |

### 2.4 Phase-B exit snapshot (main at Phase-B closure SHA)

- Build: 0 warnings, 0 errors with `-warnaserror`.
- SonarCloud: **40 issues (0 BUG, 0 VULNERABILITY, 40 CODE_SMELL)** — criterion "<50" met.
- CodeQL alerts: 0.
- Dependabot alerts: 0.
- Scorecard: runs successfully (100 alerts deferred to Phase C per [SEC-06](../issues/research/SEC-06.md)).
- Security hotspots (SonarCloud UI): 61 total, awaiting Adrian's manual disposition (see [PHASE-B-CONTINUATION.md §4](../verification/PHASE-B-CONTINUATION.md)).
- 13 tests pass (only Doctors + Books covered); **~6% overall coverage = Phase-B-6 starting point**.

### 2.5 What Phase C will deliver (so we don't pull it into B-6)

Items explicitly NOT in Phase B-6:

- Flip SonarCloud QG from informational-only to **blocking** on PR merge.
- Branch protection tightening: decide on the CodeReviewID policy (Copilot/CodeRabbit as first-party reviewer, or document the solo-dev acceptance).
- Close the 100 Scorecard alerts: SHA-pin all GitHub Actions + Dockerfile base images, add top-level `permissions:` blocks where missing, enable OSS-Fuzz or document its deferral, sign up for OpenSSF Best Practices badge. Tracked in [docs/issues/research/SEC-06.md](../issues/research/SEC-06.md).
- Refactor `DoctorAvailabilitiesAppService.GeneratePreviewAsync` (cognitive 41 -> <=15). Unblocked by Tier 1 of B-6 (see Section 12.5). Tracked in [docs/issues/research/P-11.md](../issues/research/P-11.md).
- Full Angular animations API migration beyond the 1-line `provideAnimations` swap in B-5d.
- Tighten `bootstrapModalDialogRole` suppression from `**/*.component.html` to the 2 specific modal files.

---

## 3. Why test coverage now, and why not E2E + smoke

### 3.1 Why B-6 is the last Phase-B item

Phase-B criterion 7 is test coverage >=60%. It was left for last because:
- It's **orthogonal to code-quality cleanup** (B-1 through B-5). Coverage measurement requires clean builds and no blocking warnings — those come first.
- It's a **multi-session, multi-PR investment**, not a single-PR fix. Saving it for a dedicated phase lets it get proper attention.
- The final SonarCloud Quality Gate flip (Phase C) depends on coverage being at >=60%. QG on new code requires a coverage threshold, and we can't flip the gate without tests behind it.

### 3.2 Why unit + integration tests specifically, not E2E

Phase B-6 lives in **Layer 2**. Layer 2 measures static code-correctness properties: does the code build, do unit tests pass, is line-coverage adequate. E2E tests are **Layer 3** — they measure "does the system behave correctly when deployed."

The explicit decision from Adrian's 2026-04-13 planning notes: "focus on getting the commit stage right before worrying about environment-specific stages. No point configuring environment checks for environments that don't exist yet. Once all code passes Layer 1+2, move to Layer 3/4 gates."

Concretely:
- **Unit tests** (Domain.Tests): test entity invariants, domain services, business rules in isolation — no DB, no HTTP, no UI.
- **Integration tests** (Application.Tests + EntityFrameworkCore.Tests): test AppService orchestration + repository queries against SQLite in-memory. Fast (<1s per test class), isolated, deterministic.
- **E2E / smoke tests**: Docker compose + Playwright + real browser + running AuthServer + HttpApi.Host + Angular dev server. Slow, flakier, require environment setup. Belongs in Layer 3 once we have the deploy pipeline to run them against.

### 3.3 What E2E infrastructure already exists (and is NOT Phase B-6 scope)

- **Docker E2E validation report** at [docs/verification/DOCKER-E2E-VALIDATION.md](../verification/DOCKER-E2E-VALIDATION.md): one-off cold-start validation from 2026-04-16. Manual click-through via Playwright MCP. Not automated.
- **Protractor config** at [angular/e2e/protractor.conf.js](../../angular/e2e/protractor.conf.js): legacy file from ABP scaffold. Protractor is EOL'd. Not wired into CI. Leave alone.
- **Karma + Jasmine** at [angular/karma.conf.js](../../angular/karma.conf.js): unit test runner for Angular. Currently finds 0 spec files (no `.spec.ts` tests yet). The LCOV reporter is already wired (B-4); as soon as tests exist they'll feed SonarCloud coverage.
- No Playwright / Cypress / Selenium harness in CI today.

### 3.4 Coverage metric semantics

SonarCloud's "coverage" metric counts **lines of production code executed by any test** divided by lines of production code (test code is excluded from the denominator via `sonar.coverage.exclusions` in [sonarcloud.yml:88](../../.github/workflows/sonarcloud.yml#L88)). This means:
- Unit + integration tests contribute to coverage.
- E2E tests would too, IF their coverage could be collected — but collecting coverage from a deployed Docker stack is much harder than from in-process test runs.
- A 60% unit + integration coverage is far more valuable than a 5% E2E coverage with the same investment.

---

## 4. Test infrastructure — what exists today

### 4.1 Test projects under `test/`

| Project | Role | Status today |
|---|---|---|
| `HealthcareSupport.CaseEvaluation.TestBase` | Shared base classes + `IDataSeedContributor` implementations | 1 contributor: [DoctorsDataSeedContributor.cs](../../test/HealthcareSupport.CaseEvaluation.TestBase/Data/DoctorsDataSeedContributor.cs) |
| `HealthcareSupport.CaseEvaluation.Domain.Tests` | Domain-layer tests (`DomainService`, entity invariants) | Discovers but has only sample tests |
| `HealthcareSupport.CaseEvaluation.Application.Tests` | AppService CRUD + edge cases | 3 test classes: Doctors (5 tests), Books (3 tests), 1 sample |
| `HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests` | Repository queries + nav-property joins + filter combinatorics | 7 test classes, 13 tests total |
| `HealthcareSupport.CaseEvaluation.HttpApi.Client.ConsoleTestApp` | Manual smoke client — NOT xUnit | Out of Phase-B-6 scope |

At Phase-B closure: **13 tests pass, 0 fail.** ~6% coverage. Only Doctors and Books have real coverage.

### 4.2 Test base class chain — verified `file:line` references

```
Volo.Abp.Testing.AbpIntegratedTest<TStartupModule>       (ABP framework)
  |
  v
CaseEvaluationTestBase<TStartupModule>                    [test/HealthcareSupport.CaseEvaluation.TestBase/CaseEvaluationTestBase.cs:12-67]
    - SetAbpApplicationCreationOptions: options.UseAutofac()    (line 17)
    - BeforeAddApplication: loads appsettings.json + appsettings.secrets.json (lines 20-25)
    - WithUnitOfWorkAsync<TResult>() helper for EF tests         (lines 48-65)
  |
  v
CaseEvaluationApplicationTestBase<TStartupModule>         (abstract; extends above)
  |
  v
{Entity}AppServiceTests<TStartupModule>                   (abstract per-entity base; see DoctorApplicationTests.cs:11)
  |
  v
EfCore{Entity}AppServiceTests                             (concrete; uses [Collection])
```

Parallel chain for repo tests:
```
CaseEvaluationTestBase<TStartupModule>
  |
  v
CaseEvaluationDomainTestBase<TStartupModule>              (or CaseEvaluationEntityFrameworkCoreTestBase)
  |
  v
{Entity}RepositoryTests                                   (concrete; [Collection])
```

### 4.3 SQLite in-memory — critical plumbing

[CaseEvaluationEntityFrameworkCoreTestModule.cs:54-87](../../test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/EntityFrameworkCore/CaseEvaluationEntityFrameworkCoreTestModule.cs#L54) sets up:

```csharp
private static SqliteConnection CreateDatabaseAndGetConnection()
{
    var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();
    var options = new DbContextOptionsBuilder<CaseEvaluationDbContext>()
        .UseSqlite(connection).Options;
    using (var context = new CaseEvaluationDbContext(options))
    {
        context.GetService<IRelationalDatabaseCreator>().CreateTables();
    }
    return connection;
}
```

**Why this matters:** The `SqliteConnection` must STAY OPEN for the database to persist — closing it drops the in-memory DB. The connection lives for the lifetime of `CaseEvaluationEntityFrameworkCoreFixture` (the xUnit `ICollectionFixture`).

### 4.4 Collection fixture — why `[Collection]` is mandatory

[CaseEvaluationTestConsts.cs:1-6](../../test/HealthcareSupport.CaseEvaluation.TestBase/CaseEvaluationTestConsts.cs#L1):

```csharp
public static class CaseEvaluationTestConsts
{
    public const string CollectionDefinitionName = "CaseEvaluation collection";
}
```

The collection-fixture definition sits next to the EF Core test module. Every concrete EF / AppService test class **must** have `[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]` on the class. Without it, xUnit creates a fresh module + fresh SQLite DB per test class:
- Seeded data becomes invisible across tests.
- Test suite runtime balloons (each class pays the full ABP module bootstrap).
- Occasional xUnit parallel-execution races on the connection.

**Reference to copy:** [EfCoreDoctorsAppServiceTests.cs:7](../../test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/EntityFrameworkCore/Applications/Doctors/EfCoreDoctorsAppServiceTests.cs#L7).

---

## 5. DataSeedContributor pattern — the reference to copy

[DoctorsDataSeedContributor.cs:1-34](../../test/HealthcareSupport.CaseEvaluation.TestBase/Data/DoctorsDataSeedContributor.cs#L1) is the template. Key shape:

```csharp
public class DoctorsDataSeedContributor : IDataSeedContributor, ISingletonDependency
{
    private bool IsSeeded = false;                 // gate against duplicate seeding
    private readonly IDoctorRepository _doctorRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public DoctorsDataSeedContributor(IDoctorRepository repo, IUnitOfWorkManager uow)
    { ... }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (IsSeeded) return;

        await _doctorRepository.InsertAsync(new Doctor(
            id: Guid.Parse("63b171d1-b8d1-4a84-98c2-435381633f67"),
            firstName: "<hex>", lastName: "<hex>", email: "<hex>@<hex>.com",
            gender: default, identityUserId: null));
        // ... second doctor with id b6d53903-5956-47fe-a12d-02982664ed4f

        await _unitOfWorkManager!.Current!.SaveChangesAsync();
        IsSeeded = true;
    }
}
```

### 5.1 Hardcoded synthetic GUIDs used today

| Entity | GUID | Role |
|---|---|---|
| Doctor 1 | `63b171d1-b8d1-4a84-98c2-435381633f67` | Happy-path tests reference this |
| Doctor 2 | `b6d53903-5956-47fe-a12d-02982664ed4f` | Second entity for list-count assertions |

**Anti-pattern in existing code (fix this in B-6):** tests hardcode the GUID literal inline (e.g., [DoctorApplicationTests.cs:30](../../test/HealthcareSupport.CaseEvaluation.Application.Tests/Doctors/DoctorApplicationTests.cs#L30)). For new entities, centralise in a static `TestData` class per entity:

```csharp
public static class PatientTestData
{
    public static readonly Guid Patient1Id = Guid.Parse("...");
    public static readonly Guid Patient2Id = Guid.Parse("...");
}
```

### 5.2 Lifecycle — what runs when

- Contributor is `ISingletonDependency` -> single instance per ABP module lifetime.
- ABP calls `SeedAsync` exactly once during module initialisation (see `CaseEvaluationTestBaseModule.OnApplicationInitialization`).
- Test collection = one module instance = seed runs once for the whole collection.
- `IsSeeded` flag is belt-and-braces — prevents double-insert if multiple fixtures share a contributor registration.

### 5.3 FK dependency order — contributors must seed top-down

```
1. State                                                       (no FKs)
2. AppointmentLanguage                                         (no FKs)
3. AppointmentType                                             (no FKs)
4. AppointmentStatus                                           (no FKs)
5. Location                 needs State, AppointmentType
6. Doctor                   needs IdentityUser
7. DoctorAvailability       needs Doctor, Location, AppointmentType
8. Patient                  needs State, AppointmentLanguage, IdentityUser
9. Appointment              needs Patient, AppointmentType, Location,
                            DoctorAvailability, IdentityUser
10. ApplicantAttorney       minimal deps (IdentityUser + State)
11. AppointmentAccessor     needs Appointment, IdentityUser
12. AppointmentApplicantAttorney   needs Appointment, ApplicantAttorney, IdentityUser
13. AppointmentEmployerDetail      needs Appointment, State
```

Seed-contributor ordering: ABP's `IDataSeedContributor` doesn't have an explicit `[Order]` attribute, but because each contributor receives the full `DataSeedContext` during module init, later contributors can rely on earlier ones having run — provided their `DependsOn` module chain is correct. Simplest approach: one contributor per entity, and the contributor explicitly references the prior entity's seeded GUIDs via `TestData` constants.

### 5.4 IdentityUser gap — the blocker for several entities

The Doctors contributor seeds with `identityUserId: null`. For Patients, Appointments, Accessors, and AppointmentApplicantAttorneys — several real tests need an actual IdentityUser to exercise permission + current-user flows. ABP's `IdentityUser` seeding requires `IIdentityUserRepository` + `IdentityUserManager`.

Recommended approach:
- Create **`IdentityUsersDataSeedContributor`** in `test/HealthcareSupport.CaseEvaluation.TestBase/Data/` FIRST, before any entity that FKs to IdentityUser.
- Seed 2-3 known IdentityUsers with hardcoded GUIDs and `TEST-` prefixed usernames.
- All downstream contributors reference those GUIDs by name from `TestData` constants.
- `Patient.GetOrCreatePatientForAppointmentBookingAsync` creates IdentityUsers at runtime — that needs ABP Identity module fully wired. Mark as stretch / Tier 4.

Research reference: [ABP Identity Module docs](https://abp.io/docs/latest/modules/identity). The next session must read this BEFORE attempting `IdentityUsersDataSeedContributor`.

---

## 6. Doctor reference tests dissected

### 6.1 AppService test class (abstract + concrete)

[DoctorApplicationTests.cs:11-97](../../test/HealthcareSupport.CaseEvaluation.Application.Tests/Doctors/DoctorApplicationTests.cs#L11) is the abstract base. Dependency injection:

```csharp
private readonly IDoctorsAppService _doctorsAppService = GetRequiredService<IDoctorsAppService>();
private readonly IRepository<Doctor, Guid> _doctorRepository = GetRequiredService<IRepository<Doctor, Guid>>();
```

Five test methods — one per AppService CRUD method (`GetListAsync`, `GetAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`). Assertions use Shouldly (`.ShouldBe`, `.ShouldNotBeNull`, `.ShouldContain`), **NOT** xUnit's `Assert.Equal`. Uniformly.

Concrete at [EfCoreDoctorsAppServiceTests.cs:7](../../test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/EntityFrameworkCore/Applications/Doctors/EfCoreDoctorsAppServiceTests.cs#L7):

```csharp
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreDoctorsAppServiceTests
    : DoctorsAppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule> { }
```

### 6.2 Repository test class

[DoctorRepositoryTests.cs:11-47](../../test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/EntityFrameworkCore/Domains/Doctors/DoctorRepositoryTests.cs#L11). Uses the `WithUnitOfWorkAsync` wrapper:

```csharp
await WithUnitOfWorkAsync(async () =>
{
    var result = await _doctorRepository.GetListAsync(...);
    result.Count.ShouldBe(1);
});
```

**Critical:** omitting the UoW wrapper on repository tests causes false-negative failures — EF Core's change tracker may not flush without a transaction scope.

### 6.3 Doctor test coverage gaps (even the reference is incomplete)

- `GetWithNavigationPropertiesAsync` — the 4-way JOIN query. Untested.
- All four lookup methods (`GetIdentityUserLookupAsync`, `GetTenantLookupAsync`, `GetAppointmentTypeLookupAsync`, `GetLocationLookupAsync`). Untested.
- `UpdateAsync` IdentityUser sync side-effect — updating a Doctor mirrors Name/Surname/Email into the linked IdentityUser. **No test verifies this side-effect.**

Tier 1 work should address these Doctor gaps as a warm-up.

---

## 7. Multi-tenancy minefield

From root [CLAUDE.md](../../CLAUDE.md) lines 129-137:

### 7.1 The 7 IMultiTenant entities

Doctor, Appointment, DoctorAvailability, ApplicantAttorney, AppointmentAccessor, AppointmentApplicantAttorney, AppointmentEmployerDetail.

**Test implication:** ABP's automatic data filter applies. In a test without an explicit `CurrentTenant.Change(tenantId)` scope, queries return host-side data only (TenantId == null). If your seed inserts tenant-scoped rows (TenantId != null), tests that don't change tenant will NOT see them.

Two patterns to use:
```csharp
// Pattern A: seed host-only, test host-only
await _appointmentRepository.InsertAsync(new Appointment(..., tenantId: null));

// Pattern B: test within a tenant scope
using (CurrentTenant.Change(TestTenantId))
{
    var list = await _appointmentsAppService.GetListAsync(new GetAppointmentsInput());
    list.TotalCount.ShouldBe(2);
}
```

### 7.2 The 6 host-only entities

Location, State, WcabOffice, AppointmentType, AppointmentStatus, AppointmentLanguage. Configured in `OnModelCreating` inside `if (builder.IsHostDatabase())`. Tests: seed with `tenantId: null` (they have no TenantId at all).

### 7.3 The Patient anomaly (HIGH RISK — HIPAA concern)

Patient has a `TenantId` property BUT does NOT implement `IMultiTenant`. **ABP's automatic tenant filter does NOT apply to Patient queries.** This means:
- `_patientRepository.GetListAsync()` returns patients from ALL tenants.
- Manual filtering is required in production (see [DATA-FLOWS.md](../security/DATA-FLOWS.md)).

Test impact: Patient tests **must** explicitly filter by `TenantId` in assertions, or use `IDataFilter.Disable<IMultiTenant>()` + manual `.Where(p => p.TenantId == X)`. See [DoctorsAppService.cs](../../src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorsAppService.cs) for the `using (_dataFilter.Disable<IMultiTenant>()) { ... }` pattern.

Write at least one Patient test that **fails without manual tenant filtering** to encode the gap as an executable spec.

---

## 8. HIPAA synthetic-data rules (non-negotiable)

From [.claude/rules/test-data.md](../../.claude/rules/test-data.md). A PHI scanner hook blocks real-looking patterns on commit.

| Field shape | Synthetic value |
|---|---|
| Names (first/last/middle) | Random hex matching max length, e.g., `551551e068be423cb150129a2fb3fd1f0c6bc2ecc7...` |
| Email | `{randomhex}@{randomhex}.com`, e.g., `7c7fa4aa54e94b09adf79@07d1fd7ead804f659d7d5.com` |
| Phone | 555 prefix reserved range only, e.g., `5551234567` |
| Date of birth | Obviously fake, e.g., `1990-01-01` |
| SSN-shaped | Hex-only strings that do NOT look like an SSN (no `XXX-XX-XXXX` formatting) |
| Identifiers | `TEST-` or `FAKE-` prefix, e.g., `TEST-PANEL-00001` |
| Tenant names | `TEST-TENANT-<hex>` |
| Confirmation numbers | `A00001` format fits `RequestConfirmationNumber` regex but use high digit ranges (`A99999`) to avoid collision with natural data |

Look at [DoctorsDataSeedContributor.cs:15-21](../../test/HealthcareSupport.CaseEvaluation.TestBase/Data/DoctorsDataSeedContributor.cs#L15) for the hex-name pattern already in use. Copy that style.

A helper like `TestStringUtility.RandomHex(maxLength)` might be worth adding to `TestBase` to avoid hand-generating hex per contributor.

---

## 9. Coverage measurement pipeline

### 9.1 CI pipeline (how coverage reaches SonarCloud)

From [.github/workflows/sonarcloud.yml](../../.github/workflows/sonarcloud.yml) lines 84-124:

```bash
# 1. Run tests under dotnet-coverage, producing VSCoverageXml
dotnet-coverage collect "dotnet test HealthcareSupport.CaseEvaluation.slnx --no-build -c Release" \
  -f xml -o coverage.xml

# 2. Hand coverage to SonarCloud scanner (at begin step)
dotnet sonarscanner begin /k:"..." \
  /d:sonar.cs.vscoveragexml.reportsPaths="coverage.xml" \
  /d:sonar.javascript.lcov.reportPaths="angular/coverage/**/lcov.info"

# 3. Build and test run (SonarScanner intercepts)
dotnet build ...
dotnet-coverage collect "dotnet test ..."

# 4. Close analysis
dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN"
```

Coverage exclusions (line 88): already exclude `Program.cs`, `*Module.cs`, `*DbContext*`, migrations, and `angular/src/app/proxy/**`. Test code is excluded from coverage denominators automatically.

### 9.2 Local coverage check (no SonarCloud needed)

```bash
dotnet tool install --global dotnet-coverage
dotnet tool install --global dotnet-reportgenerator-globaltool

dotnet-coverage collect "dotnet test HealthcareSupport.CaseEvaluation.slnx -c Release" -f cobertura -o cov.cobertura.xml
reportgenerator -reports:cov.cobertura.xml -targetdir:./coverage-report -reporttypes:Html
# open ./coverage-report/index.html in a browser
```

### 9.3 How to read SonarCloud coverage

- **Overall coverage %** = `new_coverage_lines / total_coverable_lines` across the whole project, minus exclusions.
- **New-code coverage %** = only lines added or changed in the latest analysis period. This is the Quality Gate metric; PRs that regress new-code coverage fail the gate.
- Per-file coverage visible at [sonarcloud.io project dashboard](https://sonarcloud.io/project/overview?id=gesco-healthcare-support_hcs-patient-portal).

---

## 10. SQLite-vs-SQL-Server pitfalls

**The codebase is clean** — no raw SQL, no vendor-specific column types, no `HierarchyId` / `TEMPORAL TABLES` / full-text search. All EF migrations are provider-agnostic.

**But watch for:**

1. **Case sensitivity in `LIKE`**: SQLite's `LIKE` is case-insensitive on ASCII but differs from SQL Server on non-ASCII. If Patient names use unicode chars in a test, a filter test might pass on SQLite and fail on prod. Mitigation: use only ASCII hex in synthetic names.
2. **DateTime precision**: SQLite stores DateTime as TEXT with arbitrary precision; SQL Server is `datetime2(7)`. Tests that compare DateTimes down to sub-millisecond precision will diverge. Always truncate to second precision in test assertions.
3. **GUID collation**: SQLite compares GUIDs as strings; SQL Server uses its own ordering. Filter tests that sort by `Id` may return different orders. Sort by a deterministic column (CreationTime, PanelNumber) in tests.
4. **Transaction isolation**: SQLite has limited SERIALIZABLE support. Concurrency tests are not reliable on SQLite — skip them or run against a real SQL Server container.
5. **`UseSqlServer()` extensions**: verify none of the migration scripts call SQL-Server-only EF extensions. Quick grep: `grep -rn "SqlServer\." src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/` — should return zero.

---

## 11. Entities still uncovered — priority matrix

| # | Entity | Priority | Why | Key gotchas |
|---|---|---|---|---|
| 1 | **Appointments** | CRITICAL | Core scheduling, 13-state lifecycle, no tests today | No enforced state transitions; slot not released on delete; two parallel UI flows; view/:id missing permissionGuard |
| 2 | **DoctorAvailabilities** | CRITICAL | Slot booking + bulk slot generation; also unlocks P-11 refactor | `GeneratePreviewAsync` cognitive 41 (deferred, see [P-11.md](../issues/research/P-11.md)); slot state machine |
| 3 | **Patients** | CRITICAL | 22+ fields, HIPAA, booking-or-create flow | Has TenantId but NOT IMultiTenant — manual filtering required; typo `RefferedBy`; `GetOrCreatePatientForAppointmentBookingAsync` requires deeper Identity infra |
| 4 | **Locations** | HIGH | FK target for Appointments + DoctorAvailability; host-scoped | Host-only (`if (builder.IsHostDatabase())` guard); 1 FK target for 3 entities |
| 5 | **ApplicantAttorneys** | HIGH | Attorney + IdentityUser link; joins Appointment | IMultiTenant + IdentityUser link required |
| 6 | **AppointmentAccessors** | SECONDARY | Access-control grants per appointment | Attorney-scoped filtering flows depend on this |
| 7 | **AppointmentApplicantAttorneys** | SECONDARY | Join entity (Appointment / Attorney / User) | Three-way FK; IMultiTenant |
| 8 | **AppointmentEmployerDetails** | SECONDARY | Employer data per appointment | Simple child-of-Appointment |
| 9 | **States** | LOOKUP | 5 inbound FKs | Host-only |
| 10 | **AppointmentTypes** | LOOKUP | M2M with Doctor | Host-only |
| 11 | **AppointmentStatuses** | LOOKUP | Lookup; note: parallel to the enum | Host-only |
| 12 | **AppointmentLanguages** | LOOKUP | Host-only lookup | Host-only |
| 13 | **WcabOffices** | LOOKUP | Host-only; Excel export feature | Host-only |

Stretch:
- `Patient.GetOrCreatePatientForAppointmentBookingAsync` — creates IdentityUsers at runtime; needs ABP Identity test infrastructure.
- `AppointmentManager` domain-service business rules (confirmation number generation `A#####`, slot booking).

---

## 12. Tier breakdown and per-PR deliverables

### 12.1 Tier 1 — Critical business path (5-6 PRs)

Prerequisite: **new `IdentityUsersDataSeedContributor` in a 0th PR**, before anything that FKs to IdentityUser.

- **PR-1A:** **Appointments** — `AppointmentsDataSeedContributor` + `AppointmentsAppServiceTests` (CRUD + each of the 5 slot-validation paths) + `EfCoreAppointmentRepositoryTests` (nav-props + filters) + document the no-state-machine gotcha as a "test gap" comment.
- **PR-1B:** **DoctorAvailabilities** — contributor + tests for `GetListAsync`, `GetWithNavigationPropertiesAsync`, `GenerateAsync`, `GeneratePreviewAsync` (at least 3 scenarios — happy path, boundary, conflict). **After this PR lands, immediately do the P-11 unblock step in Section 12.5.**
- **PR-1C:** **Patients** — contributor + tests. Explicitly cover the non-IMultiTenant filtering behaviour. Include at least one test that fails without manual tenant filtering to encode the gap.
- **PR-1D:** **Locations** — host-only contributor + tests. Simpler; useful momentum.
- **PR-1E:** **ApplicantAttorneys** — IMultiTenant + IdentityUser link tested.

### 12.2 Tier 2 — Secondary (2-3 PRs)

Do not start Tier 2 until Tier 1 coverage delta is verified (~25% bump expected). PRs: AppointmentAccessors, AppointmentApplicantAttorneys, AppointmentEmployerDetails.

### 12.3 Tier 3 — Lookups (1 PR)

All 5 host-only lookup entities in one PR. They are simple enough that bundling is fine; each entity gets a contributor + CRUD tests.

### 12.4 Stretch tier — IdentityUser-dependent flows

`Patient.GetOrCreatePatientForAppointmentBookingAsync` + `AppointmentManager` rules + `ExternalSignup` flow.

### 12.5 P-11 unblock (after PR-1B lands)

1. Edit [.github/workflows/sonarcloud.yml](../../.github/workflows/sonarcloud.yml) — remove the `abpDavailComplexity` entry and its two companion `ruleKey`/`resourceKey` lines (plus the entry from the multicriteria list).
2. Refactor `DoctorAvailabilitiesAppService.GeneratePreviewAsync` — extract `ValidateGenerateInput`, `GeneratePreviewSlots`, `DetectConflicts` helpers. Preserve throws verbatim.
3. `dotnet build -warnaserror` + `dotnet test` must stay clean.
4. Confirm SonarCloud shows S3776 cleared on the file.

Full checklist is in [docs/issues/research/P-11.md](../issues/research/P-11.md).

### 12.6 Per-PR deliverables (copy into each plan file)

Every Tier-1/Tier-2/Tier-3 PR MUST include:

- [ ] `{Entity}DataSeedContributor` in `test/HealthcareSupport.CaseEvaluation.TestBase/Data/` with hardcoded synthetic GUIDs.
- [ ] `{Entity}TestData` static class exposing the GUIDs as named constants.
- [ ] Abstract `{Entity}AppServiceTests<TStartupModule>` in `test/HealthcareSupport.CaseEvaluation.Application.Tests/{Entity}/` — CRUD happy path + 2-3 edge cases per method (null/empty guard, multi-tenant isolation where relevant, business-rule violation throws).
- [ ] Concrete `EfCore{Entity}AppServiceTests` subclass with `[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]`.
- [ ] `{Entity}RepositoryTests` in `test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/EntityFrameworkCore/Domains/{Entity}/` — nav-property queries + filter-combination tests + at least one multi-tenant isolation assertion where applicable.
- [ ] All assertions use Shouldly (`ShouldBe`, `ShouldNotBeNull`, `ShouldContain`, `ShouldThrow`).
- [ ] Repository tests wrap all assertions in `WithUnitOfWorkAsync`.
- [ ] Synthetic-data-only. Hex names, 555 phone numbers, 1990-01-01 DOBs, `TEST-`/`FAKE-` prefixes.
- [ ] CI green: backend build 0/0 with `-warnaserror`, all existing tests still pass, new tests pass, SonarCloud coverage delta is measurable.
- [ ] Commit title convention: `test({entity}): add CRUD + edge cases for {Entity} (B-6 T{tier})`.

### 12.7 Final Phase-B-6 closure PR checklist

- [ ] SonarCloud overall coverage >= 60%.
- [ ] SonarCloud new-code coverage >= 80%.
- [ ] Quality Gate green on new code.
- [ ] All 13 entities have at least 1 seed contributor.
- [ ] `P-11` refactor removed the DoctorAvailability S3776 suppression successfully.

---

## 13. Entity-specific gotchas

### 13.1 From [Appointments/CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md)

1. **No enforced state transitions.** Any code can set any `AppointmentStatus` directly. Decide whether to write tests that encode the intended state machine (and fail against current behaviour) or test only observable behaviour.
2. **Slot not released on delete.** Deleting an Appointment does not set `DoctorAvailability.BookingStatusId` back to `Available`. Test should encode this gap so it's visible in coverage.
3. **`view/:id` route has no permissionGuard.** Any authenticated user can view any appointment. UI-level issue, not unit-testable, but worth documenting in the PR.
4. **Two parallel UI flows, inconsistent form patterns.** Out of scope for unit tests; mention in PR description.
5. **`RequestConfirmationNumber` auto-generation caps at `A99999`.** No overflow handling. Write a boundary test.

### 13.2 From [Patients/CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/Patients/CLAUDE.md)

1. **Not IMultiTenant despite TenantId.** ABP's auto filter does NOT apply. Test explicit manual filter logic.
2. **Typo `RefferedBy`.** Not a test concern but the DTO field is misspelled — assertions must match.
3. **`GetOrCreatePatientForAppointmentBookingAsync` side effects.** Creates IdentityUser, creates Patient, links both. Complex flow — Tier 4 stretch.

### 13.3 From [Doctors/CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/Doctors/CLAUDE.md)

1. **UpdateAsync syncs IdentityUser.** Non-obvious side effect. Add a test that catches a `DoctorsAppService.UpdateAsync` regression.
2. **Collection sync uses "except given IDs" pattern.** Full list must be sent to replace. Write a test that exercises partial replacement.

### 13.4 From `.claude/rules/code-standards.md`

- Complexity thresholds are enforced via Sonar. **New test code must itself stay within the thresholds** (15 cognitive, 10 cyclomatic, 50 lines per function). Long test methods = refactor into private helpers or separate test methods.

### 13.5 From CI

- `dotnet format --verify-no-changes` runs on every PR. Run `dotnet format` locally before push.
- Commit-message validation (commitlint) enforces `<type>(<scope>): <subject>` at <=100 chars. Use `test({entity-kebab}): ...`.

---

## 14. CI / CD / pre-commit inventory (complete)

### 14.1 GitHub Actions workflows (`.github/workflows/`, 17 files)

| Workflow | Trigger | What it enforces | Status |
|---|---|---|---|
| `ci.yml` | PR on main/dev/staging/prod | Meta/changed-paths, Backend Build (0/0), Backend Test, Backend Format Check (dotnet format), Frontend Build, Frontend Lint, Frontend Format Check (Prettier), Frontend Test, Docs Structure Check | Active (Layer 2) |
| `sonarcloud.yml` | push + PR | C# + TS static analysis via SonarCloud scanner, coverage upload (VSCoverageXml + LCOV), Quality Gate (informational) | Active |
| `codeql-pr.yml` | PR | CodeQL csharp + javascript-typescript analysis | Active (informational) |
| `security.yml` | schedule (weekly Mon 06:00 UTC), workflow_dispatch | `dotnet list package --vulnerable`, npm audit, CodeQL weekly scheduled scan | Active |
| `scorecard.yml` | push + schedule | OpenSSF Scorecard supply-chain security analysis (100 alerts open, deferred per SEC-06) | Active |
| `commitlint.yml` | PR | Commit-message format per `@commitlint/config-conventional`: type-enum, subject <=100 chars, body <=200 chars | Active |
| `dependency-review.yml` | PR | License + known-vuln check on dependency changes | Active |
| `doc-check.yml` | PR (when docs change) | Markdown link + structure check | Active |
| `lint-meta.yml` | PR | YAML lint on workflows + config | Active |
| `pr-title.yml` | PR | PR title format validation (conventional commits) | Active |
| `pr-size.yml` | PR | Size label (S/M/L/XL) | Active |
| `labeler.yml` | PR | Auto-label by changed paths (backend/frontend/deps/ci) | Active |
| `trufflehog-pr.yml` | PR | TruffleHog v3.94.3 secret scan on diff | Active |
| `auto-pr-dev.yml` | push to main | Auto-opens promotion PR main -> development | Active |
| `deploy-dev.yml` | push to development | Runs validate (build+test placeholder, Layer 3 deferred), then auto-opens promotion PR development -> staging on success | Active (validate placeholder; auto-PR active) |
| `promote-staging.yml` | push to staging | Runs integration-validate (build+test placeholder, Layer 3 deferred); staging -> production PR is always manual | Active (validate placeholder) |
| `release.yml` | tag push | Semantic versioning + changelog automation | Placeholder |

### 14.2 Layer-1 local Git hooks (`angular/.husky/`)

Wired via `"prepare": "cd .. && husky angular/.husky"` in [angular/package.json](../../angular/package.json). Adrian documented these as active as of 2026-04-13.

| Hook | What it runs | Source |
|---|---|---|
| `pre-commit` | Gitleaks `protect --staged` (secret scan) + lint-staged (`prettier --write` + `eslint --fix --max-warnings=0` on staged `.ts`, `.html`, `.scss`, `.css`, `.json`, `.md`) | [angular/.husky/pre-commit](../../angular/.husky/pre-commit) |
| `commit-msg` | Commitlint against `angular/commitlint.config.js` (CommonJS sibling of the ESM config used by CI) | [angular/.husky/commit-msg](../../angular/.husky/commit-msg) |
| `pre-push` | Full `gitleaks detect` (whole working tree) + backend Debug build (`dotnet build -c Debug --nologo -v q`) | [angular/.husky/pre-push](../../angular/.husky/pre-push) |

WSL vs Git-Bash quirk: the hooks fall back to `dotnet.exe` if `dotnet` isn't on PATH (WSL interop to Windows .NET SDK). Don't break this in B-6.

### 14.3 Code formatters + linters

| Check | Config file | Enforces |
|---|---|---|
| Prettier (frontend) | [angular/.prettierrc](../../angular/.prettierrc) | 100 printWidth, 2-space tabs, trailing commas, single quotes, Angular HTML parser |
| ESLint (frontend) | [angular/.eslintrc.json](../../angular/.eslintrc.json) | Angular Eslint plugin, `--max-warnings=0` in lint-staged |
| `dotnet format` (backend) | [.editorconfig](../../.editorconfig) + `Directory.Build.props` | C# style, whitespace, namespace declaration |
| Commitlint (Husky + CI) | [angular/commitlint.config.mjs](../../angular/commitlint.config.mjs) + `.js` sibling | type-enum [feat/fix/docs/style/refactor/test/chore/ci/perf/build/revert], subject <=100, body-max-line <=200 |
| Markdown lint | [.markdownlint.json](../../.markdownlint.json) | Lists/tables/headers rules |

### 14.4 Dependency + security scanning

| Tool | Where | Scope |
|---|---|---|
| Dependabot | [.github/dependabot.yml](../../.github/dependabot.yml) | NuGet (weekly, `open-pull-requests-limit: 0` while ABP commercial holds Angular 20 back), npm (angular/ + AuthServer), GitHub Actions (weekly) |
| Gitleaks | `.gitleaks.toml` + pre-commit hook + pre-push hook | Secrets in staged files + whole tree |
| TruffleHog | `trufflehog-pr.yml` + scorecard.yml | Pattern-based secret scan; v3.94.3 pinned |
| CodeQL | `codeql-pr.yml` + scheduled in `security.yml` | C# + TypeScript vulnerabilities |
| OpenSSF Scorecard | `scorecard.yml` (v2.4.3 pinned) | Supply-chain hygiene; 100 findings open (Phase-C scope) |

### 14.5 Branch protection (as of 2026-04-20)

- `main`: required status checks `Backend: Build`, `Frontend: Build`. 1 required PR approval. `enforce_admins=false`. `dismiss_stale_reviews=true`. `required_linear_history=false`.
- Solo-dev workflow: Adrian `--admin` merges after CI is green; self-approval is impossible (ABP commercial licence owner is the only human on the repo).

### 14.6 Informational vs blocking — Phase-C flip target

Today, SonarCloud Quality Gate failures are **informational** — the check fails but does not block merge. Phase B-6 delivers the coverage metric that will let Phase C flip this to blocking without immediately red-lining every PR.

---

## 15. Code navigation starting points

When you need to understand a feature, follow this order — do not read the whole repo.

1. **Root [CLAUDE.md](../../CLAUDE.md)** — 280 lines. Contains the feature index table, ABP conventions, multi-tenancy rules, critical constraints.
2. **Per-feature `CLAUDE.md`** at `src/HealthcareSupport.CaseEvaluation.Domain/{Entity}/CLAUDE.md`. Every entity has one. These contain:
   - File map across 14 layers (Domain.Shared, Domain, Application.Contracts, Application, EntityFrameworkCore, HttpApi, HttpApi.Host, AuthServer, DbMigrator + 5 Angular layers).
   - Entity shape + FK relationships.
   - Multi-tenancy status.
   - Permission map.
   - Business rules.
   - **Known gotchas** — the highest-ROI section.
3. **Per-layer `CLAUDE.md`**: [Application.Contracts/CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Application.Contracts/CLAUDE.md), [Application/CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Application/CLAUDE.md), [EntityFrameworkCore/CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/CLAUDE.md), etc. Explain DTO naming, Mapperly setup, repository conventions.
4. **`.claude/discovery/reference-pattern.md`** — the full "how to add a feature" reference using Appointments as the exemplar.
5. **`.claude/rules/`** — the rules directory (`dotnet.md`, `angular.md`, `hipaa.md`, `test-data.md`, `code-standards.md`, `dotnet-env.md`).
6. **`docs/INDEX.md`** — human-readable documentation index.
7. **`docs/issues/`** — BUGS.md, INCOMPLETE-FEATURES.md, TECHNICAL-OPEN-QUESTIONS.md, and `research/` folder (60+ research docs including `P-11.md` and `SEC-06.md`).

---

## 16. External references — what to read outside this repo

**Before you write any test, read at least the Must-read list. The Should-read list is for the specific scenarios you'll hit.**

### 16.1 ABP Framework docs (Must-read)

- [ABP Testing overview](https://abp.io/docs/latest/testing) — how `AbpIntegratedTest<TStartupModule>` works, how modules compose, how DI is wired for tests.
- [ABP Data Seeding](https://abp.io/docs/latest/framework/infrastructure/data-seeding) — `IDataSeedContributor`, lifecycle, `DataSeedContext`, when it runs.
- [ABP Multi-Tenancy](https://abp.io/docs/latest/framework/architecture/multi-tenancy) — `IMultiTenant` interface, `CurrentTenant.Change(...)`, `IDataFilter.Disable<IMultiTenant>()`.
- [ABP Unit of Work](https://abp.io/docs/latest/framework/architecture/domain-driven-design/unit-of-work) — why repository tests need `WithUnitOfWorkAsync`.
- [ABP Identity module](https://abp.io/docs/latest/modules/identity) — required for seeding IdentityUsers for Patient / Appointment tests.
- [ABP SaaS module](https://abp.io/docs/latest/modules/saas) — tenant entity for multi-tenant tests.
- [ABP Startup templates / testing folder structure](https://abp.io/docs/latest/solution-templates/layered-web-application#testing) — confirms the 4-test-project layout is canonical.

### 16.2 xUnit + Shouldly + Autofac (Must-read)

- [xUnit docs — Collections and Fixtures](https://xunit.net/docs/shared-context#collection-fixture) — the semantics of `[Collection]` and `ICollectionFixture<T>` that make SQLite in-memory work.
- [xUnit docs — Writing tests](https://xunit.net/docs/getting-started/netcore/cmdline) — test discovery, `Fact` vs `Theory`, `async Task` tests.
- [Shouldly docs](https://docs.shouldly.org/) — `.ShouldBe`, `.ShouldNotBeNull`, `.ShouldContain`, `.ShouldThrow`, `.ShouldBeOfType<T>`.
- [Autofac docs — xUnit support](https://autofac.readthedocs.io/en/latest/integration/xunit.html) — how ABP wires Autofac into each test's DI graph.

### 16.3 EF Core testing (Must-read)

- [EF Core Testing guide](https://learn.microsoft.com/en-us/ef/core/testing/) — official Microsoft guidance. Read the "choosing a testing strategy" section especially.
- [EF Core SQLite in-memory](https://learn.microsoft.com/en-us/ef/core/testing/testing-without-the-database#sqlite-in-memory) — caveats, limitations, and why the connection must stay open.
- [EF Core test helpers — `IRelationalDatabaseCreator.CreateTables()`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.storage.irelationaldatabasecreator) — exactly what the test module calls.

### 16.4 Coverage tooling (Must-read)

- [dotnet-coverage official docs](https://learn.microsoft.com/en-us/dotnet/core/additional-tools/dotnet-coverage) — how `collect`, `merge`, `instrument` work; format differences (Cobertura, VSCoverageXml, Xml).
- [SonarCloud C# coverage import](https://docs.sonarcloud.io/enriching/test-coverage/dotnet-test-coverage/) — the exact `sonar.cs.vscoveragexml.reportsPaths` parameter.
- [SonarCloud Quality Gate conditions](https://docs.sonarcloud.io/improving/quality-gates/) — how new-code coverage thresholds feed the gate.

### 16.5 Similar repos to learn from (Should-read)

High-quality open-source ABP + tests reference repos (ranked by star count; verify at read time because counts drift):

- **[abpframework/abp](https://github.com/abpframework/abp)** (~12k stars) — the framework itself. Its `test/` folders are the canonical test style. Every ABP feature module has real test suites.
- **[abpframework/eShopOnAbp](https://github.com/abpframework/eShopOnAbp)** (~1k+ stars) — Microsoft eShop + ABP reference microservices. See how multi-module tests are structured.
- **[EasyAbp/EasyAbp.Abp.EventBus.Distributed.CAP](https://github.com/EasyAbp/)** — ABP community extensions with test patterns.
- **Look for**: how they structure `test/` directories, how `{Module}TestBase` classes compose, how `IDataSeedContributor` is used for integration tests, and how they handle multi-tenancy in tests.

### 16.6 Testing best-practices articles (Should-read)

- [Microsoft — Unit testing best practices for .NET](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices) — AAA pattern, naming conventions, test independence, avoiding shared state.
- [Martin Fowler — Test Pyramid](https://martinfowler.com/articles/practical-test-pyramid.html) — why unit tests should vastly outnumber integration tests, which in turn outnumber E2E. Use this to justify B-6 scope.
- [Martin Fowler — Deployment Pipeline](https://martinfowler.com/bliki/DeploymentPipeline.html) — the Pattern-3 model Adrian is following for Layer 1/2/3/4.
- [Roy Osherove — `UnitOfWork_Scenario_ExpectedBehavior` naming convention](https://osherove.com/blog/2005/4/3/naming-standards-for-unit-tests.html) — consider this style vs the existing test methods here (which use simpler `MethodName` style).

### 16.7 Angular testing (Should-read)

- [Angular Testing guide (official)](https://angular.dev/guide/testing) — official Angular 20 testing guide, standalone-component patterns.
- [Karma docs](https://karma-runner.github.io/latest/index.html) — current spec-runner in this repo (config at `angular/karma.conf.js`).
- [Jasmine docs](https://jasmine.github.io/) — assertion library Karma runs.
- Context: this repo has **no `.spec.ts` tests today**. Angular coverage starts at 0%. Phase B-6 focuses on backend coverage because that's where value density is highest; Angular tests are NOT required for the 60% target but are a natural follow-on.

### 16.8 HIPAA test-data guidance (Should-read)

- [HHS OCR — De-identification of PHI under Privacy Rule](https://www.hhs.gov/hipaa/for-professionals/privacy/special-topics/de-identification/index.html) — Safe Harbor method identifies 18 categories of identifiers that must be removed or randomised.
- [NIST — Synthetic data for healthcare](https://www.nist.gov/publications/creating-synthetic-datasets-testing-privacy-protection-tools) — rationale for hex-based synthetic identifiers.

### 16.9 DO NOT rely on training data — verify dates

- **Zero-trust verification**: Claude's training data may be stale on ABP 10.0.2 specifics, Angular 20.3+ behaviour, SonarCloud current UI, or GitHub Actions deprecations. For any claim about a specific version, verify against live docs at the timestamp of your session.
- The `.claude/rules/zero-trust-verification.md` rule in Adrian's global config mandates this.

---

## Appendix A — Phase-B criteria met at kickoff

At Phase-B closure commit (`main` SHA recorded by the final promotion PR):

1. [x] `dotnet build -warnaserror` clean (verified on every Phase-B PR)
2. [x] SonarCloud Quality Gate green on new code (informational today; blocking in Phase C)
3. [x] SonarCloud overall issues < 50 — actual at close: 40 (0 BUG, 0 VULNERABILITY, 40 CODE_SMELL)
4. [x] CodeQL alerts: 0
5. [x] Dependabot alerts: 0
6. [x] Scorecard runs successfully (100 alerts deferred per [SEC-06.md](../issues/research/SEC-06.md))
7. [ ] **THIS IS B-6. That is why this doc exists.**
8. [x] All 7 carry-over items resolved (karma lcov, common.props consolidation, dep-review timeout, paths-filter root, bin/ CodeQL, Scorecard pin, Node 20 deadline deferred)

---

## Appendix B — Phase-C backlog (NOT B-6 scope)

Tracked in dedicated research docs:

- [SEC-06.md](../issues/research/SEC-06.md) — 100 Scorecard alerts cleanup (SHA-pinning actions + Dockerfile digests + TokenPermissions + branch-protection decision).
- [P-11.md](../issues/research/P-11.md) — `DoctorAvailabilitiesAppService.GeneratePreviewAsync` cognitive-41 refactor (unblocked by Tier 1 B-6).

Phase-C planning items:

- Flip SonarCloud QG informational fail to blocking (gate decision with Adrian).
- Branch-protection tightening — CodeReviewID policy call (solo dev vs Copilot/CodeRabbit first-party review).
- Full Angular animations API migration beyond the 1-line `provideAnimations` swap in Phase-B-5d (both sync and async variants are deprecated in Angular 20.2+; final fix is CSS-only animations across the ABP LeptonX theme).
- Tighten the `bootstrapModalDialogRole` Sonar suppression from `**/*.component.html` to just the 2 affected modal files.
- Historical commit hygiene: PR #87 squash commit (`6d87a4f`, authored prior to Phase-B closure session) contains a `Co-authored-by: Claude ...` line that violates `rules/commit-format.md`. Rewriting history on `main` is destructive; accept as documented.
- Strengthen the `commit-msg` hook to block `Co-authored-by: Claude ...` / `noreply@anthropic.com` patterns going forward.

---

## Appendix C — Do-not-do list

- **Do not** one-shot all 13 entities in a single session.
- **Do not** skip external research (Section 16) before touching the codebase.
- **Do not** skip the `[Collection]` attribute on new test classes.
- **Do not** forget the `WithUnitOfWorkAsync` wrapper on repository tests.
- **Do not** seed without the `IsSeeded` flag.
- **Do not** use realistic-looking patient names, SSNs, phone numbers, or emails in tests.
- **Do not** refactor production code inside a test PR. Test-only PRs. If a test reveals a bug, open a separate bug-fix PR with `approach: tdd` — the test encodes the spec.
- **Do not** delete the `P-11` suppression in `.github/workflows/sonarcloud.yml` until Tier 1 DoctorAvailability tests land and pass.
- **Do not** close the Phase-B branch protection or admin-merge policy — solo dev workflow still applies.
- **Do not** add Playwright / Cypress / Protractor-based E2E tests to Phase B-6 scope. E2E is Layer 3 / Phase C+.
- **Do not** assume training-data knowledge is current for ABP 10.0.2, Angular 20.3+, SonarCloud UI, or GitHub Actions. Verify every version-specific fact against live docs per `.claude/rules/zero-trust-verification.md`.
