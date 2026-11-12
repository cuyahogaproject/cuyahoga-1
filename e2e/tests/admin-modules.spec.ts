import { test, expect, type APIRequestContext, type Page } from '@playwright/test';
import { adminCredentials, expectHealthyPage, extractPageLinks } from './helpers';

test.skip(!adminCredentials(), 'CUYAHOGA_ADMIN_USERNAME / CUYAHOGA_ADMIN_PASSWORD not set');

// These tests go through the transactional methods of module classes ([Transaction] on a proxied module),
// including files that are only moved into place when the database transaction commits.

/** Finds the first admin link of a module on the public site (the logged-in admin sees module tools). */
async function findModuleAdminUrl(request: APIRequestContext, baseURL: string, pattern: RegExp): Promise<string | undefined> {
  const queue = [`${baseURL}/Default.aspx`];
  const seen = new Set(queue);
  for (let i = 0; i < queue.length && i < 40; i++) {
    const response = await request.get(queue[i]);
    if (!response.ok()) continue;
    const html = await response.text();
    const adminLink = [...html.matchAll(/href\s*=\s*["']([^"']+)["']/gi)]
      .map((m) => m[1].replace(/&amp;/g, '&'))
      .find((href) => pattern.test(href));
    if (adminLink) return new URL(adminLink, queue[i]).toString();
    for (const link of extractPageLinks(html, queue[i], [/logout/i, /\/Admin\//i, /\/Modules\//i])) {
      if (!seen.has(link)) {
        seen.add(link);
        queue.push(link);
      }
    }
  }
  return undefined;
}

async function clickAndAcceptConfirm(page: Page, selector: string) {
  page.once('dialog', (dialog) => dialog.accept());
  await page.locator(selector).click();
}

/** Deletes items that an earlier, failed run left behind (rows on a module admin page with the given prefix). */
async function deleteLeftovers(page: Page, adminUrl: string, titlePrefix: string) {
  for (let i = 0; i < 10; i++) {
    await page.goto(adminUrl);
    const leftover = page.locator('tr', { hasText: titlePrefix }).first();
    if ((await leftover.count()) === 0) return;
    await leftover.locator('a', { hasText: 'Edit' }).click();
    await clickAndAcceptConfirm(page, '[id$="btnDelete"]');
    await page.waitForURL(/Admin|EditDownloads/i);
  }
}

test('articles: create and delete an article', async ({ page, baseURL }) => {
  const adminUrl = await findModuleAdminUrl(page.request, baseURL!, /Modules\/Articles\/AdminArticles\.aspx/i);
  test.skip(!adminUrl, 'no Articles section on the site');

  await deleteLeftovers(page, adminUrl!, 'e2e article ');

  const title = `e2e article ${Date.now().toString(36)}`;
  await page.goto(adminUrl!);
  await page.locator('[id$="btnNew"]').click();
  await expect(page).toHaveURL(/EditArticle\.aspx.*ArticleId=-1/i);

  await page.locator('[id$="txtTitle"]').fill(title);
  await page.locator('[id$="txtSummary"]').fill('Created by the e2e tests.');
  // The content textarea is replaced by CKEditor; set the content through the editor when it's there.
  await page.evaluate(() => {
    const ck = (window as any).CKEDITOR;
    if (ck) for (const name in ck.instances) ck.instances[name].setData('<p>e2e content</p>');
  });
  await page.locator('[id$="btnSave"]').click();
  await expect(page).toHaveURL(/AdminArticles\.aspx/i);

  const row = page.locator('tr', { hasText: title });
  await expect(row, 'the saved article is listed').toHaveCount(1);

  await row.locator('a', { hasText: 'Edit' }).click();
  await expect(page.locator('[id$="txtTitle"]')).toHaveValue(title);
  await clickAndAcceptConfirm(page, '[id$="btnDelete"]');
  await expect(page).toHaveURL(/AdminArticles\.aspx/i);
  await expect(page.locator('tr', { hasText: title }), 'the deleted article is gone').toHaveCount(0);
});

test('downloads: upload, download and delete a file', async ({ page, baseURL }) => {
  const adminUrl = await findModuleAdminUrl(page.request, baseURL!, /Modules\/Downloads\/EditDownloads\.aspx/i);
  test.skip(!adminUrl, 'no Downloads section on the site');
  const sectionId = /SectionId=(\d+)/i.exec(adminUrl!)![1];
  await deleteLeftovers(page, adminUrl!, 'e2e download ');

  const stamp = Date.now().toString(36);
  const title = `e2e download ${stamp}`;
  const contents = `e2e file contents ${stamp}`;

  await page.goto(adminUrl!);
  await page.locator('[id$="btnNew"]').click();
  await expect(page).toHaveURL(/EditFile\.aspx.*FileId=-1/i);

  await page.locator('[id$="txtTitle"]').fill(title);
  await page.locator('[id$="filUpload"]').setInputFiles({
    name: `e2e-${stamp}.txt`,
    mimeType: 'text/plain',
    buffer: Buffer.from(contents),
  });
  const uploadResponse = page.waitForResponse((r) => /EditFile\.aspx/i.test(r.url()) && r.request().method() === 'POST');
  await page.locator('[id$="btnUpload"]').click();
  await expectHealthyPage(page, await uploadResponse);
  await expect(page.locator('body'), 'no upload error').not.toContainText('Error saving the file');
  await page.locator('[id$="btnSave"]').click();
  await expect(page).toHaveURL(/EditDownloads\.aspx/i);

  const row = page.locator('tr', { hasText: title });
  await expect(row, 'the uploaded file is listed').toHaveCount(1);
  const editHref = await row.locator('a', { hasText: 'Edit' }).getAttribute('href');
  const fileId = /FileId=(\d+)/i.exec(editHref!)![1];

  // The file was moved from the temp folder into place when the transaction committed.
  const download = await page.request.get(`/${sectionId}/section.aspx/download/${fileId}`);
  expect(download.status()).toBe(200);
  expect(await download.text()).toBe(contents);

  await row.locator('a', { hasText: 'Edit' }).click();
  await clickAndAcceptConfirm(page, '[id$="btnDelete"]');
  await expect(page).toHaveURL(/EditDownloads\.aspx/i);
  await expect(page.locator('tr', { hasText: title }), 'the deleted file is gone').toHaveCount(0);

  const afterDelete = await page.request.get(`/${sectionId}/section.aspx/download/${fileId}`);
  expect(await afterDelete.text(), 'the deleted file can no longer be downloaded').not.toBe(contents);
});
