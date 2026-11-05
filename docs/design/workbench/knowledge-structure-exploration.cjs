const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const here = __dirname, dataDir = path.resolve(here, '../data');
const read = p => JSON.parse(fs.readFileSync(p, 'utf8'));
const write = (p, v) => fs.writeFileSync(p, JSON.stringify(v, null, 2) + '\n');
const batchId = 'junna-knowledge-structure-gaps-20261102-v1';
const seedFile = path.join(here, 'seed.json'), seed = read(seedFile);
assert(!seed.seedBatches.some(b => b.id.startsWith(batchId)), '本轮已经导入，保留后续用户修改。');
const live = read(path.join(dataDir, '纯那Mod设计工作台_本地编辑.json'));
const before = structuredClone(live.data), cards = [], groups = [], terms = [], structures = [];
const common = '组内所有牌共用知识关键词，泛指知识牌的效果覆盖全组；指定自己的配套牌时按牌名引用。每组只有一个常规奖励入口，附带牌沿用相同职业卡框与稀有度。奖励、发现按组获得，战斗内生成、复制与变形只处理指定对象；同组各牌分别升级。默认只影响自己，本场修改在战斗结束后清理。';
if (!before.terms.some(t => t.id === 'KW_UNPLAYABLE')) terms.push({uid: 'term_KW_UNPLAYABLE', id: 'KW_UNPLAYABLE', name: '不能被打出', kind: '关键词', definition: '不能被打出的牌无法被打出。', template: '不能被打出。', systems: [], unit: '', notes: '本体原生关键词，原文来自localization/zhs/card_keywords.json。', sources: ['本体']});
let group;
function begin(root, ruleId, structure, rarity, rules, breakthrough = false) {
  group = {root, ruleId, structure, rarity, rules, breakthrough, cardIds: []}; structures.push(group);
  assert(!before.terms.some(t => t.id === ruleId), ruleId);
  terms.push({uid: 'term_' + ruleId, id: ruleId, name: structure, kind: breakthrough ? '突破' : '规则', definition: rules, template: '', systems: ['partner'], unit: '', notes: '知识组结构分类，仅用于设计记录。', sources: ['纯那知识结构补充']});
}
function add(id, type, cost, text, upgradedText = text, options = {}) {
  assert(group && !before.cards.some(c => c.id === id) && !cards.some(c => c.id === id), id);
  const allText = text + upgradedText, termIds = ['KW_PARTNER', group.ruleId, ...(options.termIds || [])], keywords = ['知识'];
  for (const [word, term] of [['保留', 'KW_RETAIN'], ['消耗', 'KW_EXHAUST'], ['虚无', 'KW_ETHEREAL'], ['不能被打出', 'KW_UNPLAYABLE'], ['重放', 'KW_REPLAY']]) if (allText.includes(word)) {termIds.push(term);if (word !== '消耗' || /(^|。)消耗。/.test(text) || /(^|。)消耗。/.test(upgradedText)) keywords.push(word);}
  if (type === '星矢') termIds.push('KW_STAGE');
  if (allText.includes('伤害加成')) termIds.push('KW_BONUS');
  const root = id === group.root;
  cards.push({uid: 'knowledge_structure_' + id, id, name: id, type, system: 'partner', rarity: group.rarity, pool: root, cost, upgradedCost: options.upgradedCost ?? cost,
    turns: type === '星矢' ? options.turns ?? 1 : null, upgradedTurns: type === '星矢' ? options.upgradedTurns ?? options.turns ?? 1 : null,
    ...(type === '星矢' ? {arrowSubtype: options.arrowSubtype || (text.includes('造成') ? '攻击' : '技能')} : {}),
    target: options.target || (type === '攻击' || type === '星矢' && text.includes('造成') ? '敌人' : '自己'),
    text: '知识。' + text, upgradedText: '知识。' + upgradedText, keywords: [...new Set(keywords)], termIds: [...new Set(termIds)], archetypeIds: ['SYS_partner'], breakthroughIds: group.breakthrough ? [group.ruleId] : [], partnerCardIds: [],
    art: 'defaultPortrait', character: '星见纯那', role: root ? group.breakthrough ? '突破限制牌' : '知识组核心牌' : '知识牌', direction: group.structure, draftStatus: '结构探索候选', draftBatch: batchId,
    naming: {original: '', work: '', url: '', adaptation: '卡名待定，当前使用设计编号。'},
    design: {classification: group.breakthrough ? '规则突破' : '知识组结构探索', structure: group.structure, rules: common + '\n' + group.rules, port: options.port || '运转', roleReason: options.roleReason || (root ? '本组的独立奖励入口。' : '承担本组的配套收益或代价。')},
    notes: '知识结构补充；费用与数值为候选。\n结构：' + group.structure + '\n结算约定：' + group.rules + (options.notes ? '\n' + options.notes : '')});
  group.cardIds.push(id);
}
function end() {
  const ids = group.cardIds.slice(1); assert(ids.length);
  groups.push({uid: 'knowledge_structure_group_' + group.root, id: 'GROUP_' + group.root, title: group.root, mainCardId: group.root, grantedCardIds: ids, grantedCardCounts: Object.fromEntries(ids.map(id => [id, 1]))});
}

