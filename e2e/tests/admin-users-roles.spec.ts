import { test, expect, type Page } from '@playwright/test';
import { adminCredentials, expectHealthyPage } from './helpers';

// The user and role pages of the site administration: create, find, update and delete, plus the rules that protect
// the superuser and the system roles.

const messageBox = (page: Page) => page.locator('[id$="MessageBox"]');

/** The row of a role in the roles table of UserEdit.aspx. */
const userRoleCheckbox = (page: Page, roleName: string) =>
  page.locator('tr', { has: page.locator('td', { hasText: new RegExp(`^${roleName}$`) }) }).locator('input[type="checkbox"]');

/** The row of a role on Roles.aspx. */
const roleRow = (page: Page, roleName: string) =>
  page.locator('tr', { has: page.locator('td', { hasText: new RegExp(`^${roleName}$`) }) });

async function deleteUser(page: Page, userId: string) {
  await page.goto(`/Admin/UserEdit.aspx?UserId=${userId}`);
  page.once('dialog', (dialog) => dialog.accept());
  await page.locator('[id$="btnDelete"]').click();
  await expect(page).toHaveURL(/\/Admin\/Users\.aspx/i);
}

async function deleteRole(page: Page, roleId: string) {
  await page.goto(`/Admin/RoleEdit.aspx?RoleId=${roleId}`);
  page.once('dialog', (dialog) => dialog.accept());
  await page.locator('[id$="btnDelete"]').click();
  await expect(page).toHaveURL(/\/Admin\/Roles\.aspx/i);
}

test('create, find, update and delete a user', async ({ page, browser, baseURL }) => {
  const stamp = Date.now().toString(36);
  const username = `e2e-user-${stamp}`;
  const password = `pw-${stamp}`;

  let response = await page.goto('/Admin/Users.aspx');
  await expectHealthyPage(page, response);
  await page.locator('[id$="btnNew"]').click();
  await expect(page).toHaveURL(/UserEdit\.aspx\?UserId=-1/i);

  // Validation: email is required, the passwords must match and a new user needs a password.
  await page.locator('[id$="txtUsername"]').fill(username);
  await page.locator('[id$="btnSave"]').click();
  await expect(page.getByText('Email is required')).toBeVisible();
  await page.locator('[id$="txtEmail"]').fill(`${username}@example.com`);
  await page.locator('[id$="txtPassword1"]').fill(password);
  await page.locator('[id$="txtPassword2"]').fill('something else');
  await page.locator('[id$="btnSave"]').click();
  await expect(page.getByText('Both passwords must be the same')).toBeVisible();
  await page.locator('[id$="btnSave"]').click();
  await page.locator('[id$="txtPassword1"]').fill('');
  await page.locator('[id$="txtPassword2"]').fill('');
  await page.locator('[id$="btnSave"]').click();
  await expect(messageBox(page)).toContainText('Password is required');

  // Create
  await page.locator('[id$="txtFirstname"]').fill('Erik');
  await page.locator('[id$="txtLastname"]').fill('Tester');
  await page.locator('[id$="txtWebsite"]').fill('http://example.com');
  await page.locator('[id$="chkActive"]').check();
  await page.locator('[id$="txtPassword1"]').fill(password);
  await page.locator('[id$="txtPassword2"]').fill(password);
  await userRoleCheckbox(page, 'Editor').check();
  await page.locator('[id$="btnSave"]').click();
  await expect(page).toHaveURL(/UserEdit\.aspx\?UserId=\d+&message=/i);
  await expect(messageBox(page)).toContainText('User created successfully');
  const userId = /UserId=(\d+)/i.exec(page.url())![1];

  try {
    // The username can't be changed after creation.
    await expect(page.locator('[id$="txtUsername"]')).toHaveCount(0);
    await expect(page.locator('[id$="lblUsername"]')).toHaveText(username);
    await expect(userRoleCheckbox(page, 'Editor')).toBeChecked();
    // The anonymous role can't be assigned.
    await expect(userRoleCheckbox(page, 'Anonymous user')).toHaveCount(0);

    // Find
    await page.goto('/Admin/Users.aspx');
    await page.locator('[id$="txtUsername"]').fill(username);
    await page.locator('[id$="btnFind"]').click();
    const row = page.locator('tr', { hasText: username });
    await expect(row).toHaveCount(1);
    await expect(row).toContainText(`${username}@example.com`);
    await expect(row).toContainText('Tester');
    await row.getByRole('link', { name: 'Edit' }).click();
    await expect(page).toHaveURL(new RegExp(`UserEdit\\.aspx\\?UserId=${userId}$`, 'i'));

    // Update
    await page.locator('[id$="txtEmail"]').fill(`${username}@example.org`);
    await userRoleCheckbox(page, 'Editor').uncheck();
    await userRoleCheckbox(page, 'Authenticated user').check();
    await page.locator('[id$="btnSave"]').click();
    await expect(messageBox(page)).toContainText('User saved');
    await page.goto(`/Admin/UserEdit.aspx?UserId=${userId}`);
    await expect(page.locator('[id$="txtEmail"]')).toHaveValue(`${username}@example.org`);
    await expect(userRoleCheckbox(page, 'Editor')).not.toBeChecked();
    await expect(userRoleCheckbox(page, 'Authenticated user')).toBeChecked();
    await expect(page.locator('[id$="chkActive"]')).toBeChecked();

    // Cancel goes back to the list.
    await page.locator('[id$="btnCancel"]').click();
    await expect(page).toHaveURL(/\/Admin\/Users\.aspx/i);

    // Cancel in the delete confirmation keeps the user.
    await page.goto(`/Admin/UserEdit.aspx?UserId=${userId}`);
    page.once('dialog', (dialog) => dialog.dismiss());
    await page.locator('[id$="btnDelete"]').click();
    await page.goto(`/Admin/UserEdit.aspx?UserId=${userId}`);
    await expect(page.locator('[id$="lblUsername"]')).toHaveText(username);

    // The new user can log in with the password that was set in the admin, but isn't an administrator.
    const context = await browser.newContext({ baseURL, storageState: { cookies: [], origins: [] } });
    const userPage = await context.newPage();
    await userPage.goto('/Login.aspx');
    await userPage.locator('[id$="txtUsername"]').fill(username);
    await userPage.locator('[id$="txtPassword"]').fill(password);
    await userPage.locator('[id$="btnLogin"]').click();
    await expect(userPage.locator('[id$="txtPassword"]')).toHaveCount(0);
    const adminResponse = await userPage.goto('/Admin/Users.aspx');
    expect(adminResponse!.status(), 'a non-administrator gets no access').toBeGreaterThanOrEqual(400);
    await expect(userPage.getByText('Find users')).toHaveCount(0);
    await context.close();
  } finally {
    await deleteUser(page, userId);
  }

  // The deleted user is gone from the search results.
  await page.locator('[id$="txtUsername"]').fill(username);
  await page.locator('[id$="btnFind"]').click();
  await expect(page.locator('tr', { hasText: username })).toHaveCount(0);
});

