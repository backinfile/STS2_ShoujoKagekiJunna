const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const here = __dirname, dataDir = path.resolve(here, '../data');
const read = file => JSON.parse(fs.readFileSync(file, 'utf8'));
const write = (file, value) => fs.writeFileSync(file, JSON.stringify(value, null, 2) + '\n');
const seed = read(path.join(here, 'seed.json'));
const live = read(path.join(dataDir, '纯那Mod设计工作台_本地编辑.json'));
const batchId = 'junna-type-breakthrough-20261102-v1';
if (seed.seedBatches.some(batch => batch.id === batchId + '-cards')) throw Error('第三轮已生成，请在工作台继续修改。');
const before = structuredClone(live.data), data = structuredClone(before);
const cards = [], partnerGroups = [], upgradeUpdates = [];
const sources = [
  'D:/Github/STS2_Card_Reserch/docs/体系分析流程.md',
  'D:/Github/STS2_Card_Reserch/docs/体系驱动卡池设计流程.md',
  'D:/Github/STS2_Card_Reserch/docs/职业卡池设计要求_按五职业规律.md',
  'D:/Github/STS2_Card_Reserch/docs/mods/sts2_character_cards_zh.md'
];
const terms = [
  ['TB01', '类型转换', '改变实际卡牌的类型与结算方式，保留牌名、知识关系及有效修改。'],
  ['TB02', '布置生效', '箭矢在等待期间提供持续规则，完成或离场时结束该规则。'],
  ['TB03', '能力循环', '知识能力牌安装效果后仍可重新进入牌组循环，每次使用提高后续费用。'],
  ['TB04', '技能持续化', '把选定知识技能牌的单次效果转为能力牌的回合开始效果。'],
  ['TB05', '类型选取', '扩展指定选择器可选的类型，实际卡牌仍保留自身类型。'],
  ['TB06', '搭载打出', '把实际攻击牌交由已有箭矢承载，在其正常完成后打出。'],
  ['TB07', '能力期限', '以一个实际箭矢的布置时间限定借用能力的持续时间。']
].map(([id, name, definition]) => ({uid: 'term_' + id, id, name, kind: '突破', definition, template: '', systems: [], unit: '', notes: '第三轮类型突破探索。', sources: ['纯那第三轮类型突破探索']}));

function add(id, system, type, cost, text, category, oldRule, exception, investment, rules, comparison, extra = {}) {
  const termIds = [category, 'OB07', 'CD01', ...(extra.termIds || [])];
  if (type === '箭矢') termIds.push('KW_STAGE');
  if (text.endsWith('消耗。')) termIds.push('KW_EXHAUST');
  if (system === 'partner') termIds.push('KW_PARTNER');
  const card = {
    uid: 'draft_card_' + id, id, name: id, system, type, rarity: '稀有', pool: true,
    cost, upgradedCost: extra.upgradedCost ?? Math.max(0, cost - 1),
    turns: type === '箭矢' ? extra.turns : null,
    upgradedTurns: type === '箭矢' ? extra.upgradedTurns ?? extra.turns : null,
    ...(type === '箭矢' ? {arrowSubtype: extra.arrowSubtype || '技能'} : {}),
    target: extra.target || '自己', text, upgradedText: extra.upgradedText || text,
    keywords: [], termIds: [...new Set(termIds)], breakthroughIds: [category],
    archetypeIds: [...new Set(['SYS_' + system, ...(extra.archetypeIds || [])])],
    partnerCardIds: [], art: 'defaultPortrait', role: '突破限制牌',
    direction: terms.find(t => t.id === category).name,
    naming: {original: '', work: '', url: '', adaptation: '名称待定；当前显示设计编号。'},
    draftStatus: '探索候选', draftBatch: batchId,
    design: {classification: '规则突破', oldRule, exception, investment, rules, comparison,
      port: extra.port || '运转', dependencies: extra.dependencies || [],
      roleReason: oldRule + ' → ' + exception},
    notes: ['第三轮探索；卡名待定。', '原有限制：' + oldRule, '规则变化：' + exception,
      '保留的投入：' + investment, '复核：' + rules, '职业对照：' + comparison].join('\n')
  };
  cards.push(card); return card;
}

