import { ABP, eLayoutType } from '@abp/ng.core';

export const APPOINTMENT_BASE_ROUTES: ABP.Route[] = [
  {
    path: '/appointments',
    iconClass: 'fas fa-file-alt',
    name: '::Menu:Appointments',
    layout: eLayoutType.application,
    requiredPolicy: 'CaseEvaluation.Appointments',
    breadcrumbText: '::Appointments',
  },
];
