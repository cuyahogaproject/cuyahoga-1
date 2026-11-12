import { test as setup } from '@playwright/test';
import { createNode, createSection, homeNodeId } from './helpers';

// Adds the pages the tests need on top of the sample site of the installer (Home, Articles, Page 2):
// a search page and a downloads page.
setup('add search and downloads pages', async ({ page }) => {
  const homeId = await homeNodeId(page);

  const searchNodeId = await createNode(page, homeId, 'Search');
  await createSection(page, searchNodeId, 'Search', 'Search', { RESULTS_PER_PAGE: '10', SHOW_INPUT_PANEL: true });

  const downloadsNodeId = await createNode(page, homeId, 'Downloads');
  await createSection(page, downloadsNodeId, 'Downloads', 'Downloads', { SHOW_PUBLISHER: true, SHOW_DATE: true });
});
