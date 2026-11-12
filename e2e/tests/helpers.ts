import { expect, type APIRequestContext, type Page, type Response } from '@playwright/test';

/** Markers of an ASP.NET error page or Cuyahoga's own error page. */
const errorMarkers = [/Server Error in '/i, /Runtime Error/i, /Exception Details:/i, /An error occured/i];

export function adminCredentials() {
  const username = process.env.CUYAHOGA_ADMIN_USERNAME;
  const password = process.env.CUYAHOGA_ADMIN_PASSWORD;
  return username && password ? { username, password } : undefined;
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
