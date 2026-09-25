import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

export const APPLICANT_ATTORNEY_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => {
      return import('../../people/internal-people.component').then(
        (c) => c.InternalPeopleComponent,
      );
    },
    canActivate: [authGuard, permissionGuard],
    data: { section: 'aa', requiredPolicy: 'CaseEvaluation.ApplicantAttorneys' },
  },
];
