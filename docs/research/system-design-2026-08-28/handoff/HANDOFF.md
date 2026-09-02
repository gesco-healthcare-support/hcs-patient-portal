# Handoff: system design and infrastructure architecture research

> Complete package for delivering this research back to the session that commissioned it.
>
> Produced 2026-08-31 by a Claude Code session with web access and **no repository access**.
> Research scale: 34 agents, four phases, 5.7M subagent tokens, 1,972 tool calls, zero errors.
> **526 individual claims adversarially re-verified: 347 clean, 161 corrected, 12 wrong, 6
> unreachable.** About two thirds survived first-pass scrutiny, which is the argument for the
> verification pass having existed.
>
> A second workflow then audited the two finished deliverables across eight dimensions and
> adversarially verified every finding it raised. Section 7 records what that caught.

---

## 1. What is in this package

### The deliverables (what was asked for)

| File | Size | What it is |
| --- | --- | --- |
| `system-design-target.md` | 165 KB | **Document A.** One-page summary leading with three decisions; the portfolio problem and a 40-hour list; 14 corrections to the baseline documents; 14 areas each with current state, viable options, recommendation, operational cost and what would change the answer; the target architecture; answers to the five blocking questions; eight rejected patterns; 16 adjudicated contradictions; the evidence record |
| `infrastructure-requirements.md` | 59 KB | **Document B.** 62 capability requirements in five groups, each MUST or SHOULD with its reason, traceability and a test; 10 requirements that are the application's rather than the platform's; 17 anti-requirements; scoring guidance; 9 named gaps |

### The supporting record (so the conclusions can be audited, not just inherited)

| File | Size | What it is |
| --- | --- | --- |
| `research-method-and-analysis.md` | 28 KB | **Read this second.** How the research was structured and why, **the judgement calls that were mine rather than the agents'**, where the evidence conflicted and how it was resolved, what I drafted that the research overturned, and what would change the conclusions |
| `appendix-A-evidence-register.md` | 719 KB | **646 sourced claims** with source URL, second source where one was obtained, access date, the researching agent's self-declared confidence, and the adversarial verifier's verdict on that specific claim with its correction |
| `appendix-B-verification-and-critique.md` | 129 KB | **The integrity record.** Every material error the adversarial pass caught, per area, plus every vendor-neutrality finding, plus the portfolio critique that found the contradictions |
| `raw-research.json` | 2.5 MB | Unprocessed structured output from all 34 agents. Machine-readable, for anything the appendices flattened |
| `HANDOFF-PROMPT.txt` | 15 KB | **The handoff prompt, as a standalone file.** Paste it into the receiving session. It is also reproduced in section 5 below |

**Reading order for a human:** Document A section 1 (one page), then Document A section 2 (the
portfolio problem), then `research-method-and-analysis.md` section 3 (the judgement calls). That is
about twenty minutes and carries the decision. Everything else is reference.

---

## 2. The six things the receiving session most needs to know

**1. This session never had the repository, and that limitation is sharper than it sounds.** Every
statement about what the code currently does is inherited from `01-system-architecture.md` and
`03-deployment-and-constraints.md`. There is one apparent exception that is a weaker check than it
looks: Document A section 3 corrects those documents in fourteen places from framework, engine and
RFC sources read directly. **That establishes what the framework does by default. It does not
establish what this codebase does.** Treat all fourteen as hypotheses to check against source.

**2. The research was run blind to the current system's gap list, deliberately.** The fourteen area
agents received the architecture and the constraints but not a list of known problems. Handing an
agent the gap list produces a standard shaped around those gaps and nothing else. **That is why the
corrections table contains findings nobody was looking for** - the disclosure-accounting obligation
under 45 CFR 164.528 being the clearest example.

**3. The tenant boundary has never been tested and there is a specific reason to think it may not
hold.** Section 4 below. It is minutes of work, it is the only failure whose consequence is a
cross-practice PHI disclosure, and it decides whether the design's central claim is real. **If it
fails, nothing else in either document matters until it is fixed.**

**4. The six-year retention premise carried through both baseline documents is wrong**, and
correcting it saves roughly 45-70 hours of work - while surfacing a different six-year obligation
that nobody had listed, on an artefact measured in kilobytes rather than gigabytes. Document A
sections 3.8, 3.9 and 19.2.

