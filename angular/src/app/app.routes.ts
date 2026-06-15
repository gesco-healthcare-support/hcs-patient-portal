import { authGuard, permissionGuard } from '@abp/ng.core';
import { Routes } from '@angular/router';
import { postLoginRedirectGuard } from './shared/auth/post-login-redirect.guard';
import { internalUserOnlyMatchGuard } from './shared/auth/internal-user-match.guard';
import { externalUserOnlyMatchGuard } from './shared/auth/external-user-match.guard';
import { InternalShellLayoutComponent } from './shared/components/internal-shell/internal-shell-layout.component';
import { GDPR_COOKIE_CONSENT_ROUTES } from './gdpr-cookie-consent/gdpr-cookie-consent.routes';
import { STATE_ROUTES } from './states/state/state-routes';
import { APPOINTMENT_TYPE_ROUTES } from './appointment-types/appointment-type/appointment-type-routes';
import { APPOINTMENT_STATUS_ROUTES } from './appointment-statuses/appointment-status/appointment-status-routes';
import { APPOINTMENT_DOCUMENT_TYPE_ROUTES } from './appointment-document-types/appointment-document-type/appointment-document-type-routes';
import { APPOINTMENT_LANGUAGE_ROUTES } from './appointment-languages/appointment-language/appointment-language-routes';
import { LOCATION_ROUTES } from './locations/location/location-routes';
import { WCAB_OFFICE_ROUTES } from './wcab-offices/wcab-office/wcab-office-routes';
import { DOCTOR_ROUTES } from './doctors/doctor/doctor-routes';
import { DOCTOR_AVAILABILITY_ROUTES } from './doctor-availabilities/doctor-availability/doctor-availability-routes';
import { PATIENT_ROUTES } from './patients/patient/patient-routes';
import { APPOINTMENT_ROUTES } from './appointments/appointment/appointment-routes';
import { CHANGE_REQUEST_ROUTES } from './appointments/change-requests/change-request-routes';
import { AppointmentAddComponent } from './appointments/appointment-add.component';
import { APPLICANT_ATTORNEY_ROUTES } from './applicant-attorneys/applicant-attorney/applicant-attorney-routes';
import { DEFENSE_ATTORNEY_ROUTES } from './defense-attorneys/defense-attorney/defense-attorney-routes';
import { CLAIM_EXAMINER_ROUTES } from './claim-examiners/claim-examiner/claim-examiner-routes';

/**
 * Internal staff routes wrapped by the redesigned shell (navy sidebar +
 * topbar). Mounted as the children of a canMatch-gated parent below, so they
 * render inside InternalShellLayoutComponent for staff. Every path + guard is
 * unchanged from when these lived at the top level -- "wrap in place"
 * (2026-06-14 decision) keeps all existing URLs.
 *
 * Intentionally EXCLUDED (kept outside the shell, declared in APP_ROUTES):
 *   - external appointment pages, role-split out to top-level chrome-less
 *     routes: the read-only detail (appointments/view/:id) and the legacy add
 *     form (external books via ?type=1/2), plus the external booking wizard
 *     (appointments/request);
 *   - gdpr / gdpr-cookie-consent (anonymous / data-subject facing);
 *   - user-management/patients/my-profile (external patient page).
 */
