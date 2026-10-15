# Phase 3 -- Critical-path coverage

**Change class:** behaviour preservation. **Characterization tests FIRST.** These tests assert what
the system does today, not what it should do. If current behaviour turns out to be wrong, that is a
finding for the triage log and a separate fix -- do not quietly "correct" it while writing the test,
or the test stops being a safety net and becomes an opinion.

**Why this phase exists and why it sits here.** It is the direct answer to Adrian's question about
regressions. Phases 4-8 change code across the whole repo; phase 6 bumps 87 npm packages whose
failure modes are unknown in advance. There is no targeted test for "this bump broke nothing" --
only a suite catches that. This phase builds the net before the trapeze work starts.

**This phase is NOT about the coverage percentage.** Chasing the number is phase 8. This is about
five specific paths where a silent regression is dangerous rather than annoying.

---

## Scope -- the five dangerous paths

Chosen because a regression is either a security failure, a privacy failure, or a legally
consequential data error. Ordered by blast radius.

### 3.1 Tenancy resolution

Tenancy resolves from the HTTP Host header and nothing else, and each office is a separate
database. A regression here means cross-office data exposure -- the worst outcome the system has.

Cover: host-to-tenant resolution including unknown and malformed hosts, the host-aware resolver's
fallback, and that a resolved tenant actually selects the matching connection string.

Anchors exist: ADR `006-subdomain-tenant-routing` and `007-host-aware-tenant-resolver`.

**Admitted from the system design research (2026-08-31), and this is now the highest-priority item
in the phase.** Evidence and full reasoning in [10-research-corrections.md](10-research-corrections.md)
section 1.

The research's single highest-priority concern was that ABP's default `__tenant` resolvers might
outrank the hostname. Reading source settles it: both processes call `TenantResolvers.Clear()` before
registering, leaving two resolvers and no `__tenant` resolver in either
(`CaseEvaluationHttpApiHostModule.cs:404`, `CaseEvaluationAuthServerModule.cs:524`).

**But that is the registration, not a runtime assertion**, and the inference that `Clear()` runs
after the framework's defaults are added is exactly the class of reasoning this epic distrusts. Three
tests, all characterization - they assert what the system already does:

- **APP-OWN-01.** Assert the assembled `AbpTenantResolveOptions.TenantResolvers` contains exactly
  `CurrentUserTenantResolveContributor` then `HostAwareDomainTenantResolveContributor`, in that
  order, **in both processes**. This is the test that makes the whole boundary argument real rather
  than inferred, and it fails loudly if a future framework upgrade re-adds a default.
- **APP-OWN-02.** A request with an empty `Host` field value must not reach tenant resolution. The
  contributor abstains when `!Request.Host.HasValue` (`:69-72`); with the chain cleared that resolves
  to host context rather than an attacker-chosen tenant, which is survivable but should be asserted
  rather than assumed. An empty host value is legal under RFC 9112 s3.2 and Kestrel accepts it.
- **APP-OWN-03.** A token minted at office A presented to office B's API.
  `CurrentUserTenantResolveContributor` is **first**, so an authenticated request resolves tenancy
  from the token claim and **the hostname is not authoritative**. Establish what the current
  behaviour is before deciding whether it is a defect - if nothing downstream trusts the hostname for
  authorisation, this is correct rather than broken.

Two properties worth pinning while writing these, because both were mis-stated in the research and
both are cheap to lose in a refactor: `ExtractSlug` **strips the port** (`:98-99`) and **validates
against the configured domain format**, rejecting any slug containing a dot (`:110-111`, `:124`).

### 3.2 Authorization and permissions

Deny-by-default is the stated rule. Cover the permission checks on the endpoints that expose
patient data, and specifically the four external roles, which are capability-identical by design --
a regression that differentiates them is a privilege change nobody would notice by reading a diff.

Known constraint: inherited ABP identity app services cannot be re-gated, so test the boundary the
repo actually controls.

### 3.3 PHI egress

Every path where PHI leaves the system: packet generation, notification emails, the SSN reveal
(audited), and document download. A regression here is a HIPAA event, not a bug.

Note the standing hazard recorded during the Pacific epic: packet generate/regenerate fans out to
every party and attaches documents. Tests must not send real mail -- assert against the outbox with
`NotificationsPolicy.EmailEnabled` false.

