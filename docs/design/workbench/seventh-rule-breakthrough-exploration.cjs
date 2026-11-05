const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const here = __dirname, dataDir = path.resolve(here, '../data');
const read = file => JSON.parse(fs.readFileSync(file, 'utf8'));
const write = (file, value) => fs.writeFileSync(file, JSON.stringify(value, null, 2) + '\n');
const batchId = 'junna-seventh-rule-breakthrough-20261102-v1';
const seed = read(path.join(here, 'seed.json'));
assert(!seed.seedBatches.some(b => b.id.startsWith(batchId)), '本轮已写入；保留当前编辑。');
const liveFile = path.join(dataDir, '纯那Mod设计工作台_本地编辑.json');
const live = read(liveFile), before = structuredClone(live.data), data = structuredClone(before);
const criterion = '突破限制牌重点是修改游戏规则（游戏底层机制，或者新增的机制）本身。可能是带来高数值，也可能是改变正常的打法，都有可能。';
const updates = [];
function update(collection, id, values) {
  const item = data[collection].find(x => x.id === id);
  assert(item && !item.trashedAt, id);
  const changed = Object.fromEntries(Object.entries(values).filter(([k, v]) => JSON.stringify(item[k]) !== JSON.stringify(v)));
  if (!Object.keys(changed).length) return;
  const fields = [...new Set([...(collection === 'cards' ? ['text', 'upgradedText', 'cost', 'upgradedCost', 'notes'] : []), ...Object.keys(changed)])];
  const expected = Object.fromEntries(fields.map(k => [k, structuredClone(item[k])]));
  updates.push({collection, id, values: changed, expected, ...(collection === 'cards' ? {expectedAbsent: ['trashedAt']} : {})});
  Object.assign(item, structuredClone(changed));
}
const get = id => data.cards.find(c => c.id === id);
const knowledge = get('JK27');
const knowledgeDesign = {classification: '规则改写', criterion, layer: '知识关联抽牌规则',
  oldRule: '知识关联原用于获得牌时附带发放；普通抽牌按当前抽牌指令取牌。',
  exception: '抽到知识牌时，按其关联关系追加抽取关联知识牌。',
  result: '知识牌的关联关系参与抽牌结算，组内牌可以随一次知识牌抽取共同抵达。',
  rules: knowledge.notes + ' 按知识组配置识别关联牌及数量，实际抽取沿用抽牌堆、手牌上限和抽牌事件规则；直接放入手牌与真正抽牌分别识别。同名实际对象和各次抽牌事件按用户备注完整处理。',
  balance: `基础${knowledge.cost}费，升级${knowledge.upgradedCost}费；基础与升级使用同一关联抽取规则${knowledge.upgradedText.includes('固有') ? '，升级获得固有' : ''}。`,
  port: '运转', dependencies: ['JK05', 'JK11', 'JK15', 'A', 'B'],
  roleReason: '将知识牌关联关系扩展到抽牌结算。'};
update('cards', 'JK27', {direction: '关联抽取', design: knowledgeDesign,
  termIds: [...new Set([...knowledge.termIds, 'TR16'])]});
update('terms', 'CB05', {name: '关联抽取', definition: '抽到知识牌时，同时按关联关系抽取其关联知识牌；同名牌与数量沿用户牌面和备注结算。',
  notes: '按用户当前JK27牌面同步；历史洗牌方案保留在第六轮探索记录。'});
update('cards', 'JG23', {upgradedText: get('JG23').text});
update('cards', 'JG03', {upgradedText: get('JG03').text});
const energyRule = '每次实际打出攻击牌或星矢牌，在下个自己的回合开始时追加获得能量；基础每次1点、升级每次2点。同回合的合格出牌逐次累积，预约收益结算后清空。星矢布置记录一次出牌，之后完成、额外触发与重放牌效按各自事件处理。';
update('cards', 'JH05', {role: '机制核心牌', direction: '下回合能量',
  termIds: ['TR09', 'TR12', 'TR04', 'EF09'], breakthroughIds: [],
  design: {classification: '事件收益', layer: '出牌后的预约能量', port: '运转',
    trigger: '实际打出攻击牌或星矢牌后', reward: '下回合获得1点能量，升级为2点',
    rules: energyRule, balance: '基础与升级均为1费，升级提高每次预约的能量。',
    dependencies: ['JA13', 'JA17', 'JT03'], roleReason: '攻击与星矢出牌的延迟能量收益。'},
  notes: '当前定位：机制核心牌。\n基础与升级均为1费；打出攻击牌或星矢牌后，下回合分别获得1／2点能量。\n结算约定：' + energyRule});

