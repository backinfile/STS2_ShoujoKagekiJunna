const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const here = __dirname, dataDir = path.resolve(here, '../data');
const read = file => JSON.parse(fs.readFileSync(file, 'utf8'));
const write = (file, value) => fs.writeFileSync(file, JSON.stringify(value, null, 2) + '\n');
const batchId = 'junna-sixth-rule-breakthrough-20261102-v1';
const seed = read(path.join(here, 'seed.json'));
assert(!seed.seedBatches.some(b => b.id.startsWith(batchId)), '本轮已写入；保留当前编辑。');
const liveFile = path.join(dataDir, '纯那Mod设计工作台_本地编辑.json');
const live = read(liveFile), before = structuredClone(live.data), data = structuredClone(before);
const criterion = '突破限制牌重点是修改游戏规则（游戏底层机制，或者新增的机制）本身。可能是带来高数值，也可能是改变正常的打法，都有可能。';
const categories = [
  ['CB01', '结算方式', 'stage', '替换星矢牌效与回合数变化之间的结算关系。'],
  ['CB02', '在场对象', 'stage', '改变布置所持有的实际卡牌对象以及原牌占用的区域。'],
  ['CB03', '选牌范围', 'stage', '扩展手牌选择器可访问的牌区，原类型与条件筛选保留。'],
  ['CB04', '抽牌顺序', 'partner', '改写既有常规抽牌的取牌顺序，抽牌张数沿原规则。'],
  ['CB05', '洗牌范围', 'partner', '改写抽牌堆耗尽时，弃牌堆中哪些对象会参与重洗。'],
  ['CB06', '升级上限', 'partner', '改写知识牌每个实际对象的可升级次数限制。'],
  ['CB07', '伤害顺序', 'growth', '改写伤害加成进入完整伤害计算流程的位置。'],
  ['CB08', '加成公式', 'growth', '改写已有伤害加成对每段伤害的贡献公式。'],
  ['CB09', '段数公式', 'growth', '改写伤害次数与伤害加成存量之间的计算关系。'],
  ['CB10', '支付上限', 'utility', '改写一次出牌的实际能量支付上限，卡牌费用字段独立保留。'],
  ['CB11', '能量刷新', 'utility', '改写每回合起始能量额度的计算公式。'],
  ['CB12', '分次支付', 'utility', '允许一个实际卡牌的能量支付分成多次，付清后才执行出牌。']
];
const terms = categories.map(([id, name, system, definition]) => ({uid: 'term_' + id, id, name,
  kind: '突破', definition, template: '', systems: [system], unit: '',
  notes: '第六轮规则改写分类；不作为新增玩家关键词。', sources: ['纯那第六轮规则突破探索']}));
const cards = [];
function add(id, system, type, cost, text, category, design) {
  const termIds = [category];
  if (system === 'stage') termIds.push('KW_STAGE');
  if (system === 'partner') termIds.push('KW_PARTNER');
  if (system === 'growth') termIds.push('KW_BONUS');
  if (text.endsWith('消耗。')) termIds.push('KW_EXHAUST');
  const card = {uid: 'draft_card_' + id, id, name: id, system, type, rarity: '稀有', pool: true,
    cost, upgradedCost: cost - 1, turns: null, upgradedTurns: null, target: '自己',
    text, upgradedText: text, keywords: [], termIds, breakthroughIds: [category],
    archetypeIds: system === 'utility' ? [] : ['SYS_' + system], partnerCardIds: [], art: 'defaultPortrait',
    role: '突破限制牌', direction: terms.find(t => t.id === category).name,
    draftStatus: '探索候选', draftBatch: batchId,
    naming: {original: '', work: '', url: '', adaptation: '名称待定；当前显示设计编号。'},
    design: {classification: '规则改写', criterion, ...design, roleReason: design.oldRule + ' → ' + design.exception},
    notes: ['第六轮规则突破探索；卡名待定。', '原规则：' + design.oldRule,
      '规则改动：' + design.exception, '结果：' + design.result,
      '结算约定：' + design.rules, '费用与强度：' + design.balance].join('\n')};
  cards.push(card);
}

