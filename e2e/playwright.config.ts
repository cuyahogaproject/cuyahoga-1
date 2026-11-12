import { defineConfig, devices } from '@playwright/test';
import * as fs from 'node:fs';
import * as path from 'node:path';

// Optional local settings (credentials, base url). See .env.example.
const envFile = path.join(__dirname, '.env');
if (fs.existsSync(envFile)) {
  process.loadEnvFile(envFile);
}

const baseURL = process.env.CUYAHOGA_BASE_URL ?? 'http://localhost:8080';
const port = new URL(baseURL).port || '80';
const webRoot = path.resolve(__dirname, '..', 'Web');
const iisExpress = process.env.IIS_EXPRESS_PATH ?? 'C:\\Program Files\\IIS Express\\iisexpress.exe';

export const adminStorageState = path.join(__dirname, '.auth', 'admin.json');

export default defineConfig({
  testDir: './tests',
  // Tests share one database, so they run one at a time.
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  // The first request after a build compiles the app and can take a while.
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'setup', testMatch: /auth\.setup\.ts/ },
    {
      name: 'public',
      testMatch: /public\.spec\.ts/,
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'admin',
      testMatch: /admin.*\.spec\.ts/,
      dependencies: ['setup'],
      use: { ...devices['Desktop Chrome'], storageState: adminStorageState },
    },
  ],
  // Starts IIS Express on the Web folder unless the site is already running.
  // The site must be built first (Web/bin).
  webServer: {
    command: `"${iisExpress}" /path:"${webRoot}" /port:${port} /systray:false`,
    url: `${baseURL}/Default.aspx`,
    reuseExistingServer: true,
    timeout: 180_000,
  },
});
