---
status: research-output
parent-plan: 2026-05-15-doctor-invariant-enforcement.md
created: 2026-05-20
owner: Adrian
---

# Doctor-invariant plan — readiness check (research only)

The plan at `docs/plans/SlotGenerationRework/2026-05-15-doctor-invariant-enforcement.md` was written 2026-05-15. Today is 2026-05-20. This doc verifies every file path, line range, and dependency the plan cites against current `feat/replicate-old-app` source (post-PR-#207 merge).

**Bottom line:** the plan is still implementable as written, but six points need a small refresh before code starts. No fundamental rework. No code execution was needed — this is a pure read pass against source.

---

## What the plan got right (still valid)

| Plan claim | Current state | Verdict |
|---|---|---|
| `DoctorsAppService.cs:106-110` is bare `DeleteAsync(id)` | Confirmed at lines 106-110 verbatim | ✅ |
| `DoctorsAppService.cs:112-117` is unguarded `CreateAsync` | Confirmed at lines 112-117 verbatim | ✅ |
| `DoctorTenantAppService.cs:121-141` is the canonical net-new path with replay-on-conflict | Confirmed at lines 121-141 — `CreateDoctorProfileAsync` finds-or-updates via `_doctorRepository` directly, bypassing the AppService | ✅ |
| `CaseEvaluationDbContext.cs:122-132` is the Doctor entity config block | Confirmed at lines 122-132 — including the `OnDelete(DeleteBehavior.SetNull)` on `TenantId` FK | ✅ |
| `Phase11f_AppointmentConfirmationNumberUniqueIndex.cs` is the reference filtered-index migration | Exists at `Migrations/20260504170956_Phase11f_AppointmentConfirmationNumberUniqueIndex.cs:18`, uses `filter: "[TenantId] IS NOT NULL"` | ✅ — extend to `[TenantId] IS NOT NULL AND [IsDeleted] = 0` for our case |
| `PARITY-FLAG-NEW-006` is the decision driving this work | Confirmed: marked `resolved` in `_parity-flags.md:23` but implementation pending per `staff-supervisor-doctor-management.md:166` | ✅ |
| No `Phase19` / `Phase20` migration collision | Latest migration is `20260515183211_Added_Invitations`. Open. | ✅ |
| The AbpExceptionHttpStatusCodeOptions block exists | Confirmed at `CaseEvaluationHttpApiHostModule.cs:151-194` (grown today via BUG-023/24 work — 7 entries now). Easy to extend. | ✅ |

---

## What the plan got wrong (six small refresh points)

### 1. Test file naming

> Plan: "`test/HealthcareSupport.CaseEvaluation.Application.Tests/Doctors/DoctorsAppServiceTests.cs` — Existing test class. Add five new `[Fact]` tests..."

**Reality:** the file is `DoctorApplicationTests.cs`. The **class** inside is named `DoctorsAppServiceTests<TStartupModule>` (abstract generic — ABP Suite scaffold pattern). New tests must be added to a concrete subclass (or inlined in `DoctorApplicationTests.cs`).

**Refresh needed:** swap the filename in the plan + check whether tests should land in the abstract base or in a concrete subclass following the ABP Suite pattern. Read other concrete `*ApplicationTests.cs` files for the pattern.

### 2. `CaseEvaluationDomainErrorCodes.cs` insertion point

> Plan: "Append two new public const strings near the existing Doctor-area codes (line 59-84 region)"

**Reality:** the file has zero existing `Doctor` error codes. The "line 59-84 region" doesn't exist as a Doctor section. The file is now 607 lines (grew today with `InternalUserTenantMismatch` + `AppointmentInvalidTransition`).

**Refresh needed:** pick a new insertion point — either at the end (next to `AppointmentInvalidTransition` added today) or after the `Account:` codes around line 116. No semantic change, just a docstring update in the plan.

### 3. `en.json` insertion point

> Plan: "Place under the existing `Doctor` section (alphabetical — they fall between any current `Doctor:*` keys)"

**Reality:** no existing `Doctor:*` keys in `en.json`. We're establishing the namespace.

**Refresh needed:** clarify in the plan that there is no existing Doctor section; the two new keys are net-new.

### 4. `DoctorLocation` is missing from the dependent-bucket list

> Plan: "throw when the tenant has any downstream data (`DoctorAvailability`, `Appointment`, `DoctorPreferredLocation`, `DoctorAppointmentType` rows still pointing at the doctor or living inside the tenant)"

**Reality:** there are **two** host-scoped M2M tables on Doctor:
- `DoctorAppointmentType` (line 133 in DbContext) — covered by the plan
- `DoctorLocation` (line 146 in DbContext) — **NOT covered by the plan**

Both have `OnDelete(DeleteBehavior.Cascade)` from Doctor and `HasQueryFilter(x => !x.Doctor.IsDeleted)`. When the Doctor soft-deletes, the join filter would hide these rows from app-level queries — but the rows physically remain pointing at the (soft-deleted) Doctor.

**Decision (2026-05-20, Adrian):** drop BOTH `DoctorLocation` and `DoctorAppointmentType` from the probe. The purpose of the dependent-bucket check is to prevent **operational data orphaning**; host-scope M2M metadata is filtered out of every app query via `HasQueryFilter` already, so blocking delete on it adds friction with no integrity payoff. Final bucket list: `DoctorAvailability`, `Appointment`, `DoctorPreferredLocation` (active-only — see point 5).

### 5. `DoctorPreferredLocation.IsActive` filter on the dependent count

> Plan: `_doctorPreferredLocationRepository.CountAsync(x => x.DoctorId == id)`

**Reality:** `DoctorPreferredLocation` has an `IsActive` toggle and is "never hard-deleted in the normal flow" — IT Admin / Staff Supervisor flips `IsActive` on/off via a `ToggleAsync` upsert. The plan's count would catch `IsActive=false` rows and block Doctor deletion even when the doctor has deactivated every location preference.

**Decision (2026-05-20, Adrian):** filter by `x.DoctorId == id && x.IsActive`. The `IsActive=false` rows are audit-preserved historical state, not operational. Counting them would create a "soft-delete forever blocked" trap because the entity is never hard-deleted in the normal flow (`ToggleAsync` only flips IsActive — there's no app path to physically remove a row).

### 6. References to a now-deleted plan

The plan itself references items in the slot-rework chain (Phases 2-7). It's still valid — those plans exist. But:

> Plan section 5b says "this is the pattern used in `20260504170956_Phase11f_AppointmentConfirmationNumberUniqueIndex.cs` (read that migration first to confirm syntax)."

Confirmed — the reference migration still uses the exact pattern, just with a simpler one-condition filter. The compound `AND [IsDeleted] = 0` is fine; SQL Server supports it; EF Core's `HasFilter("...")` honors arbitrary SQL.

---

## New questions worth surfacing

1. **Decision (2026-05-20, Adrian, Q3 Option C-then-A):** the other session is doing per-worktree docker container isolation (problem 3 in the trichotomy below). That's orthogonal to the intra-stack `obj/` race the slot-rework Phase 2 plan bundles a fix for (problem 2). When the other session lands, **first try coordinating** — ask whether their work touches `Directory.Build.props` or relocates `obj/`. If yes, the Phase 2 bundle is moot. **Default fallback if no easy answer**: drop the Phase 2 pre-flight bundle, ship Phase 2 narrow, smoke-test for `obj/` contention, add a follow-up only if it surfaces. This decision affects Phase 2 scope only; Phase 1 (this plan) is unaffected.

   Three-way distinction worth keeping straight:
   - **OBS-22**: Docker's inotify shim on Windows drops file-change events → `dotnet watch` misses edits → manual `docker compose restart` is the workaround.
   - **Phase 2 pre-flight**: api + authserver containers bind-mount the same `./src/` and race on `obj/` writes → fix is to redirect intermediate output outside the bind-mount via `Directory.Build.props`.
   - **Parallel worktree containers** (other session): two worktrees can't both run `docker compose up` because they collide on container names + ports → fix is `COMPOSE_PROJECT_NAME` + port offsets.

   All three look like "the watcher isn't working" from the outside but have distinct mechanisms and fixes.

2. **Dedupe-probe execution gate.** Before running the migration, the plan wants this SQL run against the live dev DB:
   ```sql
   SELECT TenantId, COUNT(*) AS DoctorCount
   FROM AppEntity.Doctors
   WHERE IsDeleted = 0 AND TenantId IS NOT NULL
   GROUP BY TenantId
   HAVING COUNT(*) > 1;
   ```
   Can't run it now (containers tied up). When the docker work clears, this is the first thing to run — if it returns rows, code work is blocked until you manually dedupe. Expectation: zero rows (Falkinstein is the only tenant; only one Doctor was ever seeded).

3. **Branch strategy.** Plan says "create a new branch off `feat/replicate-old-app`. PR back to `feat/replicate-old-app`." Recommend: create `feat/doctor-invariant` off `feat/replicate-old-app`. Sub-branch makes Phase 1 reviewable in isolation. This deviates from how Block 2 worked (direct commits to feat/) — but Block 2 was firefighting; this is a clean planned slice.

4. **Permission scoping check.** Plan adds the guards to `DoctorsAppService.CreateAsync`/`DeleteAsync`. The class is `[Authorize(CaseEvaluationPermissions.Doctors.Default)]` and the methods carry `.Create` / `.Delete`. Tenant admin (Volo SaaS static admin role) gets these by default. Staff Supervisor likely also has them via the existing `InternalUserRoleDataSeedContributor.cs` `StaffSupervisorGrants()` — worth a 30-second cross-check before coding so we know who hits the friendly error vs the 403.

---

## Recommended workflow when the other session unblocks us

1. **Refresh the plan doc** to fix the 6 small drift items above. (5-10 minutes.)
2. **Resolve the two design questions** (DoctorLocation in/out; DoctorPreferredLocation IsActive filter on/off). (Adrian decision.)
3. **Create sub-branch `feat/doctor-invariant` off `feat/replicate-old-app`.**
4. **Run the dedupe-probe SQL** against the local dev DB. Halt if non-zero.
5. **TDD the AppService guards** (5 active `[Fact]` + 1 `[Skip]` per the plan). Add tests first; watch them fail; add guards; watch them pass.
6. **Add the error codes + en.json keys + HTTP 400 mappings.**
7. **Add the DbContext `HasIndex` + run `dotnet ef migrations add Phase19_DoctorOnePerTenantUniqueIndex`.** Verify the generated migration's `filter:` clause includes the `IsDeleted = 0` predicate; hand-edit if EF Core strips it.
8. **Run `dotnet ef database update` against the dev DB.** Migration must succeed (dedupe probe already confirmed no duplicates exist).
9. **Manual UI verification** per the plan's section 7 ("Verification" steps 1-13).
10. **Open PR `feat/doctor-invariant` → `feat/replicate-old-app`.** Squash-merge after self-review.

Total effort estimate: 1-2 working days (matches the status report's Stage 1 #1 estimate).

---

## Risk re-rated against current state

| Original risk | Current rating | Reason |
|---|---|---|
| Migration fails on existing duplicate Doctor rows | Low | Falkinstein is the only seeded tenant; only one Doctor was ever provisioned. Probe will confirm. |
| ABP soft-delete + filtered-index interaction | Low | Reference migration (`Phase11f`) uses the same pattern successfully. Test #2 in the plan exercises it. |
| Future tenant-provisioning path bypasses the guard | Low | `DoctorTenantAppService.CreateDoctorProfileAsync` calls the repository directly, bypassing the AppService. Already correct. |
| `DoctorLocation` host-scope M2M leaks orphans on Doctor soft-delete | Resolved | Drop both host-scope M2M from the probe; HasQueryFilter already hides orphans (Adrian decision 2026-05-20, Q1 Option C). |
| `DoctorPreferredLocation.IsActive=false` rows block deletion forever | Resolved | Filter the count by `x.IsActive == true` (Adrian decision 2026-05-20, Q2 Option A). |
| Other session's docker work might already fix the obj/ race | Plan-affecting | Decision is to coordinate-then-drop the Phase 2 bundle if no easy answer (Adrian decision 2026-05-20, Q3 Option C-then-A). Affects Phase 2 scope only. |

All three open items resolved 2026-05-20. No new high or medium risks since plan-write. Phase 1 is unblocked from a design standpoint; remaining gate is docker availability.

---

## What I did NOT do (waiting on docker)

- Did not run the dedupe-probe SQL (no DB access this session)
- Did not start `dotnet ef migrations add` (would touch state)
- Did not run the test scaffolding probe (would build code)
- Did not look for the OBS-22 fix's branch name (no visibility into the other session)

These are the first executable steps once the docker work clears.
