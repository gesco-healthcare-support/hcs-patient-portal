import { ABP, eLayoutType } from '@abp/ng.core';

export const CLAIM_EXAMINER_BASE_ROUTES: ABP.Route[] = [
  {
    path: '/claim-examiners',
    iconClass: 'fas fa-file-alt',
    name: '::Menu:ClaimExaminers',
    // UM3/UM4 (2026-06-05): firm-less Claim Examiner master, listed under User
    // Management beside the attorney directories (order after AA=4, DA=5).
    parentName: '::Menu:UserManagement',
    layout: eLayoutType.application,
    requiredPolicy: 'CaseEvaluation.ClaimExaminers',
    breadcrumbText: '::ClaimExaminers',
    order: 6,
  },
];
