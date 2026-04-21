# Demo Logins

Copy-paste cheat sheet for manual testing and demos. All users below were created by `scripts/Master-Seed.ps1` in the current local Docker environment.

> **DEV-ONLY.** Do NOT use these credentials or this password in any deployed/staging/production environment. Rotate `TEST_PASSWORD` in `.env.local` before any deployment. Source of truth for the password: `.env.local` -> `TEST_PASSWORD`.

---

## Quick reference

| Item | Value |
| --- | --- |
| Login URL | http://localhost:4200 |
| Swagger | http://localhost:44327/swagger/index.html |
| AuthServer | http://localhost:44368 |
| Password (ALL users below) | `1q2w3E*` |

**How to log in as a tenant user** (Angular UI):

1. Open http://localhost:4200 -> Login
2. On the AuthServer login page, click the current tenant label (top/right, default is "Not selected")
3. Enter the tenant name from the headers below (e.g. `Dr Nakamura 1`) -> Save
4. Enter email + `1q2w3E*` -> Login

For Swagger: use the **Authorize** button, pick `CaseEvaluation_App`, add header `__tenant: <tenant-guid>`.

---

## Host-level (no tenant switch)

Use when testing host-scoped features: multi-tenancy management, tenant CRUD, global reference data CRUD (states/types/statuses/languages/locations/WCAB).

| Role | Email | Password |
| --- | --- | --- |
| Host Admin | `admin@abp.io` | `1q2w3E*` |

---

## Tenant: `Dr Nakamura 1` (T1)

Tenant ID: `1d4bec89-2722-f839-7d50-3a20bf45034b`
Has: 13 appointments, full role coverage.

| Role | Email | Password |
| --- | --- | --- |
| Tenant Admin / Doctor | `christopher.nakamura@hcs.test` | `1q2w3E*` |
| Patient 1 | `jose.watanabe@hcs.test` | `1q2w3E*` |
| Patient 2 | `jing.morales@hcs.test` | `1q2w3E*` |
| Claim Examiner | `yuki.rodriguez@hcs.test` | `1q2w3E*` |
| Applicant Attorney | `andrew.poghosyan@hcs.test` | `1q2w3E*` |
| Defense Attorney | `lucia.reyes@hcs.test` | `1q2w3E*` |

---

## Tenant: `Dr Flores 2` (T2)

Tenant ID: `2b9592b0-f918-6731-1d35-3a20bf451097`
Has: 10 appointments, full role coverage.

| Role | Email | Password |
| --- | --- | --- |
| Tenant Admin / Doctor | `emily.flores@hcs.test` | `1q2w3E*` |
| Patient 1 | `daniel.park@hcs.test` | `1q2w3E*` |
| Patient 2 | `maria.white@hcs.test` | `1q2w3E*` |
| Claim Examiner | `thomas.yang@hcs.test` | `1q2w3E*` |
| Applicant Attorney | `anahit.harris@hcs.test` | `1q2w3E*` |
| Defense Attorney | `carlos.wu@hcs.test` | `1q2w3E*` |

---

## Tenant: `Dr Manukyan 3` (T3)

Tenant ID: `c91dcb65-24aa-5e6d-710e-3a20bf45160e`
Has: 5 appointments, full role coverage.

| Role | Email | Password |
| --- | --- | --- |
| Tenant Admin / Doctor | `jose.manukyan@hcs.test` | `1q2w3E*` |
| Patient 1 | `rosa.mendoza@hcs.test` | `1q2w3E*` |
| Patient 2 | `hiro.wu@hcs.test` | `1q2w3E*` |
| Claim Examiner | `valentina.harutyunyan@hcs.test` | `1q2w3E*` |
| Applicant Attorney | `jennifer.white@hcs.test` | `1q2w3E*` |
| Defense Attorney | `ryu.perez@hcs.test` | `1q2w3E*` |

---

## Tenant: `Dr Tanaka 4` (T4)

Tenant ID: `59b50053-ac8e-68a4-7264-3a20bf451b59`
Has: 0 appointments. Used for edge-case testing (null-patient path).

| Role | Email | Password | Notes |
| --- | --- | --- | --- |
| Tenant Admin / Doctor | `mei.tanaka@hcs.test` | `1q2w3E*` | |
| Patient (edge case) | `gabriela.taylor@hcs.test` | `1q2w3E*` | Null-field test patient |

---

## Tenant: `Dr Wu 5` (T5)

Tenant ID: `103e351d-cba3-23e1-4927-3a20bf45210d`
Has: 0 appointments, no role users (tenant admin only). Empty-tenant smoke test.

| Role | Email | Password |
| --- | --- | --- |
| Tenant Admin / Doctor | `andrew.wu@hcs.test` | `1q2w3E*` |

---

## Demo scenarios by role

Need to demo | Log in as
--- | ---
Host-wide admin panel, tenant management | `admin@abp.io` (host)
Doctor scheduling their own availability | any `Tenant Admin / Doctor` above
Patient booking an appointment | T1 Patient 1 (`jose.watanabe@hcs.test`)
Claim Examiner review workflow | T1 Claim Examiner (`yuki.rodriguez@hcs.test`)
Applicant Attorney accessing linked appointments | T1 Applicant Attorney (`andrew.poghosyan@hcs.test`)
Defense Attorney accessing appointments | T1 Defense Attorney (`lucia.reyes@hcs.test`)
Multi-tenant isolation (same role, different tenant) | T1 CE vs T2 CE vs T3 CE
Empty-tenant UX | `andrew.wu@hcs.test` on `Dr Wu 5`
Null-patient edge case | `gabriela.taylor@hcs.test` on `Dr Tanaka 4`

---

## Regenerating / resetting

If the seeded data drifts or you want a clean slate:

```bash
# From Git Bash, repo root
export TEST_PASSWORD=$(grep '^TEST_PASSWORD=' .env.local | cut -d'=' -f2-)
rm -f scripts/seed-state.json       # clear prior completion markers
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts\Master-Seed.ps1" \
  -ApiBaseUrl "http://localhost:44327" \
  -AuthServerUrl "http://localhost:44368" \
  -SkipPrerequisites
```

After re-running, the emails above may change (names are random). Re-check:

```bash
cat scripts/seed-state.json | python -c "import json,sys; d=json.load(sys.stdin); print(d['_tenantEmails']); print(d['_userEmails'])"
```

To wipe seeded data entirely: `powershell.exe -File scripts\Remove-SeedData.ps1 -ApiBaseUrl http://localhost:44327 -AuthServerUrl http://localhost:44368 -SkipPrerequisites`.

To wipe the whole database (nuclear): `docker compose down -v && docker compose up -d --build`. This also clears the ABP-seeded host admin and all tenants, and the next `db-migrator` run will reseed only the ABP defaults (not this file's users).

---

## Source of truth

- Tenants: `GET http://localhost:44327/api/saas/tenants` (host admin token)
- User emails per tenant: `scripts/seed-state.json` -> `_tenantEmails` (tenant admins) and `_userEmails` (role users)
- Password: `.env.local` -> `TEST_PASSWORD`

If any login fails after a re-seed, regenerate this file from those sources.
