import { expect, test as setup } from '@playwright/test';
import { createNode, createSection, homeNodeId, sectionIdByTitle } from './helpers';

// Adds the pages the tests need on top of the sample site of the installer (Home, Articles, Page 2):
// a search page, a downloads page and an overview page (Sitemap module, and a search box connected to the search page).
setup('add search, downloads and overview pages', async ({ page }) => {
  const homeId = await homeNodeId(page);

  const searchNodeId = await createNode(page, homeId, 'Search');
  await createSection(page, searchNodeId, 'Search', 'Search', { RESULTS_PER_PAGE: '10', SHOW_INPUT_PANEL: true });

  const downloadsNodeId = await createNode(page, homeId, 'Downloads');
  await createSection(page, downloadsNodeId, 'Downloads', 'Downloads', { SHOW_PUBLISHER: true, SHOW_DATE: true });

  // Not 'Sitemap': sitemap.aspx is the XML sitemap in every folder.
  const overviewNodeId = await createNode(page, homeId, 'Overview');
  await createSection(page, overviewNodeId, 'Overview', 'Sitemap');
  await createSection(page, overviewNodeId, 'Search box', 'SearchInput');
  const searchBoxId = await sectionIdByTitle(page, overviewNodeId, 'Search box');
  await page.goto(`/Admin/ConnectionEdit.aspx?SectionId=${searchBoxId}`);
  await page.locator('[id$="ddlSectionTo"]').selectOption({ label: 'Search - Search - Search' });
  await page.locator('[id$="btnSave"]').click();
  await expect(page).toHaveURL(new RegExp(`SectionEdit\\.aspx\\?(NodeId=\\d+&)?SectionId=${searchBoxId}$`, 'i'));
});
