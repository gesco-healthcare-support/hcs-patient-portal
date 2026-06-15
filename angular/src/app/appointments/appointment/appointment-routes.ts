import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

/**
 * Internal staff appointment routes, mounted inside the internal shell
 * (app.routes INTERNAL_SHELL_CHILDREN). The external read-only detail that used
 * to share view/:id here was hoisted to a top-level chrome-less route
 * (appointments/view/:id, canMatch externalUserOnlyMatchGuard) when the shell
 * landed (2026-06-14); internal staff get the legacy AppointmentViewComponent
 * below, now rendered inside the shell.
 */
export const APPOINTMENT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => {
      return import('./components/appointment.component').then((c) => c.AppointmentComponent);
    },
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'view/:id',
    loadComponent: () => {
      return import('./components/appointment-view.component').then(
        (c) => c.AppointmentViewComponent,
      );
    },
    canActivate: [authGuard],
  },
];