**5. The aggregate cost is not affordable and the steady state alone consumes most of the budget.**
734-1,084 hours of setup and 24.5-43.5 hours a month against roughly 40 hours a month - at the top
of the range the ongoing work alone exceeds the whole allocation, and at the bottom it still takes
more than half. Each area is
locally honest; nobody owned the portfolio. Document A section 2 owns it and gives a 38-64 hour list
if only 40 hours ever materialise. **The brief asked for this to be stated plainly rather than
absorbed by trimming the list, so it was.**

**6. There is a timing asset that expires.** No real patient data exists yet. That makes credential
rotation free, destructive restore rehearsal free, per-office encryption possible **at all**, the
platform move a build rather than a migration, and "no PHI in non-production" free to adopt. All
five become expensive, and per-office crypto-erase becomes permanently impossible for data already
backed up, the day a real practice takes a real booking.

---

## 3. Repository checks: what to run, in order

Every check below was blocked **only** by lack of repository or database access. Each names what to
open or run, and what changes depending on the answer. Times are for the check alone, not the fix.

**Total for Tier 0: about 75 minutes.** It settles whether this system can be exposed publicly at
all, and four of its seven items change a recommendation regardless of the answer.

### Tier 0 - before public exposure. A bad answer means stop

| # | Question | How | Min | What it changes |
| --- | --- | --- | --- | --- |
| **1** | **How is `HostAwareDomainTenantResolveContributor` registered?** | Read the registration call. Look for `TenantResolvers.Insert(0, ...)` versus a bare `.Add(...)` | 5 | **The entire tenant-boundary argument turns on this one line.** A bare `.Add(...)` puts it after QueryString, Route, Header and Cookie, and `?__tenant=` then outranks the hostname on every anonymous request |
| **2** | **Is `CurrentUserTenantResolveContributor` still in the chain?** | Read the assembled `AbpTenantResolveOptions.TenantResolvers`, or log the list at startup | 5 | Determines whether an authenticated request resolves tenancy from the **token claim** or from Host. If present, hostname selects and the token must independently authorise - which makes check 5 mandatory |
| **3** | **Can `__tenant` change the resolved tenant?** | `curl` with `?__tenant=`, an `__tenant` route value, an `__tenant` header and an `__tenant` cookie, against `{office}.<d>`, `{office}.api.<d>` and `{office}.auth.<d>` | 15 | If any works, this is a live cross-tenant read, not a design gap. Note `MultiTenancyMiddleware` **persists a query-string tenant into a cookie**, so one successful request poisons the session |
| **4** | **Does a request with an empty `Host` reach the fallback resolvers?** | `curl -H 'Host;'` (explicitly empty). The stock domain contributor abstains when there is no host value, and the chain continues | 10 | **This bypass survives correct resolver ordering.** An empty Host field value is legal under RFC 9112 s3.2 and Kestrel accepts it |
| **5** | **Is a token minted at office A refused at office B's API?** | Authenticate at `office-a.auth.<d>`, present the token to `office-b.api.<d>` | 15 | If accepted, tenancy is decided by a claim that another office's auth server issued. This is the check nobody had on a list before the research |
| **6** | **Are there untyped `IDistributedCache` writes?** | grep for `IDistributedCache` without the generic parameter; then watch the keyspace for keys lacking the `t:{tenantId},` prefix | 15 | One untyped write of a permission grant or settings blob is a **cross-office authorisation leak that produces no error** - office A's user holding office B's permissions |
| **7** | **Does a tenant-scoped job with no tenant argument write to the host database?** | Add a guard that throws when `CurrentTenant.Id` is null inside a tenant-scoped job; run one sweep cycle | 10 | Twelve jobs iterate offices and three run every fifteen minutes. Anything that fails the guard has been writing office-shaped data into the host database |

### Tier 1 - changes what to do first, and by how much