### 3.4 Packet generation

The packet is the legal artefact. Cover: which documents are included per role, the per-role access
rules, and the rendered dates now that everything is Pacific-normalised.

### 3.5 Booking and capacity

Capacity-aware slot booking (ADR 008) plus the lead-time rules. A regression double-books a doctor
or silently drops a slot. Both are visible to customers and hard to unwind after the fact.

---

## Method

- **Characterization first.** Run the code, observe what it does, assert that. Feathers' approach:
  the test documents current behaviour so a refactor that changes it fails loudly.
- **Prefer tests that resemble real usage** over tests bound to implementation detail -- per
  `~/.claude/rules/testing.md`. Integration-weighted for Angular, unit-heavy for backend logic.
- **Verify substituted dependencies against reality.** Two live examples from this repo: NSubstitute
  auto-mocks interface-returning members, so an unconfigured `.Current` is a stub rather than null;
  and `ICurrentTenant.Change(id)` sets the id but leaves `Name` null, which hid a blank office in
  both a UI column and an outbound email. A mock returning a plausible value proves nothing about
  production.
- **No PHI.** Synthetic data only, per `~/.claude/rules/hipaa.md`.

---

## Done bar

Not a percentage. This phase is done when, for each of the five paths, there is a test that fails
if the path's core guarantee is broken -- and each such test has been **seen to fail** by
deliberately breaking the guarantee once.

A test that has never been observed failing is an assumption, not a guard. That is the whole reason
`full-logout.spec.ts:47` (phase 1) counts as a blocker.

---

## WHAT LANDED -- 3.1 tenancy, 2026-09-04

**3.1 is COMPLETE.** The tenancy path went from nothing asserting it to pinned in both processes,
with every test seen to fail. Three task branches, merged into `feat/production-hardening`:

| Task | PR   | Merged     | What it pins                                                    |
| ---- | ---- | ---------- | --------------------------------------------------------------- |
| 1    | #686 | `a8e221df` | the resolver chain and its order, in the API AND the AuthServer  |
| 2    | #688 | `d1d70938` | port stripping, dot rejection, empty-host outcome                |
| 3    | #689 | `c9ad7de8` | office-A token at office B, characterization only         |

### The gap was FIVE assertions, not the three this document proposed

The 8 existing contributor tests were re-checked by the host strings they actually feed rather than
by their names. None carried a port, none was empty, none put a dot in the slug position. So the two
properties this document called "cheap to lose in a refactor" were also unasserted, and 3.1's real
workload was five.

### THE MOST IMPORTANT FINDING: task 1's first test COULD NOT FAIL

It was written green, 7 tests. Then the guarantee was deleted -- `options.TenantResolvers.Clear()`
removed from the API module, the exact regression the test's own docstring claimed to catch -- and
**all 7 still passed.**

The test built a bare `ServiceCollection`. `Clear()` on an empty collection is a no-op, so "exactly
two resolvers, in this order" held either way. **The test asserted what the code ADDS and was blind
to what it REMOVES.**

That is `full-logout.spec.ts:47` from phase 1, reproduced inside the phase created to eliminate it,
on the highest-stakes path in the codebase, by a session that had spent the day applying that rule to
other work. Fixed by seeding a named decoy resolver before the module's callback runs; the same break
then failed 3 tests by name.

**Standing rule adopted for the rest of the phase: "seen to fail" binds EVERY task, not only the
break-and-observe ones.** The done bar below was written for 3.2-3.5; it now applies to every test
this phase produces, whatever its approach flag.

**And the reflexive form, which is the generalisable half:** if a break does not fail, suspect the
FIXTURE before the assertion. An empty fixture makes a removal-guard vacuous.

### The empty-host test is an OUTCOME PIN, not a line guard -- deliberately

Four experiments established that no single-line deletion can fail it:

```text
delete the abstention alone           -> does not COMPILE (CS8604 under TreatWarningsAsErrors)
silence with ! and delete the guard   -> 12/12 STILL PASS  (ExtractSlug returns null for "" anyway)
make ExtractSlug yield a slug for ""  -> 12/12 STILL PASS  (the abstention returns first)
remove BOTH                           -> FAILS, alone, by name
```

