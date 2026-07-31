import { defineConfig, devices } from '@playwright/test';

/**
 * Demo e2e config. The 7 demo flows run against the Falkinstein tenant on
 * the local docker-compose stack:
 *
 *   SPA          http://falkinstein.localhost:4200
 *   AuthServer   http://falkinstein.localhost:44368
 *   API          http://falkinstein.localhost:44327
 *
 * `*.localhost` resolves to 127.0.0.1 natively per RFC 6761; no hosts file
 * edit is required on Edge / Chrome / Firefox. Safari is intentionally out
 * of scope for the local dev loop.
 *
 * The flows are NOT independent -- a booking depends on a slot existing,
 * an approval depends on a booking, and so on. We run sequentially with
 * one worker so the docker-compose state stays predictable, and we keep
 * each spec narrow + self-describing so that a failure points to its
 * specific step rather than to a tangle of upstream side effects.
 *
 * Trace + screenshot + video are all captured on first failure so the
 * one we cannot reproduce in headless still has a record. HTML report
 * lands in test-results/ for Adrian to open with `yarn report`.
 */
export default defineConfig({
  testDir: './specs',
  outputDir: './test-results/output',
  reporter: [['list'], ['html', { open: 'never', outputFolder: './test-results/html' }]],
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 90_000,
  expect: { timeout: 15_000 },
  use: {
    baseURL: 'http://falkinstein.localhost:4200',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
    ignoreHTTPSErrors: true,
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
