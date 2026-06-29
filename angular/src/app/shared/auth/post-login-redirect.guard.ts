import { inject } from '@angular/core';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { CanMatchFn, Router } from '@angular/router';
import { hasOnlyExternalRoles } from './external-user-roles';
import { resolveInternalRoleKey } from './internal-user-roles';

// Phase 9 L7 (2026-05-04) + Issue 1.1 (2026-05-12) -- post-login redirect guard.
//
// OLD behaviour (verified at
// P:\PatientPortalOld\PatientAppointment.Domain\Core\UserAuthenticationDomain.cs:99):
// the legacy app routed external users (Patient / Applicant Attorney /
// Defense Attorney / Claim Examiner / Adjuster) to /home and internal
// users (admin / Intake Staff / Staff Supervisor / IT Admin / Doctor)
// to /dashboard.
//
// ABP redirects everyone to / after authentication. This guard runs on
// the / route as a CanMatchFn -- evaluated BEFORE the lazy
// loadComponent fetch fires. Returning a UrlTree cancels the
// navigation and starts a new one without ever downloading the
// ExternalHomeComponent chunk for users who shouldn't see it. This prevents
// the "flash of empty home shell" that the prior CanActivateFn caused
// (the chunk had already loaded by the time the guard returned its
// UrlTree).
//
// Three outcomes:
//   - Anonymous       -> AuthService.navigateToLogin() (redirects out of SPA);
//                        return false to cancel the in-SPA navigation
//   - Intake Staff    -> UrlTree(/host/my-offices): they have no host-admin
//                        dashboard access, so their home is the assigned-office
//                        switcher (also the landing after they exit an office).
//   - Other internal  -> UrlTree(/dashboard)
//   - External user   -> true (ExternalHomeComponent loads at /)

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
    // External user -- keep them at /home (ExternalHomeComponent loads).
    return true;
  }

  // Intake Staff have no host dashboard access (Dashboard.Host); their home is
  // the assigned-office switcher. Routing them to /dashboard lands them on the
  // "you don't have access" page with no nav -- on login AND after exiting an
  // impersonated office (which reloads to /). Send them to /host/my-offices.
  if (resolveInternalRoleKey(roles) === 'intake') {
    return router.parseUrl('/host/my-offices');
  }

  // Other internal users (admin / IT Admin / Staff Supervisor) -- redirect to
  // dashboard before the ExternalHomeComponent chunk loads.
  return router.parseUrl('/dashboard');
};