add('JA32', 'stage', '能力', 3,
  '星矢的牌效改为每减少1回合触发，归0时离场。', 'CB01', {
    layer: '星矢结算规则', oldRule: '普通星矢在剩余回合数归0时执行一次完整牌效。',
    exception: '用实际减少的回合数决定牌效执行次数，归0只处理最后一次推进及离场。',
    result: '初始3回合的星矢正常推进可执行3次；一次加速3回合也可集中兑现3次，较长等待同时对应更多结算。',
    rules: '以每次自然推进或主动加速的实际减少量N安排N份牌效执行，各份沿用布置记录的目标和重放次数。先登记本次实际推进量，再按同一队列逐份执行；归0不追加另一份默认牌效。每个实际布置仍只产生一次归0完成事件，整批后处理一次去向，消耗与JA22沿原优先级处理。初始为0的布置仍按原规则执行一次；明确的额外触发另行处理。驻场规则按入场和离场注册，持续监听本身不因N重复安装。多份此能力设置同一种结算方式。',
    balance: '基础3费，升级2费。每段原有数值与费用不变，整体伤害和抽牌上限可能显著提高，单独评估强度。',
    port: '输出／防御／运转', dependencies: ['JA02', 'JA04', 'JA13', 'JA15', 'JA22']});

add('JA33', 'stage', '能力', 2,
  '星矢改为以复制品布置，原牌立即弃置。', 'CB02', {
    layer: '卡牌区域与布置对象', oldRule: '打出星矢后，原实际卡牌由布置持有，完成前离开普通牌组循环。',
    exception: '让复制品承担布置，原实际牌在打出流程中立即按弃置去向处理。',
    result: '原牌可以在旧布置等待期间再次抽到和打出，多份布置各自独立结算。',
    rules: '支付原牌一次费用并记录一次出牌，以当时有效修改创建一个实际复制品承担本次布置；记录同一目标、结算者、初始回合数、重放次数和原支付数值。原牌立即进入普通弃置流程，消耗仍覆盖其默认弃置。复制品之后独立推进，完成后按自身普通去向处理，JA22可处理其正常弃置。复制品建立布置不再次触发本能力，不额外记录出牌；知识打出监听只响应原出牌一次。',
    balance: '基础2费，升级1费。复制品进入正常战斗牌区，可能扩大后续星矢数量与牌组规模。',
    port: '输出／防御／运转', dependencies: ['JA02', 'JA22', 'JK15']});

add('JA34', 'stage', '能力', 2,
  '需要选择手牌的效果，也可以选择在场星矢。', 'CB03', {
    layer: '牌区选择器', oldRule: '限定手牌的选择器只访问实际手牌，在场星矢位于独立牌区。',
    exception: '为手牌选择器额外开放在场星矢区域，原有类型、数量、归属与功能限制继续生效。',
    result: '可在星矢等待期间对其进行加段、伤害加成投资、转移或费用调整。',
    rules: '扩展明确带手牌区域条件的选择器，候选对象包含合法在场星矢。星矢继续保留原类型与在场位置，不计入手牌数量，也不自动取得普通出牌权限。字段修改作用于对应实际牌，已支付费用和已建立布置的剩余计数各自保留；显式移牌指令移动该实际对象并结束旧布置，进度不转存。其他卡牌类型与来源条件按原指令执行，JT01的非星矢限制继续生效。',
    balance: '基础2费，升级1费。范围扩展的强度取决于现有选牌工具，范围与事件类型分别处理。',
    port: '输出／运转', dependencies: ['JG03', 'JG09', 'JG16', 'JH01']});

