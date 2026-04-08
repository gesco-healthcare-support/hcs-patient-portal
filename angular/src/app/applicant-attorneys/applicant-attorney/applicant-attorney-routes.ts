import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

export const APPLICANT_ATTORNEY_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => {
      return import('./components/applicant-attorney.component').then(
        c => c.ApplicantAttorneyComponent,
      );
    },
    canActivate: [authGuard, permissionGuard],
  },
];
