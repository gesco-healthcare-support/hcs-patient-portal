---
title: Workflow test variations + quirks log
date: 2026-05-26
status: ready
audience: Adrian (presenter)
---

# Workflow test variations -- quirks log

Additional probes beyond the 5 demo flows. Captures little
variations and edge cases an audience might trigger.

## Variation 1: Tenant subdomain probes

**admin.localhost** vs **falkinstein.localhost** reachability:

| Service | admin.localhost | falkinstein.localhost |
|---|---|---|
| SPA (4200) | 200 | 200 |
| AuthServer (44368) | 200 | 200 |
| API (44327) | 200 | 200 |

Both subdomains route through the same Docker port mappings. Tenant
resolution happens server-side via the subdomain name.

**Quirk:** Logging in on admin.localhost with a tenant-only user
(e.g. patient1@gesco.com) shows "Email or password is incorrect, or
your email isn't verified" -- the host context can't find the
tenant user. **Demo tactic:** Stay on falkinstein.localhost during
the whole demo. If audience asks about admin/host scope, verbal
answer only.

## Variation 2: admin@abp.io login attempt

The default ABP admin user EXISTS in both host + Falkinstein tenant
(verified via SQL on `AbpUsers`) but **`EmailConfirmed=0`** in both.
Login fails with the same "Email or password is incorrect, or your
email isn't verified" message.

**Demo tactic:** Do NOT attempt admin@abp.io login. If audience
asks how to admin the system, point at the IT Admin role (granted
to `it.admin@hcs.test` which IS confirmed in the host pass).

## Variation 3: Available demo accounts -- full list from DB

Synthesized from `AbpUsers WHERE IsDeleted=0`:

**Host-scoped (no TenantId):**
- `admin` / `admin@abp.io` -- EmailConfirmed=0 (broken)
- `it.admin@hcs.test` -- EmailConfirmed=1, IT Admin

**Falkinstein tenant-scoped:**
- `admin` / `admin@abp.io` -- EmailConfirmed=0 (broken)
- `adjuster@falkinstein.test` -- EmailConfirmed=1
- `admin@falkinstein.test` -- EmailConfirmed=1
- `appatty1@gesco.com` -- EmailConfirmed=1 (demo)
- `applicant.attorney@falkinstein.test` -- EmailConfirmed=1
- `clistaff1@gesco.com` -- EmailConfirmed=1 (demo)
- `defense.attorney@falkinstein.test` -- EmailConfirmed=1
- `patient@falkinstein.test` -- EmailConfirmed=1 (blank name -- OBS-39)
- `patient1@gesco.com` -- EmailConfirmed=1 (demo Alex Patient)
- `staff@falkinstein.test` -- EmailConfirmed=1
- `stafsuper1@gesco.com` -- EmailConfirmed=1 (demo)
- `supervisor@falkinstein.test` -- EmailConfirmed=1

**Account naming convention:** `@gesco.com` = the 4 official demo
accounts. `.test` accounts = secondary test seed (do not show in
demo). `@abp.io` admins are unverified default seeds.

## Variation 4: Wire-payload verification of F4-02 fix

Logged in as patient1, navigated to A00002 (Rejected QME), captured
`/api/app/appointments/with-navigation-properties/<id>` response:

```json
{
  "appointment": {
    "appointmentStatus": 3,
    "rejectionNotes": "Doctor schedule conflict - demo rejection test reason.",
    "rejectedById": "914d9454-74f2-4c8e-9bd9-05182fe9eb33",
    "internalUserComments": null,
    ...
  }
}
```

**Patient now sees:**
- `rejectionNotes` -- the staff's reason for rejecting
- `rejectedById` -- audit pair
- `internalUserComments: null` -- correctly redacted by
  ExternalUserDtoFilter for external roles

F4-02 fix verified end-to-end on the wire.

## Variation 5: Appointment status-button matrix verified

| Status | Approve | Reject | Save | Upload | Regenerate | Notes |
|---|---|---|---|---|---|---|
| Pending (1) | yes | yes | yes | yes | yes | A00003 |
| Approved (2) | hidden | hidden | yes | yes | yes | A00001 |
| Rejected (3) | hidden | hidden | yes | yes | yes | A00002 |

Approve/Reject correctly status-conditional. Save/Upload/Regenerate
visible across all 3 -- intentional or future polish item.

## Variation 6: Multi-tab + back + refresh

- Two tabs same-user: both stay logged in.
- Browser back button: returns to previous page, session intact.
- Page refresh on /appointments/add: form re-renders cleanly,
  session intact.
- Logout-then-login cycle: works once authserver is healthy.

## Variation 7: Patient nav menu (CORRECTED)

Patient login displays only Home content. Sidebar with master-
table links (Applicant Attorneys / Defense Attorneys / Doctor
Management) is HIDDEN by the `externaluser-role` body class
(app.component.ts:101-111 + styles.scss:73-92).

Earlier "leak" finding was from DOM-query inspection; actual
visual state has the sidebar `display: none`. See
`role-probe-live-findings.md` for the correction.

## Variation 8: Container restart instability

The api + authserver containers occasionally crash during the
post-edit hot-reload cycle. Pattern:

1. File watcher detects bind-mount source change.
2. dotnet build runs.
3. Under WSL2 memory pressure, OOM CSC error: "Cannot allocate
   memory" writing the `.pdb`.
4. Container exits unhealthy.

**Mitigation for demo:** `docker compose ps` before starting.
Restart any service marked `unhealthy` and wait ~60 seconds for
the build + run cycle. Do NOT edit source files mid-demo.

## Variation 9: SPA load timing across cold/warm

- Cold cache: ~600-800 KB over wire (gzipped via npx serve);
  ~2.5-3 seconds to interactive.
- Warm cache: ~7 KB transfer, ~1.16 seconds navigation.
- Browser-sync polling visible in network tab (dev-only,
  innocuous).

## Variation 10: Hangfire dashboard observation

Across multiple checks tonight:
- Jobs (enqueued): 0
- Retries: 0
- Recurring: 9 jobs scheduled in America/Los_Angeles tz
- Servers: 1
- Total successful jobs: 55+
- Failed jobs: 0

Clean stack health. Audience peek into Hangfire dashboard during
demo will see a green-green-green status.

## Variation 11: HangFire URL is accessible without auth

Browsed to http://falkinstein.localhost:44327/hangfire -- got the
dashboard directly without an auth challenge. ABP's default
HangfireDashboard exposes itself to authenticated users via cookie
auth; in dev with the in-process API, this happens to be
publicly accessible. **Production risk** (not demo risk): Hangfire
dashboard should require IT Admin permission post-cutover.

## Variation 12: Existing appointment status meanings

```
1  Pending
2  Approved
3  Rejected
4  NoShow
5  CancelledNoBill
6  CancelledLate
7  RescheduledNoBill
8  RescheduledLate
9  CheckedIn
10 CheckedOut
11 Billed
12 RescheduleRequested
13 CancellationRequested
```

A00001 / A00002 / A00003 cover the 3 most demo-relevant: 2 / 3 / 1.
