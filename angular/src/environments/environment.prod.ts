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
  production: true,
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
  // NO remoteEnv here (intentional -- do NOT re-add). Runtime config is loaded by
  // the pre-bootstrap IIFE in main.ts, which fetches dynamic-env.json AND prepends
  // the office subdomain (tenant-bootstrap.ts) so each office talks to its own
  // {office}.auth / {office}.api origin. ABP's remoteEnv deep-merge re-fetched the
  // office-LESS /getEnvConfig at init and (remote-wins) clobbered that prefix back
  // to the bare host -> CORS -> /error. remoteEnv is redundant with the IIFE and
  // conflicts with per-office routing. See docs/plans/2026-07-17-prod-remoteenv-office.md.
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
