---
title: Anticipated audience Q&A for Tuesday demo
date: 2026-05-25
status: ready
audience: Adrian (presenter)
related: 2026-05-25-tuesday-demo-script.md
---

# Patient Portal Demo Q&A -- Tuesday audience prep

35 anticipated questions, ranked by likelihood within each category.
Format: **Q** / **A** / Tactic ("have ready in pocket" / "show in demo"
/ "deflect").

Critical factual corrections surfaced during research:

- **CCR 31.5 is now 90 days (waivable to 120), not the older 60-day
  rule.** The dashboard subtitle currently reads "CCR Sec. 31.5 / 60
  days" -- audience members familiar with workers-comp will flag the
  outdated number. Tactic: acknowledge the regulation was amended
  and we have not yet updated the subtitle.
- **MinIO community edition went into maintenance mode in
  December 2025.** Surface this proactively rather than getting
  caught by it; mention the production-time decision (commercial
  AIStor, fork, or migrate to S3) is on the roadmap.

---

## 1. Architecture and stack choice

**Q: Why did you pick ABP Commercial instead of just writing a plain .NET app?**
A: ABP gives multi-tenancy, permission/role management, audit logging,
and a SaaS module out of the box -- all things HIPAA expects us to
build anyway. The license fee skips 6-12 months of boilerplate we
would otherwise own and maintain.
Tactic: have ready in pocket.

**Q: Why .NET instead of Node.js?**
A: The OLD app was already .NET, and our domain (medical-legal
evaluations) maps cleanly to a typed, layered ORM-based stack with
strong tooling for SQL Server and background jobs. Node would have
meant rebuilding the data layer in a less-typed ecosystem with no
ABP equivalent.
Tactic: deflect to "parity with legacy stack."

**Q: Why Angular and not React?**
A: Angular ships with the batteries we need -- DI, forms, HTTP,
routing, RxJS -- so the team writes feature code, not plumbing. ABP
also auto-generates a typed Angular proxy from the backend, which we
lose with React. The OLD app was Angular 6, so we kept the framework
family.
Tactic: deflect to "auto-generated typed client."

**Q: Why Hangfire instead of Azure Functions or a queue service?**
A: Hangfire runs inside the same .NET process, reads from the same
SQL Server we already have, and gives us a dashboard for retries and
failures. No new infrastructure, no cold starts, no extra HIPAA
Business Associate Agreement. We can swap to Azure Functions later
if we hit scale limits.
Tactic: show in demo (Hangfire dashboard).

**Q: Why SQL Server and not Postgres?**
A: The OLD app is on SQL Server, EF Core has first-class SQL Server
support, and our hosting target (Azure or on-prem Windows) has SQL
Server licenses already. The migration cost wasn't justified for a
parity port.
Tactic: have ready in pocket.

**Q: Why OpenIddict instead of Auth0?**
A: Auth0 charges per monthly active user -- costs scale linearly
with patient count. OpenIddict is open-source, ships inside ABP, and
keeps identity data on our infrastructure for HIPAA data-residency
control. Trade-off: we own the operational burden.
Tactic: have ready in pocket.

**Q: Why MinIO instead of AWS S3?**
A: MinIO is S3-API-compatible, so we can swap to real S3 with a
config change. For now, self-hosting it lets us prove data residency,
avoid egress fees, and run the demo offline. **Heads-up: the
community edition recently went into maintenance mode (Dec 2025), so
a production decision (commercial AIStor, fork, or migrate to S3) is
on the roadmap.**
Tactic: surface as a known decision point.

**Q: Isn't this stack overkill for one law firm?**
A: For one office, yes -- but the pipeline target is many firms, and
retrofitting multi-tenancy into a single-tenant app is famously
expensive. ABP makes the "one firm today, many firms next year" path
almost free.
Tactic: deflect to "Phase 2 readiness."

---

## 2. Workers-comp domain

**Q: What's the difference between QME, AME, and Panel QME?**
A: A QME is a state-certified physician who performs medical-legal
evaluations. A Panel QME is a randomly-generated list of three QMEs
from which the parties pick one. An AME is a single physician both
attorneys agree on directly -- only available when the worker is
represented.
Tactic: have ready in pocket.

**Q: Who's the Claim Examiner versus the Defense Attorney versus the Applicant Attorney?**
A: The Claim Examiner works for the insurer and administers the claim
day-to-day. The Defense Attorney represents the employer/insurer in
litigation. The Applicant Attorney represents the injured worker on
contingency, usually 9-15% of the final settlement.
Tactic: have ready in pocket.

