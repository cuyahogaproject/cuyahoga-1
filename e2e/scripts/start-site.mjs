// Starts a Cuyahoga instance for the e2e tests on an empty database: creates the database, then runs the WebForms
// site (IIS Express on the Web folder) and in front of it the ASP.NET Core site (Web.Core), both with the test
// settings in CUYAHOGA_* environment variables. The tests talk to the ASP.NET Core site, which forwards the requests
// that it doesn't handle to the WebForms site.
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
const webRoot = path.join(repoRoot, 'Web');
const coreSiteDir = path.join(repoRoot, 'Web.Core', 'bin', 'Debug', 'net10.0');
const port = process.env.CUYAHOGA_E2E_PORT ?? '8081';
const legacyPort = process.env.CUYAHOGA_E2E_LEGACY_PORT ?? '8082';
const iisExpress = process.env.IIS_EXPRESS_PATH ?? 'C:\\Program Files\\IIS Express\\iisexpress.exe';

const coreSiteDll = path.join(coreSiteDir, 'Cuyahoga.Web.Core.dll');
if (!fs.existsSync(coreSiteDll)) {
  console.error(`${coreSiteDll} not found. Build the solution (Debug) first.`);
  process.exit(1);
}

// Keep the search index of the test site apart from the one of the development site, and start empty.
const indexDir = 'index-e2e';
fs.rmSync(path.join(webRoot, indexDir), { recursive: true, force: true });

const siteEnvironment = {
  ...process.env,
  ...(await recreateDatabase()),
  CUYAHOGA_SEARCHINDEXDIR: `~/${indexDir}`,
};

const legacySite = spawn(iisExpress, [`/path:${webRoot}`, `/port:${legacyPort}`, '/systray:false'], {
  env: siteEnvironment,
  stdio: 'inherit',
});
const coreSite = spawn('dotnet', [coreSiteDll], {
  cwd: coreSiteDir,
  env: {
    ...siteEnvironment,
    ASPNETCORE_ENVIRONMENT: 'Production',
    ASPNETCORE_URLS: `http://localhost:${port}`,
    LegacySiteUrl: `http://localhost:${legacyPort}`,
  },
  stdio: 'inherit',
});

// When one site stops, stop the other one too.
const sites = [legacySite, coreSite];
const stopAll = () => sites.forEach((site) => site.exitCode === null && site.kill());
for (const site of sites) {
  site.on('exit', (code) => {
    stopAll();
    process.exit(code ?? 0);
  });
}
for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, stopAll);
}
