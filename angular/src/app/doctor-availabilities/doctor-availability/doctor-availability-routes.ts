import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

// Prompt 14 (2026-06-15): the list now loads the redesigned week-grid view and
// generate/add load the redesigned generate-slots form. Both render inside the
// internal shell; the legacy components remain in components/ for reference.
export const DOCTOR_AVAILABILITY_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./internal-availabilities.component').then((c) => c.InternalAvailabilitiesComponent),
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'generate',
    loadComponent: () =>
      import('./internal-generate-slots.component').then((c) => c.InternalGenerateSlotsComponent),
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'add',
    loadComponent: () =>
      import('./internal-generate-slots.component').then((c) => c.InternalGenerateSlotsComponent),
    canActivate: [authGuard, permissionGuard],
  },
];
