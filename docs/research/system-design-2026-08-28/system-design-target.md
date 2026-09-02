# System design target

> Vendor-neutral target architecture for a public-facing, HIPAA-regulated, multi-tenant IME
> scheduling platform serving 11 offices at launch with headroom to 33.
>
> **No cloud provider is named as a recommendation anywhere in this document.** Nothing is priced.
> Where a design needs a capability only some platforms have, it is stated as a capability
> requirement and carries an identifier from `infrastructure-requirements.md`.

| Field | Value |
| --- | --- |
| Produced | 2026-08-31 |
| Inputs | `01-system-architecture.md`, `03-deployment-and-constraints.md`, both verified 2026-08-28. **`02-runtime-and-data-profile.md` was not supplied** - see section 22 |
| Method | 34 research agents with web access and no repository access; every citation re-fetched and re-checked by a second adversarial agent; five blocking questions resolved separately; one completeness and vendor-neutrality critique over the whole body |
| Evidence | 526 individual claims re-verified: **347 clean, 161 corrected, 12 wrong, 6 unreachable.** The corrections are in section 22 and were applied before this document was written |
| Repository access | **None.** Every statement about the current system is inherited from the two input documents |

---

## 1. One page

**The system does not need re-architecting. It needs its boundary proved and its operations built.**

That is the finding, and it was not the expected one. The tenancy model is right at 11 and right at
33. The process split is right. The absence of server-side rendering, WebSockets and a message
broker is a genuine simplification. The derived connection string - one configuration key relocates
every office database - is an unusually good property that most systems of this shape do not have.

What is missing is almost entirely operational, and one thing is worse than operational: **the
isolation boundary that the whole compliance argument rests on has never been tested, and there is
a specific reason to think it may not hold.**

### The three decisions that matter most

**1. Prove and pin the tenant boundary, and treat it as the gate on public exposure.**

This displaces every other candidate because it is the only failure on the list whose consequence
is one practice's injured worker appearing in another practice's data - a disclosure that no edge
hardening, no backup and no monitoring can undo, or even detect, after the fact.

`01-system-architecture.md` section 3.1 records honestly that ABP registers default `__tenant`
resolvers and that whether they are disabled "has never been tested". Reading ABP's source rather
than assuming produced three findings that change the shape of the question:

- **There are five default resolvers, not four, and the first is not a `__tenant` resolver at
  all.** `AbpMultiTenancyModule` inserts `CurrentUserTenantResolveContributor` at index 0; the
  ASP.NET Core module then adds QueryString, Route, Header and Cookie. That first resolver reads
  the tenant claim from the access token and marks the chain handled whenever the user is
  authenticated. **For an authenticated request, tenancy is decided by the token claim, not by the
  Host header.** Host-header tenancy is the mechanism for anonymous requests - which is a smaller
  blast radius than "everything", and is precisely the set of endpoints that are unauthenticated
  and security-sensitive: login, password reset, anonymous document upload, the partner reconcile
  endpoint.
- **The real question is ordering, not removal.** `DomainTenantResolveContributor` sets
  `Handled = true` unconditionally whenever the request has a Host, before returning its match
  result. So the `__tenant` resolvers are unreachable **if and only if** the custom
  `HostAwareDomainTenantResolveContributor` is ordered ahead of them. Whether anyone "removed" them
  is irrelevant. If it was registered with the natural `.Add(...)` rather than an insert, it lands
  last, and `?__tenant=` outranks the hostname on every anonymous request - after which
  `MultiTenancyMiddleware` persists that choice into a cookie.
- **There is a spec-legal path to the `__tenant` resolvers even when ordering is correct.** If the
  request has no Host value, the domain contributor returns *without* setting `Handled` and the
  chain continues. RFC 9112 section 3.2 requires a client to send `Host:` with an empty field value
  when the target URI has no authority, and Kestrel permits it.

Two more cross-tenant hazards belong in the same test, because they have the same blast radius and
the same silence:

- **The ambient null-tenant hazard.** `01` section 7 records that ABP falls back to the ambient
  tenant when a job argument carries none, and the ambient tenant in a worker is null, which
  resolves to the **host** database. Twelve jobs iterate offices; three run every fifteen minutes.
  That is office-shaped data being written into the wrong database, silently. The input document
  treats this as a correctness hazard. It is a tenant-isolation defect and deserves a hard guard,
  not a note.
- **Untyped cache writes.** `01` section 6 records that ABP's typed `IDistributedCache<T>` prefixes
  tenant-scoped keys with `t:{tenantId},` and that **untyped usage does not**. One untyped write of
  a permission grant or a settings blob is a cross-office authorisation leak that produces no
  error - it produces office A's user holding office B's permissions.