Two independent mechanisms produce the same outcome, plus the compiler as a third. The test was
renamed from `abstains` to `selects_no_tenant` because the old name claimed a line guard it does not
provide. **It was NOT strengthened**: a test that detected the removal of one of two redundant
mechanisms would have to bind to implementation detail, and the guarantee -- "an empty Host must not
select a tenant" -- is what matters. Reporting "still safe" when one layer goes is correct behaviour.

**The compiler defence is contingent, not permanent.** It depends on
`Directory.Build.props:21 TreatWarningsAsErrors`, which that file records as having been flipped ON
deliberately in Phase B-6 after 480 nullability warnings were cleared. Two defences plus a setting,
not three.

### APP-OWN-03's open question is ANSWERED: nothing trusts the hostname for authorisation

The research left this deliberately unresolved and forbade a test from settling it. It was settled
separately, afterwards, by reading every production site that touches the request host:

```bash
git grep -n "Request\.Host" -- 'src/**/*.cs' | grep -v HostAwareDomainTenantResolveContributor
```

Two hits in the entire production tree, neither a decision: a **log message** argument in
`Pages/Account/Logout.cshtml.cs:56`, and URL composition in `Pages/Index.cshtml.cs:67`.

**So the characterized behaviour is CORRECT, not a defect.** Because tenancy resolves from the token,
an authenticated caller always operates in their own office regardless of the URL they visit, and
cannot reach another office's data by changing the hostname. **The reverse ordering would have been
the dangerous one** -- host-first would put an office-A user into office B's tenant context on
visiting office B's URL, leaving only "is authenticated" between them and another office's PHI.

That URL composition is **not** an open redirect: `TenantUrlComposer.ComposeForTenant` captures the
host from the CONFIGURED base URL and prepends a single label to it, so the request's own domain
never reaches the output. Logged to the backlog with that characterization spelled out, because a
bare line reading "unvalidated host label in redirect composition" would be picked up as a
vulnerability and it is not one.

**A FALSE HIT TO EXPECT IF YOU RE-RUN THIS.** `git grep "Host.*Tenant"` returns a dozen `isHost`
lines in the app services. Those are `CurrentTenant.Id == null` -- ABP's ROOT TENANT, a different
sense of the word from the HTTP header, and the OUTPUT of the resolver chain rather than its input.

**Note the sequence, because it is the characterization rule paying for itself rather than acting as
ceremony:** task 3 pinned the behaviour without deciding whether it was right, and the verdict came
from a separate investigation afterwards. Had the test settled it, the answer would have been an
assumption dressed as an assertion, written by the party least able to be neutral about it.

### The coverage floor moved 73 -> 72, and the honest figure was nearly missed

Asserting the chain in BOTH processes needs a `ProjectReference` from `Application.Tests` to the
AuthServer. That loads the AuthServer assembly during the run, so its lines enter the backend
denominator **for the first time** -- the previous 73.61% silently omitted an entire process.

Measured on the task 1 branch, not on the earlier probe:

```text
backend  72.47%  (33255/45891 lines over 916 files)     <- 887 + 29 AuthServer files
floor    72       and the gate FAILS at 73 on the same artefact, so it is doing work
```

**The naive reading of the same report is 68.25% over 961 files, and a floor of 68 would have been
wrong and would have DRIFTED.** 45 of those files are MessagePack-CSharp source arriving through
SourceLink -- not ours, and it moves whenever MessagePack is bumped. Caught only because the probe
was run as a CONTROL plus a test, and the control reproduced this repo's own 73.61% exactly. A
single-run probe would have baked the contamination into the floor.

Excluded via `**/MessagePack-CSharp/**` in `.coverage-exclusions`, and proven surgical: applied to the
control artefact it changes nothing (73.61%, 887 files, unmoved). **Issue #683 tracks the class** --
a third named vendor is the problem, not the solution.

### Measured baseline, replacing this document's 2026-08-28 figures

```text
backend   2291 passed   16 skipped   2307 total   0 failed   936s
frontend   667 executed                           0 failed    62s
```

The old backend figure was accurate (+1 in a week, skips identical). **The attribute-versus-executed
confusion is resolved exactly, and the attribute count must never appear in a validation loop**,
because it cannot be compared to a run:

```text
[Fact]       1591
[InlineData]  716
             ----
             2307  == executed total, to the case, zero residual
```