const categories = [
  ['DB01', '加速倍率', 'stage', '改写一次加速请求所减少的回合数。'],
  ['DB02', '重放布置', 'stage', '将星矢重放从同一布置的多次结算改为多个独立布置。'],
  ['DB03', '等待倍率', 'stage', '将布置时的初始回合数接入星矢伤害倍率。'],
  ['DB04', '抽牌来源', 'partner', '扩展抽牌名额可访问的牌区，新增弃牌堆中的知识牌。'],
  ['DB05', '生成单位', 'partner', '将单张知识牌生成请求改为整组知识牌生成。'],
  ['DB06', '关联结算', 'partner', '让实际打出的知识牌调用手牌关联牌的牌效。'],
  ['DB07', '加成增长', 'growth', '伤害加成每次进入伤害计算后，更新其自身存量。'],
  ['DB08', '加成减费', 'growth', '将伤害加成存量接入该牌当前费用公式。'],
  ['DB09', '转移保留', 'growth', '改写伤害加成转移时来源存量的清除规则。'],
  ['DB10', '能量留存', 'utility', '把回合结束的剩余能量转为本场战斗的每回合起始能量。'],
  ['DB11', '追加支付', 'utility', '允许一次出牌追加多份正数费用，以增加牌效结算次数。'],
  ['DB12', '费用与次数', 'utility', '将卡牌当前耗能接入其造成伤害次数的公式。']
];
const terms = categories.map(([id, name, system, definition]) => ({uid: 'term_' + id, id, name,
  kind: '突破', definition, template: '', systems: [system], unit: '',
  notes: '第七轮规则改写分类；不作为新增玩家关键词。', sources: ['纯那第七轮规则突破探索']}));
const cards = [];
function add(id, system, type, cost, text, category, design) {
  const termIds = [category];
  if (system === 'stage') termIds.push('KW_STAGE');
  if (system === 'partner') termIds.push('KW_PARTNER');
  if (system === 'growth') termIds.push('KW_BONUS');
  if (text.includes('重放')) termIds.push('KW_REPLAY');
  cards.push({uid: 'draft_card_' + id, id, name: id, system, type, rarity: '稀有', pool: true,
    cost, upgradedCost: cost - 1, turns: null, upgradedTurns: null, target: '自己', text, upgradedText: text,
    keywords: [], termIds, breakthroughIds: [category], archetypeIds: system === 'utility' ? [] : ['SYS_' + system],
    partnerCardIds: [], art: 'defaultPortrait', role: '突破限制牌', direction: terms.find(t => t.id === category).name,
    draftStatus: '探索候选', draftBatch: batchId,
    naming: {original: '', work: '', url: '', adaptation: '名称待定；当前显示设计编号。'},
    design: {classification: '规则改写', criterion, ...design, roleReason: design.oldRule + ' → ' + design.exception},
    notes: ['第七轮规则突破探索；卡名待定。', '原规则：' + design.oldRule,
      '规则改动：' + design.exception, '结果：' + design.result,
      '结算约定：' + design.rules, '费用与强度：' + design.balance].join('\n')});
}

add('JA35', 'stage', '能力', 2, '星矢加速的回合数翻倍。', 'DB01', {
  layer: '加速请求公式', oldRule: '加速N回合，将剩余回合数减少N，下限为0。',
  exception: '一次加速请求的N先乘以2，再应用到原目标集合。',
  result: 'JA16的加速1变为加速2，JA17对每个星矢的加速1也变为2。',
  rules: '只修改主动加速指令的请求量，之后按剩余计数取实际减少量；自然推进仍按原规则。立即加速到完成最终仍只减少实际剩余数值。目标范围、完成事件和去向沿原规则；实际推进收益读取翻倍并截断后的实际值。每份本能力为请求量提供一次乘2修正，各份按来源执行一次。',
  balance: '基础2费、升级1费；数值上限受实际剩余回合数约束。', port: '输出／防御／运转', dependencies: ['JA16', 'JA17', 'JT03']});

