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
    // Redesign (Prompt 10, 2026-06-14): the staff queue is the redesigned
    // InternalAppointmentsComponent (chips + counts + filter drawer + bulk bar),
    // replacing the legacy NgxDatatable AppointmentComponent.
    loadComponent: () => {
      return import('./components/internal-appointments.component').then(
        (c) => c.InternalAppointmentsComponent,
      );
    },
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'view/:id',
    // Redesign (Prompt 11, 2026-06-15): internal staff detail is the redesigned
    // InternalAppointmentDetailComponent (status banner + office actions + edit
    // mode), which EXTENDS the legacy AppointmentViewComponent. The external
    // read-only detail is a separate top-level role-split route.
    loadComponent: () => {
      return import('./components/internal-appointment-detail.component').then(
        (c) => c.InternalAppointmentDetailComponent,
      );
    },
    canActivate: [authGuard],
  },
];
