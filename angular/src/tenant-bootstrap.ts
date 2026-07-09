/**
 * ADR-006 (2026-05-05) -- subdomain tenant routing.
 * ADR-007 (2026-05-11) -- "admin" subdomain is the Volo SaaS Host surface;
 * resolved server-side via HostAwareDomainTenantResolveContributor.
 * In-house hosting (2026-07-09, T4) -- the base host is now runtime config
 * (dynamic-env.json `baseHost`) so the same built image serves any environment.
 *
 * The Angular SPA's environment file holds office-less service URLs baked at
 * build time (dev: `http://localhost:44368`; prod: `https://api.portal.<domain>`).
 * This module rewrites those URLs at boot, before `bootstrapApplication`, so each
 * office subdomain talks to its own AuthServer + API origin. It inserts the office
 * slug as the leftmost host label of every first-party URL, which works uniformly
 * for local dev and the production subdomain-per-service layout:
 *
 *   dev  `falkinstein.localhost:4200`
 *     -> issuer  `http://falkinstein.localhost:44368/`
 *     -> apis    `http://falkinstein.localhost:44327`
 *   prod `falkinstein.portal.example.com`
 *     -> issuer  `https://falkinstein.auth.portal.example.com/`
 *     -> apis    `https://falkinstein.api.portal.example.com`
 *
 * The bare base host (no office subdomain) redirects to `admin.<baseHost>` because
 * `admin` is the reserved Volo SaaS Host surface. The api + auth servers both
 * register `HostAwareDomainTenantResolveContributor` (see
 * `src/HealthcareSupport.CaseEvaluation.HttpApi/MultiTenancy/`) which recognises
 * `admin` as reserved and leaves CurrentTenant null (Host context). Empirically the
 * stock `DomainTenantResolveContributor` does NOT fall through on an unknown slug --
 * it 404s with `Abp-Tenant-Resolve-Error: Tenant not found!`. Without the custom
 * contributor `admin.<baseHost>` would 404 on every request.
 *
 * The backend email-link builder (Notifications/TenantUrlComposer) applies the same
 * "insert the office slug as the leftmost host label" rule; keep the two in sync.
 *
 * For Adrian (React analogue): this is the "rewrite the API base URL before any
 * provider reads the config" pattern. In React you'd set `window.__APP_CONFIG__`
 * before the tree renders. In Angular, mutating the imported `environment` constant
 * before `bootstrapApplication` accomplishes the same thing because every
 * `provideAbpCore({ environment })` consumer reads from that object.
 */

import { Environment } from '@abp/ng.core';

/** Base host used when dynamic-env.json does not supply one (local dev + tests). */
const DEFAULT_BASE_HOST = 'localhost';
const ADMIN_SLUG = 'admin';

/**
 * Returns the resolved office slug ('falkinstein', 'admin', etc.) or `null` when the
 * page has been redirected (caller should abort bootstrap on null).
 *
 * Behavior (base host from dynamic-env.json, default 'localhost'):
 * - bare base host              -> redirect to `admin.<baseHost>` (host surface)
 * - IPv4 / numeric / IPv6 host  -> redirect to `admin.<baseHost>` (no usable slug)
 * - `<slug>.<baseHost>` host    -> return the leftmost label as the slug
 */
export function detectTenantSlugAndMaybeRedirect(
  baseHost: string = DEFAULT_BASE_HOST,
): string | null {
  const hostname = window.location.hostname;

  // Bare base host, a numeric IPv4, or IPv6 loopback -> no usable office slug.
  if (hostname === baseHost || /^[0-9.]+$/.test(hostname) || hostname === '::1') {
    const port = window.location.port ? `:${window.location.port}` : '';
    const target = `${window.location.protocol}//${ADMIN_SLUG}.${baseHost}${port}${window.location.pathname}${window.location.search}${window.location.hash}`;
    window.location.replace(target);
    return null;
  }

  // The leftmost label is the office slug: `admin.localhost` -> 'admin',
  // `falkinstein.localhost` -> 'falkinstein',
  // `falkinstein.portal.example.com` -> 'falkinstein'.
  return hostname.split('.')[0];
}

/**
 * Mutates the imported `environment` object in place so every provider that reads it
 * (provideAbpCore, provideAbpOAuth, provideLogo, etc.) sees office-subdomain URLs.
 * Call before `bootstrapApplication`.
 *
 * Inserts the office slug as the leftmost host label of each first-party URL, right
 * after the scheme -- scheme, port, and path are preserved. Only our own service
 * URLs are rewritten (application.baseUrl, oAuthConfig, apis.*); external URLs (e.g.
 * address validation) live outside `environment` and are untouched.
 */
export function rewriteEnvironmentForTenantSubdomain(env: Environment, slug: string): void {
  if (!slug) {
    return;
  }

  // Insert `<slug>.` immediately after the scheme, e.g.
  //   `http://localhost:44327`            -> `http://falkinstein.localhost:44327`
  //   `https://api.portal.example.com`    -> `https://falkinstein.api.portal.example.com`
  const prependSlug = (url: string | undefined): string | undefined => {
    if (!url) return url;
    return url.replace(/^(https?:\/\/)/i, `$1${slug}.`);
  };

  if (env.application) {
    env.application.baseUrl =
      prependSlug(env.application.baseUrl as string) ?? env.application.baseUrl;
  }
  if (env.oAuthConfig) {
    const cfg = env.oAuthConfig as Record<string, unknown>;
    cfg['issuer'] = prependSlug(cfg['issuer'] as string);
    cfg['redirectUri'] = prependSlug(cfg['redirectUri'] as string);
    cfg['postLogoutRedirectUri'] = prependSlug(cfg['postLogoutRedirectUri'] as string);
  }
  if (env.apis) {
    for (const apiName of Object.keys(env.apis)) {
      const api = env.apis[apiName] as Record<string, unknown>;
      api['url'] = prependSlug(api['url'] as string);
    }
  }
}
