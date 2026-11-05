const {chromium} = require('C:/Users/17575/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const {pathToFileURL} = require('node:url');
const {createSaveServer} = require('./local-save-server.cjs');
const id = 'junna-mod-workbench-20261101-v1';
const here = __dirname;

(async () => {
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'junna-workbench-'));
  const previews = path.join(here, 'previews');
  fs.mkdirSync(previews, {recursive: true});
  const seed = JSON.parse(fs.readFileSync(path.join(here, 'seed.json'), 'utf8'));
  const baseline = JSON.stringify(seed.data);
  assert.equal(seed.workspaceId, id);
  assert.deepEqual(seed.data.mechanisms.map(item => item.name), ['箭矢', '知识', '伤害加成']);
  const initialCards = seed.data.cards.filter(card => !card.trashedAt).length, totalCards = seed.data.cards.length, initialTerms = seed.data.terms.length;
  assert(initialCards > 0 && initialTerms >= 316);
  assert(!/奈奈|舞台装置|伙伴/.test(baseline));
  const browser = await chromium.launch({headless: true, downloadsPath: path.join(previews, 'downloads'), executablePath: 'C:/Users/17575/AppData/Local/ms-playwright/chromium_headless_shell-1234/chrome-headless-shell-win64/chrome-headless-shell.exe'});
  let server;
  try {
    const context = await browser.newContext({viewport: {width: 1440, height: 1000}, acceptDownloads: true});
    await context.addInitScript(workspace => localStorage.setItem('junna-workbench:' + workspace + ':local-service-disabled', '1'), id);
    const page = await context.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    const url = pathToFileURL(path.join(here, '../纯那Mod设计工作台.html')).href;
    const ready = () => page.waitForFunction(() => window.__JUNNA_WORKBENCH__?.ready);
    const state = () => page.evaluate(() => window.__JUNNA_WORKBENCH__.getState());
    const flush = () => page.evaluate(() => window.__JUNNA_WORKBENCH__.flush());
    await page.goto(url);
    await ready();
    await page.evaluate(() => document.fonts.ready);
    assert.equal(await page.locator('.mechanism-sheet').count(), 3);
    await page.screenshot({path: path.join(previews, '纯那工作台-机制.png')});

    await page.getByRole('tab', {name: '效果与扳机', exact: true}).click();
    assert.equal(await page.locator('[data-term]').count(), initialTerms);
    await page.locator('#filter-search').fill('EF29');
    assert.equal(await page.locator('[data-term]').count(), 1);
    await page.locator('[data-term]').click();
    const original = (await state()).data.terms.find(term => term.id === 'EF29').template;
    await page.locator('[data-field="template"]').fill('箭矢加速M回合。');
    await page.locator('[data-field="template"]').blur();
    await flush();
    assert.equal((await state()).data.terms.find(term => term.id === 'EF29').template, '箭矢加速M回合。');
    await page.locator('#undo').click();
    await flush();
    assert.equal((await state()).data.terms.find(term => term.id === 'EF29').template, original);
    await page.locator('#redo').click();
    await flush();
    await page.reload();
    await ready();
    assert.equal((await state()).data.terms.find(term => term.id === 'EF29').template, '箭矢加速M回合。');
    await page.evaluate(() => window.__JUNNA_WORKBENCH__.undo());
    await flush();
    await page.getByRole('tab', {name: '效果与扳机', exact: true}).click();
    await page.screenshot({path: path.join(previews, '纯那工作台-词条.png')});

    await page.getByRole('tab', {name: '卡池', exact: true}).click();
    assert.equal(await page.locator('.card-entry').count(), initialCards);
    await page.locator('[data-action="add"]').click();
    await page.locator('[data-field="type"]').selectOption('箭矢');
    await flush();
    await page.evaluate(() => document.fonts.ready);
    assert.equal((await state()).data.cards.at(-1).type, '箭矢');
    assert.equal(await page.locator('.preview [data-type="stage"]').count(), 1);
    assert.equal(await page.evaluate(() => [...document.images].filter(image => !image.complete || !image.naturalWidth).length), 0);

    // Exports are tested in a second browser context; production data stays untouched.
    await page.locator('.menu > summary').click();
    const jsonEvent = page.waitForEvent('download');
    await page.locator('#export-json').click();
    const jsonDownload = await jsonEvent;
    const jsonFile = path.join(temp, 'export.json');
    await jsonDownload.saveAs(jsonFile);
    const exported = JSON.parse(fs.readFileSync(jsonFile, 'utf8'));
    assert.equal(exported.workspaceId, id);
    assert(exported.history.length > 0);
    const htmlEvent = page.waitForEvent('download');
    await page.locator('#export-html').click();
    const htmlFile = path.join(temp, 'export.html');
    await (await htmlEvent).saveAs(htmlFile);
    const exportedHTML = fs.readFileSync(htmlFile, 'utf8');
    assert(!exportedHTML.includes('<script id="workbench-connection"'));
    const freshContext = await browser.newContext({viewport: {width: 1440, height: 1000}});
    const fresh = await freshContext.newPage();
    fresh.on('pageerror', error => errors.push(error.message));
    await fresh.goto(pathToFileURL(htmlFile).href);
    await fresh.waitForFunction(() => window.__JUNNA_WORKBENCH__?.ready);
    assert.equal(await fresh.evaluate(() => window.__JUNNA_WORKBENCH__.getState().data.cards.length), totalCards + 1);
    await fresh.setInputFiles('#import-file', jsonFile);
    await fresh.waitForFunction(() => window.__JUNNA_WORKBENCH__.getState().workspaceId.startsWith('junna-workbench-copy-'));
    assert.equal(await fresh.evaluate(() => window.__JUNNA_WORKBENCH__.getState().data.terms.length), initialTerms);

    // Return to the clean initial content for the narrow-screen screenshot.
    await page.evaluate(value => window.__JUNNA_WORKBENCH__.importBundle(value), seed);
    await page.getByRole('tab', {name: '机制', exact: true}).click();
    await page.locator('.menu').evaluate(menu => menu.removeAttribute('open'));
    await page.locator('#toast').waitFor({state: 'hidden'});
    await page.setViewportSize({width: 390, height: 844});
    await page.screenshot({path: path.join(previews, '纯那工作台-窄屏.png')});
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
    assert.deepEqual((await state()).data, seed.data);
    assert.deepEqual(errors, []);

    // Verify the save endpoint against a temporary file, including conflict protection.
    const file = path.join(temp, 'local.json');
    fs.writeFileSync(file, JSON.stringify(seed));
    const token = crypto.randomBytes(32).toString('hex');
    server = createSaveServer({file, token, expectedWorkspace: id});
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const base = 'http://127.0.0.1:' + server.address().port;
    const headers = {Authorization: 'Bearer ' + token};
    const documentResponse = await fetch(base + '/');
    assert.equal(documentResponse.status, 200);
    assert((await documentResponse.text()).includes('<title>纯那 Mod 设计工作台</title>'));
    const initial = await fetch(base + '/file', {headers});
    assert.equal(initial.status, 200);
    const etag = initial.headers.get('etag');
    const updated = {...seed, revision: 1};
    assert.equal((await fetch(base + '/file', {method: 'PUT', headers: {...headers, 'If-Match': etag}, body: JSON.stringify(updated)})).status, 200);
    assert.equal(JSON.parse(fs.readFileSync(file, 'utf8')).revision, 1);
    assert.equal((await fetch(base + '/file', {method: 'PUT', headers: {...headers, 'If-Match': etag}, body: JSON.stringify(updated)})).status, 412);
    assert.equal((await fetch(base + '/health')).status, 401);
    console.log(JSON.stringify({passed: true, checks: ['体系名称与候选牌稿', '词条搜索与编辑', '撤销重做与刷新恢复', '箭矢牌预览', 'JSON与HTML导出及导入', '窄屏布局', '本地保存与版本冲突'], screenshots: 3, errors}));
  } finally {
    if (server) await new Promise(resolve => server.close(resolve));
    await browser.close();
    fs.rmSync(temp, {recursive: true, force: true});
  }
})().catch(error => {console.error(error); process.exitCode = 1;});