| # | Question | How | Min | What it changes |
| --- | --- | --- | --- | --- |
| **8** | **What edition is production actually running, and what features do the databases depend on?** | `SELECT @@VERSION`, then `SELECT * FROM sys.dm_db_persisted_sku_features` on **every** database | 10 | `MSSQL_PID` is `Developer`, which may not be used in production. Everything built and measured to date has run with Enterprise features silently available. **Run this before choosing an edition, not after** |
| **9** | **What is the recovery model of every database?** | `SELECT name, recovery_model_desc, log_reuse_wait_desc FROM sys.databases` | 5 | Recovery model is inherited from `model` at `CREATE DATABASE`, and offices are created by a host-UI action. **Any office in SIMPLE has no point-in-time capability, forever, and nothing surfaced it** |
| **10** | **How much free disk is there today, and what is the trend?** | One `df` and one `du` per volume, recorded daily | 5 | Within two weeks it yields a **projected exhaustion date**, which is the most decision-relevant number available. Audit is unpruned on roughly 9.7 GB free shared with the object store and the backup directory |
| **11** | **Are the two outbox drain jobs idempotent under CONCURRENT execution**, not merely on sequential retry? | Read the drain jobs. Look for a durable claim (an `UPDATE ... OUTPUT` or equivalent) versus a plain read-then-send | 20 | **The single decisive fact for whether a second API instance is dangerous.** If rows are claimed durably, the job-duplication risk drops from severe to tolerable, and Q3's answer softens |
| **12** | **Is entity history capture set to all entities, and is it saving on navigation changes?** | grep for `EntityHistorySelectors.AddAllEntities` and for `SaveEntityHistoryWhenNavigationChanges` | 10 | These two settings are the whole of the 259-rows-per-appointment ratio. **This is the cheapest order of magnitude in the document** and it is a configuration change |
| **13** | **Are the twelve recurring jobs Hangfire recurring jobs, or framework periodic workers on an in-process timer?** | Read the job registration | 10 | If they are in-process periodic workers, **nothing is persisted anywhere** and the entire job-observability technique in Area 9 does not apply. Would be the most important discovery in that area |
| **14** | **Does the reverse proxy forward the original `Host` on every location, and is there a `default_server` on 443?** | Read `docker/nginx-proxy/default.conf.template` | 10 | The default proxy behaviour is to send the **upstream's** name. And an unmatched Host currently falls through to the first server block, which is the AuthServer |
| **15** | **Read `docs/security/HIPAA-COMPLIANCE.md` and `THREAT-MODEL.md`, and diff them against Document B Group 2** | Read both | 45 | **The promised HIPAA safeguard inventory was missing from the delivered input** (Document A section 22 item 4). This research never saw the project's own assessment. Diffing may **retire several requirements** |
| **16** | **Is the application authenticating to the object store as root?** | Read the object-store credentials in the compose file and the application configuration | 5 | Confirms whether the scoped-credential change in Area 13 is still outstanding. It does **not** require the partner's cooperation |

### Tier 2 - changes a number or a wording

| # | Question | How | Min | What it changes |
| --- | --- | --- | --- | --- |
| **17** | Does the custom contributor set `Handled` unconditionally, or only on a match? | Read the contributor | 10 | If only on a match, non-matching hosts fall through rather than terminating - which changes invariant I5 and makes ordering necessary but not sufficient |
| **18** | Does it match on the **port-inclusive** host value or the port-stripped one? | Read the contributor | 5 | Invariant I2. If port-inclusive, any port-shifting hop is a silent whole-tenant outage |
| **19** | Does it validate the label against the base domain, or return the leftmost label unconditionally? | Read the contributor; test with a bare IP | 10 | Explains why a bare IP returns "Tenant not found" rather than host scope. The research's inference is that it does **not** validate |
| **20** | Where do the OpenIddict token and authorization tables live - host or tenant context? | Read the DbContext registrations | 10 | Changes **exactly which sessions survive a per-office restore**, so Q5's reconciliation list may be broader or narrower |
| **21** | Is `HideErrors` still at its default for auditing? | grep for `AbpAuditingOptions` configuration | 5 | If it has been set false, an audit-store failure is already a total outage, and Area 2's recommendation inverts |
| **22** | Do background jobs open an audit scope? | Read one job's execution path | 15 | If they do, twelve per-office recurring jobs are a **second, entirely unmeasured audit growth source** |
| **23** | Does `TenantConnectionStringProvider` replace the connection-string resolver or populate the tenant connection-strings dictionary? | Read the class and its registration | 10 | Decides whether the per-office audit database in Area 2 is a configuration change or a small code change. Small either way |
| **24** | Is `IDistributedEventBus` used anywhere in `src`, and for what? | grep | 10 | Determines how severe the in-process-event-bus finding is for multi-instance |
| **25** | Are the DataProtection keys currently encrypted at rest? | grep for `ProtectKeysWith` | 5 | Specifying a custom persistence location **deregisters the default at-rest protection**. If absent, keys are stored in the clear today |
| **26** | What is the per-database memory overhead on this instance? | `SELECT database_id, SUM(pages_kb) FROM sys.dm_os_memory_clerks GROUP BY database_id` | 10 | Replaces an explicitly unsourced estimate with a fact. The research marked this `UNVERIFIED` and refused to assert a number |
| **27** | What are the current office database sizes and the row counts of **all five** audit tables? | `sys.dm_db_partition_stats` per database | 15 | Converts several `UNKNOWN` cells in the capacity model to `MEASURED`. **This is most of what `02-runtime-and-data-profile.md` would have supplied** |

