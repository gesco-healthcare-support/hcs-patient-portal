import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

export const PATIENT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => {
      return import('./components/patient.component').then((c) => c.PatientComponent);
    },
    canActivate: [authGuard, permissionGuard],
  },
];
