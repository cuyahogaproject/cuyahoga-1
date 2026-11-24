import { test, expect, type Page } from '@playwright/test';
import { createNode, createSection, deleteNode, homeNodeId, sectionIdByTitle } from './helpers';

// The admin pages of the modules (in the ASP.NET Core site): the module admin header, editing content, and who may
// use them.

/** Waits until CKEditor has replaced its textarea (content set before that is lost). */
async function editorReady(page: Page) {
  await page.waitForFunction(() => {
    const ck = (window as any).CKEDITOR;
    const instances = ck ? Object.values(ck.instances) : [];
    return instances.length > 0 && instances.every((instance: any) => instance.status === 'ready');
  });
}

async function editorContent(page: Page, html?: string): Promise<string> {
  await editorReady(page);
  return page.evaluate((value) => {
    const instance: any = Object.values((window as any).CKEDITOR.instances)[0];
    if (value !== undefined) instance.setData(value);
    return instance.getData();
  }, html);
}

test('StaticHtml: edit the content of a section', async ({ page, browser, baseURL }) => {
  await page.goto('/page2.aspx');
  const editUrl = (await page.locator('.moduletools a', { hasText: /^Edit$/ }).getAttribute('href'))!;
  expect(editUrl).toMatch(/^\/Modules\/StaticHtml\/EditHtml\.aspx\?NodeId=\d+&SectionId=\d+$/);

  await page.goto(editUrl);
  await expect(page.locator('#headertitle')).toHaveText('Cuyahoga module administration');
  await expect(page.locator('#subheader')).toContainText('[Node: Page 2]');
  await expect(page.locator('#p_header_hplBack')).toHaveAttribute('href', '/page2.aspx');
  const original = await editorContent(page);
  const word = `zh${Date.now().toString(36)}`;
  try {
    await editorContent(page, `<p>Edited ${word}</p>`);
    await page.locator('[id$="btnSave"]').click();
    await expect(page.locator('#p_MessageBox')).toHaveText('Content saved.');
    expect(await editorContent(page)).toContain(`Edited ${word}`);

    // The public page, and the search index (instant indexing).
    await page.goto('/page2.aspx');
    await expect(page.locator('#content .section')).toContainText(`Edited ${word}`);
    await page.goto('/home/search.aspx');
    await page.locator('input[name="q"]').fill(word);
    await page.getByRole('button', { name: 'Search' }).click();
    await expect(page.locator('.searchresults li')).toHaveCount(1);
  } finally {
    await page.goto(editUrl);
    await editorContent(page, original);
    await page.locator('[id$="btnSave"]').click();
    await expect(page.locator('#p_MessageBox')).toHaveText('Content saved.');
  }

  // Visitors go to the login page; nobody may edit a section that doesn't exist.
  const visitor = await browser.newContext({ baseURL, storageState: { cookies: [], origins: [] } });
  const visitorPage = await visitor.newPage();
  await visitorPage.goto(editUrl);
  await expect(visitorPage).toHaveURL(/\/Login\.aspx\?ReturnUrl=/i);
  await visitor.close();
  const unknown = await page.request.get(editUrl.replace(/SectionId=\d+/, 'SectionId=999999'), { maxRedirects: 0 });
  expect(unknown.status()).toBe(403);
});

