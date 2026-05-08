# ADR-006: Subdomain Tenant Routing + Database-per-Tenant

**Status:** Proposed
**Date:** 2026-05-05
**Verified by:** code-inspect (current state) + ABP framework docs (target mechanism)

## Context

The OLD app's master spec
(`P:\PatientPortalOld\Documents_and_Diagrams\Architecture\SoCal Project Overview Document.docx`,
quoted verbatim in `docs/parity/_old-docs-index.md` lines 88-100) requires:

> Line 113: "Each doctor will have a separate website and database for
> appointment booking, and the administrative areas of each of these websites
> will be managed by SoCal staff."
>
> Line 599: "[Schedule Report] needs to show the merged data of all the three
> doctor's website. This will be done via replication of selective,
> non-sensitive appointment booking records in a separate read-only database."

OLD enforced the "separate website" property by deploying a discrete
single-tenant instance per doctor (`falkinstein-api.qmeame.com`,
`socal-falkinstein-api.live1.dev.radixweb.net`). There is no tenant-resolver
code in OLD because every deployment was hardcoded to one tenant.

The NEW codebase (per `docs/architecture/MULTI-TENANCY.md` lines 22-28) was
configured to use ABP's default tenant resolution chain
(`CurrentUser` -> `QueryString` -> `Route` -> `Header` -> `Cookie`). That chain
lets any client send `?__tenant=<guid>` and switch tenants from the URL bar,
which (a) breaks the OLD isolation property and (b) is HIPAA-relevant: a
patient could request another doctor's tenant data.

ADR-003 already locked dual-DbContext infrastructure (host vs tenant) and
ADR-004 locked one-doctor-per-tenant. Neither addressed how tenants get
resolved at the URL layer or whether tenant data lives in physically separate
databases. This ADR fills that gap.

## Decision

The NEW app reproduces OLD's isolation in a single deployment via two
mechanisms:

### 1. Subdomain-only tenant resolution

`AbpTenantResolveOptions.TenantResolvers` is cleared and rebuilt with exactly
two contributors, in priority order:

1. `CurrentUserTenantResolveContributor` - tenancy follows the logged-in
   user's claim (security default, must be first).
2. `DomainTenantResolveContributor` - tenancy follows the URL's subdomain
   (`{slug}.localhost` in dev, `{slug}.<real-domain>` in prod).

The default `QueryString`, `Route`, `Header`, and `Cookie` contributors are
**dropped**. `?__tenant=<id>` and `__tenant` cookies cannot override the URL.

OpenIddict accepts wildcard subdomains via
`PreConfigure<AbpOpenIddictWildcardDomainOptions>` with
`EnableWildcardDomainSupport = true` and the per-host wildcard formats
registered.

The LeptonX login layout's tenant box is hidden via a Razor component
override returning empty content. The Angular shell's tenant switcher is
disabled in `@volo/abp.ng.theme.lepton-x` shell options.

### 2. Database-per-tenant via SaaS PRO

ABP Commercial 10.0.2's SaaS module supports per-tenant connection strings.
Each tenant is created with the "Use the shared database" option **unchecked**
and a dedicated connection string assigned (e.g.
`Database=CaseEvaluation_Falkinstein`). DbMigrator auto-creates the per-tenant
schema and runs tenant-side seed contributors on save.

The host database (`CaseEvaluation`) keeps only:

- `SaasTenants`, `SaasEditions` - tenant registry
- `OpenIddictApplications`, `OpenIddictTokens`, `OpenIddictScopes` - central
  auth registry (single issuer surface across all subdomains, required by
  OAuth2)
- Host-side `AbpUsers` / `AbpRoles` (only the SoCal IT-Admin lives here per
  `InternalUsersDataSeedContributor.cs`)
- Universal lookups: `AppStates`, `AppWcabOffices`, `AppAppointmentTypes`,
  `AppAppointmentStatuses`, `AppAppointmentLanguages` (treaty/agency-driven
  reference data; safe to share)

The per-tenant database (`CaseEvaluation_<slug>`) holds:

- Tenant-side `AbpUsers` / `AbpRoles` (clinic staff + every external user)
- All `App*` business tables: Appointments, AppointmentAccessor,
  AppointmentDocuments, ChangeRequests, ApplicantAttorneys, EmployerDetails,
  Doctors, DoctorAvailabilities, DoctorPreferredLocations, CustomFields,
  DocumentPackages, NotificationTemplates, SystemParameters
