# CHECKPOINT 1 -- local prod-compose verification

Bring the production stack up on the local machine against a fake domain and prove the
never-before-run Production code path works BEFORE touching any server. This is the
Phase 1 hard STOP of `docs/plans/2026-07-09-in-house-hosting.md`.

Base domain for local verification: `portal.local`.

## 1. Prerequisites (one-time)

- Docker Desktop running.
- Two secrets only Adrian can supply, put in `secrets/env.prod` (copy `env.prod.example`):
  - `ABP_NUGET_API_KEY` -- required to BUILD the .NET images (private ABP feed).
  - `ABP_LICENSE_CODE` -- required at runtime.
  Fill the other placeholders with throwaway LOCAL values (SQL/MinIO/encryption
  passwords, the OpenIddict passphrase). `BASE_DOMAIN=portal.local`.

## 2. Windows hosts file (admin)

Hosts files cannot express wildcards, so add explicit entries. Edit
`C:\Windows\System32\drivers\etc\hosts` (as Administrator) and add:

```text
127.0.0.1 admin.portal.local        admin.api.portal.local        admin.auth.portal.local
127.0.0.1 falkinstein.portal.local  falkinstein.api.portal.local  falkinstein.auth.portal.local
127.0.0.1 hekmat.portal.local       hekmat.api.portal.local       hekmat.auth.portal.local
127.0.0.1 typo.portal.local         typo.api.portal.local         typo.auth.portal.local
```

## 3. Generate local secrets material

```bash
# Wildcard TLS cert (mkcert if present -> trusted; else openssl self-signed).
scripts/hosting/gen-local-certs.sh portal.local secrets

# OpenIddict token-signing cert. Use the SAME passphrase as AUTHSERVER_CERT_PASSPHRASE
# in secrets/env.prod.
AUTHSERVER_CERT_PASSPHRASE="<the same value>" \
  scripts/hosting/gen-openiddict-cert.sh ./secrets/openiddict.pfx
```

## 4. Bring up the stack

Option A (fast path -- DbMigrator seeds the sample offices + <it.admin@hcs.test> into the
volume; app services run Production):

```bash
docker compose -f docker-compose.prod.yml -f docker-compose.prod.localseed.yml \
  --env-file secrets/env.prod up -d --build
```

Option B (confirming run -- full Production; no seeding; create an office via the host
UI): omit the localseed override.

Watch health: `docker compose -f docker-compose.prod.yml ps` (all healthy; db-migrator
exited 0). AuthServer/API cold start can take a couple of minutes.

## 5. CHECKPOINT 1 assertions (ADR-007, on PROD hostnames through nginx on 443)

```bash
curl -k -H "Host: admin.api.portal.local"        https://127.0.0.1/api/abp/application-configuration  # => 200 (host)
curl -k -H "Host: falkinstein.api.portal.local"  https://127.0.0.1/api/abp/application-configuration  # => 200 (Falkinstein)
curl -k -H "Host: typo.api.portal.local"         https://127.0.0.1/api/abp/application-configuration  # => 404 "Tenant not found!"
```

## 6. Login + G8 observation

- Log in over HTTPS at `https://admin.portal.local` (host admin) and at
  `https://falkinstein.portal.local` (office). Creds: Option A -> `it.admin@hcs.test` /
  `1q2w3E*r`; the office admin is `admin@falkinstein.test` / `1q2w3E*r`. Option B -> the
  ABP host admin (AdminEmailDefaultValue / `1q2w3E*r`), then create an office via the UI.
- OBSERVE G8: with `SetIssuer(AuthServer:Authority)` still pinned, does the office login
  fail the SPA's OIDC discovery issuer check (expected `https://falkinstein.auth...`,
  current `https://auth...`)? Record the exact discovery `issuer` seen for admin AND an
  office. This drives the T9 fix (make the issuer per-request).

## 7. Deep adversarial probes (per Adrian, 2026-07-09)

- Silent token refresh over HTTPS (no re-login churn / redirect loop).
- Auth cookie + CSRF scope across the three per-service subdomains (no cross-office leak).
- Cross-office isolation THROUGH the proxy: an office-A session cannot read office-B data
  at office-B's subdomain (ADR-017 boundary, live).
- Forced restart mid-session: `docker compose -f docker-compose.prod.yml restart authserver api`
  -> the session + a pending email-confirmation token still validate (Redis DataProtection).
- Malformed / spoofed Host headers (unknown slug, empty leftmost label, `admin`
  look-alikes, injected `__tenant` query/cookie/header) still 404 or stay host-scoped.
- Option B: create an office via the host UI under full Production, then repeat 5-7.

## Teardown (NON-destructive -- never use `down -v`)

```bash
docker compose -f docker-compose.prod.yml down        # keeps the named volumes
```
