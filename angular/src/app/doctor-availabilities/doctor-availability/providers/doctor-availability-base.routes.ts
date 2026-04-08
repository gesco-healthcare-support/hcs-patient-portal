import { ABP, eLayoutType } from '@abp/ng.core';

export const DOCTOR_AVAILABILITY_BASE_ROUTES: ABP.Route[] = [
  {
    path: '/doctor-management/doctor-availabilities',
    iconClass: 'fas fa-calendar-check',
    name: '::Menu:DoctorAvailabilities',
    parentName: '::Menu:DoctorManagement',
    layout: eLayoutType.application,
    requiredPolicy: 'CaseEvaluation.DoctorAvailabilities',
    breadcrumbText: '::DoctorAvailabilities',
    order: 3,
  },
];
