import { enableProdMode } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';

import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';
import { environment } from './environments/environment';
import {
  detectTenantSlugAndMaybeRedirect,
  rewriteEnvironmentForTenantSubdomain,
} from './tenant-bootstrap';

// BUG-015 (Task B, 2026-05-20) -- runtime-load dynamic-env.json so the same
// built Angular image can be re-pointed at different backend ports per
// Docker stack. ABP's provideAbpCore({ environment }) captures the imported
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

  // ADR-006 (2026-05-05) -- subdomain tenant routing.
  // Detect tenant from the URL subdomain BEFORE bootstrapping. If the host
  // has no subdomain, `detectTenantSlugAndMaybeRedirect` issues a 302-style
  // `window.location.replace` to `admin.localhost:<port>` and returns null;
  // in that case we abort bootstrap because the page is about to navigate.
  const tenantSlug = detectTenantSlugAndMaybeRedirect();
  if (tenantSlug !== null) {
    rewriteEnvironmentForTenantSubdomain(environment, tenantSlug);

    if (environment.production) {
      enableProdMode();
    }

    bootstrapApplication(AppComponent, appConfig).catch((err) => console.error(err));
  }
})();