add('JK26', 'partner', '能力', 3,
  '回合开始的抽牌优先抽取知识牌。', 'CB04', {
    layer: '常规抽牌取牌顺序', oldRule: '回合开始的常规抽牌按抽牌堆的现有顺序取牌。',
    exception: '每次常规抽牌先寻找抽牌堆中的知识牌，无知识牌时按原顺序取牌。',
    result: '知识牌优先占用原有常规抽牌名额，原有抽牌张数与手牌上限保持原值。',
    rules: '仅替换回合开始常规抽牌的单张选择步骤；按当前抽牌堆顺序寻找第一个知识牌，移出该对象并发布正常抽牌事件。没有知识牌时抽取顶牌，抽牌堆为空时照常尝试重洗。回合中其他抽牌继续采用原规则；生成或复制B以及获取关联牌仍按当前约定处理。多份本能力共享同一优先顺序。',
    balance: '基础3费，升级2费。改动会挤占其他牌的常规到手机会，卡组构成影响结果。',
    port: '运转', dependencies: ['JK10', 'JK11', 'JK15', 'A', 'B']});

add('JK27', 'partner', '能力', 2,
  '洗牌时，只将知识牌洗入抽牌堆。', 'CB05', {
    layer: '重洗牌区规则', oldRule: '抽牌堆耗尽时，将符合重洗条件的弃牌堆卡牌一起洗入抽牌堆。',
    exception: '重洗候选集合改为弃牌堆中的知识牌，其余牌继续留在弃牌堆。',
    result: '首次循环后，知识牌可以独立构成后续抽牌循环，其他牌仍可由显式召回工具取用。',
    rules: '每次正常重洗在弃牌堆中按知识关键词筛选实际对象，只移动该集合并随机化。手牌、消耗区和在场星矢继续保留原位置。没有可洗入知识牌时本次重洗结束，当前抽牌请求按实际可用数量停止，避免反复尝试空洗牌。明确指定其他来源集合的洗入效果依其牌面处理。',
    balance: '基础2费，升级1费。非知识牌在首次使用后会退出自动抽牌循环，属于对正常组牌与循环规则的持续改写。',
    port: '运转', dependencies: ['JK11', 'JK15', 'A', 'B']});

add('JK28', 'partner', '技能', 2,
  '本场战斗中，知识牌可以重复升级。升级手牌中的知识牌。消耗。', 'CB06', {
    layer: '卡牌升级次数上限', oldRule: '普通知识牌按各自模型升级一次，达到上限后通常不再接受升级。',
    exception: '解除本场知识牌的单次升级上限，并提供一次手牌知识牌升级。',
    result: '后续升级可以继续改变同一知识牌的升级字段，获得高数值或进一步降低费用。',
    rules: '建立本场知识牌升级上限例外，再对当前手牌知识牌各执行一次升级。各模型需配置重复升级增量：例如B的抽牌继续每级增加1，JK10的每回合合格触发次数继续每级增加1，费用降低以0为下限。数字和规则变化按模型字段实现，牌面按等级渲染；同组各实际对象独立升级，升级不发放关联牌。复制继承实际升级等级，战斗结束按原有战斗升级还原规则处理。',
    balance: '基础2费，升级1费；本牌消耗。不同知识牌的升级增量不同，逐模型评估重复升级的上限表现。',
    port: '输出／防御／运转', dependencies: ['JK10', 'JK10B', 'JK11', 'JK15B', 'A', 'B']});