add('JA36', 'stage', '技能', 1, '你打出的下1张星矢牌获得重放1，重放独立布置。', 'DB02', {
  layer: '星矢重放解释规则', oldRule: '重放次数记录在同一个实际布置上，归0时连续执行牌效。',
  exception: '下一张星矢的原始执行与每份重放各建立独立布置。',
  result: '重放1建立两个星矢，可以分别加速并各自产生完成事件。',
  rules: '正常支付原牌一次费用并记录一次出牌，原实际牌承担首个布置，其他每份重放创建一个实际复制品。各布置继承目标、初始回合数、有效修改和内部子类型，各自的重放次数清为0；副本建立本身不再次解释重放。实际支付归首个布置记录，副本支付记录为0，费用字段照常继承。各布置独立处理完成和去向。多份本牌的下一张修正合并增加重放次数，独立布置模式设置一份。',
  balance: '基础1费、升级0费；仍使用有限张数的下一张修正，独立布置的数量收益需要评估。', port: '输出／防御／运转', dependencies: ['JA04', 'JA13', 'JA15', 'JA22']});

add('JA37', 'stage', '能力', 3, '星矢的伤害按初始回合数的倍数生效。', 'DB03', {
  layer: '星矢伤害倍率', oldRule: '星矢初始回合数控制等待，通常不参与该牌的伤害倍率。',
  exception: '以该布置记录的初始回合数R作为伤害倍率。',
  result: '初始3回合的星矢伤害按3倍计算，加速后仍读取原初始回合数。',
  rules: '在布置时记录包含有效初始修正的R，之后每段伤害以R加入正常伤害倍率流程，格挡和最终伤害限制仍按原规则。剩余回合数下降不改变R；R为0时该分量伤害为0。伤害加成仍计入原伤害计算，格挡、抽牌、生成和驻场监听等其他牌效保持原规则。多份本能力按各自来源提供倍率。',
  balance: '基础3费、升级2费；长等待与初始回合数增加可带来高数值，加速仍可提前兑现。', port: '输出', dependencies: ['JA02', 'JA04', 'JA13', 'JG23']});

add('JK29', 'partner', '能力', 2, '抽牌时，可以改为抽取弃牌堆中的知识牌。', 'DB04', {
  layer: '抽牌取牌区域', oldRule: '普通抽牌从抽牌堆取牌，弃牌堆主要通过重洗重新进入抽牌来源。',
  exception: '允许把原有抽牌名额用于抽取弃牌堆中的知识牌。',
  result: '已有知识牌可以直接占用常规或牌效的抽牌名额，JK27的关联抽取也能访问弃牌堆。',
  rules: '每个实际抽牌名额可以选择一个弃牌堆知识牌替代原取牌；带模型、牌名等筛选条件的抽取继续检查原条件。取走实际对象后发布正常抽牌事件，沿用手牌上限与停止抽牌规则。选择普通取牌时继续使用抽牌堆流程。直接放入手牌与生成或复制分别处理。',
  balance: '基础2费、升级1费；保留原抽牌数量，只改名额可以访问的来源区域。', port: '运转', dependencies: ['JK05', 'JK11', 'JK15', 'JK27']});

add('JK30', 'partner', '能力', 3, '生成知识牌时，改为生成其整组牌。', 'DB05', {
  layer: '知识牌生成单位', oldRule: '奖励与发现按组获得，普通单张生成只生成指定模型和数量。',
  exception: '把一个知识牌生成请求展开为其知识组配置。',
  result: 'A生成B时，生成结果改为整组A与两张B，并进入该次生成原指定的区域。',
  rules: '按根生成请求的每个知识牌对象展开一组配置，原指定对象计入组内对应的一份，其余份数补齐。组内升级沿原生成动作与组配置分别处理；补齐对象不递归发起新的整组生成请求。生成与复制、奖励、发现使用各自动作类型，本能力只改生成；复制继续采用当前单张复制规则。原目的区域与满手等规则保留。',
  balance: '基础3费、升级2费；整组生成可持续扩充牌组，组内安装牌与实际到手速度分别影响收益。', port: '输出／防御／运转', dependencies: ['A', 'B', 'JK27']});

