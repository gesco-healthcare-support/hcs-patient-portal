import { ConfigStateService } from '@abp/ng.core';
import { inject } from '@angular/core';
import { CanMatchFn, Router } from '@angular/router';

/**
 * Issue #105 (2026-05-13) -- guard for routes that are dead code for
 * external visitors. Returns a UrlTree to `/` when the caller is
 * authenticated (so accidental navigation lands them somewhere useful)
 * and lets anonymous callers fall through to the component bound on
 * the route -- today, {@link RouteNotFoundComponent} for `/account/register`.
 *
 * Used as a CanMatchFn so the guard fires BEFORE Angular downloads the
 * lazy component chunk; combined with first-match precedence, this
 * route entry suppresses ABP's stock account/register lazy module
 * (which would otherwise render at the same URL via the @volo/abp.ng.account
 * wildcard child route).
 */
export const redirectAuthedToHomeGuard: CanMatchFn = () => {
  const config = inject(ConfigStateService);
  const router = inject(Router);
  const currentUserId = config.getDeep('currentUser.id');
  if (currentUserId) {
    return router.parseUrl('/');
  }
  // Anonymous: fall through to the component bound on the route.
  return true;
};
