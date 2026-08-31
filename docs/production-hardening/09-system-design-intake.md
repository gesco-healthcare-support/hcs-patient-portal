# Phase 9 -- System design research intake

**Change class:** none directly. This phase is triage and routing. It converts an external research
report into queue items, or into someone else's problem, and records which.

**Status:** waiting on exercise 2. Inputs are committed at
[`../research/system-design-2026-08-28/`](../research/system-design-2026-08-28/); the output is not
in yet.

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

| Enters this epic | Routes out |
| --- | --- |
| Rate limiting on sign-in (AuthServer process, no limiter today) | WAF, DDoS absorption, CDN |
| Security headers in `docker/nginx-proxy/default.conf.template` | TLS certificate and domain strategy |
| Startup validation of required configuration | Managed database selection |
| Structured logging, correlation ids, health check content | Log aggregation and metrics backend |
| Session and token handling in application code | High-availability topology, load balancers |
| Secrets *consumption* patterns in code | Secret *store* selection (Key Vault vs alternatives) |
| Anything CodeQL or Sonar can see | Backup infrastructure and retention |

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

Populate as the report is triaged. Already known before it arrives, from
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
