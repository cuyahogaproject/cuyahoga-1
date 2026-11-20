import { test, expect, type Browser, type BrowserContext, type Page, type TestInfo } from '@playwright/test';
import { adminStorageState } from '../playwright.config';
import { expectHealthyPage } from './helpers';

// The public pages that the ASP.NET Core site renders itself (PageEngine moved to Web.Core): pages whose template
// and modules have moved. Page 2 of the sample site qualifies (template Another Red, one StaticHtml section); the
// others still come from the WebForms site. Pages are compared with the same page straight from the WebForms site.

const legacyBaseUrl = `http://localhost:${process.env.CUYAHOGA_E2E_LEGACY_PORT ?? '8082'}`;
const offlineMessage = 'This page is currently offline for maintenance.';

/** A browser context on the WebForms site itself, as if the request came through the ASP.NET Core site. */
async function legacyContext(browser: Browser, baseURL: string, asAdmin: boolean): Promise<BrowserContext> {
  return browser.newContext({
    baseURL: legacyBaseUrl,
    extraHTTPHeaders: { 'X-Forwarded-Host': new URL(baseURL).host, 'X-Forwarded-Proto': 'http' },
    storageState: asAdmin ? adminStorageState : undefined,
  });
}

/** WebForms pages have a ViewState field; the pages of the ASP.NET Core site don't. */
async function isWebFormsPage(page: Page): Promise<boolean> {
  return (await page.locator('input[name="__VIEWSTATE"]').count()) > 0;
}

/** The page looks exactly the same (pixel for pixel) as the page at the same url on the WebForms site. */
async function expectSameAsWebForms(page: Page, legacyPage: Page, url: string, testInfo: TestInfo) {
  await expectHealthyPage(page, await page.goto(url));
  await expectHealthyPage(legacyPage, await legacyPage.goto(url));
  expect(await isWebFormsPage(page), `${url} rendered by the ASP.NET Core site`).toBe(false);
  expect(await isWebFormsPage(legacyPage), `${url} rendered by the WebForms site`).toBe(true);
  const screenshot = await page.screenshot({ fullPage: true });
  const legacyScreenshot = await legacyPage.screenshot({ fullPage: true });
  if (!screenshot.equals(legacyScreenshot)) {
    await testInfo.attach(`${url} (ASP.NET Core)`, { body: screenshot, contentType: 'image/png' });
    await testInfo.attach(`${url} (WebForms)`, { body: legacyScreenshot, contentType: 'image/png' });
  }
  expect(screenshot.equals(legacyScreenshot), `${url} looks the same as on the WebForms site`).toBe(true);
}

/** The node and section ids of the first section on the page, from its edit links (administrators only). */
async function sectionIds(page: Page): Promise<{ nodeId: string; sectionId: string }> {
  const href = await page.locator('.moduletools a', { hasText: 'Section Properties' }).first().getAttribute('href');
  const match = /NodeId=(\d+)&SectionId=(\d+)/i.exec(href!)!;
  return { nodeId: match[1], sectionId: match[2] };
}

