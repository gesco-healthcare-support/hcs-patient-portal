import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

export const WCAB_OFFICE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => {
      return import('./components/wcab-office.component').then(c => c.WcabOfficeComponent);
    },
    canActivate: [authGuard, permissionGuard],
  },
];
