import { inject } from '@angular/core';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { CanMatchFn, Router } from '@angular/router';
import { hasOnlyExternalRoles } from './external-user-roles';

// Phase 9 L7 (2026-05-04) + Issue 1.1 (2026-05-12) -- post-login redirect guard.
//
// OLD behaviour (verified at
// P:\PatientPortalOld\PatientAppointment.Domain\Core\UserAuthenticationDomain.cs:99):
// the legacy app routed external users (Patient / Applicant Attorney /
// Defense Attorney / Claim Examiner / Adjuster) to /home and internal
// users (admin / Clinic Staff / Staff Supervisor / IT Admin / Doctor)
// to /dashboard.
//
// ABP redirects everyone to / after authentication. This guard runs on
// the / route as a CanMatchFn -- evaluated BEFORE the lazy
// loadComponent fetch fires. Returning a UrlTree cancels the
// navigation and starts a new one without ever downloading the
// HomeComponent chunk for users who shouldn't see it. This prevents
// the "flash of empty home shell" that the prior CanActivateFn caused
// (the chunk had already loaded by the time the guard returned its
// UrlTree).
//
// Three outcomes:
//   - Anonymous     -> AuthService.navigateToLogin() (redirects out of SPA);
//                      return false to cancel the in-SPA navigation
//   - Internal user -> UrlTree(/dashboard)
//   - External user -> true (HomeComponent loads at /)

export const postLoginRedirectGuard: CanMatchFn = () => {
  const config = inject(ConfigStateService);
  const auth = inject(AuthService);
  const router = inject(Router);

  const currentUser = config.getOne('currentUser') as {
    isAuthenticated?: boolean;
    roles?: string[] | null;
  } | null;

  // Anonymous: kick off the OAuth challenge which redirects the browser
  // out of the SPA to the AuthServer Razor login page on the same tenant
  // subdomain (port 44368). 2026-05-15 -- the SPA's own /account/login
  // route was deleted along with the rest of the SPA auth tree, so we
  // can't return a UrlTree as a fallback. Return false to cancel the
  // in-SPA navigation; the redirect that navigateToLogin already issued
  // takes the browser to AuthServer before the cancel matters.
  if (!currentUser || !currentUser.isAuthenticated) {
    auth.navigateToLogin();
    return false;
  }

  const roles = currentUser.roles ?? [];
  if (hasOnlyExternalRoles(roles)) {
    // External user -- keep them at /home (HomeComponent loads).
    return true;
  }

  // Internal user (or mixed-role user with at least one internal role)
  // -- redirect to dashboard before HomeComponent chunk loads.
  return router.parseUrl('/dashboard');
};