test.describe('public pages of the ASP.NET Core site', () => {
  test('Page 2 for a visitor', async ({ page, browser, baseURL }, testInfo) => {
    const legacy = await legacyContext(browser, baseURL!, false);
    try {
      await expectSameAsWebForms(page, await legacy.newPage(), '/page2.aspx', testInfo);
    } finally {
      await legacy.close();
    }

    await expect(page).toHaveTitle('Cuyahoga Sample Site - Page 2');
    await expect(page.locator('link[rel="stylesheet"]')).toHaveAttribute('href', '/Templates/AnotherRed/Css/red.css');
    await expect(page.locator('#nav a.selected')).toHaveText('Page 2');
    await expect(page.locator('#nav a', { hasText: 'Admin' })).toHaveCount(0);
    await expect(page.locator('#content .section h3')).toHaveText('Page 2');
    await expect(page.locator('.moduletools')).toHaveCount(0);
  });

  test('Page 2 for an administrator, under all its urls', async ({ browser, baseURL }, testInfo) => {
    const context = await browser.newContext({ storageState: adminStorageState });
    const legacy = await legacyContext(browser, baseURL!, true);
    try {
      const page = await context.newPage();
      await expectSameAsWebForms(page, await legacy.newPage(), '/page2.aspx', testInfo);

      await expect(page.locator('#nav a', { hasText: 'Admin' })).toHaveAttribute('href', '/Admin');
      const { nodeId, sectionId } = await sectionIds(page);
      const tools = page.locator('#content .moduletools');
      await expect(tools.getByRole('link', { name: 'Edit', exact: true }))
        .toHaveAttribute('href', `/Modules/StaticHtml/EditHtml.aspx?NodeId=${nodeId}&SectionId=${sectionId}`);
      await expect(tools.getByRole('link', { name: 'Page Properties' }))
        .toHaveAttribute('href', `/Admin/NodeEdit.aspx?NodeId=${nodeId}`);

      for (const url of [`/${nodeId}/view.aspx`, `/${sectionId}/section.aspx`, `/Default.aspx?NodeId=${nodeId}`]) {
        await expectHealthyPage(page, await page.goto(url));
        expect(await isWebFormsPage(page), `${url} rendered by the ASP.NET Core site`).toBe(false);
        await expect(page.locator('#content .section h3'), url).toHaveText('Page 2');
      }
      // Without the preview flag the edit links are there; in the preview of the page they aren't.
      await page.goto('/page2.aspx?preview=on');
      await expect(page.locator('.moduletools a')).toHaveCount(0);
    } finally {
      await legacy.close();
      await context.close();
    }
  });

  test('pages with modules that have not moved yet still come from the WebForms site', async ({ page }) => {
    for (const url of ['/', '/Default.aspx', '/home.aspx', '/page1.aspx']) {
      await expectHealthyPage(page, await page.goto(url));
      expect(await isWebFormsPage(page), `${url} rendered by the WebForms site`).toBe(true);
    }
    // Unknown pages: the not-found handling of the WebForms site.
    const response = await page.goto('/does-not-exist.aspx');
    expect(response!.status()).toBeGreaterThanOrEqual(400);
  });

  test('an offline page shows the offline template to visitors and the page to administrators',
    async ({ page, browser, baseURL }, testInfo) => {
      const admin = await browser.newContext({ storageState: adminStorageState });
      const legacy = await legacyContext(browser, baseURL!, false);
      const adminPage = await admin.newPage();
      await adminPage.goto('/page2.aspx');
      const { nodeId } = await sectionIds(adminPage);
      const toggleStatus = async (message: string) => {
        await adminPage.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
        adminPage.once('dialog', (dialog) => dialog.accept());
        await adminPage.locator('[id$="btnOffline"]').click();
        await expect(adminPage.locator('[id$="MessageBox"]')).toContainText(message);
      };

      await toggleStatus('Node is now offline.');
      try {
        await expectSameAsWebForms(page, await legacy.newPage(), '/page2.aspx', testInfo);
        await expect(page.getByText(offlineMessage)).toBeVisible();
        await expect(page.locator('.section')).toHaveCount(0);

        await adminPage.goto('/page2.aspx');
        expect(await isWebFormsPage(adminPage)).toBe(false);
        await expect(adminPage.getByText(offlineMessage)).toHaveCount(0);
        await expect(adminPage.locator('#content .section h3')).toHaveText('Page 2');

        // Showing the offline template doesn't change the template of the page.
        await adminPage.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
        await expect(adminPage.locator('[id$="ddlTemplates"] option:checked')).toHaveText('Another Red');
      } finally {
        await toggleStatus('Node is now online.');
        await legacy.close();
        await admin.close();
      }
      await page.goto('/page2.aspx');
      await expect(page.locator('#content .section h3')).toHaveText('Page 2');
    });
});