const INTERNAL_SHELL_CHILDREN: Routes = [
  {
    path: 'dashboard',
    loadComponent: () =>
      import('./dashboard/internal-dashboard.component').then((c) => c.InternalDashboardComponent),
    canActivate: [authGuard, permissionGuard],
  },
  // Internal appointment views. The external equivalents (read-only detail at
  // view/:id, and the legacy add form reached by external booking) are role-split
  // out to top-level chrome-less routes in APP_ROUTES; these in-shell copies serve
  // internal staff so the sidebar persists. APPOINTMENT_ROUTES holds the staff
  // list ('') + legacy AppointmentViewComponent (view/:id).
  { path: 'appointments', children: APPOINTMENT_ROUTES },
  { path: 'appointments/change-requests', children: CHANGE_REQUEST_ROUTES },
  {
    // Redesign (2026-06-15): internal staff book via the redesigned wizard,
    // wrapped by the shell. The wizard suppresses its own external navbar for
    // internal bookers (isInternalBooker) so only the shell chrome shows.
    // authGuard only (no permissionGuard), matching the prior in-shell add route.
    path: 'appointments/add',
    loadComponent: () =>
      import('./appointments/wizard/appointment-wizard.component').then(
        (c) => c.AppointmentWizardComponent,
      ),
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
    path: 'setting-management',
    loadChildren: () => import('@abp/ng.setting-management').then((c) => c.createRoutes()),
  },
  { path: 'configurations/states', children: STATE_ROUTES },
  { path: 'appointment-management/appointment-types', children: APPOINTMENT_TYPE_ROUTES },
  { path: 'appointment-management/appointment-statuses', children: APPOINTMENT_STATUS_ROUTES },
  { path: 'appointment-management/document-types', children: APPOINTMENT_DOCUMENT_TYPE_ROUTES },
  { path: 'appointment-management/appointment-languages', children: APPOINTMENT_LANGUAGE_ROUTES },
  { path: 'doctor-management/locations', children: LOCATION_ROUTES },
  { path: 'doctor-management/wcab-offices', children: WCAB_OFFICE_ROUTES },
  { path: 'doctor-management/doctors', children: DOCTOR_ROUTES },
  {
    path: 'doctor-management/doctor-availabilities/generate',
    loadComponent: () =>
      import('./doctor-availabilities/doctor-availability/internal-generate-slots.component').then(
        (c) => c.InternalGenerateSlotsComponent,
      ),
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'doctor-management/doctor-availabilities/add',
    loadComponent: () =>
      import('./doctor-availabilities/doctor-availability/internal-generate-slots.component').then(
        (c) => c.InternalGenerateSlotsComponent,
      ),
    canActivate: [authGuard, permissionGuard],
  },
  { path: 'doctor-management/doctor-availabilities', children: DOCTOR_AVAILABILITY_ROUTES },
  {
    path: 'appointment-change-logs',
    loadComponent: () =>
      import('./appointment-change-logs/appointment-change-log-list.component').then(
        (c) => c.AppointmentChangeLogListComponent,
      ),
    canActivate: [authGuard, permissionGuard],
    data: { requiredPolicy: 'CaseEvaluation.AppointmentChangeLogs' },
  },
  {
    path: 'reports',
    loadComponent: () =>
      import('./reports/appointment-report.component').then((c) => c.AppointmentReportComponent),
    canActivate: [authGuard, permissionGuard],
    data: { requiredPolicy: 'CaseEvaluation.Reports' },
  },
  { path: 'user-management/patients', children: PATIENT_ROUTES },
  { path: 'applicant-attorneys', children: APPLICANT_ATTORNEY_ROUTES },
  { path: 'defense-attorneys', children: DEFENSE_ATTORNEY_ROUTES },
  { path: 'claim-examiners', children: CLAIM_EXAMINER_ROUTES },
  // 2026-05-15 -- admin invite UI for external users. Gated by the
  // CaseEvaluation.UserManagement.InviteExternalUser permission so external
  // roles get a 403 page instead of the form; server-side re-enforced.
  {
    path: 'users/invite',
    loadComponent: () =>
      import('./external-users/components/invite-external-user.component').then(
        (c) => c.InviteExternalUserComponent,
      ),
    canActivate: [authGuard, permissionGuard],
    data: { requiredPolicy: 'CaseEvaluation.UserManagement.InviteExternalUser' },
  },
  // 2026-05-15 -- IT Admin internal-user creation. Gated by the
  // CaseEvaluation.InternalUsers.Create permission (host-scoped); the
  // AppService re-validates server-side.
  {
    path: 'internal-users',
    loadComponent: () =>
      import('./internal-users/components/internal-users-form.component').then(
        (c) => c.InternalUsersFormComponent,
      ),
    canActivate: [authGuard, permissionGuard],
    data: { requiredPolicy: 'CaseEvaluation.InternalUsers.Create' },
  },
];

