import { test as setup, expect } from '@playwright/test';
import { adminCredentials } from './helpers';

// Installs Cuyahoga on the empty test database with the web installer, like a new user would:
// core database, optional modules, administrator account and the sample site.

/** The optional modules in this repository; Web/Modules can contain others from older builds. */
const optionalModules = ['Articles', 'Downloads', 'RemoteContent'];

setup('install Cuyahoga with the sample site', async ({ page }) => {
  setup.setTimeout(180_000);

  // The first request on an empty database (Playwright's check whether the site is up) switches the site to
  // installation mode and redirects to the installer; open the installer directly.
  await page.goto('/Install/Install.aspx');

  await page.locator('[id$="btnInstallDatabase"]').click();
  await expect(page.locator('[id$="btnInstallModules"]'), 'core database installed').toBeVisible();

  const offered: string[] = [];
  for (const checkbox of await page.locator('input[id$="chkInstall"]').all()) {
    const moduleName = (await checkbox.locator('xpath=ancestor::tr[1]').innerText()).trim();
    offered.push(moduleName);
    await checkbox.setChecked(optionalModules.includes(moduleName));
  }
  expect(offered, 'optional modules offered by the installer').toEqual(expect.arrayContaining(optionalModules));
  await page.locator('[id$="btnInstallModules"]').click();

  const { password } = adminCredentials();
  await page.locator('[id$="txtPassword"]').fill(password);
  await page.locator('[id$="txtConfirmPassword"]').fill(password);
  await page.locator('[id$="btnAdmin"]').click();

  await page.locator('[id$="btnCreateSite"]').click();
  await expect(page.locator('[id$="hplContinue"]'), 'installation finished').toBeVisible();
  await expect(page.locator('[id$="lblError"]')).toHaveCount(0);
});