`[Theory]` (150) contributes its `[InlineData]` rows rather than itself. The old 1,726 came from a
grep whose trailing `]` missed `[Fact(Skip = "...")]`. Frontend is the same shape: 667 executed
against 614 `it()` calls, because some specs are generated in loops.

### What 3.1 could not prove -- task 9, CLOSED 2026-09-08

**This heading read "task 9, still open" until 2026-09-08.** It was written by #712 and falsified
by #716 twenty-three minutes later, in the same session. Corrected rather than silently rewritten,
because the pattern is the point: an entry can be accurate when written and wrong once your own work
changes what it described, and it goes on reading plausibly enough that nobody re-checks it.

The decoy proves `Clear()` removes what is present. **It does not prove ABP's defaults ARE present at
that moment**, because the decoy is seeded by construction -- which assumes the very ordering the
research flagged as the class of reasoning this epic distrusts.

**That half is now closed.** `BootedTenantResolverDefaultsTests.cs` (#716, merged to the epic as
`eb3850c7`) boots a real application and reads the assembled options. Measured 2026-09-08, a graph
that does NOT call either host module's `ConfigureMultiTenancy` contains exactly one framework
default:

```text
count=1; [CurrentUser=CurrentUserTenantResolveContributor]
```

Nothing in the harness seeds it -- neither `CaseEvaluationTestBaseModule` nor
`CaseEvaluationMultiOfficeTestModule` mentions `TenantResolvers` or `AbpTenantResolveOptions`. So the
framework does contribute a resolver of its own accord, and `Clear()` in the host modules removes
something real rather than being decorative. **That is the fact task 1 could not exhibit.** The test
asserts the exact SET rather than the count, so a swap cannot pass it; it was seen to fail by
registering one extra resolver into the booted graph, and both of its facts failed independently.

**The remaining half is now closed too, by #725.** A booted graph carrying our own
`ConfigureMultiTenancy` was reached by extending `InternalsVisibleTo` to `EntityFrameworkCore.Tests`
-- the grant bundled with the test that needed it, so the ABSENCE of the grant failed the build
naturally (`CS0117` on both host modules, measured, not `CS0122` as predicted) rather than being
proven by temporarily mutating production source.

**The break did more than detect a change: it CONFIRMED the ordering.** With `Clear()` removed:

```text
should be ["CurrentUser", "HostAwareDomain"]
but was   ["CurrentUser", "CurrentUser", "HostAwareDomain"]
```

The framework's `CurrentUser` is still present and ours is appended AFTER it. That sequence is only
possible if the framework registered first and our callback ran second -- exactly the fact task 1 and
the first half of task 9 could each only half-establish. **The tenancy question 3.1 opened is now
fully answered.**

**An asymmetry that remains UNMEASURED:** `CaseEvaluationHttpApiHostModule.cs:69` declares
`AbpAspNetCoreMvcUiMultiTenancyModule`; the AuthServer declares no multi-tenancy module at all, and
its resolved graph is still UNKNOWN.

**An asymmetry that remains UNMEASURED:** `CaseEvaluationHttpApiHostModule.cs:69` declares
`AbpAspNetCoreMvcUiMultiTenancyModule`; the AuthServer declares no multi-tenancy module at all. Its
resolved graph is still UNKNOWN -- a declared `DependsOn` list is not a resolved module graph, and
three of its declared modules are plausible transitive carriers. **If the AuthServer's collection
comes back empty, the finding is "no framework resolvers reach this graph today, so `Clear()` is
defensive here", and nothing stronger.** Absent defaults is less to be protected FROM, not less
protection.

---

## Next: 3.2-3.5 are break-and-observe, not new suites

Each path already has a guardian test. The work is one guarantee, one test, one break: apply the
break to source, run that path's test, record whether it failed AND whether the failure NAMED the
guarantee rather than a downstream symptom, revert, confirm green.

| Path | Guardian                                     | Break to apply                                           |
| ---- | -------------------------------------------- | -------------------------------------------------------- |
| 3.2  | `ExternalUserRoleGrantsTests.cs:25-38`       | remove a permission from `BookingBaselineGrants()`       |
| 3.3  | `OutboxDrainServiceTests.cs:118`             | make the drain ignore `NotificationsPolicy.EmailEnabled` |
| 3.4  | `PacketVisibilityUnitTests.cs:26-67`         | **STRUCK -- see the correction below. The break named here is tautological.** |
| 3.5  | `MultiOfficeAtomicBookingSubmitTests.cs:134` | remove the transaction boundary around child writes      |

