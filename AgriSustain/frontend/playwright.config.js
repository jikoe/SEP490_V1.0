import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  workers: 1,
  retries: 0,
  reporter: [['list']],
  use: {
    baseURL: 'http://127.0.0.1:5173',
    channel: 'msedge',
    headless: true,
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    viewport: { width: 1440, height: 1000 },
  },
  webServer: [
    {
      command: 'dotnet run --project ../src/AgriSustain.Api --configuration Release --no-build --no-restore',
      env: { Persistence__Mode: 'InMemory' },
      url: 'http://localhost:5179/health',
      reuseExistingServer: false,
      timeout: 45000,
    },
    {
      command: 'npm run dev',
      url: 'http://127.0.0.1:5173',
      reuseExistingServer: false,
      timeout: 45000,
    },
  ],
});
