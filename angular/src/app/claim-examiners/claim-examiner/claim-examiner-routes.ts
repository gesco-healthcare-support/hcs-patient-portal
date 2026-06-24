import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

export const CLAIM_EXAMINER_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => {
      return import('./components/claim-examiner.component').then((c) => c.ClaimExaminerComponent);
    },
    canActivate: [authGuard, permissionGuard],
  },
];
