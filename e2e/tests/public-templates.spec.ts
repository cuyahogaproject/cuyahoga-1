import { test, expect, type Page } from '@playwright/test';
import { adminStorageState } from '../playwright.config';
import { createNode, createSection, deleteNode, expectHealthyPage } from './helpers';

// The Classic and Default templates of the sample site in the ASP.NET Core site (they were compared pixel for pixel
// with the WebForms site while they moved): Page 2 and a page below it get each template in turn.

async function setTemplate(admin: Page, nodeId: string, template: string) {
  await admin.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
  await admin.locator('[id$="ddlTemplates"]').selectOption({ label: template });
  await admin.locator('[id$="btnSave"]').click();
  await expect(admin.locator('[id$="MessageBox"]')).toContainText('Node saved.');
}

async function open(page: Page, url: string, stylesheet: string) {
  await expectHealthyPage(page, await page.goto(url));
  await expect(page.locator('input[name="__VIEWSTATE"]'), `${url} rendered by the ASP.NET Core site`).toHaveCount(0);
  await expect(page.locator('head link[rel="stylesheet"]').first()).toHaveAttribute('href', stylesheet);
}

test('the Classic and Default templates', async ({ page, browser }) => {
  const admin = await browser.newContext({ storageState: adminStorageState });
  const adminPage = await admin.newPage();
  await adminPage.goto('/page2.aspx');
  const href = await adminPage.locator('.moduletools a', { hasText: 'Section Properties' }).first().getAttribute('href');
  const page2Id = /NodeId=(\d+)/.exec(href!)![1];
  const childId = await createNode(adminPage, page2Id, 'Sub page');
  try {
    await createSection(adminPage, childId, 'Sub', 'StaticHtml');
    await adminPage.goto(`/Admin/NodeEdit.aspx?NodeId=${childId}`);
    const childUrl = '/' + await adminPage.locator('[id$="txtShortDescription"]').inputValue() + '.aspx';

    // Classic: the main menu (the home page and its children), with the Admin link for administrators.
    for (const template of ['Cuyahoga Home', 'Cuyahoga Standard']) {
      await setTemplate(adminPage, page2Id, template);
      await setTemplate(adminPage, childId, template);
      await open(page, '/page2.aspx', '/Templates/Classic/Css/red.css');
      await expect(page.locator('#logoarea #title')).toHaveText('Cuyahoga');
      await expect(page.locator('#mainmenu > li > a').first()).toHaveText('Home');
      await expect(page.locator('#mainmenu a.selected')).toHaveText('Page 2');
      await expect(page.locator('#mainmenu a', { hasText: 'Admin' })).toHaveCount(0);
      await expect(page.locator('#main .section')).toHaveCount(1);
      await open(adminPage, '/page2.aspx', '/Templates/Classic/Css/red.css');
      await expect(adminPage.locator('#mainmenu a', { hasText: 'Admin' })).toHaveAttribute('href', '/Admin');
    }
    // Cuyahoga Standard: the submenu with the pages below the level 1 page.
    await open(page, childUrl, '/Templates/Classic/Css/red.css');
    await expect(page.locator('#side1 .submenu a.subselected')).toHaveText('Sub page');
    await expect(page.locator('#mainmenu a.selected')).toHaveText('Page 2');

    // Default: the hierarchical menu.
    await setTemplate(adminPage, page2Id, 'Cuyahoga New');
    await setTemplate(adminPage, childId, 'Cuyahoga New');
    await open(page, childUrl, '/Templates/Default/Css/red-new.css');
    await expect(page.locator('#headerside #title')).toHaveText('Cuyahoga');
    await expect(page.locator('#nav a', { hasText: 'Page 2' })).toBeVisible();
    await expect(page.locator('#nav a.selected')).toHaveText('Sub page');
    await expect(page.locator('.section')).toHaveCount(1);
  } finally {
    await setTemplate(adminPage, page2Id, 'Another Red');
    await deleteNode(adminPage, childId);
    await admin.close();
  }
});
