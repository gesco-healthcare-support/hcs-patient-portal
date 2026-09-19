# Research brief: system design and infrastructure architecture

## Your task, in one sentence

Determine what **shape** this system and its infrastructure should take for a public-facing,
HIPAA-regulated, multi-tenant scheduling platform serving 11 offices at launch and up to 33 --
and express the result as **vendor-neutral requirements** that a later platform-selection exercise
can be scored against.

## Where this sits in a sequence, and why that constrains you

Three exercises, deliberately ordered:

1. **Code standard** -- complete. Established what an industry-standard CI pipeline, test suite and
   check set looks like, and produced a 50-item remediation plan.
2. **System design and infrastructure** -- **this one.**
3. **Platform and cloud selection** -- after this.

**Do not select a cloud vendor.** Do not recommend Azure, AWS, GCP, DigitalOcean or anyone else,
and do not price anything. A separate exercise already produced a costed vendor comparison, and it
is deliberately not attached, because anchoring this work to a provider inverts the order: the
architecture should determine the platform requirements, not the reverse.

Where a design choice genuinely depends on capabilities that only some providers have, say so as a
**capability requirement** ("the platform must support X for reason Y"), never as a vendor name.

## Who this is for

**Two developers, both SDE 1.** No senior engineer, no architect, no DevOps engineer, no security
specialist, no QA. Roughly one developer-week per month is available for infrastructure work
alongside feature delivery, and the code-standard exercise has already claimed a large share of
that.

**Treat that figure as an estimate, not a measured allocation.** It has never been confirmed
against a real staffing commitment. Where a recommendation's viability turns on it, say so and
give the threshold -- "this is operable at N hours a month, and below that it decays to X" --
rather than assuming the number holds.

This has three consequences for your output, and they are not softeners:

1. **Operational complexity is a first-class cost.** An architecture that is theoretically better
   but requires expertise this team does not have is worse, not better. Rank designs by what
   survives contact with two junior developers and no on-call rotation.
2. **Prefer designs that fail obviously over designs that fail subtly.** This team will not
   notice a slow degradation. They will notice a hard error.
3. **Say what not to build.** Explicitly reject the patterns that are standard at larger
   organisations and wrong here, with reasoning. That list is as valuable as the recommendations.

## What you are given

You do **not** have the repository or the running system. Everything you need has been written
down for you. Three documents, all verified by direct source reads and live database queries on
2026-08-28:

| File                               | What it is                                                                                                                                                                                                                                                                                                                                   |
| ---------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `01-system-architecture.md`        | **Read first.** Component inventory, the tenancy model and how it resolves, identity and session mechanics, runtime processes and startup order, what Redis actually does, all twelve background jobs with schedules, storage and blob containers, integrations, an explicit list of what is deliberately absent, and a failure/coupling map |
| `02-runtime-and-data-profile.md`   | **Measured** database sizes, table-level row counts, business entity counts, the configuration surface, current operational procedures, and an explicit list of what has never been measured                                                                                                                                                 |
| `03-deployment-and-constraints.md` | The current deployment in detail, the eight hard constraints, the gaps a public deployment must close, and a HIPAA technical-safeguard inventory                                                                                                                                                                                             |

These are descriptive by design. They contain no recommendations, because recommending is your job.

Identifiers are generalised (`<office-a>`, `<corp-zone>.local`). The shapes are real.

## Five facts from those documents that should shape everything you write

Pulled out because they are easy to miss and each one changes an answer:

1. **Tenancy resolves from the HTTP `Host` header and nothing else.** Any infrastructure component
   that rewrites, normalises or collapses `Host` breaks tenant resolution outright. A bare IP
   address cannot reach the application at all. This constrains load balancers, WAFs, CDNs, health
   checks and service meshes.
2. **Audit data outnumbers business data by more than a hundred to one.** Sixteen appointments
   produced roughly 1,450 audit-log rows and 2,689 entity property-change rows in one office
   database. Audit retention is unconfigured, nothing prunes it, and a six-year retention
   expectation applies. **Audit growth, not appointment volume, is the sizing driver.**
3. **Redis carries three separate load-bearing jobs**: distributed cache, the ASP.NET
   DataProtection keyring shared between two processes, and the distributed lock. Losing its
   contents invalidates every session and breaks in-flight email-confirmation links.
4. **There is no server-side rendering and no WebSocket usage.** No Node runtime in production, no
   sticky-session requirement for realtime. The reverse proxy still carries dead WebSocket
   configuration for a SignalR hub that does not exist.
5. **Adding an office is currently a host-UI action, not a deployment**, and office connection
   strings are derived from one configuration key. Both properties are load-bearing to how the
   business expects to grow, and a design that breaks either should say so loudly.

## What to research

Twelve areas. For each: what the standard or the range of viable designs is, with citable sources;
what this system currently does; and what you recommend, with the operational cost stated.

1. **Tenancy and data architecture.** Is database-per-tenant the right model at 11 offices, at 33,
   and beyond? What are the alternatives (shared schema with a discriminator, schema-per-tenant,
   sharded pools), what does each cost operationally, and at what count does the answer change?
2. **Audit and retention architecture.** Given fact 2 above, should audit data live in the same
   database as operational data? Cover separation, retention, tamper-evidence, and how a six-year
   obligation is met without making every backup and restore carry six years of audit rows.
3. **Scaling model and topology.** What is the scaling unit? Can this run more than one API
   instance, and what breaks if it does -- Hangfire servers, the distributed lock, cache
   coherence, the migration runner? What has to change before horizontal scaling is even possible?
4. **State and session architecture.** Where should Redis live given what it holds? Co-located,
   managed, clustered? What is the blast radius of each choice, and what does the DataProtection
   keyring require of it?