test('Downloads: upload, edit, cancel and delete files', async ({ page }) => {
  await page.goto('/home/downloads.aspx');
  const listUrl = (await page.locator('.moduletools a', { hasText: /^Edit$/ }).getAttribute('href'))!;
  expect(listUrl).toMatch(/^\/Modules\/Downloads\/EditDownloads\.aspx\?NodeId=\d+&SectionId=\d+$/);
  const editUrl = listUrl.replace('EditDownloads', 'EditFile');
  const stamp = Date.now().toString(36);

  // A new file needs a file.
  await page.goto(listUrl);
  await page.locator('[id$="btnNew"]').click();
  await expect(page).toHaveURL(/EditFile\.aspx\?NodeId=\d+&SectionId=\d+&FileId=-1$/);
  await expect(page.locator('[id$="btnDelete"]')).toHaveCount(0);
  await page.locator('[id$="btnSave"]').click();
  await expect(page.locator('#p_rfvFile')).toHaveText('File is required');

  // Cancel removes a file that was uploaded but not saved.
  await page.locator('[id$="txtTitle"]').fill(`e2e cancelled ${stamp}`);
  await page.locator('[id$="filUpload"]').setInputFiles({ name: `cancel-${stamp}.txt`, mimeType: 'text/plain', buffer: Buffer.from('x') });
  await page.locator('[id$="btnUpload"]').click();
  await expect(page.locator('[id$="txtFile"]')).toHaveValue(`cancel-${stamp}.txt`);
  await expect(page.locator('[id$="txtTitle"]')).toHaveValue(`e2e cancelled ${stamp}`);
  await page.locator('[id$="btnCancel"]').click();
  await expect(page).toHaveURL(/EditDownloads\.aspx/);
  await expect(page.getByText(`e2e cancelled ${stamp}`)).toHaveCount(0);

  // Upload and save, then edit: the title, the date and the roles.
  await page.locator('[id$="btnNew"]').click();
  await page.locator('[id$="txtTitle"]').fill(`e2e file ${stamp}`);
  await page.locator('[id$="filUpload"]').setInputFiles({ name: `file-${stamp}.txt`, mimeType: 'text/plain', buffer: Buffer.from('abc') });
  await page.locator('[id$="btnUpload"]').click();
  await page.locator('[id$="btnSave"]').click();
  await expect(page).toHaveURL(/EditDownloads\.aspx/);
  const row = page.locator('tr', { hasText: `e2e file ${stamp}` });
  await expect(row).toContainText(`file-${stamp}.txt`);
  await expect(row.locator('td').nth(2)).toHaveText('3');
  const fileId = /FileId=(\d+)/.exec((await row.locator('a', { hasText: 'Edit' }).getAttribute('href'))!)![1];
  try {
    await page.goto(`${editUrl}&FileId=${fileId}`);
    await page.locator('[id$="txtTitle"]').fill(`e2e renamed ${stamp}`);
    await page.locator('[id$="calDatePublished_dateTextBox"]').fill('');
    await page.locator('[id$="btnSave"]').click();
    await expect(page.locator('#p_rfvDatePublished')).toHaveText('Date published is required');
    await page.locator('[id$="calDatePublished_dateTextBox"]').fill('2/3/2025 4:05 PM');
    const anonymous = page.locator('tr', { has: page.locator('td', { hasText: /^Anonymous user$/ }) }).locator('input');
    await anonymous.uncheck();
    await page.locator('[id$="btnSave"]').click();
    await expect(page).toHaveURL(/EditDownloads\.aspx/);
    // The list shows the date in the time zone of the user.
    await expect(page.locator('tr', { hasText: `e2e renamed ${stamp}` })).toContainText(/2\/3\/2025 \d+:05/);
    await page.goto(`${editUrl}&FileId=${fileId}`);
    await expect(anonymous).not.toBeChecked();
    await expect(page.locator('[id$="calDatePublished_dateTextBox"]')).toHaveValue('2/3/2025 4:05 PM');
  } finally {
    await page.goto(`${editUrl}&FileId=${fileId}`);
    page.once('dialog', (dialog) => dialog.accept());
    await page.locator('[id$="btnDelete"]').click();
    await expect(page).toHaveURL(/EditDownloads\.aspx/);
  }
  await expect(page.getByText(`e2e renamed ${stamp}`)).toHaveCount(0);
  expect((await page.request.get(`${editUrl}&FileId=999999`)).status()).toBe(404);
});

