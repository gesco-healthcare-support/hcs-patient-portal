import { ABP, eLayoutType } from '@abp/ng.core';

export const PATIENT_BASE_ROUTES: ABP.Route[] = [
  {
    // IP6 (2026-06-05): relocated from Doctor Management to User Management
    // (Patients are now record-only users; re-pathed + re-parented).
    path: '/user-management/patients',
    iconClass: 'fas fa-file-alt',
    name: '::Menu:Patients',
    parentName: '::Menu:UserManagement',
    layout: eLayoutType.application,
    requiredPolicy: 'CaseEvaluation.Patients',
    breadcrumbText: '::Patients',
    order: 3,
  },
];
