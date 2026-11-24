import { test, expect, type Page } from '@playwright/test';
import { expectHealthyPage, homeNodeId } from './helpers';

// The section pages of the site administration: standalone sections, editing sections with module settings,
// permissions and connections, and attaching sections to templates and nodes.

const messageBox = (page: Page) => page.locator('[id$="MessageBox"]');

/** The row of a section on Sections.aspx. */
const sectionRow = (page: Page, title: string) =>
  page.locator('tr', { has: page.locator('td', { hasText: new RegExp(`^${title}$`) }) });

/** The checkbox in a column of the authorization table of SectionEdit.aspx (view or edit) for a role. */
const permission = (page: Page, roleName: string, kind: 'View' | 'Edit') =>
  page.locator('tr', { has: page.locator('td', { hasText: new RegExp(`^${roleName}$`) }) })
    .locator(`input[id$="chk${kind}Allowed"]`);

/** Creates a standalone section (without node) and returns its id. */
async function createStandaloneSection(page: Page, title: string, moduleName: string,
  settings: Record<string, string | boolean> = {}): Promise<string> {
  await page.goto('/Admin/SectionEdit.aspx?SectionId=-1');
  await page.locator('[id$="txtTitle"]').fill(title);
  await page.locator('[id$="ddlModule"]').selectOption({ label: moduleName });
  await page.waitForLoadState();
  for (const [name, value] of Object.entries(settings)) {
    const control = page.locator(`[id$="${name}"]`);
    if (typeof value === 'boolean') {
      await control.setChecked(value);
    } else if (await control.evaluate((el) => el.tagName === 'SELECT')) {
      await control.selectOption(value);
    } else {
      await control.fill(value);
    }
  }
  await page.locator('[id$="btnSave"]').first().click();
  await expect(page).toHaveURL(/\/Admin\/Sections\.aspx/i);
  const editHref = await sectionRow(page, title).getByRole('link', { name: 'Edit' }).getAttribute('href');
  return /SectionId=(\d+)/i.exec(editHref!)![1];
}

async function deleteStandaloneSection(page: Page, title: string) {
  await page.goto('/Admin/Sections.aspx');
  page.once('dialog', (dialog) => dialog.accept());
  await sectionRow(page, title).getByRole('link', { name: 'Delete' }).click();
  await expect(sectionRow(page, title)).toHaveCount(0);
}

/** The id of the Search section that seed.setup.ts adds to the Search page under the home page. */
async function seededSearchSection(page: Page): Promise<{ nodeId: string; sectionId: string }> {
  await page.goto(`/Admin/NodeEdit.aspx?NodeId=${await homeNodeId(page)}`);
  await page.locator('#menupane a', { hasText: /^Search$/ }).click();
  const nodeId = /NodeId=(\d+)/i.exec(page.url())![1];
  const sectionHref = await page.locator('a[href*="SectionEdit.aspx?SectionId="]').first().getAttribute('href');
  return { nodeId, sectionId: /SectionId=(\d+)/i.exec(sectionHref!)![1] };
}

