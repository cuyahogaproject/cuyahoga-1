import { test, expect } from '@playwright/test';
import { adminCredentials, crawl, expectHealthyPage, trackPageErrors } from './helpers';

test.skip(!adminCredentials(), 'CUYAHOGA_ADMIN_USERNAME / CUYAHOGA_ADMIN_PASSWORD not set');

const adminPages = [
  'Default.aspx',
  'Sections.aspx',
  'Templates.aspx',
  'Modules.aspx',
  'Users.aspx',
  'Roles.aspx',
  'RebuildIndex.aspx',
];

test.describe('admin section', () => {
  for (const adminPage of adminPages) {
    test(`${adminPage} renders`, async ({ page }) => {
      const jsErrors = trackPageErrors(page);
      const response = await page.goto(`/Admin/${adminPage}`);
      await expectHealthyPage(page, response);
      await expect(page).not.toHaveURL(/Login\.aspx/i);
      expect(jsErrors, 'JavaScript errors').toEqual([]);
    });
  }

  test('every admin page reachable by link renders', async ({ page, baseURL }) => {
    // page.request shares the logged-in cookies of the browser context.
    const visited = await crawl(page.request, `${baseURL}/Admin/Default.aspx`, {
      maxPages: 150,
      scope: /\/Admin\//i,
      // Links only do GETs, but stay away from anything that sounds destructive.
      exclude: [/logout/i, /delete/i, /remove/i],
    });
    expect(visited.length, 'admin pages crawled').toBeGreaterThan(adminPages.length);
    test.info().annotations.push({ type: 'crawled', description: `${visited.length} pages` });
  });
});