add('JT01', 'stage', '技能', 1,
  '选择你手牌中的一张攻击牌或技能牌。本场战斗中，将其变为回合数为1的箭矢牌，其耗能减少1。消耗。', 'TB01',
  '攻击和技能打出后立即执行，通常无法使用箭矢的布置、加速与完成规则。',
  '实际牌转为箭矢，以等待换取费用降低及箭矢规则的适用资格。',
  '先支付转化费用并消耗本牌；目标之后每次使用都需等待，未来攻击或技能类型筛选按新类型处理。',
  '只选择手牌中的实际攻击或技能。保留牌名、知识标记、升级、伤害加成与其他有效修改；目标范围沿用原牌。费用最低0。转换不发放永久知识牌；战斗结束还原永久模型。',
  '围绕纯那的箭矢类型改变结算时点；不采用技能免费并消耗的腐化规则。',
  {termIds: ['EF25', 'RL12', 'RL22', 'RL23'], dependencies: ['JA02', 'JA04', 'JA22'], port: '输出／防御／运转'});

add('JT02', 'stage', '技能', 1,
  '选择你手牌中的一张箭矢牌。本场战斗中，将其变为技能牌，其耗能增加1。消耗。', 'TB01',
  '箭矢牌必须布置并等待完成，载荷随倒计时兑现。',
  '把尚未布置的箭矢改为立即执行的技能，以额外费用换取结算时点改变。',
  '转化费用与目标增加的费用；目标失去箭矢布置、完成、加速及相关回手规则。',
  '只修改手牌中的牌，场内布置继续使用原规则。保留载荷、目标范围、知识标记与有效修改。技能打出时执行原载荷，按技能去向处理；原箭矢初始回合数字段不再参与结算。',
  '改写箭矢与技能之间的类型边界，保留费用代价；不设置、重置或取回在场倒计时。',
  {termIds: ['EF25', 'RL12', 'RL22'], dependencies: ['JA13', 'JA15'], port: '输出／防御／运转'});

add('JT03', 'stage', '箭矢', 2,
  '此牌布置期间，你的攻击牌和技能牌耗能减少1。', 'TB02',
  '箭矢的普通载荷在完成时执行，持续减费通常由能力牌提供。',
  '箭矢在等待期间提供持续的类型减费，倒计时同时成为规则的有效时长。',
  '2费布置；完成或离场即失去减费，主动加速会提前结束减费窗口。',
  '每个实际布置提供一份减费，费用最低0。重放和额外触发沿用同一份持续规则；复制成独立布置后各有一份。完成时先撤销本布置的减费，再处理后续普通指令。升级把有效时间从2回合延长到3回合。',
  '与腐化的长期免费及消耗交换不同，本候选以箭矢的剩余时间维持临时规则，提前完成也会失去窗口。',
  {turns: 2, upgradedTurns: 3, arrowSubtype: '能力', upgradedCost: 2, termIds: ['OB17', 'EF66', 'RL16'], dependencies: ['JA16', 'JA04'], port: '运转'});

add('JT04', 'partner', '技能', 1,
  '选择你手牌中的一张知识能力牌。本场战斗中，该牌打出后进入弃牌堆，耗能增加1。消耗。', 'TB03',
  '能力牌安装后退出普通卡牌循环，通常无法再次抽到同一个实际对象。',
  '知识能力牌安装规则后重新进入牌组循环，后续每次使用增加费用。',
  '目标需要重新到手并正常支付不断提高的费用；本牌一次性使用。',
  '知识能力牌按知识关键词与能力类型共同筛选。每次实际打出正常安装一次效果，随后把同一个卡牌对象放入弃牌堆，再将其本场费用增加1；已有安装实例保留。消耗仅为本牌自身去向，不以消耗或弃牌事件产生收益。',
  '区别于创造性AI和信号增强的生成或追加打出，本候选让同一个知识能力牌以递增费用重新参与循环。',
  {termIds: ['OB17', 'RL09', 'RL11', 'RL16', 'RL23'], dependencies: ['JK04', 'JK10', 'JK15', 'A']});

add('JT05', 'partner', '技能', 2,
  '选择你手牌中的一张知识技能牌。本场战斗中，将其变为能力牌，耗能增加2，效果改为在你的回合开始时触发。消耗。', 'TB04',
  '知识技能牌每次都要抽到并打出，才能获得一次牌面效果。',
  '把同一个实际知识技能改成需要安装的能力，原载荷在回合开始执行。',
  '支付本牌及目标额外增加的2点安装费用；安装当回合不立即执行原载荷。',
  '保留知识关系、升级及载荷中的数字；目标仍需从手牌正常打出后才安装。回合开始按原指令执行并重新选择目标。消耗、保留等使用去向字段只处理能力卡牌本体，原载荷中的功能指令按回合执行。转换不进行永久发放，战斗结束还原模型。',
  '以玩家实际持有的知识技能作为确定载荷进行类型转换，区别于随机能力来源与首张牌追加执行。',
  {termIds: ['EF25', 'OB17', 'RL12', 'RL16', 'RL17'], dependencies: ['JK04B', 'JK11', 'JK15B', 'B'], port: '防御／运转'});

