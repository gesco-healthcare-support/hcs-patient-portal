import { ABP, eLayoutType } from '@abp/ng.core';

export const APPOINTMENT_LANGUAGE_BASE_ROUTES: ABP.Route[] = [
  {
    name: '::Menu:AppointmentManagement',
    iconClass: 'fas fa-calendar-alt',
    layout: eLayoutType.application,
    order: 3,
  },
  {
    path: '/appointment-management/appointment-languages',
    name: '::Menu:AppointmentLanguages',
    iconClass: 'fas fa-language',
    parentName: '::Menu:AppointmentManagement',
    requiredPolicy: 'CaseEvaluation.AppointmentLanguages',
    order: 3,
  }
];
