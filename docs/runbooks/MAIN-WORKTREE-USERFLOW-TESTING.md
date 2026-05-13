# Main Worktree Userflow Testing — Hand-off Runbook

**Audience:** the Claude session running in `W:\patient-portal\main` whose
job is to walk every userflow end-to-end against the OLD app at
`http://localhost:4202` and surface gaps as fix tickets back to the
fix worktree (`W:\patient-portal\replicate-old-app`).

**Source of this doc:** planning session 2026-05-13 — Phase A blockers
land in PR #186; this runbook captures the operational steps the main
session must take before tests can start.

---

## Boot order

The fix worktree pauses its docker stack while main runs tests. **Don't
run both worktrees' compose stacks at once** — SQL Server, Redis,
Gotenberg, MinIO would compete for the same `127.0.0.1` ports and one
side would refuse to start. Pick one stack at a time.

From `W:\patient-portal\main`:

```powershell
docker compose down -v          # wipe volumes; clean slate
docker compose build db-migrator # CRITICAL — see "DB migrator rebuild" below
docker compose up -d --build    # boot full stack
```

Healthcheck order (sql-server → redis → minio → gotenberg → db-migrator
exits → authserver → api → angular) takes ~3–5 minutes cold.

---

## DB migrator rebuild (Issue #123)

The `db-migrator` image is **stale** in the dev cache if you haven't
rebuilt it since 2026-05-12. It applies migrations + runs every
`IDataSeedContributor`. Two seed-side changes since 2026-05-12 are
**only** in source until you rebuild:

1. **Clinic Staff role expansion** (commit `6e0a030` on
   `feat/replicate-old-app`) — Clinic Staff now gets `Default`
   permission across all operational entities. Without this, the
   appointment-view fanout endpoints 403 for Clinic Staff. The fix
   worktree's dev DB has the grants via direct SQL inserts; the main
   worktree's fresh DB will only have them if db-migrator rebuilds.
