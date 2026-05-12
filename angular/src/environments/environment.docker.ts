import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost:4200';

const oAuthConfig = {
  issuer: 'http://localhost:44368/',
  redirectUri: baseUrl,
  // Bug D fix (2026-05-11) -- silent-refresh helper served from AuthServer wwwroot.
  silentRefreshRedirectUri: 'http://localhost:44368/silent-refresh.html',
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
    name: 'CaseEvaluation',
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
