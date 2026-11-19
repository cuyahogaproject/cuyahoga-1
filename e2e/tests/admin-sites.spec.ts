import { test, expect, type Page } from '@playwright/test';
import { expectHealthyPage } from './helpers';

// The site pages of the site administration: the admin home page, sites and site aliases.

const messageBox = (page: Page) => page.locator('[id$="MessageBox"]');
const selectedText = (page: Page, idSuffix: string) =>
  page.locator(`[id$="${idSuffix}"] option:checked`).innerText();

/** The row of an alias in the aliases table of SiteEdit.aspx. */
const aliasRow = (page: Page, url: string) =>
  page.locator('tr', { has: page.locator('td', { hasText: new RegExp(`^${url.replace(/[.]/g, '\\.')}$`) }) });

test('the admin home page', async ({ page }) => {
  const response = await page.goto('/Admin/Default.aspx');
  await expectHealthyPage(page, response);
  await expect(page).toHaveTitle('Cuyahoga Site Administration');
  await expect(page.getByText('From here, the webmaster can maintain the site structure, templates, users and roles.')).toBeVisible();
  // The menu lists the sites with their root nodes.
  await expect(page.locator('#menupane a[href="SiteEdit.aspx?SiteId=1"]')).toHaveText(/^Cuyahoga Sample Site \(http:\/\/localhost:\d+\)$/);
  await expect(page.locator('#menupane a[href^="NodeEdit.aspx?NodeId="]').first()).toHaveText('Home (en-US)');
  // No active site: no link to the public site.
  await expect(page.getByRole('link', { name: 'View the current site' })).toHaveCount(0);
});

