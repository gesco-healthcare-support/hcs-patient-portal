import { enableProdMode } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';

import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';
import { validateRuntimeConfig } from './config-validation';
import { environment } from './environments/environment';
import {
  detectTenantSlugAndMaybeRedirect,
  rewriteEnvironmentForTenantSubdomain,
} from './tenant-bootstrap';

/**
 * Renders a non-blocking banner naming invalid runtime settings.
 *
 * Plain DOM rather than a component because this runs before `bootstrapApplication`,
 * so no Angular component, service or router exists yet.
 *
 * The copy deliberately does NOT say the app fell back to defaults: `Object.assign` has
 * already overwritten most keys with the bad values, and only `baseHost` has a fallback,
 * so claiming otherwise would be both reassuring and wrong.
 */
function showConfigProblemBanner(problems: string[]): void {
  const settings = problems.map((p) => p.split(' ')[0]).join(', ');
  const banner = document.createElement('div');
  banner.id = 'config-problem-banner';
  banner.setAttribute('role', 'alert');
  banner.style.cssText = [
    'position:fixed',
    'top:0',
    'left:0',
    'right:0',
    'z-index:10000000',
    'padding:12px 16px',
    'background:#7f1d1d',
    'color:#fff',
    'font:14px/1.5 system-ui,sans-serif',
  ].join(';');
  banner.textContent =
    `Configuration problem: ${problems.length} ` +
    `${problems.length === 1 ? 'setting' : 'settings'} in dynamic-env.json ` +
    `${problems.length === 1 ? 'is' : 'are'} not valid -- ${settings}. ` +
    'This file is written at container start from environment variables ' +
    '(prod-dynamic-env.envsh). Tenant routing and sign-in may not work correctly ' +
    'until it is corrected.';
  document.body.appendChild(banner);
}

// BUG-015 (Task B, 2026-05-20) -- runtime-load dynamic-env.json so the same
// built Angular image can be re-pointed at different backend URLs per
// deployment. ABP's provideAbpCore({ environment }) captures the imported
// reference, so mutating environment here (before bootstrap) propagates.
// Same pattern proven by tenant-bootstrap.ts's subdomain rewrite. On fetch
// failure: console.warn + silent fallback to the baked environment.docker.ts
// URLs.
(async () => {
  try {
    const res = await fetch('dynamic-env.json', { cache: 'no-store' });
    if (res.ok) {
      Object.assign(environment, await res.json());
    } else {
      console.warn('[bootstrap] dynamic-env.json returned', res.status, '-- using baked defaults');
    }
  } catch (err) {
    console.warn('[bootstrap] dynamic-env.json fetch failed:', err, '-- using baked defaults');
  }

  // Production hardening 1.7 (2026-09-01) -- validate what the merge above produced.
  // Nothing checked the shape of dynamic-env.json, and four separate producers write
  // it, so a malformed value reached tenant routing and sign-in silently. Start anyway
  // and report loudly: on a self-hosted deployment the person who sees a blank page is
  // not the person who can read a container log, so refusing to boot would hide the
  // cause rather than surface it.
  const configProblems = validateRuntimeConfig(environment);
  if (configProblems.length > 0) {
    for (const problem of configProblems) {
      console.error('[config]', problem);
    }
    showConfigProblemBanner(configProblems);
  }

  // ADR-006 (2026-05-05) -- subdomain tenant routing.
  // In-house hosting (2026-07-09, T4) -- the base host comes from dynamic-env.json
  // (`baseHost`) so the same built SPA serves any environment; dev/tests fall back
  // to 'localhost'. Detect the office from the URL subdomain BEFORE bootstrapping.
  // If the host has no subdomain, `detectTenantSlugAndMaybeRedirect` issues a
  // 302-style `window.location.replace` to `admin.<baseHost>` and returns null; in
  // that case we abort bootstrap because the page is about to navigate.
  const baseHost = (environment as { baseHost?: string }).baseHost ?? 'localhost';
  const tenantSlug = detectTenantSlugAndMaybeRedirect(baseHost);
  if (tenantSlug !== null) {
    rewriteEnvironmentForTenantSubdomain(environment, tenantSlug);

    if (environment.production) {
      enableProdMode();
    }

    bootstrapApplication(AppComponent, appConfig).catch((err) => console.error(err));
  }
})();
