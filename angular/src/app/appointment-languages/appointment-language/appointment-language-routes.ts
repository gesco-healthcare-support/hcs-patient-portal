import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

export const APPOINTMENT_LANGUAGE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => {
      return import('./components/appointment-language.component').then(
        c => c.AppointmentLanguageComponent,
      );
    },
    canActivate: [authGuard, permissionGuard],
  },
];
