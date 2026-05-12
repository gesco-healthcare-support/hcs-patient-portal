/**
 * ADR-006 (2026-05-05) -- subdomain tenant routing.
 * ADR-007 (2026-05-11) -- "admin" subdomain is the Volo SaaS Host surface;
 * resolved server-side via HostAwareDomainTenantResolveContributor.
 *
 * The Angular SPA's environment file holds bare-host URLs
 * (`http://localhost:44368`, etc.) baked at build time. This module
 * rewrites those URLs at boot, before `bootstrapApplication`, so each
 * tenant subdomain talks to its own AuthServer + API origin:
 *
 *   `falkinstein.localhost:4200`
 *     -> issuer  `http://falkinstein.localhost:44368/`
 *     -> apis    `http://falkinstein.localhost:44327`
 *     -> baseUrl `http://falkinstein.localhost:4200`
 *
 * Bare `localhost:4200` (no subdomain) redirects to `admin.localhost:4200`
 * because `admin` is the reserved Volo SaaS Host surface. The api + auth
 * server both register `HostAwareDomainTenantResolveContributor` (see
 * `src/HealthcareSupport.CaseEvaluation.HttpApi/MultiTenancy/`) which
 * recognises `admin` as reserved and leaves CurrentTenant null (Host
 * context). Empirically the stock `DomainTenantResolveContributor`
 * does NOT fall through on an unknown slug -- it 404s with header
 * `Abp-Tenant-Resolve-Error: Tenant not found!`. Without the custom
 * contributor `admin.localhost` would 404 on every request.
 *
 * Ports must match `App__CorsOrigins` in `docker-compose.yml` and
 * `WildcardDomainsFormat` in `CaseEvaluationAuthServerModule`. The
 * regex match here is intentionally narrow (only swap the `localhost`
 * host token; preserve scheme + port + path) so production swap-out
 * is one constant change in this file.
 *
 * For Adrian (React analogue): this is the "rewrite the API base URL
 * before any provider reads the config" pattern. In React you'd do this
 * by setting `window.__APP_CONFIG__` before the React tree renders. In
 * Angular, mutating the imported `environment` constant before
 * `bootstrapApplication` accomplishes the same thing because every
 * `provideAbpCore({ environment })` consumer reads from that object.
 */

import { Environment } from '@abp/ng.core';

const HOST_BASE = 'localhost';
const ADMIN_SLUG = 'admin';

/**
 * Returns the resolved tenant slug ('falkinstein', 'admin', etc.) or
 * `null` when the page has been redirected (caller should abort
 * bootstrap on null).
 *
 * Behavior:
 * - bare `localhost` host        -> redirect to `admin.${HOST_BASE}` (host context surface)
 * - IPv4 / numeric host          -> redirect to `admin.${HOST_BASE}` (no slug usable)
 * - `<slug>.${HOST_BASE}` host   -> return slug
 * - any other host (production)  -> return first label (Phase 2 will refine)
 */
export function detectTenantSlugAndMaybeRedirect(): string | null {
  const hostname = window.location.hostname;

  // Numeric (IPv4) or bare base host -> no usable slug; redirect.
  if (hostname === HOST_BASE || /^[0-9.]+$/.test(hostname) || hostname === '::1') {
    const port = window.location.port ? `:${window.location.port}` : '';
    const target = `${window.location.protocol}//${ADMIN_SLUG}.${HOST_BASE}${port}${window.location.pathname}${window.location.search}${window.location.hash}`;
    window.location.replace(target);
    return null;
  }

  const parts = hostname.split('.');
  // Treat the leftmost label as the tenant slug. `admin.localhost` -> 'admin',
  // `falkinstein.localhost` -> 'falkinstein', `pelton.localhost` -> 'pelton'.
  // For non-localhost hosts we still extract the leftmost label so production
  // domains (e.g. `falkinstein.qmeame.com`) Just Work; Phase 2 may add a
  // configurable base-host suffix list if more than one suffix needs to be
  // honored simultaneously.
  return parts[0];
}

/**
 * Mutates the imported `environment` object in place so every provider
 * that reads it (provideAbpCore, provideAbpOAuth, provideLogo, etc.)
 * sees subdomain-prefixed URLs. Call before `bootstrapApplication`.
 *
 * Only `localhost` (bare) host tokens are rewritten -- scheme, port, and
 * path are preserved. URLs that already have a subdomain are left alone
 * (they were configured intentionally, e.g. for an external service).
 */
export function rewriteEnvironmentForTenantSubdomain(env: Environment, slug: string): void {
  // Skip rewrite when the slug already matches the apparent base, to avoid
  // mutating into `admin.admin.localhost` if the function is somehow re-entered.
  if (!slug || slug === HOST_BASE) {
    return;
  }

  const targetHost = `${slug}.${HOST_BASE}`;
  // (^|//)localhost(:port|/|end-of-string) -- match localhost only when it is
  // the host token of a URL or at the start of a string. Avoids touching
  // accidental "localhost" substrings.
  const swap = (url: string | undefined): string | undefined => {
    if (!url) return url;
    return url.replace(/(^|\/\/)localhost(?=([:/]|$))/g, `$1${targetHost}`);
  };

  if (env.application) {
    env.application.baseUrl = swap(env.application.baseUrl as string) ?? env.application.baseUrl;
  }
  if (env.oAuthConfig) {
    const cfg = env.oAuthConfig as Record<string, unknown>;
    cfg['issuer'] = swap(cfg['issuer'] as string);
    cfg['redirectUri'] = swap(cfg['redirectUri'] as string);
    cfg['postLogoutRedirectUri'] = swap(cfg['postLogoutRedirectUri'] as string);
    // Bug D fix (2026-05-11) -- silent-refresh helper lives on AuthServer
    // wwwroot; rewrite the bare-host token to the tenant subdomain.
    cfg['silentRefreshRedirectUri'] = swap(cfg['silentRefreshRedirectUri'] as string);
  }
  if (env.apis) {
    for (const apiName of Object.keys(env.apis)) {
      const api = env.apis[apiName] as Record<string, unknown>;
      api['url'] = swap(api['url'] as string);
    }
  }
}