test('create, update and delete a site with an alias', async ({ page }) => {
  const stamp = Date.now().toString(36);
  const name = `e2e site ${stamp}`;
  const siteUrl = `http://e2e-${stamp}.example.com`;
  const aliasUrl = `http://alias-${stamp}.example.com`;

  // A new site: no delete button and no aliases yet.
  let response = await page.goto('/Admin/SiteEdit.aspx?SiteId=-1');
  await expectHealthyPage(page, response);
  await expect(page.locator('[id$="btnDelete"]')).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Add alias' })).toHaveCount(0);
  // Registered users of a new site don't become administrators unless chosen so.
  expect(await selectedText(page, 'ddlRoles')).toBe('Authenticated user');

  // Validation
  await page.locator('[id$="btnSave"]').click();
  await expect(page.getByText('Name is required')).toBeVisible();
  await expect(page.getByText('Site url is required')).toBeVisible();
  await expect(page.getByText('Webmaster email is required')).toBeVisible();

  // Create. Choosing a template fills the placeholders of that template and keeps what was entered.
  await page.locator('[id$="txtName"]').fill(name);
  await page.locator('[id$="txtSiteUrl"]').fill(siteUrl);
  await page.locator('[id$="txtWebmasterEmail"]').fill(`webmaster@${stamp}.example.com`);
  await expect(page.locator('[id$="ddlPlaceholders"] option')).toHaveCount(0);
  await page.locator('[id$="ddlTemplates"]').selectOption({ label: 'Cuyahoga New' });
  await expect(page.locator('[id$="ddlPlaceholders"] option[value="maincontent"]')).toHaveCount(1);
  await expect(page.locator('[id$="txtName"]')).toHaveValue(name);
  await page.locator('[id$="ddlPlaceholders"]').selectOption('maincontent');
  await page.locator('[id$="ddlOfflinePageTemplate"]').selectOption({ label: 'Offline' });
  await page.locator('[id$="ddlCultures"]').selectOption('nl-NL');
  await page.locator('[id$="ddlRoles"]').selectOption({ label: 'Authenticated user' });
  await page.locator('[id$="chkUseFriendlyUrls"]').check();
  await page.locator('[id$="txtMetaDescription"]').fill('An e2e test site');
  await page.locator('[id$="txtMetaKeywords"]').fill('e2e, test');
  await page.locator('[id$="btnSave"]').click();
  // Saving a new site continues on the page of that site, so saving again doesn't create another site.
  await expect(page).toHaveURL(/SiteEdit\.aspx\?SiteId=\d+&message=/i);
  await expect(messageBox(page)).toContainText('Site saved.');
  const siteId = /SiteId=(\d+)/i.exec(page.url())![1];

  try {
    await page.locator('[id$="btnSave"]').click();
    await expect(messageBox(page)).toContainText('Site saved.');
    // The site is in the menu, once.
    const siteLink = page.locator('#menupane a', { hasText: `${name} (${siteUrl})` });
    await expect(siteLink).toHaveCount(1);
    await expect(siteLink).toHaveAttribute('href', `SiteEdit.aspx?SiteId=${siteId}`);

    await siteLink.click();
    await expect(page).toHaveURL(new RegExp(`SiteEdit\\.aspx\\?SiteId=${siteId}$`, 'i'));
    await expect(page.locator('[id$="txtName"]')).toHaveValue(name);
    await expect(page.locator('[id$="txtSiteUrl"]')).toHaveValue(siteUrl);
    expect(await selectedText(page, 'ddlTemplates')).toBe('Cuyahoga New');
    await expect(page.locator('[id$="ddlPlaceholders"]')).toHaveValue('maincontent');
    expect(await selectedText(page, 'ddlOfflinePageTemplate')).toBe('Offline');
    await expect(page.locator('[id$="ddlCultures"]')).toHaveValue('nl-NL');
    expect(await selectedText(page, 'ddlRoles')).toBe('Authenticated user');
    await expect(page.locator('[id$="chkUseFriendlyUrls"]')).toBeChecked();
    await expect(page.locator('[id$="txtMetaDescription"]')).toHaveValue('An e2e test site');
    // The active site has a link to its public address.
    await expect(page.getByRole('link', { name: 'View the current site' })).toHaveAttribute('href', siteUrl);

    // Update: no template clears the placeholders.
    await page.locator('[id$="ddlTemplates"]').selectOption({ label: 'No template' });
    await expect(page.locator('[id$="ddlPlaceholders"] option')).toHaveCount(0);
    await page.locator('[id$="txtName"]').fill(`${name} updated`);
    await page.locator('[id$="btnSave"]').click();
    await expect(messageBox(page)).toContainText('Site saved.');
    await page.goto(`/Admin/SiteEdit.aspx?SiteId=${siteId}`);
    await expect(page.locator('[id$="txtName"]')).toHaveValue(`${name} updated`);
    expect(await selectedText(page, 'ddlTemplates')).toBe('No template');

    // Add an alias
    await page.getByRole('link', { name: 'Add alias' }).click();
    await expect(page).toHaveURL(new RegExp(`SiteAliasEdit\\.aspx\\?SiteId=${siteId}&SiteAliasId=-1`, 'i'));
    await expect(page.locator('[id$="btnDelete"]')).toHaveCount(0);
    await page.locator('[id$="btnSave"]').click();
    await expect(page.getByText('Url is required')).toBeVisible();
    await page.locator('[id$="txtUrl"]').fill(aliasUrl);
    await page.locator('[id$="btnSave"]').click();
    await expect(page).toHaveURL(new RegExp(`SiteEdit\\.aspx\\?SiteId=${siteId}$`, 'i'));
    await expect(aliasRow(page, aliasUrl)).toContainText('Inherited from site');

    // A site with aliases can't be deleted.
    page.once('dialog', (dialog) => dialog.accept());
    await page.locator('[id$="btnDelete"]').click();
    await expect(messageBox(page)).toContainText('Unable to delete a site when a site has related aliases.');

    // Edit the alias, cancel goes back to the site.
    await page.goto(`/Admin/SiteEdit.aspx?SiteId=${siteId}`);
    await aliasRow(page, aliasUrl).getByRole('link', { name: 'Edit' }).click();
    await expect(page.locator('[id$="txtUrl"]')).toHaveValue(aliasUrl);
    await page.locator('[id$="btnCancel"]').click();
    await expect(page).toHaveURL(new RegExp(`SiteEdit\\.aspx\\?SiteId=${siteId}$`, 'i'));

    // Delete the alias
    await aliasRow(page, aliasUrl).getByRole('link', { name: 'Edit' }).click();
    page.once('dialog', (dialog) => dialog.accept());
    await page.locator('[id$="btnDelete"]').click();
    await expect(page).toHaveURL(new RegExp(`SiteEdit\\.aspx\\?SiteId=${siteId}$`, 'i'));
    await expect(aliasRow(page, aliasUrl)).toHaveCount(0);

    // Cancel goes back to the admin home page.
    await page.locator('[id$="btnCancel"]').click();
    await expect(page).toHaveURL(/\/Admin\/Default\.aspx/i);
  } finally {
    await page.goto(`/Admin/SiteEdit.aspx?SiteId=${siteId}`);
    page.once('dialog', (dialog) => dialog.accept());
    await page.locator('[id$="btnDelete"]').click();
    await expect(page).toHaveURL(/\/Admin\/Default\.aspx/i);
  }
  await expect(page.locator('#menupane a', { hasText: name })).toHaveCount(0);
});

test('a site alias with an entry node', async ({ page }) => {
  const aliasUrl = `http://entry-${Date.now().toString(36)}.example.com`;
  await page.goto('/Admin/SiteAliasEdit.aspx?SiteId=1&SiteAliasId=-1');
  // The entry nodes are the nodes of the site, indented by level.
  await expect(page.locator('[id$="ddlEntryNodes"] option').first()).toHaveText('-- inherit from site --');
  await page.locator('[id$="txtUrl"]').fill(aliasUrl);
  await page.locator('[id$="ddlEntryNodes"]').selectOption({ label: 'Home' });
  await page.locator('[id$="btnSave"]').click();
  await expect(page).toHaveURL(/SiteEdit\.aspx\?SiteId=1$/i);
  await expect(aliasRow(page, aliasUrl)).toContainText('Home (en-US)');

  await aliasRow(page, aliasUrl).getByRole('link', { name: 'Edit' }).click();
  expect(await selectedText(page, 'ddlEntryNodes')).toBe('Home');
  page.once('dialog', (dialog) => dialog.accept());
  await page.locator('[id$="btnDelete"]').click();
  await expect(aliasRow(page, aliasUrl)).toHaveCount(0);
});
