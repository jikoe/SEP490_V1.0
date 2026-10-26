import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  testMatch: ['marketplace-cart.spec.js', 'marketplace-filters.spec.js', 'password.spec.js'],
  workers: 1,
  retries: 0,
  reporter: 'list',
  use: {
    baseURL: 'http://127.0.0.1:5183',
    channel: 'msedge',
    headless: true,
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    viewport: { width: 1440, height: 1000 },
  },
  webServer: {
    command: 'npm run dev -- --port 5183',
    url: 'http://127.0.0.1:5183',
    reuseExistingServer: false,
    timeout: 45000,
  },
});
