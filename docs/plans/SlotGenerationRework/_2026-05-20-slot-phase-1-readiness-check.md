---
status: resolved
issue: slot-phase-1-readiness-check
owner: AdrianG
created: 2026-05-20
resolved: 2026-05-20 (Q1=A, Q2=A, Q4=A locked; D1-D5 baked into
  plan body; D6 left as in-plan reminder for implementer)
approach: code (research-only; updates plan body in a follow-up)
sequence: drift check for `2026-05-15-slot-rework-phase-1-schema.md`
depends-on: _2026-05-20-doctor-invariant-readiness-check.md (Plan 1
  must ship before this; this readiness check assumes Q1=C, Q2=A,
  Q3=C-then-A from that doc are already locked)
---

# Phase 1 (schema) readiness check

## Goal

Compare the Phase 1 plan against the current source on
`feat/replicate-old-app` and surface drift, contradictions, or
new questions. Output: a punch list of plan-body edits and a
small set of decisions to lock before implementation starts.

This check does NOT modify the plan body. Once the questions
below are answered, a follow-up edit pass updates the plan.

## What was verified

| Cited location | Status | Notes |
|---|---|---|
| `src/.../Domain/DoctorAvailabilities/DoctorAvailability.cs` (45 lines) | MATCHES `from` state | `Guid? AppointmentTypeId` present; no `Capacity`; no `AppointmentTypes` collection; no `Check.Range`. |
| `src/.../Domain/DoctorAvailabilities/DoctorAvailabilityManager.cs` (46 lines) | MATCHES `from` state | `CreateAsync` and `UpdateAsync` take `Guid? appointmentTypeId`. |
| `src/.../Domain/DoctorAvailabilities/DoctorAvailabilityWithNavigationProperties.cs` (13 lines) | MATCHES `from` state | Has single `AppointmentType? AppointmentType`; no list. |
| `src/.../Domain/DoctorAvailabilities/IDoctorAvailabilityRepository.cs` (15 lines) | DRIFT | Plan says "Drop `appointmentTypeId` from four method signatures." Reality: only TWO signatures carry it -- `GetListWithNavigationPropertiesAsync` and `GetCountAsync`. `GetWithNavigationPropertiesAsync(Guid id, ...)` and `GetListAsync(...)` already lack it. See drift D1. |
| `src/.../EntityFrameworkCore/DoctorAvailabilities/EfCoreDoctorAvailabilityRepository.cs` (74 lines) | PARTIAL MATCH | Two methods carry `appointmentTypeId` (matches the interface). The `ApplyFilter(IQueryable<DoctorAvailabilityWithNavigationProperties>, ...)` overload uses `appointmentTypeId` in a `WhereIf`. Plan describes dropping this filter parameter. Aligned. |
| `src/.../EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs` line 189-200 | MATCHES `from` state | Single-FK `HasOne<AppointmentType>().WithMany().HasForeignKey(x => x.AppointmentTypeId).OnDelete(SetNull)` exactly where plan expects. No `Capacity` config. |
| `src/.../Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` lines 264-275 (preview slot construction) | MATCHES | `AppointmentTypeId = item.AppointmentTypeId` literal at line 267. Plan's "set `AppointmentTypeIds = item.AppointmentTypeIds`" replacement applies. |
| `src/.../Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` lines 374-411 (`GetDoctorAvailabilityLookupAsync`) | PARTIAL DRIFT | Line 404 carries the loose-or-strict `Where` exactly as plan quotes. BUT the line is INSIDE an `if (input.AppointmentTypeId.HasValue)` block at line 398 -- it is NOT a top-level filter. The plan's "becomes" replacement code needs to substitute within the conditional. See drift D2. |
| `src/.../Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` lines 188, 210 (Create/Update calls to manager) | MATCHES | `_doctorAvailabilityManager.CreateAsync(input.LocationId, input.AppointmentTypeId, ...)` and the same on `UpdateAsync`. Plan's parameter-list update applies. |
| `src/.../Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` lines 55-56 (GetListAsync) | NEW CALL SITE | Plan does not enumerate this site, but it passes `input.AppointmentTypeId` into BOTH `GetCountAsync` and `GetListWithNavigationPropertiesAsync`. With the interface change, these need updating too. See drift D3. |
| `src/.../Application/CaseEvaluationApplicationMappers.cs` line 229-241 (DoctorAvailability mappers) | MATCHES `from` state | Bare partial classes with `Map` only. Plan's step 10 adds `[MapProperty]` -- correct delta. |
| 13 DTOs in `src/.../Application.Contracts/DoctorAvailabilities/` | MATCHES | All exist; current shape has `Guid? AppointmentTypeId`, no `Capacity`. All exactly match the plan's "from" assumptions. |
| Migrations folder | NO COLLISION | Most recent migration is `20260515183211_Added_Invitations`. No `Phase20_*`. Plan's name choice is safe. |

