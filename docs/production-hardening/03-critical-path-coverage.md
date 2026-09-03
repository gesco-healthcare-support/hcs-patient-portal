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

## Validation loop

Touches both layers, so both run:

```text
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
npx ng build
npx ng test --watch=false --browsers=ChromeHeadless
```

Baseline to beat: 2,290 backend tests passing, 603 frontend, 0 failures, 16 skipped (measured
during the Pacific epic, 2026-08-28).
