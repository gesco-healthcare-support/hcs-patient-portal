# Angular Architecture

> Purpose: Describes the Angular 20 SPA bootstrap sequence, provider list, feature module structure, and key source files. Audience: frontend developers. Last verified: 2026-06-01 vs main.

[Home](../INDEX.md) > [Frontend](./) > Angular Architecture

## Overview

The HCS Case Evaluation Portal frontend is an **Angular 20** application built entirely with **standalone components** (no NgModules). It leverages the ABP Commercial Angular framework for authentication, authorization, theming, and multi-tenancy.

## App Bootstrap Sequence

The application bootstraps via `main.ts` using Angular's `bootstrapApplication()`:

```
main.ts -> bootstrapApplication(AppComponent, appConfig)
```

- **AppComponent** (`app.component.ts`) -- Root component with `<abp-loader-bar>`, `<abp-dynamic-layout>`, and `<abp-gdpr-cookie-consent>`
- **appConfig** (`app.config.ts`) -- `ApplicationConfig` providing all framework and feature providers

```mermaid
flowchart TD
    A[main.ts] --> B[bootstrapApplication]
    B --> C[AppComponent]
    B --> D[appConfig]
    D --> E[provideRouter - APP_ROUTES]
    D --> F[provideAnimations]
    D --> G[provideAbpCore<br/>environment + registerLocale]
    D --> H[provideAbpOAuth]
    D --> FIX[CHECK_AUTHENTICATION_STATE_FN_KEY override<br/>ABP 10.0.2 bug workaround]
    D --> I[ABP Module Configs]
    D --> J[LeptonX Theme + utilities]
    D --> K[Route Providers]
    I --> I1[provideIdentityConfig]
    I --> I2[provideSettingManagementConfig]
    I --> I3[provideFeatureManagementConfig]
    I --> I4[provideAccountAdminConfig]
    I --> I5[provideCommercialUiConfig]
    I --> I6[provideGdprConfig]
    I --> I7[provideLanguageManagementConfig]
    I --> I8[provideFileManagementConfig]
    I --> I9[provideSaasConfig]
    I --> I10[provideAuditLoggingConfig]
    I --> I11[provideOpeniddictproConfig]
    I --> I12[provideTextTemplateManagementConfig]
    J --> J1[provideThemeLeptonX<br/>defaultTheme light]
    J --> J2[provideAppInitializer<br/>LPX_THEME backfill]
    J --> J3[provideSideMenuLayout]
    J --> J4[provideLogo + withEnvironmentOptions]
    J --> J5[provideAbpThemeShared<br/>HTTP errors + validation]
    J --> J6[provideNgxMask]
    J --> J7[importProvidersFrom RxReactiveFormsModule]
    J --> J8[AddressValidationProvider factory]
    K --> K1[APP_ROUTE_PROVIDER]
    K --> K2[DOCTOR_MANAGEMENT_ROUTE_PROVIDER]
    K --> K3[Entity Route Providers x12]
```

## app.config.ts Providers

The `appConfig` registers 38 provider entries in order (26 framework/utility entries followed by 12 route providers):