**Q: What is CCR Section 31.5 -- the deadline you keep referencing?**
A: It's the regulation that lets a party request a replacement QME
panel when the assigned doctor can't schedule an exam in time. **The
current bar is 90 days, waivable to 120**, after which a replacement
panel can be requested. We used to call it the "60-day rule" -- that's
the older version, and our dashboard still reads "60 days" which we
haven't updated yet.
Tactic: acknowledge gap, flag accuracy update if asked.

**Q: What is the "strike" process?**
A: When a Panel QME of three doctors is issued, each side has 10 days
to strike one name from the list. The remaining doctor performs the
evaluation. If the worker is unrepresented and doesn't pick within 10
days, the claims administrator picks.
Tactic: pocket.

**Q: What is a "joint declaration"?**
A: A filing both parties sign -- most commonly to jointly request a
replacement panel under Section 31.5, which is the fastest way to
resolve a panel dispute. The portal generates the document and routes
it for signatures.
Tactic: deflect (parity feature, not v1 demo focus).

**Q: What's a Supplemental Medical Report?**
A: A follow-up report a QME issues after the initial evaluation --
usually answering new questions from the attorneys or addressing
records that arrived late. By rule the supplemental should issue
within 60 days of the request.
Tactic: pocket.

**Q: Why does Deposition show up as an appointment type?**
A: QMEs can be deposed by either attorney about their report. We
model it as an appointment type so the doctor's calendar, billing,
and document packet all flow through the same pipeline as a regular
evaluation.
Tactic: show in demo (appointment-type dropdown).

---

## 3. Security and compliance

**Q: Is this HIPAA-compliant?**
A: The technical safeguards are in place -- TLS 1.2+ in transit, role-
based access, audit logging, password hashing. HIPAA compliance is an
organizational state, not a feature, so the answer is: the system is
built to support a compliant deployment, and the BAAs, policies, and
risk assessment are operational work that goes with go-live.
Tactic: pocket -- never say "yes, compliant" as a bare answer.

**Q: Where is PHI actually stored?**
A: Structured PHI lives in SQL Server (patient names, DOBs, claim
numbers, appointment data). Document PHI (medical records, reports)
lives in MinIO object storage. Both are isolated by tenant.
Tactic: show in demo (uploaded document round-trip).

**Q: Who can see a Social Security Number?**
A: SSN is restricted by role and need-to-know. Internal staff
(Clinic Staff, Staff Supervisor, IT Admin) and the record owner
(patient viewing own row) see the full value; external attorneys /
claim examiners see only the last 4 digits with the prefix masked.
**The redaction happens server-side at the AppService boundary --
the full value never crosses the wire to an unauthorized role.**
Tactic: show in demo if asked.

**Q: How is access controlled?**
A: ABP's permission system. Every API call checks a named permission
tied to a role. Roles map to job function -- Clinic Staff, Patient,
Attorney, Claim Examiner. Permissions are seeded at deploy time.
Tactic: show in demo (clinic-staff approval flow -- patient cannot
approve).

**Q: Are passwords hashed?**
A: Yes -- ASP.NET Core Identity uses PBKDF2 with a per-user salt. We
never store the plaintext, and password reset goes through a single-
use token sent via email.
Tactic: pocket.

**Q: What about audit logs?**
A: ABP records every API call with user, tenant, timestamp, and
changed-fields diff into a separate audit table. We can answer "who
changed this appointment and when" without writing custom code.
Tactic: pocket.

**Q: What if a user's credentials get phished?**
A: Roadmap item: MFA via OpenIddict -- supported today, not enabled
in the demo tenant. Account lockout after failed attempts is on by
default. Audit log catches the access pattern.
Tactic: deflect (Phase 2).

---

## 4. Multi-tenancy

**Q: What's the tenant model -- one DB per firm, or one shared DB?**
A: Shared DB today with row-level filtering on TenantId. ABP
automatically scopes every query to the current tenant via EF Core
global query filters. We can move a tenant to its own DB later
without code changes -- ABP-supported feature.
Tactic: pocket.

**Q: Can data leak between tenants?**
A: The filter is enforced at the ORM layer on every query, and the
tenant comes from the JWT -- a user cannot pass a different tenant
ID. The risk surface is a developer writing raw SQL or explicitly
disabling the filter, which is why we code-review those paths.
Tactic: pocket; be honest about the developer-error tail.

