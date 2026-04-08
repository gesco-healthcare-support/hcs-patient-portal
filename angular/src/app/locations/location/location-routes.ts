import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

export const LOCATION_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => {
      return import('./components/location.component').then(c => c.LocationComponent);
    },
    canActivate: [authGuard, permissionGuard],
  },
];