test('create, edit, connect and delete a standalone section', async ({ page }) => {
  const title = `e2e input ${Date.now().toString(36)}`;

  let response = await page.goto('/Admin/Sections.aspx');
  await expectHealthyPage(page, response);
  await expect(page.getByText('Manage sections that are not related to a node.')).toBeVisible();
  await page.locator('[id$="btnNew"]').click();
  await expect(page).toHaveURL(/SectionEdit\.aspx\?SectionId=-1/i);
  // Without a node there are no placeholders.
  await expect(page.locator('[id$="ddlPlaceholder"]')).toBeDisabled();

  // Validation
  await page.locator('[id$="txtCacheDuration"]').fill('');
  await page.locator('[id$="btnSave"]').first().click();
  await expect(page.getByText('Title is required')).toBeVisible();
  await expect(page.getByText('Cache duration is required (0 for no cache)')).toBeVisible();
  await page.locator('[id$="txtCacheDuration"]').fill('-1');
  await page.locator('[id$="btnSave"]').first().click();
  await expect(page.getByText('Only positive integers allowed')).toBeVisible();

  // Create a search input section, visible for anonymous users.
  await page.locator('[id$="txtTitle"]').fill(title);
  await page.locator('[id$="txtCacheDuration"]').fill('0');
  await page.locator('[id$="ddlModule"]').selectOption({ label: 'SearchInput' });
  await page.waitForLoadState();
  await expect(page.locator('[id$="txtTitle"]')).toHaveValue(title);
  await permission(page, 'Anonymous user', 'View').check();
  // Roles without editor rights have no edit checkbox.
  await expect(permission(page, 'Anonymous user', 'Edit')).toHaveCount(0);
  await page.locator('[id$="btnSave"]').first().click();
  await expect(page).toHaveURL(/\/Admin\/Sections\.aspx/i);
  const row = sectionRow(page, title);
  await expect(row).toContainText('SearchInput');
  for (const link of ['Edit', 'Delete', 'Attach to template', 'Attach to node']) {
    await expect(row.getByRole('link', { name: link })).toHaveCount(1);
  }
  const sectionId = /SectionId=(\d+)/i.exec((await row.getByRole('link', { name: 'Edit' }).getAttribute('href'))!)![1];

  try {
    // Edit: the module can't be changed any more.
    await row.getByRole('link', { name: 'Edit' }).click();
    await expect(page.locator('[id$="ddlModule"]')).toHaveCount(0);
    await expect(page.locator('[id$="lblModule"]')).toHaveText('SearchInput');
    await expect(permission(page, 'Anonymous user', 'View')).toBeChecked();
    await page.locator('[id$="txtTitle"]').fill(`${title} updated`);
    await page.locator('[id$="btnSave"]').last().click();
    await expect(messageBox(page)).toContainText('Section saved.');
    await page.goto(`/Admin/SectionEdit.aspx?SectionId=${sectionId}`);
    await expect(page.locator('[id$="txtTitle"]')).toHaveValue(`${title} updated`);
    await page.locator('[id$="txtTitle"]').fill(title);
    await page.locator('[id$="btnSave"]').first().click();
    await expect(messageBox(page)).toContainText('Section saved.');

    // Connect the search input to the search section.
    await page.goto(`/Admin/SectionEdit.aspx?SectionId=${sectionId}`);
    await page.getByRole('link', { name: 'Add connection' }).click();
    await expect(page).toHaveURL(new RegExp(`ConnectionEdit\\.aspx\\?SectionId=${sectionId}$`, 'i'));
    await expect(page.locator('[id$="lblSectionFrom"]')).toHaveText(` - ${title} - SearchInput`);
    await expect(page.locator('[id$="lblModuleType"]')).toHaveText('SearchInput');
    await expect(page.locator('[id$="ddlAction"]')).toHaveValue('Search');
    await page.locator('[id$="ddlSectionTo"]').selectOption({ label: 'Search - Search - Search' });
    await page.locator('[id$="btnSave"]').click();
    await expect(page).toHaveURL(new RegExp(`SectionEdit\\.aspx\\?SectionId=${sectionId}$`, 'i'));
    const connection = page.locator('tr', { has: page.locator('td', { hasText: /^Search - Search - Search$/ }) });
    await expect(connection).toContainText('Search');
    // All actions are connected.
    await expect(page.getByRole('link', { name: 'Add connection' })).toHaveCount(0);
    await connection.getByRole('link', { name: 'Delete' }).click();
    await expect(connection).toHaveCount(0);
    await expect(page.getByRole('link', { name: 'Add connection' })).toHaveCount(1);

    // Back goes to the standalone sections.
    await page.locator('[id$="btnBack"]').first().click();
    await expect(page).toHaveURL(/\/Admin\/Sections\.aspx/i);

    // Attach to a template and detach again.
    await sectionRow(page, title).getByRole('link', { name: 'Attach to template' }).click();
    await expect(page).toHaveTitle('Attach section to template(s)');
    await expect(page.locator('[id$="lblSection"]')).toHaveText(title);
    const templateRow = page.locator('tr', { has: page.locator('td', { hasText: /^Cuyahoga New$/ }) });
    await templateRow.locator('input[type="checkbox"]').check();
    const placeholder = await templateRow.locator('select option').first().getAttribute('value');
    await templateRow.locator('select').selectOption(placeholder!);
    await page.locator('[id$="btnSave"]').click();
    await expect(page).toHaveURL(/\/Admin\/Sections\.aspx/i);
    await expect(sectionRow(page, title)).toContainText('Cuyahoga New');
    // A section on a template can't be deleted or attached to a node.
    await expect(sectionRow(page, title).getByRole('link', { name: 'Delete' })).toHaveCount(0);
    await expect(sectionRow(page, title).getByRole('link', { name: 'Attach to node' })).toHaveCount(0);

    await sectionRow(page, title).getByRole('link', { name: 'Attach to template' }).click();
    await expect(templateRow.locator('input[type="checkbox"]')).toBeChecked();
    await expect(templateRow.locator('select')).toHaveValue(placeholder!);
    await templateRow.locator('input[type="checkbox"]').uncheck();
    await page.locator('[id$="btnSave"]').click();
    await expect(sectionRow(page, title)).not.toContainText('Cuyahoga New');
  } finally {
    await deleteStandaloneSection(page, title);
  }
});

