import { test, expect, type Browser, type BrowserContext, type Page, type TestInfo } from '@playwright/test';
import { adminStorageState } from '../playwright.config';
import { createArticle, deleteArticle, deleteDownload, expectHealthyPage, uploadDownload } from './helpers';

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
  await expectSameLook(page, legacyPage, url, testInfo);
}

/** The current page of the ASP.NET Core site looks exactly the same as the current WebForms page. */
async function expectSameLook(page: Page, legacyPage: Page, label: string, testInfo: TestInfo) {
  expect(await isWebFormsPage(page), `${label} rendered by the ASP.NET Core site`).toBe(false);
  expect(await isWebFormsPage(legacyPage), `${label} rendered by the WebForms site`).toBe(true);
  const screenshot = await page.screenshot({ fullPage: true });
  const legacyScreenshot = await legacyPage.screenshot({ fullPage: true });
  if (!screenshot.equals(legacyScreenshot)) {
    await testInfo.attach(`${label} (ASP.NET Core)`, { body: screenshot, contentType: 'image/png' });
    await testInfo.attach(`${label} (WebForms)`, { body: legacyScreenshot, contentType: 'image/png' });
    await testInfo.attach(`${label} (ASP.NET Core).html`, { body: await page.content(), contentType: 'text/plain' });
    await testInfo.attach(`${label} (WebForms).html`, { body: await legacyPage.content(), contentType: 'text/plain' });
  }
  expect(screenshot.equals(legacyScreenshot), `${label} looks the same as on the WebForms site`).toBe(true);
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
    for (const url of ['/', '/Default.aspx', '/home.aspx']) {
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

test.describe('the Downloads module in the ASP.NET Core site', () => {
  test('the list of files and the downloads', async ({ page, browser, baseURL }, testInfo) => {
    const admin = await browser.newContext({ storageState: adminStorageState });
    const legacy = await legacyContext(browser, baseURL!, false);
    const legacyAdmin = await legacyContext(browser, baseURL!, true);
    const adminPage = await admin.newPage();
    await expectHealthyPage(adminPage, await adminPage.goto('/home/downloads.aspx'));
    const { sectionId } = await sectionIds(adminPage);
    const adminUrl = new URL(
      (await adminPage.locator('.moduletools a', { hasText: /^Edit$/ }).getAttribute('href'))!, baseURL).toString();
    expect(adminUrl).toMatch(/\/Modules\/Downloads\/EditDownloads\.aspx\?NodeId=\d+&SectionId=\d+$/);

    const stamp = Date.now().toString(36);
    const contents = `e2e download contents ${stamp}`;
    const fileId = await uploadDownload(adminPage, adminUrl, `e2e public download ${stamp}`, `e2e-${stamp}.txt`, contents);
    const membersFileId = await uploadDownload(adminPage, adminUrl, `e2e members download ${stamp}`,
      `e2e-members-${stamp}.txt`, 'for members', ['Anonymous user']);
    try {
      // The list looks the same as on the WebForms site, for visitors and for administrators.
      await expectSameAsWebForms(page, await legacy.newPage(), '/home/downloads.aspx', testInfo);
      await expectSameAsWebForms(adminPage, await legacyAdmin.newPage(), '/home/downloads.aspx', testInfo);

      const downloadUrl = `/${sectionId}/section.aspx/download/${fileId}`;
      const fileLinks = page.locator(`a[href="${downloadUrl}"]`);
      await expect(fileLinks).toHaveCount(2);
      await expect(fileLinks.nth(1)).toHaveText(`e2e public download ${stamp}`);
      await expect(fileLinks.nth(1)).toHaveAttribute('title', `e2e-${stamp}.txt (${contents.length} bytes)`);
      // Visitors don't see the file for members.
      await expect(page.locator('body', { hasText: `e2e members download ${stamp}` })).toHaveCount(0);
      await expect(adminPage.getByText(`e2e members download ${stamp}`)).toBeVisible();

      // Download, and resume a download.
      const download = await page.request.get(downloadUrl);
      expect(download.status()).toBe(200);
      expect(download.headers()['content-disposition']).toMatch(new RegExp(`^attachment; filename=e2e-${stamp}\.txt`));
      expect(download.headers()['content-type']).toMatch(/^text\/plain/);
      expect(await download.text()).toBe(contents);
      const resumed = await page.request.get(downloadUrl, { headers: { Range: 'bytes=4-' } });
      expect(resumed.status()).toBe(206);
      expect(resumed.headers()['content-range']).toBe(`bytes 4-${contents.length - 1}/${contents.length}`);
      expect(await resumed.text()).toBe(contents.substring(4));

      // A file that doesn't exist, and a file for members only.
      expect((await page.request.get(`/${sectionId}/section.aspx/download/999999`)).status()).toBe(404);
      const membersDownloadUrl = `/${sectionId}/section.aspx/download/${membersFileId}`;
      expect((await page.request.get(membersDownloadUrl)).status()).toBe(403);
      const membersDownload = await adminPage.request.get(membersDownloadUrl);
      expect(membersDownload.status()).toBe(200);
      expect(await membersDownload.text()).toBe('for members');
    } finally {
      await deleteDownload(adminPage, adminUrl, fileId);
      await deleteDownload(adminPage, adminUrl, membersFileId);
      await legacy.close();
      await legacyAdmin.close();
      await admin.close();
    }
  });
});

test.describe('the Articles module in the ASP.NET Core site', () => {
  // The Articles page of the sample site: full content, 5 articles per page, comments (also anonymous), RSS.
  test('the list, a category, an article and its comments', async ({ page, browser, baseURL }, testInfo) => {
    const admin = await browser.newContext({ storageState: adminStorageState });
    const legacy = await legacyContext(browser, baseURL!, false);
    const legacyAdmin = await legacyContext(browser, baseURL!, true);
    const adminPage = await admin.newPage();
    const legacyPage = await legacy.newPage();
    await expectHealthyPage(adminPage, await adminPage.goto('/page1.aspx'));
    const { sectionId } = await sectionIds(adminPage);
    const adminUrl = new URL(
      (await adminPage.locator('.moduletools a', { hasText: /^Edit$/ }).getAttribute('href'))!, baseURL).toString();
    expect(adminUrl).toMatch(/\/Modules\/Articles\/AdminArticles\.aspx\?NodeId=\d+&SectionId=\d+$/);

    const stamp = Date.now().toString(36);
    const category = `e2e category ${stamp}`;
    const articleIds: string[] = [];
    try {
      // Six articles: two pages of five.
      for (let i = 1; i <= 6; i++) {
        articleIds.push(await createArticle(adminPage, adminUrl, `e2e article ${i} ${stamp}`, `Summary ${i}`,
          `<p>Content of article ${i}.</p>`, i === 6 ? category : undefined));
      }
      const sectionUrl = `/${sectionId}/section.aspx`;
      const articleUrl = `${sectionUrl}/${articleIds[5]}`;
      // MySQL rounds the date online to whole seconds: a new article can be online just after it was saved.
      await expect.poll(async () => (await page.request.get(articleUrl)).status()).toBe(200);

      // The list and its second page, for visitors and administrators.
      await expectSameAsWebForms(page, legacyPage, '/page1.aspx', testInfo);
      await expect(page.locator('.articlelist > li')).toHaveCount(5);
      await expect(page.locator('.pager a')).toHaveText(['2', '>', '>>']);
      await expect(page.locator('.pager span span')).toHaveText('1');
      await expect(page.locator('.syndicate a')).toHaveAttribute('href', new RegExp(`/${sectionId}/feed\\.aspx$`));
      await expect(page.locator('.articlelist h4 a', { hasText: `e2e article 6 ${stamp}` }))
        .toHaveAttribute('href', articleUrl);
      await expectSameAsWebForms(page, legacyPage, `${sectionUrl}/page/2`, testInfo);
      await expect(page.locator('.pager span span')).toHaveText('2');
      await expectSameAsWebForms(adminPage, await legacyAdmin.newPage(), '/page1.aspx', testInfo);

      // A category.
      await adminPage.goto(adminUrl.replace(/AdminArticles\.aspx/i, 'EditArticle.aspx') + `&ArticleId=${articleIds[5]}`);
      const categoryId = await adminPage.locator('[id$="ddlCategory"] option', { hasText: category }).getAttribute('value');
      await expectSameAsWebForms(page, legacyPage, `${sectionUrl}/category/${categoryId}`, testInfo);
      await expect(page.locator('#content .section h3')).toHaveText(`Category ${category}`);
      await expect(page.locator('.articlelist > li')).toHaveCount(1);

      // An article, for visitors and administrators.
      await expectSameAsWebForms(page, legacyPage, articleUrl, testInfo);
      await expect(page.locator('.articlecontent h4')).toHaveText(`e2e article 6 ${stamp}`);
      await expect(page.locator('.syndicate')).toHaveCount(0);
      await expectSameAsWebForms(adminPage, await legacyAdmin.newPage(), articleUrl, testInfo);

      // A comment without name and text: both sites show what is missing.
      const submit = (p: Page) => p.getByRole('button', { name: 'Submit' });
      await submit(page).click();
      await submit(legacyPage).click();
      await expect(page.getByText('Name is required')).toBeVisible();
      await expect(page.getByText('Comment is required')).toBeVisible();
      await expect(legacyPage.getByText('Comment is required')).toBeVisible();
      await expectSameLook(page, legacyPage, `${articleUrl} (empty comment)`, testInfo);

      // A comment without login, then one of the administrator.
      await page.locator('input[name="name"]').fill(`e2e visitor ${stamp}`);
      await page.locator('input[name="website"]').fill('https://example.com/');
      await page.locator('textarea[name="comment"]').fill('First line\n<b>not bold</b>');
      await submit(page).click();
      await expect(page).toHaveURL(new RegExp(`${articleUrl}$`));
      const visitorComment = page.locator('.articlecomments li').first();
      await expect(visitorComment.locator('p')).toHaveText('First line<b>not bold</b>');
      await expect(visitorComment.locator('a', { hasText: `e2e visitor ${stamp}` }))
        .toHaveAttribute('href', 'https://example.com/');
      await expect(visitorComment.locator('a')).toHaveAttribute('target', '_blank');

      await adminPage.goto(articleUrl);
      await expect(adminPage.locator('input[name="name"]')).toHaveCount(0);
      await adminPage.locator('textarea[name="comment"]').fill(`e2e admin comment ${stamp}`);
      await submit(adminPage).click();
      await expect(adminPage).toHaveURL(new RegExp(`${articleUrl}$`));
      await expect(adminPage.locator('.articlecomments li')).toHaveCount(2);
      await expect(adminPage.locator('.articlecomments li').nth(1)).toContainText(`e2e admin comment ${stamp}`);

      // The article and the list with comments.
      await expectSameAsWebForms(page, legacyPage, articleUrl, testInfo);
      await expectSameAsWebForms(page, legacyPage, '/page1.aspx', testInfo);
      await expect(page.locator('.articlelist a', { hasText: 'Comments 2' })).toHaveAttribute('href', `${articleUrl}#comments`);

      // A comment of a logged-in user needs the token of the form.
      const forged = await adminPage.request.post(articleUrl, { form: { comment: 'forged' } });
      expect(forged.status()).toBe(400);

      // Unknown articles and categories.
      expect((await page.request.get(`${sectionUrl}/999999`)).status()).toBe(404);
      expect((await page.request.get(`${sectionUrl}/category/999999`)).status()).toBe(404);

      // Articles open in their own section only, and not after they went offline (unless the section has an archive).
      expect((await page.request.get(`/${Number(sectionId) + 100000}/section.aspx/${articleIds[5]}`)).status())
        .not.toBe(200);
      await page.goto('/page1.aspx');
      const hasArchive = await page.locator('a[href$="/archive"]').count() > 0;
      articleIds.push(await createArticle(adminPage, adminUrl, `e2e expired ${stamp}`, 'Expired', '<p>Expired.</p>',
        undefined, true));
      expect((await page.request.get(`${sectionUrl}/${articleIds[6]}`)).status()).toBe(hasArchive ? 200 : 404);

      // Titles and summaries are text.
      articleIds.push(await createArticle(adminPage, adminUrl, `e2e markup ${stamp} <i>x</i>`, '<b>summary</b>',
        '<p>Content with <strong>html</strong>.</p>'));
      await expect.poll(async () => (await page.request.get(`${sectionUrl}/${articleIds[7]}`)).status()).toBe(200);
      await page.goto(`${sectionUrl}/${articleIds[7]}`);
      await expect(page.locator('.articlecontent h4')).toHaveText(`e2e markup ${stamp} <i>x</i>`);
      await expect(page.locator('.articlecontent strong')).toHaveText('html');
    } finally {
      for (const articleId of articleIds) {
        await deleteArticle(adminPage, adminUrl, articleId);
      }
      await legacy.close();
      await legacyAdmin.close();
      await admin.close();
    }
  });
});
