---
feature: phase-b6-tier1-pr1e-applicant-attorneys
date: 2026-04-24
status: in-progress
base-branch: main
related-issues: []
---

# PR-1E -- ApplicantAttorneys CRUD + constructor/manager split (B-6 T1)

**Parent:** [2026-04-20-phase-b6-tier1.md](./2026-04-20-phase-b6-tier1.md) (T6 section)
**Active child:** n/a (leaf)

## Context

Last Tier-1 PR in Phase B-6. Adds 13 xUnit tests (12 AppService + 1 repository) for the `ApplicantAttorney` entity.

`ApplicantAttorney` is `IMultiTenant`. Its constructor sets 3 string fields (`FirmName`, `FirmAddress`, `PhoneNumber`) + 2 FKs (`StateId`, `IdentityUserId`). Its `ApplicantAttorneyManager` assigns 5 more string fields post-construction (`WebAddress`, `FaxNumber`, `Street`, `City`, `ZipCode`). The ctor-vs-manager split is a code-generation artifact and is the reason PR-1E exists as a distinct Tier-1 item -- tests must exercise BOTH paths.

The IMultiTenant flag also makes this the contrast case vs Patient's non-IMultiTenant leak (FEAT-09): here ABP's automatic tenant filter works, and tests must assert that explicitly -- `GetListAsync` from `TenantA` context returns only Attorney1 (not Attorney2).

Inherits all decisions ratified in the PR-1D plan approval on 2026-04-23:

- Target `main` (canonical GitLab Flow per VERDICT.md).
- Reuse `W:\patient-portal\test-locations-coverage\` worktree via sequential `git checkout -b`.
- `.md` session-handoff files at worktree root stay untracked.

No new decisions required to execute.

## Canonical references (do NOT duplicate test design here)

- T6 spec (inherited): [2026-04-20-phase-b6-tier1.md](./2026-04-20-phase-b6-tier1.md) section T6.
- Feature detail: `src/HealthcareSupport.CaseEvaluation.Domain/ApplicantAttorneys/CLAUDE.md`.
- Multi-tenant contrast (Patient leak): `docs/issues/INCOMPLETE-FEATURES.md` FEAT-09 anchor `#patient-imultitenant`.

## Invariant guardrails

Same block as PR-1D:

- `[RemoteService(IsEnabled = false)]` on every AppService.
- Riok.Mapperly only; no `ObjectMapper.Map<>` for NEW mappers.
- `[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]` on every concrete EF test class.
- `await WithUnitOfWorkAsync(async () => { ... })` wrapping every repository test body.
- Bogus v35.6.5 + deterministic seed 20260420 (wired via `TestStringUtility`).
- Synthetic data only; `TEST-` prefix on identifier-shaped fields; `@test.local` emails.
- `DOTNET_ENVIRONMENT=Development ASPNETCORE_ENVIRONMENT=Development` on every dotnet command.
- Branch: `test/applicant-attorneys-coverage` off fresh `main`.
- Commit subject: `test(applicant-attorneys): add CRUD + ctor/mgr coverage (B-6 T1)` (66 chars, within 72 cap).
- Admin squash-merge ONLY after ALL required CI checks green. `--admin` is not a bypass.
- Never delete the branch without explicit Adrian approval.
- Zero-trust verification: verify every technical claim against live sources.

## Files

**New:**

- `test/HealthcareSupport.CaseEvaluation.TestBase/Data/ApplicantAttorneysTestData.cs` -- 2 deterministic GUIDs + Bogus-seeded field values for 8 string fields per attorney.
- `test/HealthcareSupport.CaseEvaluation.Application.Tests/ApplicantAttorneys/ApplicantAttorneysAppServiceTests.cs` -- 12 test methods (abstract generic base).
- `test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/EntityFrameworkCore/Applications/ApplicantAttorneys/EfCoreApplicantAttorneysAppServiceTests.cs` -- concrete class with `[Collection]`.
- `test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/EntityFrameworkCore/Domains/ApplicantAttorneys/ApplicantAttorneyRepositoryTests.cs` -- 1 repo-level test wrapped in `WithUnitOfWorkAsync`.

**Edited:**

