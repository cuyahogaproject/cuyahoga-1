import { test, expect } from '@playwright/test';

// The file manager of CKEditor (~/Support/fileman): its server side in the ASP.NET Core site.

const handler = '/Support/fileman/asp_net/main.ashx';

test('the file manager: folders and files under UserFiles', async ({ page, browser, baseURL }) => {
  const stamp = Date.now().toString(36);
  const folder = `/UserFiles/e2e-${stamp}`;
  const api = (action: string, params: Record<string, string> = {}) =>
    page.request.post(`${handler}?a=${action}`, { form: params });

  // The folders, with the files root first.
  let response = await api('DIRLIST', { type: '' });
  expect(response.status()).toBe(200);
  const dirs = await response.json();
  expect(dirs[0].p).toBe('/UserFiles');

  expect(await (await api('CREATEDIR', { d: '/UserFiles', n: `e2e-${stamp}` })).json()).toEqual({ res: 'ok', msg: '' });
  try {
    // Upload (the answer is a script for the iframe of the upload form), list, rename, copy, delete.
    response = await page.request.post(`${handler}?a=UPLOAD`, {
      multipart: {
        d: folder,
        files: { name: 'a.txt', mimeType: 'text/plain', buffer: Buffer.from('abc') },
      },
    });
    expect(await response.text()).toBe('<script>parent.fileUploaded({"res":"ok","msg":""});</script>');
    let files = await (await api('FILESLIST', { d: folder, type: '' })).json();
    expect(files).toHaveLength(1);
    expect(files[0]).toMatchObject({ p: `${folder}/a.txt`, s: '3', w: '0', h: '0' });

    expect((await (await api('RENAMEFILE', { f: `${folder}/a.txt`, n: 'b.txt' })).json()).res).toBe('ok');
    expect((await (await api('COPYFILE', { f: `${folder}/b.txt`, n: folder })).json()).res).toBe('ok');
    files = await (await api('FILESLIST', { d: folder, type: '' })).json();
    expect(files.map((f: { p: string }) => f.p).sort()).toEqual([`${folder}/b - Copy 1.txt`, `${folder}/b.txt`]);
    const download = await page.request.get(`${handler}?a=DOWNLOAD&f=${encodeURIComponent(`${folder}/b.txt`)}`);
    expect(await download.text()).toBe('abc');

    // Not allowed: other extensions, names with a path, paths outside UserFiles, a folder that isn't empty.
    expect((await (await api('RENAMEFILE', { f: `${folder}/b.txt`, n: 'b.aspx' })).json()).res).toBe('error');
    expect((await (await api('RENAMEFILE', { f: `${folder}/b.txt`, n: '../b.txt' })).json()).res).toBe('error');
    expect(await (await api('FILESLIST', { d: '/bin', type: '' })).json())
      .toEqual({ res: 'error', msg: 'Access to /bin is denied' });
    expect((await (await api('DELETEDIR', { d: folder })).json()).res).toBe('error');
    // Thumbnails are off.
    expect((await (await api('GENERATETHUMB', { f: `${folder}/b.txt` })).json()).res).toBe('error');

    for (const file of ['b.txt', 'b - Copy 1.txt']) {
      expect((await (await api('DELETEFILE', { f: `${folder}/${file}` })).json()).res).toBe('ok');
    }
  } finally {
    await api('DELETEDIR', { d: folder });
  }
  expect(await (await api('FILESLIST', { d: folder, type: '' })).json()).toMatchObject({ res: 'error' });

  // Only for administrators and editors.
  const visitor = await browser.newContext({ baseURL, storageState: { cookies: [], origins: [] } });
  expect((await visitor.request.post(`${handler}?a=DIRLIST`)).status()).toBe(403);
  await visitor.close();
});
