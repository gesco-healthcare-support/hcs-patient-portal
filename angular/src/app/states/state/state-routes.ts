import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

export const STATE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => {
      return import('./components/state.component').then(c => c.StateComponent);
    },
    canActivate: [authGuard, permissionGuard],
  },
];
