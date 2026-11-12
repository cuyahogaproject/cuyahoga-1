// Starts a Cuyahoga instance for the e2e tests on an empty database: creates the database, then runs
// IIS Express on the Web folder with the test settings in CUYAHOGA_* environment variables.
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
const webRoot = path.resolve(here, '..', '..', 'Web');
const port = process.env.CUYAHOGA_E2E_PORT ?? '8081';
const iisExpress = process.env.IIS_EXPRESS_PATH ?? 'C:\\Program Files\\IIS Express\\iisexpress.exe';

// Keep the search index of the test site apart from the one of the development site, and start empty.
const indexDir = 'index-e2e';
fs.rmSync(path.join(webRoot, indexDir), { recursive: true, force: true });

const siteEnvironment = {
  ...(await recreateDatabase()),
  CUYAHOGA_SEARCHINDEXDIR: `~/${indexDir}`,
};

const site = spawn(iisExpress, [`/path:${webRoot}`, `/port:${port}`, '/systray:false'], {
  env: { ...process.env, ...siteEnvironment },
  stdio: 'inherit',
});
site.on('exit', (code) => process.exit(code ?? 0));
for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => site.kill());
}
