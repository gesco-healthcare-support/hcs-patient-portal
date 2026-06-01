import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost:4200';

const oAuthConfig = {
  issuer: 'https://localhost:44368/',
  redirectUri: baseUrl,
  clientId: 'CaseEvaluation_App',
  responseType: 'code',
  scope: 'offline_access CaseEvaluation',
  requireHttps: true,
  impersonation: {
    tenantImpersonation: true,
    userImpersonation: true,
  },
};

export const environment = {
  production: false,
  application: {
    baseUrl,
    name: 'Appointment Portal',
  },
  oAuthConfig,
  apis: {
    default: {
      url: 'https://localhost:44327',
      rootNamespace: 'HealthcareSupport.CaseEvaluation',
    },
    AbpAccountPublic: {
      url: oAuthConfig.issuer,
      rootNamespace: 'AbpAccountPublic',
    },
  },
} as Environment;

/**
 * F2 / address validation (2026-05-29) -- Smarty config. Leave `smartyKey`
 * empty to keep the deterministic mock provider active (dev / key-not-yet-set).
 * Set it to the Smarty embedded ("website") key -- and allow-list this host in
 * the Smarty dashboard -- to activate live autocomplete + USPS standardization.
 * No code change needed; the provider factory in app.config switches on the key.
 */
export const addressValidation = {
  smartyKey: '',
  autocompleteUrl: 'https://us-autocomplete-pro.api.smarty.com/lookup',
  verifyUrl: 'https://us-street.api.smarty.com/street-address',
};
