import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

// Prompt 14 (2026-06-15): redesigned locations list + modal CRUD inside the
// internal shell. The legacy LocationComponent remains in components/.
export const LOCATION_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./internal-locations.component').then((c) => c.InternalLocationsComponent),
    canActivate: [authGuard, permissionGuard],
  },
];