## Drift items

### D1 -- Plan overcounts repository method changes

**Plan body (section 6, line 767-775):** "Drop `appointmentTypeId`
from the four method signatures."

**Reality:** Only TWO interface methods carry it
(`GetListWithNavigationPropertiesAsync`,
`GetCountAsync`). `GetWithNavigationPropertiesAsync(Guid id, ...)`
and `GetListAsync(...)` already lack the parameter.

**Fix:** in the plan body, change "four method signatures" to
"two method signatures" and name them
(`GetListWithNavigationPropertiesAsync`, `GetCountAsync`).
Mirror update on the EF Core repository's two method bodies and
the shared `ApplyFilter` overload (the second `ApplyFilter`
overload already does not carry it).

**Risk:** zero -- this is documentation accuracy. The code delta
is smaller than the plan implies.

### D2 -- `GetDoctorAvailabilityLookupAsync` snippet is inside a HasValue guard

**Plan body (section 12, line 1108-1120):** quotes the loose-or-
strict line as if it were a top-level `query = query.Where(...)`.

**Reality:** the line is INSIDE
`if (input.AppointmentTypeId.HasValue) { ... }` at line 398-405.
The Phase 7 comment block above it explains the loose-mode parity.

**Fix:** in the plan body, replace the "from" snippet with the
actual conditional block and the "to" snippet with the same
block whose inner `Where` uses the collection check:

```csharp
if (input.AppointmentTypeId.HasValue)
{
    var typeId = input.AppointmentTypeId.Value;
    query = query.Where(x =>
        !x.AppointmentTypes.Any()
        || x.AppointmentTypes.Any(at => at.AppointmentTypeId == typeId));
}
```

Semantic is preserved.

**Risk:** zero if surfaced now; HIGH if missed because a naive
edit would drop the conditional entirely and break the lookup
when callers omit `AppointmentTypeId`.

### D3 -- Plan misses the `GetListAsync` call site (lines 55-56)

**Plan body (section 12):** enumerates `GeneratePreviewAsync`,
`GetDoctorAvailabilityLookupAsync`, `CreateAsync`, `UpdateAsync`.
Does NOT mention `GetListAsync` (the paged list AppService
method).

**Reality:** lines 55-56 pass `input.AppointmentTypeId` (still
on the `GetDoctorAvailabilitiesInput` DTO) into BOTH
`GetCountAsync` and `GetListWithNavigationPropertiesAsync`.

**Fix:** the plan already drops `AppointmentTypeId` from
`GetDoctorAvailabilitiesInput` (section 9e). Once that DTO field
is gone, lines 55-56 will fail to compile -- the compiler will
catch it. But the plan should explicitly list this call site so
the implementer knows ahead of time. Options:

- **D3a** -- drop the admin-list `appointmentTypeId` filter
  entirely (plan's stated intent in section 5, line 762-766:
  "the admin list filter does not need it"). Lines 55-56 simply
  stop passing the argument.
- **D3b** -- add an overlap-check filter (slot's set contains the
  requested type) so the admin list keeps the filter affordance.