**3.2 carried a PREDICTION. It was RUN and it HELD** -- the result is recorded under
"3.2 -- the prediction was RUN, and it HELD" further down this file.

**This paragraph read "and it is still a prediction ... Run it before anyone records it as a fact"
until 2026-09-08**, by which point it had already been run and recorded seventy-three lines below.
Corrected rather than deleted, because the failure mode is worth keeping visible: a document that
sends a reader off to establish something it already answers costs that reader the entire errand, and
nothing about the stale sentence looked stale.

`ExternalUserRoleDataSeedContributor.cs:60` grants the booking baseline by looping a hardcoded
four-name array, and `ExternalUserRoleGrantsTests` pins the list's CONTENTS, not the ROLE SET.
Removing `"Defense Attorney"` from that array was predicted to fail no test, and failed none of
1,919. The accurate statement is "the role set is unasserted" -- NOT "roles are unprotected".

### THE 3.4 BREAK IN THE TABLE ABOVE IS STRUCK, and the reason generalises

**Corrected 2026-09-08, by measurement rather than by reading.** The prescribed break -- make
`AllowedKinds` return all three kinds for Doctor -- cannot do the job, for two independent reasons:

1. **Its premise no longer exists.** `PacketVisibility.AllowedKinds` has a Patient arm, a combined
   Applicant Attorney / Defense Attorney / Claim Examiner arm, and `Array.Empty<PacketKind>()` as
   the fallback. **There is no Doctor arm.** IR1 (2026-06-03) retired Doctor as an internal persona,
   so a Doctor-only caller already falls through to empty.
2. **It is the wrong direction.** This phase catches a guard being REMOVED. Adding a permissive
   branch is an addition, and the two are not symmetric.

Applied anyway as a control, it fails exactly one test: `PacketVisibilityUnitTests.cs:38-42`, the
unit test pinning the very line changed. **It proves the unit test tests the unit, and nothing else.**

```text
CONTROL, unmodified    Application 1127 passed / Domain 693 passed / MultiOffice 105 passed
BREAK A (the struck one)   Application Failed: 1 -- PacketVisibilityUnitTests.AllowedKinds_Doctor_ReturnsNone
                           Domain and MultiOffice unmoved
BREAK B (the real gap)     1127 / 693 / 105 -- ZERO failures, build clean
```

**BREAK B is the break this row should always have named.** `AppointmentPacketsAppService`
`GetListByAppointmentAsync` reads the allow-list at `:122` and filters with it at `:131`. **Delete
`:131` alone -- leaving the `:122` call so the helper still looks used -- and zero of 1,925 tests
fail.** Packet-kind filtering can be removed entirely and the suite stays green.

**The generalisable form, and it is the most valuable thing 3.4 produced:**

> A rule can be thoroughly tested where it is DEFINED and completely unasserted at the boundary
> where it is RELIED UPON. Coverage of the helper reads as coverage of the behaviour, and it is not.

This is a different shape from the catalogue's usual entry. It is not a test that cannot fail; it is
a test that guards the right thing one layer below where the guarantee actually matters.

**It is a class, not a coincidence -- a second instance surfaced the same afternoon.** `PacificTime
.TodayFrom` is covered by `Domain.Tests/Timing/PacificTimeTests.cs:115`, while `PacketTokenResolver`,
the only production caller, is executed by no test at all. See task 6.2 below.

**And the population it lives in was measured**, so nobody has to guess how far it reaches:

```bash
curl -s "https://sonarcloud.io/api/measures/component_tree?component=gesco-healthcare-support_hcs-patient-portal&metricKeys=coverage&ps=500&qualifiers=FIL"
# 20 of 52 files named *AppService* report 0.0% coverage -- 3,658 lines
```

**That is context, not a phase 3 work item.** Phase 8 already lists "application services with no
tests at all" as its second priority and instructs a successor to re-derive the list; this is that
list, derived. Phase 3 does not grow by discovery.

**The per-path file counts previously cited (17 / 74 / 9 / 13) are STRUCK.** They came from research
that recorded its own classification as "indicative rather than exact" and did not carry the glob,
and they do not reproduce together: 3.3 and 3.4 do, 3.2 and 3.5 do not, and no single method produces
all four. They were context for finding candidates, never the unit of work.

