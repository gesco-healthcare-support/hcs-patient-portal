import { ABP, eLayoutType } from '@abp/ng.core';

export const LOCATION_BASE_ROUTES: ABP.Route[] = [
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
    order: 2,
  },
];