---

## 4. Do this first: prove the tenant boundary

Separated from section 3 because it outranks everything in it.

`01-system-architecture.md` section 3.1 records honestly that ABP registers default `__tenant`
resolvers and that whether they are disabled **has never been tested**. Reading ABP's source rather
than assuming produced three findings that change the shape of the question:

**(a) There are five default resolvers, not four, and the first is not a `__tenant` resolver.**
`AbpMultiTenancyModule` inserts `CurrentUserTenantResolveContributor` at index 0; the ASP.NET Core
module then adds QueryString, Route, Header and Cookie. The first reads the tenant claim from the
**access token** and marks the chain handled whenever the user is authenticated. **So for an
authenticated request, tenancy is decided by the token, not by the Host header.** Host-header
tenancy governs the **anonymous** surface: login, password reset, anonymous document upload, the
partner reconcile endpoint.

**(b) The real question is ordering, not removal.** `DomainTenantResolveContributor` sets
`Handled = true` **unconditionally** whenever the request has a Host, before returning its match
result. So the fallback resolvers are unreachable **if and only if** the custom contributor is
ordered ahead of them - whether anyone removed them is irrelevant. If it was registered with the
natural `.Add(...)`, it lands last, and `?__tenant=` outranks the hostname on every anonymous
request, after which `MultiTenancyMiddleware` persists that choice into a cookie.

**(c) There is a spec-legal bypass that survives correct ordering.** If the request has no Host
value, the domain contributor returns **without** setting `Handled` and the chain continues. RFC
9112 section 3.2 requires a client to send `Host:` with an empty field value when the target URI has
no authority, and Kestrel permits it.

### The matrix

| # | Check | How | Expected |
| --- | --- | --- | --- |
| 1 | How is the custom resolver registered? | Read the registration call for `HostAwareDomainTenantResolveContributor` | `TenantResolvers.Insert(0, ...)` or an equivalent that puts it ahead of QueryString. A bare `.Add(...)` is a finding |
| 2 | Is `CurrentUserTenantResolveContributor` still present? | Read the assembled resolver list, or log it at startup | Determines whether the token claim can outrank the hostname |
| 3 | Can `__tenant` change the resolved tenant? | `curl` with `?__tenant=`, a route value, an `__tenant` header, and an `__tenant` cookie, against all three hostname shapes | No change in every case |
| 4 | Does an empty Host reach tenant resolution? | `curl` with an explicitly empty `Host:` header | Rejected at the edge or refused by the application |
| 5 | Is a cross-office token refused? | Mint a token at office A's auth server, present it to office B's API | 403, not 200 |
| 6 | Are there untyped `IDistributedCache` writes? | grep for untyped cache usage; observe whether written keys carry the `t:{tenantId},` prefix | Every tenant-scoped key prefixed |
| 7 | Does a null-tenant job resolve to the host database? | Add a guard that refuses to run when a tenant-scoped job argument carries no tenant; see what fails | Nothing should fail. Anything that does was writing to the wrong database |

**Then make items 3 to 7 permanent automated tests**, because every one of these failures is silent.

---

## 5. The handoff prompt

**It is also in `HANDOFF-PROMPT.txt` as a standalone file** - attach that, or paste the contents
below. Attach `system-design-target.md`, `infrastructure-requirements.md` and
`research-method-and-analysis.md` at minimum. Attach the appendices if the session will be
challenging specific claims.