**Cost: hours, not weeks.** A curl matrix (`__tenant` as query string, route value, header and
cookie, against all three hostname shapes; an empty Host; a token minted at office A presented to
office B's API) settles the first. A grep and one integration test settles the third. All of it
should then become permanent automated tests, because every one of these failures is silent.

**2. Deny by default at the edge, and take the administrative plane off the internet entirely.**

`03` section 4.1 records that the reverse proxy contains zero `add_header` directives, no TLS
pinning, and no rate limiting of any kind. `03` section 4.2 records that there is no
`default_server` on 443 and no block for the bare apex, so an unmatched Host falls through to the
first server block - the AuthServer - and `api.<base>` without an office label is served by the
Angular container. `03` section 4.0 records an unauthenticated Hangfire dashboard mounted in the
production path with antiforgery disabled.

The single highest-value structural insight here, which the input documents do not state: **the
number of edge routing rules is four, and it does not grow with office count.** Every office
hostname resolves to the same three application processes plus the object store; the office is
selected inside the application. The edge does not do tenant routing, it does *product* routing.
That is why "adding an office is a host-UI action, not a deployment" is currently true, and
protecting it should be the first filter applied to any platform candidate.

The corollary settles a design argument that would otherwise be decided by whoever writes the
config first: **the edge validates hostname SHAPE, the application validates MEMBERSHIP.** An edge
allow-list of provisioned office hostnames would work and would destroy the property.

**3. One off-box, immutable, actually-restored recovery path - covering the object store, not just
the databases - with an alert on the absence of a recent verified restore.**

`03` section 2.6 records that the backup script silently failed for months on a directory-ownership
problem, that no cron schedule was confirmed installed, that the destination still defaults to the
same disk as the data, and that **no restore has ever been tested.** Redundancy is not the top risk
here. The top risk is that the system currently has no recoverable state at all.

Two findings sharpen this. First, **backup enrolment has to be part of provisioning, not part of a
schedule.** Point-in-time restore requires the FULL recovery model, recovery model is a
per-database property inherited from `model` at `CREATE DATABASE`, and a new office database is
created by a **business action in the host UI**. So whether an office can be restored to a point in
time is decided by the state of `model` at the moment a non-engineer clicked "add office", and
nothing surfaces it. Worse, switching to FULL takes effect only after the first data backup. *The
business can create a database faster than the backup system can notice it exists.*

Second, the ordering hazard: FULL recovery **without** log backups grows the transaction log until
the disk fills. On a box with roughly 9.7 GB free shared between the databases, the object store
and the backup directory, that is not a nuisance - it is a simultaneous outage of all three. Switch
recovery models and enable log backups as one atomic change.

**And the decision embedded in this one:** do not onboard a real practice until decisions 1 to 3
hold. Every property that makes this programme executable by two SDE 1 developers - free downtime,
destructive rehearsal in production, rollback by discarding rather than reconciling, no business
associate agreement on the critical path - is a property of the no-PHI window, and it is spent the
moment the first real booking exists. **The sequencing of the business is itself an architectural
decision, and it is the one that decides whether the other two are cheap or impossible.**

### The three things not to do

**Do not adopt a cluster orchestrator.** The benefits are real and are obtained by other means; the
cost - a control plane, an ingress controller, storage drivers, and a mandatory upgrade cadence -
lands on two SDE 1 developers with no DevOps engineer and no on-call rotation. Section 20.3.

**Do not put anything in the request path that touches the `Host` header.** Section 19.4 lists
eleven invariants and the patterns that violate each.

**Do not build for 33 offices.** The model is comfortable at 33 and the nearest platform limit is
roughly ten times further out. What binds is operational attention, at somewhere between 16 and 23
offices on a realistic budget - and it binds because the migration runner emits no per-office report
and there is no log aggregation, not because of anything about database-per-tenant. Fix the two
artefacts and the same budget carries 65 to 90 offices. Section 19.1.

### Read section 2 next

Section 2 is the uncomfortable part and it is short. The full recommendation set in this document
totals **734 to 1,084 developer-hours of setup and 24.5 to 43.5 hours a month of steady state**,
against a stated allocation of roughly one developer-week per month. **At the top of that range the
steady state alone exceeds the entire allocation, and at the bottom it still consumes more than half
of it** - before any setup and before any feature work. Section 2 says what to do about that, and
gives the list to execute if only 40 hours ever materialise.

---

## 2. The portfolio problem, and the list to execute if only 40 hours materialise

This section exists because the research produced it as a finding about itself. Fourteen areas each
sized their own recommendation honestly and each concluded it was affordable. Summed, they are not.

### The arithmetic

Adding up the per-area operational costs stated in sections 4 to 17 of this document:

| | Setup, one-off | Steady state, per month |
| --- | --- | --- |
| Sum of all fourteen areas **as sized in this document** | **734 to 1,084 developer-hours** | **24.5 to 43.5 hours** |
| Sum of the underlying research's own per-area figures | 742 to 1,090 | 35 to 57 |
| Stated allocation | ~40 hours a month | ~40 hours a month |
| Implication | **18 to 27 months with zero feature work** | **At the top of the range the steady state alone exceeds the whole allocation; at the bottom it still consumes more than half of it** - before any setup and before any feature work |

The two rows differ because several areas were sized down in this document relative to the research
that produced them: the standing second environment is deferred, the cold audit archive is
de-obligated in section 21.8, and the observability ongoing cost was cut to the alert set that will
actually be read. **The lower row is what this document commits to. The upper row is what the
research thought it was recommending**, and the gap between them is roughly the scope this document
already cut.

The stated allocation is itself an estimate that has never been confirmed against a staffing
commitment, and the code-standard exercise that preceded this one already claimed 141
launch-blocking hours of the same budget.

**Say this to the business as a decision rather than letting the calendar decide it.** The three
options are the usual three - move the date, add capacity, or cut scope - and only the third is
free. This document cuts scope below; the business decides whether that is the right cut.

### The risk this creates, named because it is created by the analysis rather than found by it

**Partial adoption in an unplanned order is the most likely outcome, and six of the fourteen areas
independently identified partial adoption as worse than doing nothing.** Concretely: a half-landed
deny-by-default policy that breaks sign-in on a system with no staging; a half-executed dependency
move with state in one place and compute in another; a staging environment built before the deploy
is fail-fast; an archive written but never verified; capacity data collected but never read;
backups produced but never restored.

Several areas each claim the "do this first" slot. Without an adjudicated cross-area sequence, the
order gets chosen by whichever document a developer opens first. So here is the adjudicated
sequence.

### If only 40 hours ever materialise, do exactly these

Ordered. Each is independently valuable, independently abandonable, and leaves the system strictly
better if the next item never happens.

| # | Item | Hours | Why this one |
| --- | --- | --- | --- |
| 1 | **The tenant-boundary curl matrix**, plus a grep for untyped `IDistributedCache` writes | **2-4** | The only failure whose consequence is a cross-practice PHI disclosure. If it fails, stop and fix it; nothing else on this list matters until it passes |
| 2 | **One `df` sample a day, recorded** | **1** | Yields a projected disk-exhaustion date within two weeks. It is the most decision-relevant number the team could hold, and audit growth is unbounded and unpruned on roughly 9.7 GB free |
| 3 | **`${VAR:?message}` on every compose variable**, plus `ValidateOnStart()` on the load-bearing options classes in all three processes | **8-12** | Converts the system's most-repeated operational incident - a deploy that starts successfully with blank secrets - from silent to refused. Structural, not procedural: it keeps working when the budget goes to zero |
| 4 | **Narrow `EntityHistorySelectors` and set `SaveEntityHistoryWhenNavigationChanges = false`** | **8-14** | The cheapest order-of-magnitude available. Every downstream cost - storage, backup time, restore time, archive size - is linear in the audit ratio. Do this before separating any store, or the new store is sized wrong |
| 5 | **Backups off the box, under a credential that can write but not delete** | **8-16** | Today backups default to the same disk as the data. This is the difference between having backups and believing you have them |
| 6 | **One timed restore of one office database, recorded** | **8-12** | An untested backup is not a backup. Assume the first attempt fails; that is the point of doing it now, while every record is synthetic |
| 7 | **`default_server` on 443 that terminates unmatched Host, and no `/hangfire` route at the edge** | **3-5** | Closes the bare-apex fallthrough to the AuthServer and takes an unauthenticated administrative console off the public internet, independently of the planned code fix |

**Total: 38 to 64 hours.** Items 1, 2 and 7 together are under ten hours and remove the three risks
that can end the business. Items 3 and 4 are the two structural fixes with the best ratio in the
whole document.

### And if two quarters materialise rather than one month

The 40-hour list is the floor, not the plan. If the business commits roughly **120 to 160 hours over
two quarters** - which is what a real one-developer-week-per-month allocation actually buys once the
code-standard exercise is accounted for - the selection principle changes, and it is worth stating
because it is not "do the next seven items".

**Choose structural fixes that carry zero monthly upkeep, and explicitly defer everything that
creates a recurring obligation.** A control that needs a human every month is not a control this
team has; it is a control that will exist for one quarter and then quietly stop. The corollary
matters more than the rule: **anything deferred must be NAMED as deferred**, so that nobody later
believes a control exists when it does not.

On that principle, the two-quarter programme is the 40-hour list plus:

| Item | Hours | Why it qualifies |
| --- | --- | --- |
| Fix both deployment traps at their mechanism (section 13, items 1 and 2) | 5-10 | Deletes two manual steps permanently. Zero upkeep |
| The single gated deploy entry point | 4-6 | Structural. A script cannot forget a flag |
| Split the job processing server into its own process | 8-12 | One flag, one container definition. Zero upkeep, and it stops being cheap once the topology grows around it |
| Per-office fan-out reporting in the migrator | 8-14 | The one artefact that moves the operational ceiling from ~20 offices to ~90 |
| Rotate every credential, and split the provisioning identity from the runtime identity | 12-20 | Free now, an incident later |
| Move the DataProtection key material to the host database (**after** off-box backups) | 12-16 | Removes an entire class of platform requirement. Zero upkeep |
| Administrative plane onto an unpublished listener | 6-11 | A property of a binding, not of a path. Zero upkeep |
| Per-office database encryption at provisioning | 12-20 | **The only item with a hard expiry** - crypto-erase is conditioned on the data never having been stored in plaintext |
| Tenant lifecycle ledger | 20-30 | One artefact, three problems: offboarding, resumable provisioning, and the split-schema hazard |

**That is roughly 125 to 175 hours and almost all of it is one-time.** What it deliberately defers,
and what must therefore be recorded as absent rather than assumed: log aggregation, the standing
second environment, the cold audit archive, the export writer, and the full deny-by-default
authorisation policy.

### What is deliberately not on that list, and why

- **The alerting set.** It is genuinely important and it is second-tier here for an honest reason
  covered in section 21.7: there is no on-call rotation, no escalation path and no second
  responder. An alert that fires into an unread inbox is a control that has already failed.
- **The second environment.** It is a MUST in Document B and it is not in the first 40 hours,
  because production is a legitimate destructive-test target *right now* and will not be later. The
  no-PHI window is worth more spent on drills in production than on building a second environment
  to drill in.
- **Anything requiring a platform decision.** Log destinations, managed services, immutable archive
  storage. Those are the next exercise's outputs and should not be pre-committed here.

---

## 3. Where the input documents are wrong, incomplete, or understate something

The brief asks for this explicitly. The two input documents were verified by direct source reads on
2026-08-28 and are unusually good; every item below is a refinement, not a repudiation. Each names
the check that settles it.

| # | What `01` or `03` says | What the research establishes | Why it matters | Settles it |
| --- | --- | --- | --- | --- |
| 3.1 | `01` s3.1: "ABP registers four `__tenant` resolvers by default (query string, header, cookie, route)" | **Five**, and the first is `CurrentUserTenantResolveContributor`, which reads the tenant claim from the access token and is inserted at index 0 by `AbpMultiTenancyModule` before the ASP.NET Core module adds the other four | For an authenticated request, tenancy is decided by the **token claim**, not by Host. That reframes the whole question: Host-header tenancy governs anonymous requests. Also means a token minted at office A must be proved to fail at office B's API - a test nobody has listed | Read the module registration order; run the cross-office token test |
| 3.2 | `01` s3.1: treats the caveat as "are the four disabled?" | The correct question is **ordering**, because `DomainTenantResolveContributor` sets `Handled = true` unconditionally whenever the request has a Host. If the custom resolver is ordered first, the others are unreachable whether or not anyone removed them | Changes the fix from "remove resolvers" to "assert an ordering invariant", which is a five-line test rather than a configuration audit | Read the insertion call for `HostAwareDomainTenantResolveContributor`: `Insert(0, ...)` or `.Add(...)` |
| 3.3 | Neither document mentions the empty-Host case | An empty `Host` field value is **RFC-legal** (RFC 9112 s3.2, for a target URI with no authority) and Kestrel permits it. The domain contributor then returns without setting `Handled` and the chain falls through to the `__tenant` resolvers | A spec-legal bypass that survives correct resolver ordering. Belongs in the edge design as well as the application | Send a request with an empty Host through the full path |
| 3.4 | `01` s7 frames the ambient-null-tenant fallback as "a known correctness hazard" | It is a **tenant-isolation defect**: a tenant-scoped job whose argument carries no tenant writes office-shaped data into the host database, silently, on a fifteen-minute cadence | Same blast radius as the resolver question; a fraction of the attention | Add a hard guard that refuses to run rather than resolving to host, then look for what fails |
| 3.5 | `03` s3 constraint 2: health checks "must use a real office host (`admin.api.<domain>` for host scope) rather than a bare hostname or IP" | Correct, and it collides with the edge hardening in the same document. A probe cannot simultaneously carry an IP as Host and be rejected for not being an office hostname | Four different health surfaces are implied across the input documents with no agreed taxonomy. Section 21.4 adjudicates | Define liveness, readiness, external synthetic and administrative UI as four distinct surfaces |
| 3.6 | `03` s4.3 treats SQL Server edition as a licensing and cost decision for the hosting exercise | It is a **go-live blocker with architectural consequences today**. Developer edition carries all Enterprise functionality but may not be used in production, so everything the team has built and measured has run with Enterprise features silently available. On Standard, Resource Governor is unavailable, availability groups are limited to basic AGs carrying one database each, and online page and file restore is unavailable | The team may be depending on features that vanish at go-live, with no error. Also: Express is excluded not by its 10 GB per-database cap - which looks generous under database-per-tenant - but by a **per-instance** 1,410 MB buffer pool shared across all 34 databases, and by the absence of native backup to object storage over a standard REST interface | Run `sys.dm_db_persisted_sku_features` on every database before choosing an edition |
| 3.7 | `03` s2.6 describes backup as "works, but is not yet a backup strategy" | Understated in one specific way: **recovery model is inherited from `model` at `CREATE DATABASE`, and office databases are created by a host-UI action.** An office provisioned while `model` was SIMPLE has no point-in-time capability, forever, and no error is ever raised | Backup enrolment must move into `ProvisionAsync`, not a schedule | Query `sys.databases.recovery_model_desc` across the fleet |
| 3.8 | The six-year retention expectation is carried through both documents as applying to audit data | **45 CFR 164.316(b)(2)(i) requires six-year retention of the documentation required by 164.316(b)(1)** - the written policies and procedures implemented to comply with the Security Rule, and written records of actions, activities or assessments the subpart requires to be documented. An application audit log is neither. **164.312(b) states no retention period at all.** NIST SP 800-66r2 places the six-year specification in its Documentation section and, in its Audit Controls section, names no retention period, making it a risk-based decision | This is the largest single sizing input in the whole exercise and it was an interpretation, not a regulation. See 3.9 for the obligation that *is* real | Read 164.316(b)(1) and (b)(2)(i) together |
| 3.9 | Neither document mentions 45 CFR 164.528 | **There is a real six-year data obligation and it is a different, much smaller artefact.** 164.528(a)(1) gives individuals a right to an accounting of disclosures for the six years prior to the request. Its exclusion list does **not** exclude 164.512(l), disclosures for workers' compensation - **which is this platform's core business** | The correct design is a purpose-built, append-only **disclosure ledger** as first-class domain data in the operational database, retained six years. Tiny, business-meaningful, and it survives audit pruning. This is a finding nobody was looking for | Read 164.528(a)(1) and its exclusions against the workers'-compensation disclosure flow |
| 3.10 | `01` s7 lists `DisableGlobalLocks = true` among the Hangfire settings without comment | It affects **only** `SqlServerWriteOnlyTransaction.AcquireLock`, not `SqlServerConnection.AcquireDistributedLock`. Recurring-job deduplication is unaffected by it, and directionally it makes multiple servers behave **better** by removing a serialisation point | The intuition that it weakens multi-instance safety is backwards, and that intuition would otherwise block the one change that unblocks horizontal scale | Read the two `AcquireLock` implementations |
| 3.11 | `03` s2.4 trap 2 describes the nginx upstream-caching trap as a deployment nuisance | It is also **the binding blocker on running a second API instance**, and the most insidious one: nginx never learns the second container's address, so it receives zero traffic, the deploy looks successful, and every conclusion drawn about whether multi-instance works is drawn from a system still running one instance | Elevates a runbook step to a design requirement (REQ-REL-05) | Add a second API container and check whether it receives any request at all |
| 3.12 | `03` s4.6 notes ABP audit logging is present and "whether its retention satisfies policy is an open question" | **ABP ships no cleanup mechanism in the framework** - `IAuditLogRepository` has no delete-by-date method. ABP Commercial does ship periodic cleanup, but it is inert until someone enables "Cleanup Service System Wide" at host scope, and that settings tab is itself hidden behind a feature flag. **A developer reading only code will never find it** | The retention machinery is already bought and switched off in a place that is invisible from the source tree | Open the host-scope settings under Audit logs, Global |
| 3.13 | `01` s5 and `03` s2.3 record the memory caps | Summing the container ceilings exceeds physical RAM. That shows the caps cannot all be honoured at once; it does **not** establish current over-commitment, because `MSSQL_MEMORY_LIMIT_MB` of 7168 inside a 10240 MB cap shows caps are not usage estimates | Stated because a conclusion that "a second stack cannot run on this VM" would rest on a category error. Measure resident memory before asserting it | `docker stats` over a business day |
| 3.14 | `03` s6.6 says the stack is "portable to any Docker-capable host" | True, with one qualification the document does not make: the open-source server for the current object store is archived upstream with no patch stream, while three parts of this design would add new dependencies on it (backup target, migration transport, partner surface) | The object store is simultaneously the system's largest unprotected asset - it holds the actual medical documents, has no backup design, sits on the same disk, is published to the internet, and is authenticated to as root - and its least maintained component | Check the upstream repository status before adding dependencies on it |

**One thing both documents get right that is worth restating**, because it is load-bearing and easy
to lose: `03` section 6.3 records that only two databases exist today and ten of the eleven launch
tenant databases have never been created. **There is almost nothing to migrate.** That is what makes
the migration a build rather than a move, and it is the single biggest cost saving available in this
whole programme.

---

## 4. Area 1: Tenancy and data architecture

**Current state.** One tenant equals one office equals one SQL Server database. Two databases exist
today; the launch target is 11 and the ceiling is 33. Connection strings are **derived** by swapping
the database name in a template, so the whole fleet relocates to a different instance by changing
one configuration key. Adding an office is a host-UI action calling an idempotent provisioner. Two
DbContexts (host: 46 DbSets, 90 migrations; tenant: 44 DbSets, 15 migrations) share a base and a
shared model configuration, so an entity mapped in both needs a migration in both.

### The viable options

| Model | Isolation | Operational cost | Where it wins |
| --- | --- | --- | --- |
| **Silo: full stack per tenant** | Strongest | N of everything. Vendor guidance rates it for 1 to 100s of tenants | High-value tenants with contractual isolation requirements |
| **Database per tenant** (current) | Strong partitioning; per-tenant restore and blast radius | N databases to migrate, back up, restore, credential and monitor. Vendor guidance rates it for 1 to 100,000s of tenants | Few to many tenants, per-tenant restore required, isolation as a failure-mode property |
| **Schema per tenant** | Between the two below | Inherits the migration fan-out cost of database-per-tenant while giving up the backup, restore and relocation granularity that justifies it. Largely absent from first-party guidance | Rarely the right answer; it is the worst of both |
| **Shared schema with a discriminator** | Weakest. Vendor guidance states plainly that it "necessarily sacrifices tenant isolation" and that the engine has no built-in way to monitor or manage per-tenant resource use | Lowest: one database to migrate, back up, monitor and patch | Many small tenants, cost-dominated, low isolation requirement |
| **Sharded pools** | Between the middle two | Adds a shard map, split/merge tooling and a mandatory tenant-leading key. Rated for 1 to 1,000,000s | Hundreds to thousands of tenants |

### Recommendation: keep database per tenant, unchanged - but understand why

**The model is right, and the reason usually given for it is wrong.** First-party cloud architecture
guidance draws a distinction the model itself obscures: *data partitioning describes how tenant data is stored, and
partitioning does not by itself achieve isolation.* Isolation is a separate enforcement concern,
distinct from authentication and authorisation - a user can be fully authenticated and authorised
and still reach another tenant's data.

**This system has excellent partitioning and thin isolation.** Every office database is reachable
from the same process, over the same superuser login, and the only thing between an authenticated
user of office A and office B's records is which string a C# resolver returns. That is why
section 1's first decision is what it is.

So the argument for keeping the model is not "it gives isolation". It is narrower and stronger:

1. **It is the only option on the board whose failure mode survives an ordinary coding mistake.** A
   missing tenant migration surfaces as an invalid-object-name exception on the next request into
   that office - noticed within minutes. The equivalent defect under a shared-schema discriminator
   surfaces as one practice's injured worker appearing in another practice's appointment list -
   noticed never. With no QA function, that is worth more than density.
2. **Per-office restore is a stated requirement** (section 19.5) and is nearly free here and nearly
   impossible under a shared schema.
3. **The blast-radius property is already banked**: one office database failing affects that office
   only.

**What needs to change is not the model. It is the four artefacts around it**, in this order:

1. **Prove and pin the resolution boundary** (section 1, decision 1), then make it a permanent test.
2. **Split the provisioning identity from the runtime identity.** This is the correct fix for the
   shared superuser login, and the naive fix breaks onboarding. The causal chain matters:
   office-onboarding-as-a-UI-action forces runtime `CREATE DATABASE`, which requires elevated
   permissions, which produces a single powerful account. **Anyone who treats that account as
   sloppiness and simply demotes it will break the ability to add an office.** Give the provisioner
   DDL rights and the runtime DML only, as two identities.
3. **Make the migration fan-out report per-office outcome and exit non-zero on any failure**, and add
   a CI check that fails the build when either DbContext's model has changes not represented by a
   migration. The framework ships a first-party command for exactly this; it is one line in CI
   against a hazard the input document describes as producing runtime exceptions.
4. **Set an explicit connection-pool ceiling** on the derived template, and add an application name
   so per-office sessions are attributable in the engine's own views.

**The transferable lesson from the largest published database-per-tenant deployments** is not that
the pattern is free. The most-cited example does report roughly two dedicated engineers - operating
a purpose-built control plane that automated database lifecycle, pool rebalancing, backup retention
and point-in-time restore. **A fleet becomes tractable exactly when you stop operating databases and
start operating one report about them.** The proportionate version of that here is a table of tenant
slug against applied migration identifier. That is the whole ask.

**Operational cost.** Setup 36-58 hours: proving and pinning the boundary 6-10 (the manual test is
under an hour; the durable automated version across three hostnames and four resolver channels is
the rest); splitting the identities 12-20 (the largest item - it touches the provisioner, the
migrator, both DbContexts and the secrets file, and needs a rollback plan); per-office fan-out
reporting plus the CI model check 8-12; connection-string hardening 2-4; one documented, timed
restore 8-12. Ongoing 3-5 hours a month at 11 offices.

**What would change the answer.** Tenant counts in the hundreds, or a business model shift to many
small low-value tenants. Neither is on the table. **What would change it sooner:** if the boundary
test in decision 1 fails, the model's central claim is not real and the priority inverts from
architecture to remediation.

---

## 5. Area 2: Audit and retention architecture

**Current state.** ABP audit logging writes audit-log rows and entity property-change rows into the
same database as operational data, per office. Retention is unconfigured and nothing prunes it.
Measured: 16 appointments produced roughly 1,450 audit-log rows and 2,689 entity property-change
rows - about **259 audit rows per appointment**, and that counts only two of the **five** tables in
the audit schema.

### Recommendation: five things, in this order, and the order is the recommendation

**1. Fix generation before fixing storage.** ABP does not track entity property changes by default;
its documentation is explicit that the audit system saves no entity change unless explicitly
configured. The 2,689 rows exist because something enabled all entities. Replace that with an
explicit allow-list of the aggregates whose changes are genuinely evidence of PHI handling -
appointment, claimant, document, packet, consent, the 13-state lifecycle transitions - and exclude
catalogues, settings and outbox rows. Check whether entity history is being saved on navigation
changes, which defaults to on and is a prime suspect for the property-change count.

**This is the cheapest order of magnitude available, it is a configuration change with a test, and
every downstream cost is linear in the ratio.** Do it before separating any store, because
separating a store you are about to shrink by a large factor sizes the new store wrong.

**2. Make audit-write failure loud.** Audit errors are hidden by default, and the audit store writes
on a separate non-transactional unit of work that begins after the business transaction has
committed. Together those mean **a business transaction can commit while its audit record silently
does not**, downgraded to a warning in a console log that rotates away. For a system claiming
164.312(b) compliance that is the single most dangerous default present.

Do **not** simply turn error-hiding off - that converts an audit outage into a total outage. Keep it
on and add a nightly per-office assertion: if the newest audit row is older than N hours while the
API served traffic, fail the job loudly. Fail obviously; do not degrade.

**3. Turn on the retention machinery already bought, after writing the policy down.** ABP Commercial
ships periodic audit-log cleanup and it is inert until enabled at host scope, behind a settings tab
that is itself feature-gated. **Enabling cleanup before a written retention decision exists is
destruction of evidence, not housekeeping** - so the policy document comes first. And the policy
document is the artefact that 45 CFR 164.316(b)(2)(i) actually obliges you to keep for six years.

**4. Move the audit tables into a per-office audit database.** ABP's audit DbContext already carries
its own named connection string, and the audit store already opens its own unit of work after the
business one commits, so this introduces **no distributed transaction**. Extend the existing
connection-string provider to be name-aware - it already swaps database names - and extend the
provisioner to create two databases instead of one.

**This is what answers the backup question.** Per-database backup means the operational backup stops
carrying audit the moment the tables move, with no filegroup surgery and no edition-gated features.
And per-office restore then requires no technique at all: **you restore the operational database and
simply do not restore the audit database.** That is the decisive property.

**5. Cold retention and immutability belong on exports, never on live tables.** Monthly per-office
encrypted exports into object storage with server-side retention the writer cannot shorten, plus a
manifest of row counts and digests, plus a verification job, plus one rehearsed cold restore a year.

### What is rejected, with reasons

| Rejected | Why |
| --- | --- |
| **One central audit store across all offices** | Creates a single store holding every office's activity in a system whose entire isolation argument is per-office separation. A tenant-isolation regression bought for operational convenience |
| **Cryptographic tamper-evidence on the live audit tables** | Puts integrity machinery on the copy you are about to prune, constrains schema evolution, and does nothing for the copy that has to survive a dispute. Integrity belongs on the archive |
| **Filegroup separation with piecemeal restore** | Solves the same problem with a technique that requires SQL administration skill this team does not have, and whose restore path differs by recovery model in ways that are easy to get wrong |
| **A log or SIEM platform as the primary audit store** | Makes the regulatory record depend on an operational tool with operational retention. Ship audit *to* one if useful; do not make it the system of record |
| **Separate schema only** | Same database, so backup and restore still carry it. All of the migration cost, none of the benefit |

**Operational cost.** Setup 90-146 hours: narrowing capture 8-14; the loud freshness assertion 6-10;
enabling cleanup plus writing the policy 6-12; the per-office audit database 25-40; the export,
immutability, manifest and one cold-restore rehearsal 45-70. **Steps 1 to 3 total 20-36 hours and
remove the disk-exhaustion risk on their own; if only one month is ever available, do those.**
Ongoing 2-4 hours a month plus about 4 hours a year for the cold-restore rehearsal.

**One cost stated honestly:** step 4 doubles the database count, from 34 to about 68 at 33 offices.
That collides with section 19.1, where fleet size is what binds first. It is still right, because
the audit databases need no schema evolution of their own and because the restore property is worth
more than the count - but it makes the per-office fan-out reporting in Area 1 a hard prerequisite
rather than a nice-to-have.

**What would change the answer.** A legal determination that audit records must remain queryable
online for the full six years. That would not change the separation; it would change the archive
from cold to warm, which is a cost decision rather than an architectural one.

---

## 6. Area 3: Scaling model and topology

**Current state.** Everything runs single-instance. `HttpApi.Host` hosts the Hangfire processing
server; the AuthServer sets job execution to false.

**The scaling unit today is the whole VM**, because every stateful dependency is single-instance and
co-located. The unit the design already implies is a **cell** - a stack hosting K offices - and the
team unknowingly owns the expensive prerequisite for it: the derived connection string relocates the
fleet with no code change, and Host-based tenancy means a cell can own a DNS suffix. What is missing
is *per-office* placement, because the template is global. **That gap, not replica count, is the
thing to close before growth forces the issue.**

### Recommendation: do the role split and stop there

Make "runs the job processing server and the recurring workers" an explicit configuration flag, run
exactly one process with it enabled, and spend the remaining hours on retention, a tested restore
and off-box backups - not on a second web replica.

**Why not a second replica yet, and the honest reason is not "too hard".** It is that adding one
makes reliability *worse per hour spent*. It converts a five-single-point-of-failure system into a
four-SPOF system while introducing a class of failure invisible to a team with no aggregation, no
metrics, no APM and no alerting. The narrowest tier is the two-worker packet renderer and the SMTP
relay - the Hangfire worker pool is already pinned because bursts oversubscribe them - so more API
instances widen nothing. There is no evidence of a load problem. Vertical headroom is vast; the
nearest hard ceiling is disk, driven by unpruned audit.

**The strategic point is that the role split buys the option to go horizontal later as a
thirty-minute decision rather than a project**, at a cost of well under a day now.

**Three things people expect to block horizontal scale and which do not:** the migration runner
already serialises through a database-wide lock, so simultaneous migrators are safe; the recurring
scheduler already takes a global lock, so recurring jobs do not simply fire N times; and the
distributed lock is currently decorative, with no application acquisition sites.

**Operational cost.** Setup 45-70 hours across the whole area, of which the role split with a smoke
test proving exactly one processing server registers is 8-12. Ongoing 4-6 hours a month.

**What would change the answer.** A measured saturation, or an availability commitment a single
instance cannot meet. Neither is known, because no load test has ever been run.

---

## 7. Area 4: State and session architecture

**Current state.** One Redis instance, AOF persistence, 512 MB cap, carrying three jobs: distributed
cache, the DataProtection keyring shared between two processes under one application name, and a
distributed lock.

### Recommendation: split the state by durability class, and do it now while every record is synthetic

**The three jobs have contradictory loss functions, and only one of them is unrecoverable.**

| Job | Loss function | Correct behaviour |
| --- | --- | --- |
| **Distributed cache** | Self-healing. Cache errors are hidden by default and the application falls back to the source | Degrade and repopulate. Already correct |
| **Distributed lock** | A bounded correctness window, and there are no application acquisition sites today | Tolerable |
| **DataProtection keyring** | **Unrecoverable and total.** A deleted key makes protected data permanently undecipherable with no override, and losing the ring logs every user out and breaks in-flight email-confirmation links | Must not be co-tenanted with disposable data |

One of these is not like the others, and it is currently stored as a single un-expiring key sharing
a keyspace with cache entries in a store whose whole value proposition is eviction.

**Move the DataProtection keyring to the host database.** A dedicated context that is explicitly
non-tenant-aware and pinned to the host connection string. The reason to prefer this over a
backed-up volume is not elegance - **it is that the host database is the only thing this team
actually backs up, so the ring rides along with no new artefact anyone has to remember.**

Three traps to handle deliberately: the context must not resolve its connection string through the
current-tenant abstraction, or the ring lands in an office database when first loaded inside a
tenant-scoped request; the table belongs in the host migration set only, which interacts with the
two-DbContext hazard; and the keyring must be encrypted at rest with an explicit mechanism, because
specifying a custom persistence location deregisters the default at-rest protection and stores keys
in the clear.

**Then, and only then, make the cache genuinely disposable**: an explicit memory ceiling below the
container cap, an eviction policy, and persistence off.

**The ordering here is not optional and it is the single most dangerous sequencing error in this
document.** Applying an eviction policy before the keyring has moved silently destroys DataProtection
keys under memory pressure. The failure presents as users being intermittently logged out, weeks
later, with nothing to correlate it to.

**Operational cost.** Setup 12-16 hours - under half a developer-week, and the smallest
high-consequence item in the document. Key migration is **free right now** because a mass logout
costs nothing while all data is synthetic; later it is a coordinated event. Ongoing 0.5 hours a
month: the ring self-rotates, and the only recurring obligation is watching one certificate expiry.

**What would change the answer.** If the platform exercise produces a managed key or secret store,
that becomes the better home for the ring and this becomes a connection-string change. **Do not wait
for it** - the host-database version is cheap, reversible, and available today.

---

## 8. Area 5: Availability and disaster recovery

**Current state.** One VM, one of everything, no replication or failover. Backups run natively per
database to a directory that **defaults to the same disk as the data**. The script silently failed
for months on a permissions problem. No cron schedule was confirmed. **No restore has ever been
tested.**

### Recommendation: recoverability before redundancy, and derive the numbers rather than assert them

The current risks in order, and redundancy is fourth:

1. **No tested restore.** The probability the backup does not work is not small; it demonstrably did
   not work for months. Cheap to fix.
2. **Backups on the same disk as the data.** A disk failure loses both. Cheap to fix.
3. **No alerting.** An outage is discovered by a customer. Cheap to fix.
4. **Single instance.** A host reboot is a full outage. **Expensive to fix, and it is fourth.**

**Build:** every database on an explicit full-plus-log schedule with an integrity checksum, written
off-box under a **write-only** credential into storage with retention the writer cannot shorten;
per-office point-in-time restore scripted as a first-class operation; and **a weekly automated
restore-and-integrity-check of one rotating database, behind an alert that fires on the ABSENCE of a
green result.** That absence alert costs about two hours and is what converts this from a control
that decays silently into one that fails loudly.

**Do not** build multi-region, synchronous replication, or per-database availability groups. Section
20 covers the first; the third deserves a specific note here because it looks attractive and is a
trap: where the only available availability-group form carries **one database per group**, 34
databases means 34 groups failing over independently, and every office resolves through **one**
server address with the database name substituted in. After a partial failover, the offices that
moved are unreachable and the ones that did not are fine. That is close to the worst possible
failure shape. **High availability for this fleet has to be instance-level, because instance-level is
the only granularity the derived-connection-string design can express** - and the derivation is
worth more than the availability group.

**On the numbers, and this is deliberate:** do not let an engineer assert a recovery objective.
Maximum tolerable downtime is a business decision, elicited from whoever owns the scheduling
business. For this business it has a specific and answerable form: **can the front desk run today's
booked appointments from a printed schedule and a phone?** You need that answer anyway, because an
emergency-mode operation plan is a required implementation specification under 164.308(a)(7). If
yes, tolerable downtime is a working day or more and a cold-standby posture is proportionate. If no,
because ex-parte notice deadlines or same-day packet assembly bind, it collapses to hours.

Then **replace both numbers with a measured one**: the elapsed wall-clock of a per-office restore
performed by one developer against the written runbook *is* your recovery time objective.

One non-obvious input to the recovery point: business records lost in a gap are re-keyable at this
data density; **audit records are not.** So the recovery point should be set by audit
reconstructability, not by booking volume - a consideration that changes once audit moves to its own
database under Area 2, and which section 21.6 adjudicates.

**Operational cost.** Setup 50-90 hours, staged: recovery models plus schedules 8-12; off-box target
8-16; per-office restore script and runbook 8-16; automated weekly verification with the absence
alert 8-16; the written contingency-plan artefacts 8-16. Ongoing 3-6 hours a month.

**What would change the answer.** A business answer requiring better than a few hours. That would
justify a warm standby, and it should be priced as a decision rather than assumed.

---

## 9. Area 6: Environment strategy

**Current state.** Exactly one environment. The `staging` and `production` branches have not moved
since 2026-05-01 and are 600+ commits behind. Any change reaching users was tested only on the
machine that serves them.

### Recommendation: do not build a standing staging environment yet. Build these four, in order

**Step 1 - make the one environment you have reproducible and fail-fast (8-12 hours, once).** The
documented behaviour of the compose tooling is that an unset variable substitutes as an empty
string, which is the entire mechanism behind the `--env-file` trap. The fix is one character per
variable: a required-with-message form that exits with an error when unset or empty. Add a
configuration-validation gate before deploy, capture the resolved-digest output as the release
artefact so image tags cannot drift underneath you, and wrap the whole deploy - env file,
digest-pinned artefact, and the nginx force-recreate - in **one script that is the only supported
way to deploy.** Zero monthly cost, and it converts both known-fatal traps from silent to loud.
**This is the highest value-per-hour change in this area.**

**Step 2 - an ephemeral test harness in CI with three offices plus the host (24-40 hours).** Real
database and cache started per test run, running the real migrator and the real provisioner. **It
has no drift because it is created from the artefact every time**, which is what a standing staging
environment cannot promise.

**Step 3 - spend the no-PHI window on drills, in production (32-40 hours, and it expires).** Right
now production is a legitimate destructive-test target because every record is synthetic. Run: a
full restore rehearsal with a measured elapsed time; a 33-office provisioning drill followed by one
sweep cycle, then teardown; a deliberate mid-loop migrator failure against a broken office; a
signing-certificate rotation; a cache flush to observe session invalidation. **Record every timing.**
This produces the contingency-testing evidence that 164.308(a)(7)(ii)(D) requires, at zero PHI risk,
and it is the only time you will ever be allowed to do it in the real environment.

**Step 4 - a canary office in production (6-8 hours).** One office, real slug, synthetic data,
smoke-tested after every deploy.

**A standing second environment is step 5 and is deferred**, because it costs its own registrable
domain, its own three-depth wildcard certificates and its own DNS automation - all of which must be
maintained permanently even though the environment is not. **The `staging` branch sitting 600
commits behind is the evidence for what happens otherwise: the environment concept already existed
here and decayed.**

**On data, decide before it becomes convenient to do it wrongly:** once real practices go live,
"refresh staging from production" becomes a PHI transfer requiring production controls. The cheap
answer is a synthetic seed, and the seed already exists.

**Operational cost.** Setup 70-100 hours for steps 1 to 4. Ongoing 4-8 hours a month. Step 5 adds
24-32 hours of setup and pushes ongoing to 8-12. **Threshold: the recommendation is operable at
10-14 hours a month. Below 8, drop step 4 and keep steps 1 to 3.**

**What would change the answer.** If the business keeps the internal deployment running in parallel,
it becomes the second environment for free - but only if kept current, which is the same problem in
a different place.

---

## 10. Area 7: Network and exposure architecture

**Current state.** A TLS-terminating reverse proxy is the only service publishing host ports. The
config has **zero** response-header directives, no TLS pinning, no rate limiting, no WAF, no
`default_server` on 443 and no block for the bare apex. Application-tier rate limiting covers four
paths, and **sign-in is served by the AuthServer, a separate process that registers no limiter at
all** - whose Razor pages call application services in-process, so API middleware never runs for
them.

### The structural insight that should drive the whole edge design

**The number of edge routing rules is four, and it does not grow with office count.** Every office
hostname resolves to the same three application processes plus the object store. The edge does not
do tenant routing; it does **product** routing. That is why adding an office is a host-UI action,
and it is the single most valuable property in the design. **Protecting it should be the first filter
applied to any platform candidate - ahead of throughput, ahead of availability, ahead of everything.**

The corollary settles the allow-list argument: **the edge validates hostname SHAPE and depth; the
application validates MEMBERSHIP.** An edge allow-list of provisioned office hostnames would work,
and would mean a new paying office gets "Tenant not found" until somebody edits nginx.

### The second thing being conflated

"Tenancy resolves from the Host header" is **two** properties, and they need different treatment:

- **Routing**: the request must arrive with Host intact. An infrastructure requirement, satisfied by
  preserving Host.
- **Trust**: the application selects which database it talks to using a client-controlled string.
  A security posture, and an unusual one. Vendor architecture guidance that otherwise argues *for*
  host-name preservation states flatly: never use the value of the host in a security mechanism,
  because the user agent provides it and a user can change it.

That does not make the design wrong - selecting a tenant by hostname is how most multi-tenant
software works, and the same guidance endorses passing the incoming host name unmodified for
multi-tenant deployments precisely because they cannot statically define one domain. **What it means
is that hostname selects the tenant and the token must independently authorise it.** That is the
cross-office token test in section 1.

### Recommendation, in risk order

1. **A default deny at 443** that terminates unmatched SNI and Host, closing the bare-apex
   fallthrough to the AuthServer and the unprefixed-`api` fallthrough to the SPA.
2. **Rate limiting at the edge, not only in the application.** The two-process split is structural:
   a limiter in one process does not protect the equivalent flow in the other. **The edge is the only
   place that sees both**, and four of the five surfaces that need it - sign-in, password reset,
   registration, consent response, anonymous upload - are on the AuthServer or reachable
   anonymously.
3. **Security response headers and TLS pinning**, set at the edge so they apply uniformly to both
   processes and the static SPA. The content security policy is the one that takes real effort
   against an Angular SPA and is worth it.
4. **A WAF, evaluated against Host preservation before anything else about it is evaluated.** A
   wildcard tenancy scheme produces an unbounded set of legitimate Host values, and rulesets that
   normalise or reject unusual host headers will break offices at random.
5. **Segmentation so the data zone has no route from the edge zone** - true today by accident of the
   container network; make it true by design so it survives a platform move. Concretely: the
   static-file container should not be able to open a socket to the database.

**On health checks, because this is where fact 1 becomes an operational problem.** A bare IP returns
"Tenant not found", so every probe must send a real Host. Section 21.4 gives the four-surface
taxonomy that resolves the contradiction between "probe with an IP Host" and "reject any Host that
is not an office hostname".

**Operational cost.** Setup 30-45 hours: edge hardening 10-14; Host allowlisting by shape plus
stripping alternative host headers 4-6; rate-limit zones covering both processes 3-5; certificate
automation for the per-depth wildcards with an expiry alert 6-10; administrative de-routing 2-4;
network segmentation 4-6; a tenant-free liveness path in both processes 2-4. Ongoing 1-3 hours a
month, mostly certificate watching and reading limiter counters.

**What would change the answer.** A business decision that staff stay on VPN and only external-party
surfaces go public. That shrinks the anonymous surface to registration, login, consent response and
upload, and makes item 4 optional rather than important.

---

## 11. Area 8: Secrets and configuration architecture

**Current state.** 35 variables in one mode-600 file on the box. No secret manager, no rotation, no
access audit. Several values were pasted into chat transcripts during setup.

### Recommendation: four tiers, because these are four different problems, and spend the budget on eliminating silent failure before spending anything on cryptography

**Tier 0 - the gate. Do this first, alone, and ship it (10-14 hours).** Make every required variable
fail the bring-up when unset or empty, and add a validated options class per configuration group in
**each of the three processes**, checked at startup. The required-attribute semantics reject
present-but-empty as well as absent, which is exactly the failure this system has hit twice. **It
must be done in the AuthServer and the migrator as well as the API**: a gate on the API alone leaves
the sign-in path able to boot misconfigured. This converts the most-repeated operational incident
into a refused deploy.

**Tier 1 - delivery shape (6-8 hours).** Move the three .NET processes off environment variables onto
file-mounted secrets read by the first-party file-per-key configuration provider. Keep environment
variables only where a component forces it - the database and object-store containers do - and
**record that exception explicitly rather than leaving it implicit**, because those two hold the two
highest-value credentials in the system. Say plainly in the runbook that this is a delivery-shape
improvement and **not** encryption at rest.

**Tier 2 - storage at rest (8-12 hours).** Ciphertext committed to the repository, encrypted to four
recipients: both developers, a deploy identity, and an **offline escrow identity**. Decrypt at deploy
into the Tier 1 directory and remove the plaintext. This buys off-machine copies, multi-person
recoverability, reviewable diffs of non-secret keys, and change audit. **Be explicit with any
assessor that it buys CHANGE audit and not ACCESS audit.**

**Tier 3 - the key material, handled separately because it is categorically different (8-10 hours).**

| Secret | Why it is different | What it needs |
| --- | --- | --- |
| **The DataProtection keyring** | Not a credential. It is the key material that decrypts already-issued protected payloads. Loss is permanent and there is no override | Durable storage, backed up, and **excluded from any "rotate everything" procedure** without a documented migration |
| **The token-signing certificate and its passphrase** | Replacing it invalidates every issued token - survivable once. Losing it without a replacement means the AuthServer will not start | A pre-provisioned standby credential so rotation is a selection rather than an event, plus passphrase escrow with a **witnessed recovery drill** |

Everything else in the file is a reissuable credential: losing it is an outage, not data loss.
**Treating all 35 identically is how somebody rotates the keyring.**

**On build-time secrets:** the commercial licence and package-feed credentials are needed **at image
build**, so the build system holds them. That rules out any build arrangement that cannot hold
secrets, and it puts the build system in scope for access control - easy to forget. Use build-time
secret mounts rather than build arguments, and verify nothing lands in image history.

**On rotation, and the timing that expires: rotating every credential is free right now and becomes
an incident later.** There is no real patient data, no live office beyond the test one, and no
external user depending on a session. Every value in that file should be considered compromised -
some demonstrably are - and rotating them costs a restart today and a coordinated outage after
go-live.

**Operational cost.** Setup 39-54 hours across all four tiers plus build-time handling and a
per-secret rotation classification table. Ongoing under 1 hour a month.

**What would change the answer.** A managed secret store from the platform exercise replaces Tier 2
and becomes the better home for the keyring, collapsing Tier 3(i) into it. **Tier 0 is unaffected by
any platform decision and should not wait for one.**

---

## 12. Area 9: Observability architecture

**Current state.** Console and file log sinks. Health-check history in memory, so it does not survive
a restart. Container logs capped and rotating away. No aggregation, no metrics, no APM, no tracing,
no alerting, no uptime monitoring. **Nobody is paged when anything breaks.**

### Recommendation: two systems that happen to both be called "logs", and a strict order

**Almost every mistake available here comes from treating them as one pipeline with one retention and
one destination.**

| Pipeline | What it carries | Retention | Access | Integrity | Destination constraint |
| --- | --- | --- | --- | --- | --- |
| **HIPAA audit** | Who did what to which record. A regulatory artefact about access to ePHI | Years, set by a written risk decision (section 3.8) | Narrow. Read rarely, by a named person, under a documented procedure | **Tamper-evident on the archived copy** | Must be covered by an agreement; must not be deletable by the application |
| **Application logs** | Errors, request traces, job outcomes. An engineering artefact | Weeks | The two developers | None required | **Must contain no PHI** - see the gate below |
| **Metrics and health** | Counters, latencies, saturation, up or down. No record content | 13 months, for year-over-year comparison | The two developers | None required | **PHI-free by construction**, so it is unblocked today |

**The order is the recommendation as much as the destination is:**

1. **Turn PII logging off and prove it** with a test that greps rendered log output for known
   synthetic identifiers. **This is the gate on everything downstream**, and it is cheapest now while
   every record is synthetic. Do redaction at the call site with attribute-based masking on the
   models that reach logs, not with a general-purpose redaction pipeline - the call-site approach
   fails at compile time when someone adds a field, and the pipeline approach fails silently.
2. **Write down the audit retention period as an explicit, signed risk decision** rather than
   inheriting a default (section 3.8: the six-year number is an interpretation, and the difference is
   roughly an order of magnitude of storage).
3. **Only then enable audit cleanup.** Enabling it before step 2 is destruction of evidence.
4. **Build job, disk and per-office availability alerting** - which needs no new infrastructure at
   all.
5. **Instrument to a vendor-neutral wire protocol and leave the destination unconfigured** until the
   platform exercise concludes. This is the one requirement most likely to be silently collapsed to a
   specific product in a later document; it should not be.

**Two specifics worth naming.**

**Job observability comes from two queries against tables that already exist**: the job server's
heartbeat tells you the processing server is alive, and the recurring-job schedule set filtered to
past due times tells you a schedule is overdue. Surface both as health checks. That is perhaps four
hours and it directly attacks the failure the team cannot currently see. **One thing to verify
first**, because it changes the technique entirely: confirm which mechanism actually registers the
twelve recurring jobs. If they are framework periodic workers on an in-process timer rather than
persisted recurring jobs, nothing is stored anywhere and this does not apply - which would itself be
the most important discovery in this area.

**Aggregate the edge access log by hostname.** In a database-per-tenant system, one office diverging
from the others is the signal that one database is unwell. No other view shows that.

**Health history must survive a restart.** In-memory storage means the answer to "was it flapping
overnight" is permanently "I do not know".

**On the alert set: small is the operative word.** On a two-person team with no rotation, an alert
that fires and is not acted on trains the team to ignore alerts, and then the real one is missed.
Start with six: the site is unreachable; the API health endpoint is failing; a database is
unreachable; **the backup did not complete, or no verified restore has happened recently**; the job
queue is growing without draining; the integration outbox has failures older than an hour. **Add a
seventh only by removing one.**

**Operational cost.** Setup 50-70 hours: PII logging off plus the redaction pass 12-18; retention
decision plus cleanup configured and verified per office 6-10 (note it is a per-tenant setting, so
it scales with office count and a newly provisioned office silently inherits the default); job and
disk health checks plus persisting health history 10-14; external per-office probing plus the alert
set and a dead-man's switch 8-12; structured logging and an export path pointed at nothing 12-16.
Ongoing roughly 1 hour a month reading dashboards, which is the part that must actually happen.

**What would change the answer.** If PII logging cannot be disabled without losing diagnostic value,
the application-log destination becomes a PHI store and must be covered accordingly. More expensive,
not architecturally different.

---

## 13. Area 10: Infrastructure as code and reproducibility

**Current state.** A well-commented compose file, an override for local seeding, an nginx template,
multi-stage Dockerfiles, backup and certificate scripts, systemd units and runbooks - all in version
control. Deployment is a manual SSH procedure with **two traps that have each broken the stack**: a
mandatory `--env-file` flag with no fallback, and a reverse proxy that caches upstream addresses at
worker start and must be force-recreated after any backend rebuild.

**This is a better starting position than it sounds.** The compose file *is* the infrastructure
definition, it is committed, it is commented with the reasoning, and it works. The gap is not that
there is no infrastructure as code. **The gap is that the two traps live in a human's head.**

### Recommendation: fix both traps at their mechanism, automate the deployment, declare the platform resources, and stop

**The organising principle for a two-person team is to prefer structural fixes over procedural ones,
because structural fixes keep working when the infrastructure budget goes to zero and procedural ones
do not** - and this budget is an estimate that will be raided by feature work.

1. **Delete trap (a) at its mechanism.** Make every required variable fail the bring-up when unset,
   *and* remove the need for the flag entirely by naming the secrets file so the tooling discovers it
   automatically. **The flag's omission becomes impossible rather than merely remembered.** 1-2 hours.
2. **Delete trap (b) at its mechanism.** Move all four backends onto a resolving upstream form so
   addresses are re-resolved, and **delete the force-recreate step from the runbook**. Note that the
   variable-in-`proxy_pass` workaround currently used for the object store forfeits upstream keepalive
   pooling and passive health checks, so it should be **replaced rather than propagated**. 4-8 hours.
3. **Pin the compose project name** so the stateful volumes stop being derived from the directory
   basename. A rename of the checkout directory currently orphans every volume.
4. **Pin third-party images by digest and tag built images by commit**, so a rollback artefact exists.
5. **One deploy entry point** that validates configuration and proxy syntax first and waits with a
   bounded timeout after, so a bad deploy exits non-zero instead of hanging or appearing to succeed.
6. **Separate the migrator from the bring-up path and make it report per office**, because until that
   is true every increase in deployment automation is an increase in unsupervised migration risk.
7. **Write the rebuild procedure and execute it once, timed, on a scratch host**, with pass or fail
   defined by restored content rather than by containers being healthy.

### Where the line is, and what is over-engineering

| Level | Verdict |
| --- | --- |
| Deployment as one gated script | **Do it first.** It is what eliminates both traps |
| Fixing the traps at the root | **Do it too, and it is smaller.** The 4-8 hour resolver fix deletes a manual step permanently and has the best ratio in the document |
| Declarative platform resources (networks, databases, storage, secret references, DNS) | **Do it second.** It is what makes a platform move a re-run rather than a re-derivation |
| A full plan-and-apply pipeline with state locking and drift detection | **Over-engineering here.** Drift detection's value is proportional to the number of people making undeclared changes; there are two and they sit together. The cost is a state backend to operate and a corrupted-state failure this team cannot recover from |
| Immutable instance replacement per deploy | **Over-engineering here.** The application is already immutable at the image level, which is the 90% that matters |
| Agent-based configuration management | **No.** It solves fleet drift across many machines. There is one |
| Automating office provisioning through infrastructure code | **No, and this one is a trap.** It would convert a business action into a deployment, destroying the property the input document identifies as load-bearing. **Automate the fleet operations around offices, not the creation of offices** |

**The rule:** automate what a human executes under time pressure, declare what must be reproduced in
a second place, and leave the rest documented. The deployment is executed under time pressure. The
platform resources must be reproduced. Instance lifecycle is neither.

**Operational cost.** Setup 45-70 hours, phased and independently shippable. Ongoing 4-6 hours a
month, mostly reviewing digest bumps.

**What would change the answer.** More than two people making changes, or more than two environments.
Both raise the value of drift detection enough to reconsider.

---

## 14. Area 11: Capacity model

> **Evidence warning, stated first because it changes how this section should be read.**
> The brief names `02-runtime-and-data-profile.md` as the source of the measured baseline.
> **That document was not supplied.** Three of the four files arrived. Every input below is labelled
> `MEASURED`, `DERIVED` or `UNKNOWN`, and the `UNKNOWN` count is higher than it would have been.
> Where `02` would have supplied a figure, the cell says so. **If `02` is supplied, this model can
> be completed without re-doing any of its structure.**

**No load test has ever been run.** The model's job is not to assert throughput. It is to say what to
measure, in what order, and what threshold should cause somebody to act.

### The shape, and it inverts the usual intuition

**This architecture's resource consumption is not shaped like a web application's**, so the standard
instrument - a load test producing a maximum requests per second - measures the axis this system is
least constrained on. User request volume is bounded by staff headcount across 11 to 33 practices;
audit logging does not capture GET requests by default, so reads are nearly free; and the SPA is
served statically with no server-side rendering, so read traffic never touches .NET at all.

What actually consumes resources, in order:

1. **Audit write amplification** - over 100 rows per appointment across only two of the five audit
   tables, growing with **write requests** rather than with appointments.
2. **Recurring-job fan-out** - at 33 offices, roughly 11,300 per-office database touches a day.
3. **The job dequeue loop** - a continuous query floor against the host database that does **not**
   scale with office count and is almost certainly the largest single source of database batch
   requests at idle.
4. **Blob growth** for documents and packets - entirely unmeasured, and plausibly the dominant term
   **in bytes** even though audit is the dominant term **in rows**.
5. **The two-worker packet renderer** - a shared, non-tenant-isolated throughput ceiling for the
   entire fleet.

**Three of those five are measurable today with zero users and zero load tooling.** That is why this
model front-loads measurement over load generation.

### What is actually known

| Input | Value | Status |
| --- | --- | --- |
| Audit rows per appointment | ~1,450 audit-log + ~2,689 property-change rows per 16 appointments = **~259 rows per appointment**, across **two of five** audit tables | `MEASURED`, but quoted from the brief rather than read from `02` |
| Offices at launch / ceiling | 11 / 33 | `MEASURED` |
| Databases today | 2 | `MEASURED` |
| Host resources | 4 vCPU / 16 GB / 48 GB disk, ~9.7 GB free | `MEASURED` as at 2026-08-25, **not re-verified** |
| Recurring-job fan-out | 3 jobs every 15 min, 2 hourly, 7 daily, each one connection per office | `MEASURED` |
| Pool idle reap window | Roughly 4-8 minutes | `MEASURED` (documented behaviour) |
| Appointments/office/day, concurrent users, document volume | - | **`UNKNOWN`.** `03` s7 Q8 is still open |
| Current database sizes and row counts by table | - | **`UNKNOWN` to this exercise.** These are `02` figures |
| Latency, throughput, error rate | - | **`UNKNOWN`.** No load test, no APM |

### The two derived findings that change what to watch

**Finding 1: the recurring sweeps pay a fresh login almost every time, and nobody would look for it.**
Connection pools evict idle connections after roughly 4 to 8 minutes. The three highest-frequency
jobs run every 15 minutes. **The interval is longer than the eviction window**, so an off-hours sweep
finds empty pools and pays a fresh connect, handshake and login for every office, every cycle -
roughly 400 fresh logins an hour at 33 offices that the team believes are pooled. During business
hours, interactive traffic on the same connection string keeps pools warm, so **this is an
off-hours upper bound, not an all-day figure.** It costs latency and login overhead, not correctness.

**And the documented mitigation is architecturally unavailable here.** The vendor names this exact
situation - pool fragmentation across many databases - and prescribes connecting once and switching
database with a statement. That is incompatible with an ORM that expresses tenancy as the connection
string's catalog, and the input document confirms there is no raw SQL anywhere in the source. **State
this, because somebody will find the vendor page and try to act on it.** It is a cost to size for,
not a defect to fix.

**Finding 2: disk is the nearest hard ceiling and it has a date.** Audit growth at roughly 0.28 MB
per appointment including indexes, against 9.7 GB free shared between the databases, the object
store and the backup directory. At 300 appointments per office per month that is roughly 924 MB a
month at 11 offices and 2,772 MB at 33 - consuming the free space in about 11 months and about 3.6
months respectively, **halved again by the same-disk backup copy.** At 100 appointments a month the
runway triples and the conclusion does not change.

**The cheapest measurement in this document is one `df` sample a day.** Within two weeks it yields a
projected exhaustion date, which is the single most decision-relevant number the team could hold.

### What to measure, in priority order

| # | Measure | Threshold that should cause action |
| --- | --- | --- |
| 1 | Free disk, daily | A projected exhaustion date inside two quarters |
| 2 | Size and row count of **all five** audit tables per office, weekly | Any office growing at 3x the fleet median; any database crossing 60% of the platform's per-database limit |
| 3 | Idle database load with all user traffic removed | If the idle dequeue floor exceeds ~10% of database CPU, it is the cheapest capacity win available and it is a one-line change |
| 4 | Wall-clock of one full job sweep, per sweep and per office | A 15-minute job whose sweep exceeds ~7 minutes at 11 offices will not fit at 33. **This is the number that decides whether fan-out must parallelise before the ceiling** |
| 5 | Object-store growth per office per month, by key prefix | Growth exhausting the allocation inside two quarters. **This is the unmeasured term most likely to dominate in bytes** |
| 6 | Backup and restore duration per database | A restore exceeding the recovery-time objective means the objective is wrong |
| 7 | Peak pooled connections per office | Any pool sustaining above 50% of its configured maximum |
| 8 | Edge latency and error rate **by hostname** | One office diverging usually means one database, not one user |

**Then run one load test, deliberately, before the first real booking** - not to find a throughput
number, but because it is the only time destructive load against the real environment is free.

### What this model deliberately does not say

It asserts no requests per second, no instance sizing, and no headroom multiplier. Every such number
would be invented. **The honest statement is that this system has never been measured under load,
that the measurement is cheap, and that the first measurement is worth more than any sizing estimate
this document could produce.**

**Operational cost.** Setup 20-28 hours: passive baseline 4-8 (four scheduled entries, one query
script, one counter capture per container, one disk line); growth probe 4-6 (script the lifecycle
against the API, snapshot all five audit tables before and after, repeat about 20 times); fleet
multiplication test 8-12 (provision to 33 through the host UI, run 48 hours, collect, tear down).
The optional load test adds 8-16. Ongoing 2-3 hours a month.

**What would change the answer.** `02-runtime-and-data-profile.md` arriving, which converts several
`UNKNOWN` cells directly; and the first real office going live, which replaces the assumed
appointments-per-day figure with a measured one.

---

## 15. Area 12: Migration path

**No big-bang cutover, and no parallel run either.** Both usual answers are wrong here for the same
good reason: **there is no real patient data yet**, and ten of eleven launch databases do not exist.
**The migration is a build, not a move.**

### The central claim, which is judgement rather than sourced fact

**The migration is a precondition of go-live, not a successor to it.** Every property that makes it
executable by two SDE 1 developers - free downtime, repeatable rehearsals, no agreement on the
critical path, rollback by discarding rather than reconciling - is a property of the no-PHI window,
and every one inverts the day a real practice takes a real booking. **Sequence the business
accordingly or accept a materially worse migration.**

### The shape: relocate dependencies one at a time behind seams that already exist

The seams were built deliberately and the expensive part is already paid for: the derived connection
string relocates every office database with one configuration key; the cache endpoint, the object
store endpoint and the mail relay are each a single value. **That is branch-by-abstraction at the
infrastructure-dependency layer.** The strangler here strangles the infrastructure, not the code.
**Do not decompose the application - that is a different exercise and must not share a window with
this one.**

### The sequence

| Phase | What | Precondition | Effort |
| --- | --- | --- | --- |
| **0** | **Code and process preconditions. Nothing moves until these are true.** Per-office connection-string override so "move one office" is expressible at all (today the move is fleet-atomic); migrator per-office reporting and a non-zero exit; the CI model-drift check; startup assertions; an inventory query for outstanding email-confirmation links; the administrative-dashboard fix | The tenant-boundary test from section 1 has passed | 40-60 h |
| **1** | **A reconstructible environment, rehearsed three times on clean hosts against a staging certificate authority.** Exit criterion: built twice, from committed configuration, with content assertions | Phase 0 | 30-40 h |
| **2** | **Names, TLS and edge.** Register the public domain; three-depth wildcard certificates plus the apex; the `default_server` and apex fixes; edge indirection so a later origin move is a configuration change | The business has answered which domain (`03` s7 Q1). **This is a procurement action, not an engineering one, and everything after it is blocked by it** | 20-30 h |
| **3** | **The three stateful moves, one at a time, each with its own rehearsal**: database, cache and keyring, object store | Phases 1-2. **Backups off-box before the keyring moves** - see section 21.2 | 30 h |
| **4** | **Application cutover with a per-office canary** | Phase 3 | 20 h |
| **5** | **Retirement, a final restore test, and documentation** | Phase 4 | 10 h |

**Operational cost.** Setup 150-190 hours - roughly 4 to 5 developer-weeks of pure migration work,
on top of the per-area work in sections 4 to 17, and excluding waiting on the domain and the
agreements. **Ongoing: zero** once complete, which is the point: this is the only item in the
document whose steady-state cost is nothing, and it is why it should be finished rather than left
half-done. The rehearsal discipline it establishes - clean-host rebuilds with content assertions -
becomes the annual exercise costed in section 8.

Against roughly one developer-week a month, the migration alone is **four to five months of the
entire infrastructure allocation**, and the code-standard exercise has already claimed a large share
of the same budget. Section 2 is where that collision is resolved.

### The recognised patterns, assessed honestly rather than listed

| Pattern | Applies? | Reasoning |
| --- | --- | --- |
| **Branch by abstraction** | **Yes - it is the spine.** The precondition for the pattern is that an abstraction layer exists to let two implementations coexist. Here it does, at the dependency layer, and it was already paid for | |
| **Blue-green** | **Yes, as the cutover mechanism.** One caveat: a colour hostname under the live base domain claims an office slug. Use a separate base domain for the non-live colour | |
| **Rebuild and reseed** | **Yes, as the rehearsal mechanism.** It exists only because there is no PHI, and it is the cheapest rollback in the programme | |
| **Per-office canary** | **Yes, for the final application move only** - one office, real slug, synthetic data | |
| **Parallel run / shadow traffic** | **No.** It means duplicating live requests, which in a PHI system means duplicating PHI into an environment that may not yet be covered by an agreement, to validate a system whose only change is where it runs | |
| **Canary release across offices** | **No, not for the platform move.** Splitting the population means two environments with divergent data, and tenancy is a DNS property. It is also worth naming that **the application tier is shared**, so progressive per-tenant rollout of *application* changes is a property this architecture does **not** have - only the databases can be staged. A reader planning otherwise will plan something the architecture cannot deliver | |
| **Strangler fig** | **No.** There is nothing to strangle: the application moves intact and the code is not being replaced | |
| **Expand and contract** | **Yes, and required regardless of platform.** Migrations complete before new instances start, so during any rolling deploy old instances briefly run against the new schema. It is a code discipline, not an infrastructure feature | |

### What would make this expensive

- **Real patient data arriving before the cutover.** It stops being a build.
- **Offices going live one at a time on the old environment.** The cutover becomes per-office and
  the two environments must interoperate, which they cannot.
- **The domain decision slipping.** Everything from phase 2 onward is blocked by it.

**What would change the answer.** If the business keeps staff on VPN and exposes only external-party
surfaces (`03` s7 Q2), phases 2 and 4 shrink substantially, because the anonymous surface is what
drives the edge work.

---

## 16. Area 13: Administrative surface containment

> **Not in the brief's list of twelve.** Added because `03` section 4.0 records a finding dated after
> the rest of that document was written, and because it invalidates an assumption the edge design
> would otherwise make.

**Current state.** The job dashboard is served in the production path behind an authorisation filter
whose entire body returns true, mounted inside a tooling guard that is **not** an environment gate,
and registered with antiforgery ignored. It exposes every queued, completed and failed job with its
arguments and full exception detail - **job arguments carry appointment identifiers and recipient
addresses** - plus buttons to trigger, requeue and delete jobs. A code fix is planned and is small.

**The architectural point survives the fix:** it means **the edge cannot assume every application
route is either public by design or authenticated.** That assumption is exactly what a "put a WAF in
front and let the application handle authorisation" design rests on, and one counter-example
invalidates it.

### Recommendation: three layers, and the allow-list belongs in the application, not at the edge

That is the direct answer to the design question, and the reasoning matters. **The edge and the
application are different parsers of the same request**, and there is a specific disagreement that
defeats a path-based deny-list outright: nginx prefix matching is case-sensitive on Linux while
ASP.NET Core path matching is ordinal-ignore-case, so a `location /hangfire { deny all; }` does not
stop a request for `/Hangfire`. An edge **allow**-list does not have that defect and is the right
principle, but it couples every new controller to a manual proxy change in a deployment process that
already has a known proxy-staleness trap, so **it will rot.**

**Layer 1 - placement (3-6 hours).** Bind administrative and diagnostic surfaces to a **second
listener** constrained to its own host and port, do not publish that port, and give it no proxy
server block. Access is by SSH local forward, which costs nothing new because SSH is already the only
deployment channel. This is separation of management functionality from user functionality, and **it
is durable because it is a property of a binding, not of a path.**

**Layer 2 - deny by default in the application (16-26 hours).** A fallback authorisation policy
requiring an authenticated user, in **both** processes, with genuinely public endpoints marked
explicitly. Both processes matter: sign-in is served by the AuthServer, whose Razor pages call
application services in-process. **The cost here is the discovery, not the code** - finding every
genuinely anonymous endpoint across the framework, the authorisation server, health, anonymous upload
and the partner reconcile path.

**This is the expensive half and it is deliberately not in the first 40 hours.** It can break sign-in
on a system with no staging. Land layer 1 first, run the endpoint inventory as a **report** for a
month, then land layer 2 against a known-clean inventory.

**Layer 3 - an endpoint inventory test (4-6 hours)** that fails the build when a new endpoint is
neither authorised nor explicitly marked public. This is what stops layer 2 from decaying.

**Separately and independently (4-8 hours): issue the application a scoped object-store credential
limited to the application bucket and take the root credential offline.** Section 21.9 adjudicates
the disagreement about whether this waits for the partner. It does not - the partner's credential is
a different credential and this change does not touch it.

**The general principle, because it will apply again: defence in depth here means the edge and the
application must each be sufficient alone.** Not because the team is careless, but because there is
no security specialist to notice when one of them stops being sufficient.

**Operational cost.** Setup 25-38 hours plus 4-8 for the object-store credential and 2 for the
runbook. **That is essentially one full developer-week - the team's entire stated monthly
allocation** - so it must be scheduled as *the* infrastructure item for a month rather than
attempted alongside other work.

**What would change the answer.** Nothing. Even after the code fix, administrative surfaces should
not be publicly routable in a system holding PHI.

---

## 17. Area 14: Tenant lifecycle, including offboarding

> **Also not in the brief's list of twelve.** Onboarding is covered thoroughly by the input
> documents. **Offboarding is not mentioned anywhere.** In a database-per-tenant HIPAA system,
> "delete an office" is an architectural question, not an administrative one.

**Current state.** Unknown, and that is the finding. Nothing in the supplied documents describes what
happens when an office leaves: whether the database is dropped, retained, exported or abandoned; what
happens to its objects; what happens to its audit record; or what evidence of deletion is produced.

**Four obligations collide and point in opposite directions:** the office may be owed an export; the
retention obligation does not leave with them; retaining PHI longer than necessary is its own
exposure, and an abandoned database that nobody patches or monitors is a PHI store with no owner;
and deletion must be evidenced by a record that itself survives the deletion.

### Recommendation: four things, in priority order, and one of them has an expiry date

**(a) Build the explicit tenant lifecycle state machine and per-tenant outcome ledger in the host
database now (20-30 hours).** This is the cheapest change with the widest effect: it gives
offboarding somewhere to live, it makes provisioning observable and resumable, **and it repairs the
recorded migrator hazard where one office failing mid-loop leaves the fleet on split schema versions
with no report of which succeeded.** It is the same artefact section 19.1 identifies as the thing
that moves the operational-attention ceiling from about 20 offices to about 90.

**(b) Turn on per-office database encryption with a per-office key as a provisioning step,
immediately, while every record is synthetic (12-20 hours).** **This is the only recommendation in
this document with a hard expiry.** Backups of an encrypted database are encrypted with that
database's key and cannot be restored without it, so destroying an office's key and all its copies is
a real cryptographic erase that reaches backup media you no longer control. But sanitization guidance
conditions that on the data never having been stored in plaintext - so **every backup taken before
encryption is switched on stays permanently outside the shred.** Cost now: a few statements in the
provisioner plus a key-custody procedure. Cost later: unbounded, and for data already backed up,
impossible.

Note the platform consequence, stated as a capability rather than a product: transparent database
encryption is a tier-gated feature in edition-tiered engines and is recorded in the persisted feature
list, so **it constrains the edition before the edition is chosen.** That is a legitimate input to
the platform exercise and is captured as REQ-HIP-11.

**(c) Build the export as a verified encrypted archive before the first office leaves, not during
(30-45 hours).** Newline-delimited records per aggregate, the office's documents, its audit tables,
and **a manifest with per-table row counts and per-file digests.** Deliberately do not model it on a
clinical interoperability standard - see section 20.7.

**(d) Make destruction a staged pipeline with a clock**: Suspend, Export, Verify, Quarantine,
Destroy, Attest. **Quarantine** means DNS retired, database renamed and taken offline, jobs excluded
and access revoked, but nothing irreversible has happened. **Destroy** is a separate deliberate act
taken days later against an explicit written instruction from the covered entity.

**One correction to a common formulation:** the business-associate obligation at 45 CFR
164.504(e)(2)(ii)(J) is "at termination of the contract, **if feasible**, return or destroy all
protected health information". The feasibility condition is in the operative clause itself, not a
separate carve-out. That matters here because immutable backups **cannot** be deleted by design - and
the only reconciliation between "destroy on request" and "retention the writer cannot shorten" is key
destruction. **State that reconciliation explicitly in the runbook**, because the two requirements
read as contradictory and somebody will try to satisfy both literally.

**Note the dependency on Area 2:** phase (d) is only clean **because** audit lives in its own
per-office database. If audit sits in the operational database, retiring an office means either
keeping the whole operational database for the full retention period or destroying the audit record.
**That is the strongest practical argument for the Area 2 recommendation, and the volume argument
alone does not make it.**

**One rule to adopt now and one collision it creates:** office slugs must never be reused, in any
lifecycle state including destroyed, enforced by pre-flight refusal - otherwise a new office inherits
a retired office's DNS caches, bookmarks and outstanding email links. **The collision:** section 9
step 3 and section 14 both call for a 33-office provisioning drill, and each drill would permanently
burn 33 slugs. Resolve it by reserving a drill namespace prefix that is excluded from the
non-reuse rule and refused for real offices.

**Operational cost.** Phase 0 (items a and b plus the non-reuse rule and the CI drift check) 36-54
hours and **must land before the first real booking.** Phase 1 (items c and d) 30-45 hours and must
land before the first office leaves, which may be a year later.

**What would change the answer.** A contractual position on data ownership and export obligations,
which is a business and legal question - and it is **not** among the open questions in `03` section
7. It should be added there.

---

## 18. Target architecture

> This is my design judgement applied to the facts in the input documents. It names no provider.
> Where a shape requires a platform capability, that capability carries a requirement identifier
> from `infrastructure-requirements.md`.

### The shape in one paragraph

One deployment unit, not eleven. A single logical application serving all offices, with tenancy
resolved at the application layer from the `Host` header for anonymous requests and from the token
claim for authenticated ones - **and the token independently authorising the office the hostname
selected.** The edge is a Host-preserving reverse proxy terminating TLS with a certificate covering
three wildcard depths plus the apex, doing **product** routing in four rules that do not grow with
office count. Behind it: static SPA files, the API process, the AuthServer process, and - new - a
dedicated worker process that is the **only** process running the job processing server. Behind
those, on a second unpublished listener, the administrative plane. State lives in five places and
only five, one of which is new and one of which has moved.

### Component boundaries and trust zones

```
                        PUBLIC INTERNET  (anonymous traffic reaches registration,
                        login, password reset, consent response, document upload)
                                       |
                                    [ DNS ]
                     apex, *.<d>, *.api.<d>, *.auth.<d>, minio.<d>
                                       |
=========================== TRUST BOUNDARY 1 ============================
  EDGE ZONE   the only zone with a public address
   +--------------------------------------------------------------------+
   | TLS termination + WAF + rate limiting + security response headers   |
   | FOUR routing rules, invariant in office count (product, not tenant) |
   | default_server on 443 terminates unmatched SNI and Host             |
   | MUST forward the original Host byte-for-byte      (REQ-TEN-01)      |
   | MUST NOT be reachable by bare IP for app routes   (ANTI-02)         |
   | Validates hostname SHAPE. Does NOT hold an office allow-list        |
   | NO administrative path exists here at all         (REQ-HIP-09)      |
   +--------------------------------------------------------------------+
                                       |
=========================== TRUST BOUNDARY 2 ============================
  APPLICATION ZONE   no public address; inbound only from the edge
   +-------------+ +-------------+ +-------------+ +--------------------+
   | SPA static  | | API         | | AuthServer  | | WORKER   *NEW*     |
   | *.<d>       | | *.api.<d>   | | *.auth.<d>  | | job server: ON     |
   |             | | jobs: OFF   | | jobs: OFF   | | NO inbound route   |
   | stateless   | | stateless   | | stateless   | | separately         |
   |             | |             | |             | | restartable        |
   +-------------+ +------+------+ +------+------+ +---------+----------+
                          |               |                  |
                   +------+-------+       |                  |
                   | packet-      |       |                  |
                   | renderer     |       |                  |
                   | 2 workers    |       |                  |
                   | FLEET-WIDE   |       |                  |
                   | ceiling      |       |                  |
                   +------+-------+       |                  |
                          |               |                  |
   ...... second listener, unpublished, reached by SSH forward ..........
   | admin console | job dashboard | health UI | metrics | store console |
   .....................................................................
                          |               |                  |
=========================== TRUST BOUNDARY 3 ============================
  DATA ZONE   no route from the edge zone at all
   +----------------+ +-------------+ +-------------+ +----------------+
   | SQL: host DB   | | SQL: 11..33 | | CACHE       | | OBJECT STORE   |
   |  - identity    | |  operational| | disposable  | | 6 containers   |
   |  - tenant reg  | |  per office | | eviction ON | | + partner      |
   |  - job storage | | FULL recov. | | no persist  | |   bucket       |
   |  - lifecycle   | | log backups | |             | | scoped creds   |
   |    ledger *NEW*| | enrolled AT | | (keyring is | | root offline   |
   |  - DP KEYRING  | | PROVISION   | |  NOT here   | |                |
   |    *MOVED HERE*| |  time       | |  any more)  | |                |
   +----------------+ +------+------+ +-------------+ +----------------+
                             |
                      +------+-------------------+  +-------------------+
                      | SQL: 11..33 AUDIT DBs    |  | AUDIT ARCHIVE     |
                      | *NEW* one per office     |  | *NEW* append-only |
                      | short hot window         |  | WORM, own         |
                      | NOT restored with the    |  | retention, own    |
                      | operational database     |  | access control    |
                      +--------------------------+  +-------------------+
                                       |
=========================== TRUST BOUNDARY 4 ============================
  EXTERNAL   egress only; each needs an agreement if it can see PHI
   SMTP relay (587/STARTTLS)  |  Case Tracker partner portal (HTTPS)
   Partner credentials, scoped to the partner bucket only
```

### What changed from today, and why each change earns its place

| Change | Why | Cost |
| --- | --- | --- |
| **Worker process split out of the API** | The one hard blocker to more than one API instance, and it stops a job burst consuming API request capacity. Uses a flag the AuthServer already sets | 8-12 h, no code change beyond configuration binding |
| **DataProtection keyring moves from the cache to the host database** | The keyring is the only piece of state in the cache whose loss is unrecoverable. Moving it makes the cache genuinely disposable, and the host database is **the only thing this team actually backs up**, so the ring rides along with no new artefact to remember | 12-16 h. **Must follow off-box backups; must precede any eviction policy change** |
| **A per-office audit database** | Audit outnumbers business data by roughly 259 rows per appointment. The decisive gain is not storage: **per-office restore becomes "restore the operational database and do not restore the audit one"**, with no technique required | 25-40 h, and it doubles the fleet count - which makes per-office fleet reporting a prerequisite |
| **An append-only audit archive outside both** | Integrity belongs on the copy that has to survive a dispute, not on the copy you are about to prune | 45-70 h. Deferrable behind capture reduction |
| **A tenant lifecycle ledger in the host database** | Gives offboarding somewhere to live, makes provisioning resumable, and **repairs the silent-split-schema migrator hazard**. One artefact, three problems | 20-30 h |
| **A second, unpublished listener for the administrative plane** | The edge cannot assume every route is public-by-design or authenticated. A binding is durable where a path rule is not | 3-6 h |
| **Backup enrolment moves into provisioning** | Recovery model is inherited at `CREATE DATABASE`, and databases are created by a **business action**. *The business can create a database faster than the backup system can notice it exists* | Part of the 8-12 h recovery-model work |
| **A second environment** | Deferred, deliberately. Steps 1 to 4 of section 9 deliver more per hour, and the no-PHI window makes production a legitimate destructive-test target that will not exist later | Deferred |
| **A disclosure ledger as domain data** | The **real** six-year obligation (45 CFR 164.528) covers an accounting of disclosures, and workers'-compensation disclosures are not excluded. Tiny, business-meaningful, survives audit pruning | Application work, out of infrastructure scope, but it must be **someone's** item |

### Where state lives, exhaustively

Anything not on this list is disposable, and any design adding a sixth stateful place must justify it.

1. **The host database.** Identity, tenant registry, job storage, the lifecycle ledger, **and now the
   DataProtection keyring.** Its failure is total; an office database failure is one office. It
   should not share a recovery objective with them just because it shares an instance.
2. **The operational database per office.** The durable business record.
3. **The audit database per office.** Short hot window. Deliberately not restored with (2).
4. **The object store.** Documents, packets, anonymous uploads, packages, declarations, master
   documents, plus a partner-facing bucket with independently issued credentials.
5. **The audit archive.** Append-only, separate retention, separate access control.

**Not state, and must not become state:** the SPA container, the API, the AuthServer, the worker, the
packet renderer, the edge, and - after the keyring moves - **the cache**.

### The scaling unit

**The scaling unit is the whole deployment, not the office.** Adding an office adds two databases and
a DNS name; it does not add compute, does not add a deployment, does not touch the edge, and must
not. This is what makes 11-to-33 a configuration change rather than a migration, and it is the
property most easily lost - the moment an office needs its own instance, its own release train, its
own edge rule or its own configuration file, the model has broken.

**The unit the design implies next, if growth ever forces it, is a cell**: one stack hosting K
offices, owning a DNS suffix. The expensive prerequisite is already owned. The missing piece is
**per-office placement**, because the connection template is global today. That gap, not replica
count, is what to close before growth forces the issue - and phase 0 of the migration path closes it.

---

## 19. Answers to the five specific questions

### 19.1 Is database-per-tenant defensible at 33 offices, and what is the actual breaking point?

**Yes, and it does not break at 33 for any platform reason. Every technical limit is ten to a
thousand times further out. But the model as currently OPERATED breaks somewhere around 16 to 23
offices, on operational attention - and that break is not caused by database-per-tenant. It is
caused by two missing artefacts.**

Ranked by the office count at which each becomes the limiting factor:

| # | Constraint | Binds at | Reasoning |
| --- | --- | --- | --- |
| **1** | **Operational attention** | **~16-23** at a realistic budget; ~48 at a full 40-hour developer-week | *My model, not a sourced fact:* roughly 2.0 h/month fixed plus 0.80 h/office/month. N=11 gives 10.8 h; N=33 gives 28.4 h; N=48 gives 40. The slope is dominated by two fixable things: **post-deploy schema verification is manual at about 2 minutes per office because the migrator emits no fleet report** (66 minutes per deploy at 33 offices), and **per-office incident triage is linear in N because with no log aggregation, answering "is this one office or all of them?" requires N manual checks.** Drop the slope to 0.20 h/office and the crossover moves to **65 offices at a 15-hour budget and 90 at 20** |
| **2** | **Migration fan-out** | overlaps with (1) and is a principal cause of it | The chance at least one office fails per deploy is 1-(1-p)^N. At a 2% per-office failure rate: 20% at 11 offices, 49% at 33, crossing 50% at 35. At 5%: 43% at 11, 82% at 33, crossing 50% at **14**. Separately, deploy **downtime** is linear in N, because both application processes gate on the migrator completing and the migrator runs seed contributors on every bring-up: at 10 seconds per office that is 1.8 minutes at 11 and 5.5 at 33; at 30 seconds, 5.5 and 16.5 |
| **3** | **Backup** | the **window** is not the constraint; **the destination is**, and it binds at ~3-5 offices **on a calendar rather than on N** | A serial native backup of 33 databases at a few gigabytes each fits any night comfortably, and would still fit at 100. But the script writes to a directory on the **same disk** with roughly 9.7 GB free. And no restore has ever been tested |
| **4** | **Connection count and pools** | **~325** | Pools are keyed on the exact connection string and the provider varies only the database name, so N offices means N+1 pools at a default ceiling of 100 each. That is about 1,200 potential at 11 and 3,400 at 33, against an engine maximum of 32,767 user connections - reached only at about 326 offices. This costs memory, not correctness, at these counts |
| **5** | **Worker threads** | **hundreds** | The engine auto-configures 512 worker threads at four logical CPUs, and a worker is held only by an **active request**, not by an idle connection. The three sweeps iterate offices **sequentially**, so they occupy 3 workers regardless of N - a 170x headroom that does not move as N grows |
| **6** | **Databases per instance** | **32,766** | Never. But see the note below |
| **7** | **Per-database platform limits** | **platform-dependent, and this is deliberately not answered here** | The engine permits 32,767 databases per instance. A **managed** platform can cap far lower - one documents 100 user databases per instance in its mid tiers. 34 fits comfortably and 68 (after the audit split) still does, **but the lesson generalises: the number to check is the platform's own documented figure, and "it is SQL Server, so 32,767" is the wrong answer.** Captured as REQ-TEN-04 |

**Two constraints that bind now, independent of N, and are more urgent than anything above:**

- **The edition.** Developer edition includes all Enterprise functionality but is licensed for
  development and test, not as a production server. **This binds at one office, today.** It is a
  licensing prohibition, not a performance limit, and it has architectural consequences (section
  3.6). Express is excluded not by its per-database size cap - which looks generous under
  database-per-tenant - but by a **per-instance** buffer pool of about 1,410 MB shared across all
  databases, which at 33 offices is about 42 MB of cache per office, and by the absence of native
  backup to object storage.
- **Disk, driven by audit.** Section 14, finding 2. Months away, dated, and measurable in two weeks.

**The answer to "what is the actual breaking point":** the nearest platform break is around 325
offices. The real break is around 16 to 23, and its cause is **a two-person team's attention consumed
by manually verifying fleet schema state and manually disambiguating per-office incidents.** Both
have the same fix, and it is one artefact: **a per-office outcome report.** That is the whole thing.
It is section 17(a), it costs 20-30 hours, and it moves the ceiling past any plausible growth.

**What not to build**, stated so it can be argued with: do **not** move to a shared multi-tenant
database to fix this - it trades a fixable operational-artefact problem for an unfixable blast-radius
problem, discards per-office restore, requires retrofitting row-level security across 37 aggregate
roots and 56 application services, and forfeits the one-key relocation property. Do **not** build a
shard map. Do **not** parallelise the office sweeps: sequential iteration is what keeps concurrency
at 3 workers instead of N, it fails obviously, and parallelising it creates a subtle failure this
team would not notice.

---

### 19.2 Should audit data be separated from operational data, and if so how?

**Yes - but the premise driving the urgency is wrong, and correcting it changes the design.**

**The six-year obligation does not attach to the audit log.** 45 CFR 164.316(b)(2)(i) requires
retention of *the documentation required by paragraph (b)(1)*: the written policies and procedures
implemented to comply with the Security Rule, and a written record of any action, activity or
assessment the subpart requires to be documented. **An application audit log is neither.** 45 CFR
164.312(b), Audit controls, requires the mechanism and states **no retention period at all.** NIST SP
800-66r2 places the six-year specification in its Documentation section and, in its Audit Controls
section, names no retention period, treating it as a risk-based decision and asking where audit
information should reside. Even the pending Security Rule proposal would require logs kept "for an
amount of time that is reasonable and appropriate for the specific type of report or log", explicitly
differentiated per log type.

**What you must keep for six years is your audit and retention POLICY and the records of the activity
reviews you perform - documents measured in kilobytes.**

**But there IS a real six-year data obligation, and it is a different, much smaller artefact.**
45 CFR 164.528(a)(1) gives individuals a right to an accounting of disclosures for the six years
prior to the request. Its exclusion list does **not** exclude 45 CFR 164.512(l), disclosures for
workers' compensation - **and this platform's core business is exactly those disclosures.** So the
system owes six years of a narrow, purpose-built **disclosure record**, which should be first-class
**domain data** in the operational database carrying the content elements 164.528(b) specifies, not
audit exhaust.

**The recommended design, in dependency order**, is section 5: cut capture at source; make
audit-write failure loud; write the retention policy then enable the cleanup already shipped; move
audit into a per-office audit database; put immutability and cold retention on **exports**, never on
live tables. Plus the disclosure ledger above.

**How the obligation is met without every backup and restore carrying years of audit rows:** tiered
retention with a **separate store per tenant**. Hot audit in a per-office audit database with a short
risk-based retention; cold audit as immutable encrypted per-office exports; and the genuine six-year
artefacts - policies, activity-review records, and the disclosure accounting - held as small
documents and domain data. **Restore granularity then requires no technique at all: you restore the
operational database and simply do not restore the audit one.** That is the decisive property, and it
is what a time-boundary-only design does not give you.

**The binding constraint is not storage growth and not the six-year rule. It is that no restore has
ever been tested**, and today an office restore is inseparable from restoring a mass of audit rows
with no operational purpose and no federal retention floor. That binds first because it gates
everything else: while the operational backup is dominated by audit bytes, a restore drill is too
slow and too intimidating for two junior developers to rehearse, so the untested-restore risk never
retires - and it is the only risk on the list that can end the business.

**Smallest change that moves the needle:** two edits in one configuration block plus one document,
in well under a day, with no schema change and no infrastructure change. Turn off saving entity
history on navigation changes, replace the all-entities selector with a named allow-list, and write
the retention policy. Edit one attacks the ratio at source and is reversible. The document **is** the
artefact 164.316(b)(2)(i) actually obliges you to retain, so it converts an imagined multi-gigabyte
obligation into a discharged one.

---

### 19.3 Can this application run more than one API instance today?

**No - not today, not safely. Four things block it, and only one is the one the team would guess.**

| Mechanism | Blocks? | Reasoning |
| --- | --- | --- |
| **DataProtection keyring** | **No - already correct** | Keys are in a shared store with the application name set identically in both processes. This is the canonical multi-instance configuration and it is the usual blocker. It is already gone |
| **Migration runner** | **No, as a multi-instance concern** | It is a separate one-shot both processes gate on; N instances gate on the same run. EF Core already serialises migrations with a database-wide lock, so simultaneous migrators are safe. It **is** a rolling-deploy concern, which is a different problem needing expand-and-contract schema discipline |
| **Session affinity** | **Not needed** | No server-side rendering, no realtime, no in-process session state. The proxy's WebSocket configuration refers to a hub that does not exist |
| **Recurring-job duplication** | **Not naively** | The recurring scheduler wraps its batch in a distributed lock and each job in a per-job lock; the framework registers each job under a deterministic identifier, so N instances produce one definition, not N. Its own source comment says a lock timeout means another server did the work. **`DisableGlobalLocks = true` does not weaken this** - it affects only the write-transaction lock, not the distributed-lock API the scheduler uses, and directionally it makes N servers behave *better* |
| **1. Reverse-proxy upstream caching** | **YES - and it is the binding constraint** | nginx resolves upstreams once at worker start. Add a second API container and the proxy never learns its address: **it receives zero traffic, the deploy looks successful, no error appears anywhere, and every conclusion drawn about whether multi-instance works is drawn from a system still running one instance.** The other blockers are more dangerous, but none can manifest until traffic actually reaches instance two, and none can be diagnosed while the routing layer silently discards the evidence |
| **2. Job execution bound to the API process** | **YES** | Enqueue-once is not execute-once. The framework's own throttling documentation states that because there are no reliable failure detectors in distributed systems, the same job may be processed on different workers in corner cases. With a five-minute invisibility timeout and sweeps that open a connection per office, **a slow sweep can exceed the window and be re-fetched by the second server while the first is still running.** The jobs in question include two durable outbox drains that send patient and attorney email and post to a partner system. **A duplicate send is real-world harm** |
| **3. Unguarded office provisioning** | **YES** | `ProvisionAsync` is idempotent but not concurrency-guarded. Two instances - or one double-clicked button - can run migration and seeding against the same office simultaneously |
| **4. In-process event bus and per-instance caches** | **YES, subtly** | The default distributed event bus is in-process, so events published on one instance are not seen by the other. Any handler whose correctness depends on delivery silently stops working for half the traffic |
| **Polling floor** | Not a blocker, but size it | The dequeue loop's in-process semaphore means only one thread per **process** polls, so the floor is about 5 statements/second per process - **not per worker.** N instances means N times that against the host database, permanently |

**The smallest change that unblocks it - five items, roughly 1 to 1.5 developer-weeks, in this order:**

1. **Give the API and auth proxy blocks the resolving-upstream treatment the object-store block
   already has.** Owed regardless of the multi-instance decision, because it is the same defect that
   already breaks routing after every backend rebuild.
2. **Bind job execution to configuration and set it true on exactly one instance.** The same switch
   the AuthServer already uses. It fails **obviously**: no runner means jobs visibly stop, rather
   than subtly double-running.
3. **Wrap the whole of `ProvisionAsync` in a distributed lock keyed on the office slug**, copying the
   framework's own runtime-migrator pattern. About 15 lines, and worth doing at one instance because
   it also stops a double-clicked provision button.
4. **Set an explicit pool ceiling on the derived tenant connection string.**
5. **Audit every event-bus handler and move anything whose correctness depends on delivery onto the
   durable outbox that already exists.**

**Items 2 through 5 are all worth doing at a single instance**, which is what makes this cheap: the
work is owed anyway, and it buys the option to go horizontal later as a decision rather than a
project.

**Should they run more than one instance? Not yet** - section 6. The value of two is zero-downtime
deploys and surviving a node failure, and both are worth less to this team right now than the
operational simplicity of one instance that restarts fast and fails loudly.

**Rejected alternative:** running the job server everywhere and trusting the locks. It is supported,
and it converts every latent non-idempotency in twelve office-iterating jobs into an incident you
discover in a patient's inbox.

---

### 19.4 What must be true of every component in the request path?

**The headline correction first, because it changes the premise.** Section 3.1: there are five
default resolvers, the first reads the tenant from the access token, and the chain is first-match
with a hard break. **For an authenticated request, tenancy is decided by the token claim, not by the
Host header.** Host-header tenancy is the mechanism for **anonymous** requests - login, password
reset, anonymous upload, the partner reconcile endpoint. That is a smaller blast radius than
"everything", and it is precisely the unauthenticated, security-sensitive set.

**Eleven invariants, each stated so it is falsifiable.** "Edge" means the outermost component
terminating client TLS.

| # | Invariant | Why, and what it costs to get wrong |
| --- | --- | --- |
| **I1** | **Host reaches the application with its leftmost label byte-identical.** Case folding and stripping a default port are safe - the host is case-insensitive by specification, nginx lowercases and strips a trailing dot, and the extractor matches case-insensitively. **Label rewriting is not safe** | The office slug is the leftmost label. Rewriting it does not degrade tenant resolution; it removes it |
| **I2** | **No non-default port may appear in Host as the application sees it** | The domain contributor matches against the **raw** host value, **which includes the port.** A format of `{0}.api.example.com` will not match `office.api.example.com:8443`. This is a **silent whole-tenant outage, not an error.** Any port-shifting hop violates it |
| **I3** | **Exactly one Host header, and no absolute-form desync** | The specification requires a 400 response to more than one Host line, and requires an origin server handling an absolute-form request target to **ignore** the Host header and use the target's authority instead |
| **I4** | **A request with an empty Host must not reach tenant resolution.** An empty Host field value is specification-legal for a target URI with no authority, and the server accepts it. The domain contributor then returns **without** setting the chain handled, and the `__tenant` resolvers become reachable | **This survives correct resolver ordering.** It must be rejected at the edge as well as guarded in the application |
| **I5** | **The custom domain resolver is ordered ahead of the query-string, route, header and cookie resolvers** | The contributor sets the chain handled unconditionally whenever a Host is present, so correct ordering makes the others unreachable **whether or not anyone removed them.** This is the invariant to assert, and it is a five-line test |
| **I6** | **A token minted at one office is refused at another office's API** | Because the first resolver reads the token claim, the hostname selects the tenant and **the token must independently authorise it.** Hostname is a routing signal, not an authorisation one |
| **I7** | **Certificates cover three wildcard depths plus the apex** | A wildcard matches exactly one label, so `*.<d>` does not cover `office.api.<d>`. **Note that this needs one multi-SAN certificate, not three certificates** |
| **I8** | **The original scheme survives TLS termination** | The authorisation server's transport-security check and its issuer URLs depend on it. Losing it produces authentication failures whose message does not name the cause |
| **I9** | **The reserved slugs stay reserved, and every new infrastructure hostname is treated as a claimed slug** | `admin` means host scope; the object-store name is consumed by an exact-match rule that outranks every wildcard. A status page at `status.<d>` silently makes `status` unavailable as an office |
| **I10** | **The edge validates hostname SHAPE and depth; the application validates MEMBERSHIP** | An edge allow-list of provisioned office hostnames means a new paying office gets "Tenant not found" until somebody edits the proxy. This is the resolution of a contradiction the input documents contain and never state |
| **I11** | **Alternative host headers are stripped inbound at the edge** | `X-Forwarded-Host` and equivalents must not survive from a client, because any middleware that later honours one has been handed tenant selection |

### The patterns that violate these, with the specific violation

| Pattern | Violates | How |
| --- | --- | --- |
| **Reverse proxy with default upstream-host behaviour** | I1 | Several proxies default to sending the **upstream's** name as Host. The current configuration forwards the original explicitly; anything replacing it must do so deliberately |
| **CDN in front of the application** | I1 | Managed origin-request policies commonly **strip** the viewer Host so the CDN substitutes the origin domain. Forwarding the client Host becomes a hard capability requirement, not a preference |
| **Path-based routing or a single-hostname ingress** | I1, I10 | Collapsing offices onto one hostname with path prefixes removes the only tenant signal that exists for anonymous requests |
| **Load-balancer health checks** | I2, and see section 21.4 | Probing by IP, or by a pool hostname, returns "Tenant not found" and marks a healthy instance unhealthy |
| **Service mesh sidecars** | I1 | Meshes route on the request authority and some rewrite it. Where automatic upstream-name behaviour is a version-dependent feature flag, **do not assume the client host follows** - assert it |
| **Host normalisation in a WAF** | I1, I4 | A wildcard tenancy scheme produces an unbounded set of legitimate Host values, all of which must survive. Rulesets that normalise or reject unusual hosts break offices at random |
| **Proxies that expose the original authority in a header** | I11 | Some emit it only when a flag is explicitly enabled, and suppress it under a different flag. **A design that expects to recover the original authority from such a header without setting the flag will not receive it** |
| **Blue-green via a hostname under the live base domain** | I9 | A `green.<d>` hostname claims an office slug |
| **Any hop that changes the port** | I2 | Including a port-mapped container or a non-443 listener behind the edge |

**The one easiest to get wrong: synthetic uptime monitoring.** It is the component most likely to be
configured by somebody who was not told about the tenancy model, and the component whose false alarms
train a two-person team to ignore alerts.

---

### 19.5 What does per-office restore require?

**In one sentence:** that office's database in the FULL recovery model with an unbroken full,
differential and log chain; a tail-log backup taken **before** anything is overwritten; and a restore
sequence in which the stop-at time is repeated on **every** log restore - with the instance and all
other offices staying online throughout. **That is the easy half.**

**The hard half is that an office's state is not contained in the office database.** It is spread
across the host database, the cache, the object store, the mail relay and the partner system, **none
of which roll back with it.**

**Requirement 1: FULL recovery per database - and the constraint that actually binds is when it is
set.** Simple recovery can recover only to the end of a backup; point-in-time restore is listed among
the features it cannot use. Bulk-logged does not substitute: point-in-time restore is disallowed if
the log backup contains bulk-logged changes, so **a single index rebuild performed while a database
is temporarily in bulk-logged mode creates a log backup you cannot stop inside - silently, for that
database only.**

Recovery model is a **per-database property inherited from the `model` database at `CREATE
DATABASE`**, and in this platform a new office database is created by **a business action in the host
UI**. Two consequences:

1. **Whether an office has point-in-time capability is decided by the state of `model` at the moment
   a non-engineer clicked "add office", and nothing surfaces it.** An office provisioned while
   `model` was SIMPLE has no point-in-time capability, forever, and no error is ever raised.
2. **The switch to FULL takes effect only after the first data backup.** A freshly provisioned office
   in FULL is still effectively unprotected until its first full backup runs.

**Therefore backup enrolment must be part of provisioning, not part of a schedule.** `ProvisionAsync`
must set the recovery model, take an immediate full backup, and **fail the provisioning if either
fails** - which is idempotent and matches the existing contract. *The business can create a database
faster than the backup system can notice it exists.* **This is the single most under-appreciated
requirement in the question.**

**The ordering hazard, and do not get this wrong:** FULL recovery **without** log backups grows the
transaction log until the disk fills. On a box with roughly 9.7 GB free shared between the databases,
the object store and the backup directory, that is a simultaneous outage of all three. **Switch
recovery models and enable log backups as one atomic change**, and monitor log-reuse wait state
afterwards.

**Requirement 2: a documented reconciliation for the four things that do not roll back.**

| Not restored | What happens | What to do |
| --- | --- | --- |
| **The host database** - tenant record, host identity | Unaffected, which is correct | Nothing |
| **The host database** - job storage | Queued jobs may reference rows that no longer exist. They will fail loudly, which is acceptable - **but somebody has to expect it** | Name it in the runbook |
| **The integration outbox** (per office, in the office database) | **This is the one that can cause external harm.** Rolling back to an earlier time marks already-delivered messages undelivered, and they will be **re-sent** | Whether that is harmless depends entirely on whether the partner treats them idempotently. **Establish this with the partner before the first restore is needed.** It is a question for them, not a design decision this side can make |
| **The object store** | Objects whose metadata rows no longer exist. **That is the safe direction** - orphans are inert and enumerable | State it as the accepted outcome and provide a reconciliation job that can list them. The unsafe direction is moving object storage backwards and stranding metadata pointing at deleted objects |
| **The audit archive** | Not rolled back, **deliberately** | Rolling back the record of what happened is exactly what an audit archive must refuse to do |

**Note what changes after the Area 2 split:** the per-office audit database is a **third** database in
the picture, and the correct behaviour is to **not restore it** - which is the point. Section 21.6
adjudicates the consequence for the recovery-point objective.

**Requirement 3: the vendor's own guidance names this problem.** Recovery-model documentation warns
that related databases that must be logically consistent "might require special procedures to ensure
recoverability". The engine's only mechanism for genuinely consistent multi-database point-in-time
recovery is marked transactions, and **that is the wrong tool for this team**: it requires every
cross-database write path to participate, in a codebase with no raw SQL and no cross-database joins.
The right answer is a documented reconciliation checklist, not a distributed-transaction mechanism.

**Requirement 4: rehearsal, and the property that makes it cheap.** Office connection strings are
stored per tenant and derived from a template, so **a restored copy can be attached as a throwaway
tenant, exercised, and dropped, without touching the live office.** Restore rehearsal is therefore a
routine operation here rather than a special event. **Protect that property in any redesign** - very
few systems get it for free, and an untested restore is not a backup.

**Recommended objectives, stated as recommendations rather than findings:** do not let an engineer
assert them. Elicit maximum tolerable downtime from the business in its concrete form (section 8),
then **replace the estimate with a measured number - the wall-clock of a rehearsed per-office restore
performed by one developer against the written runbook.** A recovery point near zero is achievable
with continuous replication and adds a failure mode this team cannot debug. **Prefer the objective
that is provable by rehearsal over the objective that is better on paper.**

## 20. Not recommended: patterns that are standard elsewhere and wrong here

Each of these is a defensible choice at a different organisation. Each is rejected here for a
reason specific to this system, this regulatory position, or this team - not because it is bad. The
reasoning matters more than the verdict, because the verdicts flip when the reasons change, and the
reasons are stated so they can be argued with.

### 20.1 Microservices

**What it would buy.** Independent deployment of the packet renderer, the notification pipeline and
the integration outbox. Failure isolation between them. The ability for two developers to work on
separate services without merge contention.

**Why not here.** The application is a **layered ABP monolith with 37 aggregate roots and two
DbContexts that share a base and a shared model configuration**. The seams a microservice split
would follow do not exist in the data model: entities are mapped in both contexts and require
migrations in both. Splitting them means introducing distributed transactions or eventual
consistency into a domain that currently gets correctness for free from a single database
transaction per request.

There is also a specific multi-tenancy cost. Every service in a split would need its own copy of
the Host-header tenant resolution, its own tenant connection-string derivation, and its own
distributed-cache tenant prefixing. **Each copy is a place where tenant isolation can break
silently**, in a system where tenant isolation is the whole compliance argument.

**What it would actually cost this team.** Service-to-service authentication, distributed tracing to
debug anything, N deployment pipelines instead of one, and a class of failure - partial deployment,
version skew between services - that produces subtle wrong behaviour rather than a hard error. Rule
2 of the brief says prefer designs that fail obviously. Microservices fail subtly by construction.

**What would change this.** Nothing plausible at this scale. If the packet renderer became a
compute-heavy product in its own right serving other systems, it would deserve independence - and
it is already a separate process, which is the 90% of the benefit that is already banked.

### 20.2 Service mesh

**What it would buy.** Mutual TLS between services without application changes, uniform retry and
timeout policy, per-service traffic metrics, and traffic shifting for canary releases.

**Why not here.** Two reasons, and the first is disqualifying.

First, **meshes route on `:authority` and some rewrite it**. The single most load-bearing property
of this system is that the `Host` header reaches the application unmodified. A mesh sidecar in the
request path is a component whose default behaviour is to make routing decisions on exactly the
header that carries tenancy. It can be configured not to. It is a poor trade to introduce a
component whose misconfiguration silently breaks tenant isolation, in order to get mutual TLS
between five containers on one private network.

Second, the mesh's benefits are proportional to service count. **There are five application
processes and they all talk to the same four data stores.** The traffic graph fits on an index card.

**What would change this.** A move to many more services, or a compliance requirement for mutual
TLS between application components that cannot be met by network isolation. Neither is present:
HIPAA's transmission-security standard is met at the boundary and by keeping the data zone
unreachable from the edge.

### 20.3 Cluster orchestration (Kubernetes and equivalents)

**What it would buy.** Declarative deployment, rolling updates with health gating, self-healing
restarts, horizontal autoscaling, and a standard vocabulary for secrets and configuration. Several
of those are genuinely wanted here.

**Why not here.** This is the closest call in this section, and it is rejected on **operational
complexity as a first-class cost**, per the brief's first consequence.

A cluster orchestrator replaces one thing this team understands - a compose file and a documented
SSH procedure - with a control plane, an ingress controller, a CNI, a CSI driver, volume claims for
four stateful services, a certificate controller, and a secrets integration. Each is a component
that can fail in a way that presents as "the site is down" and requires specific expertise to
diagnose. **Two SDE 1 developers with roughly one developer-week per month, no DevOps engineer and
no on-call rotation, will not build that expertise before they need it.**

The specific failure mode to worry about is not a dramatic one. It is that the cluster works, and
then eighteen months later a version upgrade is due, the ingress controller's API has changed, and
nobody remembers why the Host-preservation annotation is set the way it is.

Note what is **not** being said: the *benefits* are real and should be obtained by other means.
Rolling updates with health gating, self-healing restarts and declarative configuration are all
requirements in Document B. They are achievable on a managed container platform without a cluster
to operate, and that is the recommendation.

**What would change this.** If the team grows a third engineer whose job includes infrastructure,
or if the organisation already operates a cluster with someone else responsible for it, the
calculus inverts - because then the control plane is not this team's cost. **This is the rejection
in this section most likely to be wrong later**, and it should be revisited at any staffing change.

### 20.4 Event sourcing

**What it would buy.** A complete, immutable, replayable history of every change - which sounds
like exactly what a HIPAA system with a six-year audit obligation wants.

**Why not here.** The obligation is already met by a different mechanism that is already in place
and already measured: ABP's audit logging plus entity property-change tracking, producing roughly
258 rows per appointment. **Event sourcing would not add compliance value; it would relocate the
same value into the write path.**

The costs are severe and specific. The domain is a 13-state appointment lifecycle maintained by two
junior developers: event sourcing means every state transition is a schema-versioned event that
must be replayable forever, and **schema evolution of historical events is a discipline this team
has no experience with and no reviewer for**. The read model becomes a projection that can lag, be
rebuilt wrongly, or drift - a class of bug that presents as "the appointment says the wrong thing"
rather than an error.

And it fights the tenancy model: an event store per office multiplies the fleet problem from Q1,
while a shared event store creates the cross-tenant data concentration rejected in Q2.

**What would change this.** A regulatory or business requirement for point-in-time reconstruction
of arbitrary past states that audit logs cannot satisfy. None is stated.

### 20.5 Multi-region

**What it would buy.** Survival of a regional outage, and lower latency for geographically
dispersed users.

**Why not here.** The user population is medical practices, attorneys and claim examiners in
**California workers' compensation**. They are in one region. Latency is not a problem to solve.

Availability is the real argument, and it fails on arithmetic. Multi-region for a stateful
application means either asynchronous replication - accepting data loss on failover, which for
34 databases plus a DataProtection keyring plus an object store is a set of independent, unrehearsed
recovery decisions - or synchronous replication, which adds write latency and a partition failure
mode. **Either way, the team that has never tested a restore now owns a failover procedure.**

There is also a compliance dimension: data residency. Multi-region deployment means deciding, per
region, whether PHI may reside there, and obtaining a business associate agreement covering each.
That is a legal cost as well as a technical one, and it buys resilience against an event
significantly rarer than the failure modes currently unaddressed - no tested restore, no alerting,
one environment.

**What would change this.** A contractual availability commitment that a single region cannot meet.
Establish the availability the business actually needs (question 9 in the deployment document is
still open) before spending anything on this.

### 20.6 Read replicas

**What it would buy.** Offloading reporting and read-heavy queries from the primary, plus a warm
standby.

**Why not here.** Three specific reasons.

First, **there is no measured read pressure**. No load test has ever been run. Adding a replica to
solve an unmeasured problem is the definition of premature optimisation, and it adds a component
whose failure mode - replication lag - is a *subtle* one. A stale read in a scheduling system shows
up as an appointment that appears unbooked and gets double-booked.

Second, **the replica story multiplies by tenant**. Thirty-four databases means thirty-four
replication relationships to monitor. The steady-state read load is currently dominated not by user
traffic but by twelve recurring jobs iterating offices and by a Hangfire queue poll interval of
zero, and **neither of those is a workload a read replica helps**: job sweeps write, and the
Hangfire poll is against the host database by design.

Third, routing reads to a replica requires the application to know which queries are safe to route.
That is an application change - a second connection string per tenant and a per-query decision -
in an application whose connection strings are derived from a single template. It would break the
one-key relocation property that makes the tenancy model operable.

Fourth, and specific to the likely edition: where the only available availability-group form carries
one database per group and **explicitly permits no read access on the secondary**, a read replica is
not merely unwise here - **it is not offered at that tier at all.** Any design that assumes one is
assuming an edition nobody has chosen yet, which is exactly the inversion this exercise exists to
avoid.

**What would change this.** A measured read bottleneck, after instrumentation exists. If reporting
becomes heavy, the first move is a scheduled extract into a separate reporting store, not a live
replica - because an extract fails obviously and a lagging replica does not.

### 20.7 A clinical interoperability standard as the tenant export format

**What it would buy.** A published, versioned, tool-supported wire format for exporting a tenant's
records, with an existing ecosystem of validators and readers.

**Why not here.** The export in section 17 has exactly one consumer: a departing practice, or its
counsel, receiving a one-time archive. It is not an integration surface, there is no receiving
system that speaks the standard, and nothing subscribes to it.

Adopting one would mean mapping 37 aggregate roots - appointments, IME lifecycle states, packet
assembly, consent responses, ex-parte-safe addressing - onto resource types designed for clinical
care records they do not correspond to. **The mapping would be lossy in both directions and the
losses would be invisible in a validator**, because a structurally valid document can omit the
scheduling semantics that are the entire business record.

The alternative is small and better: newline-delimited records per aggregate, the office's
documents, its audit tables, and a manifest with per-table row counts and per-file digests. It is
verifiable by round-trip in CI, it loses nothing, and the manifest is what makes the export
**provable** rather than merely produced.

**What would change this.** A receiving system that actually speaks the standard, or a contractual
obligation naming a format. Neither exists, and if one appears, the export is a transformation of
the archive above rather than a replacement for it.

### 20.8 Also rejected, more briefly

| Pattern | Why not |
| --- | --- |
| **Separate deployment per office** | Destroys the property that adding an office is a host-UI action rather than a deployment. Multiplies every operation by 33. The isolation it buys is already bought by database separation |
| **Message broker (Kafka, RabbitMQ)** | Durability is already provided by two database-backed outboxes that are transactional with the data they describe. A broker adds a stateful component with its own operational burden to solve a problem that is solved |
| **Autoscaling on request volume** | 11 medical practices booking appointments during business hours is a predictable, bounded workload. Autoscaling adds a mechanism that can misfire, and its failure mode - scaling down mid-request, or scaling up into connection-pool exhaustion across 34 tenant pools - is subtle. Size for peak and alert on saturation |
| **A dedicated observability platform stack, self-hosted** | Running the monitoring is a second production system. If the monitoring is self-hosted on the same infrastructure, it is also down when it is most needed |
| **Infrastructure as code covering everything** | Addressed in area 10. The proportionate level is real, and "everything" is above it |

---

## 21. Contradictions in the research body, and how this document resolves them

A completeness pass over all fourteen areas found sixteen places where areas contradicted each other.
Each is resolved here rather than left for whichever section a developer reads first, because
**several of these are cases where partial adoption in the wrong order is strictly worse than doing
nothing.**

**21.1 The cache eviction policy: three areas, three incompatible values on one setting.** One area
wants an eviction policy and persistence off, making the cache disposable. Two others require that
the store holding key material must not evict.

**Resolved:** they reconcile only if the keyring moves first, and **the ordering is mandatory.**
Sequence: off-box backups, then move the keyring to the host database, then and only then set an
eviction policy and turn persistence off. **Applying the eviction policy first silently destroys
DataProtection keys under memory pressure**, and the failure presents weeks later as intermittent
logouts with nothing to correlate it to. Section 7 states this; Document B carries it as an ordering
note on REQ-APP-01.

**21.2 Where the DataProtection keyring lives: four areas, three mental models.** One moves it to the
host database; one is written on the premise it stays in the cache; one builds its most vivid
migration risk on the cache assumption; one requires it be recoverable independently of the primary
host.

**Resolved:** it moves to the host database (section 7), and **the independence requirement is
satisfied by off-box backups, not by a separate store** - which is why 21.1's ordering puts backups
first. The migration-phase mechanics that assume a cache copy are superseded: after the move, the
phase-3 hazard about broken confirmation links **evaporates**, because the ring travels with the
database.

**21.3 The office allow-list at the edge.** One area requires the edge to reject any Host not on an
allow-list of provisioned offices, and in the same list requires that provisioning an office need no
edge change. Those are incompatible, and three other areas assume the second.

**Resolved: the edge validates hostname SHAPE and depth; the application validates MEMBERSHIP.**
Invariant I10. This is the version that preserves the property the whole design rests on. Without
this adjudication, whichever a developer reads first gets built, and if it is the allow-list, a new
paying office gets "Tenant not found" until somebody edits the proxy.

**21.4 Health probes: four surfaces, no taxonomy, and two mutually exclusive requirements.** One area
requires a liveness path answering with an IP as Host; the same area requires the edge to terminate
any Host that is not an office hostname. A probe cannot satisfy both.

**Resolved: four distinct surfaces.**

| Surface | Where | Host it carries | Who reads it |
| --- | --- | --- | --- |
| **Liveness** | The application process, on the **unpublished administrative listener** | Anything; the listener does not do tenant resolution | The container platform |
| **Readiness** | Same listener; names each failed dependency rather than reporting a flat down | Same | The container platform, and a human diagnosing |
| **External synthetic** | Through the edge, **per office**, by real office hostname | A real office hostname | The uptime monitor. This is the one that detects one office diverging |
| **Health UI and metrics** | The administrative listener, reached by SSH forward | Not applicable | The two developers |

Binding liveness and readiness to the administrative listener is what dissolves the contradiction:
the probe never traverses the edge, so it is never subject to the edge's Host rules.

**21.5 The audit database split versus every restore and sizing assumption.** After the split, a
point-in-time restore involves a third database, several sizing claims stop being true, and the
database count requirements collide (34, 40, or about 68 at 33 offices).

**Resolved:** the split stands, and **the restore answer becomes simpler, not harder: you do not
restore the audit database.** Document B sizes REQ-TEN-04 at the post-split count with headroom, and
the collision is a documentation problem rather than a design one. The real cost is the doubled fleet
count, which is why per-office fleet reporting is a **prerequisite** for the split rather than a
companion to it.

**21.6 The recovery-point objective was argued from audit reconstructability, which the split
invalidates.** One area argues the recovery point should be set by audit reconstructability rather
than booking volume, because business records are re-keyable at this data density and audit records
are not.

**Resolved:** that argument was correct **before** the split and is superseded by it. Once audit is
in its own database with its own backup schedule, the two have **separate recovery points** - which
is better, because it lets the operational database take a looser objective while audit takes a
tighter one, instead of one number serving both badly.

**21.7 Tamper evidence: one area recommends ledger tables on the live audit tables, another forbids
them.**

**Resolved: forbidden on the live tables, required on the archive.** Integrity machinery on the copy
you are about to prune constrains schema evolution and protects the wrong artefact. The copy that has
to survive a dispute is the archive. Document B states the acceptance criterion behaviourally rather
than naming a mechanism, so that both a cryptographically verified row store and write-once storage
can satisfy it.

**21.8 The six-year reading is disputed inside the body, and the largest single line item depends on
it.** Two areas state that the six-year clock attaches to documentation rather than to audit rows;
one builds its most expensive item on the contested reading and hedges in a closing paragraph.

**Resolved in favour of the sceptical reading** (sections 3.8 and 19.2), which is also the reading the
regulation's own text supports on a plain read. **The consequence: the 45-70 hour cold-archive item
is not obligated by 164.316 and should be justified on risk grounds or deferred.** What replaces it
is much cheaper: a written retention policy, and a disclosure ledger sized in kilobytes.

**21.9 Object-store credentials: rotate now or defer to the partner's schedule.** Two areas say
rotate before exposure; one folds it into a phase deferred to the partner's timetable, potentially
past go-live.

**Resolved: rotate now.** These are **two different credentials.** Issuing the application a scoped
credential limited to the application bucket and taking the root credential offline **does not touch
the partner's credential** and does not require their cooperation. Only re-credentialing the partner
waits for the partner.

**21.10 Job execution: "exactly one processing server, ever" versus a cutover plan that runs two.**

**Resolved:** "exactly one" is the steady-state invariant. **The cutover exception is deliberate,
time-boxed, and must be written down as such** - during a cutover both tiers run against shared job
storage for one cycle specifically to observe that each recurring job fires once. That is a
supervised test, not a topology. Document B states REQ-APP-07 as a steady-state requirement with the
exception named.

**21.11 Forwarded-header trust versus container addressing.** Trusting forwarded headers safely
requires pinning the known proxy address - which is a container address that changes on every
rebuild, the exact premise of the resolver fix elsewhere.

**Resolved:** pin the trusted proxy by **network range**, not by address, and treat the common remedy
of clearing the known-proxy list entirely as forbidden. Carried as ANTI-14.

**21.12 Rate limiting at two enforcement points with no adjudication.**

**Resolved:** the edge is the structural answer because it is the only place that sees both processes
(section 10). **Keep the existing application limiter and align its keys and windows with the edge's,
rather than removing it** - it is the only limiter that survives if the edge is ever bypassed
internally. But **the edge's counters are the ones reviewed monthly**, and the application limiter's
responses must be distinguishable in the logs, or a limited request looks like an application error.

**21.13 Where rehearsals happen.** One area says use production as the destructive-test target while
the no-PHI window holds; another requires a separate registrable domain for non-production; a third
requires three rehearsals on clean hosts.

**Resolved, and they are not actually in conflict once separated by purpose:** destructive drills
(restore, provisioning fan-out, migrator failure, certificate rotation, cache flush) happen **in
production, now, while data is synthetic** - that is the expiring asset. Build-and-rebuild rehearsals
happen on **clean scratch hosts** against a staging certificate authority. **A standing second
environment with its own domain is step 5 and is deferred** (section 9).

**21.14 Secret delivery: a rule with documented exceptions that another area makes absolute.** One
area requires file-mounted secrets and documents the database and object-store containers as forced
exceptions; another forbids secrets in the process environment without exception - and those two
containers hold the two highest-value credentials.

**Resolved: the exception stands and must be recorded as an accepted risk with a compensating
control**, not quietly. The compensating control is that neither container is reachable from the edge
zone and both are on the shortest rotation cycle. Document B states REQ-TEAM-07 with the exception
named, because a requirement with an undocumented exception is a requirement nobody can test.

**21.15 Audit-write failure detection: the requirement is stronger than the design.** One area
requires that a failure to write an audit record produce a distinct alertable signal; the design
keeps error-hiding on and substitutes a nightly per-office freshness assertion, which detects a total
outage but not an individual failure.

**Resolved: the gap is real and is accepted deliberately.** Turning error-hiding off converts an
audit outage into a total application outage, which is worse. **The honest statement is that
individual audit-write failures are not currently detectable, that the freshness assertion catches
the class that matters most, and that closing the remainder needs an application change to record
failures durably rather than to log them.** That change belongs on the application backlog and is
named here so it is not mistaken for solved.

**21.16 Slug non-reuse versus the fan-out drills.** Non-reuse in every lifecycle state, versus two
areas each requiring a repeatable 33-office drill that would burn 33 slugs.

**Resolved:** reserve a drill namespace prefix, exclude it from the non-reuse rule, and refuse it for
real offices (section 17).

---

## 22. Evidence, verification, and what this document could not establish

### How the evidence was produced

Fourteen area agents researched the design space **blind to the current system's own gap list**, so
the standard was not shaped around the gaps that were already known. Each produced structured
findings with a source URL, an access date and a self-declared confidence for every claim. A second
adversarial agent per area then **re-fetched every cited URL** and judged whether the page supported
the claim as stated, defaulting to "does not support". The same pass audited every capability
requirement for vendor-neutrality violations. Five question agents worked the blocking questions
independently. One critique agent read the whole body for completeness, contradictions and
unjustified complexity.

### Where the citations are

**Every standard, limit, threshold and platform capability asserted in this document carries a source
URL and an access date in `handoff/appendix-A-evidence-register.md`** - 646 evidence items across
the fourteen areas and the five questions, each with its source, its second source where one was
obtained, the access date, the researching agent's self-declared confidence, and the adversarial
verifier's verdict on that specific claim with its correction where the verdict was not clean.

**The main text of this document deliberately does not carry inline URLs.** It is written to be read
by a developer making decisions, and a decision document interleaved with 646 footnotes is not one.
The register is organised by area and question in the same order as sections 4 to 19, so any asserted
figure can be traced by finding its area and scanning that area's claims.

**Three conventions in the register that matter when using it:**

- **A claim marked `partially` is not a claim to discard.** In almost every case the substance holds
  and something narrower is wrong: a threshold cited to the wrong page of the right document, a
  quotation paraphrased rather than reproduced, a version gone stale, or a scope stated too widely.
  **The verifier's note says which**, and this document was written from the corrected version.
- **A claim marked `REASONING` in its confidence field is judgement, not sourced fact.** The brief
  asks for these to be separable so they can be argued with. In this document they are the
  recommendations themselves, the operational-cost estimates, the capacity model's derived figures,
  the office-count thresholds in section 19.1, and every "what would change the answer".
- **Anything this exercise could not verify is marked in place rather than promoted.** Six items were
  unreachable - mostly paywalled or bot-blocked - and their figures were **dropped rather than
  carried into a document a reader cannot check.**

### What the verification pass caught

**526 individual claims re-checked: 347 clean, 161 corrected, 12 wrong, 6 unreachable.** About
two-thirds survived first-pass scrutiny, which is the argument for the pass having existed. Ten of
the fourteen areas returned an overall verdict of "material errors".

Representative catches, all corrected before this document was written:

- **A verbatim quotation attributed to a page that does not contain it** - the verifier called it the
  single most serious provenance defect in its set.
- **A threshold cited from a 32-bit failure mode and applied to a 64-bit deployment.** A category
  error that would have put a spurious memory ceiling into the backup design.
- **A regulation cited to the wrong URL** - the six-year retention rule pointed at the technical
  safeguards section rather than the documentation section. The substance was right; the citation
  would not have survived review.
- **Component defaults stated from memory** against pages that never name them - a cache eviction
  policy and a persistence default among them, both load-bearing for a requirement. **Corrected by
  asserting the setting explicitly rather than relying on a default.**
- **The resolver-ordering finding itself was corrected.** The first pass said the query-string
  resolver sits at index 0 and that the framework's domain helper inserts at index 0. The verifier
  read the modules and found neither is true: the **current-user** resolver is at index 0, and the
  helper inserts **after** it. **That correction made the finding more consequential, not less** -
  section 3.1.
- **A tool claimed actively maintained that had been archived upstream six weeks earlier**, which
  inverted a migration step's verification argument.
- **A DNS propagation window misread by roughly eight times**, because a public resolver's own cache
  cap was mistaken for the parent delegation lifetime. Planning a nameserver change against six hours
  instead of forty-eight would have materially understated the risk.
- **Several stale package versions**, and several statistics behind paywalled or bot-blocked sources
  that were dropped rather than carried into a document a reader cannot check.

**On vendor neutrality:** the audit found **no requirement anywhere in the body that names a product
as a recommendation.** It did find requirements written in one product's vocabulary - a specific
ORM's type names, one object-storage API's exact retention terms, one framework's tenancy words -
which would not be evaluable against a different platform. **Those were restated as capabilities in
`infrastructure-requirements.md`.** Where a name survives there, it is because it is the incumbent
component being constrained, not a selection being made, and it is flagged as such.

### What this document could not establish

Stated so nothing here is mistaken for complete.

1. **`02-runtime-and-data-profile.md` was not supplied.** Only three of the four files arrived. It is
   the document the brief points at most often: measured database sizes, table-level row counts, the
   configuration surface, and the explicit "never measured" list. **Area 11 is built as a structure
   plus an instrumentation plan with every input labelled measured, derived or unknown**, and the
   cells `02` would have filled say so. The one load-bearing figure - the audit ratio - was available
   from the brief itself and is used as given, with every conclusion that depends on it marked. If
   `02` is supplied, folding it in is an edit, not a rebuild.
2. **No repository access.** Every statement about the current system is inherited from the two input
   documents. Where they are wrong, this document is wrong in the same place - except where section 3
   corrects them from framework source.
3. **The single most important thing this document could not check is one file read**: whether
   `HostAwareDomainTenantResolveContributor` was registered with an insert at position zero or an
   append. **The entire tenant-boundary argument turns on it**, and it is a five-minute check.
   Alongside it: whether the current-user resolver is still present, and the current recovery model
   of every database.
4. **A second promised input was absent, and it was the project's own HIPAA assessment.** The
   brief's file table says `03` contains a HIPAA technical-safeguard inventory. **It does not** - its
   section 4 ends at 4.10 and the document carries no CFR citations at all. Section 12 of this
   document therefore rests on the regulation text, adversarially verified, rather than on the
   project's own view of which safeguards it currently meets. **That view exists in the repository**:
   `03` section 5 names `docs/security/HIPAA-COMPLIANCE.md` and `THREAT-MODEL.md` and says to read
   both before proposing an architecture. This research could not, and did not. Diffing them against
   Document B's Group 2 is the highest-value HIPAA-specific action available to a session with the
   repository, and it may retire several requirements.

5. **California and state-law exposure is entirely absent from this analysis**, and that is a real
   gap rather than a scoping decision. The body cites federal regulation extensively while the
   product handles **California workers' compensation and California medical records.** The
   Confidentiality of Medical Information Act and California breach-notification law impose
   obligations that differ from and can exceed HIPAA, and state medical-evaluation record rules bear
   directly on the retention question this document spends the most effort on. **A verification pass
   that has already caught one misread statute should be pointed here next**, and this is the
   question most worth an hour with counsel.
6. **The one-developer-week-per-month figure was never confirmed against a staffing commitment**, and
   section 2's whole argument is sized against it. Thresholds are stated wherever a recommendation's
   viability turns on it.
7. **Whether the pending federal Security Rule proposal has been finalised was checked and answered:
   it has not**, as of the access date, and no new federal log-retention period is in force. Plan
   against the rule as it stands. If it is finalised, several "do eventually" items become "do now".
8. **Nothing here has been measured under load, and no restore has ever been performed.** The two
   cheapest measurements in this document - a daily disk sample and one timed restore - would change
   more of it than any further research.

### The framing worth preserving

**Roughly the first 40 hours of value here is proof and deletion, not construction.** Proving the
tenant boundary, sampling the disk, cutting audit capture at source, and closing the default-server
fallthrough are four items that build almost nothing. They establish whether the design's central
claim is true, put a date on the nearest hard failure, remove an order of magnitude of growth, and
take an unauthenticated administrative console off the internet.

**And one piece of timing expires.** There is no real patient data yet. That makes credential rotation
free, destructive restore testing free, per-office encryption possible at all, the platform move a
build rather than a migration, and "no PHI in non-production" free to adopt. **All five become
expensive, and one becomes impossible, the day a real practice takes a real booking.**
