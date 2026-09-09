import { Route, Routes } from '@angular/router';
import { permissionGuard } from '@abp/ng.core';

import { APP_ROUTES } from './app.routes';

/**
 * Pins the permission guard on the booking wizard (BUG-038 / #554).
 *
 * <p>The wizard is reachable at one path, `appointments/request`, declared TWICE: once
 * chrome-less for external users and once as a child of the internal shell. Guarding one and
 * not the other would make the fix depend on which chrome the user happened to get, and the
 * two declarations are 350 lines apart, so nothing but a test keeps them in step.</p>
 *
 * <p>This is not the security boundary. `AppointmentsAppService.SubmitAsync` carries
 * `[Authorize(CaseEvaluationPermissions.Appointments.Create)]` and is authoritative; the
 * route guard exists so a user who cannot book is refused at the click rather than after
 * filling in the whole wizard.</p>
 */
describe('APP_ROUTES booking guard (#554)', () => {
  /** The server-side constant this must mirror: `CaseEvaluationPermissions.Appointments.Create`. */
  const BOOKING_POLICY = 'CaseEvaluation.Appointments.Create';

  function collect(routes: Routes, path: string, found: Route[] = []): Route[] {
    for (const route of routes) {
      if (route.path === path) {
        found.push(route);
      }
      if (route.children) {
        collect(route.children, path, found);
      }
    }
    return found;
  }

  const bookingRoutes = collect(APP_ROUTES, 'appointments/request');

  it('declares the wizard at exactly two places, external and in-shell', () => {
    // If this number changes, the loop below is no longer checking what it claims to.
    expect(bookingRoutes.length).toBe(2);
  });

  it('guards every declaration on the booking permission', () => {
    for (const route of bookingRoutes) {
      const guards = (route.canActivate ?? []) as unknown[];
      expect(guards)
        .withContext('every appointments/request declaration needs permissionGuard')
        .toContain(permissionGuard as unknown);
      expect(route.data?.['requiredPolicy'])
        .withContext('requiredPolicy must mirror the server-side Appointments.Create')
        .toBe(BOOKING_POLICY);
    }
  });

  it('keeps the unsaved-changes guard alongside it', () => {
    // permissionGuard was added to an existing canActivate array; dropping canDeactivate
    // while editing that object would silently lose the "you have unsaved changes" prompt.
    for (const route of bookingRoutes) {
      expect(route.canDeactivate?.length).toBeGreaterThan(0);
    }
  });

  it('leaves exactly one of the two external-only, so the role split still holds', () => {
    // canMatch is what routes external users to the chrome-less copy before the shell parent
    // is considered. Guarding must not have disturbed it.
    const externalOnly = bookingRoutes.filter((r) => (r.canMatch ?? []).length > 0);
    expect(externalOnly.length).toBe(1);
  });
});
