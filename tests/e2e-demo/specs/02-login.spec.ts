import { test, expect } from '@playwright/test';
import { INTERNAL_USERS, URLS } from './helpers/demo-fixtures';
import { login } from './helpers/auth';

/**
 * Login probes for each internal user role we care about for the demo.
 * Run before the full demo flow so we know the OAuth pipeline + role
 * assignment + post-login routing all work for the seeded accounts.
 */

test.describe('01 -- internal user login', () => {
  test('Staff Supervisor can log in and lands on /dashboard', async ({ page }) => {
    await login(page, INTERNAL_USERS.supervisor.email, INTERNAL_USERS.supervisor.password);
    await expect(page, 'should land on dashboard route').toHaveURL(/\/dashboard/, {
      timeout: 30_000,
    });
  });

  test('Tenant admin can log in', async ({ page }) => {
    await login(page, INTERNAL_USERS.admin.email, INTERNAL_USERS.admin.password);
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 30_000 });
  });

  test('Clinic Staff can log in', async ({ page }) => {
    await login(page, INTERNAL_USERS.staff.email, INTERNAL_USERS.staff.password);
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 30_000 });
  });
});
