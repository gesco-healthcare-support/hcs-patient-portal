import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

export const CLAIM_EXAMINER_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => {
      return import('../../people/internal-people.component').then(
        (c) => c.InternalPeopleComponent,
      );
    },
    canActivate: [authGuard, permissionGuard],
    data: { section: 'ce', requiredPolicy: 'CaseEvaluation.ClaimExaminers' },
  },
];