```
I commissioned a research project on system design and infrastructure architecture, and I am handing
the results back to you. Attached are the deliverables and the supporting record.

CONTEXT

The application is a workers' compensation Independent Medical Examination (IME) scheduling portal:
.NET 10, ABP Commercial 10.0.2, Angular 20.3.19, SQL Server 2022, EF Core, OpenIddict, Redis, MinIO,
a small Python/Flask packet renderer, all containerised via docker-compose. Multi-tenant, one SQL
Server database per office, 11 offices at launch with headroom to 33. Tenancy resolves from the HTTP
Host header. It is about to move from an internal office LAN to the public internet for the first
time. Maintained by two SDE 1 developers with no senior engineer, no architect, no security
specialist, no DevOps and no QA, at roughly one developer-week per month for infrastructure work.
There is no real patient data in it yet; everything is synthetic.

This was the SECOND of three exercises. The first established a production code standard and
produced a 50-item remediation plan (141 launch-blocking hours). This one determined what shape the
system and its infrastructure should take, expressed as vendor-neutral requirements. The third,
platform and cloud selection, comes next and has not started.

The brief for this exercise said explicitly: do not pick a cloud provider, do not price anything.
That is an ordering constraint, not a style preference - the architecture is supposed to generate
the platform requirements, not the reverse. Please preserve it. If you find yourself about to say
"so we should use X", that belongs in the next exercise, not this one.

WHAT IS ATTACHED

  system-design-target.md            Document A. One-page summary leading with three decisions;
                                     the portfolio problem and a 40-hour list; 14 corrections to
                                     the baseline documents; 14 areas each with current state,
                                     options, recommendation, operational cost and what would
                                     change the answer; the target architecture; answers to the
                                     five blocking questions; eight rejected patterns; 16
                                     adjudicated contradictions; the evidence record.

  infrastructure-requirements.md     Document B. 62 capability requirements in five groups
                                     (tenancy, HIPAA, team size, application mechanics, release),
                                     each MUST or SHOULD with its reason, traceability and a test;
                                     10 requirements that are the application's rather than the
                                     platform's; 17 anti-requirements; scoring guidance; 9 gaps.

  research-method-and-analysis.md    READ THIS SECOND. How the research was structured and why,
                                     the judgement calls that were mine rather than the agents',
                                     where the evidence conflicted and how it was resolved, what I
                                     drafted and the research overturned, and what would change the
                                     conclusions.

  appendix-A-evidence-register.md    646 sourced claims with source URL, access date, the agent's
                                     confidence, and the adversarial verifier's verdict on that
                                     specific claim with its correction.

  appendix-B-verification-and-critique.md   What the adversarial pass caught, per area, plus the
                                     portfolio critique that found the contradictions.

  raw-research.json                  Unprocessed structured output from all 34 agents.

Read Document A section 1 (one page), then section 2 (the portfolio problem), then
research-method-and-analysis.md section 3 (the judgement calls). That is about twenty minutes and
carries the decision. Do not rewrite either deliverable until I ask.

THE CRITICAL LIMITATION, AND PLEASE DO NOT LOSE IT

The research session had web access but NOT repository access and no access to the running system
or its databases. Every statement in these documents about what the code currently does is INHERITED
from two baseline documents, not read from source. Where those documents are wrong, the deliverables
are wrong in the same place.

There is one important exception and it is a weaker kind of check than it looks: Document A section
3 corrects the baseline documents in fourteen places, and those corrections come from reading the
ABP, ASP.NET Core, SQL Server, Hangfire and RFC sources directly. That establishes what the
FRAMEWORK does by default. It does not establish what THIS codebase does. Treat every one of those
fourteen as a hypothesis to check against source, not as a finding.

You presumably do have the repository. Section 3 of HANDOFF.md lists the repository checks, ordered,
with the exact command or file for each and what each one changes. Running them and reporting back
is the single most valuable thing you can do next.

THE ONE THING TO DO BEFORE ANYTHING ELSE

Prove the tenant boundary. It is minutes of work and it decides whether the rest of this analysis is
built on something real.

The baseline document records honestly that ABP registers default __tenant resolvers and that
whether they are disabled "has never been tested". Reading ABP's source rather than assuming turned
up three things that change the question:

  1. There are FIVE default resolvers, not four, and the first is not a __tenant resolver at all.
     AbpMultiTenancyModule inserts CurrentUserTenantResolveContributor at index 0; the ASP.NET Core
     module then adds QueryString, Route, Header and Cookie. The first resolver reads the tenant
     claim from the ACCESS TOKEN and marks the chain handled whenever the user is authenticated.
     So for an AUTHENTICATED request, tenancy is decided by the token, not by the Host header.
     Host-header tenancy is the mechanism for ANONYMOUS requests - login, password reset, anonymous
     document upload, the partner reconcile endpoint.

  2. The real question is ORDERING, not removal. DomainTenantResolveContributor sets Handled = true
     unconditionally whenever the request has a Host, before returning its match result. So the
     __tenant resolvers are unreachable IF AND ONLY IF the custom HostAwareDomainTenantResolveContributor
     is ordered ahead of them. Whether anyone removed them is irrelevant. If it was registered with
     the natural .Add(...) rather than an insert, it lands last, and ?__tenant= outranks the hostname
     on every anonymous request - after which MultiTenancyMiddleware persists that choice into a
     cookie.

  3. There is a spec-legal path to the fallback resolvers even when ordering is correct. If the
     request has no Host value, the domain contributor returns WITHOUT setting Handled and the chain
     continues. RFC 9112 section 3.2 requires a client to send Host: with an empty field value when
     the target URI has no authority, and Kestrel permits it.

The check is a curl matrix plus one file read:
  - read the registration call for HostAwareDomainTenantResolveContributor: Insert(0, ...) or .Add(...)?
  - __tenant as query string, route value, header and cookie, against all three hostname shapes
  - a request with an empty Host header
  - a token minted at office A presented to office B's API

Two more cross-tenant hazards belong in the same session because they have the same blast radius and
the same silence: the ambient null-tenant fallback (a tenant-scoped job whose argument carries no
tenant resolves to the HOST database - twelve jobs iterate offices and three run every fifteen
minutes), and untyped IDistributedCache writes (the typed cache prefixes tenant-scoped keys with
t:{tenantId}, untyped usage does not - one untyped write of a permission grant is a cross-office
authorisation leak that produces no error).

If any of these fails, stop. It is the only class of failure in the entire body whose consequence is
one practice's injured worker appearing in another practice's data, and no amount of edge hardening,
backup or monitoring can undo or even detect it after the fact.

THE FINDINGS THAT MOST CHANGE WHAT WE DO

1. THE SIX-YEAR AUDIT RETENTION PREMISE IS WRONG, AND THE REAL OBLIGATION IS SOMETHING ELSE.
   45 CFR 164.316(b)(2)(i) requires six-year retention of the DOCUMENTATION required by 164.316(b)(1)
   - the written policies and procedures implemented to comply with the Security Rule, and written
   records of actions the subpart requires to be documented. An application audit log is neither, and
   164.312(b) states no retention period at all. What we must keep for six years is our audit and
   retention POLICY, measured in kilobytes.
   BUT: 45 CFR 164.528(a)(1) gives individuals a right to an accounting of disclosures for six years,
   and its exclusion list does NOT exclude 164.512(l), disclosures for workers' compensation - which
   is our entire business. So we owe six years of a narrow, purpose-built DISCLOSURE LEDGER, as
   first-class domain data, not audit exhaust. Nobody was looking for this and it is not in either
   baseline document.

2. THE SHARED sa LOGIN IS A CONSEQUENCE, NOT SLOPPINESS, AND THE NAIVE FIX BREAKS ONBOARDING.
   Office-onboarding-as-a-host-UI-action forces runtime CREATE DATABASE, which requires elevated
   permissions, which is where the shared superuser account comes from. Anyone who "fixes" it by
   demoting that account will break the ability to add an office. The correct fix is to split the
   PROVISIONING identity from the RUNTIME identity and keep both properties.

3. WE ARE PROBABLY RUNNING A DATABASE EDITION WE MAY NOT USE IN PRODUCTION, AND SWITCHING SILENTLY
   REMOVES FEATURES. MSSQL_PID is Developer, which carries all Enterprise functionality but is
   licensed for development and test only. So everything built and measured to date has run with
   higher-tier features silently available. Nothing errors when the edition changes; capabilities
   simply stop existing. Run sys.dm_db_persisted_sku_features on every database BEFORE choosing an
   edition. This is a go-live blocker being treated as a later cost decision.

4. BACKUP ENROLMENT HAS TO MOVE INTO PROVISIONING. Point-in-time restore requires the FULL recovery
   model; recovery model is a per-database property inherited from the model database at CREATE
   DATABASE; and office databases are created by a BUSINESS ACTION in the host UI. So whether an
   office can be restored to a point in time is decided by the state of the model database at the
   moment a non-engineer clicked "add office", and nothing surfaces it. Worse, the switch to FULL
   only takes effect after the first data backup. The business can create a database faster than the
   backup system can notice it exists.

5. THE BINDING BLOCKER ON A SECOND API INSTANCE IS THE REVERSE PROXY, NOT HANGFIRE. nginx resolves
   proxy_pass upstreams once at worker start, so a second API container never receives traffic: the
   deploy looks successful, no error appears, and every conclusion drawn about whether multi-instance
   works is drawn from a system still running one instance. There are four blockers in total and this
   is the one that makes the others invisible.

6. THE AGGREGATE COST EXCEEDS THE BUDGET BY AN ORDER OF MAGNITUDE, AND THE STEADY STATE ALONE
   CONSUMES MOST OF IT. 734-1,084 hours of setup and 24.5-43.5 hours a month of ongoing work
   against roughly 40 hours a month - so at the top of the range the ongoing work alone exceeds the
   whole allocation, and at the bottom it still takes more than half. Each of the fourteen areas is locally honest; the sum is not affordable.
   Document A section 2 owns this and gives a 38-64 hour list to execute if only 40 hours ever
   materialise. Take that to the business as a decision rather than letting the calendar decide.

RULES OF EVIDENCE THAT WERE APPLIED AND SHOULD CONTINUE TO APPLY

Every standard, limit, threshold and platform capability asserted carries a source URL and an access
date in appendix-A-evidence-register.md, or is marked UNVERIFIED with what was tried. 526 individual
claims were re-checked by a second adversarial agent instructed to default to "does not support the
claim". That pass caught a verbatim quotation attributed to a page that does not contain it, a
threshold taken from a 32-bit failure mode and applied to a 64-bit deployment, a regulation cited to
the wrong URL, several component defaults stated from memory against pages that never name them, a
tool claimed maintained that had been archived upstream, and a DNS propagation window misread by
roughly eight times. Only about two thirds of claims survived first-pass scrutiny.

Please preserve that convention. Do not replace a cited figure with one from memory, and do not
promote anything marked UNVERIFIED to asserted without opening the source yourself. If you correct
something, say what you checked.

THREE THINGS I ALREADY KNOW ARE MISSING

02-runtime-and-data-profile.md was never delivered to the research session - only three of the four
input files arrived. It is the one with measured database sizes and table-level row counts. The
capacity model in Document A section 14 is therefore built as a structure plus an instrumentation
plan, with every input labelled MEASURED, DERIVED or UNKNOWN and the cells that document would have
filled marked as such. If you have it, folding it in is an edit rather than a rebuild.

The HIPAA technical-safeguard inventory the brief says is in 03-deployment-and-constraints.md is not
in the delivered file - its section 4 ends at 4.10 and it carries no CFR citations at all. So
Document B's HIPAA group rests on the regulation text, which was adversarially verified, rather than
on our own assessment of which safeguards we currently meet. That assessment exists in the repo:
docs/security/HIPAA-COMPLIANCE.md and THREAT-MODEL.md, which 03 section 5 says explicitly to read
before proposing an architecture. The research never saw them. Diffing them against Document B
Group 2 is check 15 in HANDOFF.md and it may retire several requirements.

California and state law are entirely absent. The analysis argues carefully about federal retention
clocks for a product handling California workers' compensation and California medical records. CMIA
and California breach-notification law impose obligations that differ from and can exceed HIPAA, and
state medical-evaluation record rules bear directly on the retention question the documents spend the
most effort on. This is the item most worth an hour with counsel, and a verification pass that has
already caught one misread statute should be pointed here next.

WHAT I WANT FROM YOU

Start by telling me what you would do next, and flag anything in the analysis you think is wrong or
that the repository contradicts. Likely next steps, in the order I would guess:

  (a) Prove the tenant boundary - the curl matrix and the one file read above. Report what it shows.
  (b) Run the rest of the repository checks in HANDOFF.md section 3 and report what they change.
  (c) Take the capacity finding to the business as a decision: the steady state alone consumes the
      whole allocation, and the three options are laid out in Document A section 2.
  (d) Only then start on the 40-hour list.

Two framings worth preserving. First, roughly the first 40 hours of value here is PROOF AND DELETION,
not construction - proving the tenant boundary, sampling the disk daily, cutting audit capture at
source, and closing the default-server fallthrough are four items that build almost nothing.

Second, one piece of timing expires. There is no real patient data yet. That makes credential
rotation free, destructive restore testing free, per-office encryption possible at all, the platform
move a build rather than a migration, and "no PHI in non-production" free to adopt. All five become
expensive, and one becomes permanently impossible, the day a real practice takes a real booking.
```