---

## WHAT LANDED -- 3.2, 3.3, 3.4 and 3.5, on 2026-09-08

| Task | Path | PR             | What it pins                                                    |
| ---- | ---- | -------------- | ----------------------------------------------------------------- |
| 4    | 3.2  | #693           | the external booking role set, as an independent literal          |
| 5    | 3.3  | #697, **#705** | document download; packet download, where a REAL defect was found |
| 6.1  | 3.4  | #709           | the per-role packet KIND filter at the boundary that uses it      |
| 6.3  | 3.4  | #711           | the kind gate on the patient packet email handler                 |

### 3.2 -- the prediction was RUN, and it HELD

Removing `"Defense Attorney"` from the grant loop failed **zero of 1,919 tests**. The role is still
created, still assignable, and holds nothing; a user given it logs in and every booking action is
refused, presenting as a permissions bug on a correctly-configured-looking role. Now pinned.

**Tracked as #692 for unification, with one constraint that must survive it:**
`MultiOfficeExternalRoleGrantsTests` keeps a DELIBERATE, INDEPENDENT literal role list. Whoever
unifies the three production lists must NOT make that test read from the unified one -- doing so
moves the assertion and the code under test together and silently restores the vacuity. It will look
like finishing the job.

### 3.3 -- this path contained a REAL DEFECT, not only a test gap

- **Document download** had no test anywhere (`git grep -n "DownloadAsync" -- 'test/'` returned
  nothing) while being the only thing separating one external party from another's documents. **The
  guard existed and worked**; what was missing was anything that would catch its removal. #697.
- **Packet download was an actual hole.** All four public methods of `AppointmentPacketsAppService`
  took a caller-supplied `appointmentId` with **no party-level check at all**, and every external
  role is granted `{Group}.AppointmentPackets` in the booking baseline, so the permission gate
  admitted every external party by design. Within one office, any valid appointment id returned
  another party's packet. Fixed in #705; each of the four guards was then deleted individually and
  each produced a failure in its own method's test.
- **Signature download is structurally safe** and needs no fix: `UserSignatureAppService
  .DownloadAsync()` takes no parameter and reads the current user's own extension property, so there
  is no caller-supplied identity to confuse. It has zero tests, which is a coverage gap rather than a
  hole, and is logged rather than chased.
- Notification email and the SSN reveal already had adequate guardians; breaks were run on both.

### 3.4 -- task 6.2 could NOT be written honestly, and was not written

Pinning the Pacific date stamp is **blocked, and deliberately left blocked**. `IClock` is injected
and substitutable, so the clock was never the obstacle. `PacketTokenResolver` has **22 constructor
dependencies**, no test constructs it, and `DateNow` is assigned inline with `FormatDate` private
static -- so no narrower seam exists. With the real clock the only reachable assertion is
`DateNow == FormatDate(PacificTime.TodayFrom(now))`, **which compares the code to itself and passes
forever, including with the Pacific conversion deleted.**

**Tracked as #710.** The guarantee it would have pinned is one that ALREADY BROKE in production -- a
packet generated after roughly 4-5pm Pacific was stamped with tomorrow's date, on the legal artefact.
It remains unguarded, and that is stated rather than papered over.

The standing rule applied here: **a needed seam is a flag to raise, not a cost to absorb** -- applied
to test infrastructure rather than production code, which is the same principle.

### 3.5 -- the guardian that never guarded, and a harness that cannot host one

**Task 7 was scoped as a break-and-observe tick. It produced the largest finding of the phase.**

`SubmitAsync` writes a patient, an appointment and six child groups atomically, and the docstring at
`AppointmentsAppService.cs:714-726` records why: production appointments **A00010 and A00011** shipped
as half-bookings, full attorney columns with zero join rows. The failure mode is not hypothetical.

**Two tests are named for that guarantee and neither exercises it.** Both provoke their failure with a
5,000-character body-part description against a 500-character DTO constraint
(`AppointmentBodyPartCreateDto.cs:11`, `AppointmentBodyPartConsts.cs:12`), so ABP's validation
interceptor rejects the request **before `SubmitAsync` runs**. Nothing is written, and the three
"nothing survived" assertions are satisfied by a database that was never touched. The test comment
asserted the opposite in as many words.

Measured by capturing the exception the tests had been swallowing:

