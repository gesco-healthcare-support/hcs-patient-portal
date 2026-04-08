import { ABP, eLayoutType } from '@abp/ng.core';

export const WCAB_OFFICE_BASE_ROUTES: ABP.Route[] = [
  {
    name: '::Menu:DoctorManagement',
    iconClass: 'fas fa-user-md',
    layout: eLayoutType.application,
    order: 5,
  },
  {
    path: '/doctor-management/wcab-offices',
    name: '::Menu:WcabOffices',
    parentName: '::Menu:DoctorManagement',
    layout: eLayoutType.application,
    requiredPolicy: 'CaseEvaluation.WcabOffices',
    breadcrumbText: '::WcabOffices',
    order: 2,
  },
];
