import { ABP, eLayoutType } from '@abp/ng.core';

export const PATIENT_BASE_ROUTES: ABP.Route[] = [
  {
    path: '/doctor-management/patients',
    iconClass: 'fas fa-file-alt',
    name: '::Menu:Patients',
    parentName: '::Menu:DoctorManagement',
    layout: eLayoutType.application,
    requiredPolicy: 'CaseEvaluation.Patients',
    breadcrumbText: '::Patients',
    order: 4,
  },
];
