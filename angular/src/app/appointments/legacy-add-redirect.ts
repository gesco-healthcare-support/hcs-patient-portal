import { inject } from '@angular/core';
import { RedirectFunction, Router } from '@angular/router';

/**
 * Sends a pure-external caller from the retired `/appointments/add` form to the booking wizard at
 * `/appointments/request`.
 *
 * <p>Phase 4a (2026-08-03). The legacy add form was the last surface rendering
 * `AppointmentAddComponent`'s own template, but nothing navigated to it -- external booking had
 * already moved to the wizard -- so it survived only as a typed URL, and every booking change had to
 * be verified on two surfaces or risk the unused one drifting.</p>
 *
 * <p>A REDIRECT rather than a plain deletion, because the internal shell route is gated by
 * `internalUserOnlyMatchGuard`: an external user who stopped matching the removed route would fall
 * past the shell to the `**` 404 rather than reaching a booking form at all.</p>
 *
 * <p>Returns a `UrlTree` rather than a path string so `?type=1` / `?type=2` (new vs re-evaluation,
 * read when the wizard initialises) is carried across explicitly. A bare string would leave that to
 * the router's default redirect handling, which is exactly the kind of implicit behaviour that
 * silently drops a deep link.</p>
 */
export const legacyAddRedirect: RedirectFunction = (route) =>
  inject(Router).createUrlTree(['/appointments/request'], {
    queryParams: route.queryParams,
    fragment: route.fragment ?? undefined,
  });
