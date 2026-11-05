const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const here = __dirname, dataDir = path.resolve(here, '../data');
const read = file => JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
const write = (file, value) => fs.writeFileSync(file, JSON.stringify(value, null, 2) + '\n');
const batchId = 'junna-fourth-breakthrough-20261102-v1';
const seed = read(path.join(here, 'seed.json'));
assert(!seed.seedBatches.some(b => b.id.startsWith(batchId)), '本轮已写入；保留当前编辑。');
const live = read(path.join(dataDir, '纯那Mod设计工作台_本地编辑.json'));
const data = structuredClone(live.data), before = structuredClone(data);
const sources = [
  'D:/Github/STS2_Card_Reserch/docs/体系分析流程.md',
  'D:/Github/STS2_Card_Reserch/docs/体系驱动卡池设计流程.md',
  'D:/Github/STS2_Card_Reserch/docs/职业卡池设计要求_按五职业规律.md',
  'D:/Github/STS2_Card_Reserch/docs/mods/sts2_character_cards_zh.md',
  'D:/Github/STS2_Card_Reserch/docs/mods/karen_mod_character_cards_zh.md',
  path.join(dataDir, '纯那Mod设计工作台_本地编辑.json').replaceAll('\\', '/')
];
const terms = [
  ['SB19', '推进时点', 'stage', '将箭矢每回合的自然推进移至另一个时点，单个布置每个共享回合仍只自然推进一次。'],
  ['SB20', '支付时点', 'stage', '箭矢保留原费用，先布置，随后以未来获得的能量偿还实际欠费。'],
  ['SB21', '承伤时点', 'stage', '把实际生命值损伤延后到指定箭矢完成时结算，箭矢的等待时间同时成为承伤期限。'],
  ['RB21', '合并支付', 'partner', '同组的多张实际知识牌通过一次主动操作打出，合并支付最高的一项费用。'],
  ['RB22', '直接打出', 'partner', '已打出知识牌为同组实际牌开放额外出牌区域，仍按费用与类型正常打出。'],
  ['RB23', '整组复制', 'partner', '把复制单张知识牌的操作扩展为一整组；同一操作只扩展一次。']
].map(([id, name, system, definition]) => ({uid: 'term_' + id, id, name, kind: '突破', definition,
  template: '', systems: [system], unit: '', notes: '第四轮突破分类；只用于设计筛选。', sources: ['纯那第四轮突破探索']}));
const cards = [];
function add(id, system, type, cost, text, category, oldRule, exception, investment, rules, comparison, extra = {}) {
  const termIds = [category, ...(extra.termIds || [])];
  if (system === 'stage') termIds.push('KW_STAGE');
  if (system === 'partner') termIds.push('KW_PARTNER');
  if (system === 'growth') termIds.push('KW_BONUS');
  if (text.endsWith('消耗。')) termIds.push('KW_EXHAUST');
  const direction = [...data.terms, ...terms].find(t => t.id === category).name;
  const card = {uid: 'draft_card_' + id, id, name: id, system, type, rarity: '稀有', pool: true,
    cost, upgradedCost: cost - 1, turns: null, upgradedTurns: null, target: '自己', text, upgradedText: text,
    keywords: [], termIds: [...new Set(termIds)], breakthroughIds: [category],
    archetypeIds: ['SYS_' + system], partnerCardIds: [], art: 'defaultPortrait',
    role: '突破限制牌', direction, draftStatus: '探索候选', draftBatch: batchId,
    naming: {original: '', work: '', url: '', adaptation: '名称待定；当前显示设计编号。'},
    design: {classification: '规则突破', oldRule, exception, investment, rules, comparison,
      port: extra.port || '运转', dependencies: extra.dependencies || [],
      references: extra.references || [], risk: extra.risk || '',
      roleReason: oldRule + ' → ' + exception},
    notes: ['第四轮探索；卡名待定。', '原有限制：' + oldRule, '规则变化：' + exception,
      '保留的投入：' + investment, '结算约定：' + rules, '参照：' + comparison,
      '风险：' + (extra.risk || '费用与卡位留待实战调整。')].join('\n')};
  cards.push(card);
}

