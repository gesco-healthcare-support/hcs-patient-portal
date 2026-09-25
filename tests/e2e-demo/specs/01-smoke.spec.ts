import { test, expect } from '@playwright/test';
import { URLS, TENANT } from './helpers/demo-fixtures';

/**
 * Pre-flight smoke. Diagnoses infrastructure breakage independently of
 * the demo flow.
 *
 * NOTE: Node.js's HTTP stack (used by Playwright's `request` fixture)
 * does NOT honor RFC 6761's `*.localhost` resolution, so subdomain HTTP
 * probes via the `request` fixture fail with ENOTFOUND. Chromium's own
 * resolver handles `*.localhost` correctly. So every smoke probe is
 * page-driven (page.goto / page.evaluate) -- not request-fixture-driven.
 *
 * The day Adrian adds `127.0.0.1 falkinstein.localhost` to the hosts
 * file (T6), the request-fixture path becomes available and these tests
 * can be tightened. Until then, browser-driven probes are sufficient
 * because the demo itself runs in a browser.
 */

test.describe('00 -- pre-flight smoke', () => {
  test('SPA root responds 200 on the tenant subdomain', async ({ page }) => {
    const response = await page.goto(URLS.spa);
    expect(response, 'page.goto should resolve').not.toBeNull();
    expect(response!.status(), `${URLS.spa} should respond`).toBeLessThan(400);
  });

  test('AuthServer publishes per-tenant issuer (T1 wildcard)', async ({ page }) => {
    // Fetch the OpenID config via the BROWSER context so RFC 6761
    // resolution works. page.evaluate runs window.fetch in-page,
    // which uses Chromium's resolver.
    await page.goto(URLS.spa);
    const issuer = await page.evaluate(async (authUrl) => {
      const r = await fetch(`${authUrl}/.well-known/openid-configuration`);
      const j = await r.json();
      return j.issuer as string;
    }, URLS.authServer);
    expect(issuer, 'issuer must reflect the subdomain').toContain(TENANT.slug);
  });

  test('API health endpoint responds', async ({ page }) => {
    await page.goto(URLS.spa);
    const status = await page.evaluate(async (apiUrl) => {
      const r = await fetch(`${apiUrl}/health-status`);
      return r.status;
    }, URLS.api);
    expect(status).toBe(200);
  });

  test('Bare localhost redirects to admin.localhost (T3 host context)', async ({ page }) => {
    await page.goto('http://localhost:4200/');
    await page.waitForURL(/admin\.localhost/, { timeout: 10_000 });
    expect(page.url()).toContain('admin.localhost');
  });

  test('AuthServer login page hides the LeptonX tenant box (T2)', async ({ page }) => {
    await page.goto(
      `${URLS.authServer}/Account/Login?ReturnUrl=${encodeURIComponent(URLS.spa)}`
    );
    const tenantText = await page.getByText(/Tenant:|Not selected/i).count();
    expect(tenantText, 'tenant switcher should not be rendered').toBe(0);
  });
});