begin('JK44', 'FK13', '负担打包', '罕见', 'JK44与JK44B一起获得，分别按自己的牌面使用。JK44B基础为0费技能，不能被打出且保留，持续占用手牌；升级后移除保留并获得虚无，回合结束时按虚无处理。两种版本均不能作为正常出牌，也不产生出牌触发。JK44的强度独立于JK44B是否存在；移除负担、仅复制JK44或让升级负担离场，均保留正常收益。关键词按当前升级版本实际生效。');
add('JK44', '攻击', 1, '造成24点伤害。', '造成32点伤害。', {termIds: ['EF01'], port: '输出'});
add('JK44B', '技能', 0, '保留。不能被打出。', '虚无。不能被打出。', {port: '负担', roleReason: '承担牌组占位、抽牌与手牌负担；升级后可自行离场。'});
end();

begin('JK45', 'FK14', '共存代价', '罕见', 'JK45伤害结算前检查自己手牌中是否有JK45B，有则额外造成1次相同伤害；该额外次数与伤害加成、其他次数修正按已有规则处理。手牌中每张JK45B分别使自己星矢牌的当前耗能增加1，移出手牌即停止该来源修正，最低费用按原规则处理。JK45B可正常支付1点能量打出并消耗，升级为0费；它没有其他即时收益。持有它可以保留JK45的第二次伤害，处理它可以停止加费；基础和升级的共存条件相同。');
add('JK45', '星矢', 1, '造成14点伤害。《JK45B》在手牌中时，额外造成1次伤害。', '造成18点伤害。《JK45B》在手牌中时，额外造成1次伤害。', {turns: 1, termIds: ['EF01', 'EF42'], port: '输出'});
add('JK45B', '技能', 1, '保留。在手牌中时，星矢牌耗能增加1。消耗。', undefined, {upgradedCost: 0, port: '负担', roleReason: '持续负担也是同组星矢额外伤害的条件。'});
end();

begin('JK46', 'FK15', '累积负担', '罕见', '每份已生效JK46在自己的回合开始时，先使自己的所有星矢加速2回合，再向弃牌堆生成1张基础版本临时JK46B。该生成不附带另一组牌。每张在手牌中的JK46B，使此时新打出的己方星矢的初始回合数增加1；同名来源相加，已布置的星矢不回溯改数。JK46B可以正常支付1费打出并消耗，升级后0费，基础与升级均保留；新生成版本仍为基础牌。JK46升级只降低安装费用，负担频率与加速量保持。');
add('JK46', '能力', 2, '回合开始时，所有星矢加速2回合，并将1张《JK46B》放入弃牌堆。', undefined, {upgradedCost: 1, termIds: ['TR02', 'EF29', 'EF22'], port: '运转'});
add('JK46B', '技能', 1, '保留。在手牌中时，打出的星矢牌回合数增加1。消耗。', undefined, {upgradedCost: 0, port: '负担'});
end();

begin('JK47', 'FK16', '共用次数', '稀有', '每位玩家在本场战斗中为所有同名JK47B、JK47C共用一个实际打出次数池，初始3次；重复获得、生成、复制不会重置或追加初始值。JK47增加该池剩余次数，基础2、升级3。打出B或C在支付和入队前检查并预留1次，不能支付则不允许打出；主动取消退还预留。免费实际打出也消耗次数，牌效重放与额外执行不再次占用。次数为0时两种牌都不能被打出；JT01转换后的星矢在布置时占次数，完成时不再次占用。两种牌的升级只分别提高伤害与格挡，固定共享初始值保持3。', true);
add('JK47', '技能', 0, '《JK47B》《JK47C》本场战斗合计可多打出2次。消耗。', '《JK47B》《JK47C》本场战斗合计可多打出3次。消耗。', {port: '运转'});
add('JK47B', '攻击', 0, '造成12点伤害。本场战斗与《JK47C》合计只能打出3次。', '造成16点伤害。本场战斗与《JK47C》合计只能打出3次。', {termIds: ['EF01'], port: '输出'});
add('JK47C', '技能', 0, '获得10点格挡。本场战斗与《JK47B》合计只能打出3次。', '获得14点格挡。本场战斗与《JK47B》合计只能打出3次。', {termIds: ['EF05'], port: '防御'});
end();

