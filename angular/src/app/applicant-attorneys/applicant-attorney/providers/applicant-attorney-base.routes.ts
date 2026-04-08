import { ABP, eLayoutType } from '@abp/ng.core';

export const APPLICANT_ATTORNEY_BASE_ROUTES: ABP.Route[] = [
  {
    path: '/applicant-attorneys',
    iconClass: 'fas fa-file-alt',
    name: '::Menu:ApplicantAttorneys',
    layout: eLayoutType.application,
    requiredPolicy: 'CaseEvaluation.ApplicantAttorneys',
    breadcrumbText: '::ApplicantAttorneys',
  },
];
