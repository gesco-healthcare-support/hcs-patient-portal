---
feature: in-house-hosting
date: 2026-07-09
status: in-progress
base-branch: main
deploy-target: development
related-issues: []
---

## Goal

Add production hosting support so the database-per-office build (PR #339) can run on
Gesco's LAN-only in-house server behind nginx + internal-CA TLS, deployed from the
development branch, with the never-run Production code path verified locally first.

## Context

The db-per-office epic merged to main (PR #339); development is 127 commits behind and
carries none of it. Nothing in the repo is hostable as-is: the only compose file builds
dev targets (bind-mounted source, no restart policy), there is no reverse proxy, no TLS,
and the tenant resolver + SPA + email-URL builder are hard-coded to `localhost`. CI
validates and opens promotion PRs but deploys to no server.

Decision gates resolved with Adrian this session:

- Reach: LAN-only UAT. Public hosting is a later, separate Azure effort. TLS still mandatory.
- Deploy branch: development. Build hosting support on a feat/ branch off main; PR
  main -> development catches development up AND carries the hosting support; deploy development.
- URL layout: subdomain-per-service. Leftmost label = office; `admin` = reserved host slug.
  `{office}.portal.<domain>` (Angular), `{office}.api.portal.<domain>` (API),
  `{office}.auth.portal.<domain>` (AuthServer).
- SQL: in-container, persistent volume, scheduled backups.
- Reverse proxy: nginx (matches the vidcon deploy Adrian runs), scripted cert renewal.
- TLS trust: ask IT for an existing internal CA / GPO root distribution.
- Backups: destination deferred to IT policy; job built with a configurable target.
- Deploy mechanism: scripted SSH deploy that builds on the server; no self-hosted runner, no GHCR.

### Verified this session (code + official docs) -- no assumptions carried forward

Every claim below was confirmed by reading the code or an official source; citations inline.

- G1 config seam: `ConfigureMultiTenancy()` is parameterless in BOTH modules
  (AuthServerModule.cs:494, HttpApiHostModule.cs:380) but `configuration` is already in scope
  at each call site (AuthServerModule.cs:91/189/478, HttpApiHostModule.cs:78/102). So T1 threads
  the format string into the method; no new plumbing to fetch configuration.
- G7 ForwardedHeaders (Microsoft Learn, aspnetcore-10.0): since .NET 8.0.17 / 9.0.6 the
  Forwarded Headers Middleware IGNORES `X-Forwarded-*` from any proxy not in
  KnownProxies/KnownNetworks (loopback trusted by default). nginx-in-a-container is
  non-loopback. The existing dev-branch config clears both lists (AuthServerModule.cs:215-216);
  that is the documented "trust any proxy" escape and works, but scoping KnownNetworks to the
  docker bridge subnet is the safer form. Config-only alternative:
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` (clears lists + enables XForwardedFor|XForwardedProto).
  The API host (HttpApiHostModule) has ZERO forwarded-header handling today -- add it.
  Source: https://learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0
- G6 cert (ABP Configuring-OpenIddict): `AddProductionEncryptionAndSigningCertificate("openiddict.pfx",
  pass)` does `File.Exists(fileName)` relative to the content root and throws
  FileNotFoundException if missing; on success loads `new X509Certificate2(fileName, pass)` as both
  signing + encryption cert. So mounting the pfx at `/app/openiddict.pfx` (container WORKDIR /app)
  satisfies it. It is DISTINCT from the nginx TLS cert. Linux containers can need the
  `X509KeyStorageFlags.EphemeralKeySet` overload for private-key access (current code uses the
  2-arg overload at AuthServerModule.cs:180) -- may require a code tweak, confirmed at the checkpoint.
  The csproj embeds the pfx only `Condition="Exists('./openiddict.pfx')"`, so keeping the file
  absent at build time (it is mounted at runtime) avoids baking the secret into the image.
  Source: https://abp.io/docs/latest/Deployment/Configuring-OpenIddict
- G8 issuer (ABP support #7332 -- nearly identical setup): a static `SetIssuer(Authority)`
  (AuthServerModule.cs:181, runs in every non-Development env) forces the OIDC issuer to one host,
  so per-subdomain tenants fail the SPA's discovery check ("expected https://tenant...  current
  https://..."). Removing SetIssuer lets OpenIddict compute the issuer per-request from host+scheme
  and fixes tenants -- and in the ABP case it broke only the SUBDOMAIN-LESS base domain. OUR host
  surface has its OWN subdomain (`admin.`), so a per-request issuer should serve host + every office
  uniformly. This makes T9 a concrete, evidence-based hypothesis (not a blind TBD), still confirmed
  at CHECKPOINT 1. Depends on G7 (OpenIddict must see the forwarded https scheme + original Host).
  Source: https://abp.io/support/questions/7332/Infinite-Loop-Issue-with-Superadmin-Login-in-Multitenant-OpenIddict-Configuration
- SEEDING GATE (the big one): every test-data seeder is gated `if (!IsDevelopment()) return;` --
  InternalUsersDataSeedContributor.cs:94 (it.admin@hcs.test + office admin/staff),
  OfficeSeedDataContributor.cs:54 (creates the 4 offices as SaaS tenants + their databases),
  plus External/DemoPatient/DemoExternal/OfficeAvailability. Under Production the DbMigrator seeds
  NO offices and NO test users. Only the ABP-standard host admin
  (CaseEvaluationConsts.AdminEmailDefaultValue / AdminPasswordDefaultValue = "1q2w3E*r", seeded in
  all envs via CaseEvaluationDbMigrationService.cs:113-119) exists. Consequences: (a) local
  verification needs a data strategy to have an office + login under the prod path (see T0);
  (b) on the real server the 4 offices are CREATED via the host UI, not seeded (Phase 3).
- Confirmed working unchanged: OpenIddict wildcard redirect URIs are config-driven
  (`App:WildcardDomainsFormat`, AuthServerModule.cs:117-140); the API IssuerValidator accepts any
  single-label subdomain of the authority host on the same scheme+port (HttpApiHostModule.cs:899-922);
  per-office DBs derive from the `Default` connection string (TenantConnectionStringProvider);
  Hangfire uses SQL storage (survives restarts); DataProtection keys persist to Redis
  (AuthServerModule.cs:373 / HttpApiHostModule.cs:986); the SPA merges dynamic-env.json with a
  SHALLOW `Object.assign` (main.ts:23) so the prod file must be complete per top-level key; the
  nginx-image `.envsh` entrypoint pattern (docker-dynamic-env.envsh) is the ready mechanism for a
  prod dynamic-env.json. A single wildcard `*.portal.X` does NOT cover `*.api.portal.X` (TLS
  wildcards match one label), so subdomain-per-service needs three DNS records + a 3-SAN cert.

## Approach

Chosen: three phases, hard STOP checkpoints between them, verification-first.

- Phase 1 stands up a minimum bootable prod stack and drives a real login locally (fake domain
  in the Windows hosts file + mkcert wildcard cert, through nginx on 443). Its purpose is to make
  the never-run Production code path (G6 cert, G7 forwarded headers, G8 issuer) execute for the
  first time in a safe place and record exactly what breaks.
- Phase 2 fixes whatever CHECKPOINT 1 surfaced, then finishes the operational gaps.
- Phase 3 is the actual server deploy, gated on Adrian's SSH creds + IT DNS/CA + explicit approval.

Rejected alternatives: deploy main directly (bypasses the cascade; throwaway); predict-and-fix G8
without running it (never-run branch -- observe then fix, now evidence-guided); Helm/K8s (overkill
for one box); Traefik/Caddy auto-TLS (internal CA + GPO makes ACME unnecessary; nginx matches vidcon).

Config-key naming: one new key, `App:TenantDomainFormat`, read by both hosts; default `{0}.localhost`
so local dev is unchanged; prod sets it per service. The frontend base host (G2) and email base host
(G3) derive from the same domain so the shared substitution rule (TenantUrlComposer's doc comment)
is preserved.

Local verification data strategy (T0) -- RESOLVED (Adrian, 2026-07-09): do BOTH Option A and
Option B. Option A (DbMigrator=Development seeds the volume; app services=Production) is the fast
path used to observe G6/G7/G8 at CHECKPOINT 1; Option B (full Production + create an office via the
host UI) is run once to confirm the real office-provisioning path works under Production. Verification
depth -- RESOLVED (Adrian, 2026-07-09): DEEP -- run the adversarial probe set at CHECKPOINT 1, not
just the happy-path checklist.

## Tasks

- T0 (RESOLVED: both A and B): Implement the local verification data strategy.
  - approach: code
  - context: test seeders (offices + users) run only in Development; the prod cert/issuer/https code
    runs only in non-Development. One env setting cannot give both, so we use two complementary runs:
    - Option A (primary, fast path for CHECKPOINT 1): DbMigrator container runs
      `DOTNET_ENVIRONMENT=Development` (seeds the 4 offices + it.admin@hcs.test into the persistent SQL
      volume); AuthServer/API/Angular run `ASPNETCORE_ENVIRONMENT=Production` (exercise the prod path
      against already-seeded data). Seeders live only in DbMigrator, so the app services never re-seed.
      No code change; LOCAL-ONLY (the real server never runs the migrator in Development).
    - Option B (confirming pass): a full-Production run (DbMigrator also Production) where we log in as
      the ABP-standard host admin, create a test office via the host UI (exercises the real
      provisioning path under Production), then log in at its subdomain. Confirms the server's actual
      go-live path before Phase 3.
    - Option C (rejected): a `SeedTestData` flag ungating the seeders in Production -- a verification-only
      config seam that must be fenced off from the server. Rejected; A + B cover the need.
  - files-touched: [docker-compose.prod.yml (env split + a second override for the full-Production run),
    docs/runbooks/hosting-local-verification.md]
  - acceptance: (A) after the Development DbMigrator run, >=1 office tenant + it.admin@hcs.test exist and
    the app services run under ASPNETCORE_ENVIRONMENT=Production; (B) a fresh full-Production stack lets
    the host admin create an office via the UI and log into it.

### Phase 1 -- minimum bootable prod stack (forced gaps)

- T1 (G1): Config-drive the tenant-resolver suffix in both hosts.
  - approach: test-after
  - files-touched: [src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs,
    src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs,
    src/HealthcareSupport.CaseEvaluation.AuthServer/appsettings.json,
    src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.json]
  - detail: read `App:TenantDomainFormat` from IConfiguration; pass it into `ConfigureMultiTenancy`
    (currently parameterless at AuthServerModule.cs:494 / HttpApiHostModule.cs:380; configuration is
    in scope at the call sites). Default `{0}.localhost` when unset. Do NOT touch the ExtractSlug
    parser (already suffix-agnostic).
  - acceptance: unit test proves ExtractSlug yields `falkinstein` from
    `falkinstein.auth.portal.example.test` under format `{0}.auth.portal.example.test`, and still
    from `falkinstein.localhost` under the default; ADR-007 behavior (admin -> host, typo -> 404)
    unchanged locally.

- T2 (G7): Honor nginx X-Forwarded-Proto on the production path (both hosts).
  - approach: test-after
  - files-touched: [src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs,
    src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs]
  - detail: the AuthServer configures ForwardedHeaders only inside the dev-only
    `if (!RequireHttpsMetadata)` branch (AuthServerModule.cs:205-218) while UseForwardedHeaders runs
    unconditionally (:518). Configure ForwardedHeaders.XForwardedProto for the prod path too, trusting
    the nginx proxy -- prefer KnownNetworks scoped to the docker bridge subnet; clearing both lists
    (as the dev branch does) is the documented fallback. The HttpApi.Host has NO forwarded-header
    handling at all -- add UseForwardedHeaders + options. Consider the config-only
    `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` as an alternative lever.
  - acceptance: with RequireHttpsMetadata=true, a request carrying `X-Forwarded-Proto: https` is
    treated as HTTPS (Request.Scheme == https) so OpenIddict does not reject it; asserted by a test
    and/or a curl-through-proxy check at the checkpoint.

- T3 (G6): Generate and mount the OpenIddict signing certificate for prod.
  - approach: code
  - files-touched: [scripts/hosting/gen-openiddict-cert.sh, docker-compose.prod.yml,
    .env.prod.example, docs/runbooks/hosting-secrets.md]
  - detail: script generates a long-lived self-signed `openiddict.pfx` with a passphrase; the prod
    compose mounts it read-only at `/app/openiddict.pfx` (content root) and sets
    `AuthServer__CertificatePassPhrase` from the env file. NEVER bake into the image (keep the file
    absent at build so the csproj Exists-condition does not embed it); NEVER commit (.gitignore). If
    the Linux container throws a private-key access error, switch AuthServerModule.cs:180 to the
    3-arg overload with `X509KeyStorageFlags.EphemeralKeySet` (decided from the checkpoint evidence).
  - acceptance: the prod AuthServer container starts without the
    `AddProductionEncryptionAndSigningCertificate` file-not-found crash.

- T4 (G2): Config-drive the Angular base host + prod dynamic-env generation.
  - approach: test-after
  - files-touched: [angular/src/tenant-bootstrap.ts, angular/Dockerfile, angular/prod-dynamic-env.envsh,
    angular/nginx.conf]
  - detail: replace the hard-coded `HOST_BASE = 'localhost'` (tenant-bootstrap.ts:42) and the
    literal-`localhost` URL swap with a base host sourced from runtime config (dynamic-env.json). Add a
    prod entrypoint (modeled on docker-dynamic-env.envsh) that regenerates dynamic-env.json from env
    vars into the prod nginx image (remember main.ts:23 merges SHALLOW -- the file must be complete per
    top-level key). Fix the bare-host redirect to target `admin.<base>` (not `admin.localhost`) and
    ensure the bare `portal.<domain>` host is not mistaken for an office slug.
  - acceptance: served under the prod image, the SPA at `falkinstein.portal.<domain>` rewrites its
    API/auth URLs to `falkinstein.api.portal.<domain>` / `falkinstein.auth.portal.<domain>`; bare host
    redirects to `admin.portal.<domain>`; a jsdom/unit test covers the rewrite for a non-localhost base.

- T5 (G4): Author docker-compose.prod.yml.
  - approach: code
  - files-touched: [docker-compose.prod.yml, .env.prod.example, .gitignore, .dockerignore]
  - detail: prod image targets (Dockerfile defaults; no `target: dev`), no source bind-mounts, named
    volumes (sqldata incl. all per-office DBs, miniodata, redis-data), `restart: unless-stopped`,
    healthchecks on every long-lived service, log rotation (json-file max-size/max-file), db-migrator
    one-shot `restart: no` with `depends_on: service_healthy` (env per T0), real prod env
    (AuthServer__Authority, App__SelfUrl, App__CorsOrigins with the three `https://*.<zone>` wildcards,
    App__AngularUrl, App__WildcardDomainsFormat__* for the prod hosts, AuthServer__RequireHttpsMetadata=true,
    App__HealthUiCheckUrl, App:TenantDomainFormat per service, ASPNETCORE_ENVIRONMENT=Production for the
    app services). Secrets ONLY via the git-ignored .env file; ship .env.prod.example with placeholders.
  - acceptance: `docker compose -f docker-compose.prod.yml --env-file .env.prod config` valid;
    `build` builds all prod targets; no secret literals in the committed compose or example.

- T6 (G5): Persist Redis (DataProtection keys) in the prod compose.
  - approach: code
  - files-touched: [docker-compose.prod.yml]
  - detail: redis gets a named volume and `--appendonly yes` (AOF) so the DataProtection keyring
    (AuthServerModule.cs:373, HttpApiHostModule.cs:986) survives restarts.
  - acceptance: redis persists a written key across `docker compose restart redis` (verified via the
    login-survives-restart test, T11).

- T7: nginx reverse-proxy config for the subdomain-per-service layout.
  - approach: code
  - files-touched: [docker/nginx/nginx.conf, docker/nginx/conf.d/*.conf, docker-compose.prod.yml]
  - detail: nginx terminates TLS on 443 with the wildcard cert(s); server blocks route
    `*.portal.<domain>` -> angular, `*.api.portal.<domain>` -> api, `*.auth.portal.<domain>` ->
    authserver; forward the ORIGINAL Host (`proxy_set_header Host $host`) + `X-Forwarded-Proto $scheme`
    (paired with T2, and required for G8's per-request issuer). Local cert = mkcert wildcard (T8);
    server cert = IT internal-CA 3-SAN wildcard.
  - acceptance: through nginx on 443, each subdomain reaches the correct upstream and the upstream sees
    the original Host (checkpoint reproduces ADR-007's three assertions).

- T8: Local verification harness (fake domain + mkcert wildcard cert).
  - approach: code
  - files-touched: [scripts/hosting/gen-local-certs.sh, docs/runbooks/hosting-local-verification.md]
  - detail: document the explicit Windows hosts-file entries (hosts files cannot express wildcards):
    admin.portal.local, admin.api.portal.local, admin.auth.portal.local, falkinstein.portal.local,
    falkinstein.api.portal.local, falkinstein.auth.portal.local, typo.api.portal.local -> 127.0.0.1;
    plus an mkcert wildcard cert covering `*.portal.local`, `*.api.portal.local`, `*.auth.portal.local`.
    Base domain for local verification = `portal.local`.
  - acceptance: cert generated + locally trusted; hosts entries documented; runbook lists the exact
    curl + browser steps for the checkpoint.

- CHECKPOINT 1 (hard STOP -- report to Adrian before Phase 2):
  Bring up docker-compose.prod.yml locally against portal.local (data per T0) through nginx on 443.
  1. `curl -k -H "Host: admin.api.portal.local" https://.../api/abp/application-configuration` => 200
     (host context).
  2. `curl -k -H "Host: falkinstein.api.portal.local" .../api/abp/application-configuration` => 200
     (Falkinstein tenant).
  3. `curl -k -H "Host: typo.api.portal.local" .../api/abp/application-configuration` => 404
     "Tenant not found!".
  4. Log in end-to-end over HTTPS at `admin.portal.local` (host admin) AND at
     `falkinstein.portal.local` (office login) -- creds per T0.
  5. OBSERVE G8: with the current `SetIssuer(Authority)` pin, does the office login fail the SPA's
     OIDC discovery issuer check? Does removing/guarding SetIssuer let OpenIddict compute the correct
     per-host issuer for BOTH admin and office? Record the exact discovery `issuer` values seen.
  6. Record every other never-run-path surprise (cert key access / EphemeralKeySet, forwarded headers,
     CORS, cookie scope, HTTPS redirects).
  7. DEEP adversarial probes (Adrian, 2026-07-09): silent token-refresh loop over HTTPS (no
     re-login churn); auth-cookie scope + CSRF token behavior across the three per-service subdomains
     (cookies must not leak across offices); cross-office isolation spot-check THROUGH the proxy (an
     office-A session cannot read office-B data at office-B's subdomain); forced restart mid-session
     (authserver + api) to confirm Redis-backed session/key survival; malformed / spoofed Host-header
     fuzzing (unknown slug, empty label, `admin` look-alikes, injected `__tenant`) still 404s or stays
     host-scoped. Also run Option B (create an office via the host UI under full Production) and repeat
     assertions 3-5 against it.
  Write findings into the Verification log below and STOP.

### Phase 2 -- resolve findings + operational finish

- T9 (G8): Make the OIDC issuer per-request instead of a fixed pin.
  - approach: test-after
  - files-touched: [src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs
    (and/or prod env)]
  - detail: evidence-based hypothesis from ABP #7332 + CHECKPOINT 1 -- remove or guard the
    `SetIssuer(new Uri(Authority))` pin (AuthServerModule.cs:181) so OpenIddict derives the issuer from
    the forwarded host+scheme (T2/T7 make that host+scheme correct). Confirm it serves BOTH the `admin`
    host surface and office subdomains; exact final form decided from the checkpoint evidence.
  - acceptance: admin + office login succeed over HTTPS on prod hostnames, the SPA's discovery issuer
    matches the token `iss`, and a silent token refresh succeeds; no issuer errors in logs.

- T10 (G3): Config-drive the email-link base host in sync with the frontend.
  - approach: tdd
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Application/Notifications/TenantUrlComposer.cs,
    test/.../ (new or existing notifications test)]
  - detail: TenantUrlComposer.cs:43 rewrites bare-`localhost` to `{tenant}.localhost`; make the base
    host configurable so prod emails render `{office}.portal.<domain>` links. Keep the substitution rule
    identical to tenant-bootstrap.ts (frontend/backend parity per the doc comment). Pure string logic +
    notification boundary => TDD.
  - acceptance: unit tests prove `https://falkinstein.portal.<domain>/...` for tenant Falkinstein,
    already-prefixed URLs and host-scope (null tenant) unchanged; a rendered reminder-email link is
    confirmed at the Phase 2 checkpoint.

- T11 (G5 verification): Login survives an AuthServer restart.
  - approach: test-after
  - files-touched: [docs/runbooks/hosting-local-verification.md]
  - detail: log in, `docker compose -f docker-compose.prod.yml restart authserver`, confirm the session
    and a pending email-confirmation token still validate (proves Redis-persisted DataProtection keys, T6).
  - acceptance: post-restart the session is still authenticated and a pre-restart confirmation link
    still resolves.

- T12: Fail-fast validation of required secrets/env at startup.
  - approach: test-after
  - files-touched: [src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs,
    src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs]
  - detail: in Production, assert required config is present and non-placeholder (ConnectionStrings:Default,
    AuthServer:CertificatePassPhrase, StringEncryption passphrase, Redis, App:TenantDomainFormat, SMTP if
    mailing enabled) and throw a clear exception naming what is missing. OWASP: never log the values.
    Fail visibly rather than run degraded.
  - acceptance: starting the prod stack with a required key blank aborts with a message naming the missing
    key(s) and no secret values in the log.

- T13: Database backup job with a configurable destination.
  - approach: code
  - files-touched: [scripts/hosting/backup-databases.sh, docker-compose.prod.yml,
    docs/runbooks/hosting-backup-restore.md]
  - detail: nightly native SQL `.bak` of the host DB plus every `CaseEvaluation_*` per-office DB
    (enumerate sys.databases dynamically so new offices are covered), to a configurable destination (env
    var; exact target deferred to IT policy). Document retention (e.g. 14 daily + 8 weekly) and restore.
  - acceptance: run locally, the job produces a restorable `.bak` for the host DB and each per-office DB;
    restore spot-checked on one DB.

- T14: Scripted SSH deploy (build on server) -- authored, not run.
  - approach: code
  - files-touched: [scripts/hosting/deploy.sh, docs/runbooks/hosting-deploy.md]
  - detail: versioned script run from Adrian's machine: SSH to the box, `git pull` development,
    `docker compose -f docker-compose.prod.yml build`, `up -d --force-recreate db-migrator`, then `up -d`.
    Needs ABP_NUGET_API_KEY on the server for the build. Document prerequisites (Docker, cert, .env.prod,
    DNS). Do NOT execute against any server in this epic.
  - acceptance: script + runbook reviewed; a `set -x`/dry-run walkthrough documented; no server touched.

- CHECKPOINT 2 (hard STOP): full local prod-compose verification passes; report to Adrian; propose the
  main -> development PR.

### Phase 3 -- server deploy (separate gate, DO NOT execute in this epic)

- T15: Deploy to the in-house server + stand up real offices + run the ADR-017 isolation gate.
  - approach: code
  - files-touched: [runbook edits only]
  - detail: ONLY after Adrian hands over SSH creds, IT delivers wildcard DNS (three records:
    `*.portal.<domain>`, `*.api.portal.<domain>`, `*.auth.portal.<domain>`) + an internal-CA 3-SAN
    wildcard cert distributed by GPO, and Adrian explicitly approves. On the box: change the ABP-standard
    host admin's default password immediately; CREATE the 4 real offices via the host UI (they are NOT
    seeded in Production -- OfficeSeedDataContributor is Dev-only); then run
    docs/runbooks/database-per-office-go-live-isolation-gate.md (provision two real office DBs, attempt
    cross-office access through every pathway, confirm physical separation) before any stakeholder use.
  - acceptance: not in scope for this epic; blocked pending the gate.

## Risk / Rollback

- Blast radius (Phase 1-2): all changes on a new feat/ branch in a new worktree off main; nothing merges
  until Adrian approves. The C# edits (T1, T2, T3, T9, T12) touch startup modules shared by dev --
  mitigated by config defaults keeping local dev on `{0}.localhost` + the ~1300-test suite. No changes to
  live databases; no `docker compose down -v`; the running local dev stack (main project, canonical ports)
  is untouched -- prod verification uses docker-compose.prod.yml with its own volumes.
- Biggest technical risk: G8 (never-run Production path), now bounded -- CHECKPOINT 1 runs it locally
  before any server touch; the fix (T9) is evidence-guided (ABP #7332), not a guess.
- Seeder-gate risk: Production seeds no offices/test-users; mishandling this makes verification look
  "broken" when it is by-design. Handled by T0 (local) + Phase 3 office-creation-via-UI (server).
- Secondary risks: shallow dynamic-env merge (T4); hosts files cannot express wildcards (T8); one wildcard
  does not cover a second label, so three DNS records + a 3-SAN cert (T7/T8, Phase 3 DNS ask); Linux
  container cert key access may need EphemeralKeySet (T3).
- Rollback: Phase 1-2 are additive (new compose, scripts, config keys with dev defaults); reverting the
  branch restores prior state; no production system exists yet to roll back. Phase 3 rollback = redeploy
  the prior git revision via the deploy script (T14).

## Verification

Run at CHECKPOINT 1 (Phase 1 subset) and in full at CHECKPOINT 2, against `portal.local` through nginx on
443 with the mkcert wildcard cert and data per T0:

1. Backend suite green: `dotnet test HealthcareSupport.CaseEvaluation.slnx` (T1 slug, T2 forwarded-proto,
   T10 composer, T12 fail-fast).
2. `docker compose -f docker-compose.prod.yml --env-file .env.prod build` succeeds; stack healthy
   (`docker compose ps` all healthy; db-migrator exited 0).
3. ADR-007 assertions on PROD hostnames: admin.api.portal.local => 200; falkinstein.api.portal.local =>
   200 (Falkinstein); typo.api.portal.local => 404 "Tenant not found!".
4. Login end-to-end over HTTPS at admin.portal.local and an office subdomain; OIDC redirect completes;
   discovery issuer matches token `iss` (G8 resolved).
5. Login survives `docker compose -f docker-compose.prod.yml restart authserver` (T6/T11).
6. A rendered appointment-reminder email link points at `{office}.portal.local` (T10).
7. Backup job produces a restorable `.bak` for the host DB and each `CaseEvaluation_*` DB (T13).
8. Startup fails fast with a clear message when a required prod env key is blank (T12).

Deep adversarial probes (CHECKPOINT 1 + 2, per Adrian 2026-07-09):
9. Silent token refresh succeeds repeatedly over HTTPS with no re-login churn or redirect loop.
10. Auth cookies + CSRF tokens are correctly scoped per subdomain; no cookie leaks across offices or
    between the portal/api/auth hosts.
11. Cross-office isolation holds THROUGH the proxy: an office-A session cannot read office-B data at
    office-B's subdomain (ADR-017 boundary, exercised live rather than in the SQLite harness).
12. Forced restart mid-session (authserver + api) leaves the session valid (Redis DataProtection + SQL
    Hangfire survive).
13. Malformed/spoofed Host headers (unknown slug, empty leftmost label, `admin` look-alikes, injected
    `__tenant` query/cookie/header) still 404 or stay host-scoped -- never cross into another tenant.
14. Option B: under full Production, the host admin creates an office via the UI and logs into it;
    assertions 3-5 and 9-13 repeat green against that office.

Findings log (filled at CHECKPOINT 1): _to be completed when the stack first boots._
