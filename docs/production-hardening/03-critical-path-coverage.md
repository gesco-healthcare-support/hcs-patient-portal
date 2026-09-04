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

### What 3.1 does NOT prove -- task 9, still open

The decoy proves `Clear()` removes what is present. **It does not prove ABP's defaults ARE present at
that moment**, because the decoy is seeded by construction -- which assumes the very ordering the
research flagged as the class of reasoning this epic distrusts.

Task 9 closes it by booting a real application (`AbpIntegratedTest`, already used at
`CaseEvaluationTestBase.cs:12`) and reading the assembled options. **Its FIRST step is not the
assertion but proving the fixture is non-empty:** boot the graph WITHOUT our configuration and report
what `IOptions<AbpTenantResolveOptions>` contains. If it is empty, the booted test proves nothing and
must not be written -- that is a finding about the harness, not a licence to force ASP.NET hosting
into the test project.

**An asymmetry to carry into it:** `CaseEvaluationHttpApiHostModule.cs:69` declares
`AbpAspNetCoreMvcUiMultiTenancyModule`; the AuthServer declares no multi-tenancy module at all. Its
resolved graph is UNKNOWN -- a declared `DependsOn` list is not a resolved module graph, and three of
its declared modules are plausible transitive carriers. **If the AuthServer's collection comes back
empty, the finding is "no framework resolvers reach this graph today, so `Clear()` is defensive
here", and nothing stronger.** Absent defaults is less to be protected FROM, not less protection.

---

## Next: 3.2-3.5 are break-and-observe, not new suites

Each path already has a guardian test. The work is one guarantee, one test, one break: apply the
break to source, run that path's test, record whether it failed AND whether the failure NAMED the
guarantee rather than a downstream symptom, revert, confirm green.

| Path | Guardian                                     | Break to apply                                           |
| ---- | -------------------------------------------- | -------------------------------------------------------- |
| 3.2  | `ExternalUserRoleGrantsTests.cs:25-38`       | remove a permission from `BookingBaselineGrants()`       |
| 3.3  | `OutboxDrainServiceTests.cs:118`             | make the drain ignore `NotificationsPolicy.EmailEnabled` |
| 3.4  | `PacketVisibilityUnitTests.cs:26-67`         | make `AllowedKinds` return all three kinds for Doctor    |
| 3.5  | `MultiOfficeAtomicBookingSubmitTests.cs:134` | remove the transaction boundary around child writes      |

**3.2 carries a PREDICTION, and it is still a prediction.**
`ExternalUserRoleDataSeedContributor.cs:60` grants the booking baseline by looping a hardcoded
four-name array, and `ExternalUserRoleGrantsTests` pins the list's CONTENTS, not the ROLE SET.
Removing `"Defense Attorney"` from that array is predicted to fail no test. **Run it before anyone
records it as a fact.** If it holds, the accurate statement is "the role set is unasserted" -- NOT
"roles are unprotected".

**The per-path file counts previously cited (17 / 74 / 9 / 13) are STRUCK.** They came from research
that recorded its own classification as "indicative rather than exact" and did not carry the glob,
and they do not reproduce together: 3.3 and 3.4 do, 3.2 and 3.5 do not, and no single method produces
all four. They were context for finding candidates, never the unit of work.

---

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
