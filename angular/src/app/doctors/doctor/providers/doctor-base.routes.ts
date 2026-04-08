import { ABP, eLayoutType } from '@abp/ng.core';

export const DOCTOR_BASE_ROUTES: ABP.Route[] = [
  {
    path: '/doctor-management/doctors',
    iconClass: 'fas fa-user',
    name: '::Menu:Doctors',
    parentName: '::Menu:DoctorManagement',
    layout: eLayoutType.application,
    requiredPolicy: 'CaseEvaluation.Doctors',
    breadcrumbText: '::Doctors',
    order: 1,
  },
];