add('JA25', 'stage', '能力', 3,
  '加速效果改为作用于所有箭矢。', 'SB02',
  '单体加速与限定目标的加速，只覆盖各自选中的箭矢。',
  '替换加速操作的目标范围，让同一操作覆盖整个箭矢场面。',
  '3费安装，升级2费；需要先布置箭矢并取得加速工具，整体推进也会结束想要保留的在场效果。',
  '只替换加速指令的箭矢集合；原牌其余伤害、格挡等指令照常。固定N回合逐个应用N，立即加速到完成则逐个读取各自剩余回合数。一次指令在执行前获取并去重当前有效布置，逐个完成结算；过程中新增的布置留到下一次操作。自然推进不视为加速。已覆盖全场的加速执行一次，多份本能力只产生同一范围替换，不相互触发。JT03的自加速也可覆盖全场，返能次数仍由攻击出牌事件决定。',
  '本体《刀扇》把专属对象的目标规则改为全体；这里扩展的是箭矢加速操作的选取范围。与JA03的出牌触发加速、JA17的单次全场加速分别承担规则改写与工具角色。',
  {termIds: ['EF29'], port: '输出／防御／运转', dependencies: ['JA04', 'JA16', 'JA17', 'JT03'],
    references: ['本体：刀扇'], risk: 'JA04可同时兑现全场；JT03也会提前离场，构筑需要在结算爆发与在场窗口之间选择。'});

add('JA26', 'stage', '能力', 2,
  '箭矢的自然倒计时改在你的回合结束时减少。', 'SB19',
  '箭矢每个玩家方回合开始时自然推进，刚布置的箭矢通常要等到下一轮。',
  '保留每轮一次自然推进，把推进时点移到回合结束，使本回合的新布置也能赶上该时点。',
  '2费安装，升级1费；仍需正常支付布置费用并经历剩余等待，完成在回合结束，不能继续安排普通出牌。',
  '用自然推进来源标记替换回合开始的推进入口，剩余计数不直接设置或重置。每个布置记录本共享回合是否已自然推进：安装前已经推进的布置当轮结束不再推进，新布置可以推进一次。结束阶段只对入口快照中的布置处理，新生成布置从下一轮起参加；主动加速继续正常结算。多份本能力共享同一个时点替换。',
  '本体《所向无敌》使用回合结束这一特殊结算窗口，Karen《约定的那天》改变资源跨回合的去留。本候选改变的是箭矢自然推进时点，保留每轮一次的推进量。',
  {port: '输出／防御／运转', dependencies: ['JA13', 'JA15', 'JA22', 'JT03'],
    references: ['本体：所向无敌', 'Karen：约定的那天'], risk: '1回合箭矢可以在布置当轮完成；抽牌箭矢在回合结束时抵达，结束阶段的弃牌顺序应与本体事件队列一致。'});

add('JA27', 'stage', '技能', 2,
  '本回合打出的箭矢牌暂不支付费用，改为从之后获得的能量中扣除。消耗。', 'SB20',
  '布置箭矢必须先有足够能量并立即支付，无法预支后续回合的能量。',
  '把本回合箭矢的支付改为实际欠费，允许先布置，再用之后的能量偿还。',
  '2费启动，升级1费；本牌消耗，箭矢的原费用完整计入欠费，后续能量收入会被先行扣除。',
  '在正常费用修正后记录应付数值，免除当前能量充足的打出条件并登记等额欠费；费用字段与2费以上判定保持原值。已有能量不因登记欠费被重复扣除。此后所有新增能量，包括回合开始刷新得到的额度，先抵扣欠费，余量进入当前可用能量；欠费超过一次收入时继续保留至付清，战斗结束清除。JA02已改为0费的箭矢登记0；JK10等返能先偿还。X费仍以当前可用能量确定X，费用锁定一次；复制或重放牌效不重复登记同一次打出的欠费。',
  '本体《预借时间》先给固定能量并提高当回合费用；本候选逐张记录箭矢真实费用，改变支付顺序，保留完整的未来负担。',
  {termIds: ['EF09'], dependencies: ['JA13', 'JA15', 'JK10', 'JA02'], references: ['本体：预借时间'],
    risk: '可以用未来多个回合的行动换取当回合铺场；欠费必须跨回合保留，避免只扣一次可用能量就清空。'});

