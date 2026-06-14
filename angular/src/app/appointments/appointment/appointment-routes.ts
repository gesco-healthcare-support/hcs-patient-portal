import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';
import { externalUserOnlyMatchGuard } from '../../shared/auth/external-user-match.guard';

export const APPOINTMENT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => {
      return import('./components/appointment.component').then((c) => c.AppointmentComponent);
    },
    canActivate: [authGuard, permissionGuard],
  },
  {
    // Redesign swap (2026-06-14): external users get the reworked read-only
    // detail at the canonical view/:id. Internal staff fall through to the
    // legacy AppointmentViewComponent below (the internal detail rework is
    // Prompt 11, not built). ExternalAppointmentDetailComponent EXTENDS
    // AppointmentViewComponent, so the legacy class must stay either way.
    // canMatch is evaluated before the lazy chunk loads, so internal users
    // never download the external bundle.
    path: 'view/:id',
    canMatch: [externalUserOnlyMatchGuard],
    loadComponent: () => {
      return import('./components/external-appointment-detail.component').then(
        (c) => c.ExternalAppointmentDetailComponent,
      );
    },
    canActivate: [authGuard],
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