add('JK31', 'partner', '能力', 3, '知识牌打出后，额外结算手牌中其关联牌的效果。', 'DB06', {
  layer: '知识关联的使用结算', oldRule: '知识牌打出时执行自己的牌效，关联关系不直接调用其他牌的效果。',
  exception: '一次实际知识出牌结束后，按当时手牌中实际关联对象追加结算其牌效。',
  result: '使用一张知识牌，可以同时调用手中伙伴的伤害、防御或能力效果。',
  rules: '在实际打出流程结束后的手牌快照中，按知识关联模型筛选全部实际对象，每个对象按其当前牌效与有效修改执行。调用效果保持该关联对象原位置，牌身的消耗、保留与费用等标签按实际出牌去向规则区分，不因本次调用移动本体。攻击伤害读取被调用实际牌的伤害加成；能力效果安装独立来源实例。额外执行本身沿原事件规则，不建立新的实际出牌事件或新的关联调用批次；过程中进入手牌的新对象留到后续实际出牌再检查。',
  balance: '基础3费、升级2费；多份同名关联牌按实际手牌快照参与，数值和能力叠加需要单独评估。', port: '输出／防御／运转', dependencies: ['JK11', 'JK15', 'JK27', 'A', 'B']});

add('JG24', 'growth', '能力', 3, '伤害加成在每次生效后翻倍。', 'DB07', {
  layer: '伤害加成存量更新', oldRule: '伤害加成参与一次伤害计算后，存量保持原值。',
  exception: '每次实际使用伤害加成贡献一段伤害后，将该实际牌的存量乘以2。',
  result: '同一载体的多次伤害可以依次使用S、2S、4S等加成，后续投资与结算次数共同影响上限。',
  rules: '有合法伤害目标并将正存量加入一段伤害计算时视为一次生效；该次伤害结算后更新对应实际牌的存量。多段与多目标沿原结算队列逐次处理；格挡后的生命损失数值不改变此前是否使用了加成。该存量更新不再触发自身，转移、复制或单纯显示数值按各自操作处理。JG23在一个伤害指令开始时确定本次派生次数，过程中存量变化供后续指令重新读取。',
  balance: '基础3费、升级2费；每份本能力各提供一次翻倍更新，高存量与多段会带来较高上限。', port: '输出', dependencies: ['JG03', 'JG09', 'JG16', 'JG23']});

add('JG25', 'growth', '能力', 2, '每5点伤害加成，使该牌耗能减少1。', 'DB08', {
  layer: '费用派生公式', oldRule: '卡牌耗能与该牌的伤害加成存量分别计算。',
  exception: '当前费用增加一项减免floor(伤害加成/5)，存量本身保留。',
  result: '10点伤害加成同时贡献伤害并减少2点耗能，转移投资也会重新分配减费。',
  rules: '对有伤害加成的数值费用牌，在当前费用公式中加入该项减免，最低为0，不向原基础费用永久写入派生结果。存量增加、转移或移除后重新计算；X费保留原X费规则。该减免真实改变当前耗能，后续高费条件和按耗能派生的效果读取新值。同类公式设置一份；伤害加成贡献和JG23派生次数继续分别计算。',
  balance: '基础2费、升级1费；费用降低会影响高费判定，以及按耗能增加伤害次数的其他规则。', port: '输出／运转', dependencies: ['JG09', 'JG16', 'JG23', 'JK10']});

add('JG26', 'growth', '能力', 2, '转移伤害加成时，来源牌仍保留原加成。', 'DB09', {
  layer: '伤害加成转移规则', oldRule: '转移时从来源清除被转出的存量，受体获得对应存量。',
  exception: '受体仍获得原转移数额，来源保留该次转移前的原加成。',
  result: 'JG16集中已有投资后，其他来源牌仍能继续使用其原投资。',
  rules: '先按原转移效果去重并记录合法来源对象及本次转出量，受体按原规则增加相应总额；来源的原清除步骤被本能力替换为保留。目标自身原存量只保留一份，不作为额外来源重复转入自身。本次仍属于转移操作，来源保留步骤不另触发一次转移或新获得；复制卡牌采用原继承规则。',
  balance: '基础2费、升级1费；重复使用转移工具可能增加全场存量，工具本身的费用与去向照常。', port: '输出', dependencies: ['JG16', 'JG23', 'JG24']});