test('RemoteContent: add, verify, refresh, edit and delete feeds', async ({ page, baseURL }) => {
  const stamp = Date.now().toString(36);
  const nodeId = await createNode(page, await homeNodeId(page), `e2e admin feeds ${stamp}`);
  try {
    await createSection(page, nodeId, 'Feeds', 'RemoteContent', { CACHE_DURATION: '60', SHOW_CONTENTS: true });
    const sectionId = await sectionIdByTitle(page, nodeId, 'Feeds');
    const listUrl = `/Modules/RemoteContent/AdminRemoteContent.aspx?NodeId=${nodeId}&SectionId=${sectionId}`;
    await page.goto(listUrl);
    await page.locator('[id$="btnNew"]').click();
    await expect(page).toHaveURL(/EditFeed\.aspx\?NodeId=\d+&SectionId=\d+&FeedId=-1$/);

    // A new feed can be saved once it was verified.
    await expect(page.locator('[id$="btnSave"]')).toBeDisabled();
    await page.locator('[id$="txtUrl"]').fill(`${baseURL}/does-not-exist.xml`);
    await page.locator('[id$="btnVerify"]').click();
    await expect(page.locator('#p_MessageBox')).toHaveClass('errorbox');
    await expect(page.locator('[id$="btnSave"]')).toBeDisabled();
    await page.locator('[id$="txtUrl"]').fill(`${baseURL}/3/feed.aspx`);
    await page.locator('[id$="btnVerify"]').click();
    await expect(page.locator('#p_MessageBox')).toHaveText('Feed is valid');
    await expect(page.locator('[id$="txtTitle"]')).toHaveValue('Articles');
    await page.locator('[id$="txtTitle"]').fill('');
    await page.locator('[id$="txtNumberOfItems"]').fill('many');
    await page.locator('[id$="btnSave"]').click();
    await expect(page.locator('#p_rfvTitle')).toHaveText('Title is required');
    await expect(page.locator('#p_covNumberOfItems')).toHaveText('Invalid number');
    await page.locator('[id$="txtTitle"]').fill(`e2e feed ${stamp}`);
    await page.locator('[id$="txtNumberOfItems"]').fill('3');
    await page.locator('[id$="btnSave"]').click();
    await expect(page).toHaveURL(/AdminRemoteContent\.aspx/);
    const row = page.locator('tr', { hasText: `e2e feed ${stamp}` });
    await expect(row).toContainText('/3/feed.aspx');

    // Refresh gets the items now (the public RemoteContent test shows them).
    await row.locator('a[id$="lbtRefresh"]').click();
    await page.waitForLoadState();
    await expect(page.locator('#p_MessageBox')).toHaveCount(0);
    await expect(row).toHaveCount(1);

    // Edit, then delete.
    await row.locator('a', { hasText: 'Edit' }).click();
    await expect(page.locator('[id$="btnSave"]')).toBeEnabled();
    await page.locator('[id$="txtNumberOfItems"]').fill('5');
    await page.locator('[id$="btnSave"]').click();
    await expect(row.locator('td').nth(2)).toHaveText('5');
    const editUrl = (await row.locator('a', { hasText: 'Edit' }).getAttribute('href'))!;
    await page.goto(editUrl);
    page.once('dialog', (dialog) => dialog.accept());
    await page.locator('[id$="btnDelete"]').click();
    await expect(page).toHaveURL(/AdminRemoteContent\.aspx/);
    await expect(row).toHaveCount(0);
    expect((await page.request.get(editUrl.replace(/FeedId=\d+/, 'FeedId=999999'))).status()).toBe(404);
  } finally {
    // The feeds first: a section with feeds can't be deleted.
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
    const sectionId = await sectionIdByTitle(page, nodeId, 'Feeds').catch(() => null);
    if (sectionId) {
      await page.goto(`/Modules/RemoteContent/AdminRemoteContent.aspx?NodeId=${nodeId}&SectionId=${sectionId}`);
      for (const href of await page.locator('a[id$="hplEdit"]').evaluateAll((links) => links.map((a) => a.getAttribute('href')))) {
        await page.goto(href!);
        page.once('dialog', (dialog) => dialog.accept());
        await page.locator('[id$="btnDelete"]').click();
        await expect(page).toHaveURL(/AdminRemoteContent\.aspx/);
      }
    }
    await deleteNode(page, nodeId);
  }
});

