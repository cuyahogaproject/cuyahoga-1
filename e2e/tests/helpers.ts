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
 * Adds a section with the given module to a node. Settings are module settings by name: strings fill a text box,
 * booleans set a checkbox.
 */
export async function createSection(page: Page, nodeId: string, title: string, moduleName: string,
  settings: Record<string, string | boolean> = {}) {
  await page.goto(`/Admin/SectionEdit.aspx?SectionId=-1&NodeId=${nodeId}`);
  await page.locator('[id$="txtTitle"]').fill(title);
  await page.locator('[id$="ddlModule"]').selectOption({ label: moduleName });
  await page.waitForLoadState();
  for (const [name, value] of Object.entries(settings)) {
    const control = page.locator(`[id$="${name}"]`);
    await expect(control, `setting ${name} of ${moduleName}`).toBeVisible();
    if (typeof value === 'boolean') {
      await control.setChecked(value);
    } else {
      await control.fill(value);
    }
  }
  await page.locator('[id$="btnSave"]').click();
  await expect(page, `section ${title} saved`).toHaveURL(new RegExp(`NodeEdit\\.aspx\\?NodeId=${nodeId}`, 'i'));
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