test('the superuser keeps the administrator role and can\'t delete itself', async ({ page }) => {
  const { username } = adminCredentials();
  await page.goto('/Admin/Users.aspx');
  await page.locator('[id$="txtUsername"]').fill(username);
  await page.locator('[id$="btnFind"]').click();
  await page.locator('tr', { has: page.locator('td', { hasText: new RegExp(`^${username}$`) }) })
    .getByRole('link', { name: 'Edit' }).click();
  const userId = /UserId=(\d+)/i.exec(page.url())![1];

  await userRoleCheckbox(page, 'Administrator').uncheck();
  await page.locator('[id$="btnSave"]').click();
  await expect(messageBox(page)).toContainText(`The user '${username}' has to have the 'Administrator' role.`);
  await page.goto(`/Admin/UserEdit.aspx?UserId=${userId}`);
  await expect(userRoleCheckbox(page, 'Administrator')).toBeChecked();

  page.once('dialog', (dialog) => dialog.accept());
  await page.locator('[id$="btnDelete"]').click();
  await expect(messageBox(page)).toContainText("You can't delete yourself.");
});

test('page through the search results', async ({ page }) => {
  // Ten users per page: eleven users with a common prefix give two pages.
  const prefix = `e2e-page-${Date.now().toString(36)}-`;
  const userIds: string[] = [];
  try {
    for (let i = 0; i < 11; i++) {
      await page.goto('/Admin/UserEdit.aspx?UserId=-1');
      await page.locator('[id$="txtUsername"]').fill(`${prefix}${String(i).padStart(2, '0')}`);
      await page.locator('[id$="txtEmail"]').fill(`${prefix}${i}@example.com`);
      await page.locator('[id$="txtPassword1"]').fill('long-enough-password');
      await page.locator('[id$="txtPassword2"]').fill('long-enough-password');
      await page.locator('[id$="btnSave"]').click();
      await expect(page).toHaveURL(/UserEdit\.aspx\?UserId=\d+&message=/i);
      userIds.push(/UserId=(\d+)/i.exec(page.url())![1]);
    }

    await page.goto('/Admin/Users.aspx');
    await page.locator('[id$="txtUsername"]').fill(prefix);
    await page.locator('[id$="btnFind"]').click();
    const pager = page.locator('[id$="pgrUsers"]');
    const results = page.locator('tr', { hasText: prefix });
    await expect(results).toHaveCount(10);
    await expect(pager.locator('span')).toHaveText('1');
    await expect(pager.getByRole('link')).toHaveText(['2', '>', '>>']);

    await pager.getByRole('link', { name: '2', exact: true }).click();
    await expect(results).toHaveCount(1);
    await expect(results).toContainText(`${prefix}10`);
    await expect(pager.locator('span')).toHaveText('2');
    await expect(pager.getByRole('link')).toHaveText(['<<', '<', '1']);
  } finally {
    for (const userId of userIds) {
      await deleteUser(page, userId);
    }
  }
});

