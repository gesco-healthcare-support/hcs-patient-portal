# CHECKPOINT 1 -- local prod-compose verification

> Purpose: run the production compose stack on a local machine against a fake domain, and prove the
> Production code path works BEFORE touching any server.
> Audience: whoever prepares a hosting change for the server.
> Owner: the portal maintainer.
> **Last tested: run at least once; confirmed by the portal maintainer on 2026-09-28; date of the
> run not recorded.**

## When you need this page

- Before the first deploy of the production stack to a new server.
- Before deploying a change to the reverse proxy, the compose files, TLS, tenant resolution, or the
  AuthServer's issuer and redirect handling.

It is a local rehearsal. It touches no server, and its teardown keeps its own data.

Base domain for local verification: `portal.local`.

## 1. Prerequisites (one-time)

- Docker Desktop running.
- Two secrets only the portal maintainer can supply, put in `secrets/env.prod` (copy `env.prod.example`):
  - `ABP_NUGET_API_KEY` -- required to BUILD the .NET images (private ABP feed).
  - `ABP_LICENSE_CODE` -- required at runtime.
  Fill the other placeholders with throwaway LOCAL values (SQL/MinIO/encryption
  passwords, the OpenIddict passphrase). `BASE_DOMAIN=portal.local`.

## 2. Windows hosts file (admin)

Hosts files cannot express wildcards, so add explicit entries. Edit
`C:\Windows\System32\drivers\etc\hosts` (as Administrator) and add:

```text
127.0.0.1 admin.portal.local        admin.api.portal.local        admin.auth.portal.local
127.0.0.1 test-office.portal.local  test-office.api.portal.local  test-office.auth.portal.local
127.0.0.1 typo.portal.local         typo.api.portal.local         typo.auth.portal.local
```

`test-office` is the synthetic office every environment seeds (`OfficeSeedData`). `typo` is a
deliberately unknown office, used to prove an unknown host is refused. Add a line for any office
you create under Option B.

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

Option A (fast path -- the DbMigrator runs as Development, so it also seeds the TEST office's
admin, the demo users and <it.admin@hcs.test> into the volume; app services run Production):

```bash
docker compose -f docker-compose.prod.yml -f docker-compose.prod.localseed.yml \
  --env-file secrets/env.prod up -d --build
```

Option B (confirming run -- full Production; no seeding; create an office via the host
UI): omit the localseed override. The migrator then generates the host admin's password into the
admin-password folder, so create that folder first, owned by the containers' user (uid 1654), as
`env.prod.example` describes under `ADMIN_PASSWORD_DIRECTORY`; a folder Docker creates itself is
owned by root, and the migrator's first write fails.

Watch health: `docker compose -f docker-compose.prod.yml ps` (all healthy; db-migrator
exited 0). AuthServer/API cold start can take a couple of minutes.

## 5. CHECKPOINT 1 assertions (ADR-007, on PROD hostnames through nginx on 443)

```bash
curl -sk -o /dev/null -w '%{http_code}\n' -H "Host: admin.api.portal.local"        https://127.0.0.1/api/abp/application-configuration  # expect 200 (host)
curl -sk -o /dev/null -w '%{http_code}\n' -H "Host: test-office.api.portal.local"  https://127.0.0.1/api/abp/application-configuration  # expect 200 (TEST office)
curl -sk -o /dev/null -w '%{http_code}\n' -H "Host: typo.api.portal.local"         https://127.0.0.1/api/abp/application-configuration  # expect 404 (no such office)
```

Any other status stops the checkpoint: see Abort.

## 6. Login and the per-office issuer

- Log in over HTTPS at `https://admin.portal.local` (host) and at
  `https://test-office.portal.local` (office).
  - Option A seeds `it.admin@hcs.test` with `InternalUsersDataSeedContributor.DefaultPassword` and
    the TEST office admin `test.admin@example.test` with
    `CaseEvaluationConsts.AdminPasswordDefaultValue`. **Neither signs in here:** outside Development
    the AuthServer refuses both published passwords (`KnownDefaultPasswordSignInManager`, listed in
    `AdminPasswordPolicy.KnownDefaults`), and this rig runs the AuthServer as Production; the
    override changes only the migrator. Use Option A for the routing checks in section 5, and
    Option B for sign-in.
  - Option B: the ABP host admin, whose generated password is in
    `secrets/admin-passwords/admin-password-host`. Then create an office via the UI.
- **The issuer is per request.** The AuthServer no longer pins `SetIssuer`
  (`CaseEvaluationAuthServerModule.cs`, the T9 comment), so each office's OIDC discovery reports
  its own host. Check it:
  `curl -sk https://test-office.auth.portal.local/.well-known/openid-configuration` should report
  an `issuer` on the `test-office.auth.portal.local` host, and the admin host should report its
  own. A fixed `https://auth...` issuer on an office means the regression is back, and office
  login will fail the SPA's issuer check.

## 7. Deep adversarial probes (added 2026-07-09)

- Silent token refresh over HTTPS (no re-login churn / redirect loop).
- Auth cookie + CSRF scope across the three per-service subdomains (no cross-office leak).
- Cross-office isolation THROUGH the proxy: an office-A session cannot read office-B data
  at office-B's subdomain (ADR-017 boundary, live).
- Forced restart mid-session: `docker compose -f docker-compose.prod.yml restart authserver api`
  -> the session + a pending email-confirmation token still validate (Redis DataProtection).
- Malformed / spoofed Host headers (unknown slug, empty leftmost label, `admin`
  look-alikes, injected `__tenant` query/cookie/header) still 404 or stay host-scoped.
- Option B: create an office via the host UI under full Production, then repeat 5-7.

## Abort

Any assertion that does not give the expected result stops the checkpoint. Do not carry the change
to a server. Tear down (below), keeping the volumes so the state can be inspected, and escalate.

## Teardown (NON-destructive -- never use `down -v`)

```bash
docker compose -f docker-compose.prod.yml down        # keeps the named volumes
```

## Escalation

If the stack is not fully healthy (all services `(healthy)`, `db-migrator` exited 0) within
30 minutes of `up`, or any assertion fails, stop and hand to the portal maintainer with the failing
command, its output, and `docker compose -f docker-compose.prod.yml logs --tail=200 <service>`
for the service involved.