add('JT06', 'growth', '能力', 2,
  '需要选择攻击牌的效果，也可以选择箭矢牌。', 'TB05',
  '攻击牌选择器不接受独立类型的箭矢，单牌伤害加成、加段与转移无法选择箭矢。',
  '扩展选择攻击牌的操作，使其可选择箭矢，并保持箭矢自己的结算规则。',
  '2费安装；仍需要增伤、加段或转移来源，以及实际箭矢载体与等待。',
  '只扩展明确选择攻击牌的选择器；原有牌区、数量、牌主等条件继续生效。原生类型、打出类型事件及全体攻击牌的范围均保持原口径。具有伤害段的箭矢可接受相应加成与段数修改；没有可修改伤害段时跳过该指令。',
  '不是已删除JA21的箭矢全局攻击身份。这里只为纯那现有单牌投资操作开放箭矢对象。',
  {archetypeIds: ['SYS_stage'], termIds: ['EF52', 'RL22', 'KW_BONUS'], dependencies: ['JG03', 'JG09', 'JG16', 'JA13'], port: '输出／运转'});

add('JT07', 'stage', '技能', 1,
  '将手牌中的一张攻击牌放在目标敌人的一个箭矢上。该箭矢完成时，免费打出这张攻击牌。消耗。', 'TB06',
  '攻击牌通常需从手牌立即支付并打出，已有箭矢只能结算自身的载荷。',
  '让一个实际攻击牌由已有箭矢承载，在其正常完成后支付等待代价并免费打出。',
  '本牌费用、攻击牌暂时离手，以及已布置且目标匹配的箭矢；只有正常完成才执行攻击。',
  '攻击牌以独立实际对象挂载，保留类型、知识标记与有效修改。源箭矢正常牌效及其重放批次结束后，按挂载顺序打出攻击，仍记录攻击和知识出牌事件。额外触发不释放挂载牌。复制源箭矢保持原挂载对象的唯一归属。目标失效时将挂载攻击退回手牌。',
  '区别于攻击追加执行：没有制造副本，真实攻击牌借用纯那已有箭矢的完成时间进入一次出牌。',
  {target: '敌人', termIds: ['RL07', 'RL11', 'RL28', 'EF49'], dependencies: ['JA13', 'JA17', 'JK10B', 'JG03'], port: '输出／运转'});

add('JT08', 'stage', '技能', 1,
  '免费打出手牌中的一张能力牌，其效果仅在一个箭矢布置期间生效。该箭矢完成后，将能力牌放回手牌。消耗。', 'TB07',
  '能力牌通常支付一次安装费用后持续整场，普通箭矢无法决定其有效期限。',
  '借助一个现存箭矢暂时安装能力，以有限持续时间换取免费安装与到期回手。',
  '本牌费用、实际能力牌及尚在等待中的箭矢；箭矢完成后需要重新支付才能正式安装能力。',
  '选择一个已有箭矢及手牌能力，正常记录一次能力出牌并建立一个有来源标识的能力实例。整批完成指令与挂载攻击处理后，仅移除这次借用的能力实例，并取回对应实际牌。已经获得的格挡、能量和生成牌保留。源箭矢提前离场时同样结束借用。',
  '以纯那实际箭矢决定能力持续时间，不采用能力牌追加打出或随机生成能力的既有路线。',
  {archetypeIds: ['SYS_partner'], termIds: ['OB17', 'RL16', 'EF49', 'RL11'], dependencies: ['JK04', 'JK10', 'JK15', 'A'], port: '输出／防御／运转'});

