import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

export const DEFENSE_ATTORNEY_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => {
      return import('./components/defense-attorney.component').then(
        (c) => c.DefenseAttorneyComponent,
      );
    },
    canActivate: [authGuard, permissionGuard],
  },
];
