import { test, expect, type Browser, type BrowserContext, type Page, type TestInfo } from '@playwright/test';
import { adminStorageState } from '../playwright.config';
import {
  adminCredentials, createArticle, createNode, createSection, deleteArticle, deleteDownload, deleteNode,
  expectHealthyPage, homeNodeId, sectionIdByTitle, updateSectionSettings, uploadDownload,
} from './helpers';

// The public pages that the ASP.NET Core site renders itself (PageEngine moved to Web.Core): pages whose template
// and modules have moved: all modules and the templates of the sample site have.
// Pages are compared with the same page straight from the WebForms site.

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

  test('unknown pages go to the not-found handling of the WebForms site', async ({ page }) => {
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

      // The RSS feeds of the section and of a category are the same as the ones of the WebForms site (apart from the
      // time they were made: the first pubDate and the lastBuildDate of the channel).
      const withoutBuildTime = (xml: string) =>
        xml.replace(/<pubDate>[^<]*<\/pubDate>/, '').replace(/<lastBuildDate>[^<]*<\/lastBuildDate>/, '');
      for (const feedUrl of [`/${sectionId}/feed.aspx`, `/${sectionId}/feed.aspx/category/${categoryId}`]) {
        const feed = await page.request.get(feedUrl);
        const legacyFeed = await legacy.request.get(feedUrl);
        expect(feed.status(), feedUrl).toBe(200);
        expect(feed.headers()['content-type'], feedUrl).toBe(legacyFeed.headers()['content-type']);
        expect(withoutBuildTime(await feed.text()), feedUrl).toBe(withoutBuildTime(await legacyFeed.text()));
      }
      expect(await (await page.request.get(`/${sectionId}/feed.aspx`)).text())
        .toContain(`<link>${baseURL}${articleUrl}</link>`);
      expect((await page.request.get('/999999/feed.aspx')).status()).toBe(404);

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

test.describe('the Sitemap module in the ASP.NET Core site', () => {
  test('all pages the user may view', async ({ page, browser, baseURL }, testInfo) => {
    const admin = await browser.newContext({ storageState: adminStorageState });
    const legacy = await legacyContext(browser, baseURL!, false);
    const legacyAdmin = await legacyContext(browser, baseURL!, true);
    try {
      await expectSameAsWebForms(page, await legacy.newPage(), '/home/overview.aspx', testInfo);
      const sitemap = page.locator('#sitemap');
      await expect(sitemap.locator('> ul > li > a').first()).toHaveText('Home');
      await expect(sitemap.locator('a', { hasText: 'Page 2' })).toHaveAttribute('href', '/page2.aspx');
      await expect(sitemap.locator('a', { hasText: 'Downloads' })).toHaveAttribute('href', '/home/downloads.aspx');
      await expectSameAsWebForms(await admin.newPage(), await legacyAdmin.newPage(), '/home/overview.aspx', testInfo);

      // sitemap.aspx (in any folder) and sitemap.xml are the XML sitemap of the WebForms site.
      for (const url of ['/sitemap.aspx', '/sitemap.xml', '/home/sitemap.aspx']) {
        const response = await page.request.get(url);
        expect(response.status(), url).toBe(200);
        expect(await response.text(), url).toContain('<urlset');
      }
    } finally {
      await legacy.close();
      await legacyAdmin.close();
      await admin.close();
    }
  });
});

test.describe('the Search module in the ASP.NET Core site', () => {
  /** The search duration differs per request: show the same number on both sites before comparing. */
  async function sameDuration(page: Page, legacyPage: Page) {
    await page.locator('.searchduration').evaluate((span) => { span.textContent = '0.01'; });
    await legacyPage.locator('[id$="lblDuration"]').evaluate((span) => { span.textContent = '0.01'; });
  }

  const setResultsPerPage = (adminPage: Page, nodeId: string, sectionId: string, value: string) =>
    updateSectionSettings(adminPage, nodeId, sectionId, { RESULTS_PER_PAGE: value });

  test('the search page, results with pages, and the search box', async ({ page, browser, baseURL }, testInfo) => {
    const admin = await browser.newContext({ storageState: adminStorageState });
    const legacy = await legacyContext(browser, baseURL!, false);
    const legacyAdmin = await legacyContext(browser, baseURL!, true);
    const adminPage = await admin.newPage();
    const legacyPage = await legacy.newPage();
    await expectSameAsWebForms(page, legacyPage, '/home/search.aspx', testInfo);
    await expectSameAsWebForms(adminPage, await legacyAdmin.newPage(), '/home/search.aspx', testInfo);
    const { nodeId, sectionId } = await sectionIds(adminPage);

    await adminPage.goto('/page1.aspx');
    const articlesAdminUrl = new URL(
      (await adminPage.locator('.moduletools a', { hasText: /^Edit$/ }).getAttribute('href'))!, baseURL).toString();
    const word = `zs${Date.now().toString(36)}`;
    const articleIds: string[] = [];
    await setResultsPerPage(adminPage, nodeId, sectionId, '2');
    try {
      for (let i = 1; i <= 3; i++) {
        articleIds.push(await createArticle(adminPage, articlesAdminUrl, `e2e search ${i} ${word}`, `Summary ${i}`,
          `<p>Content ${i} ${word}</p>`));
      }
      const searchUrl = `/${sectionId}/section.aspx/Search?q=${word}`;

      // The results and their second page.
      for (const [url, count] of [[searchUrl, 2], [`${searchUrl}&page=2`, 1]] as const) {
        await expectHealthyPage(page, await page.goto(url));
        await expectHealthyPage(legacyPage, await legacyPage.goto(url));
        await expect(page.locator('.searchresults li')).toHaveCount(count);
        await sameDuration(page, legacyPage);
        await expectSameLook(page, legacyPage, url, testInfo);
      }
      await expect(page.locator('.pager a')).toHaveText(['<<', '<', '1']);
      await expect(page.locator('.pager a').first()).toHaveAttribute('href', `${searchUrl}&page=1`);
      await expect(page.locator('input[name="q"]')).toHaveValue(word);

      // Nothing found, and an invalid query.
      await expectSameAsWebForms(page, legacyPage, `/${sectionId}/section.aspx/Search?q=nothing${word}`, testInfo);
      await expect(page.getByText('No results found')).toBeVisible();
      await expectSameAsWebForms(page, legacyPage, `/${sectionId}/section.aspx/Search?q=%22`, testInfo);

      // The search form of the page, and the search box on another page.
      await page.goto('/home/search.aspx');
      await page.locator('input[name="q"]').fill(word);
      await page.getByRole('button', { name: 'Search' }).click();
      await expect(page).toHaveURL(new RegExp(`/${sectionId}/section\\.aspx/Search\\?q=${word}$`));
      await expect(page.locator('.searchresults li')).toHaveCount(2);
      await page.goto('/home/overview.aspx');
      await page.locator('input[name="q"]').fill(word);
      await page.locator('input[type="submit"]').click();
      await expect(page).toHaveURL(new RegExp(`/${sectionId}/section\\.aspx/Search\\?q=${word}$`));
      await expect(page.locator('.searchresults li')).toHaveCount(2);
    } finally {
      for (const articleId of articleIds) {
        await deleteArticle(adminPage, articlesAdminUrl, articleId);
      }
      await setResultsPerPage(adminPage, nodeId, sectionId, '10');
      await legacy.close();
      await legacyAdmin.close();
      await admin.close();
    }
  });
});

test.describe('the LanguageSwitcher module in the ASP.NET Core site', () => {
  test('links, flags and a list of the other languages, and the language of the browser',
    async ({ page, browser, baseURL }, testInfo) => {
      const admin = await browser.newContext({ storageState: adminStorageState });
      const legacy = await legacyContext(browser, baseURL!, false);
      const adminPage = await admin.newPage();
      const legacyPage = await legacy.newPage();

      // A Dutch home page with a language switcher.
      await adminPage.goto('/Admin/NodeEdit.aspx?NodeId=-1&SiteId=1');
      await adminPage.locator('[id$="txtTitle"]').fill(`e2e nl ${Date.now().toString(36)}`);
      await adminPage.locator('[id$="ddlCultures"]').selectOption('nl-NL');
      // A root node has no permissions of a parent: everyone may view it.
      for (const viewAllowed of await adminPage.locator('input[id$="chkViewAllowed"]').all()) {
        await viewAllowed.check();
      }
      await adminPage.locator('[id$="btnSave"]').click();
      await expect(adminPage).toHaveURL(/NodeEdit\.aspx\?NodeId=\d+&message=/i);
      const nodeId = /NodeId=(\d+)/i.exec(adminPage.url())![1];
      try {
        const dutchUrl = `/${await adminPage.locator('[id$="txtShortDescription"]').inputValue()}.aspx`;
        await createSection(adminPage, nodeId, 'Language', 'LanguageSwitcher', { DISPLAY_MODE: 'Text' });
        const sectionId = await sectionIdByTitle(adminPage, nodeId, 'Language');
        const setMode = (mode: string) => updateSectionSettings(adminPage, nodeId, sectionId, { DISPLAY_MODE: mode });

        await expectSameAsWebForms(page, legacyPage, dutchUrl, testInfo);
        await expect(page.locator('.languageswitcher a')).toHaveText(['English']);
        await expect(page.locator('.languageswitcher a')).toHaveAttribute('href', '/home.aspx');

        await setMode('Flag');
        await expectSameAsWebForms(page, legacyPage, dutchUrl, testInfo);
        await expect(page.locator('.languageswitcher a img'))
          .toHaveAttribute('src', '/Modules/LanguageSwitcher/Images/flags/us.png');

        // The list goes to the home page of the language, and remembers the language for the start page.
        await setMode('DropDown');
        await expectSameAsWebForms(page, legacyPage, dutchUrl, testInfo);
        await expect(page.locator('.languageswitcher select')).toHaveValue('nl-NL');
        await page.locator('.languageswitcher select').selectOption('en-US');
        await page.locator('.languageswitcher input[type="image"]').click();
        await expect(page).toHaveURL(/\/home\.aspx$/);
        const cookies = await page.context().cookies();
        expect(cookies.find((cookie) => cookie.name === 'CuyahogaCulture')?.value).toBe('en-US');
        await page.goto(`${dutchUrl}?culture=nl-NL`);
        await expect(page).toHaveURL(new RegExp(`${dutchUrl.replace('.', '\.')}$`));
        await page.goto('/');
        expect(await isWebFormsPage(page), 'the Dutch start page').toBe(false);
        await expect(page.locator('.languageswitcher select')).toHaveValue('nl-NL');
        await page.context().clearCookies({ name: 'CuyahogaCulture' });

        // The start page goes to the home page of the language of the browser.
        await updateSectionSettings(adminPage, nodeId, sectionId, { REDIRECT_TO_USER_LANGUAGE: true });
        for (const site of [baseURL!, legacyBaseUrl]) {
          const start = await page.request.get(`${site}/`, {
            maxRedirects: 0,
            headers: {
              'Accept-Language': 'en-US,en;q=0.9',
              Cookie: 'CuyahogaCulture=nl-NL',
              'X-Forwarded-Host': new URL(baseURL!).host,
            },
          });
          expect(start.status(), `the start page of ${site}`).toBe(302);
          expect(start.headers()['location'], `the start page of ${site}`).toMatch(/\/home\.aspx$/);
        }
        const dutchBrowser = await page.request.get('/', {
          maxRedirects: 0,
          headers: { 'Accept-Language': 'nl-NL,nl;q=0.9', Cookie: 'CuyahogaCulture=nl-NL' },
        });
        expect(dutchBrowser.status()).toBe(200);
        // Not on other pages.
        const dutchPage = await page.request.get(dutchUrl, {
          maxRedirects: 0,
          headers: { 'Accept-Language': 'en-US,en;q=0.9' },
        });
        expect(dutchPage.status()).toBe(200);
      } finally {
        await deleteNode(adminPage, nodeId);
        await legacy.close();
        await admin.close();
      }
    });
});

test.describe('the RemoteContent module in the ASP.NET Core site', () => {
  test('the items of the feeds of a section', async ({ page, browser, baseURL }, testInfo) => {
    const admin = await browser.newContext({ storageState: adminStorageState });
    const legacy = await legacyContext(browser, baseURL!, false);
    const adminPage = await admin.newPage();
    const stamp = Date.now().toString(36);

    // The feed: the RSS feed of the Articles section of this site, with a new article.
    await adminPage.goto('/page1.aspx');
    const { sectionId: articlesSectionId } = await sectionIds(adminPage);
    const articlesAdminUrl = new URL(
      (await adminPage.locator('.moduletools a', { hasText: /^Edit$/ }).getAttribute('href'))!, baseURL).toString();
    const articleId = await createArticle(adminPage, articlesAdminUrl, `e2e feed item <${stamp}>`, 'Feed summary',
      `<p>Feed <b>content</b> ${stamp}</p>`);
    const nodeId = await createNode(adminPage, await homeNodeId(adminPage), `e2e feeds ${stamp}`);
    await createSection(adminPage, nodeId, 'Feeds', 'RemoteContent', {
      CACHE_DURATION: '60', SHOW_CONTENTS: true, SHOW_DATES: true, SHOW_SOURCES: true, SHOW_AUTHORS: true,
    });
    const sectionId = await sectionIdByTitle(adminPage, nodeId, 'Feeds');
    try {
      await adminPage.goto(`/Modules/RemoteContent/EditFeed.aspx?NodeId=${nodeId}&SectionId=${sectionId}&FeedId=-1`);
      await adminPage.locator('[id$="txtUrl"]').fill(`${baseURL}/${articlesSectionId}/feed.aspx`);
      await adminPage.locator('[id$="btnVerify"]').click();
      await expect(adminPage.getByText('Feed is valid')).toBeVisible();
      await adminPage.locator('[id$="txtNumberOfItems"]').fill('5');
      await adminPage.locator('[id$="btnSave"]').click();
      await expect(adminPage).toHaveURL(/AdminRemoteContent\.aspx/i);
      // Saving doesn't fetch the items.
      await adminPage.locator('a[id$="lbtRefresh"]').click();
      await adminPage.waitForLoadState('networkidle');

      await adminPage.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
      const url = `/${await adminPage.locator('[id$="txtShortDescription"]').inputValue()}.aspx`;
      await expectSameAsWebForms(page, await legacy.newPage(), url, testInfo);
      const item = page.locator('.genericitem', { hasText: `e2e feed item <${stamp}>` });
      await expect(item.locator('h4 a')).toHaveAttribute('href', new RegExp(`/${articlesSectionId}/section\.aspx/${articleId}$`));
      // The contents of a feed are text.
      await expect(item.locator('div').first()).toHaveText(`Feed content ${stamp}`);
      await expect(item.locator('b')).toHaveCount(0);
    } finally {
      // The feed, then the page and the article.
      await adminPage.goto(`/Modules/RemoteContent/AdminRemoteContent.aspx?NodeId=${nodeId}&SectionId=${sectionId}`);
      const edit = adminPage.locator('a', { hasText: /^Edit$/ });
      if (await edit.count() > 0) {
        await edit.first().click();
        adminPage.once('dialog', (dialog) => dialog.accept());
        await adminPage.locator('[id$="btnDelete"]').click();
        await expect(adminPage).toHaveURL(/AdminRemoteContent\.aspx/i);
      }
      await deleteNode(adminPage, nodeId);
      await deleteArticle(adminPage, articlesAdminUrl, articleId);
      await legacy.close();
      await admin.close();
    }
  });
});

test.describe('the User module in the ASP.NET Core site', () => {
  test('the login form, logging in and out', async ({ page, browser, baseURL }, testInfo) => {
    const legacy = await legacyContext(browser, baseURL!, false);
    const legacyAdmin = await legacyContext(browser, baseURL!, true);
    const legacyPage = await legacy.newPage();
    try {
      for (const url of ['/', '/Default.aspx', '/home.aspx']) {
        await expectSameAsWebForms(page, legacyPage, url, testInfo);
      }
      const login = (p: Page) => p.getByRole('button', { name: 'Login' });

      // Without username and password: the message (the WebForms site shows it after the postback).
      await login(page).click();
      await expect(page).toHaveURL(/\/home\.aspx\?loginerror=missing$/);
      await login(legacyPage).click();
      await expect(page.getByText('Please enter both a username and a password')).toBeVisible();
      await expectSameLook(page, legacyPage, '/home.aspx (login without username)', testInfo);

      // A wrong password.
      const credentials = adminCredentials();
      await page.locator('input[name="username"]').fill(credentials.username);
      await page.locator('input[name="password"]').fill('wrong');
      await login(page).click();
      await expect(page).toHaveURL(/\/home\.aspx\?loginerror=invalid$/);
      await expect(page.getByText('Invalid username or password')).toBeVisible();

      // Log in: the login cookie of the WebForms site, also for the admin pages.
      await page.locator('input[name="username"]').fill(credentials.username);
      await page.locator('input[name="password"]').fill(credentials.password);
      await login(page).click();
      await expect(page).toHaveURL(/\/home\.aspx$/);
      await expect(page.getByText(`Logged in as ${credentials.username}`)).toBeVisible();
      await expectSameAsWebForms(page, await legacyAdmin.newPage(), '/home.aspx', testInfo);
      await expectHealthyPage(page, await page.goto('/Admin/Default.aspx'));
      await expect(page).toHaveURL(/\/Admin\/Default\.aspx$/i);

      // Log out.
      await page.goto('/home.aspx');
      await page.getByRole('button', { name: 'Logout' }).click();
      await expect(page).toHaveURL(/\/home\.aspx$/);
      await expect(login(page)).toBeVisible();
      await page.goto('/Admin/Default.aspx');
      await expect(page).toHaveURL(/Login\.aspx/i);

      // Only back to this site, and only from its pages.
      const form = { action: 'login', username: 'x', password: 'y', returnUrl: '/home.aspx' };
      expect((await page.request.post('/UserLogin.axd', { form, maxRedirects: 0 })).status()).toBe(302);
      expect((await page.request.post('/UserLogin.axd', {
        form: { ...form, returnUrl: '//example.com/' }, maxRedirects: 0,
      })).status()).toBe(400);
      expect((await page.request.post('/UserLogin.axd', {
        form, maxRedirects: 0, headers: { Origin: 'http://example.com' },
      })).status()).toBe(400);
    } finally {
      await legacy.close();
      await legacyAdmin.close();
    }
  });
});

test.describe('the UserProfile module in the ASP.NET Core site', () => {
  test('a profile, registering, resetting the password and editing the profile',
    async ({ page, browser, baseURL }, testInfo) => {
      const admin = await browser.newContext({ storageState: adminStorageState });
      const legacy = await legacyContext(browser, baseURL!, false);
      const legacyAdmin = await legacyContext(browser, baseURL!, true);
      const adminPage = await admin.newPage();
      const legacyPage = await legacy.newPage();
      const legacyAdminPage = await legacyAdmin.newPage();
      await adminPage.goto('/userprofile.aspx');
      const { sectionId } = await sectionIds(adminPage);
      const sectionUrl = `/${sectionId}/section.aspx`;
      const stamp = Date.now().toString(36);
      try {
        // Visitors: the edit page asks to log in.
        for (const url of ['/userprofile.aspx', `${sectionUrl}/ViewProfile/1`, `${sectionUrl}/ViewProfile/999999`,
          `${sectionUrl}/Register`, `${sectionUrl}/ResetPassword`]) {
          await expectSameAsWebForms(page, legacyPage, url, testInfo);
        }
        await page.goto(`${sectionUrl}/ViewProfile/1`);
        await expect(page.locator('#content h3').last()).toHaveText('Profile of admin');

        // Register: what is missing, an invalid address, then a new user. There is no mail server for the tests, so
        // the registration fails on both sites (and the new user is removed again).
        const fill = async (name: string, legacyId: string, value: string) => {
          await page.locator(`input[name="${name}"]`).fill(value);
          await legacyPage.locator(`input[id$="${legacyId}"]`).fill(value);
        };
        const submit = async (text: string, label: string) => {
          await page.getByRole('button', { name: text }).click();
          await legacyPage.getByRole('button', { name: text }).click();
          await expectSameLook(page, legacyPage, label, testInfo);
        };
        await page.goto(`${sectionUrl}/Register`);
        await legacyPage.goto(`${sectionUrl}/Register`);
        await submit('Register', 'register without username and e-mail address');
        await expect(page.getByText('Username is required')).toBeVisible();
        await fill('username', 'txtUsername', `e2e${stamp}`);
        await fill('email', 'txtEmail', 'not an address');
        await submit('Register', 'register with an invalid e-mail address');
        await fill('email', 'txtEmail', `e2e${stamp}@example.com`);
        await submit('Register', 'register');

        // Reset the password of an unknown user.
        await page.goto(`${sectionUrl}/ResetPassword`);
        await legacyPage.goto(`${sectionUrl}/ResetPassword`);
        await fill('username', 'txtUsername', `nobody${stamp}`);
        await fill('email', 'txtEmail', `nobody${stamp}@example.com`);
        await submit('Reset password', 'reset the password of an unknown user');
        await expect(page.getByText('No user found with the given username and email')).toBeVisible();

        // When the e-mail with the new password can't be sent, the user keeps the old password.
        const username = `e2e-reset-${stamp}`;
        await adminPage.goto('/Admin/UserEdit.aspx?UserId=-1');
        await adminPage.locator('[id$="txtUsername"]').fill(username);
        await adminPage.locator('[id$="txtEmail"]').fill(`${username}@example.com`);
        await adminPage.locator('[id$="chkActive"]').check();
        await adminPage.locator('[id$="txtPassword1"]').fill(`pw-${stamp}`);
        await adminPage.locator('[id$="txtPassword2"]').fill(`pw-${stamp}`);
        await adminPage.locator('[id$="btnSave"]').click();
        await expect(adminPage).toHaveURL(/UserEdit\.aspx\?UserId=\d+&message=/i);
        const userId = /UserId=(\d+)/i.exec(adminPage.url())![1];
        try {
          await page.goto(`${sectionUrl}/ResetPassword`);
          await page.locator('input[name="username"]').fill(username);
          await page.locator('input[name="email"]').fill(`${username}@example.com`);
          await page.getByRole('button', { name: 'Reset password' }).click();
          await expect(page.getByText(/^Resetting of your password failed/)).toBeVisible();
          const user = await browser.newContext({ storageState: { cookies: [], origins: [] } });
          const userPage = await user.newPage();
          await userPage.goto('/home.aspx');
          await userPage.locator('input[name="username"]').fill(username);
          await userPage.locator('input[name="password"]').fill(`pw-${stamp}`);
          await userPage.getByRole('button', { name: 'Login' }).click();
          await expect(userPage.getByText(`Logged in as ${username}`)).toBeVisible();
          await user.close();
        } finally {
          await adminPage.goto(`/Admin/UserEdit.aspx?UserId=${userId}`);
          adminPage.once('dialog', (dialog) => dialog.accept());
          await adminPage.locator('[id$="btnDelete"]').click();
          await expect(adminPage).toHaveURL(/\/Admin\/Users\.aspx/i);
        }

        // The profile of the administrator.
        await expectSameAsWebForms(adminPage, legacyAdminPage, '/userprofile.aspx', testInfo);
        await adminPage.locator('input[name="email"]').fill('');
        await legacyAdminPage.locator('input[id$="txtEmail"]').fill('');
        await adminPage.getByRole('button', { name: 'Save account information' }).click();
        await legacyAdminPage.getByRole('button', { name: 'Save account information' }).click();
        await expectSameLook(adminPage, legacyAdminPage, 'save the profile without e-mail address', testInfo);
        await adminPage.locator('input[name="currentpassword"]').fill('wrong password');
        await legacyAdminPage.locator('input[id$="txtCurrentPassword"]').fill('wrong password');
        for (const p of [adminPage, legacyAdminPage]) {
          await p.locator('input[type="password"]').nth(1).fill('new password');
          await p.locator('input[type="password"]').nth(2).fill('new password');
          await p.getByRole('button', { name: 'Save new password' }).click();
        }
        await expectSameLook(adminPage, legacyAdminPage, 'save the password with a wrong current password', testInfo);

        await adminPage.goto('/userprofile.aspx');
        const firstName = await adminPage.locator('input[name="firstname"]').inputValue();
        await adminPage.locator('input[name="firstname"]').fill(`e2e ${stamp}`);
        await adminPage.getByRole('button', { name: 'Save account information' }).click();
        await expect(adminPage.getByText('Your account information was successfully updated.')).toBeVisible();
        await adminPage.goto('/userprofile.aspx');
        await expect(adminPage.locator('input[name="firstname"]')).toHaveValue(`e2e ${stamp}`);
        await adminPage.locator('input[name="firstname"]').fill(firstName);
        await adminPage.getByRole('button', { name: 'Save account information' }).click();
        await expect(adminPage.getByText('Your account information was successfully updated.')).toBeVisible();

        // The profile of a logged-in user needs the token of the form.
        const forged = await adminPage.request.post(sectionUrl, { form: { email: 'forged@example.com' } });
        expect(forged.status()).toBe(400);
      } finally {
        await legacy.close();
        await legacyAdmin.close();
        await admin.close();
      }
    });
});