test('module settings of a standalone section', async ({ page }) => {
  const title = `e2e articles ${Date.now().toString(36)}`;
  await page.goto('/Admin/SectionEdit.aspx?SectionId=-1');
  await page.locator('[id$="txtTitle"]').fill(title);
  await page.locator('[id$="ddlModule"]').selectOption({ label: 'Articles' });
  await page.waitForLoadState();
  // The settings of the chosen module: text boxes, checkboxes and lists for enum settings.
  await expect(page.locator('[id$="DISPLAY_TYPE"] option')).toHaveText(['HeadersOnly', 'HeadersAndSummary', 'FullContent']);
  await page.locator('[id$="btnSave"]').first().click();
  await expect(messageBox(page)).toContainText('The value for Number of articles to display is required.');

  const sectionId = await createStandaloneSection(page, title, 'Articles',
    { NUMBER_OF_ARTICLES_IN_LIST: '5', DISPLAY_TYPE: 'FullContent', ALLOW_COMMENTS: true, SHOW_AUTHOR: false });
  try {
    await page.goto(`/Admin/SectionEdit.aspx?SectionId=${sectionId}`);
    await expect(page.locator('[id$="NUMBER_OF_ARTICLES_IN_LIST"]')).toHaveValue('5');
    await expect(page.locator('[id$="DISPLAY_TYPE"]')).toHaveValue('FullContent');
    await expect(page.locator('[id$="ALLOW_COMMENTS"]')).toBeChecked();
    await expect(page.locator('[id$="SHOW_AUTHOR"]')).not.toBeChecked();
    // Articles can link to user profiles.
    await expect(page.getByRole('link', { name: 'Add connection' })).toHaveCount(1);
  } finally {
    await deleteStandaloneSection(page, title);
  }
});

