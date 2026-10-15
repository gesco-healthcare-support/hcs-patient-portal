import { Environment } from '@abp/ng.core';
import {
  detectTenantSlugAndMaybeRedirect,
  rewriteEnvironmentForTenantSubdomain,
  TenantBootstrapLocation,
} from './tenant-bootstrap';

/**
 * T4 (in-house hosting, 2026-07-09) -- pins the office-subdomain URL rewrite.
 * rewriteEnvironmentForTenantSubdomain is pure (no window), so it is unit-tested
 * directly.
 *
 * Production hardening (task 1.1, 2026-08-31) -- the redirect half
 * (detectTenantSlugAndMaybeRedirect) is now unit-tested too, via the injected
 * TenantBootstrapLocation seam. It was previously covered only by the in-browser
 * CHECKPOINT 1 bring-up, which is why Sonar tssecurity:S6105 on the `replace` call
 * had no executable rebuttal. The "destination origin is not caller-controlled"
 * block below is that rebuttal; see docs/production-hardening/00-triage-log.md.
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
        default: {
          url: 'https://api.portal.example.com',
          rootNamespace: 'HealthcareSupport.CaseEvaluation',
        },
        AbpAccountPublic: {
          url: 'https://auth.portal.example.com',
          rootNamespace: 'AbpAccountPublic',
        },
      },
    } as unknown as Environment;
  }

  it('prepends the office slug to production per-service subdomains', () => {
    const env = prodEnv();

    rewriteEnvironmentForTenantSubdomain(env, 'falkinstein');

    expect((env.application as { baseUrl: string }).baseUrl).toBe(
      'https://falkinstein.portal.example.com',
    );
    expect((env.oAuthConfig as { issuer: string }).issuer).toBe(
      'https://falkinstein.auth.portal.example.com/',
    );
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

    expect((env.application as { baseUrl: string }).baseUrl).toBe(
      'http://falkinstein.localhost:4200',
    );
    expect((env.oAuthConfig as { issuer: string }).issuer).toBe(
      'http://falkinstein.localhost:44368/',
    );
    expect((env.apis as Record<string, { url: string }>)['default'].url).toBe(
      'http://falkinstein.localhost:44327',
    );
  });

  it('maps the reserved admin slug onto each service host', () => {
    const env = prodEnv();

    rewriteEnvironmentForTenantSubdomain(env, 'admin');

    expect((env.apis as Record<string, { url: string }>)['default'].url).toBe(
      'https://admin.api.portal.example.com',
    );
  });

  it('is a no-op when the slug is empty', () => {
    const env = prodEnv();

    rewriteEnvironmentForTenantSubdomain(env, '');

    expect((env.apis as Record<string, { url: string }>)['default'].url).toBe(
      'https://api.portal.example.com',
    );
  });
});

describe('detectTenantSlugAndMaybeRedirect', () => {
  const BASE = 'portal.example.com';
  const ADMIN_ORIGIN = 'https://admin.portal.example.com';

  /**
   * Builds a stand-in for window.location. `replace` records instead of navigating, so
   * the assertion can inspect the composed target. A plain closure rather than a jasmine
   * spy: under this repo's karma + @angular/build esbuild runner some jasmine constructs
   * fail with an opaque "ReferenceError: i is not defined", and a closure sidesteps that
   * class of harness problem entirely.
   */
  function fakeLocation(overrides: Partial<TenantBootstrapLocation> = {}): {
    loc: TenantBootstrapLocation;
    replaced: string[];
  } {
    const replaced: string[] = [];
    const loc: TenantBootstrapLocation = {
      hostname: BASE,
      protocol: 'https:',
      port: '',
      pathname: '/',
      search: '',
      hash: '',
      replace: (url: string) => {
        replaced.push(url);
      },
      ...overrides,
    };
    return { loc, replaced };
  }

  describe('slug resolution', () => {
    it('returns the office slug and does not redirect for an office subdomain', () => {
      const { loc, replaced } = fakeLocation({ hostname: 'falkinstein.portal.example.com' });

      expect(detectTenantSlugAndMaybeRedirect(BASE, loc)).toBe('falkinstein');
      expect(replaced).toHaveSize(0);
    });

    it('returns the admin slug for the reserved host surface without redirecting', () => {
      const { loc, replaced } = fakeLocation({ hostname: 'admin.portal.example.com' });

      expect(detectTenantSlugAndMaybeRedirect(BASE, loc)).toBe('admin');
      expect(replaced).toHaveSize(0);
    });

    it('redirects the bare base host to the reserved admin surface', () => {
      const { loc, replaced } = fakeLocation({ hostname: BASE });

      expect(detectTenantSlugAndMaybeRedirect(BASE, loc)).toBeNull();
      expect(replaced).toHaveSize(1);
      expect(new URL(replaced[0]).host).toBe('admin.portal.example.com');
    });

    it('redirects a numeric IPv4 host to the admin surface', () => {
      const { loc, replaced } = fakeLocation({ hostname: '192.168.101.41' });

      expect(detectTenantSlugAndMaybeRedirect(BASE, loc)).toBeNull();
      expect(new URL(replaced[0]).host).toBe('admin.portal.example.com');
    });

    it('redirects the IPv6 loopback to the admin surface', () => {
      const { loc, replaced } = fakeLocation({ hostname: '::1' });

      expect(detectTenantSlugAndMaybeRedirect(BASE, loc)).toBeNull();
      expect(new URL(replaced[0]).host).toBe('admin.portal.example.com');
    });
  });

  describe('an in-tenant target is honoured', () => {
    it('preserves scheme, port, path, query and fragment across the redirect', () => {
      const { loc, replaced } = fakeLocation({
        hostname: BASE,
        protocol: 'http:',
        port: '4200',
        pathname: '/appointments/view/42',
        search: '?tab=notes',
        hash: '#top',
      });

      detectTenantSlugAndMaybeRedirect(BASE, loc);

      expect(replaced[0]).toBe(
        'http://admin.portal.example.com:4200/appointments/view/42?tab=notes#top',
      );
    });

    it('omits the port segment when the page has no explicit port', () => {
      const { loc, replaced } = fakeLocation({ hostname: BASE, pathname: '/patients' });

      detectTenantSlugAndMaybeRedirect(BASE, loc);

      expect(replaced[0]).toBe('https://admin.portal.example.com/patients');
    });
  });

  /**
   * The executable rebuttal to Sonar tssecurity:S6105. The rule's concern is that a
   * caller-supplied value could relocate the redirect to a host the attacker controls.
   * It cannot: the authority is composed from a literal ('admin') plus deployment config
   * (baseHost), and every caller-influenceable component lands after the leading '/' of
   * location.pathname. Each case below is a crafted target that would be an off-origin
   * bounce if the authority were caller-influenceable.
   */
  describe('a crafted external target is rejected -- destination origin is not caller-controlled', () => {
    const CRAFTED_PATHNAMES = [
      '//evil.example.net/signin',
      '///evil.example.net',
      '/@evil.example.net',
      '/..//evil.example.net',
      '/%2f%2fevil.example.net',
      '/\\evil.example.net',
    ];

    CRAFTED_PATHNAMES.forEach((pathname) => {
      it(`keeps the admin origin for a crafted pathname: ${pathname}`, () => {
        const { loc, replaced } = fakeLocation({ hostname: BASE, pathname });

        detectTenantSlugAndMaybeRedirect(BASE, loc);

        expect(replaced).toHaveSize(1);
        expect(new URL(replaced[0]).origin).toBe(ADMIN_ORIGIN);
      });
    });

    it('keeps the admin origin when query and fragment name an external host', () => {
      const { loc, replaced } = fakeLocation({
        hostname: BASE,
        search: '?next=https://evil.example.net/login&redirect=//evil.example.net',
        hash: '#@evil.example.net',
      });

      detectTenantSlugAndMaybeRedirect(BASE, loc);

      const target = new URL(replaced[0]);
      expect(target.origin).toBe(ADMIN_ORIGIN);
      expect(target.host).toBe('admin.portal.example.com');
    });

    it('keeps the admin origin when a crafted userinfo separator precedes the path', () => {
      const { loc, replaced } = fakeLocation({
        hostname: BASE,
        pathname: '/x@evil.example.net/y',
      });

      detectTenantSlugAndMaybeRedirect(BASE, loc);

      expect(new URL(replaced[0]).hostname).toBe('admin.portal.example.com');
    });

    it('never resolves to a host outside the configured tenant host set', () => {
      const { loc, replaced } = fakeLocation({
        hostname: BASE,
        pathname: '//evil.example.net/a',
        search: '?u=https://evil.example.net',
        hash: '#//evil.example.net',
      });

      detectTenantSlugAndMaybeRedirect(BASE, loc);

      expect(new URL(replaced[0]).hostname.endsWith(`.${BASE}`)).toBe(true);
    });
  });
});