add('JA28', 'stage', '技能', 2,
  '选择一个箭矢。你受到的生命值损伤推迟到它完成时结算。消耗。', 'SB21',
  '攻防结算后的生命值损伤立即扣除，箭矢等待通常只延后自己的收益。',
  '让一个现存箭矢的等待时间承接未来损伤，完成时一次兑现这份负担。',
  '2费启动，升级1费；需要一个有效箭矢并消耗本牌，积存的损伤仍需承受，主动加速会提前到期。',
  '绑定一个实际布置instanceId。从安装到该布置完成前，按每次损伤发生时的伤害修正与格挡计算实际生命值损伤，再将结果暂存；格挡此时正常消耗。结算时先撤销本次暂存规则，再扣除累计数值，不再次计算攻防，也不被另一份暂存规则接走。多个有效绑定按最早安装的一份接管新损伤；各自存量只归各自实例。复制箭矢不复制暂存负担，额外触发不提前提取负担，异常离场也立即结清。失去生命的支付代价仍即时支付。实际生命损失事件在兑现时触发一次。胜利结束按战斗结束清理。',
  'Karen《水分补充》阻止下一次生命值损伤，本体《肉盾》改写承伤分配。这里以箭矢的现有完成时点延后实际承伤，不生成格挡成长或消耗收益路线。',
  {port: '防御', dependencies: ['JA13', 'JA15', 'JA04', 'JT03'],
    references: ['Karen：水分补充', '本体：肉盾'], risk: '延后致命伤可以争取击杀窗口，但回血不会清空暂存负担；加速全场也会使承伤提前。'});

add('JK20', 'partner', '能力', 3,
  '同组知识牌可以一并打出，只支付其中最高的耗能。', 'RB21',
  '同组知识牌分别打出并各自支付，获得一整组也意味着更多次支付。',
  '开放一次主动打出同组多张实际手牌的操作，将总费用改为其中最高的一项。',
  '3费安装，升级2费；组内至少两张牌必须同时在手，仍须支付最高费用，各牌效果按顺序执行。',
  '玩家从手牌选择同组的至少两张可打出知识牌并安排顺序；包含多份同名牌时按实际对象去重。先快照费用并支付最高一项，再将选中对象保留在同一待出牌批次，逐张记录正常出牌事件、选择目标并执行。每张保留原费用字段，支付总额记录在批次上，2费以上判定仍读取各牌费用。批次保留中的对象由该批次持有，召回效果跳过它们，已经结算并离开批次的对象可按原规则被召回；新抵达手牌的牌不自动加入批次。箭矢正常布置，能力正常安装，消耗去向照常；批次不再次嵌套自身。',
  '本体《子弹时间》改写一手牌的支付规则；Karen《约定之塔桥》批量打出专属区域中的真实牌。本候选要求固定知识组与同时在手，以最高费用替代逐张费用，组内仍是不同的实际牌。',
  {dependencies: ['JK10', 'JK10B', 'JK11', 'JK11B', 'A', 'B'], references: ['本体：子弹时间', 'Karen：约定之塔桥'],
    risk: '整组在手的回报显著，手牌调度仍是门槛；JK11双向召回需要尊重正在执行批次的对象归属。'});

add('JK21', 'partner', '能力', 3,
  '打出知识牌后，本回合可从抽牌堆和弃牌堆打出同组的知识牌。', 'RB22',
  '知识牌先进入手牌才能主动打出，同组其他牌散落牌堆时会中断衔接。',
  '打出一张后，为该组开放持续至回合结束的跨区域主动打出权限。',
  '3费安装，升级2费；必须先打出本组的一张牌，区域内的后续牌仍逐张支付正常费用。',
  '本能力安装后观察到实际知识出牌事件，将对应groupId加入本回合可直接打出的组集合。玩家可查看并选择抽牌堆或弃牌堆中该组的实际牌，支付后直接进入正常出牌流程；不先加入手牌，也不触发抽牌或进入手牌事件。每次出牌后均重新检查实际位置、费用与可打出条件。权限在回合结束清除，消耗区、已安装能力和布置中的箭矢沿用各自位置。类型、知识标记与自己的具名引用全部保留。',
  'Karen《命运的齿轮》让专属区域的牌绕过抵达手牌；这里由本组实际出牌取得两个普通牌区的主动调用权限，并完整支付。与JK07的触发回手、JK11的具名召回承担不同规则。',
  {dependencies: ['JK10B', 'JK11', 'JK11B', 'JK15', 'A', 'B'], references: ['Karen：命运的齿轮'],
    risk: '可重复调用非消耗知识技能和攻击，费用仍限制连续使用；选择界面展示当前合法对象，不能靠旧快照重复使用已离区的牌。'});

