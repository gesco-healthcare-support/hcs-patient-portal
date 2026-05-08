---
feature: subdomain-tenant-routing
date: 2026-05-05
status: draft
base-branch: feat/replicate-old-app-track-domain
related-issues: []
related-adr: docs/decisions/006-subdomain-tenant-routing.md
---

# Subdomain tenant routing + DB-per-tenant + FEAT-09 fix (Phase 1A)

## Goal

Reproduce the OLD app's "one URL = one tenant + one DB" property in the NEW
single-deployment codebase, locked to one tenant (Falkinstein) for Phase 1A.

## Context

See ADR-006 for the full architectural reasoning. Summary:

- OLD spec required per-doctor website + DB; OLD enforced via separate
  deployments. NEW currently uses ABP default header/cookie/route resolution
  which lets `?__tenant=` override the URL - breaks OLD isolation and is
  HIPAA-relevant.
- This change wires `DomainTenantResolveContributor` + OpenIddict wildcard +
  per-tenant connection strings + LeptonX hide so subdomain alone determines
  the tenant.
- Patient is currently host-only (`Patient.cs` does not implement
  `IMultiTenant`); this is documented bug FEAT-09 with a skipped test pinning
  the fix. We close it as part of this PR because cross-tenant isolation
  cannot hold otherwise.

Constraints:

- Branch CLAUDE.md: HIPAA, no `ng serve`, ABP conventions
  (`[RemoteService(IsEnabled = false)]`, manual controllers, Mapperly).
- `*.localhost` resolves to 127.0.0.1 on Edge/Chrome/Firefox via RFC 6761;
  Safari excluded (not in support matrix).
- Existing dual-DbContext infrastructure (ADR-003) and one-doctor-per-tenant
  model (ADR-004) are preserved.

## Approach

### Decisions captured in ADR-006

1. Tenant resolvers cleared and rebuilt: only `CurrentUser` + `Domain`. No
   `__tenant` query/cookie/route/header.
2. OpenIddict wildcard via `AbpOpenIddictWildcardDomainOptions`.
3. LeptonX tenant box hidden via Razor component override; Angular shell
   tenant switcher disabled.
4. Per-tenant connection strings via SaaS PRO; one SQL Server, multiple
   databases for Phase 1.
5. Patient and Location move from host-side to tenant-side. Patient via
   `IMultiTenant`; Location via tenant-side DbContext config (no interface
   change needed since Location currently doesn't implement IMultiTenant
   either - confirm during T4).

### Rejected alternatives

1. Phase-1 forced-tenant via `forcedTenant` cookie - doesn't match OLD's
   enforcement.
2. Per-port worktrees per tenant - violates user's request for
   `falkinstein.localhost:4200` URL pattern.
3. Single shared DB with row-level filtering - violates OLD-parity directive.

## Tasks

### T1: Wire `DomainTenantResolveContributor` + OpenIddict wildcard

**Approach:** `code` (config edits, no business logic).

**Files touched:**

- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs`
- `src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs`
- `docker-compose.yml` (CORS origins for `*.localhost:4200`)
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.json` and
  `appsettings.Development.json` (new config keys for wildcard format if not
  hardcoded)

**Acceptance:**

- Both modules call
  `Configure<AbpTenantResolveOptions>(o => { o.TenantResolvers.Clear(); o.TenantResolvers.Add(new CurrentUserTenantResolveContributor()); o.AddDomainTenantResolver("{0}.localhost"); });`
- Both modules call
  `PreConfigure<AbpOpenIddictWildcardDomainOptions>(o => { o.EnableWildcardDomainSupport = true; o.WildcardDomainsFormat.Add("http://{0}.localhost"); /* and the :4200/:44368/:44327 variants if separate redirect URIs needed */ });`
- `App__CorsOrigins` includes `http://*.localhost:4200`,
  `http://*.localhost:44368`, `http://*.localhost:44327` and existing CORS
  wildcard support handles them
  (`SetIsOriginAllowedToAllowWildcardSubdomains` confirmed at
  `CaseEvaluationHttpApiHostModule.cs:228-237` per `security-hardening.md:21`).
- `dotnet build` succeeds; `docker compose restart authserver api` brings both
  back to healthy.

### T2: Hide LeptonX tenant box on AuthServer login

**Approach:** `code` (Razor partial override).

**Files touched:**

- `src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/Account/Components/TenantBox/Default.cshtml` (new file)

**Acceptance:**

- Visiting the AuthServer login page shows no "Tenant: Not selected / switch"
  block.
- `__tenant` query/cookie still ignored (resolver-level, T1 already covers).

### T3: Angular tenant detection from subdomain

**Approach:** `code`.

**Files touched:**

