const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const here = __dirname, dataDir = path.resolve(here, '../data');
const read = p => JSON.parse(fs.readFileSync(p, 'utf8'));
const write = (p, v) => fs.writeFileSync(p, JSON.stringify(v, null, 2) + '\n');
const batchId = 'junna-personal-arrow-scope-20261102-v1';
const seedFile = path.join(here, 'seed.json'), seed = read(seedFile);
assert(!seed.seedBatches.some(b => b.id.startsWith(batchId)), '本轮规则已导入，保留后续用户修改。');
const live = read(path.join(dataDir, '纯那Mod设计工作台_本地编辑.json'));
const before = structuredClone(live.data), data = structuredClone(before), updates = [];
function update(collection, id, values) {
  const entry = collection === 'root' ? data[id] : data[collection].find(e => e.id === id);
  assert(entry, id);
  const changed = Object.fromEntries(Object.entries(values).filter(([k, v]) => JSON.stringify(v) !== JSON.stringify(entry[k])));
  if (!Object.keys(changed).length) return;
  const expected = Object.fromEntries(Object.keys(changed).filter(k => Object.hasOwn(entry, k)).map(k => [k, structuredClone(entry[k])]));
  const expectedAbsent = Object.keys(changed).filter(k => !Object.hasOwn(entry, k));
  updates.push({collection, id, values: changed, expected, ...(expectedAbsent.length ? {expectedAbsent} : {})});
  Object.assign(entry, structuredClone(changed));
}
const scope = {
  mode: 'perPlayer',
  scope: '每位玩家各有一个独立的星矢牌堆，每个布置只存在于其牌主的牌堆中。',
  visibility: '界面按玩家分别展示星矢牌堆，保留牌主、记录目标、剩余回合数和重放次数；查看信息与效果作用范围分别处理。',
  defaultSelection: '星矢的加速、立即加速到完成、额外触发、复制和数量读取，默认只访问效果持有者自己的星矢牌堆，再按牌面目标和数量筛选。',
  targetScope: '目标敌人的、目标的先在玩家自己的星矢牌堆中按布置目标筛选；以自己为目标的再按当前玩家ID筛选。联机牌明确指定其他玩家或所有玩家时，按指定范围访问对应玩家的牌堆。',
  actor: '你打出、你的回合、星矢加速与完成等扳机默认只响应效果持有者自己的出牌、回合和星矢事件；专门联机牌按牌面订阅跨玩家事件。',
  globalModifiers: '本职业卡牌提供的费用、重放、初始回合数、伤害加成及其他持续修正，默认只作用于效果持有者自己的牌与布置。同一玩家的有效来源按原规则叠加；联机牌按牌面指定的跨玩家范围处理。',
  fields: ['instanceId', 'cardOwnerId', 'controllerId', 'targetId', 'deployedTurnId', 'pileOwnerId'],
  ownership: 'cardOwnerId记录实际牌主，pileOwnerId与牌主对应，controllerId记录本次结算者，targetId记录布置目标。常规加速、额外触发保留归属和所在牌堆；专门联机效果另按牌面处理。',
  recipient: '星矢按记录的结算者计算伤害并作用于记录的敌人目标。格挡、抽牌、能量和手牌等玩家收益默认归效果持有者自己；联机牌明确指定其他玩家时按指定接收者处理。',
  reactiveRewards: '星矢加速、完成和效果执行的触发收益默认仅由该星矢牌主自己的有效能力响应。专门联机牌按牌面明确的玩家范围监听事件，每个有效来源对同一事件结算一次。',
  turnStart: '每位玩家在自己的回合开始时，先处理自己的格挡、能量、常规抽牌和回合开始能力，再推进其星矢牌堆中已有的布置；以玩家ID及回合编号记录，每位玩家每回合自动推进一次。',
  resolution: '各玩家独立牌堆中的计数变化和效果执行进入战斗结算队列；同一时点按玩家座位与各自布置顺序处理。每个完成布置只建立一个正常结算批次，重放与追加次数后处理一次去向，旧选择跳过已离场实例。',
  movement: '正常触发后进入该实际牌主的弃牌堆；消耗牌进入其消耗牌堆；明确的其他去向按牌面处理。',
  copying: '在场复制创建新的instanceId，复制品归执行复制的玩家所有，并进入其独立星矢牌堆；目标、计数及牌面修改按既有复制规则继承。原布置的归属与所在牌堆保留。',
  invalidTarget: '目标失效时按既有规则跳过依赖该目标的部分，其余指令继续。战斗结束时分别清理每位玩家的星矢牌堆，永久牌组与战斗内修改分别结算。',
  rendering: '各客户端以玩家ID与instanceId分别展示牌堆、布置和灯光；联机操作的选择高亮与结算反馈按实际作用的玩家及布置同步。',
  multiplayerException: '只有专门的联机牌，才按牌面明确范围影响其他玩家或全部玩家。'
};
const personalRule = '本职业卡牌的卡牌、牌堆、能力及触发收益默认归玩家自己；专门联机牌按牌面明确指定其他玩家或全部玩家。伤害仍按牌面指定的敌人目标结算。';
const oldScope = before.meta.stageDeviceSharing;
const replacements = new Map(Object.entries(oldScope).filter(([key, value]) => typeof value === 'string' && typeof scope[key] === 'string').map(([key, value]) => [value, scope[key]]));
replacements.set('每个玩家方回合开始时，共享牌堆中的已有星矢计数减少1；同一共享回合只处理一次。', '每位玩家自己的回合开始时，其星矢牌堆中的已有星矢计数减少1；每位玩家每回合只处理一次。');
replacements.set('星矢牌的通用重放与耗能修改对所有玩家有效。布置时汇总有效的重放增加量，多名纯那各提供重放1时合计重放2，归0时总共执行3次；通用耗能修改按牌面指定的范围处理。', scope.globalModifiers);
replacements.set('自动推进按共享牌堆的布置先后处理该玩家方回合开始前的快照；每个仍有效的对象轮到时计数减少1，减至0则立即结算。已经离场或归0的对象跳过；新布置对象从下个玩家方回合开始自动推进。', '自动推进按该玩家独立牌堆的布置先后处理其回合开始前的快照；有效对象计数减少1，减至0立即结算。已离场或归0的对象跳过，新布置从该玩家下回合开始自动推进。');
replacements.set('星光馆按以能力持有者为目标的共享星矢数量计算抽牌，与星矢牌主分别识别。', '星光馆只统计能力持有者自己星矢牌堆中符合牌面目标条件的布置数量。');
function replace(text) {for (const [from, to] of replacements) if (from) text = text.split(from).join(to);return text;}
const stage = data.mechanisms.find(m => m.id === 'ME_STAGE');
update('mechanisms', 'ME_STAGE', {rules: [...stage.rules.map(replace), personalRule]});
update('root', 'meta', {stageDeviceSharing: scope});
update('root', 'meta', {cardEffectScope: {defaultPlayerScope: 'self', scopeOwner: 'effectOwnerId', arrowPileScope: 'perPlayer', defaultCollections: ['hand', 'drawPile', 'discardPile', 'exhaustPile', 'arrowPile'], defaultEventScope: 'self', multiplayer: 'explicitCardText', enemyTargets: '按牌面指定的敌人或所有敌人', rule: personalRule}});
update('root', 'meta', {ruleConventions: [...data.meta.ruleConventions.map(replace), personalRule]});
update('root', 'meta', {stageDeviceWording: {...data.meta.stageDeviceWording, scope: '先限定为玩家自己的星矢牌堆，再按牌面目标筛选；省略数量时作用于范围内全部布置，写明一个时选择其中一个。联机牌另按明确玩家范围处理。', targetMeaning: '目标敌人的星矢、目标的星矢，均指玩家自己牌堆中以该目标为布置目标的星矢；以自己为目标则指向玩家自身。'}});
update('root', 'meta', {cardSelectionRules: {...data.meta.cardSelectionRules, defaultPlayerScope: 'self', multiplayerException: 'explicitCardText'}});
const termUpdates = {
  OB13: {definition: '每位玩家独立管理自己已经打出、布置中的星矢牌；卡牌悬挂并向记录目标照下灯光。'},
  OB19: {definition: '专门联机牌按牌面明确指定一名其他玩家或所有玩家；普通职业牌默认仅处理玩家自己的对象和收益。'},
  TR45: {definition: '玩家自己的回合开始时，仅推进其独立牌堆中的星矢，剩余回合数减少1，归0时按正常完成流程处理。', template: '在你的回合开始时，{效果}。'},
  EF43: {definition: data.terms.find(t => t.id === 'EF43').definition + ' 默认只处理玩家自己的牌与布置。'},
  EF66: {definition: '以本场规则修改玩家自己当前与后续符合类型的牌的能量费用；星矢回合数保持独立。'},
  CD23: {definition: '在玩家自己的牌中，从本组扩到其他知识牌、普通星矢或通用攻击；联机牌另按明确范围处理。'},
  RL24: {definition: '一次性变周期来源、覆盖自己其他组或未来对象时，分别保留频率与适用对象；跨玩家范围由专门联机牌明确。'},
  RL29: {definition: '先在玩家自己的对象中扩展目标集合并去重，再分别执行原操作；追加目标不递归传播，增量获得与存量设置分别计算。'},
  SB19: {definition: '将玩家自己星矢的每回合自然推进移至另一个时点，单个布置在该玩家每回合仍只自然推进一次。'},
  FK11: {definition: data.terms.find(t => t.id === 'FK11').definition.replaceAll('下一玩家方回合', '该玩家下回合')},
  KW_STAGE: {notes: '每位玩家独立持有星矢牌堆；普通职业牌的选择、修正与触发默认只作用于玩家自己，联机牌按明确范围处理。'}
};
for (const [id, values] of Object.entries(termUpdates)) update('terms', id, values);
for (const id of ['JA18', 'JA26', 'JK42', 'JK42B', 'JK42C']) {
  const c = data.cards.find(c => c.id === id), values = {};
  const transform = v => typeof v === 'string' ? v.replaceAll('共享场面数量', '玩家自己的星矢数量').replaceAll('本共享回合', '该牌主本回合').replaceAll('每个玩家方回合开始时自然推进', '牌主自己的回合开始时自然推进').replaceAll('下一玩家方回合', '该玩家下回合') : Array.isArray(v) ? v.map(transform) : v && typeof v === 'object' ? Object.fromEntries(Object.entries(v).map(([k, x]) => [k, transform(x)])) : v;
  for (const key of ['notes', 'design']) if (c[key]) values[key] = transform(c[key]);
  update('cards', id, values);
}
update('styles', 'STYLE_002', {content: '“所有星矢”默认指自己的全部星矢；联机牌跨玩家时明确写出“所有玩家的星矢”。'});
const style = {uid: 'junna_style_personal_scope', id: 'STYLE_PLAYER_SCOPE', name: '默认作用范围', content: personalRule};
assert(!data.styles.some(s => s.id === style.id));data.styles.push(style);
for (const old of before.cards) {const c = data.cards.find(c => c.id === old.id);for (const key of ['text', 'upgradedText', 'cost', 'upgradedCost', 'trashedAt']) assert.equal(c[key], old[key]);}
for (const key of ['partnerGroups', 'archetypes']) assert.deepEqual(data[key], before[key]);
assert(!/共用一个星矢牌堆|遍历整个共享牌堆|多个纯那的效果均可作用于彼此|星矢牌的通用重放与耗能修改对所有玩家有效|向所有玩家发布/.test(JSON.stringify({stage: data.mechanisms.find(m => m.id === 'ME_STAGE'), meta: data.meta})));
const batches = [{id: batchId + '-rules', label: '独立星矢牌堆与默认个人范围', updates, applyUpdatesOnImport: true}, {id: batchId + '-style', label: '默认作用范围约定', collection: 'styles', ids: [style.id], entries: [style]}];
const record = {schemaVersion: 1, id: batchId, date: '2026-11-02', sourceRevision: live.revision, title: '星矢独立牌堆与默认个人范围', supersedes: '星矢共享牌堆与普通职业牌默认跨玩家生效', userRequest: '每个玩家单独一个星矢牌堆。默认所有本职业卡牌只影响玩家自己，专门联机卡按牌面影响全部玩家。', scope, personalRule, updates, style, cardTextsPreserved: true};
write(path.join(dataDir, '纯那箭矢个人范围约定.json'), record);
write(path.join(here, '.tmp-personal-scope-before.json'), {revision: live.revision, data: before});
seed.data = data;seed.revision = live.revision;
for (const key of ['history', 'undoStack', 'redoStack']) seed[key] = structuredClone(live[key]);
seed.assets = {...seed.assets, ...live.assets};seed.seedBatches.push(...batches);seed.appliedSeedBatches = [...new Set([...seed.appliedSeedBatches, ...live.appliedSeedBatches, ...batches.map(b => b.id)])];write(seedFile, seed);
const trackingFile = path.join(dataDir, '纯那职业卡池设计记录.json'), tracking = read(trackingFile);
tracking.ruleChanges ??= [];tracking.ruleChanges.push({id: batchId, date: '2026-11-02', sourceRevision: live.revision, source: '纯那箭矢个人范围约定.json', scope: '独立牌堆；普通职业牌默认自己；联机牌明确跨玩家范围。'});write(trackingFile, tracking);
console.log(JSON.stringify({sourceRevision: live.revision, updatedEntries: updates.length, cards: data.cards.length, cardTextsPreserved: true, playerScope: 'self', arrowPiles: 'perPlayer'}));
