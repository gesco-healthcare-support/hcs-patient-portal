import { inject } from '@angular/core';
import { ConfigStateService } from '@abp/ng.core';
import { CanMatchFn } from '@angular/router';
import { hasOnlyExternalRoles } from './external-user-roles';

/**
 * CanMatchFn that matches a route for everyone EXCEPT pure-external users --
 * the exact complement of {@link externalUserOnlyMatchGuard}. Used to gate the
 * internal-shell parent route so:
 *
 *   - internal staff (any non-external role) match -> they get the shell;
 *   - anonymous users (zero roles) also match, so the child routes' authGuard
 *     can issue the OAuth challenge / redirect to login (rather than falling
 *     through to the wildcard 404);
 *   - pure-external users do NOT match, so they fall through to their own
 *     chrome-less external routes declared outside the shell.
 *
 * Mirrors externalUserOnlyMatchGuard so a shared path (e.g. appointments) can
 * resolve to the internal shell for staff and the external page for patients.
 */
export const internalUserOnlyMatchGuard: CanMatchFn = () => {
  const config = inject(ConfigStateService);
  const currentUser = config.getOne('currentUser') as { roles?: string[] | null } | null;
  return !hasOnlyExternalRoles(currentUser?.roles ?? []);
};
