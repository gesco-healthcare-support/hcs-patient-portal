[Home](../INDEX.md) > [Architecture](./) > Offices and Hosting

# Offices and Hosting

> Purpose: Reference for how an office name becomes a subdomain, a database and a routed hostname, and what the production stack runs. Audience: backend and infrastructure engineers changing office naming, the reverse proxy or the production compose file.

One office is one ABP tenant row, one subdomain label, and one SQL database. Those three are
the same string:

```text
office name  "falkinstein"
  -> slug            falkinstein          (validated, not transformed)
  -> tenant Name     falkinstein          (what the Host resolver looks up)
  -> database        CaseEvaluation_falkinstein
  -> SPA host        falkinstein.<BASE_DOMAIN>
  -> API host        falkinstein.api.<BASE_DOMAIN>
  -> Auth host       falkinstein.auth.<BASE_DOMAIN>
```

Before adding anything to `docker/nginx-proxy/default.conf.template`, read
[The maintenance trap](#the-maintenance-trap) on this page. It is the one change in this area
that looks complete, passes review, and leaves an office unreachable.

For the reasoning behind the isolation boundary itself, see
[Tenancy and Isolation](./TENANCY-AND-ISOLATION.md). For entity classification and the resolver
chain, see [Multi-Tenancy Strategy](./MULTI-TENANCY.md).

## Why database-per-office

Two reasons, stated by the product owner on 2026-09-28:

1. **Isolation and HIPAA.** Office data is separated by the database boundary rather than only by
   a `TenantId` column and a query filter.
2. **Blast radius.** One consolidated database means a single corruption or failure takes down
   every office. Separate databases confine that to one office.

## The office slug

### It is validated, not transformed

`TenantNaming.DeriveSlug` (`src/HealthcareSupport.CaseEvaluation.Domain.Shared/MultiTenancy/TenantNaming.cs`)
trims the office name and lowercases it, then **validates** the result. It never rewrites spaces to
hyphens, strips punctuation, or otherwise repairs a name. A name that is not already a DNS-safe
token fails fast with `ArgumentException`.

The reason is the round trip. `HostAwareDomainTenantResolveContributor.ResolveAsync` extracts the
leftmost label from the Host and assigns it to `context.TenantIdOrName`, and ABP looks that value up
against the tenant's stored `Name`. `DoctorTenantAppService.CreatePracticeAsync` stores the slug as
that `Name`. So the slug and the stored name must stay byte-identical: transform the slug at
creation time and the subdomain would no longer resolve to the office it names.

### Rules

| Rule | Value | Source |
|---|---|---|
| Maximum length | 63 characters | `TenantNaming.MaxSlugLength` (DNS label limit; also bounds the database name) |
| Shape | `^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$` -- lowercase alphanumeric ends, optional internal hyphens | `TenantNaming.SlugPattern` |
| Regex options | `RegexOptions.CultureInvariant`, 1-second match timeout | `TenantNaming.SlugPattern` |
| Blank name | `ArgumentException` | `TenantNaming.DeriveSlug` |
| Reserved name | `ArgumentException`, with a different message per reserved set | `TenantNaming.DeriveSlug` |

The match timeout is defence in depth rather than a fix for a known hazard: the pattern has no
catastrophic-backtracking risk, and `IsValidSlug` rejects anything over `MaxSlugLength` before the
pattern runs at all. The code comment at the pattern says so; it is the better statement of the
reasoning.

### Where the rules are applied

| Layer | Member | Behaviour |
|---|---|---|
| Domain.Shared | `TenantNaming.DeriveSlug`, `TenantNaming.IsValidSlug` | The rules themselves; throws `ArgumentException` |
| Application | `DoctorTenantAppService.DeriveSlugOrThrow` | Calls `DeriveSlug`, converts `ArgumentException` into `UserFriendlyException` so the New Practice form shows the message |
| Application.Contracts | `CreatePracticeInput.Slug` `[StringLength(63)]` | Mirrors `MaxSlugLength` at the DTO boundary |

## Databases and connection strings

| Database | Name | Holds |
|---|---|---|
| Host | `CaseEvaluation` | Tenant rows, office branding, and other host-scoped data |
| Office | `CaseEvaluation_{slug}` | That office's data. Prefix is `TenantNaming.DatabaseNamePrefix` |

`TenantNaming.GetDatabaseName(slug)` composes the office name and re-validates the slug first, so an
invalid slug cannot reach a catalog name.

### One secret, many catalogs

There is no per-office connection-string setting. `TenantNaming.BuildConnectionString(base, slug)`
takes a base connection string and swaps only the catalog:

- it loads the base into a `DbConnectionStringBuilder`;
- removes both `Initial Catalog` and `Database` (SQL Server treats them as synonyms, so removing one
  is not enough);
- sets `Database` to `CaseEvaluation_{slug}`;
- keeps the base server, authentication and every other option unchanged.

`TenantConnectionStringProvider` (`src/HealthcareSupport.CaseEvaluation.Domain/Data/`) chooses the
base: the optional `App:TenantDbTemplate` override if set, otherwise `ConnectionStrings:Default`.
With neither configured it throws `AbpException` naming both keys. The override exists to place
office databases on a separate SQL instance without duplicating credentials.

The point of deriving rather than configuring is that SQL credentials stay in the single
secret-managed `Default` and are never copied into a second config key.

### When it is used

- **Office creation.** `DoctorTenantAppService.CreateTenantWithOfficeDatabaseAsync` builds the
  connection string, reachability-checks it with `IConnectionStringChecker` before creating
  anything, then stores it on the tenant row with `SetDefaultConnectionString` inside the host
  transaction, and provisions the office database out of band afterwards.
- **Seeding.** `OfficeSeedDataContributor` builds it per office from the office slug.
- **Every subsequent query.** ABP's stock `MultiTenantConnectionStringResolver` reads the string
  stored on the tenant record. Nothing in this repo re-derives it per request.

## Reserved slugs: two separate sets

| Set | Members | Reserved because |
|---|---|---|
| `TenantNaming.ReservedSlug` | `admin` | `admin.<host>` is the Volo SaaS host-context surface. The resolver deliberately does not look it up in the tenant store |
| `TenantNaming.ProxyReservedSlugs` | `api`, `auth`, `minio`, `health`, `www` | The reverse proxy answers these single-label hosts itself with an exact `server_name`, so a request for them never reaches the application |

Both sets are refused by `DeriveSlug` and by `IsValidSlug`, with **different error messages on
purpose**: telling an administrator that `minio` is "reserved for the host-context surface" would
send them looking in the wrong place.

They are kept as two sets rather than one list because only `admin` carries host-context meaning.
Merging them would invite adding `api` or `minio` to
`HostAwareDomainTenantResolveContributor.ReservedHostSlug`, where they do not belong: those names
are not aliases for host context, they are names the proxy consumes.

`admin` is guarded at three layers, each of which can refuse independently:

| Layer | Constant |
|---|---|
| Domain.Shared | `TenantNaming.ReservedSlug` |
| Request boundary | `HostAwareDomainTenantResolveContributor.ReservedHostSlug` |
| Office creation | `DoctorTenantAppService.ReservedTenantNameAdmin` |

The AuthServer's `wwwroot/global-scripts.js` mirrors the same name client-side. The Domain.Shared
copy is the lowest layer so that Domain and Application can validate without referencing the host
projects.

## Host to office resolution

`HostAwareDomainTenantResolveContributor`
(`src/HealthcareSupport.CaseEvaluation.HttpApi/MultiTenancy/`) is the only tenant source for an
anonymous request. Authenticated requests never reach it: `CurrentUserTenantResolveContributor` runs
first and reads the office from the token.

The Host template comes from `App:TenantDomainFormat`, defaulting to `{0}.localhost` when unset.
Production sets it per service, so each process matches only its own hostname shape:

| Service | `App__TenantDomainFormat` |
|---|---|
| AuthServer | `{0}.auth.${BASE_DOMAIN}` |
| API | `{0}.api.${BASE_DOMAIN}` |

Resolution order, from `ResolveAsync`:

1. No HTTP request at all (a background job, a console host): abstain.
2. A single office label matching the template: `admin` keeps host context; any other label is
   handed to ABP, whose middleware answers 404 when no such office exists.
3. A name in `InternalHosts` (`localhost`, `authserver`): host context. These serve the container
   health checks, the HealthChecks UI poll and internal calls to the AuthServer by container name.
4. Anything else -- an empty or dotted label, a bare or foreign host, an IP, an empty Host --
   is refused.

The refusal throws `BusinessException(HostNotServedErrorCode, HostNotServedMessage)`. ABP's
`MultiTenancyMiddleware` catches it and answers 404 with an `Abp-Tenant-Resolve-Error` header, the
same response shape as an unknown office. The message is the fixed string
`This host does not serve an office.` and never includes the request's Host, because that value is
caller-controlled and would otherwise reach both a response header and the logs.

`ExtractSlug` returns no slug (and therefore falls through to the internal-host check and then the
refusal) when the format has no `{0}`, when prefix or suffix do not match, when the slot is empty,
or when the extracted label contains a dot.

## nginx routing table

`docker/nginx-proxy/default.conf.template` is rendered at container start by the nginx image's
envsubst, which substitutes only variables present in the container environment -- so nginx's own
runtime variables (`$host`, `$scheme`, `$remote_addr`) survive. `BASE_DOMAIN`,
`TRUSTED_PROXY_SET_REAL_IP_FROM` and `TRUSTED_PROXY_REAL_IP_RECURSIVE` are supplied by
`docker-compose.prod.yml`, and the two proxy variables are **always defined, empty by default**: an
undefined variable is left as a literal `${...}` and nginx will not start.

nginx selects a server block by name specificity, not file order: exact name, then longest `*.`
wildcard, then suffix wildcard, then regex, then `default_server`.

| Listener / `server_name` | Result |
|---|---|
| `:80` `_` (default_server) | 301 to `https://$host$request_uri`, for every host |
| `:443` `_` (default_server) | **421**. Catch-all for any Host matching no block below |
| `*.auth.${BASE_DOMAIN}` | Proxy to `authserver:8080`. `client_max_body_size 15m`, WebSocket upgrade headers |
| `*.api.${BASE_DOMAIN}` | Proxy to `api:8080`. `client_max_body_size 15m`, WebSocket upgrade headers |
| `api.${BASE_DOMAIN}` `auth.${BASE_DOMAIN}` | **JSON 404** with code `missing_office_label` |
| `health.${BASE_DOMAIN}` | `= /health-status` proxies to `api:8080` with the upstream Host pinned to `admin.api.${BASE_DOMAIN}`; every other path is a JSON 404 with code `health_probe_only` |
| `www.${BASE_DOMAIN}` | 301 to the apex |
| `${BASE_DOMAIN}` (apex) | Static explanation page from `docker/nginx-proxy/apex/index.html`, served by the proxy itself. No upstream request, names no office |
| `minio.${BASE_DOMAIN}` | Proxy to `minio:9000`, path-style addressing. `client_max_body_size 0`, request and response buffering off, send/read timeouts 300s |
| `*.${BASE_DOMAIN}` | Proxy to `angular:8080` (the SPA). Least specific; the blocks above win |

Notes the template records, worth keeping in mind when editing it:

- **The 421 is not a drop.** HTTP/2 is on for every block and one certificate covers several names,
  so a browser may coalesce connections across them. 421 is the RFC 9110 signal to retry on a fresh
  connection; 444 would give an uninterpretable reset and conceals little, since port 80 answers
  every host and the TLS handshake announces the certificate's names before Host is read.
- **The catch-all needs its `ssl_` directives.** On 443 the handshake completes before Host is read,
  so this block serves the certificate whenever SNI is absent or unmatched.
- **The bare `api` / `auth` 404 is answered at the proxy on purpose.** Proxying it to the API
  instead would run the request in host context, because the domain resolver extracts no slug from
  a bare `api.<domain>` -- an undocumented second alias for the admin surface, reached by extraction
  failing rather than by the reserved-slug rule. 404 rather than 421 because 421 tells a client to
  retry a hostname that will never work.
- **`missing_office_label` and `health_probe_only` are grep markers.** A bare 404 cannot tell "the
  proxy refused" from "the API has no such route", which is how a check comes back green having
  proved nothing.
- **The MinIO block re-resolves its upstream** (`resolver 127.0.0.11`, Docker's embedded DNS, with
  the upstream in a variable) because a backend rebuild moves container IPs and has silently broken
  routing there twice. It also preserves the Host header, which S3 signature v4 signs.
- **Real client IP.** `real_ip_header X-Forwarded-For` is unconditional, but nginx only rewrites
  `$remote_addr` for connections from an address listed in `set_real_ip_from`. With
  `TRUSTED_PROXY_SET_REAL_IP_FROM` empty (the LAN case) nothing is trusted and nothing is rewritten.
  Whether `real_ip_recursive` belongs on is a per-balancer decision; the template explains both
  cases and names what is still unverified.

## The maintenance trap

**Adding an exact single-label `server_name` block to `docker/nginx-proxy/default.conf.template`
requires adding that label to `TenantNaming.ProxyReservedSlugs`. Neither file references the
other.**

What goes wrong when the second edit is missed:

1. nginx ranks an exact `server_name` above every wildcard, whatever the file order. So
   `newthing.<BASE_DOMAIN>` is captured by the new block instead of falling through to
   `*.${BASE_DOMAIN}`.
2. Nothing in the application refuses an office named `newthing`, because `ProxyReservedSlugs` does
   not list it. Creation succeeds: the tenant row is written, the database
   `CaseEvaluation_newthing` is provisioned and seeded, and the office looks healthy from the admin
   surface.
3. The office then has no reachable front door. Its SPA host is consumed by the proxy block, the
   request never reaches the application, and no error anywhere explains why.

This is not hypothetical. `minio` became an exact host in August 2026 and its own comment said it
"becomes a RESERVED office slug, like `admin`" -- with nothing enforcing that. `api` and `auth`
arrived with the same gap in #1021, and `health` and `www` joined in #1100.

**The one thing that catches it** is a drift guard in the test suite:
`TenantNamingTests.ProxyReservedSlugs_matches_the_exact_single_label_hosts_in_the_nginx_template`
(`test/HealthcareSupport.CaseEvaluation.Domain.Tests/MultiTenancy/TenantNamingTests.cs`). It parses
the template, strips comments first so prose about `server_name` is not counted, collects exact
single-label hosts only (wildcards such as `*.api.${BASE_DOMAIN}` reserve nothing), and compares the
result to `ProxyReservedSlugs` **as a set in both directions**, so a stale entry left after a block
is removed fails as loudly as a missing one. It asserts the parse found at least one host, and
throws rather than skipping if it cannot find the template, because a guard that quietly passes when
its input goes missing is a failure this repository has shipped before.

So the coupling is enforced by a test, and by nothing in either file. A reader of the nginx template
alone, or of `TenantNaming` alone, will not see it. Run the Domain tests after touching either.

## Production stack

`docker-compose.prod.yml` defines ten services. The reverse proxy is the only one published to the
host; every other service is reachable only on the internal Docker network.

| Service | Image or build | Role |
|---|---|---|
| `sql-server` | `mcr.microsoft.com/mssql/server:2022-CU25-GDR2-ubuntu-22.04` | Host database plus every office database. Memory-capped so SQL cannot starve the app tier; `BACKUP DATABASE` writes to a bind-mounted directory |
| `redis` | `redis:7.4.9-alpine` | Distributed cache and the DataProtection keyring, shared by the API and AuthServer. AOF persistence on, so a restart does not log everyone out |
| `minio` | `quay.io/minio/minio` (pinned release tag) | S3-compatible object store for documents. Not published to the host |
| `minio-init` | `quay.io/minio/mc` (pinned release tag) | One-shot. Creates the configured buckets if absent, then exits |
| `packet-renderer` | Built from `docker/packet-renderer/Dockerfile` | Document/packet rendering service on port 3001 |
| `db-migrator` | Built from the DbMigrator project, `prod` target | One-shot. Applies EF migrations and runs seed contributors, then exits. The API and AuthServer wait on `service_completed_successfully` |
| `authserver` | Built from the AuthServer project, `prod` target | OpenIddict plus the Razor identity UI. Mounts `openiddict.pfx` read-only; never baked into the image |
| `api` | Built from the HttpApi.Host project, `prod` target | The application API. Split-horizon OIDC: reaches the AuthServer internally, validates tokens against the public issuer |
| `angular` | Built from `./angular`, `prod` target | The SPA, served by its own nginx. Runtime environment is written into `dynamic-env.json` at start |
| `reverse-proxy` | `nginx:1.31.2-alpine` | Terminates TLS, routes by Host per the table above. The only service with published ports (80 and 443) |

Supporting detail:

- **Persistent named volumes** `sqldata`, `redisdata`, `miniodata` hold all state, including every
  per-office database and the DataProtection keys.
- **Secrets come only from the git-ignored `secrets/env.prod`** (see `env.prod.example`). Nothing
  secret is committed or baked into an image.
- **The ABP Commercial feed key is a BuildKit secret**, not a build ARG: an ARG value is recorded in
  the metadata of every layer of the stage that declares it and would be readable with
  `docker history`.
- **Image tags are pinned to what is deployed**, recorded rather than upgraded. The MinIO images
  come from quay.io, not Docker Hub, which has withdrawn anonymous pull for both MinIO
  repositories.

## Related documentation

- [Tenancy and Isolation](./TENANCY-AND-ISOLATION.md) -- why the boundary is built this way
- [Multi-Tenancy Strategy](./MULTI-TENANCY.md) -- resolver chain, dual DbContext, entity classification
- [System Overview](./OVERVIEW.md) -- topology, projects, ports
