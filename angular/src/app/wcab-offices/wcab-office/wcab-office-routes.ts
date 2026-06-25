import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

// Prompt 14 (2026-06-15): redesigned WCAB offices list + modal CRUD inside the
// internal shell. The legacy WcabOfficeComponent remains in components/.
export const WCAB_OFFICE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./internal-wcab-offices.component').then((c) => c.InternalWcabOfficesComponent),
    canActivate: [authGuard, permissionGuard],
  },
];
