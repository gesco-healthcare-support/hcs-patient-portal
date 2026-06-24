import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

export const APPOINTMENT_DOCUMENT_TYPE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => {
      return import('./components/appointment-document-type.component').then(
        (c) => c.AppointmentDocumentTypeComponent,
      );
    },
    canActivate: [authGuard, permissionGuard],
  },
];