- `AppPatients` and `AppLocations` - both move from host to tenant as part of
  this change (FEAT-09 fix and OLD-parity fix respectively)

### 3. Phase plan

- **Phase 1A (this PR):** ship the resolver + wildcard + LeptonX hide +
  Angular subdomain detection + FEAT-09 (Patient IMultiTenant) + Falkinstein
  tenant on its own DB. Smoke test the user flow.
- **Phase 1B (follow-up PR):** add Pelton tenant. Cross-tenant isolation
  tests demonstrate Falkinstein appointments are invisible at
  `pelton.localhost:4200`.
- **Phase 2 (future):** `IBrandingAppService` per-tenant config endpoint;
  reporting replica DB; production domain swap.

## Consequences

**Easier:**

- OLD's isolation property is preserved without per-doctor deployments.
- Production swap-out is one config block per host plus DNS - no schema or
  app-code changes.
- Cross-tenant tests run against real isolation, not mocks.
- HIPAA-relevant tenant boundary is enforced at the resolver layer, not at
  every AppService method.

**Harder:**

- Two host-only entities (`Patient`, `Location`) must move to the tenant
  side. Two EF migrations (drop in host, create in tenant) and DbContext
  config edits required.
- Local dev requires `*.localhost` subdomains. Edge/Chrome/Firefox handle
  RFC 6761 transparently. Hosts file entries added belt-and-suspenders.
  **Safari on macOS does not support `*.localhost`** - not in our support
  matrix but flagged.
- Cookie auth on the AuthServer must scope cookies correctly. We're on
  bearer tokens (OAuth2 code+PKCE), so SPA storage is naturally
  origin-scoped. AuthServer cookie auth still applies - verify cookies are
  not domain-scoped to `.localhost`.

## Alternatives Considered

1. **Phase-1 forced-tenant via `forcedTenant` query/cookie** - Smaller code
   surface, but still allows `?__tenant=` from a knowledgeable user. Doesn't
   match OLD's enforcement story. Rejected.

2. **Per-port worktrees** - Each tenant runs its own `docker compose` stack
   on its own port (4200/4210/...). Closest to OLD's actual deployment
   model. Already partially supported via `scripts/worktrees/`. Rejected for
   Phase 1A because it requires N stacks for N tenants and complicates the
   demo (separate containers, separate volumes); the user explicitly asked
   for "URL like `falkinstein.localhost:4200`" which means single deployment
   + subdomain.

3. **Single shared database with row-level filtering** - ABP default. Easy to
   set up, but loses physical isolation. Strict OLD-parity directive
   prohibits. Rejected per the directive in
   `project_old-app-context.md`.

## Verification

This ADR's claims are verified against:

- ABP framework docs:
  https://abp.io/docs/latest/framework/architecture/multi-tenancy
- Volosoft Medium article on Angular + OpenIddict subdomain resolution:
  https://medium.com/volosoft/how-to-use-domain-based-tenant-resolver-in-abp-with-angular-and-openiddict-d749ad1df2c3
- ABP SaaS module per-tenant connection strings:
  https://abp.io/modules/tenant-management
- LeptonX tenant box hide pattern:
  https://community.abp.io/posts/hide-the-tenant-switch-of-the-login-page-4foaup7p
- Microsoft Learn `*.localhost` TLD support:
  https://learn.microsoft.com/en-us/aspnet/core/test/localhost-tld?view=aspnetcore-10.0

OLD spec citations are verbatim quotes from
`docs/parity/_old-docs-index.md` lines 88-100, sourcing
`Documents_and_Diagrams/Architecture/SoCal Project Overview Document.docx`
lines 113 and 599.

FEAT-09 (Patient must be IMultiTenant) is documented in
`src/HealthcareSupport.CaseEvaluation.Domain/Patients/CLAUDE.md` line 117 +
`Patients/CLAUDE.md` Known Gotchas section. Skipped test
`PatientsAppServiceTests.GetListAsync_WhenCallerIsTenantScoped_ReturnsOnlyTheirTenantPatients`
pins the target behavior and flips green when this ADR ships.