test('log out from the user list', async ({ browser, baseURL }) => {
  // A login of its own: logging out must not end the session of the other tests.
  const context = await browser.newContext({ baseURL, storageState: { cookies: [], origins: [] } });
  const page = await context.newPage();
  const { username, password } = adminCredentials();
  await page.goto('/Login.aspx');
  await page.locator('[id$="txtUsername"]').fill(username);
  await page.locator('[id$="txtPassword"]').fill(password);
  await page.locator('[id$="btnLogin"]').click();
  await expect(page.locator('[id$="txtPassword"]')).toHaveCount(0);

  await page.goto('/Admin/Users.aspx');
  await page.getByRole('link', { name: 'Log out' }).click();
  await expect(page).toHaveURL(/Login\.aspx\?ReturnUrl=/i);
  await page.goto('/Admin/Users.aspx');
  await expect(page).toHaveURL(/Login\.aspx\?ReturnUrl=%2fAdmin%2fUsers\.aspx/i);
  await context.close();
});

test('create, update and delete a role', async ({ page }) => {
  const stamp = Date.now().toString(36);
  const roleName = `e2e role ${stamp}`;

  const response = await page.goto('/Admin/Roles.aspx');
  await expectHealthyPage(page, response);
  // The system roles can't be edited.
  for (const systemRole of ['Administrator', 'Anonymous user']) {
    await expect(roleRow(page, systemRole).getByRole('link', { name: 'Edit' })).toHaveCount(0);
    await expect(roleRow(page, systemRole).locator('img')).toHaveCount(1);
  }

  await page.locator('[id$="btnNew"]').click();
  await expect(page).toHaveURL(/RoleEdit\.aspx\?RoleId=-1/i);

  // Validation: a name and at least one permission.
  await page.locator('[id$="btnSave"]').click();
  await expect(page.getByText('Name is required')).toBeVisible();
  await page.locator('[id$="txtName"]').fill(roleName);
  await page.locator('[id$="btnSave"]').click();
  await expect(messageBox(page)).toContainText('Please select one or more Permission(s)');

  // Create
  await page.getByLabel('Authenticated', { exact: true }).check();
  await page.getByLabel('Editor', { exact: true }).check();
  await page.locator('[id$="btnSave"]').click();
  await expect(page).toHaveURL(/\/Admin\/Roles\.aspx/i);
  const row = roleRow(page, roleName);
  await expect(row).toHaveCount(1);
  await expect(row).toContainText('Authenticated, Editor');
  const editHref = await row.getByRole('link', { name: 'Edit' }).getAttribute('href');
  const roleId = /RoleId=(\d+)/i.exec(editHref!)![1];

  try {
    // Update
    await row.getByRole('link', { name: 'Edit' }).click();
    await expect(page.getByLabel('Editor', { exact: true })).toBeChecked();
    await page.locator('[id$="txtName"]').fill(`${roleName} updated`);
    await page.getByLabel('Editor', { exact: true }).uncheck();
    await page.locator('[id$="btnSave"]').click();
    await expect(messageBox(page)).toContainText('Role saved');
    await page.goto('/Admin/Roles.aspx');
    await expect(roleRow(page, `${roleName} updated`)).toContainText('Authenticated');
    await expect(roleRow(page, `${roleName} updated`)).not.toContainText('Editor');

    // The role can be assigned to users.
    await page.goto('/Admin/UserEdit.aspx?UserId=-1');
    await expect(userRoleCheckbox(page, `${roleName} updated`)).toHaveCount(1);

    // Cancel goes back to the list.
    await page.goto(`/Admin/RoleEdit.aspx?RoleId=${roleId}`);
    await page.locator('[id$="btnCancel"]').click();
    await expect(page).toHaveURL(/\/Admin\/Roles\.aspx/i);
  } finally {
    await deleteRole(page, roleId);
  }
  await expect(roleRow(page, `${roleName} updated`)).toHaveCount(0);
});
