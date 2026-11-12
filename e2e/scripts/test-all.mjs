// Runs the e2e tests on every database type and merges the results into one HTML report (playwright-report).
// Continues with the next database when a run fails; exits with an error when any run failed.
import { spawnSync } from 'node:child_process';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';

const e2eDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const databases = ['mssql', 'mysql', 'postgres'];
// Each run writes blob-report/report-<database>.zip, but the blob reporter clears blob-report at the start of a
// run, so the reports are collected in a separate directory.
const blobDir = path.join(e2eDir, 'blob-report');
const collectedDir = path.join(e2eDir, 'blob-report-all');

function npx(args, env = {}) {
  return spawnSync('npx', args, { cwd: e2eDir, stdio: 'inherit', shell: true, env: { ...process.env, ...env } }).status;
}

fs.rmSync(collectedDir, { recursive: true, force: true });
fs.mkdirSync(collectedDir, { recursive: true });

const failed = [];
for (const database of databases) {
  console.log(`\n=== e2e tests on ${database} ===`);
  if (npx(['playwright', 'test'], { CUYAHOGA_E2E_DB: database }) !== 0) {
    failed.push(database);
  }
  const report = path.join(blobDir, `report-${database}.zip`);
  if (fs.existsSync(report)) {
    fs.copyFileSync(report, path.join(collectedDir, `report-${database}.zip`));
  } else {
    console.error(`No report for ${database} (${report}).`);
  }
}

console.log('\n=== Merging the reports ===');
npx(['playwright', 'merge-reports', '--reporter', 'html', 'blob-report-all'], { PLAYWRIGHT_HTML_OPEN: 'never' });
console.log('Open the report with: npm run report');

if (failed.length > 0) {
  console.error(`\nFailed on: ${failed.join(', ')}`);
  process.exit(1);
}
console.log(`\nPassed on: ${databases.join(', ')}`);
