import { authGuard, permissionGuard } from '@abp/ng.core';
import { Routes } from '@angular/router';
import { postLoginRedirectGuard } from './shared/auth/post-login-redirect.guard';
import { GDPR_COOKIE_CONSENT_ROUTES } from './gdpr-cookie-consent/gdpr-cookie-consent.routes';
import { STATE_ROUTES } from './states/state/state-routes';
import { APPOINTMENT_TYPE_ROUTES } from './appointment-types/appointment-type/appointment-type-routes';
import { APPOINTMENT_STATUS_ROUTES } from './appointment-statuses/appointment-status/appointment-status-routes';
import { APPOINTMENT_LANGUAGE_ROUTES } from './appointment-languages/appointment-language/appointment-language-routes';
import { LOCATION_ROUTES } from './locations/location/location-routes';
import { WCAB_OFFICE_ROUTES } from './wcab-offices/wcab-office/wcab-office-routes';
import { DOCTOR_ROUTES } from './doctors/doctor/doctor-routes';
import { DOCTOR_AVAILABILITY_ROUTES } from './doctor-availabilities/doctor-availability/doctor-availability-routes';
import { PATIENT_ROUTES } from './patients/patient/patient-routes';
import { APPOINTMENT_ROUTES } from './appointments/appointment/appointment-routes';
import { AppointmentAddComponent } from './appointments/appointment-add.component';
import { APPLICANT_ATTORNEY_ROUTES } from './applicant-attorneys/applicant-attorney/applicant-attorney-routes';
import { DEFENSE_ATTORNEY_ROUTES } from './defense-attorneys/defense-attorney/defense-attorney-routes';

export const APP_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    // Issue 1.1 (2026-05-12) -- canMatch (not canActivate) so the
    // guard fires BEFORE the lazy HomeComponent chunk downloads.
    // Anonymous / internal users get redirected via UrlTree without
    // ever loading the home shell, eliminating the flash. External
    // users continue to HomeComponent at /. See
    // shared/auth/post-login-redirect.guard.ts for the three outcomes.
    canMatch: [postLoginRedirectGuard],
    loadComponent: () => import('./home/home.component').then((c) => c.HomeComponent),
  },
  {
    path: 'dashboard',
    loadComponent: () =>
      import('./dashboard/dashboard.component').then((c) => c.DashboardComponent),
    canActivate: [authGuard, permissionGuard],
  },
  // 2026-05-15 -- the SPA `/account/*` routes are gone. All
  // authentication UI (Login, Register, ConfirmUser, ForgotPassword,
  // ResetPassword, ResendVerification) is hosted by the AuthServer
  // Razor pages on port 44368. Anonymous SPA users are redirected to
  // AuthServer by the OAuth challenge; authenticated SPA users have
  // no business landing on /account/* pages anyway. Manage-profile +
  // change-password live on AuthServer Razor (/Account/Manage) too.
  // Keep the proxy types under angular/src/app/proxy/account/ -- the
  // identity / saas / openiddict feature modules still depend on the
  // shared DTOs from that package.
  {
    path: 'gdpr',
    loadChildren: () => import('@volo/abp.ng.gdpr').then((c) => c.createRoutes()),
  },
  {
    path: 'identity',
    loadChildren: () => import('@volo/abp.ng.identity').then((c) => c.createRoutes()),
  },
  {
    path: 'language-management',
    loadChildren: () => import('@volo/abp.ng.language-management').then((c) => c.createRoutes()),
  },
  {
    path: 'saas',
    loadChildren: () => import('@volo/abp.ng.saas').then((c) => c.createRoutes()),
  },
  {
    path: 'audit-logs',
    loadChildren: () => import('@volo/abp.ng.audit-logging').then((c) => c.createRoutes()),
  },
  {
    path: 'openiddict',
    loadChildren: () => import('@volo/abp.ng.openiddictpro').then((c) => c.createRoutes()),
  },
  {
    path: 'text-template-management',
    loadChildren: () =>
      import('@volo/abp.ng.text-template-management').then((c) => c.createRoutes()),
  },
  {
    path: 'file-management',
    loadChildren: () => import('@volo/abp.ng.file-management').then((c) => c.createRoutes()),
  },
  {
    path: 'gdpr-cookie-consent',
    children: GDPR_COOKIE_CONSENT_ROUTES,
  },
  {
    path: 'setting-management',
    loadChildren: () => import('@abp/ng.setting-management').then((c) => c.createRoutes()),
  },
  { path: 'configurations/states', children: STATE_ROUTES },
  { path: 'appointment-management/appointment-types', children: APPOINTMENT_TYPE_ROUTES },
  { path: 'appointment-management/appointment-statuses', children: APPOINTMENT_STATUS_ROUTES },
  { path: 'appointment-management/appointment-languages', children: APPOINTMENT_LANGUAGE_ROUTES },
  { path: 'appointments', children: APPOINTMENT_ROUTES },
  { path: 'doctor-management/locations', children: LOCATION_ROUTES },
  { path: 'doctor-management/wcab-offices', children: WCAB_OFFICE_ROUTES },
  { path: 'doctor-management/doctors', children: DOCTOR_ROUTES },
  {
    path: 'doctor-management/doctor-availabilities/generate',
    loadComponent: () =>
      import('./doctor-availabilities/doctor-availability/components/doctor-availability-generate.component').then(
        (c) => c.DoctorAvailabilityGenerateComponent,
      ),
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'doctor-management/doctor-availabilities/add',
    loadComponent: () =>
      import('./doctor-availabilities/doctor-availability/components/doctor-availability-generate.component').then(
        (c) => c.DoctorAvailabilityGenerateComponent,
      ),
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'appointments/add',
    loadComponent: () => Promise.resolve(AppointmentAddComponent),
    canActivate: [authGuard],
  },
  {
    path: 'appointments/view/:id/change-log',
    loadComponent: () =>
      import('./appointments/appointment-change-logs/appointment-change-logs.component').then(
        (c) => c.AppointmentChangeLogsComponent,
      ),
    canActivate: [authGuard, permissionGuard],
    data: { requiredPolicy: 'CaseEvaluation.AppointmentChangeLogs' },
  },
  {
    path: 'doctor-management/patients/my-profile',
    loadComponent: () =>
      import('./patients/patient/components/patient-profile.component').then(
        (c) => c.PatientProfileComponent,
      ),
    canActivate: [authGuard],
  },
  { path: 'doctor-management/doctor-availabilities', children: DOCTOR_AVAILABILITY_ROUTES },
  { path: 'doctor-management/patients', children: PATIENT_ROUTES },
  { path: 'applicant-attorneys', children: APPLICANT_ATTORNEY_ROUTES },
  { path: 'defense-attorneys', children: DEFENSE_ATTORNEY_ROUTES },
  // 2026-05-15 -- admin invite UI for external users. Gated by the
  // CaseEvaluation.UserManagement.InviteExternalUser permission so
  // external roles get a 403 page instead of seeing the form;
  // server-side AppService re-enforces the same permission.
  {
    path: 'users/invite',
    loadComponent: () =>
      import('./external-users/components/invite-external-user.component').then(
        (c) => c.InviteExternalUserComponent,
      ),
    canActivate: [authGuard, permissionGuard],
    data: { requiredPolicy: 'CaseEvaluation.UserManagement.InviteExternalUser' },
  },
];
