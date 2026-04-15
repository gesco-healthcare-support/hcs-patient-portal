import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

export const DOCTOR_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => {
      return import('./components/doctor.component').then((c) => c.DoctorComponent);
    },
    canActivate: [authGuard, permissionGuard],
  },
];
