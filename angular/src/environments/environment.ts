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
    name: 'CaseEvaluation',
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
