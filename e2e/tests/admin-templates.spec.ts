import { test, expect, type Page } from '@playwright/test';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { expectHealthyPage } from './helpers';

// The template pages of the site administration (templates, placeholders, the file editor) and rebuilding the
// full-text index.

const messageBox = (page: Page) => page.locator('[id$="MessageBox"]');
const templateRow = (page: Page, name: string) =>
  page.locator('tr', { has: page.locator('td', { hasText: new RegExp(`^${name}$`) }) });
const placeholderRow = (page: Page, placeholder: string) =>
  page.locator('tr', { has: page.locator('td', { hasText: new RegExp(`^\\s*${placeholder}\\s*$`) }) });

test('create, edit and delete a template, attach a section to a placeholder', async ({ page }) => {
  const stamp = Date.now().toString(36);
  const name = `e2e template ${stamp}`;
  const sectionTitle = `e2e template section ${stamp}`;

  let response = await page.goto('/Admin/Templates.aspx');
  await expectHealthyPage(page, response);
  await expect(page.getByText('NOTE: You have to upload the physical templates')).toBeVisible();
  const anotherRed = templateRow(page, 'Another Red');
  await expect(anotherRed).toContainText('Templates/AnotherRed');
  // Only the stylesheet can be edited: the templates are Razor layouts of the ASP.NET Core site.
  await expect(anotherRed.getByRole('link', { name: '[Edit]' }))
    .toHaveAttribute('href', 'TemplateEditor.aspx?file=Templates/AnotherRed/css/red.css');
  // A template that is in use can't be deleted.
  await anotherRed.getByRole('link', { name: 'Edit', exact: true }).click();
  await expect(page.locator('[id$="txtName"]')).toHaveValue('Another Red');
  await expect(page.locator('[id$="btnDelete"]')).toHaveCount(0);

  // New: validation and checking the base path.
  await page.goto('/Admin/Templates.aspx');
  await page.locator('[id$="btnNew"]').click();
  await expect(page).toHaveURL(/TemplateEdit\.aspx\?TemplateId=-1/i);
  await expect(page.locator('[id$="btnDelete"]')).toHaveCount(0);
  await expect(page.getByRole('heading', { name: 'Placeholders' })).toHaveCount(0);
  await page.locator('[id$="btnSave"]').click();
  await expect(page.getByText('Name is required')).toBeVisible();
  await expect(page.getByText('Base path is required')).toBeVisible();
  await page.locator('[id$="txtBasePath"]').fill('Templates/DoesNotExist');
  await page.locator('[id$="btnVerifyBasePath"]').click();
  await expect(messageBox(page)).toContainText('The base path you entered could not be found on the server.');
  await page.locator('[id$="txtBasePath"]').fill('Templates/AnotherRed');
  await page.locator('[id$="btnVerifyBasePath"]').click();
  await expect(page.locator('[id$="ddlTemplateControls"] option')).toHaveText(['Cuyahoga.ascx', 'Offline.ascx']);
  await expect(page.locator('[id$="ddlCss"] option')).toHaveText(['red.css']);

  // Create
  await page.locator('[id$="txtName"]').fill(name);
  await page.locator('[id$="ddlTemplateControls"]').selectOption('Cuyahoga.ascx');
  await page.locator('[id$="btnSave"]').click();
  await expect(page).toHaveURL(/\/Admin\/Templates\.aspx/i);
  await expect(templateRow(page, name)).toContainText('Cuyahoga.ascx');

  // A standalone section to attach.
  await page.goto('/Admin/SectionEdit.aspx?SectionId=-1');
  await page.locator('[id$="txtTitle"]').fill(sectionTitle);
  await page.locator('[id$="ddlModule"]').selectOption({ label: 'StaticHtml' });
  await page.waitForLoadState();
  await page.locator('[id$="btnSave"]').first().click();
  await expect(page).toHaveURL(/\/Admin\/Sections\.aspx/i);

  try {
    // Edit: the placeholders of the template control.
    await page.goto('/Admin/Templates.aspx');
    await templateRow(page, name).getByRole('link', { name: 'Edit', exact: true }).click();
    const templateId = /TemplateId=(\d+)/i.exec(page.url())![1];
    await expect(page.locator('[id$="txtBasePath"]')).toHaveValue('Templates/AnotherRed');
    await expect(page.locator('[id$="ddlTemplateControls"]')).toHaveValue('Cuyahoga.ascx');
    await expect(page.locator('[id$="ddlCss"]')).toHaveValue('red.css');
    await expect(placeholderRow(page, 'maincontent')).toHaveCount(1);

    // Attach a section to a placeholder, then detach it.
    await placeholderRow(page, 'maincontent').getByRole('link', { name: 'Attach section' }).click();
    await expect(page).toHaveURL(new RegExp(`TemplateSection\\.aspx\\?TemplateId=${templateId}&Placeholder=maincontent$`, 'i'));
    await expect(page.locator('[id$="lblTemplate"]')).toHaveText(name);
    await expect(page.locator('[id$="lblPlaceholder"]')).toHaveText('maincontent');
    await page.locator('[id$="ddlSections"]').selectOption({ label: sectionTitle });
    await page.locator('[id$="btnAttach"]').click();
    await expect(page).toHaveURL(new RegExp(`TemplateEdit\\.aspx\\?TemplateId=${templateId}$`, 'i'));
    await expect(placeholderRow(page, 'maincontent').getByRole('link', { name: sectionTitle })).toHaveAttribute('href', /SectionEdit\.aspx\?SectionId=\d+$/);
    page.once('dialog', (dialog) => dialog.accept());
    await placeholderRow(page, 'maincontent').getByRole('link', { name: 'Detach section' }).click();
    await expect(messageBox(page)).toContainText('Section in Placeholder maincontent detached');
    await expect(placeholderRow(page, 'maincontent').getByRole('link', { name: 'Attach section' })).toHaveCount(1);

    // Back on the attach page goes to the template.
    await placeholderRow(page, 'maincontent').getByRole('link', { name: 'Attach section' }).click();
    await page.locator('[id$="btnBack"]').click();
    await expect(page).toHaveURL(new RegExp(`TemplateEdit\\.aspx\\?TemplateId=${templateId}$`, 'i'));

    // Update
    await page.locator('[id$="txtName"]').fill(`${name} updated`);
    await page.locator('[id$="btnSave"]').click();
    await expect(messageBox(page)).toContainText('Template saved');

    // Delete (not in use)
    page.once('dialog', (dialog) => dialog.accept());
    await page.locator('[id$="btnDelete"]').click();
    await expect(page).toHaveURL(/\/Admin\/Templates\.aspx/i);
    await expect(templateRow(page, `${name} updated`)).toHaveCount(0);
  } finally {
    await page.goto('/Admin/Sections.aspx');
    const row = templateRow(page, sectionTitle);
    page.once('dialog', (dialog) => dialog.accept());
    await row.getByRole('link', { name: 'Delete' }).click();
    await expect(row).toHaveCount(0);
  }
});

