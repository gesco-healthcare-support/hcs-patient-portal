import { ABP, eLayoutType } from '@abp/ng.core';

/**
 * AP1 supervisor nav entries for the two change-request approval queues. Both
 * are gated on the AppointmentChangeRequests (Default) read permission, so only
 * roles granted the inbox (Staff Supervisor / IT Admin) see them.
 */
export const CHANGE_REQUEST_BASE_ROUTES: ABP.Route[] = [
  {
    path: '/appointments/change-requests/reschedules',
    iconClass: 'fas fa-calendar-alt',
    name: '::ChangeRequest:PendingReschedules',
    layout: eLayoutType.application,
    requiredPolicy: 'CaseEvaluation.AppointmentChangeRequests',
    order: 3,
  },
  {
    path: '/appointments/change-requests/cancellations',
    iconClass: 'fas fa-calendar-times',
    name: '::ChangeRequest:PendingCancellations',
    layout: eLayoutType.application,
    requiredPolicy: 'CaseEvaluation.AppointmentChangeRequests',
    order: 4,
  },
];