2. **Software@gesco.com inbox users** (commit `584bdd3` on
   `fix/test-handoff-blockers`, in PR #186) — the 4 real-inbox external
   users (SoftwareThree/Four/Five/Six → Patient/AA/DA/CE) are seeded by
   `DemoExternalUsersDataSeedContributor.InboxedExternalUsers`. Old
   image doesn't have this code.

**Rule:** always `docker compose build db-migrator` before `up -d --build`
when running on main. Or run a full `--no-cache` build once per session
start.

---

## Test data re-seed checklist (Issue #124)

Test data wiped mid-session 2026-05-12. Before running flows, re-seed
the artifacts the userflow tests assume exist.

### What gets wiped on `down -v`

Everything in `sqldata` + `miniodata` volumes. That's:

- Every appointment, document row, packet row.
- Every uploaded blob (TEST-DOC-001, TEST-DOC-002, packet PDFs).
- Every Hangfire job + recurring-job state.
- Every audit log.

### What's restored by `db-migrator` automatically

- Default tenant `Falkinstein` with slug `falkinstein`.
- Seeded internal users: `admin@falkinstein.test`, `supervisor@falkinstein.test`,
  `staff@falkinstein.test`, plus `it.admin@hcs.test` (host scope).
- Extra tenant admins: `SoftwareOne@evaluators.com`, `SoftwareTwo@evaluators.com`.
- Seeded external demo users: `patient@`, `adjuster@`, `applicant.attorney@`,
  `defense.attorney@falkinstein.test`.
- Inbox-routed external users (NEW after PR #186): `SoftwareThree@gesco.com`
  → Patient, `SoftwareFour@gesco.com` → Applicant Attorney,
  `SoftwareFive@gesco.com` → Defense Attorney, `SoftwareSix@gesco.com`
  → Claim Examiner.
- All host-scoped lookups (Locations, States, AppointmentTypes, AppointmentLanguages,
  AppointmentStatuses, WcabOffices, SystemParameters).
- Default password for every seeded user: `1q2w3E*r`.

### What you need to re-create by hand

Before any flow tests:

| Artifact | How to create | Why needed |
|---|---|---|
| 64 DoctorAvailability slots | Sign in as `admin@falkinstein.test` → Doctor Availabilities → bulk-add (or seed via API loop) | All booking flows need a slot to pick |
| ≥1 sample appointment per status (Pending / Approved / CheckedIn / Rejected) | Book + transition through admin UI | List + detail flows need rows in different states |
| ≥1 sample document per package kind (PatientPacket / AttyCEPacket / DoctorPacket / JointDeclaration / AdHoc) | Use upload UI as appropriate role | Document-review + packet flows |
| Sample patient with realistic demographics (synthetic per `.claude/rules/test-data.md`) | Book a new appointment as Patient | Patient-management flows |

### Tenant ID drift

The Falkinstein tenant ID drifts on every fresh DB. As of session-end
2026-05-12 it was `5DEDAC97-CC77-8B94-70BB-3A2131147C8D`, previously
`198F2E9A-...`. **Don't cache the GUID in test scripts** — always look
it up at session start:

```sql
SELECT Id FROM AbpTenants WHERE NormalizedName = 'FALKINSTEIN';
```

Or via the resolver endpoint:

```bash
curl http://localhost:44327/api/public/external-signup/resolve-tenant?name=falkinstein
```

---

## Quick credential reference

| Role | Email | Password | Subdomain |
|---|---|---|---|
| Host admin | admin@abp.io | 1q2w3E* | localhost:44368 (no subdomain) |
| IT Admin | it.admin@hcs.test | 1q2w3E*r | localhost:44368 |
| Tenant admin | admin@falkinstein.test | 1q2w3E*r | falkinstein.localhost:44368 |
| Staff Supervisor | supervisor@falkinstein.test | 1q2w3E*r | falkinstein.localhost:44368 |
| Clinic Staff | staff@falkinstein.test | 1q2w3E*r | falkinstein.localhost:44368 |
| Tenant admin (extra) | SoftwareOne@evaluators.com | 1q2w3E*r | falkinstein.localhost:44368 |
| Tenant admin (extra) | SoftwareTwo@evaluators.com | 1q2w3E*r | falkinstein.localhost:44368 |
| Patient (synthetic) | patient@falkinstein.test | 1q2w3E*r | falkinstein.localhost:44368 |
| Applicant Attorney (synthetic) | applicant.attorney@falkinstein.test | 1q2w3E*r | falkinstein.localhost:44368 |
| Defense Attorney (synthetic) | defense.attorney@falkinstein.test | 1q2w3E*r | falkinstein.localhost:44368 |
| Claim Examiner (synthetic) | adjuster@falkinstein.test | 1q2w3E*r | falkinstein.localhost:44368 |
| Patient (Gmail inbox) | SoftwareThree@gesco.com | 1q2w3E*r | falkinstein.localhost:44368 |
| Applicant Attorney (Gmail inbox) | SoftwareFour@gesco.com | 1q2w3E*r | falkinstein.localhost:44368 |
| Defense Attorney (Gmail inbox) | SoftwareFive@gesco.com | 1q2w3E*r | falkinstein.localhost:44368 |
| Claim Examiner (Gmail inbox) | SoftwareSix@gesco.com | 1q2w3E*r | falkinstein.localhost:44368 |

Note: SoftwareFour's inbox routes some mail to Junk (mailbox-level rule,
not a code issue) — see `docs/demo-readiness/2026-05-11-pre-demo.md`
item B. Check Junk folder if expected mail doesn't appear.

---

## URLs

| What | URL |
|---|---|
| OLD app (read-only reference) | http://localhost:4202 |
| AuthServer | http://localhost:44368 |
| API | http://localhost:44327 |
| Angular SPA | http://localhost:4200 |
| Tenant subdomain | http://falkinstein.localhost:{port} |
| MinIO console | http://localhost:9001 (creds in `docker/.env`) |
| Hangfire dashboard | http://localhost:44327/hangfire |
| Health check | http://localhost:44327/health-status |

---

## Reporting findings back to the fix worktree

When you find a bug, file it as a structured ticket in this format and
add it to the planning conversation (the fix worktree session will pick
it up):

```
[BUG-{NN}] {short title}

Severity: blocker | high | medium | low
Role: Patient | Applicant Attorney | Defense Attorney | Claim Examiner |
      Clinic Staff | Staff Supervisor | IT Admin | admin
Flow: {feature name from docs/parity/wave-1-parity/}
Steps to reproduce:
  1. ...
Expected:
  ...
Actual:
  ...
NEW source: src/{path}:{line}
OLD reference: P:/PatientPortalOld/{path}:{line}
```

---

## Related

- [DEMO-LOGINS.md](./DEMO-LOGINS.md) — original logins reference (some entries superseded by this doc post-PR #186)
- [DOCKER-DEV.md](./DOCKER-DEV.md) — docker compose pattern
- [LOCAL-DEV.md](./LOCAL-DEV.md) — host-side dev (non-docker)
- [docs/demo-readiness/2026-05-11-pre-demo.md](../demo-readiness/2026-05-11-pre-demo.md) — bug-list from prior demo prep including mailbox-side gotchas
