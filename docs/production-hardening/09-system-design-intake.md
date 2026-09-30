# Phase 9 -- System design research intake

**Change class:** none directly. This phase is triage and routing. It converts an external research
report into queue items, or into someone else's problem, and records which.

**Status: RECEIVED 2026-08-31. First triage pass done.** Inputs are committed at
[`../research/system-design-2026-08-28/`](../research/system-design-2026-08-28/).

The output is two deliverables (`system-design-target.md`, `infrastructure-requirements.md`) plus a
method document, two appendices and the raw agent output. **62 capability requirements, 10
application-owned requirements, 17 anti-requirements.**

**Repository verification of its claims is in
[10-research-corrections.md](10-research-corrections.md)** - four claims contradicted, three
confirmed, one corrected in the research's favour. Read that before acting on any statement the
research makes about what this code does; it had no repository access and says so.

**What has been routed so far** (the file-read subset; the runtime checks are still open):

| Finding                                        | Routed to                                                         |
| ---------------------------------------------- | ----------------------------------------------------------------- |
| APP-OWN-01/02/03 - tenant resolver assertions  | [03](03-critical-path-coverage.md) s3.1, now the phase's top item |
| No `default_server` on 443                     | [01](01-blockers.md) s1.5                                         |
| DataProtection keys unencrypted at rest        | [01](01-blockers.md) s1.6                                         |
| REQ-REL-06 - migration check in CI             | [02](02-enforcement.md) s2.6                                      |
| Audit-ratio re-derivation                      | [08](08-coverage-expansion.md)                                    |
| Platform, backup, licensing, observability, HA | Deferred list below                                               |

**Not yet triaged:** the majority of the 62 requirements. Most depend on the runtime checks or on a
platform decision, and several may be retired by the HIPAA diff (check 15). Triage them as their
phase comes up rather than in one pass - the routing rule below is what makes that safe.

---

## What arrives

The brief asks for vendor-neutral MUST/SHOULD platform capability requirements, an explicit
anti-requirements section, and a target architecture derived from the evidence -- with **no cloud
vendor named and nothing priced**, deliberately. The costed comparison from 2026-08-26 was withheld
so the architecture generates the platform requirements rather than a chosen platform back-filling
an architecture to justify it.

Expect it to cover: edge security, TLS and domain, runtime topology, observability, backup,
availability, and secrets handling.

---

## THE ROUTING RULE -- decide this before reading the report, not after

An architecture report is persuasive and arrives with momentum. Read against no boundary, it will
pull this epic into infrastructure work, and the enforcement and coverage phases -- the ones that
actually harden the codebase -- will stall behind a platform decision that is not ours to make yet.

> **A finding enters this epic only if the fix is a change to a file in THIS repository.
> If it needs a platform, procurement, or an infrastructure decision, it is recorded here and
> routed out.**

Applied:

| Enters this epic                                                | Routes out                                           |
| --------------------------------------------------------------- | ---------------------------------------------------- |
| Rate limiting on sign-in (AuthServer process, no limiter today) | WAF, DDoS absorption, CDN                            |
| Security headers in `docker/nginx-proxy/default.conf.template`  | TLS certificate and domain strategy                  |
| Startup validation of required configuration                    | Managed database selection                           |
| Structured logging, correlation ids, health check content       | Log aggregation and metrics backend                  |
| Session and token handling in application code                  | High-availability topology, load balancers           |
| Secrets _consumption_ patterns in code                          | Secret _store_ selection (Key Vault vs alternatives) |
| Anything CodeQL or Sonar can see                                | Backup infrastructure and retention                  |

The right-hand column is the **deployment architecture track**, and it is gated on exercise 3
(platform selection), which is itself gated on this report. Recording those items here is useful;
building them here is not.

Where a finding is genuinely both -- observability is the clearest case, since it needs code to emit
and a platform to collect -- split it. The emitting half enters the epic; the collecting half routes
out. Do not defer the whole item because half of it is blocked.

---

## Procedure when the report lands

1. **Do not interrupt the phase in flight.** Finish the current phase first. The report is not
   time-sensitive -- nothing ships on it, and exercise 3 has not started. Interrupting phases 1-3 to
   chase architecture recommendations is the specific failure this section exists to prevent.
2. **Commit the output** next to its inputs, in `docs/research/system-design-2026-08-28/`. Keep the
   `<office-a>` anonymisation intact if any attachment is regenerated.
3. **Triage every recommendation through the routing rule above.** Record the routing decision, not
   just the recommendation.
4. **For items entering the epic:** append them to the phase they belong to, rather than creating a
   phase 9 work queue. A rate-limiting fix is a security fix and belongs with its neighbours; an
   observability change that needs tests belongs behind phase 3. Slot by nature, not by origin.
5. **For items routing out:** list them in the "Deferred to deployment architecture" section below,
   with enough context that whoever picks up exercise 3 does not have to re-read the report.
6. **Re-check the anti-requirements.** The brief asks for them explicitly. They are the most
   valuable part for a successor, because they record what the system deliberately does NOT need --
   which is what stops a future team over-building.