test('attach a standalone section to a node', async ({ page }) => {
  const title = `e2e attach ${Date.now().toString(36)}`;
  const sectionId = await createStandaloneSection(page, title, 'StaticHtml');
  const homeId = await homeNodeId(page);

  await page.goto('/Admin/Sections.aspx');
  await sectionRow(page, title).getByRole('link', { name: 'Attach to node' }).click();
  await expect(page).toHaveURL(new RegExp(`SectionAttachNode\\.aspx\\?SectionId=${sectionId}$`, 'i'));
  await expect(page).toHaveTitle('Attach section');
  await expect(page.locator('[id$="lblModuleType"]')).toHaveText('StaticHtml');
  // Save is possible after choosing a node with a template.
  await expect(page.locator('[id$="btnSave"]')).toBeDisabled();
  await expect(page.locator('[id$="ddlPlaceholder"]')).toHaveCount(0);
  await page.locator('[id$="lbxAvailableNodes"]').selectOption({ label: 'Home' });
  await page.waitForLoadState();
  await expect(page.locator('[id$="btnSave"]')).toBeEnabled();
  await page.locator('[id$="ddlPlaceholder"]').selectOption('maincontent');
  await page.locator('[id$="btnSave"]').click();
  await expect(page).toHaveURL(new RegExp(`NodeEdit\\.aspx\\?NodeId=${homeId}$`, 'i'));

  // The section is part of the node now: delete it there.
  const nodeSectionRow = page.locator('tr', { hasText: title });
  await expect(nodeSectionRow).toHaveCount(1);
  page.once('dialog', (dialog) => dialog.accept());
  await nodeSectionRow.locator('a[id$="lbtDelete"]').click();
  await expect(nodeSectionRow).toHaveCount(0);
  await page.goto('/Admin/Sections.aspx');
  await expect(sectionRow(page, title)).toHaveCount(0);
});

test('a section of a node', async ({ page }) => {
  const { nodeId, sectionId } = await seededSearchSection(page);

  // A new section of a node chooses one of the placeholders of the node template; Back goes to the node.
  await page.goto(`/Admin/SectionEdit.aspx?SectionId=-1&NodeId=${nodeId}`);
  await expect(page.locator('[id$="ddlPlaceholder"]')).toBeEnabled();
  await expect(page.locator('[id$="ddlPlaceholder"] option[value="maincontent"]')).toHaveCount(1);
  await page.locator('[id$="btnBack"]').first().click();
  await expect(page).toHaveURL(new RegExp(`NodeEdit\\.aspx\\?NodeId=${nodeId}$`, 'i'));

  // The settings of an existing section.
  const response = await page.goto(`/Admin/SectionEdit.aspx?NodeId=${nodeId}&SectionId=${sectionId}`);
  await expectHealthyPage(page, response);
  // The menu is expanded to the node of the section.
  await expect(page.locator('#menupane .nodeActive')).toHaveText('Search');
  await expect(page.locator('#menupane a', { hasText: 'Home (en-US)' })).toHaveCount(1);
  await expect(page.getByRole('link', { name: 'Add a new node under the active node' }))
    .toHaveAttribute('href', `NodeEdit.aspx?NodeId=-1&ParentNodeId=${nodeId}`);
  await expect(page.locator('[id$="lblModule"]')).toHaveText('Search');
  await expect(page.locator('[id$="ddlPlaceholder"]')).toHaveValue('maincontent');
  await expect(page.locator('[id$="RESULTS_PER_PAGE"]')).toHaveValue('10');
  await expect(page.locator('[id$="SHOW_INPUT_PANEL"]')).toBeChecked();
  await page.locator('[id$="RESULTS_PER_PAGE"]').fill('11');
  await page.locator('[id$="btnSave"]').first().click();
  await expect(messageBox(page)).toContainText('Section saved.');
  await page.goto(`/Admin/SectionEdit.aspx?NodeId=${nodeId}&SectionId=${sectionId}`);
  await expect(page.locator('[id$="RESULTS_PER_PAGE"]')).toHaveValue('11');
  await page.locator('[id$="RESULTS_PER_PAGE"]').fill('10');
  await page.locator('[id$="btnSave"]').first().click();
  await expect(messageBox(page)).toContainText('Section saved.');
  // The view link in the header goes to the section on the public site.
  await expect(page.getByRole('link', { name: 'View the current site' })).toHaveAttribute('href', new RegExp(`/${sectionId}/section\\.aspx$`));
});
