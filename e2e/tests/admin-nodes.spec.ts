import { test, expect, type Page } from '@playwright/test';
import { createNode, createSection, expectHealthyPage, homeNodeId } from './helpers';

// The node pages of the site administration: node properties, status, external links, moving nodes and sections,
// detaching sections, duplicating nodes, permissions and the custom menus of root nodes.

const messageBox = (page: Page) => page.locator('[id$="MessageBox"]');

/** The checkbox in a column of the authorization table of NodeEdit.aspx for a role. */
const permission = (page: Page, roleName: string, kind: 'View' | 'Edit') =>
  page.locator('tr', { has: page.locator('td', { hasText: new RegExp(`^${roleName}$`) }) })
    .locator(`input[id$="chk${kind}Allowed"]`);

/** The row of a section in the sections table of NodeEdit.aspx. */
const sectionRow = (page: Page, title: string) =>
  page.locator('tr', { has: page.locator('td', { hasText: new RegExp(`^${title}$`) }) });

/** Deletes the sections of a node and then the node itself. */
async function deleteNode(page: Page, nodeId: string) {
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

test('node properties, validation, add child and cancel', async ({ page }) => {
  const stamp = Date.now().toString(36);
  const homeId = await homeNodeId(page);
  const nodeId = await createNode(page, homeId, `e2e props ${stamp}`);
  try {
    let response = await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
    await expectHealthyPage(page, response);
    await expect(page.locator('[id$="lblParentNode"]')).toHaveText('Home');
    await expect(page.locator('[id$="lblStatus"]')).toHaveText('Online');
    const shortDescription = await page.locator('[id$="txtShortDescription"]').inputValue();
    // Preview opens the public page of the node.
    await expect(page.locator('[id$="btnPreview"]')).toHaveAttribute('onclick', `window.open('/${shortDescription}.aspx?preview=on');`);

    // Validation
    await page.locator('[id$="txtTitle"]').fill('');
    await page.locator('[id$="txtShortDescription"]').fill('has spaces');
    await page.locator('[id$="btnSave"]').click();
    await expect(page.getByText('Title is required')).toBeVisible();
    await expect(page.getByText('No spaces are allowed')).toBeVisible();
    await page.locator('[id$="txtShortDescription"]').fill('');
    await page.locator('[id$="btnSave"]').click();
    await expect(page.getByText('Short description is required')).toBeVisible();

    // Update
    await page.locator('[id$="txtTitle"]').fill(`e2e props ${stamp} updated`);
    await page.locator('[id$="txtShortDescription"]').fill(`e2e-props-${stamp}`);
    await page.locator('[id$="ddlCultures"]').selectOption('nl-NL');
    await page.locator('[id$="chkShowInNavigation"]').uncheck();
    await page.locator('[id$="txtMetaDescription"]').fill('Node description');
    await page.locator('[id$="txtMetaKeywords"]').fill('node, keywords');
    await permission(page, 'Anonymous user', 'View').uncheck();
    await page.locator('[id$="btnSave"]').click();
    await expect(messageBox(page)).toContainText('Node saved.');
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
    await expect(page.locator('[id$="txtTitle"]')).toHaveValue(`e2e props ${stamp} updated`);
    await expect(page.locator('[id$="txtShortDescription"]')).toHaveValue(`e2e-props-${stamp}`);
    await expect(page.locator('[id$="ddlCultures"]')).toHaveValue('nl-NL');
    await expect(page.locator('[id$="chkShowInNavigation"]')).not.toBeChecked();
    await expect(page.locator('[id$="txtMetaDescription"]')).toHaveValue('Node description');
    await expect(permission(page, 'Anonymous user', 'View')).not.toBeChecked();
    await expect(permission(page, 'Administrator', 'View')).toBeChecked();
    // The menu shows the active node, nodes hidden from the navigation with another icon.
    await expect(page.locator('#menupane .nodeActive')).toHaveText(`e2e props ${stamp} updated`);
    await expect(page.locator('#menupane .node', { hasText: `e2e props ${stamp} updated` }).locator('img'))
      .toHaveAttribute('src', /doc2-disabled\.gif$/);

    // Add a child: a new node gets its friendly url when it's saved; Cancel goes back to the parent.
    await page.locator('[id$="btnNew"]').click();
    await expect(page).toHaveURL(new RegExp(`NodeEdit\\.aspx\\?NodeId=-1&ParentNodeId=${nodeId}$`, 'i'));
    await expect(page.locator('[id$="txtShortDescription"]')).toHaveCount(0);
    await expect(page.locator('[id$="lblParentNode"]')).toHaveText(`e2e props ${stamp} updated`);
    await expect(page.locator('[id$="btnDelete"]')).toHaveCount(0);
    // A child inherits the culture and the permissions of its parent.
    await expect(page.locator('[id$="ddlCultures"]')).toHaveValue('nl-NL');
    await expect(permission(page, 'Anonymous user', 'View')).not.toBeChecked();
    await page.locator('[id$="btnCancel"]').click();
    await expect(page).toHaveURL(new RegExp(`NodeEdit\\.aspx\\?NodeId=${nodeId}$`, 'i'));

    // Cancel on an existing node goes to the admin home page.
    await page.locator('[id$="btnCancel"]').click();
    await expect(page).toHaveURL(/\/Admin\/Default\.aspx/i);
  } finally {
    await deleteNode(page, nodeId);
  }
});

test('take a node offline and bring it online', async ({ page }) => {
  const nodeId = await createNode(page, await homeNodeId(page), `e2e offline ${Date.now().toString(36)}`);
  try {
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
    await expect(page.locator('[id$="btnOffline"]')).toHaveValue('Take Offline');
    page.once('dialog', (dialog) => dialog.accept());
    await page.locator('[id$="btnOffline"]').click();
    await expect(messageBox(page)).toContainText('Node is now offline.');
    await expect(page.locator('[id$="lblStatus"]')).toHaveText('Offline');
    await expect(page.locator('[id$="btnOffline"]')).toHaveValue('Bring online');

    page.once('dialog', (dialog) => dialog.accept());
    await page.locator('[id$="btnOffline"]').click();
    await expect(messageBox(page)).toContainText('Node is now online.');
    await expect(page.locator('[id$="lblStatus"]')).toHaveText('Online');
  } finally {
    await deleteNode(page, nodeId);
  }
});

test('a node that links to an external url', async ({ page }) => {
  const nodeId = await createNode(page, await homeNodeId(page), `e2e link ${Date.now().toString(36)}`);
  try {
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
    await expect(page.locator('[id$="txtLinkUrl"]')).toHaveCount(0);
    await page.locator('[id$="chkLink"]').check();
    await expect(page.locator('[id$="txtLinkUrl"]')).toBeVisible();
    // A link has no template and no sections.
    await expect(page.locator('[id$="ddlTemplates"]')).toHaveCount(0);
    await expect(page.locator('#contentpane h4', { hasText: 'Sections' })).toHaveCount(0);
    await page.locator('[id$="txtLinkUrl"]').fill('http://example.com/external');
    await page.locator('[id$="ddlLinkTarget"]').selectOption('New');
    await page.locator('[id$="btnSave"]').click();
    await expect(messageBox(page)).toContainText('Node saved.');

    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
    await expect(page.locator('[id$="chkLink"]')).toBeChecked();
    await expect(page.locator('[id$="txtLinkUrl"]')).toHaveValue('http://example.com/external');
    await expect(page.locator('[id$="ddlLinkTarget"]')).toHaveValue('New');
    await expect(page.locator('[id$="btnPreview"]')).toHaveAttribute('onclick', "window.open('http://example.com/external?preview=on');");

    // No link any more: the url is cleared.
    await page.locator('[id$="chkLink"]').uncheck();
    await expect(page.locator('[id$="ddlTemplates"]')).toBeVisible();
    await page.locator('[id$="btnSave"]').click();
    await expect(messageBox(page)).toContainText('Node saved.');
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
    await expect(page.locator('[id$="chkLink"]')).not.toBeChecked();
    await page.locator('[id$="chkLink"]').check();
    await expect(page.locator('[id$="txtLinkUrl"]')).toHaveValue('');
  } finally {
    await deleteNode(page, nodeId);
  }
});

test('move nodes and sections, detach a section, change the template', async ({ page }) => {
  const stamp = Date.now().toString(36);
  const homeId = await homeNodeId(page);
  const nodeA = await createNode(page, homeId, `e2e move ${stamp} A`);
  const nodeB = await createNode(page, homeId, `e2e move ${stamp} B`);
  try {
    // Right: B becomes the last child of A (the node before it). Left: back under Home.
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeB}`);
    await expect(page.locator('[id$="btnDown"]')).toHaveCount(0);
    await page.locator('[id$="btnRight"]').click();
    await expect(page.locator('[id$="lblParentNode"]')).toHaveText(`e2e move ${stamp} A`);
    await expect(page.locator('[id$="btnRight"]')).toHaveCount(0);
    await page.locator('[id$="btnLeft"]').click();
    await expect(page.locator('[id$="lblParentNode"]')).toHaveText('Home');
    // Down: A was before B.
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeA}`);
    await page.locator('[id$="btnDown"]').click();
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${homeId}`);
    const children = (await page.locator('#menupane a[href*="NodeEdit.aspx?NodeId="]').allInnerTexts()).map((t) => t.trim());
    expect(children.indexOf(`e2e move ${stamp} B`)).toBeLessThan(children.indexOf(`e2e move ${stamp} A`));

    // Two sections in the same placeholder: the second moves up.
    await createSection(page, nodeA, `e2e section ${stamp} 1`, 'StaticHtml');
    await createSection(page, nodeA, `e2e section ${stamp} 2`, 'StaticHtml');
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeA}`);
    const titles = async () => (await page.locator('tr a[href*="SectionEdit.aspx?SectionId="]').evaluateAll(
      (links) => links.map((link) => link.closest('tr')!.querySelector('td')!.textContent!.trim())));
    expect(await titles()).toEqual([`e2e section ${stamp} 1`, `e2e section ${stamp} 2`]);
    await expect(sectionRow(page, `e2e section ${stamp} 1`).getByRole('link', { name: 'Move up' })).toHaveCount(0);
    // The module admin page of a section.
    await expect(sectionRow(page, `e2e section ${stamp} 1`).getByRole('link', { name: 'Admin' }))
      .toHaveAttribute('href', new RegExp(`Modules/StaticHtml/EditHtml\\.aspx\\?NodeId=${nodeA}&SectionId=\\d+$`));
    await sectionRow(page, `e2e section ${stamp} 2`).getByRole('link', { name: 'Move up' }).click();
    await expect(page).toHaveURL(new RegExp(`NodeEdit\\.aspx\\?NodeId=${nodeA}$`, 'i'));
    expect(await titles()).toEqual([`e2e section ${stamp} 2`, `e2e section ${stamp} 1`]);
    await sectionRow(page, `e2e section ${stamp} 2`).getByRole('link', { name: 'Move down' }).click();
    expect(await titles()).toEqual([`e2e section ${stamp} 1`, `e2e section ${stamp} 2`]);

    // A node with sections can't link to an external url or be deleted, a node with children can't be deleted.
    await expect(page.locator('[id$="chkLink"]')).toBeDisabled();
    page.once('dialog', (dialog) => dialog.accept());
    await page.locator('[id$="btnDelete"]').click();
    await expect(messageBox(page)).toContainText("Can't delete a node when there are sections attached.");
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${homeId}`);
    page.once('dialog', (dialog) => dialog.accept());
    await page.locator('[id$="btnDelete"]').click();
    await expect(messageBox(page)).toContainText(/Can't delete a node when there are (sections|child nodes) attached/);

    // Detach: the section becomes a standalone section.
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeA}`);
    await sectionRow(page, `e2e section ${stamp} 2`).locator('a[id$="lbtDetach"]').click();
    await expect(sectionRow(page, `e2e section ${stamp} 2`)).toHaveCount(0);
    await page.goto('/Admin/Sections.aspx');
    const standalone = page.locator('tr', { has: page.locator('td', { hasText: new RegExp(`^e2e section ${stamp} 2$`) }) });
    await expect(standalone).toHaveCount(1);
    page.once('dialog', (dialog) => dialog.accept());
    await standalone.getByRole('link', { name: 'Delete' }).click();
    await expect(standalone).toHaveCount(0);

    // Choosing another template saves the node.
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeA}`);
    await page.locator('[id$="ddlTemplates"]').selectOption({ label: 'Cuyahoga Standard' });
    await expect(messageBox(page)).toContainText('Node saved while setting the template.');
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeA}`);
    await expect(page.locator('[id$="ddlTemplates"] option:checked')).toHaveText('Cuyahoga Standard');
  } finally {
    await deleteNode(page, nodeA);
    await deleteNode(page, nodeB);
  }
});