function userCard(id, type, cost, upgradedCost, text, extra = {}) {
  return {uid: 'user_card_' + id + '_20261102', id, name: id, system: 'partner', type,
    rarity: extra.pool === false ? '附带' : '罕见', pool: extra.pool !== false,
    cost, upgradedCost, turns: null, upgradedTurns: null, target: '自己',
    text, upgradedText: text, keywords: ['知识'],
    termIds: ['KW_PARTNER', ...(extra.termIds || [])], breakthroughIds: [],
    archetypeIds: ['SYS_partner'], partnerCardIds: [], art: 'defaultPortrait',
    role: extra.pool === false ? '知识牌' : '机制基础牌', direction: '知识牌持续来源',
    naming: {original: '', work: '', url: '', adaptation: '用户指定名称。'},
    notes: extra.notes || '', draftStatus: '用户指定', draftBatch: batchId};
}
cards.push(userCard('A', '能力', 2, 1,
  '知识。在你的回合结束时，将1张《B》放入抽牌堆。',
  {termIds: ['TR03', 'EF22'], notes: '用户指定。获得A时附带两张B。回合结束时生成一张基础版本的临时B，随机放入抽牌堆；本次临时生成沿用战斗生成规则。升级只把费用从2降为1。'}));
cards.push(userCard('B', '技能', 0, 0,
  '知识。抽1张牌。获得1点能量。消耗。',
  {pool: false, termIds: ['EF06', 'EF09', 'KW_EXHAUST'], notes: '用户指定。A附带两张同名B。B的升级效果尚待指定，当前沿用基础效果。用户明确指定本牌为具有抽牌效果的技能牌。'}));
partnerGroups.push({uid: 'user_group_A_20261102', id: 'GROUP_A', title: 'A', mainCardId: 'A', grantedCardIds: ['B'], grantedCardCounts: {B: 2}});
cards.find(c => c.id === 'A').partnerCardIds = ['B'];
cards.find(c => c.id === 'B').partnerCardIds = ['A'];

function updateUpgrade(id, values) {
  const card = data.cards.find(c => c.id === id);
  if (!card || card.trashedAt) return;
  const changed = Object.fromEntries(Object.entries(values).filter(([k, v]) => JSON.stringify(card[k]) !== JSON.stringify(v)));
  if (Object.keys(changed).length) {Object.assign(card, changed); upgradeUpdates.push({collection: 'cards', id, values: changed});}
}
for (const id of ['JA02', 'JA22', 'JK05', 'JK10']) {
  const card = data.cards.find(c => c.id === id); updateUpgrade(id, {upgradedText: card.text});
}
const immediate = data.cards.find(c => c.id === 'JA04');
if (immediate.text.includes('下1/2张')) updateUpgrade('JA04', {
  text: immediate.text.replace('下1/2张', '下一张'), upgradedText: immediate.text.replace('下1/2张', '下2张')
});
else updateUpgrade('JA04', {upgradedText: immediate.text.replace('下一张', '下2张').replace('下1张', '下2张')});
for (const id of ['JK11', 'JK11B']) {
  const card = data.cards.find(c => c.id === id);
  updateUpgrade(id, {upgradedText: card.text.replace(/获得(\d+)点格挡/, (_, n) => '获得' + (Number(n) + 3) + '点格挡')});
}
const transfer = data.cards.find(c => c.id === 'JG16');
updateUpgrade('JG16', {upgradedText: transfer.text.startsWith('保留。') ? transfer.text : '保留。' + transfer.text});
const guard = data.cards.find(c => c.id === 'JK15B');
if (guard.upgradedCost > guard.cost) updateUpgrade('JK15B', {upgradedCost: guard.cost});

for (const term of terms) {assert(!data.terms.some(t => t.id === term.id)); data.terms.push(term);}
for (const card of cards) {assert(!data.cards.some(c => c.id === card.id)); data.cards.push(card);}
data.partnerGroups.push(...partnerGroups);
const style = data.styles.find(s => s.name === '不要写其他职业中已经有的流派牌');
assert(style, '保留用户已经写入的职业流派约定');
const knownTerms = new Set(data.terms.map(t => t.id));
for (const card of cards) for (const id of card.termIds) assert(knownTerms.has(id), card.id + ' 未知词条 ' + id);
for (const card of cards.filter(c => c.id.startsWith('JT'))) {
  assert(card.design.oldRule && card.design.exception && card.design.investment && card.design.comparison);
  assert(!/每当.*(?:消耗|弃置|丢弃)|获得\d+点力量|给予\d+层中毒/.test(card.text), card.id);
}
for (const old of before.cards) {
  const next = data.cards.find(c => c.id === old.id), changed = upgradeUpdates.find(u => u.id === old.id)?.values || {};
  for (const key of new Set([...Object.keys(old), ...Object.keys(next)]))
    if (!(key in changed)) assert.deepEqual(next[key], old[key], old.id + ' ' + key);
}
assert.deepEqual(data.partnerGroups.slice(0, before.partnerGroups.length), before.partnerGroups);
assert.deepEqual(data.styles, before.styles);

