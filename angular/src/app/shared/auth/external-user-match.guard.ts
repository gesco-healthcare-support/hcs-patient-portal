import { inject } from '@angular/core';
import { ConfigStateService } from '@abp/ng.core';
import { CanMatchFn } from '@angular/router';
import { hasOnlyExternalRoles } from './external-user-roles';

/**
 * CanMatchFn that matches a route ONLY for pure-external users (every role
 * is an external role: Patient / Applicant Attorney / Defense Attorney /
 * Claim Examiner).
 *
 * Used to serve the redesigned external pages at the canonical routes
 * (e.g. appointments/view/:id) while internal staff fall through to the
 * legacy component declared on the SAME path immediately below. The
 * redesigned external components are role-specific and EXTEND their legacy
 * counterparts; the internal equivalents are not reworked yet (Prompts
 * 11/12), so the legacy components must stay live for internal users.
 *
 * Mirrors the post-login-redirect.guard contract: a user with any internal
 * role (including mixed external+internal) is treated as internal and does
 * NOT match. Anonymous users (zero roles) return false and fall through to
 * the legacy route, whose authGuard issues the OAuth challenge.
 */
export const externalUserOnlyMatchGuard: CanMatchFn = () => {
  const config = inject(ConfigStateService);
  const currentUser = config.getOne('currentUser') as { roles?: string[] | null } | null;
  return hasOnlyExternalRoles(currentUser?.roles ?? []);
};