add('JG21', 'growth', '能力', 1,
  '伤害加成改为在伤害倍率结算后加入。', 'CB07', {
    layer: '每段伤害计算顺序', oldRule: '伤害加成先加入该段原始伤害，再参与后续伤害倍率计算。',
    exception: '将伤害加成的贡献移到全部伤害倍率计算之后、格挡和最终伤害限制之前。',
    result: '该分量保留其原值，不受虚弱、易伤等伤害倍率的放大或缩小，原伤害部分仍按原规则计算。',
    rules: '读取该实际牌的伤害加成贡献，将基础伤害、力量等其他来源先按原流程处理伤害倍率和取整，再加入本分量。格挡、无实体的最终伤害限制、生命值结算等后续步骤保持原规则。每段与每个目标分别处理。该伤害仍是原牌造成的普通伤害，保留来源和类型；同类计算顺序设置一份。',
    balance: '基础1费，升级0费。对正向与负向倍率都会产生影响，不额外增加伤害加成存量。',
    port: '输出', dependencies: ['JG03', 'JG09', 'JG16', 'JA13']});

add('JG22', 'growth', '能力', 3,
  '伤害加成按卡牌耗能的倍数生效。', 'CB08', {
    layer: '伤害加成贡献公式', oldRule: '每段伤害增加一份存储的伤害加成，与卡牌费用分别计算。',
    exception: '每段伤害的加成贡献改为存量乘以该牌耗能。',
    result: '3费牌的每段获得3倍存量贡献，0费牌的该分量为0；费用变化可以改变既有投资的兑现量。',
    rules: '每段读取当前实际牌的有效耗能C及伤害加成S，以S×C作为加成贡献，存量S本身保持不变。X费读取本次实际打出记录的X；星矢沿布置记录保留X，普通数值费用在触发时读取当前字段。实际支付封顶或免费打出方式与费用字段分别处理。JG21改变此贡献进入计算流程的位置，同类公式设置一份。',
    balance: '基础3费，升级2费。可以显著提高高费载体的数值，JA02将费用设为0时相应贡献也为0。',
    port: '输出', dependencies: ['JG09', 'JG16', 'JH01', 'JT02']});

add('JG23', 'growth', '能力', 3,
  '每5点伤害加成，使该牌的伤害次数增加1。', 'CB09', {
    layer: '伤害次数计算公式', oldRule: '卡牌原有伤害次数与伤害加成存量分别记录，加成只贡献每段数值。',
    exception: '伤害次数增加当前伤害加成除以5向下取整的结果，同时保留每段伤害加成。',
    result: '10点伤害加成同时提供每段加成与2次额外伤害，数值提升由新段数公式产生。',
    rules: '固定伤害指令在执行前读取该实际牌当前存量S，以原有效次数N加floor(S/5)作为本次次数；不向原字段永久写入派生结果。JG03改变原有效次数，JG16转移存量后派生次数随之重算。每个独立伤害指令使用同一规则，未含伤害指令的牌仍不产生伤害。倍率或存量设置不递归生成额外派生次数，同类公式设置一份。',
    balance: '基础3费，升级2费。单次高存量、多目标与重放可能共同带来高数值，独立于规则分类评估。',
    port: '输出', dependencies: ['JG03', 'JG09', 'JG16', 'JA13']});

add('JH04', 'utility', '能力', 3,
  '打出卡牌时，最多支付2点能量。', 'CB10', {
    layer: '出牌支付规则', oldRule: '普通出牌需要拥有并支付等于当前耗能的能量。',
    exception: '普通能量支付改为min(当前耗能,2)，能量充足条件同步按实际应付额判断。',
    result: '高费牌保留其费用字段与高费判定，但每次实际最多支付2点能量。',
    rules: '在本次其他费用修正后确定C，实际支付min(C,2)，正常0费仍支付0。费用字段不设为2，JK10和JG22等费用读取继续使用C。X费先按当前可用能量确定本次X，再封顶实际支付，不开放任意超额X。明确的免费出牌优先支付0，非能量代价按各自规则处理。多份支付上限取相同上限，不重复扣减。',
    balance: '基础3费，升级2费。安装本身在规则生效前正常支付，随后多张高费牌可受益。',
    port: '运转', dependencies: ['JK10', 'JG22', 'JH01', 'JT02']});

