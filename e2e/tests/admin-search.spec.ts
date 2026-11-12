import { test, expect, type APIRequestContext, type Page } from '@playwright/test';
import { extractPageLinks } from './helpers';

// Full-text search: instant indexing when content changes, rebuilding the index and querying it.

/** Finds the first public page with the given content (crawls from the home page). */
async function findPage(request: APIRequestContext, baseURL: string, pattern: RegExp): Promise<string | undefined> {
  const queue = [`${baseURL}/Default.aspx`];
  const seen = new Set(queue);
  for (let i = 0; i < queue.length && i < 40; i++) {
    const response = await request.get(queue[i]);
    if (!response.ok()) continue;
    const html = await response.text();
    if (pattern.test(html)) return queue[i];
    for (const link of extractPageLinks(html, queue[i], [/logout/i, /\/Admin\//i, /\/Modules\//i])) {
      if (!seen.has(link)) {
        seen.add(link);
        queue.push(link);
      }
    }
  }
  return undefined;
}

async function search(page: Page, searchPageUrl: string, text: string) {
  await page.goto(searchPageUrl);
  await page.locator('[id$="txtSearchText"]').fill(text);
  await page.locator('[id$="btnSearch"]').click();
  await page.waitForLoadState();
  await expect(page.locator('body'), 'no search error').not.toContainText('Error while performing full-text search');
}

test('search finds new articles, also after rebuilding the index', async ({ page, baseURL }) => {
  const searchPageUrl = await findPage(page.request, baseURL!, /id="[^"]*txtSearchText"/);
  const articlesAdminPage = await findPage(page.request, baseURL!, /Modules\/Articles\/AdminArticles\.aspx/i);
  expect(searchPageUrl, 'the site has a search page').toBeTruthy();
  expect(articlesAdminPage, 'the site has an Articles section').toBeTruthy();

  // A unique word, so the search only finds this article.
  const word = `zq${Date.now().toString(36)}`;
  const title = `e2e search ${word}`;

  // Create the article (instant indexing adds it to the index).
  await page.goto(articlesAdminPage!);
  const adminHref = await page.locator('a[href*="Modules/Articles/AdminArticles.aspx"]').first().getAttribute('href');
  const adminUrl = new URL(adminHref!.replace(/&amp;/g, '&'), page.url()).toString();
  await page.goto(adminUrl);
  await page.locator('[id$="btnNew"]').click();
  await page.locator('[id$="txtTitle"]').fill(title);
  await page.locator('[id$="txtSummary"]').fill('Created by the e2e search test.');
  await page.evaluate((w) => {
    const ck = (window as any).CKEDITOR;
    if (ck) for (const name in ck.instances) ck.instances[name].setData(`<p>Searchable content ${w}</p>`);
  }, word);
  await page.locator('[id$="btnSave"]').click();
  await expect(page).toHaveURL(/AdminArticles\.aspx/i);

  try {
    await search(page, searchPageUrl!, word);
    await expect(page.locator('a', { hasText: title }), 'found through instant indexing').toHaveCount(1);

    // Rebuild the index from the site administration.
    await page.goto('/Admin/RebuildIndex.aspx');
    await page.locator('[id$="btnRebuild"]').click();
    await page.waitForLoadState();
    await expect(page.locator('body')).not.toContainText(/error/i);

    await search(page, searchPageUrl!, word);
    await expect(page.locator('a', { hasText: title }), 'found after rebuilding the index').toHaveCount(1);
  } finally {
    // Delete the article (instant indexing removes it from the index).
    await page.goto(adminUrl);
    const row = page.locator('tr', { hasText: title });
    if ((await row.count()) > 0) {
      await row.locator('a', { hasText: 'Edit' }).click();
      page.once('dialog', (dialog) => dialog.accept());
      await page.locator('[id$="btnDelete"]').click();
      await expect(page).toHaveURL(/AdminArticles\.aspx/i);
    }
  }

  await search(page, searchPageUrl!, word);
  await expect(page.locator('a', { hasText: title }), 'gone from the index after deleting').toHaveCount(0);
});
