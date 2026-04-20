import {
  CHECK_AUTHENTICATION_STATE_FN_KEY,
  ConfigStateService,
  provideAbpCore,
  withOptions,
} from '@abp/ng.core';
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
import { provideAccountPublicConfig } from '@volo/abp.ng.account/public/config';
import { provideGdprConfig, withCookieConsentOptions } from '@volo/abp.ng.gdpr/config';
import { provideAuditLoggingConfig } from '@volo/abp.ng.audit-logging/config';
import { provideLanguageManagementConfig } from '@volo/abp.ng.language-management/config';
import { registerLocale } from '@volo/abp.ng.language-management/locale';
import { provideFileManagementConfig } from '@volo/abp.ng.file-management/config';
import { provideSaasConfig } from '@volo/abp.ng.saas/config';
import { provideTextTemplateManagementConfig } from '@volo/abp.ng.text-template-management/config';
import { provideOpeniddictproConfig } from '@volo/abp.ng.openiddictpro/config';
import { HttpErrorComponent, provideThemeLeptonX } from '@volosoft/abp.ng.theme.lepton-x';
import { provideSideMenuLayout } from '@volosoft/abp.ng.theme.lepton-x/layouts';
import { ApplicationConfig, Injector } from '@angular/core';
import { OAuthService } from 'angular-oauth2-oidc';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { environment } from '../environments/environment';
import { APP_ROUTES } from './app.routes';
import { APP_ROUTE_PROVIDER } from './route.provider';
import { STATES_STATE_ROUTE_PROVIDER } from './states/state/providers/state-route.provider';
import { APPOINTMENT_TYPES_APPOINTMENT_TYPE_ROUTE_PROVIDER } from './appointment-types/appointment-type/providers/appointment-type-route.provider';
import { APPOINTMENT_STATUSES_APPOINTMENT_STATUS_ROUTE_PROVIDER } from './appointment-statuses/appointment-status/providers/appointment-status-route.provider';
import { APPOINTMENT_LANGUAGES_APPOINTMENT_LANGUAGE_ROUTE_PROVIDER } from './appointment-languages/appointment-language/providers/appointment-language-route.provider';
import { DOCTOR_MANAGEMENT_ROUTE_PROVIDER } from './doctor-management/providers/doctor-management-route.provider';
import { LOCATIONS_LOCATION_ROUTE_PROVIDER } from './locations/location/providers/location-route.provider';
import { DOCTORS_DOCTOR_ROUTE_PROVIDER } from './doctors/doctor/providers/doctor-route.provider';
import { DOCTOR_AVAILABILITIES_DOCTOR_AVAILABILITY_ROUTE_PROVIDER } from './doctor-availabilities/doctor-availability/providers/doctor-availability-route.provider';
import { PATIENTS_PATIENT_ROUTE_PROVIDER } from './patients/patient/providers/patient-route.provider';
import { APPOINTMENTS_APPOINTMENT_ROUTE_PROVIDER } from './appointments/appointment/providers/appointment-route.provider';
import { APPLICANT_ATTORNEYS_APPLICANT_ATTORNEY_ROUTE_PROVIDER } from './applicant-attorneys/applicant-attorney/providers/applicant-attorney-route.provider';

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
    provideAccountPublicConfig(),
    provideCommercialUiConfig(),
    provideThemeLeptonX(),
    provideSideMenuLayout(),
    provideAbpThemeShared(
      withHttpErrorConfig({
        errorScreen: {
          component: HttpErrorComponent,
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
    APPOINTMENT_LANGUAGES_APPOINTMENT_LANGUAGE_ROUTE_PROVIDER,
    DOCTOR_MANAGEMENT_ROUTE_PROVIDER,
    LOCATIONS_LOCATION_ROUTE_PROVIDER,
    DOCTORS_DOCTOR_ROUTE_PROVIDER,
    DOCTOR_AVAILABILITIES_DOCTOR_AVAILABILITY_ROUTE_PROVIDER,
    PATIENTS_PATIENT_ROUTE_PROVIDER,
    APPOINTMENTS_APPOINTMENT_ROUTE_PROVIDER,
    APPLICANT_ATTORNEYS_APPLICANT_ATTORNEY_ROUTE_PROVIDER,
  ],
};
