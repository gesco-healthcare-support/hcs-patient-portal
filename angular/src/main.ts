import { enableProdMode } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';

import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';
import { environment } from './environments/environment';
import {
  detectTenantSlugAndMaybeRedirect,
  rewriteEnvironmentForTenantSubdomain,
} from './tenant-bootstrap';

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
