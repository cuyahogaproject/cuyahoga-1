import { test, expect, type Page, type TestInfo } from '@playwright/test';
import { adminStorageState } from '../playwright.config';
import { createNode, createSection, deleteNode } from './helpers';

// The Classic and Default templates of the sample site in the ASP.NET Core site, the same as on the WebForms site:
// Page 2 and a page below it get each template in turn.
const legacyBaseUrl = 'http://localhost:8082';

async function same(page: Page, legacyPage: Page, url: string, label: string, testInfo: TestInfo) {
  for (const p of [page, legacyPage]) await p.goto(url);
  expect(await page.locator('input[name="__VIEWSTATE"]').count(), `${label}: ASP.NET Core`).toBe(0);
  const a = await page.screenshot({ fullPage: true });
  const b = await legacyPage.screenshot({ fullPage: true });
  if (!a.equals(b)) {
    await testInfo.attach(label + ' core', { body: a, contentType: 'image/png' });
    await testInfo.attach(label + ' legacy', { body: b, contentType: 'image/png' });
    await testInfo.attach(label + ' core.html', { body: await page.content(), contentType: 'text/plain' });
    await testInfo.attach(label + ' legacy.html', { body: await legacyPage.content(), contentType: 'text/plain' });
  }
  expect(a.equals(b), `${label} looks the same as on the WebForms site`).toBe(true);
}

async function setTemplate(admin: Page, nodeId: string, template: string) {
  await admin.goto(`/Admin/NodeEdit.aspx?NodeId=${nodeId}`);
  await admin.locator('[id$="ddlTemplates"]').selectOption({ label: template });
  await admin.locator('[id$="btnSave"]').click();
  await expect(admin.locator('[id$="MessageBox"]')).toContainText('Node saved.');
}

test('the Classic and Default templates look the same as on the WebForms site', async ({ page, browser, baseURL }, testInfo) => {
  const headers = { 'X-Forwarded-Host': new URL(baseURL!).host, 'X-Forwarded-Proto': 'http' };
  const legacy = await browser.newContext({ baseURL: legacyBaseUrl, extraHTTPHeaders: headers });
  const legacyAdmin = await browser.newContext({ baseURL: legacyBaseUrl, extraHTTPHeaders: headers, storageState: adminStorageState });
  const admin = await browser.newContext({ storageState: adminStorageState });
  const adminPage = await admin.newPage();
  const legacyPage = await legacy.newPage();
  const legacyAdminPage = await legacyAdmin.newPage();
  await adminPage.goto('/page2.aspx');
  const href = await adminPage.locator('.moduletools a', { hasText: 'Section Properties' }).first().getAttribute('href');
  const page2Id = /NodeId=(\d+)/.exec(href!)![1];
  const childId = await createNode(adminPage, page2Id, 'Sub page');
  try {
    await createSection(adminPage, childId, 'Sub', 'StaticHtml');
    await adminPage.goto(`/Admin/NodeEdit.aspx?NodeId=${childId}`);
    const childUrl = '/' + await adminPage.locator('[id$="txtShortDescription"]').inputValue() + '.aspx';
    for (const template of ['Cuyahoga Home', 'Cuyahoga Standard', 'Cuyahoga New']) {
      await setTemplate(adminPage, page2Id, template);
      await setTemplate(adminPage, childId, template);
      await same(page, legacyPage, '/page2.aspx', `${template} page 2`, testInfo);
      await same(page, legacyPage, childUrl, `${template} child`, testInfo);
      await same(adminPage, legacyAdminPage, '/page2.aspx', `${template} page 2 admin`, testInfo);
    }
  } finally {
    await setTemplate(adminPage, page2Id, 'Another Red');
    await deleteNode(adminPage, childId);
    await legacy.close();
    await legacyAdmin.close();
    await admin.close();
  }
});