---

## 6. If you only carry three files

`system-design-target.md`, `infrastructure-requirements.md`, `research-method-and-analysis.md`.

The appendices are reference material for challenging specific claims. **The method document is what
lets the receiving session tell my reasoning apart from the agents' sourced findings**, which
matters more than any individual citation - because the reasoning is the part that should be argued
with, and the citations have already been adversarially checked.

---

## 7. What the audit of the finished deliverables found

The two deliverables were audited after they were written, inline rather than by agents: every
requirement ID and section reference resolved mechanically, the portfolio arithmetic recomputed from
the per-area figures, and every claim the research's own verification pass had refuted grepped for in
the shipped text. Three things came out of it.

**1. The portfolio arithmetic was wrong and has been corrected.** Document A originally carried the
research's aggregate (roughly 700-1,050 hours setup, 35-57 hours a month) rather than the sum of the
costs **as this document sizes them**. Recomputed: **734-1,084 hours of setup and 24.5-43.5 hours a
month.** The gap is real and is now shown as two rows in section 2, because it is exactly the scope
Document A already cut relative to the research. **One claim was weakened as a result**: "the steady
state alone consumes the whole allocation" is true at the top of the range and not at the bottom, and
it now says so.

**2. A promised input was missing and nobody noticed until this audit.** The brief's file table says
`03-deployment-and-constraints.md` contains a HIPAA technical-safeguard inventory. **The delivered
file does not** - its section 4 ends at 4.10 and it carries no CFR citations anywhere. One
requirement traced to a section that does not exist; that trace is fixed. More importantly, Document
B Group 2 rests on the regulation text rather than on the project's own safeguard assessment - and
that assessment **exists in the repository**, at `docs/security/HIPAA-COMPLIANCE.md` and
`THREAT-MODEL.md`, which `03` section 5 says explicitly to read before proposing an architecture.
This is check 15 and it may retire several requirements.

