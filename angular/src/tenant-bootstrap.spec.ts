import { Environment } from '@abp/ng.core';
import { rewriteEnvironmentForTenantSubdomain } from './tenant-bootstrap';

/**
 * T4 (in-house hosting, 2026-07-09) -- pins the office-subdomain URL rewrite.
 * rewriteEnvironmentForTenantSubdomain is pure (no window), so it is unit-tested
 * directly; the redirect half (detectTenantSlugAndMaybeRedirect) uses
 * window.location and is verified in-browser at the CHECKPOINT 1 bring-up.
 */
describe('rewriteEnvironmentForTenantSubdomain', () => {
  function prodEnv(): Environment {
    return {
      production: true,
      application: { baseUrl: 'https://portal.example.com', name: 'X' },
      oAuthConfig: {
        issuer: 'https://auth.portal.example.com/',
        redirectUri: 'https://portal.example.com',
        clientId: 'CaseEvaluation_App',
        responseType: 'code',
        scope: 'offline_access CaseEvaluation',
      },
      apis: {
        default: { url: 'https://api.portal.example.com', rootNamespace: 'HealthcareSupport.CaseEvaluation' },
        AbpAccountPublic: { url: 'https://auth.portal.example.com', rootNamespace: 'AbpAccountPublic' },
      },
    } as unknown as Environment;
  }

  it('prepends the office slug to production per-service subdomains', () => {
    const env = prodEnv();

    rewriteEnvironmentForTenantSubdomain(env, 'falkinstein');

    expect((env.application as { baseUrl: string }).baseUrl).toBe('https://falkinstein.portal.example.com');
    expect((env.oAuthConfig as { issuer: string }).issuer).toBe('https://falkinstein.auth.portal.example.com/');
    const apis = env.apis as Record<string, { url: string }>;
    expect(apis['default'].url).toBe('https://falkinstein.api.portal.example.com');
    expect(apis['AbpAccountPublic'].url).toBe('https://falkinstein.auth.portal.example.com');
  });

  it('still works for the local dev localhost base', () => {
    const env = {
      application: { baseUrl: 'http://localhost:4200', name: 'X' },
      oAuthConfig: { issuer: 'http://localhost:44368/' },
      apis: { default: { url: 'http://localhost:44327', rootNamespace: 'x' } },
    } as unknown as Environment;

    rewriteEnvironmentForTenantSubdomain(env, 'falkinstein');

    expect((env.application as { baseUrl: string }).baseUrl).toBe('http://falkinstein.localhost:4200');
    expect((env.oAuthConfig as { issuer: string }).issuer).toBe('http://falkinstein.localhost:44368/');
    expect((env.apis as Record<string, { url: string }>)['default'].url).toBe('http://falkinstein.localhost:44327');
  });

  it('maps the reserved admin slug onto each service host', () => {
    const env = prodEnv();

    rewriteEnvironmentForTenantSubdomain(env, 'admin');

    expect((env.apis as Record<string, { url: string }>)['default'].url).toBe('https://admin.api.portal.example.com');
  });

  it('is a no-op when the slug is empty', () => {
    const env = prodEnv();

    rewriteEnvironmentForTenantSubdomain(env, '');

    expect((env.apis as Record<string, { url: string }>)['default'].url).toBe('https://api.portal.example.com');
  });
});
