import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { ConfigStateService } from '@abp/ng.core';
import { hasOnlyExternalRoles } from './external-user-roles';

// Phase 9 L7 (2026-05-04) -- post-login redirect guard.
//
// OLD behaviour (verified at
// P:\PatientPortalOld\PatientAppointment.Domain\Core\UserAuthenticationDomain.cs:99):
// the legacy app routed external users (Patient / Applicant Attorney /
// Defense Attorney / Claim Examiner / Adjuster) to /home and internal
// users (admin / Clinic Staff / Staff Supervisor / IT Admin / Doctor)
// to /dashboard.
//
// ABP redirects everyone to / after authentication. This guard runs on
// the / route: if the user is authenticated AND every role they hold is
// internal, it issues a UrlTree redirect to /dashboard. External users
// stay at /home (the home component already mounted at /).
//
// Anonymous users pass through; authGuard on the dashboard route catches
// them at /dashboard if they ever land there before authenticating. The
// home route is intentionally not gated so the public landing page
// renders before login.

export const postLoginRedirectGuard: CanActivateFn = () => {
  const config = inject(ConfigStateService);
  const router = inject(Router);

  const currentUser = config.getOne('currentUser') as {
    isAuthenticated?: boolean;
    roles?: string[] | null;
  } | null;

  // Anonymous: let the home route render. This matches OLD where the
  // login screen is the entry point but / itself was the doctor's
  // landing page once authenticated.
  if (!currentUser || !currentUser.isAuthenticated) {
    return true;
  }

  const roles = currentUser.roles ?? [];
  if (hasOnlyExternalRoles(roles)) {
    // External user -- keep them at /home.
    return true;
  }

  // Internal user (or mixed-role user with at least one internal role)
  // -- redirect to dashboard.
  return router.createUrlTree(['/dashboard']);
};