- `test/HealthcareSupport.CaseEvaluation.TestBase/Data/CaseEvaluationIntegrationTestSeedContributor.cs` -- inject `IApplicantAttorneyRepository`; add `SeedApplicantAttorneysAsync()`; call after `SeedPatientsAsync()`. Seed Attorney1 under `CurrentTenant.Change(TenantsTestData.TenantARef)` with `IdentityUsersTestData.ApplicantAttorney1UserId`; Attorney2 under `CurrentTenant.Change(TenantsTestData.TenantBRef)` with `IdentityUsersTestData.DefenseAttorney1UserId` (any valid IdentityUser FK target works; tenant context is what IMultiTenant filters on).

## Test surface (13 tests)

**AppService tests (12)** in `ApplicantAttorneysAppServiceTests.cs` abstract base, wired via `EfCoreApplicantAttorneysAppServiceTests` concrete:

CRUD happy path (5):

1. `GetAsync_ReturnsSeededAttorney` -- Get Attorney1 by ID; assert all 8 strings + `IdentityUserId` match seed values.
2. `GetListAsync_FromHostContext_ReturnsAttorneysFromBothTenants` -- no tenant wrap; host sees both Attorney1 and Attorney2 (IMultiTenant filter applies only inside tenant context).
3. `CreateAsync_SetsAllEightStringFields_AndBothFks` -- create with all 8 strings populated; assert both ctor-set fields (FirmName/FirmAddress/PhoneNumber) and manager-post-construction fields (WebAddress/FaxNumber/Street/City/ZipCode) persist.
4. `UpdateAsync_ChangesMutableFields` -- update Attorney1's FirmName + WebAddress; verify persisted; restore seed values in the same test body.
5. `DeleteAsync_RemovesAttorney` -- create disposable; delete; verify `FindAsync` returns null.

Validation guards (4):

6. `CreateAsync_WhenIdentityUserIdIsEmpty_ThrowsUserFriendlyException` -- AppService guard at line 91-94.
7. `UpdateAsync_WhenIdentityUserIdIsEmpty_ThrowsUserFriendlyException` -- AppService guard at line 103-106.
8. `ApplicantAttorneyManager_CreateAsync_WhenFirmNameExceedsMax_ThrowsArgumentException` -- constructor-path `Check.Length` at `ApplicantAttorney.cs:54`.
9. `ApplicantAttorneyManager_CreateAsync_WhenWebAddressExceedsMax_ThrowsArgumentException` -- manager-post-construction-path `Check.Length` at `ApplicantAttorneyManager.cs:28`.

IMultiTenant isolation (2; contrast vs Patient FEAT-09):

10. `GetListAsync_FromTenantAContext_ReturnsOnlyAttorney1` -- wrap `GetListAsync` in `_currentTenant.Change(TenantsTestData.TenantARef)`; assert Attorney1 returned AND Attorney2 NOT returned.
11. `GetListAsync_FromTenantBContext_ReturnsOnlyAttorney2` -- mirror for TenantB.

IdentityUser lookup semantics (1):

12. `GetIdentityUserLookupAsync_FiltersByEmail_NotByUsername` -- pass filter `"@test.local"` (unique to email format, not username); assert non-empty result and `ApplicantAttorney1UserId` present.

**Repository test (1)** in `ApplicantAttorneyRepositoryTests.cs`:

13. `GetListAsync_PlainOverload_AppliesFirmNameFilter` -- wrap in `WithUnitOfWorkAsync`; call `_attorneyRepository.GetListAsync(firmName: Attorney1FirmName)`; assert Attorney1 in results and filter narrows by `Contains(firmName)`.

## Execution workflow (5 stops)

- **Stop A** -- after ExitPlanMode approval of the containing plan. Done.
- **Stop B** -- after writing this plan file + test list is self-evident, before writing any `.cs`. Post test list to Adrian if review is wanted; otherwise proceed.
- **Stop C** -- after local verification green (build 0/0, test all passing, format clean), before `git commit`.
- **Stop D** -- after husky hooks pass, before `git push`.
- **Stop E** -- after CI is green, before `gh pr merge --admin --squash`. Adrian decides.

Commits inside the branch (two atomic commits squashed on merge):

- Commit 1: `docs(plans): add nested-plan program index (B-6 T1 scaffold)` -- 5 new + 1 edited level-index files (PROGRAM, LAYER-2, LAYER-2-PHASE-B, LAYER-2-PHASE-B-6, this PR-1E plan, T6-plan header edit).
- Commit 2: `test(applicant-attorneys): add CRUD + ctor/mgr coverage (B-6 T1)` -- 4 new test files + 1 seed contributor edit.