export const APP_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    // Issue 1.1 (2026-05-12) -- canMatch (not canActivate) so the
    // guard fires BEFORE the lazy ExternalHomeComponent chunk downloads.
    // Anonymous / internal users get redirected via UrlTree without
    // ever loading the home shell, eliminating the flash. External
    // users continue to ExternalHomeComponent at /. See
    // shared/auth/post-login-redirect.guard.ts for the three outcomes.
    canMatch: [postLoginRedirectGuard],
    loadComponent: () =>
      import('./home/external-home.component').then((c) => c.ExternalHomeComponent),
  },
  {
    // PR4 -- public, no-login document upload reached by a per-document
    // verification-code link. Intentionally guard-free (NO canMatch /
    // canActivate) so an anonymous patient reaches it without an OAuth
    // redirect. The page renders chrome-less because the app uses a bare
    // router-outlet (no LeptonX layout); it authorizes via the code, and the
    // backend endpoint is [AllowAnonymous] + per-code rate-limited.
    path: 'public/document-upload/:id/:verificationCode',
    loadComponent: () =>
      import('./public-document-upload/public-document-upload.component').then(
        (c) => c.PublicDocumentUploadComponent,
      ),
  },
  {
    // Group D (2026-06-09) -- public, no-login opposing-side consent page reached by
    // the single-use token link in the consent email. Guard-free + chrome-less
    // (bare router-outlet, no LeptonX layout), same as document-upload; authorizes
    // via the token.
    path: 'public/change-request-consent/:token',
    loadComponent: () =>
      import('./public-change-request-consent/public-change-request-consent.component').then(
        (c) => c.PublicChangeRequestConsentComponent,
      ),
  },
  // 2026-05-15 -- the SPA `/account/*` routes are gone. All authentication UI
  // (Login, Register, ConfirmUser, ForgotPassword, ResetPassword,
  // ResendVerification) is hosted by the AuthServer Razor pages on port 44368.
  // Manage-profile + change-password live on AuthServer Razor (/Account/Manage)
  // too. Keep the proxy types under angular/src/app/proxy/account/ -- the
  // identity / saas / openiddict feature modules still depend on those DTOs.
  {
    path: 'gdpr',
    loadChildren: () => import('@volo/abp.ng.gdpr').then((c) => c.createRoutes()),
  },
  {
    path: 'gdpr-cookie-consent',
    children: GDPR_COOKIE_CONSENT_ROUTES,
  },
  // ---- External + shared appointment routes (OUTSIDE the shell). Declared
  // before the shell parent so they win for external users; internal staff fail
  // the externalUserOnlyMatchGuard and fall through to the in-shell copies in
  // INTERNAL_SHELL_CHILDREN. canMatch fires before the lazy chunk loads, so each
  // role only downloads its own bundle. ----
  {
    // External read-only detail at the canonical view/:id; internal staff fall
    // through to the in-shell legacy AppointmentViewComponent (APPOINTMENT_ROUTES).
    path: 'appointments/view/:id',
    canMatch: [externalUserOnlyMatchGuard],
    loadComponent: () =>
      import('./appointments/appointment/components/external-appointment-detail.component').then(
        (c) => c.ExternalAppointmentDetailComponent,
      ),
    canActivate: [authGuard],
  },
  {
    // Legacy add form, reached by external booking (external-home -> ?type=1/2).
    // Role-split so external books chrome-less while internal staff get the
    // in-shell copy (INTERNAL_SHELL_CHILDREN) and keep the sidebar.
    path: 'appointments/add',
    canMatch: [externalUserOnlyMatchGuard],
    loadComponent: () => Promise.resolve(AppointmentAddComponent),
    canActivate: [authGuard],
  },
  {
    // Redesign (temp, 2026-06-13): the new external booking wizard. External-only
    // as of 2026-06-15 (externalUserOnlyMatchGuard) -- internal staff book the
    // same wizard at the in-shell /appointments/add, so they must never land on
    // this chrome-less route; they fall through to the shell instead.
    path: 'appointments/request',
    canMatch: [externalUserOnlyMatchGuard],
    loadComponent: () =>
      import('./appointments/wizard/appointment-wizard.component').then(
        (c) => c.AppointmentWizardComponent,
      ),
    canActivate: [authGuard],
  },
  {
    // Redesign swap (2026-06-14): my-profile serves the reworked page.
    // PatientProfileRedesignComponent EXTENDS PatientProfileComponent. It is
    // external/patient-only, so it stays outside the internal shell.
    path: 'user-management/patients/my-profile',
    loadComponent: () =>
      import('./patients/patient/components/patient-profile-redesign.component').then(
        (c) => c.PatientProfileRedesignComponent,
      ),
    canActivate: [authGuard],
  },
  {
    // Redesign internal shell (2026-06-14): wrap the internal staff routes in the
    // navy sidebar + topbar. canMatch (internalUserOnlyMatchGuard) renders the
    // shell only for staff + anonymous; pure-external users fall through to the
    // external routes above. Anonymous users match here so the children's
    // authGuard issues the OAuth challenge (rather than hitting the wildcard).
    path: '',
    component: InternalShellLayoutComponent,
    canMatch: [internalUserOnlyMatchGuard],
    canActivate: [authGuard],
    children: INTERNAL_SHELL_CHILDREN,
  },
  // Redesign (2026-06-14): catch-all 404. MUST stay last so the routes above
  // match first. Renders chrome-less (bare router-outlet) so the branded
  // NotFound card looks identical for external and internal users. API 404s are
  // handled separately by AppHttpErrorComponent.
  {
    path: '**',
    loadComponent: () =>
      import('./shared/ui/not-found/not-found.component').then((c) => c.NotFoundComponent),
  },
];
