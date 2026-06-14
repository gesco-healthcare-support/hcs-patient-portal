import {
  CHECK_AUTHENTICATION_STATE_FN_KEY,
  ConfigStateService,
  provideAbpCore,
  withOptions,
} from '@abp/ng.core';
import { provideAppInitializer } from '@angular/core';
import { clearOAuthStorage, provideAbpOAuth } from '@abp/ng.oauth';
import { provideSettingManagementConfig } from '@abp/ng.setting-management/config';
import { provideFeatureManagementConfig } from '@abp/ng.feature-management';
import {
  provideAbpThemeShared,
  provideLogo,
  withEnvironmentOptions,
  withHttpErrorConfig,
  withValidationBluePrint,
} from '@abp/ng.theme.shared';
import { provideIdentityConfig } from '@volo/abp.ng.identity/config';
import { provideCommercialUiConfig } from '@volo/abp.commercial.ng.ui/config';
import { provideAccountAdminConfig } from '@volo/abp.ng.account/admin/config';
import { provideGdprConfig, withCookieConsentOptions } from '@volo/abp.ng.gdpr/config';
import { provideAuditLoggingConfig } from '@volo/abp.ng.audit-logging/config';
import { provideLanguageManagementConfig } from '@volo/abp.ng.language-management/config';
import { registerLocale } from '@volo/abp.ng.language-management/locale';
import { provideFileManagementConfig } from '@volo/abp.ng.file-management/config';
import { provideSaasConfig } from '@volo/abp.ng.saas/config';
import { provideTextTemplateManagementConfig } from '@volo/abp.ng.text-template-management/config';
import { provideOpeniddictproConfig } from '@volo/abp.ng.openiddictpro/config';
import { provideThemeLeptonX, withThemeLeptonXOptions } from '@volosoft/abp.ng.theme.lepton-x';
import { provideNgxMask } from 'ngx-mask';
import { RxReactiveFormsModule } from '@rxweb/reactive-form-validators';
import { provideSideMenuLayout } from '@volosoft/abp.ng.theme.lepton-x/layouts';
import { ApplicationConfig, Injector, importProvidersFrom, inject } from '@angular/core';
import { HttpBackend, HttpClient } from '@angular/common/http';
import { OAuthService } from 'angular-oauth2-oidc';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { environment, addressValidation } from '../environments/environment';
import { APP_ROUTES } from './app.routes';
import { AppHttpErrorComponent } from './shared/ui/state-message/app-http-error.component';
import { APP_ROUTE_PROVIDER } from './route.provider';
import { STATES_STATE_ROUTE_PROVIDER } from './states/state/providers/state-route.provider';
import { APPOINTMENT_TYPES_APPOINTMENT_TYPE_ROUTE_PROVIDER } from './appointment-types/appointment-type/providers/appointment-type-route.provider';
import { APPOINTMENT_STATUSES_APPOINTMENT_STATUS_ROUTE_PROVIDER } from './appointment-statuses/appointment-status/providers/appointment-status-route.provider';
import { APPOINTMENT_DOCUMENT_TYPES_APPOINTMENT_DOCUMENT_TYPE_ROUTE_PROVIDER } from './appointment-document-types/appointment-document-type/providers/appointment-document-type-route.provider';
import { APPOINTMENT_LANGUAGES_APPOINTMENT_LANGUAGE_ROUTE_PROVIDER } from './appointment-languages/appointment-language/providers/appointment-language-route.provider';
import { DOCTOR_MANAGEMENT_ROUTE_PROVIDER } from './doctor-management/providers/doctor-management-route.provider';
import { LOCATIONS_LOCATION_ROUTE_PROVIDER } from './locations/location/providers/location-route.provider';
import { DOCTORS_DOCTOR_ROUTE_PROVIDER } from './doctors/doctor/providers/doctor-route.provider';
import { DOCTOR_AVAILABILITIES_DOCTOR_AVAILABILITY_ROUTE_PROVIDER } from './doctor-availabilities/doctor-availability/providers/doctor-availability-route.provider';
import { PATIENTS_PATIENT_ROUTE_PROVIDER } from './patients/patient/providers/patient-route.provider';
import { APPOINTMENTS_APPOINTMENT_ROUTE_PROVIDER } from './appointments/appointment/providers/appointment-route.provider';
import { APPOINTMENTS_CHANGE_REQUEST_ROUTE_PROVIDER } from './appointments/change-requests/providers/change-request-route.provider';
import { APPLICANT_ATTORNEYS_APPLICANT_ATTORNEY_ROUTE_PROVIDER } from './applicant-attorneys/applicant-attorney/providers/applicant-attorney-route.provider';
import { DEFENSE_ATTORNEYS_DEFENSE_ATTORNEY_ROUTE_PROVIDER } from './defense-attorneys/defense-attorney/providers/defense-attorney-route.provider';
import { CLAIM_EXAMINERS_CLAIM_EXAMINER_ROUTE_PROVIDER } from './claim-examiners/claim-examiner/providers/claim-examiner-route.provider';
import { AddressValidationProvider } from './shared/address/address-validation.provider';
import { MockAddressProvider } from './shared/address/mock-address.provider';
import { SmartyAddressProvider } from './shared/address/smarty-address.provider';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(APP_ROUTES),
    APP_ROUTE_PROVIDER,
    provideAnimations(),
    provideAbpCore(
      withOptions({
        environment,
        registerLocaleFn: registerLocale(),
      }),
    ),
    provideAbpOAuth(),
    // ABP 10.0.2 bug fix: checkAccessToken uses this.injector instead of the injector parameter.
    // In strict mode, this is undefined for plain functions, crashing app init after OAuth login.
    // Fixed in ABP 10.1.0. Remove this override when upgrading past 10.1.0.
    // Ref: @abp/ng.oauth/fesm2022/abp-ng.oauth.mjs line 358
    {
      provide: CHECK_AUTHENTICATION_STATE_FN_KEY,
      useValue: (injector: Injector) => {
        const configState = injector.get(ConfigStateService);
        const oAuth = injector.get(OAuthService);
        if (oAuth.hasValidAccessToken() && !configState.getDeep('currentUser.id')) {
          clearOAuthStorage(injector);
        }
      },
    },
    provideIdentityConfig(),
    provideSettingManagementConfig(),
    provideFeatureManagementConfig(),
    provideAccountAdminConfig(),
    // Note (2026-05-19): the Account Public provider was removed from
    // app.config because it registered SPA /account/* routes that were
    // deleted 2026-05-15. Auth UI is hosted entirely on the AuthServer
    // Razor pages now.
    provideCommercialUiConfig(),
    // 2026-05-12 (Issue 1.5) — force light theme as the default for
    // first-time visitors regardless of OS dark-mode preference.
    // `defaultTheme: 'light'` sets the bootstrap default; `disableSystemOption`
    // removes the "System" entry from the picker. Users who explicitly
    // toggle to dark still have that choice persisted in LPX_THEME.
    provideThemeLeptonX(
      withThemeLeptonXOptions({
        defaultTheme: 'light',
        themeOptions: {
          localStorageKey: 'LPX_THEME',
          disableSystemOption: true,
        },
      }),
    ),
    // 2026-05-12 (Issue 1.5) — backfill returning users with stale
    // localStorage['LPX_THEME'] = 'system'. Without this, users who
    // loaded the SPA before the default was changed continue to follow
    // their OS preference.
    provideAppInitializer(() => {
      if (typeof window === 'undefined') return;
      const v = window.localStorage.getItem('LPX_THEME');
      if (v === null || v === 'system') {
        window.localStorage.setItem('LPX_THEME', 'light');
      }
    }),
    provideSideMenuLayout(),
    // Issue 2.1 + 2.8 (2026-05-12) — ngx-mask drives the on-screen SSN
    // redaction (`[hiddenInput]="true"` shows '*' while typing).
    // @rxweb/reactive-form-validators adds named domain validators
    // (socialSecurityNumber, conditional required, digit, etc.) on top
    // of Angular's standard Validators.* set.
    provideNgxMask(),
    importProvidersFrom(RxReactiveFormsModule),
    // F2 / address validation -- Smarty adapter when an embedded key is
    // configured, otherwise the deterministic mock (dev / key-not-yet-set).
    // Set `addressValidation.smartyKey` in src/environments/environment*.ts +
    // allow-list the host in Smarty to activate live autocomplete + USPS
    // standardization; no code change required.
    {
      provide: AddressValidationProvider,
      useFactory: () => {
        const cfg = addressValidation;
        // Bare HttpClient built on HttpBackend so ABP's HTTP interceptors
        // (which attach the `__tenant` + Authorization headers) do NOT run on
        // the cross-origin Smarty calls. Those custom headers force a CORS
        // preflight Smarty rejects ("__tenant not allowed by
        // Access-Control-Allow-Headers"), and we must not leak our tenant/
        // bearer token to a third party. 2026-06-10 fix.
        return cfg.smartyKey
          ? new SmartyAddressProvider(new HttpClient(inject(HttpBackend)), {
              key: cfg.smartyKey,
              autocompleteUrl: cfg.autocompleteUrl,
              verifyUrl: cfg.verifyUrl,
            })
          : new MockAddressProvider();
      },
    },
    provideAbpThemeShared(
      withHttpErrorConfig({
        errorScreen: {
          // Redesign (2026-06-14): branded HTTP error screen replacing ABP's
          // LeptonX HttpErrorComponent for both surfaces. Maps 401 (session-
          // timeout), 403, 404, and 500 to the state-message card.
          component: AppHttpErrorComponent,
          forWhichErrors: [401, 403, 404, 500],
          hideCloseIcon: true,
        },
      }),
      withValidationBluePrint({
        wrongPassword: 'Password must contain uppercase, lowercase, number, and special character',
      }),
    ),
    provideLogo(withEnvironmentOptions(environment)),
    provideGdprConfig(
      withCookieConsentOptions({
        cookiePolicyUrl: '/gdpr-cookie-consent/cookie',
        privacyPolicyUrl: '/gdpr-cookie-consent/privacy',
      }),
    ),
    provideLanguageManagementConfig(),
    provideFileManagementConfig(),
    provideSaasConfig(),
    provideAuditLoggingConfig(),
    provideOpeniddictproConfig(),
    provideTextTemplateManagementConfig(),
    STATES_STATE_ROUTE_PROVIDER,
    APPOINTMENT_TYPES_APPOINTMENT_TYPE_ROUTE_PROVIDER,
    APPOINTMENT_STATUSES_APPOINTMENT_STATUS_ROUTE_PROVIDER,
    APPOINTMENT_DOCUMENT_TYPES_APPOINTMENT_DOCUMENT_TYPE_ROUTE_PROVIDER,
    APPOINTMENT_LANGUAGES_APPOINTMENT_LANGUAGE_ROUTE_PROVIDER,
    DOCTOR_MANAGEMENT_ROUTE_PROVIDER,
    LOCATIONS_LOCATION_ROUTE_PROVIDER,
    DOCTORS_DOCTOR_ROUTE_PROVIDER,
    DOCTOR_AVAILABILITIES_DOCTOR_AVAILABILITY_ROUTE_PROVIDER,
    PATIENTS_PATIENT_ROUTE_PROVIDER,
    APPOINTMENTS_APPOINTMENT_ROUTE_PROVIDER,
    APPOINTMENTS_CHANGE_REQUEST_ROUTE_PROVIDER,
    APPLICANT_ATTORNEYS_APPLICANT_ATTORNEY_ROUTE_PROVIDER,
    DEFENSE_ATTORNEYS_DEFENSE_ATTORNEY_ROUTE_PROVIDER,
    CLAIM_EXAMINERS_CLAIM_EXAMINER_ROUTE_PROVIDER,
  ],
};
