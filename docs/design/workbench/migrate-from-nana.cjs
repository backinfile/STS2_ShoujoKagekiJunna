// One-time migration. Subsequent builds use only files inside the Junna project.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const source = process.argv[2] || 'D:/Godot/Proj/STS2_ShoujoKagekiAijoNana/docs/design';
const here = __dirname;
const seedFile = path.join(here, 'seed.json');
if (fs.existsSync(seedFile)) throw Error('初始数据已存在，保留当前文件。');
const read = file => fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, '');
const snapshotFile = path.join(source, 'data/奈奈Mod制作台_本地编辑.json');
const snapshotText = read(snapshotFile);
const snapshot = JSON.parse(snapshotText);
const replaceText = value => value.replaceAll('舞台装置', '箭矢').replaceAll('装置', '箭矢').replaceAll('伙伴', '知识').replaceAll('奈奈', '纯那');
const adapt = value => typeof value === 'string' ? replaceText(value) : Array.isArray(value) ? value.map(adapt) : value && typeof value === 'object' ? Object.fromEntries(Object.entries(value).map(([key, item]) => [key, adapt(item)])) : value;
const data = adapt(snapshot.data);
const sourceCards = snapshot.data.cards;
const quotedNames = sourceCards.map(card => `《${card.name}》`);
const specificNames = ['RE:CREATE', 'Starlight', 'Star Divine', 'Star Diamond', '再演', '舞台少女的闪耀', '压轴斩', '花道', '任性的高速公路', '第一颗星', '情热', '追星刃'];
const mentionsCard = text => [...quotedNames, ...specificNames].some(name => text.includes(name));

data.title = '纯那 Mod 设计工作台';
data.cards = [];
data.partnerGroups = [];
data.styles = [];
data.terms = data.terms.filter(term => !term.cardId && !['ST_INSTANT', 'ST_FINISH', 'ST_FIRST_STAR'].includes(term.id));
// Keep generic effect templates while removing references to previous card drafts.
for (const term of data.terms) {
  for (const key of ['definition', 'template', 'upgradedTemplate', 'notes']) {
    if (typeof term[key] !== 'string') continue;
    term[key] = term[key].replace(/《[^》]+》/g, '《牌名》');
    for (const name of specificNames) term[key] = term[key].replaceAll(name, '《牌名》');
  }
  term.sources = (term.sources || []).map(value => value === '完整卡池' ? '既有体系规则' : value);
}
const stage = data.mechanisms.find(item => item.system === 'stage');
stage.rules = stage.rules.map(rule => rule.includes('通用重放与耗能修改')
  ? '箭矢牌的通用重放与耗能修改对所有玩家有效。布置时汇总有效的重放增加量，多名纯那各提供重放1时合计重放2，归0时总共执行3次；通用耗能修改按牌面指定的范围处理。'
  : rule);
const knowledge = data.mechanisms.find(item => item.system === 'partner');
knowledge.rules = knowledge.rules.filter(rule => !rule.includes('倾向于') && !rule.includes('RE:CREATE'));
data.mechanisms.find(item => item.system === 'growth').name = '伤害加成';
for (const archetype of data.archetypes) if (archetype.system === 'growth') archetype.name = archetype.definition = '伤害加成';
data.meta.startingDeck = [];
data.meta.namingRequirements = {general: '使用原作完整词语或忠实中文翻译，保留出处。'};
data.meta.rewardRules = Object.fromEntries(Object.entries(data.meta.rewardRules).map(([key, value]) => [key, value.replace(/[，、]\d+种/g, '')]));
data.meta.ruleConventions = data.meta.ruleConventions.filter(rule => !mentionsCard(rule));
data.meta.systemNames = {stage: '箭矢', partner: '知识', growth: '伤害加成', utility: '体系外'};
data.meta.sourceDate = '2026-11-01';
delete data.meta.legacyRevisions;
const state = {schemaVersion: 1, workspaceId: 'junna-mod-workbench-20261101-v1', revision: 0, data, history: [], undoStack: [], redoStack: [], assets: {}, seedBatches: [], appliedSeedBatches: [], ui: {tab: 'mechanisms', selected: null, mechanismFilter: 'all'}};
fs.writeFileSync(seedFile, JSON.stringify(state, null, 2) + '\n');

