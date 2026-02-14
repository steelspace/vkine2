import { defineConfig, devices } from '@playwright/test';
import path from 'path';

export default defineConfig({
  testDir: './tests',
  timeout: 30_000,
  expect: { timeout: 5_000 },
  fullyParallel: true,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: 'http://localhost:5291',
    headless: true,
    viewport: { width: 1280, height: 800 },
    actionTimeout: 5_000,
    trace: 'on-first-retry'
  },
  webServer: {
    command: 'dotnet run --project ./vkine.csproj',
    port: 5291,
    cwd: path.join(__dirname, '..'),
    timeout: 120_000,
    reuseExistingServer: true
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'webkit', use: { ...devices['Desktop Safari'] } }
  ]
});