test('edit a template file', async ({ page }) => {
  // A real file of the site: put it back afterwards.
  const cssFile = path.resolve(__dirname, '..', '..', 'Web.Core', 'wwwroot', 'Templates', 'AnotherRed', 'Css', 'red.css');
  const original = fs.readFileSync(cssFile);

  // Only stylesheets in the template folder.
  const onlyStylesheets = 'Only stylesheets (.css) in the template directory can be edited.';
  await page.goto('/Admin/TemplateEditor.aspx?file=Web.config');
  await expect(messageBox(page)).toContainText(onlyStylesheets);
  await expect(page.locator('[id$="btnSave"]')).toBeDisabled();
  await page.goto('/Admin/TemplateEditor.aspx?file=Templates/AnotherRed/Cuyahoga.ascx');
  await expect(messageBox(page)).toContainText(onlyStylesheets);
  await expect(page.locator('[id$="btnSave"]')).toBeDisabled();

  try {
    await page.goto('/Admin/TemplateEditor.aspx?file=Templates/AnotherRed/css/red.css');
    await expect(page).toHaveTitle('Edit file');
    const editor = page.locator('[id$="txtName"]');
    const contents = await editor.inputValue();
    expect(contents).toContain('{');
    await editor.fill(contents + '\n/* e2e */');
    page.once('dialog', (dialog) => dialog.accept());
    await page.locator('[id$="btnSave"]').click();
    await expect(messageBox(page)).toContainText('File saved.');
    expect(fs.readFileSync(cssFile, 'utf8')).toContain('/* e2e */');

    await page.locator('[id$="btnBack"]').click();
    await expect(page).toHaveURL(/\/Admin\/Templates\.aspx/i);
  } finally {
    fs.writeFileSync(cssFile, original);
  }
});

test('rebuild the full-text index', async ({ page }) => {
  const response = await page.goto('/Admin/RebuildIndex.aspx');
  await expectHealthyPage(page, response);
  await expect(page.getByText('The index was successfully rebuilt.')).toHaveCount(0);
  await page.locator('[id$="btnRebuild"]').click();
  await expect(page.getByText('The index was successfully rebuilt.')).toBeVisible();
});

test('the modules: installation and activation', async ({ page }) => {
  await page.goto('/Admin/Modules.aspx');
  await expect(page.locator('#contentpane h1')).toHaveText('Modules');
  const row = (name: string) => page.locator('tr', { has: page.locator('td', { hasText: new RegExp(`^${name}$`) }) });
  await expect(row('Articles')).toContainText(/Installed \(\d+\.\d+\.\d+\)/);
  await expect(row('Articles')).toContainText('Active');
  await expect(row('StaticHtml')).toContainText('Cuyahoga.Modules');
  // Installed modules can be uninstalled, not installed again.
  await expect(row('Articles').locator('a', { hasText: 'Uninstall' })).toHaveCount(1);
  await expect(row('Articles').locator('a', { hasText: /^Install$/ })).toHaveCount(0);

  // Load on startup.
  const activation = row('RemoteContent').locator('input[type="checkbox"]');
  await expect(activation).toBeChecked();
  await activation.uncheck();
  await expect(row('RemoteContent').locator('input[type="checkbox"]')).not.toBeChecked();
  await row('RemoteContent').locator('input[type="checkbox"]').check();
  await expect(row('RemoteContent').locator('input[type="checkbox"]')).toBeChecked();
  await expect(row('RemoteContent')).toContainText('Active');
});

test('the preview of a template picks a placeholder', async ({ page }) => {
  await page.goto('/page2.aspx');
  const href = (await page.locator('.moduletools a', { hasText: 'Section Properties' }).first().getAttribute('href'))!;
  await page.goto(href.replace(/^.*\/Admin\//, '/Admin/'));
  const placeholder = page.locator('[id$="ddlPlaceholder"]');
  await expect(placeholder.locator('option')).toHaveText(['searchinput', 'globalMenu', 'side1content', 'maincontent']);
  const [preview] = await Promise.all([page.waitForEvent('popup'), page.locator('#p_hplLookup').click()]);
  await expect(preview.locator('#header input[type="button"]')).toHaveValue('searchinput');
  await preview.locator('input[type="button"][value="side1content"]').click();
  await expect(placeholder).toHaveValue('side1content');
});
