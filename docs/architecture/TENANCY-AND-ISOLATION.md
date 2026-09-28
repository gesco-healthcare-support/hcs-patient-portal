[Home](../INDEX.md) > [Architecture](./) > Tenancy and Isolation

# Tenancy and Isolation

> Purpose: Explains why office isolation is built the way it is -- why a caller cannot choose their own office, why the schema is declared twice, and why cross-office work needs an explicit runner. Audience: backend engineers who need the reasoning behind the tenancy boundary before changing anything near it.

This is an explanation, not a procedure. It answers "why is it like this" rather than "how do I add
an entity". For the latter see [Multi-Tenancy Strategy](./MULTI-TENANCY.md) and
[Common Tasks](../onboarding/COMMON-TASKS.md).

## Why database-per-office

An office (an ABP tenant) gets its own database rather than a `TenantId` column in a shared one.
Two reasons, and the second is the one usually left out.

**Isolation and HIPAA.** A filter that is forgotten is a filter that leaks. A separate database
means a query that forgets its scope cannot return another office's patients, because those rows
are not reachable on that connection at all. The isolation is physical rather than conditional.

**Blast radius.** One consolidated database means a single corruption, a bad migration or a storage
failure takes down every office at once. Separate databases mean a failure is contained to the
office it happened in. This is an availability argument, not a privacy one, and it survives even if
the privacy argument were satisfied some other way.

The cost is paid in the rest of this page: nothing can query across offices in one statement, the
schema has to be declared twice, and any work that spans offices has to be written as a loop.

## How a request becomes an office

Every request resolves to exactly one office (or to host context) before any repository call runs.
ABP does this with a chain of tenant resolve contributors, tried in order until one handles the
request. Both host processes replace that chain:

```csharp
options.TenantResolvers.Clear();
options.TenantResolvers.Add(new CurrentUserTenantResolveContributor());
options.TenantResolvers.Add(
    HostAwareDomainTenantResolveContributor.FromConfiguration(configuration));
```

### `TenantResolvers.Clear()` is the security control

This is the single most load-bearing line in the tenancy boundary, and it is easy to read past
because it looks like tidy-up.

ABP's stock chain includes contributors that read the tenant from the request itself: query string,
cookie, header and route. Any one of them means a caller can append `?__tenant=<guid>` -- or set a
header -- and select which office's database to read. Under database-per-office that is not a
privilege-escalation bug that a later permission check might still catch. It is a different
connection string. The permission check would pass, because the caller genuinely holds the
permission; it would simply be applied to another office's data.

So `Clear()` is what makes `?__tenant=` inert, and only two contributors are added back:

1. `CurrentUserTenantResolveContributor` -- an authenticated caller's office comes from the claim
   in their token. First on purpose: a signed-in caller's identity must win over anything in the
   request envelope.
2. `HostAwareDomainTenantResolveContributor` -- an anonymous caller's office comes from the Host
   header, matched against `App:TenantDomainFormat` (`{0}.localhost` when unset).

Both sources are things the caller cannot simply assert. The token is signed; the Host determines
which certificate and which front-end the caller reached in the first place. Neither is a free-text
parameter.

`HostAwareDomainTenantResolveContributor` refuses a Host that names no office rather than falling
through to host context, because nothing follows it in the chain -- abstaining *would be* host
context. Its refusal is a throw, since ABP's `MultiTenancyMiddleware` turns a resolution exception
into the same 404 shape an unknown office gets, with no tenant-store lookup. The refusal message is
fixed text and never echoes the Host, which is caller-controlled and would otherwise reach both a
response header and the logs.

### Why the chain is declared twice

There are two `internal static ConfigureMultiTenancy` methods, one in
`CaseEvaluationAuthServerModule` and one in `CaseEvaluationHttpApiHostModule`. They are deliberate
near-duplicates rather than a single shared helper, because the two host modules do not share a
project that could hold one.

Duplication of a security control is normally a smell, and here it is a real risk: the two could
drift, and a token minted by an AuthServer resolving one way would then be read by an API resolving
another. The mitigation is that the duplication is asserted rather than trusted.
`TenantResolverChainTests.Both_processes_register_the_same_chain_in_the_same_order` compares the two
assembled chains directly, so drift fails a test instead of shipping.