---

## Interaction with the ordering

This phase does not get a fixed slot in the queue. It runs whenever the report arrives, takes an
hour or two of triage, and then dissolves into the existing phases.

**It must not become a reason to reorder.** The ordering in the [README](README.md) was chosen by
what survives a handoff, and an external report does not change that logic. If the report argues
for a different order, that is a decision to put to Adrian explicitly -- not one to absorb quietly.

---

## Deferred to deployment architecture

**Routed out 2026-08-31 from the delivered report.** Each needs a platform, procurement or an
infrastructure decision, so none is buildable in this repository. Recorded with enough context that
whoever runs exercise 3 does not have to re-read the deliverables.

| Item                                                                                                            | Why it routes out                                                                                                                                                           | Ref                 |
| --------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------- |
| Business associate agreements for **every** service in the data path, including the log and backup destinations | Contractual, not technical, and a statutory precondition to real PHI                                                                                                        | REQ-HIP-01          |
| Managed backup, patching and availability for stateful services                                                 | The team has no database specialist; the alternative is that it does not happen                                                                                             | REQ-TEAM-02         |
| 80 databases on one endpoint, each independently restorable to a point in time                                  | Where the tenancy model meets the platform hardest. **The platform's documented per-endpoint limit binds, not the engine's**                                                | REQ-TEN-04, -06     |
| Runtime `CREATE DATABASE` by an identity the application holds                                                  | A platform treating this as an administrative action breaks how the business adds an office                                                                                 | REQ-TEN-03          |
| **Database edition licensed for production**, with persisted feature dependencies checked first                 | `MSSQL_PID` is Developer, licensed for dev and test only. Tier changes remove capabilities **without raising an error**. A go-live blocker being treated as a cost decision | REQ-APP-06          |
| Backup enrolment atomic with provisioning                                                                       | Point-in-time capability is inherited from `model` at `CREATE DATABASE`, and offices are created by a business action in the host UI                                        | REQ-TEN-08, -09     |
| Object-store backup                                                                                             | It holds the actual medical documents, shares a disk with the databases and the backup directory, and **nothing currently requires it to be backed up**                     | Doc B s9.8          |
| WAF, DDoS absorption, CDN, rate-limiting infrastructure                                                         | Edge infrastructure                                                                                                                                                         | Doc B s7            |
| Multi-SAN wildcard certificate at three depths; DNS with an automatable API                                     | Requires a DNS provider decision                                                                                                                                            | REQ-TEN-10, ANTI-05 |
| Centralised logging and alerting destinations                                                                   | **Blocked behind REQ-HIP-08**: PII logging is on by default, so logs carry PHI and cannot leave the host as they are                                                        | REQ-TEAM-04         |
| Secret store with access control and a change record                                                            | Store selection is a platform decision; how the application consumes secrets is ours                                                                                        | REQ-TEAM-07         |
| A second environment provisioned by the same mechanism                                                          | Sequenced behind cheaper steps deliberately, because production is a legitimate destructive-test target **while the data is synthetic**                                     | REQ-TEAM-09         |
| Immutable retention no credential can shorten                                                                   | Offered in a weaker form by many platforms. **Test with the most privileged credential the deployment holds**                                                               | REQ-HIP-06          |

**Two that are neither ours nor the platform's, and need a human:**

- **California and state law are entirely absent from the analysis.** It argues federal retention
  clocks for a product handling California workers' compensation and California medical records.
  CMIA and state breach-notification law can exceed the federal floor, and state medical-evaluation
  record rules bear on the retention question directly. The research names this as the single item
  most worth an hour with counsel. **It is not an engineering task and should not wait for one.**
- **The capacity decision.** 734-1,084 hours of setup and 24.5-43.5 hours a month against roughly 40.
  **Do not take those figures to the business before the audit ratio is re-derived** - see
  [10-research-corrections.md](10-research-corrections.md) section 5 for why the number is suspect
  in the direction that inflates it.

Already known before the report arrived, from
[`../research/system-design-2026-08-28/03-deployment-and-constraints.md`](../research/system-design-2026-08-28/03-deployment-and-constraints.md):

- **Edge security is entirely unconfigured.** The nginx template has zero `add_header` directives --
  no HSTS, CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, no TLS pinning, no
  `limit_req`. The headers themselves are a repo file and so enter the epic; WAF and rate-limiting
  infrastructure do not.
- **No default server on 443 and an apex with no route.**
- **SQL Server licensing** for a public deployment.
- **Single point of failure at every tier.**
- **No observability** of any kind.
- **No staging environment** -- `staging` and `production` are 631 commits behind `main` and
  untouched since 2026-05-01. Adrian's standing decision (2026-08-31) is to defer the staging reset
  until this epic lands, then reset once from a known-good `main`.

---

## What this phase feeds

Exercise 3, platform selection, scores platform options against the MUST/SHOULD requirements this
report produces. **Do not start exercise 3 from this epic** -- it is a separate exercise with its
own brief, and starting it early re-introduces exactly the dependency inversion the three-exercise
sequence was designed to avoid.
