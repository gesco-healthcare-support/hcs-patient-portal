import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost:4200';

const oAuthConfig = {
  issuer: 'http://localhost:44368/',
  redirectUri: baseUrl,
  clientId: 'CaseEvaluation_App',
  responseType: 'code',
  scope: 'offline_access CaseEvaluation',
  requireHttps: false,
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
      url: 'http://localhost:44327',
      rootNamespace: 'HealthcareSupport.CaseEvaluation',
    },
    AbpAccountPublic: {
      url: oAuthConfig.issuer,
      rootNamespace: 'AbpAccountPublic',
    },
  },
} as Environment;

/**
 * F2 / address validation (2026-05-29) -- Smarty config. Empty `smartyKey`
 * keeps the mock provider; set the embedded ("website") key + allow-list the
 * host in Smarty to activate live autocomplete + USPS standardization.
 */
export const addressValidation = {
  smartyKey: '',
  autocompleteUrl: 'https://us-autocomplete-pro.api.smarty.com/lookup',
  verifyUrl: 'https://us-street.api.smarty.com/street-address',
};