PR title: `test(applicant-attorneys): add CRUD + ctor/mgr coverage (B-6 T1)`.
PR body: 10-section template per `~/.claude/rules/pr-format.md`; Summary mentions both the tests and the nested-plan scaffold.

## Post-merge

1. Pull `main` in each active worktree.
2. Query SonarCloud overall-coverage metric via API; compare to 7.3% baseline and 25-30% Tier-1 target. Report the actual delta.
3. Observe the cascade: `auto-pr-dev.yml` opens main->development PR; `deploy-dev.yml` opens dev->staging after validate; `staging->production` stays manual. Report any stall; do not auto-resolve.
4. Update [2026-04-20-phase-b6-tier1.md](./2026-04-20-phase-b6-tier1.md) (Level 4 parent): mark T6 MERGED (PR #<n>); mark Tier 1 COMPLETE; set `Active child:` to n/a.
5. Update [LAYER-2-PHASE-B-6-PLAN.md](./LAYER-2-PHASE-B-6-PLAN.md) (Level 3): mark Tier 1 COMPLETE; move `Active child:` pointer to "Draft Tier 2 plan".
6. Update [LAYER-2-PHASE-B-PLAN.md](./LAYER-2-PHASE-B-PLAN.md) (Level 2), [LAYER-2-PLAN.md](./LAYER-2-PLAN.md) (Level 1), [PROGRAM.md](./PROGRAM.md) (Level 0) status-summary lines to reflect the new position.
7. Save emerging feedback (e.g. cascade observations) to memory if surprising.

## Verification

**Local gate (before Stop C):**

```bash
cd W:/patient-portal/test-locations-coverage
DOTNET_ENVIRONMENT=Development ASPNETCORE_ENVIRONMENT=Development \
  dotnet build HealthcareSupport.CaseEvaluation.slnx -c Release -warnaserror   # 0/0
DOTNET_ENVIRONMENT=Development ASPNETCORE_ENVIRONMENT=Development \
  dotnet test HealthcareSupport.CaseEvaluation.slnx -c Release --no-build      # all prior + 13 new pass
dotnet format --verify-no-changes                                              # exit 0
```

**CI gate (before Stop E):** all 23 required checks green (same set as PR #129). CodeQL may show `NEUTRAL` on the no-op top-level run; that is normal.

**Navigation verification:** walking UP from this plan via `Parent:` reaches `PROGRAM.md` without a dead end; walking DOWN from `PROGRAM.md` via `Active child:` pointers reaches this plan.

## Risk / Rollback

- Test-only PR + docs-only plan additions. No production code touched. Blast radius: test surface + plan index.
- Rollback: `git revert <squash-sha>` on `main`. No data migrations, no feature flags, no backfills.
- Seed contributor conflict risk: low. No other open PR edits `CaseEvaluationIntegrationTestSeedContributor.cs`.
- IMultiTenant isolation assertions depend on `CurrentTenant.Change` behaviour being correct. Validated by existing Doctor + Patient tests.

## Out of scope

- Tier 2, Tier 3, Phase C, Layer 3, Layer 4 -- each drafts its own plan when the prerequisite completes.
- FEAT-10 (`WithCurrentUser` helper) / FEAT-14 (SQLite FK enforcement) -- tracked separately in `docs/issues/INCOMPLETE-FEATURES.md`.
- Dedicated TenantB-scoped IdentityUser for Attorney2 -- reusing `DefenseAttorney1UserId` (TenantA-scoped) as the FK target is acceptable; FK integrity at the DB level is tenant-agnostic.

## Success criteria

- 13 new test methods pass locally + in CI.
- PR title + body per format rules; all 23 required CI checks green.
- Level 0-5 plan files present; `Parent:` / `Active child:` pointers navigable both ways.
- Post-merge: SonarCloud coverage delta reported (actual vs 7.3% baseline vs 25-30% target); parent level indexes updated; active-child at Level 3 points to "Draft Tier 2 plan".
- Zero branches deleted without explicit approval; zero unplanned file edits outside enumerated paths; zero deviations from T6 canonical spec without a stop-and-ask handoff.