5. **Availability and disaster recovery.** What availability design is proportionate here? Cover
   backup architecture for database-per-tenant specifically -- per-office restore is a different
   problem from instance restore -- plus restore rehearsal, and realistic recovery point and
   recovery time objectives for a business of this size.
6. **Environment strategy.** How many environments should exist, with what parity, and how are
   they provisioned? Today there is exactly one, and any change reaching users has been tested
   only on the machine that serves them.
7. **Network and exposure architecture.** Given fact 1: edge design, TLS termination, WAF
   placement, internal segmentation, and what must be true of any component in the request path.
   Include what the health-check design has to look like when a bare IP returns "Tenant not found".
8. **Secrets and configuration architecture.** From one mode-600 file on a server to what? Cover
   runtime secrets, build-time secrets (the ABP licence is needed at image build), rotation, and
   the two secrets whose loss is unrecoverable rather than inconvenient.
9. **Observability architecture.** Note the constraint: PII logging is enabled by default, so logs
   currently cannot leave the machine without carrying PHI. Treat **HIPAA audit logging and
   application logging as separate pipelines with different retention, access and integrity
   requirements**, and say what each needs.
10. **Infrastructure as code and reproducibility.** Today: a compose file and a manual SSH
    procedure with two documented traps. What is the proportionate level of automation for this
    team, and what is over-engineering?
11. **Capacity model.** Build a defensible model from the measured baseline in
    `02-runtime-and-data-profile.md`. Be explicit about which inputs are measured, which are
    extrapolated, and which are unknown. **No load test has ever been run**, so the model's job is
    to say what to measure and what thresholds to watch, not to assert throughput.
12. **Migration path.** How does the system get from its current single-VM shape to the target
    without a big-bang cutover? Sequence it, and name what must be true before each step.

## Five specific questions to resolve

These block decisions and each has a determinate answer:

1. **Is database-per-tenant defensible at 33 offices, and what is the actual breaking point?**
   Name the constraint that binds first -- connection count, backup window, migration fan-out,
   per-database platform limits, or operational attention.
2. **Should audit data be separated from operational data, and if so how?** The measured ratio is
   in `02`. The six-year obligation is in `03`.
3. **Can this application run more than one API instance today?** Work through Hangfire, the Redis
   lock, the DataProtection keyring and the migration runner. If not, name precisely what blocks
   it and what the smallest change is that unblocks it.
4. **What must be true of every component in the request path** for Host-header tenancy to keep
   working, and which common infrastructure patterns violate it?
5. **What does per-office restore require?** Restoring one office to a point in time is a
   different operation from restoring an instance, and the answer shapes the entire backup
   architecture.

## Rules of evidence

Carried forward from the previous exercise, which caught a fabricated standards quotation and a
misread statute in its own verification pass. They apply here.

- **Cite a source with a URL and access date for every standard, limit, threshold or platform
  capability you assert.** Prefer first-party vendor documentation, NIST, and recognised
  architecture references over blog posts and consultancy material.
- **Do not invent limits, capabilities or product behaviours.** Verify before asserting.
- **Mark anything you could not verify as `UNVERIFIED`**, with what you tried.
- **If you have no web access**, say so at the top and mark every external claim accordingly.
- **Distinguish your reasoning from sourced fact.** Architecture is judgement; say which parts are
  yours so they can be argued with.
- **Where the attached documents are wrong or internally inconsistent, say so explicitly.** They
  were verified by direct reads, but they can still be wrong.

## What to produce

**Document A -- `system-design-target.md`.**

- A one-page summary a developer can act on, leading with the three decisions that matter most.
- Per-area detail for the twelve areas: current state, the viable options, the recommendation, the
  operational cost, and what would change the answer.
- **A target architecture description** at a level someone could build from -- components, their
  boundaries, what talks to what, where state lives, where the trust boundaries are.
- Answers to the five specific questions.
- **An explicit "not recommended" section**: patterns that are standard elsewhere and wrong here,
  each with the reason. Microservices, service mesh, Kubernetes, event sourcing, multi-region and
  read replicas are all candidates -- evaluate them honestly rather than dismissing them, and
  reject with reasoning where rejection is right.

**Document B -- `infrastructure-requirements.md`.**

The vendor-neutral requirements list, written so the platform-selection exercise can score
candidates against it. Each requirement should be:

- stated as a capability, not a product
- marked **MUST** or **SHOULD**, with the reason for the classification
- traceable to something in the attached documents or to a cited standard
- testable -- somebody must be able to determine whether a given platform satisfies it

Group them so a reader can see which requirements are driven by the tenancy model, which by HIPAA,
which by team size, and which by the application's specific mechanics. Include a short section on
**anti-requirements**: things a platform must _not_ do, of which "must not rewrite the Host header"
is the obvious first entry.

Write in plain ASCII. No em dashes, no smart quotes.

## Where to stop and ask

Proceed without stopping unless a load-bearing fact in the attached documents appears wrong, in
which case confirm before building on the correction. Otherwise state assumptions inline and keep
going.

## Done when

- [ ] All twelve areas are covered with a recommendation and its operational cost.
- [ ] The five specific questions each have an answer or an explicit "could not determine".
- [ ] Every asserted limit, capability or standard carries a source URL and access date, or is
      marked `UNVERIFIED`.
- [ ] Document B's requirements are all capability-shaped, classified MUST or SHOULD, and testable.
- [ ] The anti-requirements section is non-empty.
- [ ] The "not recommended" section engages with each rejected pattern rather than dismissing it.
- [ ] **No cloud vendor is named as a recommendation anywhere in either document.**
- [ ] The capacity model states clearly which of its inputs are measured and which are guesses.
