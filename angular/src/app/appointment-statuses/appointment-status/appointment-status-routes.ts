import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

export const APPOINTMENT_STATUS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => {
      return import('./components/appointment-status.component').then(
        c => c.AppointmentStatusComponent,
      );
    },
    canActivate: [authGuard, permissionGuard],
  },
];
