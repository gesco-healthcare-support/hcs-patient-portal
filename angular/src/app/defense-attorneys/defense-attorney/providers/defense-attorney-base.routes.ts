import { ABP, eLayoutType } from '@abp/ng.core';

export const DEFENSE_ATTORNEY_BASE_ROUTES: ABP.Route[] = [
  {
    path: '/defense-attorneys',
    iconClass: 'fas fa-file-alt',
    name: '::Menu:DefenseAttorneys',
    // UM3 (2026-06-05): relocated under User Management (re-parent only; URL kept
    // to avoid breaking internal bookmarks).
    parentName: '::Menu:UserManagement',
    layout: eLayoutType.application,
    requiredPolicy: 'CaseEvaluation.DefenseAttorneys',
    breadcrumbText: '::DefenseAttorneys',
    order: 5,
  },
];
