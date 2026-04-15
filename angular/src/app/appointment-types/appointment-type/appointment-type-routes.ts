import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

export const APPOINTMENT_TYPE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => {
      return import('./components/appointment-type.component').then(
        (c) => c.AppointmentTypeComponent,
      );
    },
    canActivate: [authGuard, permissionGuard],
  },
];