The AuthServer half matters more than the API half, which is the opposite of the intuition. The
AuthServer mints the token that `CurrentUserTenantResolveContributor` later reads. A framework
upgrade that re-added a caller-supplied resolver *on the login path* would compromise every request
downstream, and pinning only the API side would leave that invisible. That is why the AuthServer
grants `InternalsVisibleTo` for its helper at all.

### Why testing this needs a decoy

A test of `Clear()` is a test of a negative guarantee: that nothing caller-supplied survives. A
negative guarantee cannot be proved against an empty fixture.

Build a bare `ServiceCollection`, run the module's configuration callback, and assert "exactly two
resolvers, in this order". That test passes. Now delete `TenantResolvers.Clear()` from production
code. It still passes -- a bare collection had no resolvers, so `Clear()` was a no-op and the
assertion never depended on it. Measured 2026-09-04: with no decoy, deleting the line left all
seven tests in that file green.

The fix is to seed a decoy contributor before the module's callback runs, named for a
caller-supplied source (`QueryStringDecoy`) so that removing `Clear()` fails the count, the ordering
and the "no caller-supplied resolver" assertion together. The decoy is load-bearing setup and is
commented as such, because the natural reading of a line that adds something the test does not
assert on is that it is noise to be cleaned up.

That decoy buys one fact and not two. It proves `Clear()` removes what is present. It cannot prove
ABP's own defaults are present at that moment to be removed, because a bare `ServiceCollection` has
no module ordering -- if the framework registered its defaults *after* our callback they would
survive in production with every test still green. Answering that needs options read from a booted
application, which is what `BootedTenantResolverDefaultsTests` does. Measured there on 2026-09-08: a
booted graph without either host module carries exactly one framework default,
`CurrentUserTenantResolveContributor`. So the framework does contribute of its own accord, `Clear()`
is removing something, and the real failure being guarded against is not a developer deleting a line
but an ABP upgrade adding a resolver -- a change that touches no file in this repository.

Note the discrepancy this exposed: the module comments describe ABP's defaults as five contributors
(CurrentUser, QueryString, Route, Header, Cookie). The measured set today is one. The comment
describes what the framework may contribute, not what it currently does; the `Clear()` is worth
keeping either way, precisely because that set is the framework's to change.

## Why the schema is declared twice

There are two DbContexts over the same entities:

- `CaseEvaluationDbContext` -- `MultiTenancySides.Both`, migrations in `Migrations/`. This maps the
  host (management) database: the tenant registry, the host-only tables, and everything else.
- `CaseEvaluationTenantDbContext` -- `MultiTenancySides.Tenant`, migrations in `TenantMigrations/`.
  This maps an office database.

Host-only tables are gated behind `if (builder.IsHostDatabase())` in the first context and simply
absent from the second: `IntakeOfficeAssignment` (which host operator may enter which office) and
`OfficeBranding` (the per-office brand the login page needs before any office is resolved) must
never exist in an office database, because both are statements *about* offices and belong above them.

The consequence that catches people: **an entity mapped in both contexts needs a migration in
both.** There is no generator that notices you added one and not the other. Adding `TenantId` to
`NotificationTemplateType` produced two migrations seventeen seconds apart,
`Migrations/20260625013839_AddNotificationTemplateTypeTenantId` and
`TenantMigrations/20260625013856_AddNotificationTemplateTypeTenantId`, because one alone would have
altered the host database and left every office database without the column. The failure mode is not
a build error. It is a runtime error in one office, after deploy.

The same asymmetry bites in subtler ways. The host context configures the Doctor join tables with
`DeleteBehavior.Cascade` and the office context with `DeleteBehavior.NoAction`, so the same
operation behaves differently depending on which database it runs against. The office context also
has to declare `Doctor`'s collection relationships *before* `ConfigureDoctorJoinEntities`, or EF
treats the later declaration as a second relationship and silently adds a `DoctorId1` shadow column.
Shared configuration that genuinely is identical lives in `ConfigureCaseEvaluationShared` so it is
declared once; what remains duplicated is duplicated because it differs.

## Which entities are per-office, and a documentation warning

`Patient`, `Location`, `State`, `AppointmentType`, `AppointmentLanguage` and `WcabOffice` all
implement `IMultiTenant`. ABP's automatic tenant filter applies to them, and under
database-per-office each office holds its own rows. They are **per-office data, not host-scoped
shared reference data.**

