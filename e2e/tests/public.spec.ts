import { test, expect } from '@playwright/test';
import { crawl, expectHealthyPage, trackPageErrors } from './helpers';

test.describe('public site', () => {
  test('home page renders without errors', async ({ page }) => {
    const jsErrors = trackPageErrors(page);
    const response = await page.goto('/');
    await expectHealthyPage(page, response);
    await expect(page.locator('body')).not.toBeEmpty();
    expect(jsErrors, 'JavaScript errors').toEqual([]);
  });

  test('every page reachable from the home page renders', async ({ request, baseURL }) => {
    const visited = await crawl(request, `${baseURL}/Default.aspx`, {
      maxPages: 75,
      exclude: [/logout/i, /\/Admin\//i],
    });
    expect(visited.length, 'pages crawled').toBeGreaterThan(1);
    test.info().annotations.push({ type: 'crawled', description: `${visited.length} pages` });
  });

  test('login page shows the login form', async ({ page }) => {
    const response = await page.goto('/Login.aspx');
    await expectHealthyPage(page, response);
    await expect(page.locator('[id$="txtUsername"]')).toBeVisible();
    await expect(page.locator('[id$="txtPassword"]')).toBeVisible();
  });

  test('login with a wrong password is rejected', async ({ page }) => {
    await page.goto('/Login.aspx');
    await page.locator('[id$="txtUsername"]').fill('e2e-nonexistent-user');
    await page.locator('[id$="txtPassword"]').fill('wrong-password');
    await page.locator('[id$="btnLogin"]').click();
    await expect(page).toHaveURL(/Login\.aspx/i);
    await expect(page.locator('[id$="lblError"]')).toBeVisible();
  });

  test('admin pages require login', async ({ page }) => {
    await page.goto('/Admin/Default.aspx');
    await expect(page).toHaveURL(/Login\.aspx\?ReturnUrl=/i);
  });

  test('sitemap.xml is valid XML with urls', async ({ request }) => {
    const response = await request.get('/sitemap.xml');
    expect(response.status()).toBe(200);
    const body = await response.text();
    expect(body).toMatch(/<urlset[\s>]/);
    expect(body).toMatch(/<loc>/);
  });

  test('RSS endpoint responds', async ({ request }) => {
    const response = await request.get('/Rss.aspx');
    expect(response.status()).toBeLessThan(400);
  });

  test('runtime folders are not served', async ({ request }) => {
    for (const url of ['/Config/properties.config', '/index/segments', '/files/test.txt']) {
      const response = await request.get(url);
      expect.soft(response.status(), url).toBeGreaterThanOrEqual(400);
    }
  });
});