**Q: Can one tenant see another's appointments?**
A: No. The appointments table has a TenantId column; the filter is
applied automatically; the API endpoints rely on it. We have not yet
written cross-tenant tests in CI -- that's a hardening item.
Tactic: pocket; flag the test-gap honestly if pressed.

---

## 5. Deployment and operations

**Q: Where will this actually be hosted?**
A: Target is Azure (App Service plus Azure SQL plus Azure Blob in
place of MinIO) for the production tenant, behind our existing
identity perimeter. The current demo runs in Docker on a workstation.
Tactic: pocket.

**Q: What's the disaster-recovery story?**
A: SQL Server point-in-time backups, MinIO bucket replication, and
infrastructure-as-code so we can rebuild the environment from a
clean slate. RTO and RPO targets are still being finalized.
Tactic: pocket -- be honest the RTO/RPO is open.

**Q: How do you back up the documents?**
A: MinIO supports bucket replication to a second node or to S3.
We're planning daily snapshots plus continuous replication. In Azure,
this becomes Geo-Redundant Storage with a config flag.
Tactic: pocket.

**Q: What if a critical bug ships to production?**
A: Two layers: feature flags let us turn a broken feature off without
redeploying, and the deployment pipeline supports rolling back to the
previous container image in minutes.
Tactic: deflect to BUG-036 -- "this is exactly how the recent fix
flowed."

---

## 6. Email and notifications

**Q: Where do emails come from?**
A: SMTP credentials configured per environment. Production will use
a vetted relay (SendGrid or Microsoft Graph) under a Business
Associate Agreement so PHI in email subjects/bodies is covered.
Tactic: pocket.

**Q: Can email templates be customized per tenant?**
A: Yes -- ABP's localization plus a template-per-tenant override
pattern lets each firm rebrand subject lines, sender name, and body.
The default templates are in source control.
Tactic: deflect (Phase 2 polish -- works today, not part of demo).

**Q: What if the SMTP server is down?**
A: Outbound mail is dispatched via Hangfire jobs with retry --
failures sit in the dashboard until SMTP recovers. The user-facing
action (e.g., invite a patient) still succeeds; the email just
delivers when the channel is back.
Tactic: show in demo (Hangfire dashboard during invite-external-user).

**Q: Where are the templates stored?**
A: As JSON localization files in source, loaded at startup. Per-
tenant overrides are stored in the database under the SaaS module.
Tactic: pocket.

---

## 7. The BUG-036 demo specifically

**Q: Why did this bug happen in the first place?**
A: A document-packet regeneration path silently failed when a soft-
deleted AttyCE row blocked a fresh INSERT against the unique index.
The unit test covered the create path but not the regenerate path.
Fixed with a 3-layer solution: filtered unique index, OnCompleted
deferral, and catch-filter widening.
Tactic: show in demo (live regen succeeds first attempt).

**Q: Could it happen again?**
A: We added a regression test that fails without the fix, so the
build would catch the same defect. The filtered unique index excludes
soft-deleted rows, which is the structural fix.
Tactic: pocket.

**Q: How long was the bug in the code?**
A: Roughly since the document-packet retention feature shipped --
about six weeks. Nobody hit it because it only surfaced when a
packet was regenerated after an AttyCE retention soft-delete fired,
which is a rare workflow.
Tactic: pocket -- be honest with the timeline.

**Q: Was real patient data lost?**
A: No data loss. The bug caused the regenerate INSERT to fail with a
SQL Server unique-index violation; the source-of-truth record was
always correct, and there is no real patient data in this environment
-- we're on synthetic test data.
Tactic: pocket -- emphasize synthetic data.

---

## 8. Tangents

**Q: Why does the dashboard show 0 for "Billed This Month"?**
A: Billing is a later milestone -- the widget is wired to the data
model but the billing-event source isn't generating records yet.
Showing the widget early lets us validate layout and permissions
before the data lands.
Tactic: acknowledge and move on.

**Q: Can patients self-book appointments?**
A: Yes, with role-gated caveats. Patients pick a slot from the
doctor's published availability; clinic staff approves QME and AME
bookings because those have legal scheduling rules.
Tactic: show in demo (registration + book flow).

**Q: Where's the calendar view? This is just a list.**
A: V1 is list view, with filters by date and type -- that's what the
OLD app had and what users asked for in the parity review. A calendar
grid view is on the v2 backlog once parity ships.
Tactic: deflect to roadmap.
