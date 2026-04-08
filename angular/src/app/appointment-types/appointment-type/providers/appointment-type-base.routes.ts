import { ABP, eLayoutType } from '@abp/ng.core';

export const APPOINTMENT_TYPE_BASE_ROUTES: ABP.Route[] = [
  {
    name: '::Menu:AppointmentManagement',
    iconClass: 'fas fa-calendar-alt',
    layout: eLayoutType.application,
    order: 3,
  },
  {
    path: '/appointment-management/appointment-types',
    name: '::Menu:AppointmentTypes',
    iconClass: 'fas fa-tags',
    parentName: '::Menu:AppointmentManagement',
    requiredPolicy: 'CaseEvaluation.AppointmentTypes',
    order: 1,
  }
];