- `angular/src/environments/environment.ts` - export
  `tenantSlugFromHost()` helper.
- `angular/src/app/app.config.ts` (or wherever `provideAbpCore` /
  `provideOAuth` are configured) - rewrite issuer + API base URL to
  subdomain-aware values at bootstrap.
- `angular/src/app/app.config.ts` shell config - hide tenant switcher in
  `@volo/abp.ng.theme.lepton-x`.

**Acceptance:**

- Visiting `http://falkinstein.localhost:4200/` causes the SPA to use
  `http://falkinstein.localhost:44368` as OAuth issuer and
  `http://falkinstein.localhost:44327` as API base URL.
- Visiting `http://localhost:4200/` (no subdomain) resolves to host context
  (no tenant) - Adrian to confirm whether this should redirect to a tenant
  picker, error, or stay on host.
- Tenant switcher UI is not present in the shell.

### T4: FEAT-09 - Patient becomes IMultiTenant + Location moves to tenant

**Approach:** `tdd` for Patient (the skipped test
`GetListAsync_WhenCallerIsTenantScoped_ReturnsOnlyTheirTenantPatients` is
the failing test to enable; T4 makes it pass). `code` for Location (no
existing test pinning behavior).

**Files touched:**

- `src/HealthcareSupport.CaseEvaluation.Domain/Patients/Patient.cs` -
  implement `IMultiTenant`.
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs` -
  remove Patient + Location from `IsHostDatabase()` guard.
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationTenantDbContext.cs` -
  add Patient + Location config.
- New EF migrations:
  `Migrations/<ts>_FEAT09_Patient_Location_MoveToTenant.cs` (drop in host)
  and `TenantMigrations/<ts>_FEAT09_Patient_Location_MoveToTenant.cs`
  (create in tenant).
- `src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs` -
  re-check the host-context behavior section; the
  `GetListAsync_FromHostContext_ReturnsPatientsFromBothTenants` test may need
  to flip to host-context-disabled-filter pattern.
- `test/HealthcareSupport.CaseEvaluation.Application.Tests/Patients/PatientsAppServiceTests.cs` -
  remove `[Skip]` from the cross-tenant test.

**Acceptance:**

- All Patient + Location tests pass.
- `dotnet ef migrations add ...` produces idempotent migrations on both
  contexts.
- `Patients/CLAUDE.md` Known Gotchas section updated to mark FEAT-09 as
  resolved (the entry can stay, status flipped).

### T5: Seed Falkinstein tenant with dedicated DB

**Approach:** `code`.

**Files touched:**

- `src/HealthcareSupport.CaseEvaluation.Domain/Saas/FalkinsteinTenantDataSeedContributor.cs`
  (new) - creates `Falkinstein` tenant with
  `Database=CaseEvaluation_Falkinstein` connection string, idempotent. Only
  runs in Development per `IsDevelopment()` check (matches existing
  `InternalUsersDataSeedContributor` pattern).
- `docker-compose.yml` or `docker/appsettings.secrets.json` - no change
  expected; the connection string for the tenant is built from the existing
  `MSSQL_SA_PASSWORD` env var via the seed contributor.

**Acceptance:**

- `docker compose down -v && docker compose up -d` produces:
  - Host DB `CaseEvaluation` with one row in `SaasTenants` (Falkinstein) and
    one row in `AbpUsers` (`it.admin@hcs.test`).
  - Tenant DB `CaseEvaluation_Falkinstein` with three users
    (`admin@falkinstein.test`, `supervisor@falkinstein.test`,
    `staff@falkinstein.test`) per `InternalUsersDataSeedContributor`.
- Tenant DB has all `App*` tables; host DB does not.

### T6: Hosts file + smoke test

**Approach:** `code` + manual verification.

**Files touched:**

- `C:\Windows\System32\drivers\etc\hosts` - add `127.0.0.1 falkinstein.localhost`
  (Adrian runs as admin; Claude cannot edit privileged paths).
- No source code changes; verification only.

**Acceptance:**

- `curl -s -o /dev/null -w "%{http_code}\n" http://falkinstein.localhost:4200/` -> 200
- `curl -sk -o /dev/null -w "%{http_code}\n" http://falkinstein.localhost:44368/.well-known/openid-configuration` -> 200, body's `issuer` field matches `http://falkinstein.localhost:44368`
- `curl -sk -o /dev/null -w "%{http_code}\n" http://falkinstein.localhost:44327/health-status` -> 200
- Login flow: visit `http://falkinstein.localhost:4200/`, click Login,
  AuthServer login page shows no tenant box, sign in as
  `admin@falkinstein.test` / `1q2w3E*r`, redirected back to SPA, can see
  appointments page (empty, no slot data yet).

## Risk / Rollback