add('JK22', 'partner', '能力', 3,
  '复制知识牌时，改为复制其整组牌。', 'RB23',
  '奖励与发现获取知识整组，普通单张生成或复制仍只给指定对象。',
  '只对复制操作建立整组例外，让一次单张复制同时提供整组配置的卡牌。',
  '3费安装，升级2费；需要复制来源，整组新牌占用正常牌区与牌组循环，仍需取得并支付。',
  '将一次单张复制请求包装为一次组展开：目标本体保留正常复制继承；组配置中的其余份数以模型生成，不共享原牌伤害加成或临时费用。组内数量按配置执行，例如复制A或任一B都得到一张A与两张B，所选实际源计入其中一份。所有结果进入原复制操作的目的区域。每个原始复制请求只展开一次，补齐对象不再发起复制或关联获取。手牌溢出沿用正常去向。本能力只改变复制，A回合结束生成一张B仍只生成B；一般奖励、发现和其他单张生成保持当前规则。',
  'Karen《世界上最空虚的人》把专属资源区升级为完整运转来源。本候选让知识的整组结构进入复制操作；与JK15提供复制来源分别承担规则层与来源层。',
  {termIds: ['RL12'], dependencies: ['JK15', 'JK15B', 'A', 'B'], references: ['Karen：世界上最空虚的人'],
    risk: '整组复制显著增加牌量，抽牌受限时会稀释关键牌；组展开须只执行一次，避免补齐对象递归复制。'});

add('JG17', 'growth', '技能', 2,
  '选择一张手牌，使其伤害对所有敌人生效。消耗。', 'GB06',
  '集中投入的单体伤害牌需要逐个处理敌人，伤害段数增加也仍围绕原目标。',
  '改写一张实际牌的伤害目标集合，让同一份伤害与伤害加成覆盖所有敌人。',
  '2费修改，升级1费；本牌消耗，仍需实际伤害牌、增伤投入及原支付或等待流程。',
  '选择攻击牌或能造成伤害的箭矢牌，修改该实际对象本场战斗的伤害指令目标集合。每段按各个敌人的状态独立计算，保留原段数和伤害加成；已经群伤的指令不重复覆盖同一个敌人。牌上伤害以外的指令继续使用原目标，箭矢的记录目标与完成时点照常，不移动或重定向在场箭矢。类型、知识标记与内部子类型保持原值；复制继承有效修改，JG03增加的伤害段同样采用此目标集合。',
  '本体《刀扇》把小刀改为群体攻击；此处把范围修改施加到玩家选中的一张实际投资载体，服务JG03加段和JG16集中伤害加成，不引入小刀生成或低费连打收益。',
  {termIds: ['RL22', 'RL12'], port: '输出', dependencies: ['JG03', 'JG09', 'JG16', 'JA13', 'JK10B'],
    references: ['本体：刀扇'], risk: '多段、集中投资与多敌数量会相乘；单敌战中只保留范围修改，收益自然变小。'});