```text
old trigger, 5,000 chars    Volo.Abp.Validation.AbpValidationException
new trigger, bad FK         Microsoft.EntityFrameworkCore.DbUpdateException
```

**Three breaks, one cause.** Deleting `[UnitOfWork]` from `:733`, setting `isTransactional: false`,
and removing `AddAlwaysDisableUnitOfWorkTransaction` each moved **zero of 512 tests** -- all three
target code that never executes in these tests.

**What concealed it**, and the split in that file is exact rather than careless:

```text
ThrowAsync<BusinessException>   :189, :392, :468, :497, :590    every business-failure test
ThrowAsync<Exception>           :157, :433                      BOTH rollback tests
```

A base-type assertion cannot distinguish "rolled back correctly" from "failed earlier and therefore
never wrote anything". Those are the same green.

**The guarantee cannot be covered in this harness at all.** A trigger that genuinely reaches the
database was built -- a non-existent `WcabOfficeId`, a bare `Guid?` at
`AppointmentInjuryDetailCreateDto.cs:30` with no validation attribute -- and the rewritten test
**still fails against unmodified production code**. That is a property of the harness, and it was
proven by control rather than argued:

```text
Control1_SingleType_RollsBackOnAPlainThrow         FAIL   count should be 0 but was 1
Control2_TwoEntityTypes_BothRollBackOnAPlainThrow  FAIL   count should be 0 but was 1
```

One row, one entity type, one unit of work, then a plain `InvalidOperationException` -- deliberately
NOT a database error, so nothing is confounded with the thing under test. **The row survived: this
harness cannot roll back a single row of a single type.** Consistent with
`CaseEvaluationMultiOfficeTestModule.cs:110-120`, which routes every DbContext to a per-request
resolved connection string against separate named SQLite databases.

**No conclusion about production follows, and none is drawn.** The read rule was fixed before the
control ran: the row surviving means the instrument cannot measure the thing, so the booking result is
an artefact and says nothing about the booking path's correctness.

**Outcome.** The two tests were renamed to describe what they actually verify, their assertions
tightened to the MEASURED `AbpValidationException`, and the false comment corrected (#734). The
guarantee itself is tracked in **#732**, `Blocks public hosting`, alongside #707 -- same class: a real
guarantee, no regression guard, blocked on test infrastructure rather than on production code.

**Also settled here:** `[UnitOfWork]` at `:733` is decorative. ABP wraps application service methods
in a unit of work by convention, so the attribute is not what supplies it, and the docstring's "what
makes it atomic: `[UnitOfWork]` plus..." is imprecise. Proven in this codebase rather than quoted from
ABP's documentation: with the attribute deleted, success-path tests asserting persistence still
passed, which is impossible if `CurrentUnitOfWork` at `:770` had been null.

### The finding that outgrew this phase: no permission attribute can be regression-tested

Both test harnesses call `AddAlwaysAllowAuthorization()` (`CaseEvaluationTestBaseModule.cs:27`,
`CaseEvaluationMultiOfficeTestModule.cs:103`), and every test project inherits one of them. So the
authorization interceptor always succeeds and **243 permission-bearing `[Authorize]` attributes
across 49 files are inert under test.** Delete any one and the suite stays green.

**This is not a vulnerability** -- the checks work in production, and always-allow is ABP's own
template default. What is absent is any regression guard, over an entire category of guard, in the
phase built to eliminate exactly that.

**Tracked as #707, in the `Blocks public hosting` milestone by Adrian's instruction 2026-09-08**, so
it must be resolved before the portal is exposed to the public internet. It carries a four-layer
design; it needs a harness, not a test, which is why it is not a phase 3 item.

## Validation loop

Touches both layers, so both run:

```text
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
npx ng build
npx ng test --watch=false --browsers=ChromeHeadless
```

**Run the frontend half FIRST** -- 62s against 936s, so it is free information.

Baseline to beat, MEASURED 2026-09-04 on `feat/production-hardening`, superseding the 2026-08-28
figures: **2,291 backend passing / 16 skipped / 2,307 total / 0 failed**, and **667 frontend executed
/ 0 failed**, plus whatever this phase's own tasks add. A drop in either count is a regression, not a
rounding difference. **No `[Fact]`/`[Theory]` attribute count is a criterion**, because it cannot be
compared to a run.
