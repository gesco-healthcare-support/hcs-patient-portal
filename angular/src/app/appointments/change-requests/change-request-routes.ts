import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';
import { ChangeRequestType } from '../../proxy/appointment-change-requests/change-request-type.enum';
import { ChangeRequestListComponent } from './change-request-list.component';

/**
 * AP1 supervisor change-request approval pages. Gated by the
 * AppointmentChangeRequests (Default) read permission; the approve/reject
 * actions inside re-enforce `.Approve` / `.Reject` server-side. `data.changeRequestType`
 * selects which queue the shared list component renders.
 */
export const CHANGE_REQUEST_ROUTES: Routes = [
  {
    path: 'reschedules',
    component: ChangeRequestListComponent,
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'CaseEvaluation.AppointmentChangeRequests',
      changeRequestType: ChangeRequestType.Reschedule,
    },
  },
  {
    path: 'cancellations',
    component: ChangeRequestListComponent,
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'CaseEvaluation.AppointmentChangeRequests',
      changeRequestType: ChangeRequestType.Cancel,
    },
  },
];
