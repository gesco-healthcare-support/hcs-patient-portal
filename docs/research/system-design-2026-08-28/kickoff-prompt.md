# Kickoff prompt -- paste this alongside the four uploaded files

---

I need you to run a research project. Four files are attached. **Three of them are input, not the
answer; the fourth is your specification.**

## What you are doing

Determine what **shape** this system and its infrastructure should take, and express the result as
**vendor-neutral requirements** a later platform-selection exercise can score candidates against.

The application is a workers' compensation medical-examination scheduling portal: .NET 10, ABP
Commercial, Angular 20, SQL Server, EF Core, OpenIddict, Redis, MinIO, all containerised.
Multi-tenant with **one SQL Server database per office**, 11 offices at launch and headroom to 33.
**Tenancy resolves from the HTTP Host header and nothing else.** It runs today on a single internal
VM and is about to face the public internet for the first time. It is maintained by **two SDE 1
developers with no senior engineer, no architect, no DevOps and no QA**, at roughly one
developer-week per month -- an estimate, not a confirmed allocation. Where a design's viability
depends on that number, state the threshold rather than assuming it holds.

## The one instruction that matters most

**Do not pick a cloud provider. Do not price anything.**

This is the middle of three exercises. The code standard is done. Platform and cloud selection
comes _after_ this one, deliberately, and a costed vendor comparison already exists and is being
withheld from you on purpose. Anchoring this work to a provider inverts the order -- the
architecture should generate the platform requirements, not the other way round.

Where a design genuinely needs a capability only some providers have, state it as a capability
requirement ("the platform must support X because Y"), never as a product name.

## Read this first

**`00-RESEARCH-BRIEF.md` is the specification.** Twelve research areas, five specific questions,
the evidence rules, the exact output format, and the definition of done. Read it completely before
starting.

The other three are the system described for you, because **you do not have the repository or
access to the running system**. They were written specifically so you would not need it:

- `01-system-architecture.md` -- components, the tenancy model and how it resolves, identity and
  session mechanics, runtime processes and startup order, what Redis actually does, all twelve
  background jobs with their cron schedules, storage, integrations, what is deliberately absent,
  and a failure/coupling map.
- `02-runtime-and-data-profile.md` -- **measured** database sizes, table-level row counts, business
  entity counts, the configuration surface, current operational procedures, and an explicit list
  of what has never been measured.
- `03-deployment-and-constraints.md` -- the current deployment, the eight hard constraints, the
  gaps a public deployment must close, and a HIPAA technical-safeguard inventory.

All three are descriptive by design and contain no recommendations. That is your job.

## This is a research task, not a summarisation task

The most likely failure is a session that reads the attachments, reorganises them into an
architecture diagram, and hands back something that never left the room. The attachments say what
the system **is**. They say nothing about what it **should** be.

**Search the web for every standard, limit and platform capability you assert.** Your training data
has a cutoff and this is a 2026 question about capabilities and limits that change quarterly.
Specifically:

- Do not state a database or connection limit from memory. Fetch it.
- Do not assert what a load balancer does to the Host header without opening its documentation.
- Do not recommend a tool or pattern without confirming it is current and maintained.
- Do not quote a HIPAA requirement without a CFR citation.

Start from first-party vendor documentation, NIST, and recognised architecture references
(Microsoft's own multi-tenant SaaS architecture guidance is directly relevant given the stack).
Cross-reference anything load-bearing.

## Five facts from the attachments that should shape everything

Easy to miss, and each one changes an answer:

1. **Host-header tenancy.** Anything in the request path that rewrites, normalises or collapses
   `Host` breaks tenant resolution outright. A bare IP cannot reach the application at all. This
   constrains load balancers, WAFs, CDNs, health checks and service meshes.
2. **Audit data outnumbers business data by over a hundred to one.** Sixteen appointments produced
   ~1,450 audit rows and ~2,689 property-change rows in one office database, retention is
   unconfigured, nothing prunes it, and six-year retention applies. **Audit growth is the sizing
   driver, not appointments.**
3. **Redis holds three load-bearing things**: the cache, the DataProtection keyring shared across
   two processes, and the distributed lock. Losing its contents invalidates every session.
4. **No server-side rendering and no WebSockets.** No Node runtime in production, no sticky-session
   requirement.
5. **Adding an office is a host-UI action, not a deployment**, and office connection strings derive
   from one config key. Both are load-bearing to how the business grows.

## Method

1. Read the brief and all three attachments fully before writing anything.
2. For each of the twelve areas, establish the range of viable designs **first**, then compare
   against what this system does. Starting from the current design finds only the problems it
   already hints at.
3. Answer the five specific questions. Each has a determinate answer.
4. Write both documents.

## What I need back

- `system-design-target.md` -- the target architecture, per-area recommendations with their
  operational cost, the five answers, and an explicit **not recommended** section that engages
  with each rejected pattern rather than dismissing it.
- `infrastructure-requirements.md` -- vendor-neutral, capability-shaped, MUST/SHOULD classified,
  traceable and testable, including an **anti-requirements** section.

## What would make this useless

- **Naming a cloud vendor.** The whole point of doing this before platform selection is lost.
- **An architecture this team cannot operate.** Two SDE 1s, no DevOps, no on-call. Operational
  complexity is a first-class cost, not a footnote. Rank designs by what survives contact with
  that team.
- **Uncited assertions.** They cannot evaluate whether a claimed limit is real.
- **Reflexively recommending Kubernetes, microservices, service mesh, event sourcing or
  multi-region.** Evaluate each honestly and reject with reasoning where rejection is right --
  but do the evaluation, do not skip it in either direction.
- **A capacity model that asserts throughput.** No load test has ever been run. The model's job is
  to say what to measure and which thresholds to watch.

## Start by

Telling me in a few lines what you will research and in what order, and flagging anything in the
brief you think is scoped wrongly or missing. Then begin, and work through to both documents
without stopping for approval, stating assumptions inline where you must make them.
