const fs = require('node:fs');
const path = require('node:path');
const {ensureConfig} = require('./mcp-config.cjs');
const here = __dirname;
const design = path.dirname(here);
const readJSON = file => JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
const safe = value => JSON.stringify(value).replace(/</g, '\\u003c').replace(/\u2028/g, '\\u2028').replace(/\u2029/g, '\\u2029');
const snapshotArg = process.argv.indexOf('--snapshot');
const state = readJSON(snapshotArg<0?path.join(here, 'seed.json'):path.resolve(process.argv[snapshotArg+1]));
const assets = readJSON(path.join(here, 'assets.json'));
assets['floating-arrow-frame'] = 'data:image/svg+xml;base64,' + fs.readFileSync(path.join(design, '../..', 'ShoujoKagekiJunna/images/floating_arrow_c1.svg')).toString('base64');
for (const [id, file] of Object.entries({
  'energy-junna': 'junna-energy-workbench-v4.png',
  'portrait-stage-junna': 'arrow-portrait-workbench-clean-v5.png'
})) assets[id] = 'data:image/png;base64,' + fs.readFileSync(path.join(design, 'assets/arrow/2026-11-02', file)).toString('base64');
const config = ensureConfig();
const fileName = '纯那Mod设计工作台_本地编辑.json';
const connection = {baseURL: 'http://127.0.0.1:' + config.port, token: config.token, workspaceId: state.workspaceId, fileName};
const css = ['workbench.css', 'card-model.css', 'fit-pool.css', 'relic.css', 'power.css', 'card-hover-tips.css', 'initial-config.css','mobile.css'].map(file => fs.readFileSync(path.join(here, file), 'utf8')).join('\n').replaceAll('__REGULAR_FONT__', assets['game-regular']).replaceAll('__BOLD_FONT__', assets['game-bold']);
const html = fs.readFileSync(path.join(here, 'shell.html'), 'utf8')
  .replace('<!--PWA-->', '')
  .replace('<!--STYLE-->', () => '<style>' + css + '</style>')
  .replace('<!--CONNECTION-->', () => '<script id="workbench-connection" type="application/json">' + safe(connection) + '</script>')
  .replace('<!--ASSETS-->', () => '<script id="workbench-assets" type="application/json">' + safe(assets) + '</script>')
  .replace('<!--DATA-->', () => '<script id="workbench-data" type="application/json">' + safe(state) + '</script>')
  .replace('<!--ROUTE_DATA-->', () => '<script id="workbench-route-data" type="application/json">' + safe(readJSON(path.join(here, 'route-statistics-defaults.json'))) + '</script>')
  .replace('<!--APP-->', () => '<script id="workbench-battle-sim" type="application/json">' + safe(fs.existsSync(path.join(design, 'data/纯那箭矢等待补偿战斗模拟_20261103.json'))?readJSON(path.join(design, 'data/纯那箭矢等待补偿战斗模拟_20261103.json')):null) + '</script><script id="workbench-card-audit" type="application/json">' + safe(readJSON(path.join(design, 'data/纯那全卡牌数值校验_20261103.json'))) + '</script><script>' + ['arrow-focus.js','energy-wording.js', 'card-reference-wording.js', 'effect-statistics.js', 'route-statistics.js', 'route-statistics-view.js', 'pool-reference.js','native-card-pools.js','native-pool-comparison.js', 'card-model-view.js', 'fit-pool-view.js', 'relic-view.js', 'power-view.js', 'card-hover-tips.js', 'workbench-domain.js', 'initial-config.js','mobile-layout.js', 'mcp-web-client.js', 'app.js'].map(file => fs.readFileSync(path.join(here, file), 'utf8')).join('\n') + '</script>');
fs.mkdirSync(path.join(design, 'data'), {recursive: true});
const localFile = path.join(design, 'data', fileName);
if (!fs.existsSync(localFile)) fs.writeFileSync(localFile, JSON.stringify(state, null, 2) + '\n', {flag: 'wx'});
fs.writeFileSync(path.join(design, '纯那Mod设计工作台.html'), html);
console.log(JSON.stringify({file: '纯那Mod设计工作台.html', mechanisms: state.data.mechanisms.length, terms: state.data.terms.length, cards: state.data.cards.length, htmlBytes: Buffer.byteLength(html)}));
