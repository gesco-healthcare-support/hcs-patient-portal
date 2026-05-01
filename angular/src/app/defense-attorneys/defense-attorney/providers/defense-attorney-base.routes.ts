import { ABP, eLayoutType } from '@abp/ng.core';

export const DEFENSE_ATTORNEY_BASE_ROUTES: ABP.Route[] = [
  {
    path: '/defense-attorneys',
    iconClass: 'fas fa-file-alt',
    name: '::Menu:DefenseAttorneys',
    layout: eLayoutType.application,
    requiredPolicy: 'CaseEvaluation.DefenseAttorneys',
    breadcrumbText: '::DefenseAttorneys',
  },
];
