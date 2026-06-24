import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';
import { InternalChangeRequestInboxComponent } from './internal-change-request-inbox.component';

/**
 * Supervisor change-request approval. Redesign (Prompt 13, 2026-06-15): the two
 * legacy per-type Bootstrap tables (reschedules / cancellations) are unified into
 * one tabbed inbox at the parent path; the old per-type paths redirect into it so
 * existing deep links keep working. Gated by the AppointmentChangeRequests
 * (Default) read permission; approve/reject re-enforce `.Approve` / `.Reject`
 * server-side.
 */
export const CHANGE_REQUEST_ROUTES: Routes = [
  {
    path: '',
    component: InternalChangeRequestInboxComponent,
    canActivate: [authGuard, permissionGuard],
    data: { requiredPolicy: 'CaseEvaluation.AppointmentChangeRequests' },
  },
  { path: 'reschedules', redirectTo: '', pathMatch: 'full' },
  { path: 'cancellations', redirectTo: '', pathMatch: 'full' },
];