test('duplicate a node and propagate permissions', async ({ page }) => {
  const stamp = Date.now().toString(36);
  const homeId = await homeNodeId(page);
  const parent = await createNode(page, homeId, `e2e dup ${stamp}`);
  const child = await createNode(page, parent, `e2e dup ${stamp} child`);
  let copy: string | undefined;
  try {
    await createSection(page, parent, `e2e dup ${stamp} section`, 'StaticHtml');

    // Propagate the permissions of the parent to its child nodes and sections.
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${parent}`);
    await permission(page, 'Anonymous user', 'View').uncheck();
    await page.locator('[id$="chkPropagateToChildNodes"]').check();
    await page.locator('[id$="chkPropagateToSections"]').check();
    await page.locator('[id$="btnSave"]').click();
    await expect(messageBox(page)).toContainText('Node saved.');
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${child}`);
    await expect(permission(page, 'Anonymous user', 'View')).not.toBeChecked();
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${parent}`);
    await sectionRow(page, `e2e dup ${stamp} section`).getByRole('link', { name: 'Edit' }).click();
    await expect(page.locator('input[id$="chkViewAllowed"]').first()).not.toBeChecked();

    // Duplicate: a copy next to the node, with copies of its sections.
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${parent}`);
    page.once('dialog', (dialog) => dialog.accept());
    await page.locator('[id$="btnDuplicate"]').click();
    await expect(page).toHaveURL(/NodeEdit\.aspx\?NodeId=\d+&message=/i);
    await expect(messageBox(page)).toContainText('Node has been duplicated.');
    copy = /NodeId=(\d+)/i.exec(page.url())![1];
    expect(copy).not.toBe(parent);
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${copy}`);
    await expect(page.locator('[id$="txtTitle"]')).toHaveValue(`Copy of e2e dup ${stamp}`);
    await expect(page.locator('[id$="lblParentNode"]')).toHaveText('Home');
    await expect(sectionRow(page, `e2e dup ${stamp} section`)).toHaveCount(1);
    await expect(permission(page, 'Anonymous user', 'View')).not.toBeChecked();
  } finally {
    if (copy) await deleteNode(page, copy);
    await deleteNode(page, child);
    await deleteNode(page, parent);
  }
});

test('custom menus of a root node', async ({ page }) => {
  const stamp = Date.now().toString(36);
  const homeId = await homeNodeId(page);
  const menuName = `e2e menu ${stamp}`;
  const nodeId = await createNode(page, homeId, `e2e menu node ${stamp}`);
  try {
    // Only root nodes have menus.
    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
    await expect(page.getByRole('link', { name: 'Add menu' })).toHaveCount(0);

    await page.goto(`/Admin/NodeEdit.aspx?NodeId=${homeId}`);
    await page.getByRole('link', { name: 'Add menu' }).click();
    await expect(page).toHaveURL(new RegExp(`MenuEdit\\.aspx\\?MenuId=-1&NodeId=${homeId}$`, 'i'));
    await expect(page.locator('[id$="btnDelete"]')).toHaveCount(0);
    await page.locator('[id$="btnSave"]').click();
    await expect(page.getByText('Name is required')).toBeVisible();

    // Pick nodes: add two, change their order, remove one and add it again.
    await page.locator('[id$="txtName"]').fill(menuName);
    await page.locator('[id$="ddlPlaceholder"]').selectOption('side1content');
    const available = page.locator('[id$="lbxAvailableNodes"]');
    const selected = page.locator('[id$="lbxSelectedNodes"]');
    await available.selectOption({ label: 'Home' });
    await page.locator('[id$="btnAdd"]').click();
    await available.selectOption({ label: `     e2e menu node ${stamp}` });
    await page.locator('[id$="btnAdd"]').click();
    await expect(selected.locator('option')).toHaveText(['Home', `e2e menu node ${stamp}`]);
    await expect(available.locator('option', { hasText: /^Home$/ })).toHaveCount(0);
    await selected.selectOption({ label: `e2e menu node ${stamp}` });
    await page.locator('[id$="btnUp"]').click();
    await expect(selected.locator('option')).toHaveText([`e2e menu node ${stamp}`, 'Home']);
    await selected.selectOption({ label: `e2e menu node ${stamp}` });
    await page.locator('[id$="btnDown"]').click();
    await expect(selected.locator('option')).toHaveText(['Home', `e2e menu node ${stamp}`]);
    await selected.selectOption({ label: 'Home' });
    await page.locator('[id$="btnRemove"]').click();
    await expect(selected.locator('option')).toHaveText([`e2e menu node ${stamp}`]);
    await expect(available.locator('option', { hasText: /^Home$/ })).toHaveCount(1);
    await page.locator('[id$="btnSave"]').click();
    await expect(page).toHaveURL(new RegExp(`NodeEdit\\.aspx\\?NodeId=${homeId}$`, 'i'));
    const menuRow = page.locator('tr', { has: page.locator('td', { hasText: new RegExp(`^${menuName}$`) }) });
    await expect(menuRow).toContainText('side1content');

    // Edit: the menu keeps its nodes.
    await menuRow.getByRole('link', { name: 'Edit' }).click();
    await expect(page.locator('[id$="txtName"]')).toHaveValue(menuName);
    await expect(page.locator('[id$="ddlPlaceholder"]')).toHaveValue('side1content');
    await expect(selected.locator('option')).toHaveText([`e2e menu node ${stamp}`]);
    await page.locator('[id$="txtName"]').fill(`${menuName} updated`);
    await page.locator('[id$="btnSave"]').click();
    await expect(messageBox(page)).toContainText('Menu saved');
    await page.locator('[id$="btnBack"]').click();
    await expect(page).toHaveURL(new RegExp(`NodeEdit\\.aspx\\?NodeId=${homeId}$`, 'i'));

    // Delete
    const updatedRow = page.locator('tr', { has: page.locator('td', { hasText: new RegExp(`^${menuName} updated$`) }) });
    await updatedRow.getByRole('link', { name: 'Edit' }).click();
    page.once('dialog', (dialog) => dialog.accept());
    await page.locator('[id$="btnDelete"]').click();
    await expect(page).toHaveURL(new RegExp(`NodeEdit\\.aspx\\?NodeId=${homeId}$`, 'i'));
    await expect(updatedRow).toHaveCount(0);
  } finally {
    await deleteNode(page, nodeId);
  }
});