| Provider | Purpose |
|----------|---------|
| `provideRouter(APP_ROUTES)` | Application routing |
| `APP_ROUTE_PROVIDER` | Home and Dashboard menu registration |
| `provideAnimations()` | Angular animations |
| `provideAbpCore(withOptions({ environment, registerLocaleFn }))` | ABP framework core with environment and locale |
| `provideAbpOAuth()` | OAuth/OIDC authentication |
| `{ provide: CHECK_AUTHENTICATION_STATE_FN_KEY, useValue: fn }` | ABP 10.0.2 bug workaround: `checkAccessToken` crashes in strict mode because it reads `this.injector` instead of the injector parameter. The override clears OAuth storage via the injector argument instead. Remove when upgrading past ABP 10.1.0. |
| `provideIdentityConfig()` | Identity module UI |
| `provideSettingManagementConfig()` | Settings module UI |
| `provideFeatureManagementConfig()` | Feature management UI |
| `provideAccountAdminConfig()` | Account admin pages (Account Public removed 2026-05-15; auth UI now served by AuthServer Razor pages) |
| `provideCommercialUiConfig()` | Commercial UI components (LookupSelect, etc.) |
| `provideThemeLeptonX(withThemeLeptonXOptions(...))` | LeptonX theme; `defaultTheme: 'light'`, system option disabled |
| `provideAppInitializer(...)` | Backfills `LPX_THEME = 'light'` for returning users whose localStorage still holds `'system'` |
| `provideSideMenuLayout()` | Side-menu shell layout |
| `provideNgxMask()` | Drives SSN on-screen redaction (`[hiddenInput]="true"` shows `*` while typing) |
| `importProvidersFrom(RxReactiveFormsModule)` | Adds named domain validators (`socialSecurityNumber`, conditional required, digit) on top of Angular `Validators.*` |
| `{ provide: AddressValidationProvider, useFactory: fn }` | Injects `SmartyAddressProvider` when `environment.addressValidation.smartyKey` is set; falls back to `MockAddressProvider` otherwise |
| `provideAbpThemeShared(withHttpErrorConfig, withValidationBluePrint)` | HTTP error screens (401/403/404/500) and validation blueprint |
| `provideLogo(withEnvironmentOptions(environment))` | Logo configuration |
| `provideGdprConfig(withCookieConsentOptions)` | GDPR cookie/privacy consent |
| `provideLanguageManagementConfig()` | Language management UI |
| `provideFileManagementConfig()` | File management UI |
| `provideSaasConfig()` | SaaS/tenant management UI |
| `provideAuditLoggingConfig()` | Audit log viewer |
| `provideOpeniddictproConfig()` | OpenIddict management UI |
| `provideTextTemplateManagementConfig()` | Text template management |
| Feature entity route providers (x12) | Menu registration for each feature module (see list below) |

Feature entity route provider tokens (12, registered at the end of the providers array):

- `STATES_STATE_ROUTE_PROVIDER`
- `APPOINTMENT_TYPES_APPOINTMENT_TYPE_ROUTE_PROVIDER`
- `APPOINTMENT_STATUSES_APPOINTMENT_STATUS_ROUTE_PROVIDER`
- `APPOINTMENT_LANGUAGES_APPOINTMENT_LANGUAGE_ROUTE_PROVIDER`
- `DOCTOR_MANAGEMENT_ROUTE_PROVIDER`
- `LOCATIONS_LOCATION_ROUTE_PROVIDER`
- `DOCTORS_DOCTOR_ROUTE_PROVIDER`
- `DOCTOR_AVAILABILITIES_DOCTOR_AVAILABILITY_ROUTE_PROVIDER`
- `PATIENTS_PATIENT_ROUTE_PROVIDER`
- `APPOINTMENTS_APPOINTMENT_ROUTE_PROVIDER`
- `APPLICANT_ATTORNEYS_APPLICANT_ATTORNEY_ROUTE_PROVIDER`
- `DEFENSE_ATTORNEYS_DEFENSE_ATTORNEY_ROUTE_PROVIDER`

Note: `APP_ROUTE_PROVIDER` (Home/Dashboard menu registration) is also a route provider token but is registered at position 2 in the providers array, immediately after `provideRouter`.

## ABP Angular Packages

| Package | Version |
|---------|---------|
| `@abp/ng.core` | ~10.0.2 |
| `@abp/ng.components` | ~10.0.2 |
| `@abp/ng.oauth` | ~10.0.2 |
| `@abp/ng.theme.shared` | ~10.0.2 |
| `@abp/ng.setting-management` | ~10.0.2 |
| `@abp/ng.feature-management` | ~10.0.2 |
| `@volo/abp.ng.identity` | ~10.0.2 |
| `@volo/abp.ng.saas` | ~10.0.2 |
| `@volo/abp.ng.openiddictpro` | ~10.0.2 |
| `@volo/abp.ng.account` | ~10.0.2 |
| `@volo/abp.ng.audit-logging` | ~10.0.2 |
| `@volo/abp.ng.gdpr` | ~10.0.2 |
| `@volo/abp.ng.language-management` | ~10.0.2 |
| `@volo/abp.ng.file-management` | ~10.0.2 |
| `@volo/abp.ng.text-template-management` | ~10.0.2 |
| `@volo/abp.commercial.ng.ui` | ~10.0.2 |
| `@volosoft/abp.ng.theme.lepton-x` | ~5.0.2 |

## Environment Configuration

Defined in `angular/src/environments/environment.ts`:

