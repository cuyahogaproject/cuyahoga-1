import { test as setup, expect } from '@playwright/test';
import { adminStorageState } from '../playwright.config';
import { adminCredentials } from './helpers';

setup('log in as administrator', async ({ page }) => {
  const credentials = adminCredentials();
  await page.goto('/Login.aspx?ReturnUrl=%2fAdmin%2fDefault.aspx');
  await page.locator('[id$="txtUsername"]').fill(credentials.username);
  await page.locator('[id$="txtPassword"]').fill(credentials.password);
  await page.locator('[id$="btnLogin"]').click();

  await expect(page, 'login should land on the admin home page').toHaveURL(/\/Admin\/Default\.aspx/i);
  await page.context().storageState({ path: adminStorageState });
});
