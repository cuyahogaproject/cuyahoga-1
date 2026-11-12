import { test, expect } from '@playwright/test';
import { expectHealthyPage } from './helpers';

// Exercises the NHibernate write path (insert, update, delete within a request transaction),
// which the data access upgrades are most likely to break.
test('create, update and delete a page node', async ({ page }) => {
  const stamp = Date.now().toString(36);
  const title = `e2e node ${stamp}`;

  // Use the first node in the admin tree as parent.
  await page.goto('/Admin/Default.aspx');
  const firstNodeHref = await page.locator('a[href*="NodeEdit.aspx?NodeId="]').first().getAttribute('href');
  expect(firstNodeHref, 'a node link on the admin home page').toBeTruthy();
  const parentId = /NodeId=(\d+)/i.exec(firstNodeHref!)![1];

  // Create
  let response = await page.goto(`/Admin/NodeEdit.aspx?NodeId=-1&ParentNodeId=${parentId}`);
  await expectHealthyPage(page, response);
  await page.locator('[id$="txtTitle"]').fill(title);
  // The friendly url (short description) of a new node is generated on save.
  await expect(page.locator('[id$="txtShortDescription"]')).toHaveCount(0);
  await page.locator('[id$="btnSave"]').click();
  await expect(page).toHaveURL(/NodeEdit\.aspx\?NodeId=\d+&message=/i);
  const nodeId = /NodeId=(\d+)/i.exec(page.url())![1];
  expect(nodeId).not.toBe(parentId);

  try {
    const shortDescription = await page.locator('[id$="txtShortDescription"]').inputValue();
    expect(shortDescription, 'generated friendly url').not.toBe('');

    // The new node is visible on the public site.
    response = await page.goto(`/${shortDescription}.aspx`);
    await expectHealthyPage(page, response);
    await expect(page).toHaveTitle(new RegExp(title));

    // Update
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
    await page.locator('[id$="txtTitle"]').fill(`${title} updated`);
    await page.locator('[id$="btnSave"]').click();
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
    await expect(page.locator('[id$="txtTitle"]')).toHaveValue(`${title} updated`);
  } finally {
    // Delete (also the cleanup when an assertion above fails)
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
    page.once('dialog', (dialog) => dialog.accept());
    await page.locator('[id$="btnDelete"]').click();
    await expect(page).toHaveURL(new RegExp(`NodeEdit\\.aspx\\?NodeId=${parentId}$`, 'i'));
  }

  response = await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
  expect(await page.content(), 'deleted node should not load in the editor').not.toContain(`${title} updated`);
});
