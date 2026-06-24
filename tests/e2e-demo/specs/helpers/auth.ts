import { Page, expect } from '@playwright/test';
import { URLS } from './demo-fixtures';

/**
 * Drives the OAuth code-flow login: visit the SPA, click "Login" (or wait
 * for the auto-redirect), enter credentials on the AuthServer page,
 * submit, and wait for the SPA to land in an authenticated state.
 *
 * The SPA's bootstrap rewrites the OAuth issuer to the subdomain
 * (tenant-bootstrap.ts), so the AuthServer URL we redirect to is
 * `<slug>.localhost:44368`. ABP's login page uses ASP.NET Core Identity
 * input names: `LoginInput.UserNameOrEmailAddress`, `LoginInput.Password`.
 * Submit button has `type="submit"`. The post-login redirect lands the
 * user on the SPA root, then `postLoginRedirectGuard` routes internal
 * users to /dashboard.
 */
export async function login(
  page: Page,
  email: string,
  password: string
): Promise<void> {
  // 1. Visit the SPA. If unauthenticated, ABP's auto-login (via OAuth
  //    OIDC discovery) redirects to the AuthServer login page after a
  //    short bootstrap. We trigger by clicking the Login link to make
  //    the redirect deterministic in the test rather than depending
  //    on whatever the SPA's idle behaviour is.
  await page.goto(`${URLS.spa}/`);

  // The SPA renders a Login link/button on the home page when no token
  // is present. Different LeptonX layouts surface the action via either
  // a top-right button or a sidebar link, so we try both.
  const loginTriggers = [
    page.getByRole('link', { name: /^Login$/i }).first(),
    page.getByRole('button', { name: /^Login$/i }).first(),
    page.getByText(/^Login$/i).first(),
  ];
  let triggered = false;
  for (const trigger of loginTriggers) {
    if (await trigger.count() > 0 && await trigger.isVisible().catch(() => false)) {
      await trigger.click();
      triggered = true;
      break;
    }
  }
  if (!triggered) {
    // Fall back to the OIDC initiate URL directly, mimicking what the
    // angular-oauth2-oidc library does on `initLoginFlow()`.
    await page.goto(
      `${URLS.authServer}/connect/authorize?` +
        `response_type=code&` +
        `client_id=CaseEvaluation_App&` +
        `redirect_uri=${encodeURIComponent(URLS.spa)}&` +
        `scope=${encodeURIComponent('openid profile email CaseEvaluation offline_access')}&` +
        `state=playwright-login`
    );
  }

  // 2. We should now be on the AuthServer login page. Wait for the
  //    URL to settle on it and the form to be visible.
  await page.waitForURL(/\/Account\/Login/i, { timeout: 30_000 });

  // The ABP login form input names are stable across LeptonX versions:
  // `LoginInput.UserNameOrEmailAddress` + `LoginInput.Password`.
  await page.fill('input[name="LoginInput.UserNameOrEmailAddress"]', email);
  await page.fill('input[name="LoginInput.Password"]', password);

  // 3. Submit. After the round-trip, we're redirected back to the SPA
  //    on its tenant subdomain. The postLoginRedirectGuard then routes
  //    internal users to /dashboard.
  await Promise.all([
    page.waitForURL(new RegExp(URLS.spa.replace(/[.\\/:]/g, '\\$&')), {
      timeout: 30_000,
    }),
    page.locator('form').getByRole('button', { name: /Login/i }).click(),
  ]);

  // 4. Confirm the SPA shell rendered with a logged-in account dropdown.
  //    LeptonX's avatar/account dropdown shows the user's display name
  //    or email, so we assert the email substring appears somewhere on
  //    the page (account header, top-bar, etc.).
  await expect(
    page.locator('body'),
    'logged-in shell should reference the user email'
  ).toContainText(email.split('@')[0], { timeout: 30_000 });
}

/**
 * Drives logout via the LeptonX user dropdown. Used between flows in
 * specs that switch between internal + external users.
 */
export async function logout(page: Page): Promise<void> {
  // LeptonX exposes the avatar dropdown trigger; the Logout item is
  // inside it. Selector mirrors what LeptonX 4.x renders.
  await page
    .locator(
      'lpx-user-menu, lpx-avatar, [data-cy="user-menu"], button[aria-label*="user" i]'
    )
    .first()
    .click({ trial: false });
  await page.getByRole('menuitem', { name: /^Logout$/i }).click();
  await page.waitForURL((url) => !url.toString().includes('/dashboard'), {
    timeout: 15_000,
  });
}
