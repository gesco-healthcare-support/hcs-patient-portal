# Demo Logins

> Purpose: Copy-paste credential cheat sheet for manual testing and demos. Audience: Developer.

Copy-paste cheat sheet for manual testing and demos.

> **STALE TENANT TABLES (2026-10-01).** The per-tenant tables below (Nakamura, Flores, Manukyan,
> Tanaka, Wu) were produced by the retired PowerShell harness (`scripts/Master-Seed.ps1`), which can
> no longer run. They do NOT describe what a fresh environment contains. For the current demo
> accounts, see [Regenerating / resetting](#regenerating--resetting). Treat the tables as historical
> until this page is rewritten.

> **DEV-ONLY.** Do NOT use these credentials or this password in any deployed/staging/production environment. Rotate `TEST_PASSWORD` in `.env.local` before any deployment. Source of truth for the password: `.env.local` -> `TEST_PASSWORD`.

---

## Quick reference

| Item | Value |
| --- | --- |
| Login URL | <http://localhost:4200> |
| Swagger | <http://localhost:44327/swagger/index.html> |
| AuthServer | <http://localhost:44368> |
| Password (ALL users below) | `1q2w3E*` |

**How to log in as a tenant user** (Angular UI):

1. Open <http://localhost:4200> -> Login
2. On the AuthServer login page, click the current tenant label (top/right, default is "Not selected")
3. Enter the tenant name from the headers below (e.g. `Dr Nakamura 1`) -> Save
4. Enter email + `1q2w3E*` -> Login

For Swagger: use the **Authorize** button and pick `CaseEvaluation_App`. Do NOT add a `__tenant`
header: the API ignores it on purpose. The office comes from the signed-in user, or, for an anonymous
request, from the host name (`<office>.localhost`).

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

| Need to demo | Log in as |
| --- | --- |
| Host-wide admin panel, tenant management | `admin@abp.io` (host) |
| Doctor scheduling their own availability | any `Tenant Admin / Doctor` above |
| Patient booking an appointment | T1 Patient 1 (`jose.watanabe@hcs.test`) |
| Claim Examiner review workflow | T1 Claim Examiner (`yuki.rodriguez@hcs.test`) |
| Applicant Attorney accessing linked appointments | T1 Applicant Attorney (`andrew.poghosyan@hcs.test`) |
| Defense Attorney accessing appointments | T1 Defense Attorney (`lucia.reyes@hcs.test`) |
| Multi-tenant isolation (same role, different tenant) | T1 CE vs T2 CE vs T3 CE |
| Empty-tenant UX | `andrew.wu@hcs.test` on `Dr Wu 5` |
| Null-patient edge case | `gabriela.taylor@hcs.test` on `Dr Tanaka 4` |

---

## Regenerating / resetting

**Do not use `scripts/Master-Seed.ps1` or `scripts/Remove-SeedData.ps1`.** They belong to a retired
PowerShell harness that cannot run against the current app: it signs in with the OAuth password
grant, removed on 2026-05-19, and selects offices with a `__tenant` header the API ignores. Both
scripts now stop at the start and say so.

Local demo data comes from the `db-migrator` seed. The local Docker stack runs it with
`DOTNET_ENVIRONMENT=Development`, which is what enables the demo seed contributors. To re-run it
against the existing databases (idempotent; existing rows are left alone):

```bash
docker compose run --rm db-migrator
```

In Development it creates, for each office the seed provides:

- one demo user per external role, from `DemoExternalUsersDataSeedContributor`:
  `patient@<slug>.test`, `adjuster@<slug>.test` (Claim Examiner),
  `applicant.attorney@<slug>.test` and `defense.attorney@<slug>.test`;
- the patient login with a linked Patient record, from `DemoPatientDataSeedContributor`, so My
  Profile resolves.

Both live in `src/HealthcareSupport.CaseEvaluation.Domain/Identity/`; read them for the exact
behaviour and password source rather than relying on this page.

To wipe the whole database: `docker compose down -v && docker compose up -d --build`. The next
`db-migrator` run then reseeds from scratch.

---

## Source of truth

- Tenants: `GET http://localhost:44327/api/saas/tenants` (host admin token)
- Demo user emails: the seed contributors named under [Regenerating / resetting](#regenerating--resetting)
- Password: `.env.local` -> `TEST_PASSWORD`

If any login fails after a re-seed, regenerate this file from those sources.
