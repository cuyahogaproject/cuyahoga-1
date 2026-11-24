import { expect, type APIRequestContext, type Page, type Response } from '@playwright/test';

/** Markers of an ASP.NET error page or Cuyahoga's own error page. */
const errorMarkers = [/Server Error in '/i, /Runtime Error/i, /Exception Details:/i, /An error occured/i];

/** The administrator account of the test site (the installer creates 'admin' with this password). */
export function adminCredentials() {
  return { username: 'admin', password: 'e2e-admin-password' };
}

/** The id of the first root node (the home page of the sample site). */
export async function homeNodeId(page: Page): Promise<string> {
  await page.goto('/Admin/Default.aspx');
  const href = await page.locator('a[href*="NodeEdit.aspx?NodeId="]').first().getAttribute('href');
  return /NodeId=(\d+)/i.exec(href!)![1];
}

/** Creates a page node under the parent and returns its id. */
export async function createNode(page: Page, parentId: string, title: string): Promise<string> {
  await page.goto(`/Admin/NodeEdit.aspx?NodeId=-1&ParentNodeId=${parentId}`);
  await page.locator('[id$="txtTitle"]').fill(title);
  await page.locator('[id$="btnSave"]').click();
  await expect(page).toHaveURL(/NodeEdit\.aspx\?NodeId=\d+&message=/i);
  return /NodeId=(\d+)/i.exec(page.url())![1];
}

/**
 * Adds a section with the given module to a node. Settings are module settings by name: strings fill a text box or
 * select an option of a list, booleans set a checkbox.
 */
export async function createSection(page: Page, nodeId: string, title: string, moduleName: string,
  settings: Record<string, string | boolean> = {}) {
  await page.goto(`/Admin/SectionEdit.aspx?SectionId=-1&NodeId=${nodeId}`);
  await page.locator('[id$="txtTitle"]').fill(title);
  await page.locator('[id$="ddlModule"]').selectOption({ label: moduleName });
  await page.waitForLoadState();
  await fillSectionSettings(page, settings);
  await page.locator('[id$="btnSave"]').click();
  await expect(page, `section ${title} saved`).toHaveURL(new RegExp(`NodeEdit\\.aspx\\?NodeId=${nodeId}`, 'i'));
}

/** Changes module settings of a section (see createSection). */
export async function updateSectionSettings(page: Page, nodeId: string, sectionId: string,
  settings: Record<string, string | boolean>) {
  await page.goto(`/Admin/SectionEdit.aspx?NodeId=${nodeId}&SectionId=${sectionId}`);
  await fillSectionSettings(page, settings);
  await page.locator('[id$="btnSave"]').first().click();
  await expect(page.locator('[id$="MessageBox"]')).toContainText('Section saved.');
}

async function fillSectionSettings(page: Page, settings: Record<string, string | boolean>) {
  for (const [name, value] of Object.entries(settings)) {
    const control = page.locator(`[id$="${name}"]`);
    await expect(control, `setting ${name}`).toBeVisible();
    if (typeof value === 'boolean') {
      await control.setChecked(value);
    } else if (await control.evaluate((element) => element.tagName === 'SELECT')) {
      await control.selectOption(value);
    } else {
      await control.fill(value);
    }
  }
}

/** The id of the section with the given title on a node. */
export async function sectionIdByTitle(page: Page, nodeId: string, title: string): Promise<string> {
  await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
  const href = await page.locator('tr', { hasText: title }).locator('a[href*="SectionEdit.aspx"]').first()
    .getAttribute('href');
  return /SectionId=(\d+)/i.exec(href!)![1];
}

/** Deletes the sections of a node and then the node itself. */
export async function deleteNode(page: Page, nodeId: string) {
  await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
  const deleteLinks = page.locator('a[id$="lbtDelete"]');
  for (let count = await deleteLinks.count(); count > 0; count--) {
    page.once('dialog', (dialog) => dialog.accept());
    await deleteLinks.first().click();
    await expect(deleteLinks).toHaveCount(count - 1);
  }
  page.once('dialog', (dialog) => dialog.accept());
  await page.locator('[id$="btnDelete"]').click();
  await expect(page).not.toHaveURL(new RegExp(`NodeEdit\\.aspx\\?NodeId=${nodeId}$`, 'i'));
}

/** Collects uncaught JavaScript errors on a page so a test can assert there were none. */
export function trackPageErrors(page: Page): string[] {
  const errors: string[] = [];
  page.on('pageerror', (err) => errors.push(err.message));
  return errors;
}

/** Asserts that a navigation produced a normal page: no 4xx/5xx, no error page, no redirect to Error.aspx. */
export async function expectHealthyPage(page: Page, response: Response | null) {
  expect(response, 'navigation response').not.toBeNull();
  expect(response!.status(), `HTTP status of ${page.url()}`).toBeLessThan(400);
  expect(page.url(), 'redirected to the error page').not.toMatch(/Error\.aspx/i);
  const body = await page.content();
  for (const marker of errorMarkers) {
    expect(body, `error marker ${marker} on ${page.url()}`).not.toMatch(marker);
  }
}

/** Same check as expectHealthyPage, for a raw HTTP request. Uses soft assertions so a crawl reports every bad url. */
export async function expectHealthyUrl(request: APIRequestContext, url: string): Promise<string> {
  const response = await request.get(url);
  expect.soft(response.status(), `HTTP status of ${url}`).toBeLessThan(400);
  expect.soft(response.url(), `${url} redirected to the error page`).not.toMatch(/Error\.aspx/i);
  const body = await response.text();
  for (const marker of errorMarkers) {
    expect.soft(body, `error marker ${marker} on ${url}`).not.toMatch(marker);
  }
  return body;
}

/** Same-origin page links (.aspx) in an html document, normalised to absolute urls without fragment. */
export function extractPageLinks(html: string, pageUrl: string, exclude: RegExp[] = []): string[] {
  const origin = new URL(pageUrl).origin;
  const links = new Set<string>();
  for (const match of html.matchAll(/href\s*=\s*["']([^"'#]+)["']/gi)) {
    const raw = match[1].replace(/&amp;/g, '&');
    if (/^(javascript|mailto):/i.test(raw)) continue;
    let url: URL;
    try {
      url = new URL(raw, pageUrl);
    } catch {
      continue;
    }
    if (url.origin !== origin || !/\.aspx/i.test(url.pathname)) continue;
    const href = url.toString();
    if (exclude.some((re) => re.test(href))) continue;
    links.add(href);
  }
  return [...links];
}

/** Breadth-first crawl from a start url, checking every page it reaches. Returns the visited urls. */
export async function crawl(
  request: APIRequestContext,
  startUrl: string,
  options: { maxPages: number; exclude?: RegExp[]; scope?: RegExp },
): Promise<string[]> {
  const queue = [startUrl];
  const seen = new Set(queue);
  const visited: string[] = [];
  while (queue.length > 0 && visited.length < options.maxPages) {
    const url = queue.shift()!;
    const body = await expectHealthyUrl(request, url);
    visited.push(url);
    for (const link of extractPageLinks(body, url, options.exclude)) {
      if (options.scope && !options.scope.test(link)) continue;
      if (!seen.has(link)) {
        seen.add(link);
        queue.push(link);
      }
    }
  }
  return visited;
}

/**
 * Uploads a file with the admin pages of a Downloads section (EditDownloads.aspx url) and returns its id. The file
 * gets the roles of the section, unless deniedRoles lists roles (by name) that may not download it.
 */
export async function uploadDownload(page: Page, adminUrl: string, title: string, fileName: string, contents: string,
  deniedRoles: string[] = []): Promise<string> {
  await page.goto(adminUrl);
  await page.locator('[id$="btnNew"]').click();
  await expect(page).toHaveURL(/EditFile\.aspx.*FileId=-1/i);
  await page.locator('[id$="txtTitle"]').fill(title);
  await page.locator('[id$="filUpload"]').setInputFiles({ name: fileName, mimeType: 'text/plain', buffer: Buffer.from(contents) });
  const uploadResponse = page.waitForResponse((r) => /EditFile\.aspx/i.test(r.url()) && r.request().method() === 'POST');
  await page.locator('[id$="btnUpload"]').click();
  await expectHealthyPage(page, await uploadResponse);
  await expect(page.locator('body'), 'no upload error').not.toContainText('Error saving the file');
  for (const role of deniedRoles) {
    await page.locator('tr', { has: page.locator('td', { hasText: new RegExp(`^${role}$`) }) })
      .locator('[id$="chkRole"]').uncheck();
  }
  await page.locator('[id$="btnSave"]').click();
  await expect(page).toHaveURL(/EditDownloads\.aspx/i);
  const editHref = await page.locator('tr', { hasText: title }).locator('a', { hasText: 'Edit' }).getAttribute('href');
  return /FileId=(\d+)/i.exec(editHref!)![1];
}

/** Deletes a file of a Downloads section with its admin pages (EditDownloads.aspx url). */
export async function deleteDownload(page: Page, adminUrl: string, fileId: string) {
  await page.goto(adminUrl.replace(/EditDownloads\.aspx/i, 'EditFile.aspx') + `&FileId=${fileId}`);
  page.once('dialog', (dialog) => dialog.accept());
  await page.locator('[id$="btnDelete"]').click();
  await expect(page).toHaveURL(/EditDownloads\.aspx/i);
}

/**
 * Creates an article with the admin pages of an Articles section (AdminArticles.aspx url) and returns its id. A
 * category that doesn't exist yet is created.
 */
export async function createArticle(page: Page, adminUrl: string, title: string, summary: string, content: string,
  category?: string, expired = false): Promise<string> {
  await page.goto(adminUrl);
  await page.locator('[id$="btnNew"]').click();
  await expect(page).toHaveURL(/EditArticle\.aspx.*ArticleId=-1/i);
  await page.locator('[id$="txtTitle"]').fill(title);
  await page.locator('[id$="txtSummary"]').fill(summary);
  // The content textarea is replaced by CKEditor; set the content through the editor when it's there (once it's
  // ready: content set before that is lost).
  await page.waitForFunction(() => {
    const ck = (window as any).CKEDITOR;
    const instances = ck ? Object.values(ck.instances) : [];
    return !ck || (instances.length > 0 && instances.every((instance: any) => instance.status === 'ready'));
  });
  await page.evaluate((html) => {
    const ck = (window as any).CKEDITOR;
    if (ck) for (const name in ck.instances) ck.instances[name].setData(html);
    else (document.querySelector('[id$="txtContent"]') as HTMLTextAreaElement).value = html;
  }, content);
  if (category) {
    await page.locator('[id$="txtCategory"]').fill(category);
  }
  if (expired) {
    // Online and offline in the past (ISO dates parse in every culture).
    await page.locator('[id$="calDateOnline_dateTextBox"]').fill('2000-01-01');
    await page.locator('[id$="calDateOffline_dateTextBox"]').fill('2001-01-01');
  }
  await page.locator('[id$="btnSave"]').click();
  await expect(page).toHaveURL(/AdminArticles\.aspx/i);
  const editHref = await page.locator('tr', { hasText: title.split('<')[0] }).locator('a', { hasText: 'Edit' }).getAttribute('href');
  return /ArticleId=(\d+)/i.exec(editHref!)![1];
}

/** Deletes an article (and its comments) with the admin pages of an Articles section (AdminArticles.aspx url). */
export async function deleteArticle(page: Page, adminUrl: string, articleId: string) {
  await page.goto(adminUrl.replace(/AdminArticles\.aspx/i, 'EditArticle.aspx') + `&ArticleId=${articleId}`);
  page.once('dialog', (dialog) => dialog.accept());
  await page.locator('[id$="btnDelete"]').click();
  await expect(page).toHaveURL(/AdminArticles\.aspx/i);
}