Plan's section 5 already commits to **D3a**. Make it explicit.

**Risk:** zero (the compiler catches it).

### D4 -- Mapper attribute drift between code and convention

**Convention (per `src/.../Application/CLAUDE.md`):** use
`[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]`.

**Reality:** existing `DoctorAvailability*` mappers use bare
`[Mapper]` (line 229, 236 of `CaseEvaluationApplicationMappers.cs`).

**Decision needed:** when editing those mappers to add
`[MapProperty]`, do we also upgrade the `[Mapper]` attribute to
the conventional form? Effect: enforces target-side mapping
completeness; fails the build if any target property has no
matching source. Could surface latent bugs but could also block
the PR.

**Recommendation:** leave the `[Mapper]` form alone in this PR.
Filing a separate cleanup PR to apply the convention across all
mappers avoids mixing concerns. Note in the plan body.

### D5 -- Pre-flight section (obj/ race) is now superseded

**Plan body, "Pre-flight: fix the docker bind-mount obj/ race"**
section near the top: declares a hard pre-flight step to
relocate `obj/` per project. Plan's "Decision 11" locks this in.

**Reality:** the Doctor invariant readiness check resolved Q3 as
"C then A" -- coordinate with the other session about parallel-
worktree docker, then default to dropping the pre-flight bundle
if they touch `Directory.Build.props` or relocate `obj/`.

**Fix:** the plan body's pre-flight section should be reworded
to:

- Reference the readiness-check decision Q3=C-then-A.
- Restate that the pre-flight is OPTIONAL and depends on the
  other session's outcome.
- If the other session ships first: no pre-flight needed here;
  this plan re-verifies `docker compose up -d --build` reaches
  port 44368 and 44327 within 3 minutes on a cold tree.
- If they do NOT ship first: this plan's pre-flight section
  applies as written.

**Risk:** medium -- if both sessions independently relocate
`obj/`, the second one's PR will conflict. Coordination must
happen before the SECOND session starts the relocation.

### D6 -- Plan's FK name guess for `DropForeignKey`

**Plan body (section 8, line 916-919):** hardcodes the FK name
as `FK_AppEntity.DoctorAvailabilities_AppEntity.AppointmentTypes_AppointmentTypeId`
and says "confirm by running `sp_fkeys` before editing this
migration."

**Reality:** I cannot run `sp_fkeys` against the local DB right
now (docker stack unavailable). The Phase 1 plan already names
this as a risk and includes the verification step. No drift in
the plan body; surfaced here as a reminder for the implementer.

## Open questions (need decisions)

### Q1 -- `DoctorAvailability` constructor parameter order

The plan's new constructor signature is:

```csharp
public DoctorAvailability(
    Guid id,
    Guid locationId,
    DateTime availableDate,
    TimeOnly fromTime,
    TimeOnly toTime,
    BookingStatus bookingStatusId,
    int capacity = 1)
```

Note: `appointmentTypeId` is REMOVED from the constructor; the
plan moves it to a post-construction collection seed via
`AddAppointmentType`. Manager's `CreateAsync` does the seeding.

**Existing usage outside the manager:**

- Tests in `EfCoreDoctorAvailabilityRepository_Tests` may
  construct `DoctorAvailability` directly. With the constructor
  signature change, those tests break.

**Options:**

- **A** -- Keep `capacity` as a defaulted last parameter; any
  call site that previously passed positional `appointmentTypeId`
  breaks at compile time. Fix: grep+replace.
- **B** -- Mark the old ctor `[Obsolete]` and forward to the new
  one with `appointmentTypeIds: new List<Guid>()`. Allows a
  multi-PR rollout but bloats the entity.

**Recommendation:** **A**. The signature change is small, the
compiler surfaces every site, and Mapperly does not generate
constructor calls.

### Q2 -- Backfill semantics for soft-deleted slots

**Plan body (migration `Up()`, line 902-910):**

