import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';

export const STATE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => {
      return import('../../configuration/internal-configuration.component').then(
        (c) => c.InternalConfigurationComponent,
      );
    },
    canActivate: [authGuard, permissionGuard],
    data: { section: 'states', requiredPolicy: 'CaseEvaluation.States' },
  },
];
