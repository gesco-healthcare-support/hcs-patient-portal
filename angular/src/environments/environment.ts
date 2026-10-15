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
 *
 * STATUS 2026-09-28: the Smarty SUBSCRIPTION IS NOT RENEWED, so live autocomplete and
 * USPS standardisation are most likely NOT working, even though a key is present here
 * and the provider factory will happily select the live provider because the key is
 * non-empty. A non-empty key is therefore not evidence that address lookup works.
 *
 * If you are picking this up: open your own Smarty account and subscription, or choose an
 * alternative that integrates better with Azure, rather than assuming this one is live.
 * The key below is Smarty's "website" key, which is public by design and protected only by
 * the dashboard host allow-list, so it is not a secret -- but it is also not ours to rely on.
 */
export const addressValidation = {
  smartyKey: '280901894760220099',
  autocompleteUrl: 'https://us-autocomplete-pro.api.smarty.com/lookup',
  verifyUrl: 'https://us-street.api.smarty.com/street-address',
};