for (const term of terms) {assert(!data.terms.some(t => t.id === term.id)); data.terms.push(term);}
for (const card of cards) {assert(!data.cards.some(c => c.id === card.id)); data.cards.push(card);}
const knownTerms = new Set(data.terms.map(t => t.id));
for (const card of cards) {
  for (const id of card.termIds) assert(knownTerms.has(id), card.id + ' 未知词条 ' + id);
  assert.equal(card.text, card.upgradedText, card.id + ' 升级仅减费');
  assert.equal(card.upgradedCost, card.cost - 1);
  assert(card.design.oldRule && card.design.exception && card.design.investment && card.design.rules);
  assert(!/抽\d+张牌|获得\d+点力量|每当.*(?:消耗|弃置|丢弃)/.test(card.text), card.id);
  assert(card.text.length <= 48, card.id + ' 牌文长度');
}
assert.deepEqual(data.cards.slice(0, before.cards.length), before.cards, '完整保留旧牌与垃圾桶');
for (const key of Object.keys(before).filter(k => !['cards', 'terms'].includes(k))) assert.deepEqual(data[key], before[key], key);
assert.deepEqual(data.terms.slice(0, before.terms.length), before.terms, '保留现有词条');
const batches = [
  {id: batchId + '-terms', label: '补充第四轮突破分类', collection: 'terms', ids: terms.map(t => t.id), entries: terms},
  {id: batchId + '-cards', label: '新增第四轮8张突破候选', collection: 'cards', ids: cards.map(c => c.id), entries: cards}
];
seed.data = data;
seed.revision = live.revision;
seed.history = structuredClone(live.history);
seed.undoStack = structuredClone(live.undoStack);
seed.redoStack = structuredClone(live.redoStack);
seed.assets = {...seed.assets, ...live.assets};
seed.seedBatches.push(...batches);
seed.appliedSeedBatches = [...new Set([...seed.appliedSeedBatches, ...live.appliedSeedBatches, ...batches.map(b => b.id)])];
const summary = {newBreakthroughs: cards.length, newTerms: terms.length,
  active: data.cards.filter(c => !c.trashedAt).length,
  pool: data.cards.filter(c => !c.trashedAt && c.pool).length,
  grants: data.cards.filter(c => !c.trashedAt && !c.pool).length,
  trash: data.cards.filter(c => c.trashedAt).length, total: data.cards.length};
const document = {schemaVersion: 1, id: batchId, date: '2026-11-02', title: '纯那第四轮突破探索',
  sourceRevision: live.revision, sources,
  criterion: '改变既有支付、时点、区域、对象集合或整组操作规则；不以高频触发、单纯大数值或周期收益充当突破。',
  summary, cards, terms, recommendations: ['JA26', 'JK20', 'JK21', 'JG17'],
  review: {scope: '新增8张探索候选；既有77张记录、垃圾桶、知识组、机制和风格约定逐项保持原值。',
    playerText: '牌面只写玩家执行的规则；费用计算、实例归属、批次与结算顺序留在设计备注。',
    comparisons: '使用本地本体与Karen提取卡表，借鉴目标集合、支付、区域调用与结算时点的改写模式。',
    rejected: ['普通高频加速、返能、抽牌与复制来源单独列为常规牌，本轮不补。',
      '取回等待中箭矢、设置或重置倒计时、暂停自然推进和已删除的全场重放方案保持收起。',
      '知识整组同步升级和加成、改变组别、能力回收与技能转长期能力不另换编号恢复。',
      '消耗收益、弃牌收益、力量和格挡成长不加入。'],
    pending: ['费用、稀有度和最终卡位在选择候选后再校准。', 'JA27欠费与JA28延后承伤需要实现明确的结算归属。', '本轮为设计数据，不含游戏运行时实现。']}};
write(path.join(dataDir, '纯那第四轮突破探索.json'), document);
const trackingFile = path.join(dataDir, '纯那职业卡池设计记录.json');
const tracking = read(trackingFile);
tracking.date = '2026-11-02'; tracking.stage = '第四轮规则突破探索'; tracking.status = '参考本体与Karen新增8张突破候选';
for (const source of sources) if (!tracking.sources.includes(source)) tracking.sources.push(source);
const style = data.styles.find(s => s.id === 'STYLE_006');
if (style && !tracking.requirements.includes(style.content)) tracking.requirements.push(style.content);
tracking.current = {...summary, source: '纯那第四轮突破探索.json'};
tracking.batches.push({id: batchId, date: '2026-11-02', sourceRevision: live.revision, ...summary,
  source: '纯那第四轮突破探索.json', recommendations: document.recommendations});
tracking.recommendations = {stage: ['JA26', 'JA28'], partner: ['JK20', 'JK21'], growth: ['JG17']};
write(trackingFile, tracking);
write(path.join(here, 'seed.json'), seed);
console.log(JSON.stringify({sourceRevision: live.revision, summary, ids: cards.map(c => c.id),
  preservedOldCards: before.cards.length, preservedLiveRevision: read(path.join(dataDir, '纯那Mod设计工作台_本地编辑.json')).revision}, null, 2));