This needs stating plainly because the repository's own documentation said the opposite for months,
and that is worth more attention than the fact itself.

`Patient` was converted on 2026-05-05 (FEAT-09, ADR-006 task 4). Before that it was host-only with a
manual `TenantId` column and no automatic filter, which meant any caller holding the Patients
permission could read every office's patients. The remaining five were converted on 2026-07-08 in
the database-per-office epic.

The documents describing them were not revisited. As of this writing,
`docs/feedback-research/IP5-wcab-offices-crud-supervisor.md` still states "WcabOffice is host-scoped
(no IMultiTenant)", and `docs/parity-research/G-07-07.md` still reasons that `Location` is
host-scoped "per the source doc". Both were *correct when written*: IP5's content dates from
2026-06-10, a month before the conversion. `docs/decisions/003-dual-dbcontext-host-tenant.md` says
the office context contains "no Location, WcabOffice, Patient"; all three are declared in it today.

That is the cautionary note, and it is not about carelessness. Each of those statements was true,
became false through a change made somewhere else, and went on reading plausibly -- so nobody
re-checked it, and later work reasoned from it as established fact. A document cannot notice that
the world moved. When a change alters what something *is*, the record that described it is part of
the change, and the practical habit is to grep the docs for the claim you just invalidated rather
than to trust that someone will catch it in review.

## How work reaches every office when there is no request

Recurring jobs and host-level dashboards have no Host header and no token, so nothing resolves an
office for them. And because each office is a separate database, there is no query that spans them:
`SELECT ... FROM Appointments` cannot see more than one office however it is written.

`TenantWorkRunner` is the single audited place where "do this for every office" happens. It reads
the office ids from the tenant registry -- deliberately inside `Change(null)`, because the registry
lives in the host database and the multi-tenant filter would otherwise hide the very rows being
enumerated -- then runs the caller's delegate once per office inside `ICurrentTenant.Change(id)`, so
each iteration's repository calls land on that office's connection. It mirrors the per-tenant loop in
`CaseEvaluationDbMigrationService`, which is the pattern already proven to route correctly under
database-per-office.

Exceptions propagate, so one failing office aborts the run. That is the honest default for a
consistency job; callers wanting best-effort iteration catch inside their own delegate, where they
can say what a partial failure means.

### The trap: `Change(id)` sets the id and leaves the name null

`ICurrentTenant.Change(officeId)` sets `ICurrentTenant.Id`. It does not set `ICurrentTenant.Name`,
because it was given an id and never consulted the tenant store. Inside that scope `CurrentTenant.Name`
is null.

This is quiet rather than loud. Nothing throws; a template renders an empty string. It shipped twice
-- a blank office name in an alert email, and a blank column on an admin screen -- and both reached
production because unit tests substituted `ICurrentTenant` with a mock that returned a plausible
name. The mock proved the code compiled against the interface. It could not prove what the real
implementation returns at runtime, and the real implementation returned null.

The rule that follows: inside a changed tenant scope, take the office name from the tenant store (or
from an explicit parameter carried in), never from ambient `ICurrentTenant.Name`. The overload
`Change(id, name)` is available where the caller already knows the name -- `ExternalSignupAppService`
resolves it first and refuses to proceed if it comes back blank, rather than sending an invitation
naming nobody. The general lesson is broader than tenancy: where a test substitutes an ambient
contextual service, ask what that service actually returns in production before believing the test.

## What this design does not give you

Worth stating so nobody assumes more than is there.

- **Isolation is not authorisation.** Resolving the right office means a caller is looking at the
  right database. It says nothing about whether they may see a particular appointment inside it.
  That is `AppointmentReadAccessGuard` over `AppointmentAccessRules`, and holding a permission is
  not the same as having access to a given record.
- **Host context is still reachable, on purpose.** The reserved `admin` label and the internal host
  names keep host context so the management surface and the health checks work. Those are the only
  routes to it; anything else naming no office is refused.
- **Cross-office aggregation is a loop, not a query.** Anything summing across offices pays N round
  trips by construction. That is a direct consequence of the isolation choice at the top of this
  page, and it is the price of the blast-radius argument.
