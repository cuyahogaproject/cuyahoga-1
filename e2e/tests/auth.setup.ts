import { test as setup, expect } from '@playwright/test';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { adminStorageState } from '../playwright.config';
import { adminCredentials } from './helpers';

setup('log in as administrator', async ({ page }) => {
  const credentials = adminCredentials();
  if (!credentials) {
    // Admin tests skip themselves without credentials; write an empty state so the project can start.
    fs.mkdirSync(path.dirname(adminStorageState), { recursive: true });
    fs.writeFileSync(adminStorageState, JSON.stringify({ cookies: [], origins: [] }));
    setup.skip(true, 'CUYAHOGA_ADMIN_USERNAME / CUYAHOGA_ADMIN_PASSWORD not set');
    return;
  }

  await page.goto('/Login.aspx?ReturnUrl=%2fAdmin%2fDefault.aspx');
  await page.locator('[id$="txtUsername"]').fill(credentials.username);
  await page.locator('[id$="txtPassword"]').fill(credentials.password);
  await page.locator('[id$="btnLogin"]').click();

  await expect(page, 'login should land on the admin home page').toHaveURL(/\/Admin\/Default\.aspx/i);
  await page.context().storageState({ path: adminStorageState });
});