```typescript
apis: {
  default: {
    url: 'https://localhost:44327',        // Main API (HttpApi.Host)
    rootNamespace: 'HealthcareSupport.CaseEvaluation',
  },
  AbpAccountPublic: {
    url: 'https://localhost:44368/',       // AuthServer
    rootNamespace: 'AbpAccountPublic',
  },
}

oAuthConfig: {
  issuer: 'https://localhost:44368/',
  clientId: 'CaseEvaluation_App',
  responseType: 'code',
  scope: 'offline_access CaseEvaluation',
  requireHttps: true,
  impersonation: { tenantImpersonation: true, userImpersonation: true },
}
```

## Feature Modules

The app contains 22 feature directories under `angular/src/app/` (excluding `proxy/` and `shared/`).

```mermaid
flowchart TD
    APP[AppComponent] --> HOME[home]
    APP --> DASH[dashboard]
    APP --> APT_MGMT[Appointment Management]
    APP --> DOC_MGMT[Doctor Management]
    APP --> CONFIG[Configurations]
    APP --> ATTORNEYS[applicant-attorneys]
    APP --> DEF_ATT[defense-attorneys]
    APP --> USER_MGMT[User Management]
    APP --> APPTS[appointments]
    APP --> ABP_BUILTIN[ABP Built-in Modules]

    APT_MGMT --> AT[appointment-types]
    APT_MGMT --> AS[appointment-statuses]
    APT_MGMT --> AL[appointment-languages]
    APT_MGMT --> ADOC[appointment-documents]
    APT_MGMT --> APKT[appointment-packet]

    DOC_MGMT --> LOC[locations]
    DOC_MGMT --> WCAB[wcab-offices]
    DOC_MGMT --> DOC[doctors]
    DOC_MGMT --> DA[doctor-availabilities]
    DOC_MGMT --> PAT[patients]

    CONFIG --> ST[states]

    USER_MGMT --> IUSR[internal-users]
    USER_MGMT --> EUSR[external-users]

    APPTS --> APPTS_LIST[Appointment List]
    APPTS --> APPTS_ADD[Appointment Add]
    APPTS --> APPTS_VIEW[Appointment View]

    ABP_BUILTIN --> ACCT[account]
    ABP_BUILTIN --> IDENT[identity]
    ABP_BUILTIN --> SAAS[saas]
    ABP_BUILTIN --> OIDC[openiddict]
    ABP_BUILTIN --> AUDIT[audit-logs]
    ABP_BUILTIN --> SETTINGS[setting-management]
    ABP_BUILTIN --> LANG[language-management]
    ABP_BUILTIN --> TXT[text-template-management]
    ABP_BUILTIN --> FILE[file-management]
    ABP_BUILTIN --> GDPR_CC[gdpr-cookie-consent]
    ABP_BUILTIN --> GDPR[gdpr]
```

## Proxy Services

All proxy services live in `angular/src/app/proxy/` and are auto-generated from the backend API via ABP CLI. Each entity has a `service.ts` + `models.ts` + `index.ts`. The Angular proxy is auto-generated (see `angular/src/app/CLAUDE.md`).

## Styles

The application uses `styles.scss` which includes:

- **LeptonX theme CSS** -- Custom properties for light/dim/dark themes, logo configuration
- **External user overrides** -- `body.externaluser-role` class hides LeptonX topbar, sidebar, and expands content area
- **Asset references** -- SVG backgrounds for login pages, logos, and getting-started imagery

Third-party style dependencies are loaded via `angular.json` and include ngx-datatable, FontAwesome, Bootstrap Icons, and LeptonX theme CSS bundles.

## Key Source Files

| File | Purpose |
|------|---------|
| `angular/src/main.ts` | Bootstrap entry point |
| `angular/src/app/app.component.ts` | Root component with role detection |
| `angular/src/app/app.config.ts` | All providers and module configuration |
| `angular/src/app/app.routes.ts` | Complete route definitions |
| `angular/src/app/route.provider.ts` | Home/Dashboard menu registration |
| `angular/src/environments/environment.ts` | API URLs and OAuth config |
| `angular/src/styles.scss` | Global styles and LeptonX overrides |

---

**Related Documentation:**
- [Component Patterns](COMPONENT-PATTERNS.md)
- [Routing & Navigation](ROUTING-AND-NAVIGATION.md)
- Proxy Services (the Angular proxy is auto-generated; see `angular/src/app/CLAUDE.md`)
- [Architecture Overview](../architecture/OVERVIEW.md)
