import { test, expect, type Page } from '@playwright/test';
import { adminCredentials, expectHealthyPage } from './helpers';

test.skip(!adminCredentials(), 'CUYAHOGA_ADMIN_USERNAME / CUYAHOGA_ADMIN_PASSWORD not set');

// Exercises the mapped collections of the site structure: child nodes (ordering), sections of a node and
// section settings (a map).

const prefix = 'e2e structure ';

async function firstNodeId(page: Page): Promise<string> {
  await page.goto('/Admin/Default.aspx');
  const href = await page.locator('a[href*="NodeEdit.aspx?NodeId="]').first().getAttribute('href');
  return /NodeId=(\d+)/i.exec(href!)![1];
}

async function createNode(page: Page, parentId: string, title: string): Promise<string> {
  await page.goto(`/Admin/NodeEdit.aspx?NodeId=-1&ParentNodeId=${parentId}`);
  await page.locator('[id$="txtTitle"]').fill(title);
  await page.locator('[id$="btnSave"]').click();
  await expect(page).toHaveURL(/NodeEdit\.aspx\?NodeId=\d+&message=/i);
  return /NodeId=(\d+)/i.exec(page.url())![1];
}

/** Deletes the sections of a node and then the node itself. */
async function deleteNode(page: Page, nodeId: string) {
  await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
  while ((await page.locator('a[id$="lbtDelete"]').count()) > 0) {
    page.once('dialog', (dialog) => dialog.accept());
    await page.locator('a[id$="lbtDelete"]').first().click();
    await page.waitForLoadState();
  }
  page.once('dialog', (dialog) => dialog.accept());
  await page.locator('[id$="btnDelete"]').click();
  await page.waitForLoadState();
}

/** The titles of the child nodes as listed in the admin navigation of the parent node, in order. */
async function childTitles(page: Page, parentId: string): Promise<string[]> {
  await page.goto(`/Admin/NodeEdit.aspx?NodeId=${parentId}`);
  return (await page.locator('a[href*="NodeEdit.aspx?NodeId="]').allInnerTexts()).map((t) => t.trim());
}

test('reorder nodes and save section settings', async ({ page }) => {
  const parentId = await firstNodeId(page);

  // Remove what an earlier, failed run left behind.
  for (const title of (await childTitles(page, parentId)).filter((t) => t.startsWith(prefix))) {
    const href = await page.locator('a[href*="NodeEdit.aspx?NodeId="]', { hasText: title }).first().getAttribute('href');
    await deleteNode(page, /NodeId=(\d+)/i.exec(href!)![1]);
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${parentId}`);
  }

  const stamp = Date.now().toString(36);
  const titleA = `${prefix}${stamp} A`;
  const titleB = `${prefix}${stamp} B`;
  const nodeA = await createNode(page, parentId, titleA);
  const nodeB = await createNode(page, parentId, titleB);

  try {
    // New nodes are added at the end; moving B up puts it before A.
    let titles = await childTitles(page, parentId);
    expect(titles.indexOf(titleA)).toBeLessThan(titles.indexOf(titleB));
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeB}`);
    await page.locator('[id$="btnUp"]').click();
    await page.waitForLoadState();
    titles = await childTitles(page, parentId);
    expect(titles.indexOf(titleB), 'B moved above A').toBeLessThan(titles.indexOf(titleA));

    // Add a Search section with settings to node A.
    await page.goto(`/Admin/SectionEdit.aspx?SectionId=-1&NodeId=${nodeA}`);
    await page.locator('[id$="txtTitle"]').fill(`${prefix}search`);
    await page.locator('[id$="ddlModule"]').selectOption({ label: 'Search' });
    await expect(page.locator('[id$="RESULTS_PER_PAGE"]')).toBeVisible();
    await page.locator('[id$="RESULTS_PER_PAGE"]').fill('7');
    await page.locator('[id$="SHOW_INPUT_PANEL"]').check();
    await page.locator('[id$="btnSave"]').click();
    await expect(page).toHaveURL(new RegExp(`NodeEdit\\.aspx\\?NodeId=${nodeA}`, 'i'));

    // The settings are stored and read back.
    await page.locator('a[id$="hplEdit"]', { hasText: 'Edit' }).first().click();
    await expect(page.locator('[id$="RESULTS_PER_PAGE"]')).toHaveValue('7');
    await expect(page.locator('[id$="SHOW_INPUT_PANEL"]')).toBeChecked();

    // The public page renders the section (the module reads its settings).
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeA}`);
    const shortDescription = await page.locator('[id$="txtShortDescription"]').inputValue();
    const response = await page.goto(`/${shortDescription}.aspx`);
    await expectHealthyPage(page, response);
  } finally {
    await deleteNode(page, nodeA);
    await deleteNode(page, nodeB);
  }

  const titles = await childTitles(page, parentId);
  expect(titles.filter((t) => t.startsWith(prefix)), 'test nodes are deleted').toEqual([]);
});
