import { defineConfig } from '@playwright/test';

// UI test for checkout + VietQR payment against the real backend and SQL database.
// Run: npx playwright test -c playwright.payment.config.js
const baseURL = process.env.E2E_BASE_URL || 'http://localhost:5174';

export default defineConfig({
  testDir: './e2e',
  workers: 1,
  retries: 0,
  timeout: 90_000,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report/payment' }]],
  use: {
    baseURL,
    channel: 'msedge',
    headless: true,
    screenshot: 'on',
    trace: 'retain-on-failure',
    viewport: { width: 1440, height: 1000 },
  },
  webServer: [
    {
      command: 'dotnet bin/Debug/net8.0/AgriSustain.dll',
      cwd: '../AgriSustain',
      env: { ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: 'http://localhost:5179' },
      url: 'http://localhost:5179/health',
      reuseExistingServer: true,
      timeout: 60_000,
    },
    {
      command: `npx vite --port ${new URL(baseURL).port}`,
      url: baseURL,
      reuseExistingServer: true,
      timeout: 60_000,
    },
  ],
});
