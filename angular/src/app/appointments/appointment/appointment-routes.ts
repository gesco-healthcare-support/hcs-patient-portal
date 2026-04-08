import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

export const APPOINTMENT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => {
      return import('./components/appointment.component').then(c => c.AppointmentComponent);
    },
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'view/:id',
    loadComponent: () => {
      return import('./components/appointment-view.component').then(c => c.AppointmentViewComponent);
    },
    canActivate: [authGuard],
  },
];
