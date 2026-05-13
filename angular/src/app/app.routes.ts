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
    // Phase 9 L7 (2026-05-04) -- post-login routing parity with OLD:
    // internal users (admin / Clinic Staff / Staff Supervisor / IT Admin
    // / Doctor) redirect to /dashboard; external users (Patient / AA /
    // DA / CE / Adjuster) stay on /home. The guard returns a UrlTree
    // for the redirect so this is route-level, not a flash-render of
    // home before redirect.
    canActivate: [postLoginRedirectGuard],
    loadComponent: () => import('./home/home.component').then((c) => c.HomeComponent),
  },
  {
    path: 'dashboard',
    loadComponent: () =>
      import('./dashboard/dashboard.component').then((c) => c.DashboardComponent),
    canActivate: [authGuard, permissionGuard],
  },
  // 2026-05-06: the SPA register page is dead -- the live register flow
  // lives on the AuthServer Razor page at port 44368. Redirect anyone
  // landing on /account/register on the SPA to the AuthServer URL on the
  // same subdomain so they reach the working form.
  {
    path: 'account/register',
    loadComponent: () =>
      import('./shared/auth/redirect-to-authserver-register.component').then(
        (c) => c.RedirectToAuthServerRegisterComponent,
      ),
  },
  // 2026-05-06 -- OLD-parity URL alias. OLD emailed
  // `/verify-email/{userId}?query={UUID}`; redirect such links to ABP's
  // `/account/email-confirmation?userId&confirmationToken` so legacy links
  // resolve. Token format is incompatible (OLD UUID vs NEW DataProtection)
  // so verification itself will fail for genuinely-old codes; but the
  // user-facing routing works.
  {
    path: 'verify-email/:userId',
    loadComponent: () =>
      import('./shared/auth/verify-email-redirect.component').then(
        (c) => c.VerifyEmailRedirectComponent,
      ),
  },
  // Issue 1.4 (2026-05-12) -- custom email-confirmation component that
  // surfaces a Resend Verification button. Must be declared BEFORE the
  // wildcard `path: 'account'` route below so Angular's first-match
  // precedence picks this over ABP's stock component. Reference:
  // https://docs.abp.io/en/abp/latest/UI/Angular/Component-Replacement
  {
    path: 'account/email-confirmation',
    loadComponent: () =>
      import('./shared/auth/custom-email-confirmation.component').then(
        (c) => c.CustomEmailConfirmationComponent,
      ),
  },
  {
    path: 'account',
    loadChildren: () => import('@volo/abp.ng.account/public').then((c) => c.createRoutes()),
  },
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
  // D.2 (2026-04-30): admin invite UI for external users. Backend role-based
  // gate (admin / Staff Supervisor / IT Admin) is the authoritative authz; the
  // route guard here is just authGuard. External users hitting this URL get
  // a 403 from the backend on submit -- the UI itself is harmless to render.
  {
    path: 'users/invite',
    loadComponent: () =>
      import('./external-users/components/invite-external-user.component').then(
        (c) => c.InviteExternalUserComponent,
      ),
    canActivate: [authGuard],
  },
];
