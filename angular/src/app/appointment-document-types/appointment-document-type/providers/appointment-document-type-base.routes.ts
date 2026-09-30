import { ABP, eLayoutType } from '@abp/ng.core';

export const APPOINTMENT_DOCUMENT_TYPE_BASE_ROUTES: ABP.Route[] = [
  {
    name: '::Menu:AppointmentManagement',
    iconClass: 'fas fa-calendar-alt',
    layout: eLayoutType.application,
    order: 3,
  },
  {
    path: '/appointment-management/document-types',
    name: '::Menu:AppointmentDocumentTypes',
    iconClass: 'fas fa-folder-open',
    parentName: '::Menu:AppointmentManagement',
    requiredPolicy: 'CaseEvaluation.AppointmentDocumentTypes',
    order: 3,
  },
];
