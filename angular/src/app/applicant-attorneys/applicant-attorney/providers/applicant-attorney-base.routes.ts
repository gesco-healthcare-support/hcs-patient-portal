import { ABP, eLayoutType } from '@abp/ng.core';

export const APPLICANT_ATTORNEY_BASE_ROUTES: ABP.Route[] = [
  {
    path: '/applicant-attorneys',
    iconClass: 'fas fa-file-alt',
    name: '::Menu:ApplicantAttorneys',
    // UM3 (2026-06-05): relocated under User Management (re-parent only; URL kept
    // to avoid breaking internal bookmarks).
    parentName: '::Menu:UserManagement',
    layout: eLayoutType.application,
    requiredPolicy: 'CaseEvaluation.ApplicantAttorneys',
    breadcrumbText: '::ApplicantAttorneys',
    order: 4,
  },
];