add('JH07', 'utility', '能力', 3, '回合结束时，将剩余能量加入本场战斗的每回合能量。', 'DB10', {
  layer: '剩余能量的去留与刷新额度', oldRule: '普通剩余能量在回合结束时清空，不改变之后每回合的刷新额度。',
  exception: '把结束时的剩余能量转成持续本场的每回合能量增加量。',
  result: '本回合剩余2点能量，可以让之后每回合起始能量都增加2。',
  rules: '在本能力的回合结束时点读取实际剩余能量E，将该笔能量从当前值清除，并为每回合刷新额度增加E。后续回合可以继续把新的实际余量转入；各笔以回合和来源事件记录一次，已转出的能量不再作为同一时点的剩余量重复处理。其他下回合预约收益按各自时点获得，战斗结束清理本场增加量。',
  balance: '基础3费、升级2费；保留行动余量可以形成持续能量增长，等待回合与防御投入需要一起评估。', port: '运转', dependencies: ['JA02', 'JH05']});

add('JH08', 'utility', '能力', 3, '耗能不低于1的牌，可以多次支付费用来多次结算。', 'DB11', {
  layer: '一次出牌的支付与结算次数', oldRule: '一次正常出牌支付一份费用，原始牌效结算一次，其他追加由独立来源提供。',
  exception: '允许为同一次正数费用出牌追加支付，每追加一份费用增加一份牌效结算。',
  result: '2费牌可以支付6点能量，获得三份原始牌效，仍只使用一个实际对象。',
  rules: '仅接受正数数值耗能，准备出牌时锁定费用C并选择支付份数；正常首份按原支付方式处理，额外每份实际支付C点能量。0费和X费采用原使用方式。实际出牌事件仍记录一次，追加份数与原重放等追加按既有同根相加规则合并；星矢建立一个布置并记录总执行份数。整批后处理一次正常去向，额外执行不再次请求支付或再次触发同一支付规则。',
  balance: '基础3费、升级2费；追加支付受当前真实能量约束，能力、抽牌等载荷也能额外执行。', port: '输出／防御／运转', dependencies: ['JH05', 'JH07', 'JA13', 'JA15']});

add('JH09', 'utility', '能力', 3, '卡牌每有1点耗能，便额外造成1次伤害。', 'DB12', {
  layer: '费用与伤害次数公式', oldRule: '卡牌当前耗能通常不直接决定伤害指令的执行次数。',
  exception: '每个伤害指令的次数增加该实际牌当前耗能C。',
  result: '原本造成一次伤害的2费牌改为造成三次，伤害加成继续参与每次计算。',
  rules: '每个伤害指令开始时读取当前正数数值耗能C，以原有效次数N加C作为本次次数；X费采用本次记录的X。没有伤害指令的牌保持原功能，0费不获得额外次数。JG23的派生增加量与本来源相加，费用变化在后续指令重新读取；不把派生次数写回原字段。',
  balance: '基础3费、升级2费；费用交换、加费和减费均会影响该规则的伤害收益。', port: '输出', dependencies: ['JH01', 'JT02', 'JG23', 'JG25']});

for (const t of terms) {assert(!data.terms.some(x => x.id === t.id)); data.terms.push(t);}
for (const c of cards) {assert(!data.cards.some(x => x.id === c.id)); data.cards.push(c);}
const known = new Set(data.terms.map(t => t.id));
assert.equal(cards.length, 12);
for (const c of cards) {
  for (const id of c.termIds) assert(known.has(id), c.id + ' 未知词条 ' + id);
  assert.equal(c.text, c.upgradedText); assert.equal(c.upgradedCost, c.cost - 1);
  assert(c.design.oldRule && c.design.exception && c.design.result);
  assert(!before.cards.filter(x => x.trashedAt).some(x => x.text === c.text), c.id + ' 重复旧牌');
  assert(!/获得\d+点力量|每当.*(?:消耗|弃置|丢弃)/.test(c.text), c.id);
}
for (const old of before.cards) {
  const changed = updates.find(u => u.collection === 'cards' && u.id === old.id)?.values || {};
  assert.deepEqual(get(old.id), {...old, ...changed}, '保留现有牌 ' + old.id);
  assert.equal(get(old.id).text, old.text, '保留用户基础牌面 ' + old.id);
  assert.equal(get(old.id).cost, old.cost); assert.equal(get(old.id).upgradedCost, old.upgradedCost);
  assert.equal(get(old.id).trashedAt, old.trashedAt);
}
assert.equal(get('JK27').notes, before.cards.find(c => c.id === 'JK27').notes);
assert.equal(get('JH05').role, '机制核心牌'); assert.equal(get('JH05').breakthroughIds.length, 0);
for (const key of Object.keys(before).filter(k => !['cards', 'terms'].includes(k))) assert.deepEqual(data[key], before[key], key);
const batches = [
  {id: batchId + '-sync', label: '同步用户牌面与内部说明', updates, applyUpdatesOnImport: true},
  {id: batchId + '-terms', label: '补充第七轮规则分类', collection: 'terms', ids: terms.map(t => t.id), entries: terms},
  {id: batchId + '-cards', label: '新增四方向12张规则突破候选', collection: 'cards', ids: cards.map(c => c.id), entries: cards}
];
const summary = {newBreakthroughs: cards.length, syncedCards: updates.filter(u => u.collection === 'cards').length,
  syncedTerms: updates.filter(u => u.collection === 'terms').length,
  perMechanism: {stage: 3, partner: 3, growth: 3, highCost: 3},
  active: data.cards.filter(c => !c.trashedAt).length, pool: data.cards.filter(c => !c.trashedAt && c.pool).length,
  grants: data.cards.filter(c => !c.trashedAt && !c.pool).length, trash: data.cards.filter(c => c.trashedAt).length, total: data.cards.length};
