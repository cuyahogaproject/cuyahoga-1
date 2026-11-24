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

  test('absolute urls use the address of the site, not the one of the forwarded WebForms site', async ({ request, baseURL }) => {
    // The sitemap urls come from the site url, which the installer took from the install request.
    const body = await (await request.get('/sitemap.xml')).text();
    const locations = [...body.matchAll(/<loc>([^<]*)<\/loc>/g)].map((match) => match[1]);
    expect(locations.length).toBeGreaterThan(0);
    for (const location of locations) {
      expect(location.startsWith(`${baseURL}/`), location).toBe(true);
    }
  });

  test('the installer of an installed site', async ({ page }) => {
    await page.goto('/Install/Install.aspx');
    await expect(page.locator('#lblError')).toContainText("Can't install Cuyahoga.");
    await expect(page.locator('input[type="submit"]')).toHaveCount(0);
    // The later steps need the cookie of the browser that installed the database.
    const token = await page.locator('input[name="__RequestVerificationToken"]').inputValue();
    const response = await page.request.post('/Install/Install.aspx', {
      form: { __RequestVerificationToken: token, btnCreateSite: 'Yes, create a site' },
    });
    expect(response.status()).toBe(403);
  });

  test('runtime folders are not served', async ({ request }) => {
    for (const url of ['/Web.config', '/Web.local.config', '/appsettings.json', '/index/segments', '/files/test.txt',
      '/Install/Core/Database/mssql2000/install.sql', '/Modules/Articles/Install/Database/mssql2000/install.sql',
      '/Modules/User/EmailTemplates/Register.txt', '/App_Data/index/segments']) {
      const response = await request.get(url);
      expect.soft(response.status(), url).toBeGreaterThanOrEqual(400);
    }
  });

  test('the public files of the site and the modules are served', async ({ request }) => {
    for (const url of ['/Admin/Css/Admin.css', '/Templates/AnotherRed/Css/red.css', '/Images/feed-icon.png',
      '/Support/ckeditor/ckeditor.js', '/Support/fileman/index.html', '/Modules/Downloads/Images/pdf.gif',
      '/Modules/LanguageSwitcher/Images/go.gif']) {
      const response = await request.get(url);
      expect.soft(response.status(), url).toBe(200);
    }
  });
});