add('JH05', 'utility', '能力', 2,
  '每回合的起始能量至少等于本场战斗中打出过的最高耗能。', 'CB11', {
    layer: '回合能量刷新公式', oldRule: '每回合起始能量额度主要由基础值和能量上限修正决定。',
    exception: '为起始能量额度增加本场已打出最高耗能这一动态下限。',
    result: '打出过5费牌后，后续每回合的起始能量额度至少为5。',
    rules: '读取本场所有已经完成打出流程的实际牌在打出时的有效耗能，取最大值H；X费采用当次X。回合开始按max(原规则起始额度,H)确定刷新额度，原规则保留能量、其他额外能量与起始事件顺序分别处理。免费或支付封顶不会修改记录用的费用字段；只布置一次的星矢、重放牌效和额外触发不重复记录出牌。',
    balance: '基础2费，升级1费。历史记录覆盖安装前的本场出牌；实际收益取决于费用增加与高费卡牌。',
    port: '运转', dependencies: ['JK10', 'JH01', 'JH04', 'JT02']});

add('JH06', 'utility', '能力', 2,
  '耗能不低于2的牌可以分次支付，付清时打出。', 'CB12', {
    layer: '支付与出牌的原子性', oldRule: '一张普通数值费用牌需要在一次出牌动作中付清全部费用。',
    exception: '允许为一个实际对象预付部分能量，款项保留，付清后才执行一次正常出牌。',
    result: '可以跨行动或跨回合为高费牌支付能量，完成时按原牌规则结算。',
    rules: '手牌中的数值费用牌当前耗能不低于2时，玩家可选择支付部分真实可用能量；预付不记作打出，按实际对象记录本场已付额P，离开手牌仍保留记录。再次准备打出时读取当前费用C，还需支付max(0,C-P)，付清并有合法目标后才完成出牌并清空记录。费用下降导致超额已付时不返还，费用增加则补足差额；尚未在手时不自动打出。复制不复制支付记录，X费不参与此例外；明确免费打出或移除该对象时清除其记录。JH04存在时，本次总应付额先按其上限确定，再扣除已付额。',
    balance: '基础2费，升级1费。已付能量仍是实际支出，游戏需要显示分次支付记录，结算细节留在实现约定。',
    port: '运转', dependencies: ['JK10', 'JH01', 'JH04']});

for (const t of terms) {assert(!data.terms.some(x => x.id === t.id)); data.terms.push(t);}
for (const c of cards) {assert(!data.cards.some(x => x.id === c.id)); data.cards.push(c);}
const known = new Set(data.terms.map(t => t.id));
assert.equal(cards.length, 12);
for (const c of cards) {
  for (const id of c.termIds) assert(known.has(id), c.id + ' 未知词条');
  assert.equal(c.text, c.upgradedText); assert.equal(c.upgradedCost, c.cost - 1);
  assert(c.design.layer && c.design.oldRule && c.design.exception && c.design.result);
  assert(!before.cards.filter(x => x.trashedAt).some(x => x.text === c.text), c.id + ' 重复旧牌');
  assert(!/获得\d+点力量|每当.*(?:消耗|弃置|丢弃)/.test(c.text), c.id);
}
assert.deepEqual(data.cards.slice(0, before.cards.length), before.cards, '保留全部现有牌');
for (const key of Object.keys(before).filter(k => !['cards', 'terms'].includes(k))) assert.deepEqual(data[key], before[key], key);
const batches = [
  {id: batchId + '-terms', label: '补充第六轮规则分类', collection: 'terms', ids: terms.map(t => t.id), entries: terms},
  {id: batchId + '-cards', label: '新增12张机制规则突破候选', collection: 'cards', ids: cards.map(c => c.id), entries: cards}
];
const summary = {newBreakthroughs: 12, newCards: 12, perMechanism: {stage: 3, partner: 3, growth: 3, highCost: 3},
  active: data.cards.filter(c => !c.trashedAt).length, pool: data.cards.filter(c => !c.trashedAt && c.pool).length,
  grants: data.cards.filter(c => !c.trashedAt && !c.pool).length, trash: data.cards.filter(c => c.trashedAt).length, total: data.cards.length};
