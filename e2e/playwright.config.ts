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

// The database type of this run. Project names carry it, so a report merged from runs on several databases
// (npm run test:all) shows which database each result belongs to.
const database = process.env.CUYAHOGA_E2E_DB ?? 'mssql';
const project = (name: string) => `${name}-${database}`;

export default defineConfig({
  testDir: './tests',
  // Separate results per database type, so a run on one database doesn't remove those of another.
  outputDir: `test-results/${database}`,
  // Tests share one database, so they run one at a time.
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  // The first request after a build compiles the app and can take a while.
  timeout: 60_000,
  expect: { timeout: 10_000 },
  // The blob report of each database type is merged into one HTML report by scripts/test-all.mjs.
  reporter: [
    ['list'],
    ['html', { open: 'never' }],
    ['blob', { outputDir: 'blob-report', fileName: `report-${database}.zip` }],
  ],
  metadata: { database },
  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  // A fresh site for every run: install (web installer with the sample site) -> log in -> add test content -> tests.
  projects: [
    { name: project('install'), testMatch: /install\.setup\.ts/ },
    { name: project('login'), testMatch: /auth\.setup\.ts/, dependencies: [project('install')] },
    {
      name: project('seed'),
      testMatch: /seed\.setup\.ts/,
      dependencies: [project('login')],
      use: { storageState: adminStorageState },
    },
    {
      name: project('public'),
      testMatch: /public\.spec\.ts/,
      dependencies: [project('seed')],
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: project('admin'),
      testMatch: /admin.*\.spec\.ts/,
      dependencies: [project('seed')],
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
