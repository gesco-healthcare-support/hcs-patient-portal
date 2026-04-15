import { ABP, eLayoutType } from '@abp/ng.core';

export const APPOINTMENT_STATUS_BASE_ROUTES: ABP.Route[] = [
  {
    name: '::Menu:AppointmentManagement',
    iconClass: 'fas fa-calendar-alt',
    layout: eLayoutType.application,
    order: 3,
  },
  {
    path: '/appointment-management/appointment-statuses',
    name: '::Menu:AppointmentStatuses',
    iconClass: 'fas fa-traffic-light',
    parentName: '::Menu:AppointmentManagement',
    requiredPolicy: 'CaseEvaluation.AppointmentStatuses',
    order: 2,
  },
];