**Blast radius:**

- AuthServer + HttpApi.Host modules touched. Misconfigured wildcard or
  cleared resolvers cause login failures or all-tenant-unresolvable state.
- EF migrations move `Patient` + `Location`. If applied to a populated host
  DB, data is lost (tables are recreated on tenant side empty). Phase 1A
  starts from `docker compose down -v` so this is moot in dev; production
  rollout (when it happens) requires a data-migration script.
- LeptonX override is purely additive; reverting is a single file delete.

**Rollback:**

- All changes are squash-merged onto `feat/replicate-old-app-track-domain`.
  Single revert via `git revert` undoes everything.
- For DB rollback: `docker compose down -v` resets state.

## Verification (end-to-end test procedure)

1. `docker compose down -v && docker compose build && docker compose up -d`.
2. Wait for all health checks: SQL healthy -> AuthServer healthy -> API
   healthy -> Angular up.
3. Verify host DB has Falkinstein tenant:
   `docker exec replicate-old-app-sql-server-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'myPassw@rd' -C -d CaseEvaluation -Q "SELECT Name FROM SaasTenants"`
   returns `Falkinstein`.
4. Verify tenant DB exists and has its own users:
   `... -d CaseEvaluation_Falkinstein -Q "SELECT Email FROM AbpUsers"`
   returns the three tenant users.
5. Visit `http://falkinstein.localhost:4200/` in Edge/Chrome.
6. Click Login -> AuthServer page loads at
   `http://falkinstein.localhost:44368/Account/Login...`. No "Tenant: Not
   selected / switch" UI visible.
7. Sign in as `admin@falkinstein.test` / `1q2w3E*r`. Redirect back to SPA
   landing page.
8. SPA shows authenticated header. Navigate to Appointments page (empty
   list).
9. Run skipped Patient test:
   `dotnet test --filter "FullyQualifiedName~PatientsAppServiceTests.GetListAsync_WhenCallerIsTenantScoped_ReturnsOnlyTheirTenantPatients"`
   - passes.
10. Attempt cross-tenant URL injection: visit
    `http://falkinstein.localhost:4200/?__tenant=00000000-0000-0000-0000-000000000000`.
    Tenant remains Falkinstein (URL wins, query param ignored).

If any step fails, halt and report per the
`feedback_test-failures-stop-and-report.md` rule.

## Verification gate decisions (locked 2026-05-05)

1. **No-subdomain landing behavior:** Bare `http://localhost:4200/` redirects
   to `http://admin.localhost:4200/` at SPA bootstrap. `admin` is a reserved
   slug that resolves to host context (no tenant) - ABP's domain resolver
   returns null when the slug does not match a registered tenant, which is
   the host context naturally. No special resolver logic needed; T5 adds a
   reserved-name guard so a tenant cannot be created with the name "admin".
2. **Falkinstein tenant DB connection string:** Source. Built in
   `FalkinsteinTenantDataSeedContributor.cs` from `MSSQL_SA_PASSWORD` env
   var. Production migration path: delete the seed contributor + create the
   row via SaaS UI or pre-populate `docker/appsettings.secrets.json`.
3. **AuthServer cookie scope:** Watch in T6. Default ABP behavior scopes
   cookies to the exact origin; if smoke test shows cross-subdomain bleed,
   add explicit `Configure<CookieAuthenticationOptions>(o => o.Cookie.Domain = null)`.

## T7: Seed demo external users per tenant (added 2026-05-05)

**Approach:** `code`.

Seeds one user per external role per tenant, idempotent, Development-only,
matches the OLD role taxonomy (4 external roles per
`project_role-model.md`).

**Files touched:**

- `src/HealthcareSupport.CaseEvaluation.Domain/Identity/DemoExternalUsersDataSeedContributor.cs`
  (new) - mirrors the existing `InternalUsersDataSeedContributor.cs`
  pattern.

**Users seeded per tenant** (where `<slug>` is the tenant slug, e.g.
`falkinstein`):

| Email                              | Role              |
|------------------------------------|-------------------|
| `patient@<slug>.test`              | Patient           |
| `adjuster@<slug>.test`             | Adjuster          |
| `applicant.attorney@<slug>.test`   | Applicant Attorney |
| `defense.attorney@<slug>.test`     | Defense Attorney  |

All with password `1q2w3E*r` (matches existing internal-user seed).

**Acceptance:**

- After `docker compose down -v && docker compose up -d`,
  `CaseEvaluation_Falkinstein` DB has the four external users above plus the
  three internal users (admin/supervisor/staff) from the existing seed.
- Each user has the correct role assigned per `AbpUserRoles`.
- The seed is idempotent: running DbMigrator twice does not duplicate.
- The seed is skipped in non-Development environments.
