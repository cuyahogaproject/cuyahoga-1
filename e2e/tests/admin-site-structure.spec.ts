import { test, expect, type Page } from '@playwright/test';
import { createNode, createSection, expectHealthyPage, homeNodeId } from './helpers';

// Exercises the mapped collections of the site structure: child nodes (ordering), sections of a node and
// section settings (a map).

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
  const parentId = await homeNodeId(page);
  const stamp = Date.now().toString(36);
  const titleA = `e2e structure ${stamp} A`;
  const titleB = `e2e structure ${stamp} B`;
  const nodeA = await createNode(page, parentId, titleA);
  const nodeB = await createNode(page, parentId, titleB);

  // New nodes are added at the end; moving B up puts it before A.
  let titles = await childTitles(page, parentId);
  expect(titles.indexOf(titleA)).toBeLessThan(titles.indexOf(titleB));
  await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeB}`);
  await page.locator('[id$="btnUp"]').click();
  await page.waitForLoadState();
  titles = await childTitles(page, parentId);
  expect(titles.indexOf(titleB), 'B moved above A').toBeLessThan(titles.indexOf(titleA));

  // Add a Search section with settings to node A.
  await createSection(page, nodeA, 'e2e structure search', 'Search', { RESULTS_PER_PAGE: '7', SHOW_INPUT_PANEL: true });

  // The settings are stored and read back.
  await page.locator('a[id$="hplEdit"]', { hasText: 'Edit' }).first().click();
  await expect(page.locator('[id$="RESULTS_PER_PAGE"]')).toHaveValue('7');
  await expect(page.locator('[id$="SHOW_INPUT_PANEL"]')).toBeChecked();

  // The public page renders the section (the module reads its settings).
  await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeA}`);
  const shortDescription = await page.locator('[id$="txtShortDescription"]').inputValue();
  const response = await page.goto(`/${shortDescription}.aspx`);
  await expectHealthyPage(page, response);

  // Deleting the sections and nodes leaves the site as it was.
  await deleteNode(page, nodeA);
  await deleteNode(page, nodeB);
  titles = await childTitles(page, parentId);
  expect(titles.filter((t) => t.startsWith('e2e structure ')), 'test nodes are deleted').toEqual([]);
});