const batches = [
  {id: batchId + '-upgrades', label: '按当前基础牌文同步升级效果', updates: upgradeUpdates, applyUpdatesOnImport: true},
  {id: batchId + '-terms', label: '补充类型突破分类', collection: 'terms', ids: terms.map(t => t.id), entries: terms},
  {id: batchId + '-cards', label: '新增类型突破候选与A、B', collection: 'cards', ids: cards.map(c => c.id), entries: cards},
  {id: batchId + '-groups', label: 'A关联两张B', collection: 'partnerGroups', ids: partnerGroups.map(g => g.id), entries: partnerGroups},
  {id: batchId + '-style', label: '保留职业流派约定', collection: 'styles', ids: [style.id], entries: [style]}
];
seed.data = data;
seed.assets = {...seed.assets, ...live.assets};
seed.seedBatches.push(...batches);
seed.appliedSeedBatches = [...new Set([...seed.appliedSeedBatches, ...batches.map(b => b.id)])];
write(path.join(here, 'seed.json'), seed);
const summary = {typeBreakthroughs: 8, userCards: 2, upgradedCards: upgradeUpdates.length,
  active: data.cards.filter(c => !c.trashedAt).length, pool: data.cards.filter(c => !c.trashedAt && c.pool).length,
  grants: data.cards.filter(c => !c.trashedAt && !c.pool).length, total: data.cards.length};
const document = {schemaVersion: 1, id: batchId, date: '2026-11-02', title: '纯那第三轮类型突破探索',
  sourceRevision: live.revision, sources, criterion: '逐张说明原有类型规则、改变后的权限或生命周期、保留的代价及与既有职业流派的区别。',
  style: structuredClone(style), summary, cards, partnerGroups, terms, upgradeUpdates,
  recommendations: ['JT01', 'JT03', 'JT06', 'JT07'],
  rejected: [
    {direction: '消耗手牌以返能、免费打出或加速', reason: '会把消耗动作接成新的收益路线，依照用户的职业流派约定收起。'},
    {direction: '技能打出造成固定伤害、格挡触发输出、力量成长', reason: '避开已有职业的技能密度、格挡与力量路线。'},
    {direction: '箭矢全局视为攻击牌', reason: '保留已删除JA21；JT06只探索选择器范围，实际类型与出牌事件保持独立。'},
    {direction: '仅按类型连续出牌获得数值、技能额外打出、随机生成能力', reason: '前者仍是触发收益；后两者已有爆发、信号增强和创造性AI等参照。'}
  ],
  review: {scope: '本轮只补充类型突破候选和用户指定A、B，不补完整卡池。',
    upgrades: '旧牌依当前基础效果同步升级分支。JK11与JK11B沿用当前双向召回；JK15B升级费用同步0。',
    pending: ['费用与等待数值为探索初稿。', 'B的升级沿用基础效果，待用户指定。', 'JT05与JT08涉及能力实例的类型和期限变更，实际实现时需要按来源实例处理。']}};
write(path.join(dataDir, '纯那第三轮类型突破探索.json'), document);
const tracking = read(path.join(dataDir, '纯那职业卡池设计记录.json'));
tracking.date = '2026-11-02'; tracking.stage = '类型规则突破与升级同步'; tracking.status = '新增8张类型突破候选与用户指定A、B';
if (!tracking.requirements.includes(style.name)) tracking.requirements.push(style.name + '。' + style.content);
tracking.current = {...summary, source: '纯那第三轮类型突破探索.json'};
tracking.batches.push({id: batchId, ...summary, source: '纯那第三轮类型突破探索.json', recommendations: document.recommendations});
write(path.join(dataDir, '纯那职业卡池设计记录.json'), tracking);
let readme = fs.readFileSync(path.join(here, 'README.md'), 'utf8');
readme = readme.replace(/当前保留27张规则突破候选[^\r\n]+/, '第三轮新增8张类型突破候选、用户指定的A与B，并同步已有牌的升级效果。[第三轮探索](../data/纯那第三轮类型突破探索.json)记录原有限制、规则变化、代价和职业对照；当前编辑以本地编辑文件为准。卡名以编号暂代，费用与升级为初稿。');
fs.writeFileSync(path.join(here, 'README.md'), readme);
console.log(JSON.stringify({sourceRevision: live.revision, summary, upgradeIds: upgradeUpdates.map(u => u.id), newIds: cards.map(c => c.id), preservedStyle: style.id}, null, 2));
