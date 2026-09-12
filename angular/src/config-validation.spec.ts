import { validateRuntimeConfig } from './config-validation';

/**
 * Production hardening (task 1.7, 2026-09-01) -- validation of the runtime config
 * merged from dynamic-env.json at `main.ts:23`.
 *
 * The merge is `Object.assign(environment, await res.json())` with no shape check of
 * any key, and `baseHost` from that merge is concatenated into a URL authority in
 * tenant-bootstrap.ts. Four separate producers write this file and they already
 * disagree on types: two emit `"production": true` as a boolean, the Helm configmap
 * emits the string `"true"`.
 *
 * NOT a fix for the S6105 open-redirect finding, which is closed as a false positive.
 * This is configuration validation on its own merits.
 *
 * ABSENT KEYS ARE NOT ERRORS. Three of the four producers omit `baseHost` entirely and
 * the baked environment covers them, so flagging absence would fail every dev stack.
 */
describe('validateRuntimeConfig', () => {
  /** A config with every setting present and valid, as the production producer emits it. */
  function validProdConfig(): Record<string, unknown> {
    return {
      production: true,
      baseHost: 'portal.example.com',
      application: {
        baseUrl: 'https://portal.example.com',
        name: 'Appointment Portal',
        logoUrl: '',
      },
      oAuthConfig: {
        issuer: 'https://auth.portal.example.com/',
        redirectUri: 'https://portal.example.com',
        clientId: 'CaseEvaluation_App',
        responseType: 'code',
        scope: 'offline_access openid profile email phone CaseEvaluation',
        requireHttps: true,
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
    };
  }

  /** The dev shape: http throughout, production false, no baseHost at all. */
  function validDevConfig(): Record<string, unknown> {
    return {
      production: false,
      application: { baseUrl: 'http://localhost:4200', name: 'X', logoUrl: '' },
      oAuthConfig: {
        issuer: 'http://localhost:44368/',
        redirectUri: 'http://localhost:4200',
        clientId: 'CaseEvaluation_App',
        responseType: 'code',
        scope: 'offline_access CaseEvaluation',
        requireHttps: false,
      },
      apis: { default: { url: 'http://localhost:44327', rootNamespace: 'x' } },
    };
  }

  describe('valid configurations produce no problems', () => {
    it('accepts the production shape', () => {
      expect(validateRuntimeConfig(validProdConfig())).toEqual([]);
    });

    it('accepts the dev shape, including http and a missing baseHost', () => {
      expect(validateRuntimeConfig(validDevConfig())).toEqual([]);
    });

    it('accepts an empty object -- every key is optional', () => {
      expect(validateRuntimeConfig({})).toEqual([]);
    });

    it('accepts the optional Helm-only oAuth flags when they are real booleans', () => {
      const cfg = validDevConfig();
      (cfg['oAuthConfig'] as Record<string, unknown>)['skipIssuerCheck'] = true;
      (cfg['oAuthConfig'] as Record<string, unknown>)['strictDiscoveryDocumentValidation'] = false;
      expect(validateRuntimeConfig(cfg)).toEqual([]);
    });
  });

  /**
   * The rule most likely to be wrong in the tightening direction. A host does not have
   * to look like a public domain: single-label internal names and fully-qualified names
   * with a trailing dot are both legitimate, and a validator that rejects them would
   * break real deployments while looking correct.
   */
  describe('host rule does not over-reject legitimate hosts', () => {
    ['portal.example.com', 'intranet', 'portal.local', 'example.com.', 'a-b.c-d.example'].forEach(
      (host) => {
        it(`accepts "${host}"`, () => {
          const cfg = validDevConfig();
          cfg['baseHost'] = host;
          expect(validateRuntimeConfig(cfg)).toEqual([]);
        });
      },
    );
  });

  describe('baseHost rejects values that are not a bare host', () => {
    const BAD: Array<[string, string]> = [
      ['https://portal.example.com', 'a scheme'],
      ['portal.example.com/', 'a path separator'],
      ['gesco.com@evil.example.net', 'credentials'],
      ['portal.example.com:8443', 'a port'],
      ['portal example com', 'whitespace'],
      ['', 'an empty string'],
    ];

    BAD.forEach(([host, why]) => {
      it(`rejects ${why}: "${host}"`, () => {
        const cfg = validDevConfig();
        cfg['baseHost'] = host;
        const problems = validateRuntimeConfig(cfg);
        expect(problems.length).toBeGreaterThan(0);
        expect(problems.join(' ')).toContain('baseHost');
      });
    });
  });

  describe('service URLs must be absolute http(s)', () => {
    it('rejects a relative URL, which cannot serve as an origin', () => {
      const cfg = validDevConfig();
      (cfg['oAuthConfig'] as Record<string, unknown>)['issuer'] = '/auth';
      const problems = validateRuntimeConfig(cfg);
      expect(problems.join(' ')).toContain('oAuthConfig.issuer');
    });

    it('rejects a non-http scheme', () => {
      const cfg = validDevConfig();
      (cfg['apis'] as Record<string, Record<string, unknown>>)['default']['url'] =
        'ftp://files.example.com';
      expect(validateRuntimeConfig(cfg).join(' ')).toContain('apis.default.url');
    });

    it('names every offending URL, not just the first', () => {
      const cfg = validDevConfig();
      (cfg['oAuthConfig'] as Record<string, unknown>)['issuer'] = 'not a url';
      (cfg['oAuthConfig'] as Record<string, unknown>)['redirectUri'] = 'also not a url';
      const joined = validateRuntimeConfig(cfg).join(' ');
      expect(joined).toContain('oAuthConfig.issuer');
      expect(joined).toContain('oAuthConfig.redirectUri');
    });

    /** apis is a map; a rule hardcoded to the two known names would miss a third. */
    it('checks api entries it has never heard of', () => {
      const cfg = validDevConfig();
      (cfg['apis'] as Record<string, unknown>)['SomeFutureApi'] = {
        url: 'not a url',
        rootNamespace: 'x',
      };
      expect(validateRuntimeConfig(cfg).join(' ')).toContain('apis.SomeFutureApi.url');
    });
  });

  describe('https is required only when production is true', () => {
    it('rejects an http service URL when production is true', () => {
      const cfg = validProdConfig();
      (cfg['oAuthConfig'] as Record<string, unknown>)['issuer'] = 'http://auth.portal.example.com/';
      expect(validateRuntimeConfig(cfg).join(' ')).toContain('oAuthConfig.issuer');
    });

    it('allows the same http URL when production is false', () => {
      const cfg = validProdConfig();
      cfg['production'] = false;
      (cfg['oAuthConfig'] as Record<string, unknown>)['issuer'] = 'http://auth.portal.example.com/';
      expect(validateRuntimeConfig(cfg)).toEqual([]);
    });
  });

  /**
   * The inconsistency that widened this item's scope: prod-dynamic-env.envsh and
   * dev-entrypoint.sh emit real booleans, the Helm configmap emits quoted strings.
   */
  describe('boolean settings must be real booleans', () => {
    it('rejects the string "true" for production', () => {
      const cfg = validDevConfig();
      cfg['production'] = 'true';
      expect(validateRuntimeConfig(cfg).join(' ')).toContain('production');
    });

    it('rejects the string "false" for requireHttps', () => {
      const cfg = validDevConfig();
      (cfg['oAuthConfig'] as Record<string, unknown>)['requireHttps'] = 'false';
      expect(validateRuntimeConfig(cfg).join(' ')).toContain('oAuthConfig.requireHttps');
    });
  });

  describe('required text settings must not be empty', () => {
    it('rejects an empty clientId', () => {
      const cfg = validDevConfig();
      (cfg['oAuthConfig'] as Record<string, unknown>)['clientId'] = '';
      expect(validateRuntimeConfig(cfg).join(' ')).toContain('oAuthConfig.clientId');
    });

    it('allows an empty logoUrl, which the production producer emits deliberately', () => {
      const cfg = validDevConfig();
      (cfg['application'] as Record<string, unknown>)['logoUrl'] = '';
      expect(validateRuntimeConfig(cfg)).toEqual([]);
    });
  });

  describe('malformed input does not throw', () => {
    [null, undefined, 'a string', 42].forEach((input) => {
      it(`returns a list rather than throwing for ${JSON.stringify(input)}`, () => {
        expect(Array.isArray(validateRuntimeConfig(input))).toBe(true);
      });
    });
  });
});