const document = {schemaVersion: 1, id: batchId, date: '2026-11-02', title: '纯那第七轮规则突破探索',
  sourceRevision: live.revision, criterion, summary, cards, terms, syncUpdates: updates,
  sources: ['D:/Github/STS2_Card_Reserch/docs/体系分析流程.md', 'D:/Github/STS2_Card_Reserch/docs/体系驱动卡池设计流程.md',
    'D:/Github/STS2_Card_Reserch/docs/职业卡池设计要求_按五职业规律.md', liveFile.replaceAll('\\', '/')],
  styleSnapshot: structuredClone(data.styles),
  review: {scope: '先同步当前用户修改，再对星矢、知识、伤害加成及费用规则各提出3张候选。候选数量与最终体系预算分开记录。',
    synchronization: 'JK27保留用户备注并同步关联抽取说明；JG23升级措辞及JG03升级换行跟随基础文本；JH05按出牌后的预约能量更新为机制核心牌。',
    highCost: '沿用用户当前高费倾向定义，包含高费牌、加费效果与利用能量的效果，相关候选归体系外。',
    implementation: '本轮为设计数据；支付、复制、生成、额外执行与实际打出分别处理。结算说明保留在内部备注。',
    pending: ['候选强度、费用与最终取舍分别评估。', '使用能量、伤害次数和关联执行的组合，需要按记录的同根事件顺序实现。']}};
write(path.join(dataDir, '纯那第七轮规则突破探索.json'), document);
write(path.join(here, '.tmp-seventh-before.json'), {revision: live.revision, data: before, updates});
seed.data = data; seed.revision = live.revision;
seed.history = structuredClone(live.history); seed.undoStack = structuredClone(live.undoStack); seed.redoStack = structuredClone(live.redoStack);
seed.assets = {...seed.assets, ...live.assets}; seed.seedBatches.push(...batches);
seed.appliedSeedBatches = [...new Set([...seed.appliedSeedBatches, ...live.appliedSeedBatches, ...batches.map(b => b.id)])];
write(path.join(here, 'seed.json'), seed);
const trackingFile = path.join(dataDir, '纯那职业卡池设计记录.json'), tracking = read(trackingFile);
tracking.stage = '用户修改同步与第七轮机制规则突破探索'; tracking.status = '同步4张牌与1个分类，新增12张规则改写候选';
tracking.current = {...summary, source: '纯那Mod设计工作台_本地编辑.json'};
tracking.batches.push({id: batchId, date: '2026-11-02', sourceRevision: live.revision, ...summary,
  source: '纯那第七轮规则突破探索.json', synced: updates.map(u => ({collection: u.collection, id: u.id}))});
tracking.recommendations = {stage: ['JA35', 'JA36', 'JA37'], partner: ['JK29', 'JK30', 'JK31'],
  growth: ['JG24', 'JG25', 'JG26'], highCost: ['JH07', 'JH08', 'JH09']};
write(trackingFile, tracking);
console.log(JSON.stringify({sourceRevision: live.revision, summary, ids: cards.map(c => c.id),
  liveFileUnchanged: read(liveFile).revision === live.revision}));