```sql
INSERT INTO [AppEntity].[DoctorAvailabilityAppointmentType] ...
WHERE da.[AppointmentTypeId] IS NOT NULL
  AND da.[IsDeleted] = 0;
```

The plan EXCLUDES soft-deleted slots from the backfill. If a
soft-deleted slot is later restored (un-`IsDeleted`-ed), its
`AppointmentTypeId` is gone and the slot lands in loose mode
("any type accepted").

**Options:**

- **A** -- Backfill ALL rows (including `IsDeleted = 1`). Preserves
  any future un-soft-delete operation's semantics exactly.
- **B** -- Backfill only live rows (plan's current text). Soft-
  deleted slots silently convert to loose mode on restore.
- **C** -- Backfill live rows + record a count of skipped soft-
  deleted slots in the migration log so operators can audit
  later.

**Recommendation:** **A**. The join table inherits the parent's
soft-delete filter via `HasQueryFilter(x => !x.DoctorAvailability.IsDeleted)`
(plan section 7, line 816), so backfilled rows on soft-deleted
slots stay hidden. The cost of A is one extra row per soft-
deleted slot; the benefit is that "restore" stays lossless.

### Q3 -- Capacity for backfilled rows

**Plan body (line 853-859):** adds `Capacity` column with
`defaultValue: 1`. Existing rows get `1`.

**Verification:** the OLD app's slot generator emits one row per
appointment-type per time block (per
`_slot-generation-deep-dive.md`). Today every existing slot is
"one appointment per slot" by construction. So `1` is correct
backfill semantics.

**No decision needed**; flagging as a sanity-check.

### Q4 -- Mapperly source-gen for `ICollection -> List<Guid>`

**Plan body (section 10, line 1063-1067):** suggests a manual
converter `MapAppointmentTypeIds`. Mapperly's source-gen often
handles `ICollection<X>.Select(x => x.Y).ToList()` via inline
expressions when the property names line up; an explicit
converter is rarely required.

**Options:**

- **A** -- Try Mapperly's default `[MapProperty]` first; only add
  the manual converter if the source-gen warning appears.
- **B** -- Always include the manual converter (plan's current
  text). Defensive, slightly more code.

**Recommendation:** **A**. Mapperly is usually smart enough for
this shape; fall back to the converter on first build failure.

## Decisions to lock before implementation

| # | Question | Recommendation |
|---|---|---|
| Q1 | Constructor parameter order | A -- breaking change, compiler surfaces sites |
| Q2 | Backfill soft-deleted slots | A -- include them, preserve restore semantics |
| Q4 | Mapperly converter | A -- try default first |

Q3 needs no decision; D5 depends on coordination with the other
session.

## Risk re-rating

**Plan body lists three risks** (FK name mismatch, test
references, Mapperly source-gen). All still apply.

**New risk surfaced by this check:** D2's conditional-block
gotcha. If the implementer naively swaps the `Where` line
without preserving the `if (input.AppointmentTypeId.HasValue)`
guard, callers that omit `AppointmentTypeId` lose the
loose-mode behavior. Mitigation: D2's "Fix" snippet above.

**Other-session coordination (Q3-C-then-A):** if the other
session and this PR both modify `Directory.Build.props` or
relocate `obj/`, the SECOND PR conflicts. Mitigation: ask the
other session whether they're touching the obj/ race fix
BEFORE this PR is opened.

## Workflow

This readiness check informs (does not modify) the Phase 1 plan
body. Once decisions Q1, Q2, Q4 are locked and D1-D6 are
agreed, a follow-up edit pass updates
`2026-05-15-slot-rework-phase-1-schema.md` to bake in those
changes. Then the plan is ready to execute as soon as the
Doctor invariant plan (Plan 1) ships and the docker stack
returns.

## How to apply

1. Read this doc.
2. Decide Q1, Q2, Q4 (recommendations: A, A, A).
3. Coordinate with the other session on Q3 / D5.
4. Edit the Phase 1 plan body to incorporate D1-D6 and the
   locked decisions.
5. Mark this readiness check `status: resolved`.