test('Articles: add, edit and delete articles and comments', async ({ page }) => {
  await page.goto('/page1.aspx');
  const listUrl = (await page.locator('.moduletools a', { hasText: /^Edit$/ }).getAttribute('href'))!;
  expect(listUrl).toMatch(/^\/Modules\/Articles\/AdminArticles\.aspx\?NodeId=\d+&SectionId=\d+$/);
  const sectionId = /SectionId=(\d+)/.exec(listUrl)![1];
  const stamp = Date.now().toString(36);

  // A new article needs a title.
  await page.goto(listUrl);
  await page.locator('[id$="btnNew"]').click();
  await expect(page).toHaveURL(/EditArticle\.aspx\?NodeId=\d+&SectionId=\d+&ArticleId=-1$/);
  await expect(page.locator('[id$="btnDelete"]')).toHaveCount(0);
  await expect(page.locator('[id$="chkSyndicate"]')).toBeChecked();
  await page.locator('[id$="btnSave"]').click();
  await expect(page.locator('#p_rfvTitle')).toHaveText('The title is required');

  // Save with a new category and dates (in the time zone of the user), without syndication.
  await page.locator('[id$="txtTitle"]').fill(`e2e admin article ${stamp}`);
  await page.locator('[id$="txtSummary"]').fill('A summary');
  await editorContent(page, `<p>Admin content ${stamp}</p>`);
  await page.locator('[id$="txtCategory"]').fill(`e2e admin category ${stamp}`);
  await page.locator('[id$="chkSyndicate"]').uncheck();
  await page.locator('[id$="calDateOnline_dateTextBox"]').fill('1/2/2020 3:04 PM');
  await page.locator('[id$="btnSave"]').click();
  await expect(page).toHaveURL(/AdminArticles\.aspx/);
  const row = page.locator('tr', { hasText: `e2e admin article ${stamp}` });
  await expect(row).toContainText(`e2e admin category ${stamp}`);
  await expect(row).toContainText('1/2/2020 3:04:00 PM');
  const articleId = /ArticleId=(\d+)/.exec((await row.locator('a', { hasText: 'Edit' }).getAttribute('href'))!)![1];
  const editUrl = `${listUrl.replace('AdminArticles', 'EditArticle')}&ArticleId=${articleId}`;
  try {
    await page.goto(editUrl);
    await expect(page.locator('[id$="txtSummary"]')).toHaveValue('A summary');
    await expect(page.locator('[id$="chkSyndicate"]')).not.toBeChecked();
    await expect(page.locator('[id$="ddlCategory"] option:checked')).toHaveText(`e2e admin category ${stamp}`);
    await expect(page.locator('[id$="calDateOnline_dateTextBox"]')).toHaveValue('1/2/2020 3:04 PM');
    expect(await editorContent(page)).toContain(`Admin content ${stamp}`);
    await page.locator('[id$="ddlCategory"]').selectOption('0');
    await page.locator('[id$="btnSave"]').click();
    await expect(row.locator('td').nth(1)).toHaveText('');

    // Comments: shown with their author, and deleted.
    await page.goto(`/${sectionId}/section.aspx/${articleId}`);
    await page.locator('textarea[name="comment"]').fill(`e2e admin comment ${stamp}`);
    await page.getByRole('button', { name: 'Submit' }).click();
    await page.goto(listUrl);
    await row.locator('a', { hasText: 'Comments' }).click();
    await expect(page).toHaveURL(/AdminComments\.aspx\?NodeId=\d+&SectionId=\d+&ArticleId=\d+$/);
    const comment = page.locator('tr', { hasText: `e2e admin comment ${stamp}` });
    await expect(comment).toContainText('admin');
    page.once('dialog', (dialog) => dialog.accept());
    await comment.locator('a', { hasText: 'Delete' }).click();
    await expect(comment).toHaveCount(0);
    await page.locator('[id$="btnBack"]').click();
    await expect(page).toHaveURL(/AdminArticles\.aspx/);
  } finally {
    await page.goto(editUrl);
    page.once('dialog', (dialog) => dialog.accept());
    await page.locator('[id$="btnDelete"]').click();
    await expect(page).toHaveURL(/AdminArticles\.aspx/);
  }
  await expect(row).toHaveCount(0);
  expect((await page.request.get(editUrl.replace(/ArticleId=\d+/, 'ArticleId=999999'))).status()).toBe(404);
});
