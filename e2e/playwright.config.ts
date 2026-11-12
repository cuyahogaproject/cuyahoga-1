import { defineConfig, devices } from '@playwright/test';
import * as fs from 'node:fs';
import * as path from 'node:path';

// Optional local settings (database type, port). See .env.example.
const envFile = path.join(__dirname, '.env');
if (fs.existsSync(envFile)) {
  process.loadEnvFile(envFile);
}

// The test site runs next to the development site (port 8080) on its own empty database.
const port = process.env.CUYAHOGA_E2E_PORT ?? '8081';
const baseURL = `http://localhost:${port}`;

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
  // A fresh site for every run: install (web installer with the sample site) -> log in -> add test content -> tests.
  projects: [
    { name: 'install', testMatch: /install\.setup\.ts/ },
    { name: 'login', testMatch: /auth\.setup\.ts/, dependencies: ['install'] },
    {
      name: 'seed',
      testMatch: /seed\.setup\.ts/,
      dependencies: ['login'],
      use: { storageState: adminStorageState },
    },
    {
      name: 'public',
      testMatch: /public\.spec\.ts/,
      dependencies: ['seed'],
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'admin',
      testMatch: /admin.*\.spec\.ts/,
      dependencies: ['seed'],
      use: { ...devices['Desktop Chrome'], storageState: adminStorageState },
    },
  ],
  // Creates an empty database (CUYAHOGA_E2E_DB: mssql, mysql or postgres) and starts IIS Express on the Web folder.
  // The site must be built first (Web/bin).
  webServer: {
    command: 'node scripts/start-site.mjs',
    url: `${baseURL}/Default.aspx`,
    reuseExistingServer: false,
    timeout: 180_000,
    stdout: 'pipe',
  },
});