begin('JK48', 'FK17', '相互抑制', '罕见', 'JK48造成伤害后，使本场自己所有实际JK48B的基础格挡修正减少2；JK48B获得格挡后，使所有实际JK48的基础伤害修正减少3。修正持续本场，最低伤害、格挡均为0；伤害修正与正数伤害加成存量分开记录。按当前实际对象处理，后续新生成从原始牌面开始，复制沿已有修改继承。升级只提高当前牌自身基础伤害或格挡，削弱另一张的数值不变。两种牌都具有独立用途，频繁使用其中一种会降低另一种的价值。');
add('JK48', '攻击', 1, '造成18点伤害。《JK48B》的格挡减少2。', '造成24点伤害。《JK48B》的格挡减少2。', {termIds: ['EF01'], port: '输出'});
add('JK48B', '技能', 1, '获得14点格挡。《JK48》的伤害减少3。', '获得18点格挡。《JK48》的伤害减少3。', {termIds: ['EF05'], port: '防御'});
end();

begin('JK49', 'FK18', '分裂与合并', '稀有', '初始获得JK49与1张JK49B。JK49伤害执行后，当前实际对象在本次整批结算结束时拆为2张JK49B进入自己的弃牌堆，替代原正常去向；一张B承接源对象的伤害加成、重放和本场费用修正，另一张从B原始牌面生成，避免拆分复制数值存量。拆分后的B继承执行源的升级状态。JK49B先正常获得格挡，再可选择手牌中的另一张同名B；当前执行B与所选B在整批后合并为1张JK49进入弃牌堆。两个输入对象各处理一次，不再单独弃置；数值修正按存量合并，输出升级状态沿当前执行B。没有另一张手牌B时仍正常获得格挡。分合均为本场变形，不构成新出牌，不额外发放知识组；战后恢复永久牌组与其独立升级状态。源有重放时重复伤害或格挡，分合去向在整批后处理一次。明确消耗去向优先，已消耗输入不再参与分合。', true);
add('JK49', '攻击', 2, '造成20点伤害。将此牌拆为2张《JK49B》放入弃牌堆。', '造成28点伤害。将此牌拆为2张《JK49B》放入弃牌堆。', {termIds: ['EF01'], port: '输出／运转'});
add('JK49B', '技能', 0, '获得4点格挡。可与手牌中的另一张《JK49B》合并为《JK49》。', '获得6点格挡。可与手牌中的另一张《JK49B》合并为《JK49》。', {termIds: ['EF05'], port: '防御／运转'});
end();

begin('JK50', 'FK19', '费用代付', '稀有', 'JK50的付费选择在能量可支付性检查之前进行：可以正常支付自己的当前耗能，也可以选择手牌中的1张JK50B，快照其当前耗能并以此作为本次实际能量支付量，同时消耗该张B。选择替代方案时只支付一次能量，B没有被打出。JK50自身的耗能字段保持4，费用条件与按当前耗能计算的其他效果继续读取JK50；实际花费读取替代支付量。JK50B的本场加减费会影响代理金额，其基础2费、升级1费，保留且不能正常打出。支付失败或取消则两张牌与能量都不发生修改；无法消耗的媒介不参与选择。免费打出时沿原免费规则处理，可跳过此可选媒介支付。', true);
add('JK50', '攻击', 4, '可消耗手牌中的《JK50B》，改为支付其耗能。造成32点伤害。', '可消耗手牌中的《JK50B》，改为支付其耗能。造成44点伤害。', {termIds: ['EF01'], port: '输出'});
add('JK50B', '技能', 2, '保留。不能被打出。', undefined, {upgradedCost: 1, port: '运转', roleReason: '作为同组卡牌的付费媒介，升级会降低代理支付量。'});
end();