**3. Everything else came back clean.** 89 requirement IDs defined, zero broken references in either
direction, zero orphans. Every input-document section reference resolves except the one above. All
thirteen claims the research's verification pass had refuted are either absent from the deliverables
or present only in their corrected form. No currency figure anywhere; no cloud provider named as a
recommendation.

**A note on how this audit was run**, because it bears on the method. It was first launched as a
34-plus-agent workflow with an unbounded verify stage - one agent per finding raised - which would
have run past a hundred agents. That was the wrong tool and it was stopped. Cross-reference
integrity is a grep, arithmetic is arithmetic, and "did the correction land" is a string search. The
one materially wrong thing in the deliverables was found inline with a six-line script, in about two
minutes, while the workflow was still spinning up. **Fan-out is for reading breadth this session does
not have; it is not for checking a document against itself.**

---

## 8. Known gaps in this package

Stated so nothing here is mistaken for complete.

- **No repository verification of anything.** Section 2, item 1.
- **`02-runtime-and-data-profile.md` was never supplied.** Only three of four input files arrived.
  It is the one the brief points at most often: measured database sizes, table-level row counts, the
  configuration surface, and the explicit "never measured" list. The capacity model is built as a
  structure plus an instrumentation plan with every input labelled `MEASURED`, `DERIVED` or
  `UNKNOWN`. The one load-bearing figure, the audit ratio, came from the brief itself. **Folding
  `02` in is an edit, not a rebuild.**
- **California and state law are entirely absent**, and this is a real gap rather than a scoping
  decision. The analysis cites federal regulation extensively for a product handling California
  workers' compensation and California medical records. State confidentiality and
  breach-notification law can exceed the federal floor, and state medical-evaluation record rules
  bear directly on the retention question the documents spend the most effort on. **The single item
  most worth an hour with counsel.**
- **Nothing has been measured under load and no restore has ever been performed.** The two cheapest
  measurements in the package - one daily disk sample and one timed restore - would change more of
  it than any further research.
- **The one-developer-week-per-month figure was never confirmed** against a staffing commitment, and
  the portfolio analysis is sized against it. Thresholds are stated wherever a recommendation's
  viability turns on it.
- **Six evidence items were unreachable** - mostly paywalled or bot-blocked - and their figures were
  **dropped rather than carried into a document a reader cannot check.**
- **The deployment-process risk has no requirement covering it.** The most probable cause of the next
  outage is a manual deploy by one of two people, and the cheapest controls - a declared change
  window, a two-person rule, a written rollback plan per deploy - are operating-agreement items, not
  platform capabilities. Named in Document B section 9.6 and deliberately not dressed up as a
  requirement.
