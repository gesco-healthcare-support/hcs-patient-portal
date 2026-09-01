/**
 * Validation for the runtime configuration merged from `dynamic-env.json`.
 *
 * `main.ts:23` does `Object.assign(environment, await res.json())` with no shape check
 * of any key, and `baseHost` from that merge is concatenated into a URL authority in
 * `tenant-bootstrap.ts`. A malformed value therefore produces wrong tenant routing
 * silently, and the symptom looks like an application bug rather than a bad deploy.
 *
 * FOUR producers write this file and they already disagree on types:
 *   angular/prod-dynamic-env.envsh:31   "production": true      (boolean)
 *   angular/dev-entrypoint.sh:32        "production": false     (boolean)
 *   etc/helm/.../angular-configmap.yaml "production": "true"    (string)
 * Only `prod-dynamic-env.envsh` emits `baseHost` at all, which is why validation lives
 * here rather than only in that script -- the app is the one place that sees every
 * route, including a hand-edited file.
 *
 * NOT a fix for `tssecurity:S6105`, which was triaged as a false positive and closed.
 * This is configuration validation justified on its own merits.
 *
 * Pure and Angular-free by design, mirroring `tenant-bootstrap.ts`, so it can be unit
 * tested directly and called before `bootstrapApplication`.
 */

/** Settings that must be a real boolean when present, by dotted path. */
const BOOLEAN_SETTINGS = [
  'production',
  'oAuthConfig.requireHttps',
  'oAuthConfig.strictDiscoveryDocumentValidation',
  'oAuthConfig.skipIssuerCheck',
] as const;

/** Settings that must be a non-empty string when present, by dotted path. */
const REQUIRED_TEXT_SETTINGS = [
  'application.name',
  'oAuthConfig.clientId',
  'oAuthConfig.responseType',
  'oAuthConfig.scope',
] as const;

/**
 * Settings that must be an absolute http(s) URL when present. `apis.*` is NOT listed
 * here because it is a map -- it is iterated separately so a future entry is covered
 * without anyone remembering to extend this list.
 */
const URL_SETTINGS = [
  'application.baseUrl',
  'oAuthConfig.issuer',
  'oAuthConfig.redirectUri',
] as const;

/**
 * `scope` is checked non-empty only, deliberately NOT for `openid`. The Helm producer
 * emits `"offline_access CaseEvaluation"` with no `openid`, so requiring it would flag
 * an existing producer from inside a validation change. Whether that producer is itself
 * wrong is a separate question and is recorded in the backlog.
 */

/** Reads a dotted path, returning undefined if any segment is missing or not an object. */
function read(root: unknown, path: string): unknown {
  let node: unknown = root;
  for (const segment of path.split('.')) {
    if (node === null || typeof node !== 'object') {
      return undefined;
    }
    node = (node as Record<string, unknown>)[segment];
  }
  return node;
}

/**
 * A bare host: no scheme, no path, no credentials, no port, no whitespace. Deliberately
 * permissive about SHAPE -- a single-label internal name (`intranet`) and a
 * fully-qualified name with a trailing dot (`example.com.`) are both legitimate, and a
 * rule that rejected them would break real deployments while looking correct.
 */
function isBareHost(value: string): boolean {
  if (value.length === 0) {
    return false;
  }
  if (/[\s/@:\\?#]/.test(value)) {
    return false;
  }
  return /^[A-Za-z0-9._-]+$/.test(value);
}

/** Absolute http(s) URL. `new URL` without a base rejects relative values by throwing. */
function parseAbsoluteHttpUrl(value: string): URL | null {
  let url: URL;
  try {
    url = new URL(value);
  } catch {
    return null;
  }
  return url.protocol === 'http:' || url.protocol === 'https:' ? url : null;
}

/**
 * Validates the merged runtime configuration.
 *
 * ABSENT KEYS ARE NOT ERRORS: three of the four producers omit `baseHost`, and the
 * baked environment covers what the file does not supply, so treating absence as a
 * failure would break every dev stack.
 *
 * @returns one human-readable problem per offending setting; empty when valid.
 */
export function validateRuntimeConfig(env: unknown): string[] {
  const problems: string[] = [];

  if (env === null || typeof env !== 'object') {
    return problems;
  }

  // Read before validating: the https rule below keys off this value, and a malformed
  // `production` is reported separately rather than silently disabling that rule.
  const production = read(env, 'production');
  const requireHttps = production === true;

  for (const path of BOOLEAN_SETTINGS) {
    const value = read(env, path);
    if (value !== undefined && typeof value !== 'boolean') {
      problems.push(
        `${path} must be true or false, not the ${typeof value} ${JSON.stringify(value)}`,
      );
    }
  }

  for (const path of REQUIRED_TEXT_SETTINGS) {
    const value = read(env, path);
    if (value !== undefined && (typeof value !== 'string' || value.trim().length === 0)) {
      problems.push(`${path} must be a non-empty value`);
    }
  }

  const baseHost = read(env, 'baseHost');
  if (baseHost !== undefined) {
    if (typeof baseHost !== 'string' || !isBareHost(baseHost)) {
      problems.push(
        `baseHost must be a bare hostname with no scheme, path, port or credentials, not ${JSON.stringify(baseHost)}`,
      );
    }
  }

  const checkUrl = (path: string, value: unknown): void => {
    if (value === undefined) {
      return;
    }
    const url = typeof value === 'string' ? parseAbsoluteHttpUrl(value) : null;
    if (!url) {
      problems.push(`${path} must be an absolute http or https URL, not ${JSON.stringify(value)}`);
      return;
    }
    if (requireHttps && url.protocol !== 'https:') {
      problems.push(`${path} must use https when production is true, not ${JSON.stringify(value)}`);
    }
  };

  for (const path of URL_SETTINGS) {
    checkUrl(path, read(env, path));
  }

  // `apis` is a map of named services. Iterate it rather than naming `default` and
  // `AbpAccountPublic`, so an entry added later is validated without a code change.
  const apis = read(env, 'apis');
  if (apis !== null && typeof apis === 'object') {
    for (const name of Object.keys(apis as Record<string, unknown>)) {
      const entry = (apis as Record<string, unknown>)[name];
      if (entry === null || typeof entry !== 'object') {
        problems.push(`apis.${name} must be an object`);
        continue;
      }
      checkUrl(`apis.${name}.url`, (entry as Record<string, unknown>)['url']);
      const ns = (entry as Record<string, unknown>)['rootNamespace'];
      if (ns !== undefined && (typeof ns !== 'string' || ns.trim().length === 0)) {
        problems.push(`apis.${name}.rootNamespace must be a non-empty value`);
      }
    }
  }

  // `application.logoUrl` is intentionally unchecked beyond type: the production
  // producer emits "" on purpose, and a root-relative path is equally valid.
  const logoUrl = read(env, 'application.logoUrl');
  if (logoUrl !== undefined && typeof logoUrl !== 'string') {
    problems.push('application.logoUrl must be a string');
  }

  return problems;
}