begin('JK51', 'FK20', '去向代付', '稀有', 'JK51本身为1费1回合的格挡星矢，正常完成整批后将被消耗。每份已生效JK51B仅在自己JK51实际将进入消耗牌堆时提供一次替代：可以消耗该能力的实际来源卡并移除对应能力实例，令本次JK51沿原普通去向进入弃牌堆；不让多个B对同一次去向连锁替代。来源B进入自己的消耗牌堆。替代后若另有有效JA22，按其现行约定处理本来会正常弃置的JK51。星矢的额外触发与重放只重复格挡，不新增整批去向；没有实际消耗去向时不支付B。JK51C从消耗牌堆取回1张实际JK51B并自己消耗，不能选择已经在手或仍生效的能力；取回的B需要再次正常打出。B升级只把安装费用降为0，C升级只增加保留，均不改变去向替代次数。', true);
add('JK51', '星矢', 1, '获得20点格挡。消耗。', '获得28点格挡。消耗。', {turns: 1, termIds: ['EF05'], port: '防御'});
add('JK51B', '能力', 1, '《JK51》将被消耗时，可改为消耗此牌。', undefined, {upgradedCost: 0, port: '运转', roleReason: '把指定星矢的消耗去向转移给当前能力来源。'});
add('JK51C', '技能', 0, '将消耗牌堆中的1张《JK51B》放入手牌。消耗。', '保留。将消耗牌堆中的1张《JK51B》放入手牌。消耗。', {termIds: ['EF16'], port: '运转'});
end();

assert.equal(groups.length, 8); assert.equal(cards.length, 18);
const data = structuredClone(before); data.cards.push(...cards); data.terms.push(...terms); data.partnerGroups.push(...groups);
const app = fs.readFileSync(path.join(here, 'app.js'), 'utf8');
const context = {seed: {data}, clone: structuredClone, uid: () => {throw Error('缺少稳定编号');}, COLLECTIONS: ['mechanisms', 'terms', 'cards', 'partnerGroups', 'archetypes', 'styles'], TYPES: ['攻击', '技能', '能力', '星矢'], RARITIES: ['基础', '普通', '罕见', '稀有', '远古']};
vm.createContext(context); vm.runInContext(app.slice(app.indexOf('  function normalizeData('), app.indexOf('  function patchesBetween(')), context);
const normalized = JSON.parse(JSON.stringify(context.normalizeData(data)));
for (const old of before.cards) assert.deepEqual(normalized.cards.find(c => c.id === old.id), old, '保留旧牌 ' + old.id);
for (const key of ['meta', 'styles', 'mechanisms', 'archetypes', 'title']) assert.deepEqual(normalized[key], before[key]);
for (const g of groups) for (const id of g.grantedCardIds) {const c = normalized.cards.find(c => c.id === id); assert.equal(c.pool, false); assert.equal(c.rarity, g.mainCardId && normalized.cards.find(c => c.id === g.mainCardId).rarity);}
for (const c of cards) {assert(c.text.startsWith('知识。') && c.upgradedText.startsWith('知识。'));assert(!c.text.includes('所有玩家'));if (c.type === '星矢') assert(['攻击', '技能', '能力'].includes(c.arrowSubtype));}
const batches = [
  {id: batchId + '-terms', label: '知识结构补充规则', collection: 'terms', ids: terms.map(t => t.id), entries: terms},
  {id: batchId + '-cards', label: '新增八组知识结构牌面', collection: 'cards', ids: cards.map(c => c.id), entries: cards},
  {id: batchId + '-groups', label: '新增八组知识关联', collection: 'partnerGroups', ids: groups.map(g => g.id), entries: groups}
];
write(path.join(here, '.tmp-knowledge-structure-before.json'), {revision: live.revision, data: before, expected: normalized});
write(path.join(dataDir, '纯那知识结构补充_20261102.json'), {schemaVersion: 1, id: batchId, date: '2026-11-02', sourceRevision: live.revision, title: '知识结构空缺补充', summary: {groups: groups.length, cards: cards.length, entryCards: groups.length, breakthroughGroups: structures.filter(g => g.breakthrough).length}, styleSnapshot: before.styles, structures, cards: normalized.cards.filter(c => cards.some(n => n.id === c.id)), groups, terms});
for (const key of ['data', 'revision', 'history', 'undoStack', 'redoStack', 'ui', 'appliedSeedBatches']) if (live[key] !== undefined) seed[key] = structuredClone(live[key]);
seed.assets = {...seed.assets, ...live.assets}; seed.seedBatches.push(...batches); write(seedFile, seed);
const trackingFile = path.join(dataDir, '纯那职业卡池设计记录.json'), tracking = read(trackingFile);
tracking.batches.push({id: batchId, date: '2026-11-02', sourceRevision: live.revision, newGroups: groups.length, newCards: cards.length, source: '纯那知识结构补充_20261102.json'}); write(trackingFile, tracking);
console.log(JSON.stringify({sourceRevision: live.revision, newCards: cards.length, newGroups: groups.length, structures: structures.map(g => ({id: g.root, structure: g.structure, breakthrough: g.breakthrough})), oldCardsPreserved: true}));
