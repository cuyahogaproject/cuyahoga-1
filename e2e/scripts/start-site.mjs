// Starts a Cuyahoga site (Web.Core) for the e2e tests on an empty database: creates the database and an empty data
// folder (files, search index, UserFiles), then runs the site with the test settings in CUYAHOGA_* environment
// variables. The project folder is the content root, so the public files (wwwroot), the install scripts and the files
// of the modules are the ones of the build.
// Playwright starts this script (webServer in playwright.config.ts) and stops it after the tests.
import { spawn } from 'node:child_process';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';
import { recreateDatabase } from './databases.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));

// Playwright passes its environment (including e2e/.env); load .env too when the script runs on its own.
const envFile = path.join(here, '..', '.env');
if (fs.existsSync(envFile)) {
  process.loadEnvFile(envFile);
}
const repoRoot = path.resolve(here, '..', '..');
const siteDir = path.join(repoRoot, 'Web.Core');
const siteDll = path.join(siteDir, 'bin', 'Debug', 'net10.0', 'Cuyahoga.Web.Core.dll');
const port = process.env.CUYAHOGA_E2E_PORT ?? '8081';

if (!fs.existsSync(siteDll)) {
  console.error(`${siteDll} not found. Build the solution (Debug) first.`);
  process.exit(1);
}

// The data of the test site, apart from the one of the development site, and empty at the start.
const dataPath = path.join(here, '..', '.site-data');
fs.rmSync(dataPath, { recursive: true, force: true });

const site = spawn('dotnet', [siteDll], {
  cwd: siteDir,
  env: {
    ...process.env,
    ...(await recreateDatabase()),
    ASPNETCORE_ENVIRONMENT: 'Production',
    ASPNETCORE_URLS: `http://localhost:${port}`,
    DataPath: dataPath,
  },
  stdio: 'inherit',
});
site.on('exit', (code) => process.exit(code ?? 0));
for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => site.kill());
}