for (const file of ['app.js', 'shell.html', 'workbench.css', 'local-save-server.cjs', 'start-local-save.ps1']) {
  let content = replaceText(read(path.join(source, 'workbench', file)))
    .replaceAll('nana', 'junna').replaceAll('NANA', 'JUNNA')
    .replaceAll('20261031-v1', '20261101-v1')
    .replaceAll('纯那 Mod 制作台', '纯那 Mod 设计工作台')
    .replaceAll('纯那Mod制作台', '纯那Mod设计工作台');
  if (file === 'local-save-server.cjs') content = content.replace('port:17841', 'port:17842');
  if (file === 'app.js') {
    // A blank Junna pool has no historical Nana card migrations.
    content = content.replace(/if\(c\.id==='NS07'&&[\s\S]*?clone\(latest\[key\]\);\}/, '');
    content = content.replace(/^  if\(!saved&&!mirror\)\{try\{const old=[^\n]*\n/m, '');
  }
  if (file === 'shell.html') content = content.replaceAll('机制定稿', '机制');
  if (file === 'app.js') content = content.replaceAll('机制定稿', '机制');
  if (file === 'workbench.css') {
    // Keep the established layout; use Junna's blue accent for workspace chrome.
    content = content.replaceAll('#d9c37c', '#85cad7').replaceAll('#d8ccaa', '#bddce3').replaceAll('#cfc9b6', '#c3d9df')
      .replaceAll('#8e7c43', '#588f9c').replaceAll('#d9c37c0c', '#85cad70c').replaceAll('#c8b36a99', '#85cad799');
  }
  fs.writeFileSync(path.join(here, file), content);
}
const sourceHTML = read(path.join(source, '奈奈Mod制作台.html'));
const assetMatch = sourceHTML.match(/<script id="workbench-assets" type="application\/json">([\s\S]*?)<\/script>/);
if (!assetMatch) throw Error('原制作台缺少内置素材。');
const assets = JSON.parse(assetMatch[1]);
// Neutral vector placeholder; each future card can receive its own illustration.
const placeholder = '<svg xmlns="http://www.w3.org/2000/svg" width="500" height="460" viewBox="0 0 500 460"><rect width="500" height="460" fill="#1a2934"/><g fill="none" stroke="#466778" stroke-width="2"><path d="M86 345 181 117 312 179 413 73M181 117 268 337 411 375M312 179 268 337"/></g><g fill="#83c8d5"><circle cx="86" cy="345" r="4"/><circle cx="181" cy="117" r="5"/><circle cx="312" cy="179" r="4"/><circle cx="413" cy="73" r="4"/><circle cx="268" cy="337" r="4"/><circle cx="411" cy="375" r="3"/><path d="m250 173 5 18 18 5-18 5-5 18-5-18-18-5 18-5Z"/></g></svg>';
assets.defaultPortrait = 'data:image/svg+xml;base64,' + Buffer.from(placeholder).toString('base64');
fs.writeFileSync(path.join(here, 'assets.json'), JSON.stringify(assets));
fs.writeFileSync(path.join(here, 'provenance.json'), JSON.stringify({source: snapshotFile, sourceRevision: snapshot.revision, sha256: crypto.createHash('sha256').update(snapshotText).digest('hex'), mappings: {'舞台装置': '箭矢', '伙伴': '知识'}, retained: ['机制规则', '通用词条', '编辑功能'], empty: ['卡池', '知识关联组', '初始牌组', '风格约定', '编辑历史'], excludedTerms: snapshot.data.terms.filter(term => !data.terms.some(retained => retained.id === term.id)).map(term => term.id)}, null, 2) + '\n');
console.log(JSON.stringify({mechanisms: data.mechanisms.length, terms: data.terms.length, cards: data.cards.length, assets: Object.keys(assets).length}));
