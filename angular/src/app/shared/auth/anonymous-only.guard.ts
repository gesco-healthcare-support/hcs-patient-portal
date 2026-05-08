import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { ConfigStateService } from '@abp/ng.core';
import { hasOnlyExternalRoles } from './external-user-roles';

// 2026-05-06 -- anonymous-only guard. Mirrors OLD's CanActivatePage
// `anonymous: true` behaviour at
// P:\PatientPortalOld\patientappointment-portal\src\app\domain\authorization\can-activate-page.ts:47-63.
//
// When an authenticated user lands on /account/login or /account/register,
// redirect them to their role-appropriate home. Internal users go to
// /dashboard, external users go to / (home). The auth blob is left
// untouched -- we never log the prior user out implicitly.
//
// Anonymous users pass through and the form renders normally.
export const anonymousOnlyGuard: CanActivateFn = () => {
  const config = inject(ConfigStateService);
  const router = inject(Router);

  const currentUser = config.getOne('currentUser') as {
    isAuthenticated?: boolean;
    roles?: string[] | null;
  } | null;

  if (!currentUser || !currentUser.isAuthenticated) {
    return true;
  }

  const roles = currentUser.roles ?? [];
  return hasOnlyExternalRoles(roles)
    ? router.createUrlTree(['/'])
    : router.createUrlTree(['/dashboard']);
};
