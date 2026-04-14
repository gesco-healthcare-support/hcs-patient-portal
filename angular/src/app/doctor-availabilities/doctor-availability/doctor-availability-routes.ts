import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

export const DOCTOR_AVAILABILITY_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => {
      return import('./components/doctor-availability.component').then(
        (c) => c.DoctorAvailabilityComponent,
      );
    },
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'generate',
    loadComponent: () => {
      return import('./components/doctor-availability-generate.component').then(
        (c) => c.DoctorAvailabilityGenerateComponent,
      );
    },
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'add',
    loadComponent: () => {
      return import('./components/doctor-availability-generate.component').then(
        (c) => c.DoctorAvailabilityGenerateComponent,
      );
    },
    canActivate: [authGuard, permissionGuard],
  },
];
