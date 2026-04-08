import { ABP, eLayoutType } from '@abp/ng.core';

export const DOCTOR_MANAGEMENT_BASE_ROUTES: ABP.Route[] = [
  {
    name: '::Menu:DoctorManagement',
    iconClass: 'fas fa-user-md',
    layout: eLayoutType.application,
    order: 5,
  },
  {
    path: '/doctor-management/locations',
    iconClass: 'fas fa-map-marker-alt',
    name: '::Menu:Locations',
    parentName: '::Menu:DoctorManagement',
    layout: eLayoutType.application,
    requiredPolicy: 'CaseEvaluation.Locations',
    breadcrumbText: '::Locations',
    order: 1,
  },
  {
    path: '/doctor-management/wcab-offices',
    iconClass: 'fas fa-building',
    name: '::Menu:WcabOffices',
    parentName: '::Menu:DoctorManagement',
    layout: eLayoutType.application,
    requiredPolicy: 'CaseEvaluation.WcabOffices',
    breadcrumbText: '::WcabOffices',
    order: 2,
  },
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