const correction = {source: '用户明确纠正', userText: criterion,
  supersedes: '第五轮复盘中根据删留推导的设计必要条件。',
  withdrawnInferences: ['具名配合应优先于通用规则', '单牌字段调度应优先于全局规则', '低门槛和立即用途是突破牌的必要条件', '高数值主要属于普通收益而不属于突破'],
  method: '先说明原规则和规则改动；数值、打法、强度、费用与复杂度分别评价。删除记录保留取舍事实，原因以用户说明为准。'};
const document = {schemaVersion: 1, id: batchId, date: '2026-11-02', title: '纯那第六轮规则突破探索',
  sourceRevision: live.revision, criterionCorrection: correction, summary, cards, terms,
  sources: ['D:/Github/STS2_Card_Reserch/docs/体系分析流程.md', 'D:/Github/STS2_Card_Reserch/docs/体系驱动卡池设计流程.md',
    'D:/Github/STS2_Card_Reserch/docs/职业卡池设计要求_按五职业规律.md',
    'D:/Github/STS2_Card_Reserch/docs/mods/sts2_character_cards_zh.md',
    'D:/Github/STS2_Card_Reserch/docs/mods/karen_mod_character_cards_zh.md', liveFile.replaceAll('\\', '/')],
  styleSnapshot: structuredClone(data.styles),
  review: {scope: '四方向各3张规则改写候选；本轮不新增关联牌或普通示例牌。',
    highCost: '按现有定义，高费仍为卡池倾向，费用规则候选归体系外。',
    naming: '暂用编号；正式名称继续按原作原词与出处要求处理。',
    implementation: '牌面保持动作短句，选择器、公式、支付记录与结算顺序列入内部备注；本轮更新设计数据。',
    pending: ['规则分类与强度取舍分开评估。', '重复升级增量需要逐知识牌模型配置。', '分次支付需要实际对象级记录与对应操作界面。']}};
write(path.join(dataDir, '纯那第六轮规则突破探索.json'), document);
write(path.join(here, '.tmp-sixth-before.json'), {revision: live.revision, data: before});
seed.data = data; seed.revision = live.revision;
seed.history = structuredClone(live.history); seed.undoStack = structuredClone(live.undoStack); seed.redoStack = structuredClone(live.redoStack);
seed.assets = {...seed.assets, ...live.assets}; seed.seedBatches.push(...batches);
seed.appliedSeedBatches = [...new Set([...seed.appliedSeedBatches, ...live.appliedSeedBatches, ...batches.map(b => b.id)])];
write(path.join(here, 'seed.json'), seed);
const trackingFile = path.join(dataDir, '纯那职业卡池设计记录.json'), tracking = read(trackingFile);
tracking.stage = '按用户定义进行机制规则突破探索'; tracking.status = '新增12张规则改写候选';
tracking.criterionCorrection = correction;
if (!tracking.requirements.includes(criterion)) tracking.requirements.push(criterion);
tracking.current = {...summary, source: '纯那Mod设计工作台_本地编辑.json'};
tracking.batches.push({id: batchId, date: '2026-11-02', sourceRevision: live.revision, ...summary,
  source: '纯那第六轮规则突破探索.json', criterionCorrection: correction});
tracking.recommendations = {stage: ['JA32', 'JA33', 'JA34'], partner: ['JK26', 'JK27', 'JK28'], growth: ['JG21', 'JG22', 'JG23'], highCost: ['JH04', 'JH05', 'JH06']};
write(trackingFile, tracking);
console.log(JSON.stringify({sourceRevision: live.revision, summary, ids: cards.map(c => c.id),
  liveFileUnchanged: read(liveFile).revision === live.revision}));
