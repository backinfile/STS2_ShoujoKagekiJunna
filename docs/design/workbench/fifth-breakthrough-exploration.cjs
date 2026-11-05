const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const here = __dirname, dataDir = path.resolve(here, '../data');
const read = file => JSON.parse(fs.readFileSync(file, 'utf8'));
const write = (file, value) => fs.writeFileSync(file, JSON.stringify(value, null, 2) + '\n');
const batchId = 'junna-fifth-breakthrough-20261102-v1';
const seed = read(path.join(here, 'seed.json'));
assert(!seed.seedBatches.some(b => b.id.startsWith(batchId)), '本轮已写入；保留当前编辑。');
const liveFile = path.join(dataDir, '纯那Mod设计工作台_本地编辑.json');
const live = read(liveFile), before = structuredClone(live.data), data = structuredClone(before);
const cards = [], partnerGroups = [];
const categories = [
  ['FB01', '费用与等待', 'stage', '交换一张未布置箭矢的耗能与初始回合数，重新安排两种投入。'],
  ['FB02', '完成用途', 'stage', '把指定一次布置的牌效提前执行，完成时改为返还原支付的能量。'],
  ['FB03', '加速分配', 'stage', '把同一次多对象加速的总推进预算集中使用，保留原操作总量。'],
  ['FB04', '交替形态', 'partner', '同一个实际知识牌打出后变为另一具名形态，改变下次抵达时的功能与费用。'],
  ['FB05', '牌效移植', 'partner', '把实际手牌的功能指令追加到一张具名知识牌，原对象仍保留。'],
  ['FB06', '响应出牌', 'partner', '在敌人攻击前为手中的具名知识牌开放主动出牌窗口。'],
  ['FB07', '基础转存', 'growth', '把固定基础伤害转成可转移的伤害加成，保留当前每段总额。'],
  ['FB08', '伤害结构', 'growth', '在两张实际手牌之间交换固定基础伤害与段数，费用和各自伤害加成留在原对象。'],
  ['FB09', '加成与段数', 'growth', '在一张牌上交换伤害加成与伤害段数，让两种投资结构相互转换。'],
  ['FB10', '费用转移', 'utility', '交换两张实际手牌的费用字段，牌效与其他修改仍归原对象。'],
  ['FB11', '使用去向', 'utility', '在打出前移除一张牌的消耗，以持续增加费用换取后续循环资格。'],
  ['FB12', '递增费用', 'utility', '先把一张实际手牌设为0费，此后每次实际使用使该对象变贵。']
];
const terms = categories.map(([id, name, system, definition]) => ({uid: 'term_' + id, id, name,
  kind: '突破', definition, template: '', systems: [system], unit: '',
  notes: '第五轮设计分类；不新增玩家关键词。', sources: ['纯那第五轮突破探索']}));
function add(id, system, type, cost, text, category, design, extra = {}) {
  const termIds = [category, ...(extra.termIds || [])];
  if (system === 'stage') termIds.push('KW_STAGE');
  if (system === 'partner') termIds.push('KW_PARTNER');
  if (system === 'growth') termIds.push('KW_BONUS');
  if (text.endsWith('消耗。')) termIds.push('KW_EXHAUST');
  const card = {uid: 'draft_card_' + id, id, name: id, system, type, rarity: '稀有', pool: true,
    cost, upgradedCost: extra.upgradedCost ?? Math.max(0, cost - 1), turns: null, upgradedTurns: null,
    target: extra.target || '自己', text, upgradedText: extra.upgradedText || text,
    keywords: system === 'partner' ? ['知识'] : [], termIds: [...new Set(termIds)],
    breakthroughIds: [category], archetypeIds: system === 'utility' ? [] : ['SYS_' + system],
    partnerCardIds: [], art: 'defaultPortrait', role: '突破限制牌',
    direction: terms.find(t => t.id === category).name, draftStatus: '探索候选', draftBatch: batchId,
    naming: {original: '', work: '', url: '', adaptation: '名称待定；当前显示设计编号。'},
    design: {classification: '规则突破', ...design, roleReason: design.oldRule + ' → ' + design.exception},
    notes: ['第五轮探索；卡名待定。', '原有限制：' + design.oldRule,
      '规则变化：' + design.exception, '保留的投入：' + design.investment,
      '结算约定：' + design.rules, '与旧案的区别：' + design.trashComparison,
      '待观察：' + design.risk].join('\n')};
  cards.push(card); return card;
}
function companion(id, type, cost, text, upgradedText, mainId, extra = {}) {
  cards.push({uid: 'draft_card_' + id, id, name: id, system: 'partner', type, rarity: '附带', pool: false,
    cost, upgradedCost: cost, turns: null, upgradedTurns: null, target: extra.target || (type === '攻击' ? '敌人' : '自己'),
    text, upgradedText, keywords: ['知识'], termIds: ['KW_PARTNER', ...(text.includes('保留。') ? ['KW_RETAIN'] : []),
      ...(text.includes('消耗。') ? ['KW_EXHAUST'] : [])], breakthroughIds: [], archetypeIds: ['SYS_partner'],
    partnerCardIds: [mainId], art: 'defaultPortrait', role: '知识牌', direction: '具名配合',
    naming: {original: '', work: '', url: '', adaptation: '名称待定；当前显示设计编号。'},
    notes: '与《' + mainId + '》同组。基础与升级分别沿用对应牌面；奖励、发现按组获得，单张生成或复制按当前约定处理。',
    draftStatus: '关联示例牌', draftBatch: batchId});
  cards.find(c => c.id === mainId).partnerCardIds = [id];
  partnerGroups.push({uid: 'draft_group_' + mainId, id: 'GROUP_' + mainId, title: mainId,
    mainCardId: mainId, grantedCardIds: [id], grantedCardCounts: {[id]: 1}});
}

add('JA29', 'stage', '技能', 1,
  '选择一张箭矢牌，交换其耗能与回合数。消耗。', 'FB01', {
    oldRule: '箭矢的能量费用与初始等待是两个分别设定的字段。',
    exception: '把一张实际箭矢的两项投入对调，保留牌效与类型。',
    investment: '1费，升级0费；本牌消耗，目标仍须正常支付交换后的费用并等待。',
    rules: '只选手牌中的数值费用箭矢。读取当前耗能和初始回合数字段并一次交换，修改持续本场战斗；已在场对象的剩余计数保持原值。全局持续修正按原层级继续应用，不循环重算本次交换。牌面效果、伤害加成、知识关系及内部子类型保留。',
    trashComparison: 'JA20与JA27替换支付来源；本候选仍付能量，以一张未布置牌自身的两个字段作交换。既有在场取回与重置方案继续收起。',
    risk: '费用与回合数相同的牌没有交换收益；加费牌和不同费用的箭矢才会打开取舍。',
    port: '输出／防御／运转', dependencies: ['JA13', 'JA15', 'JT03', 'JT01'],
    closestTrashIds: ['JA20', 'JA27']});

add('JA30', 'stage', '技能', 1,
  '你打出的下1张箭矢牌立即触发效果，完成时改为返还耗能。', 'FB02', {
    oldRule: '箭矢完成时执行自身延迟牌效，打出时只支付并布置。',
    exception: '该次布置的牌效在打出时执行，完成事件改为退回本次布置实际支付的能量。',
    investment: '本牌基础1费、升级0费；箭矢正常预付费用，等待结束才拿回这笔能量。',
    rules: '下一张箭矢正常选择目标、支付并建立布置，随后执行其牌效及原记录的重放次数；布置保留并自然推进。该布置的正常完成批次改为返还实际支付的能量一次，然后按普通去向处理；消耗仍覆盖弃置，JA22仍可改写正常弃置。额外触发继续执行原牌效，驻场监听仍按原入场与离场注册。立即加速到完成可以提前收到返还。只修改这一次布置，后续再次使用同一实际牌采用原规则。',
    trashComparison: 'JA26只移动全场推进时点，JA27先用后还欠费；这里先付清费用，并改变一次真实布置在打出与完成时分别做的事。',
    risk: '需要比较立即兑现与未来返能的节奏；免费布置时实际支付为0，返还同样为0。',
    port: '输出／防御／运转', dependencies: ['JA04', 'JA13', 'JA15', 'JA22'],
    closestTrashIds: ['JA26', 'JA27']});

add('JA31', 'stage', '能力', 2,
  '加速多个箭矢时，可以将总加速回合数用于一个箭矢。', 'FB03', {
    oldRule: '一次多对象加速按各自分配的推进量分别执行，不能将其集中给一个对象。',
    exception: '玩家可在原可选范围中集中分配同一操作的推进预算，总量守恒。',
    investment: '2费安装，升级1费；需要多对象加速工具与已布置的箭矢，未选中的对象放弃本次推进。',
    rules: '在执行一次加速指令前，按原指令确定有效对象快照，以各对象原计划实际减少量求和。玩家可改选该指令原合法范围内的一个布置，集中施加该预算，归0按正常完成处理；超过其剩余计数的部分丢弃。自然推进继续采用原规则，不进入本能力。同一指令只作一次分配，多份能力不复制预算，过程中新增布置不加入快照。',
    trashComparison: 'JA25把每次加速扩散到全场并扩大总推进；本候选用原操作的同一笔推进量集中兑现，保留其他箭矢的等待窗口。',
    risk: '当前主要与JA17配合；单对象加速保持原用法。后续补池时再评估多对象工具密度。',
    port: '输出／防御／运转', dependencies: ['JA17', 'JT03'], closestTrashIds: ['JA25']});

add('JK23', 'partner', '攻击', 3,
  '知识。造成24点伤害。将此牌变为《JK23B》。', 'FB04', {
    oldRule: '实际牌在反复抵达时仍使用同一功能和费用，成组牌各自独立。',
    exception: '同一个实际牌在高费攻击与零费防御两个具名形态之间交替。',
    investment: '攻击形态3费、升级2费；转换后的牌仍按正常去向离手，需要再次抵达。',
    rules: '本次牌效批次结束后，实际对象采用另一形态的原生类型、基础费用与功能指令；保留该实际对象的升级状态和可继承修改。去向先按本次打出原有规则处理，变形不额外打出或复制。伤害加成跟随实际对象保留，在无伤害的形态暂不贡献；另一张同组实际牌不受影响。JK23B打出后同样变回JK23。',
    trashComparison: 'JK16为全部知识牌开放随时选择形态；这里由一对具体牌在实际使用后交替，攻击与防御的到手顺序由使用历史决定。',
    risk: '两张初始牌可能逐步处于相同形态；升级跟随实际对象，不同步整组。',
    port: '输出／防御', dependencies: ['JG03', 'JG16', 'JK05'], closestTrashIds: ['JK16']}, {target: '敌人'});
companion('JK23B', '技能', 0,
  '知识。获得8点格挡。将此牌变为《JK23》。保留。',
  '知识。获得12点格挡。将此牌变为《JK23》。保留。', 'JK23');

add('JK24', 'partner', '技能', 2,
  '知识。将一张攻击或技能手牌的效果加入手牌中的《JK24B》。消耗。', 'FB05', {
    oldRule: '复制牌保留一个完整模型，无法把一张牌的功能追加进另一张实际知识牌。',
    exception: '以具名知识牌为实际载体，追加一个手牌模型的功能指令。',
    investment: '2费，升级1费；需要来源牌和JK24B同时在手，本牌与JK24B各自消耗。',
    rules: '选择手牌中的攻击或技能，以及一张实际JK24B；将来源的功能指令快照追加到该B的格挡指令之后、消耗去向之前。来源仍留在手中。牌身标签、知识关系、费用、类型、升级和消耗去向不移植；追加指令中的此牌指向接收B，目标在B打出时按指令重新选择。该B保留0费与自身消耗；只修改所选实际对象，不同步同名牌或整组。',
    trashComparison: 'JT07把实际攻击挂到箭矢上等待打出，JT05把技能改为持续能力；这里保留来源实际牌，将一次指令快照追加到具名、一次性使用的知识载体。',
    risk: '带多目标或再次改写牌效的来源需要校验指令目标；初稿仅接受攻击和技能，不移植能力安装规则。',
    port: '输出／防御／运转', dependencies: ['JK05', 'JK15'], closestTrashIds: ['JT05', 'JT07']});
companion('JK24B', '技能', 0, '知识。获得4点格挡。消耗。', '知识。获得7点格挡。消耗。', 'JK24');

add('JK25', 'partner', '能力', 2,
  '知识。敌人攻击前，你可以免费打出手牌中的《JK25B》。', 'FB06', {
    oldRule: '玩家通常在自己的行动阶段打牌，防御牌需要预先打出。',
    exception: '为一张实际具名防御牌开放敌人攻击前的可选出牌窗口。',
    investment: '2费安装，升级1费；需要把实际JK25B保留在手，使用后仍进入正常弃牌循环。',
    rules: '每次敌人实际攻击行动开始前暂停该行动，允许玩家选择手中的实际JK25B免费打出，也可以继续敌人行动。按实际卡牌正常结算出牌事件与去向；多段攻击只在整次行动前开放一个窗口，可用牌受实际手牌数量限制。该能力不生成、复制或召回B，非攻击行动不开放窗口；B在自己的回合仍可正常付费打出。被转为箭矢的B依其当前类型布置，仍须等待。',
    trashComparison: 'JK21开放两个牌区的通用出牌权限；这里保持手牌区域，只为一张具名防御工具开放玩家可决定是否响应的新行动窗口。',
    risk: 'B必须实际留在手中，提前打出后无法响应；实现时需明确暂停点与继续行动按钮。',
    port: '防御', dependencies: ['JK05', 'JK15'], closestTrashIds: ['JK21']});
companion('JK25B', '技能', 1, '知识。获得6点格挡。给予1层虚弱。保留。', '知识。获得9点格挡。给予1层虚弱。保留。', 'JK25', {target: '敌人'});

add('JG18', 'growth', '技能', 1,
  '选择一张手牌，将其基础伤害转为伤害加成。消耗。', 'FB07', {
    oldRule: '固定基础伤害属于牌的原生载荷，JG16只能转移后天伤害加成。',
    exception: '将一张牌的固定基础伤害转成可被既有伤害加成操作读取和转移的存量。',
    investment: '1费，升级0费；本牌消耗，目标基础伤害同时清零，每段当前总额保持原值。',
    rules: '只选手牌中具有一组固定基础伤害与固定段数的攻击或伤害箭矢。将该基础伤害字段设为0，再把原值加入该实际牌的伤害加成一次；不把力量、敌方状态或其他动态贡献计入。段数保持原值，因此转换前后当前每段伤害相同。重复作用于已清零基础字段时新增为0，修改只持续本场战斗。',
    trashComparison: 'JG13把伤害加成写入永久基础伤害；这里把本场固定基础字段转成可调度的投资，当前伤害不凭空增加，也不修改永久牌组。',
    risk: '与JG16共同使用可抽走被改造牌的原始输出，留下0基础载体；需要比较供体与受体价值。',
    port: '输出', dependencies: ['JG16', 'JG03', 'JA13', 'JK10B'], closestTrashIds: ['JG13']});

add('JG19', 'growth', '技能', 2,
  '选择两张手牌，交换其基础伤害和伤害次数。消耗。', 'FB08', {
    oldRule: '每张牌的固定伤害结构与其费用、类型和已有投资绑定。',
    exception: '在两个实际载体之间交换固定伤害结构，各自的费用与伤害加成仍留在原对象。',
    investment: '2费，升级1费；本牌消耗，需要同时持有两个适用载体。',
    rules: '两张牌都必须是具有一组固定基础伤害和固定段数的攻击或伤害箭矢，分别快照后交换这两个字段，持续本场战斗。伤害加成、能量费用、目标范围、其余功能指令、类型与箭矢回合数保持各自原值；计算当前动态伤害时仍逐个读取原持有者的有效修改。动态或多种异质伤害指令暂不进入该选择器。',
    trashComparison: 'JG04、JG05扩散已有投资，JG17把目标范围改为群体；这里移动基础载荷结构，保留各自投资的归属。',
    risk: '可把多段结构移给高投资对象，也可把高基础值移给低费对象；两张供体都需要重新评估。',
    port: '输出', dependencies: ['JG03', 'JG16', 'JA13', 'JK10B'], closestTrashIds: ['JG04', 'JG05', 'JG17']});

add('JG20', 'growth', '技能', 2,
  '选择一张有伤害加成的手牌，交换其伤害加成与伤害次数。消耗。', 'FB09', {
    oldRule: '伤害加成只作为每段附加值，固定段数是另一项独立字段。',
    exception: '在一个实际载体上交换投资存量与固定段数，改变之后继续投入的方式。',
    investment: '2费，升级1费；本牌消耗，原有伤害加成变为段数，原有段数成为新的伤害加成。',
    rules: '只选手牌中具有正整数伤害加成、一组固定基础伤害与正整数固定段数的攻击或伤害箭矢。两字段同时交换，保持基础伤害、费用、类型、目标范围及等待不变。交换作为存量改写处理，不另发布新获得伤害加成事件；效果持续本场战斗。动态段数或异质伤害指令暂不进入选择器。',
    trashComparison: 'JG01和JG06直接放大伤害加成数字或贡献；这里用实际存量替换段数，同时把旧段数写回存量，后续投资的结构随之改变。',
    risk: '成熟单段载体可变为多段，成熟多段载体也可能缩段；需观察与JG16集中投入后的上限。',
    port: '输出', dependencies: ['JG03', 'JG09', 'JG16'], closestTrashIds: ['JG01', 'JG06']});

add('JH01', 'utility', '技能', 2,
  '选择两张手牌，交换其耗能。消耗。', 'FB10', {
    oldRule: '两张牌的费用分别依附自己的牌效，无法挪给另一实际对象。',
    exception: '让一张低费牌承接另一张牌的高费用，另一张取得低费用，交换字段总量守恒。',
    investment: '2费，升级1费；本牌消耗，需要两张数值费用手牌同时到手，供体也会变贵。',
    rules: '交换两个实际手牌当前数值费用对应的本场字段，持续性全局修正按原层级继续应用。X费、不可打出的费用标记不参与。各自牌效、伤害加成、类型、知识关系与箭矢回合数不变；之后支付和高费条件分别读取各自新费用。',
    trashComparison: 'JA27建立未来欠费，JK20合并一整组支付；这里只交换两张当前手牌的费用，仍逐张正常付清。',
    risk: '低费供体需要承担高费，换出的便宜牌是否优先使用形成取舍；与JK10和JT01有直接连接。',
    port: '运转', dependencies: ['JK10', 'JT01'], closestTrashIds: ['JA27', 'JK20']});

add('JH02', 'utility', '技能', 2,
  '选择一张手牌，移除其消耗，耗能增加2。消耗。', 'FB11', {
    oldRule: '带消耗的实际牌打出后退出普通循环。',
    exception: '在打出前改写一个对象的使用去向，以持续费用增加换取后续循环。',
    investment: '2费，升级1费；本牌消耗，目标每次使用都多付2点能量，仍需再次抵达。',
    rules: '只选手牌中当前带消耗、具有数值费用的实际牌。移除其牌身消耗标签并增加2点本场耗能；牌效中的明确消耗其他对象等功能指令保留。不从消耗牌堆召回，不响应消耗事件，也不改能力安装后的正常位置。改造后的箭矢完成时走正常弃置，JA22可继续处理。',
    trashComparison: 'JA23开放消耗区出牌，JT04让能力打出后回到牌堆；这里在使用前改单张消耗标签，并保留普通类型去向和加费。',
    risk: 'B等返能牌改造后仍需付净能量；观察复制继承与费用覆盖，保持费用和标签的修改归属一致。',
    port: '运转', dependencies: ['B', 'JK15B', 'JA22', 'JT01'], closestTrashIds: ['JA23', 'JT04']});

add('JH03', 'utility', '技能', 2,
  '选择一张手牌，耗能变为0，此后每次打出后耗能增加1。消耗。', 'FB12', {
    oldRule: '固定费用通常决定每次重复使用的相同投入。',
    exception: '让一张实际牌以0费开始，后续使用逐次变贵，从首轮爆发转向高费投入。',
    investment: '2费，升级1费；本牌消耗，免费只对应改造后第一次使用，之后费用逐次增加。',
    rules: '选择一张数值费用手牌，设定本场费用为0，并为该实际对象登记出牌后加费1。每次实际打出只增加一次，额外执行牌效、箭矢完成和在场复制不增加使用次数。费用增加先于打出后的复制监听，JK15复制时继承已增加的费用及本修改。多次重新改造可重新设为0，同类加费规则按同一来源类型覆盖为一份；全局持续费用设置照常应用。',
    trashComparison: 'JT04通过回收同一个能力对象叠加安装；这里保持原类型与去向，只改变一个普通实际对象之后的支付曲线。',
    risk: '同一对象的0、1、2费阶段价值不同；后续高费能支持JK10，反复使用仍承担真实费用。',
    port: '运转', dependencies: ['JK10', 'JK15', 'JT01'], closestTrashIds: ['JT04']});

for (const t of terms) {assert(!data.terms.some(x => x.id === t.id)); data.terms.push(t);}
for (const c of cards) {assert(!data.cards.some(x => x.id === c.id)); data.cards.push(c);}
data.partnerGroups.push(...partnerGroups);
const known = new Set(data.terms.map(t => t.id));
const breakthroughs = cards.filter(c => c.role === '突破限制牌');
assert.equal(breakthroughs.length, 12); assert.equal(cards.length, 15);
for (const c of cards) {
  for (const id of c.termIds) assert(known.has(id), c.id + ' 未知词条 ' + id);
  assert(c.text.length <= 45, c.id + ' 牌面长度');
  assert(!before.cards.filter(x => x.trashedAt).some(x => x.text === c.text), c.id + ' 重复旧牌');
  assert(!/抽\d+张牌|获得\d+点力量|每当.*(?:消耗|弃置|丢弃)/.test(c.text), c.id);
  if (c.role === '突破限制牌') {
    assert(c.design.oldRule && c.design.exception && c.design.investment && c.design.trashComparison);
    assert.equal(c.upgradedText, c.text); assert.equal(c.upgradedCost, c.cost - 1);
  }
}
assert.deepEqual(data.cards.slice(0, before.cards.length), before.cards, '保留旧牌与垃圾桶');
assert.deepEqual(data.partnerGroups.slice(0, before.partnerGroups.length), before.partnerGroups);
for (const key of Object.keys(before).filter(k => !['cards', 'terms', 'partnerGroups'].includes(k))) assert.deepEqual(data[key], before[key], key);
const batches = [
  {id: batchId + '-terms', label: '补充第五轮突破分类', collection: 'terms', ids: terms.map(t => t.id), entries: terms},
  {id: batchId + '-cards', label: '新增四方向突破候选与具名关联牌', collection: 'cards', ids: cards.map(c => c.id), entries: cards},
  {id: batchId + '-groups', label: '补充三组具名知识牌', collection: 'partnerGroups', ids: partnerGroups.map(g => g.id), entries: partnerGroups}
];
const trash = before.cards.filter(c => c.trashedAt);
const bulk = live.history.find(r => r.label === '精简常规牌，保留机制示例牌');
const bulkIds = bulk.patches.filter(p => p.collection === 'cards' && !p.before?.trashedAt && p.after?.trashedAt).map(p => p.after.id);
const lessons = [
  {status: '记录事实', evidence: bulkIds, conclusion: '27张由精简常规牌的批量操作收起，主要反映数量与卡位取舍，分开记录。'},
  {status: '用户明确约定', evidence: ['STYLE_001', 'STYLE_005', 'STYLE_006'], conclusion: '抽牌受限；避开其他职业已有流派；牌面简洁，结算放内部。'},
  {status: '根据删留推断', evidence: ['JA25', 'JA26', 'JA27', 'JA28', 'JG17'], conclusion: '改写规则本身还需要具体玩法价值；仅扩大范围、移动时点或建立欠费承伤账本的候选均已撤下。'},
  {status: '根据删留推断', evidence: ['JK16', 'JK17', 'JK18', 'JK19', 'JK20', 'JK21', 'JK22', 'JK11', 'A'], conclusion: '知识优先探索具名牌之间的实际配合，减少整组管理规则。保留JK11与A的具名引用作为设计锚点。'},
  {status: '根据删留推断', evidence: ['JG01', 'JG04', 'JG05', 'JG13', 'JG14', 'JG15', 'JG03', 'JG16'], conclusion: '伤害加成优先探索具体载体的结构与已有存量调度，减少倍率、全局传播与永久转化。'},
  {status: '本轮执行准则', evidence: breakthroughs.map(c => c.id), conclusion: '每张写出规则变化、保留的代价与最接近旧案的区别；三张关联示例牌另列，升级先只减费。'}
];
const summary = {newBreakthroughs: 12, newGrants: 3, newCards: cards.length,
  perMechanism: {stage: 3, partner: 3, growth: 3, highCost: 3},
  active: data.cards.filter(c => !c.trashedAt).length, pool: data.cards.filter(c => !c.trashedAt && c.pool).length,
  grants: data.cards.filter(c => !c.trashedAt && !c.pool).length, trash: trash.length, total: data.cards.length};
const document = {schemaVersion: 1, id: batchId, date: '2026-11-02', title: '纯那第五轮突破探索',
  sourceRevision: live.revision, summary,
  sources: ['D:/Github/STS2_Card_Reserch/docs/体系分析流程.md', 'D:/Github/STS2_Card_Reserch/docs/体系驱动卡池设计流程.md',
    'D:/Github/STS2_Card_Reserch/docs/职业卡池设计要求_按五职业规律.md',
    'D:/Github/STS2_Card_Reserch/docs/mods/sts2_character_cards_zh.md',
    'D:/Github/STS2_Card_Reserch/docs/mods/karen_mod_character_cards_zh.md', liveFile.replaceAll('\\', '/')],
  trashReview: {total: trash.length, bulkTrimIds: bulkIds, remaining: trash.filter(c => !bulkIds.includes(c.id)).map(c => c.id), lessons},
  styleSnapshot: structuredClone(data.styles), cards, terms, partnerGroups,
  review: {scope: '四方向各3张突破候选，知识另配3张必要关联示例牌；已有85张记录与63张垃圾桶牌保留。',
    highCost: '按用户定义，高费仍是卡池倾向；三张费用规则候选归体系外，不增加关键词或新流派。',
    text: '面向玩家只保留动作短句；选择器、继承、去向、结算窗口放设计备注。',
    comparisons: '对照本体与Karen的费用、形态转换、重放、目标及牌区操作，保留原职业的消耗、弃牌、力量、小刀和充能球流派边界。',
    pending: ['费用和最终卡位留待取舍后校准。', 'JK24牌效移植与JK25响应窗口需要运行时支持；本轮只更新设计工作台。', 'JG18—JG20初稿先选择固定且同质的伤害结构。']}};
write(path.join(dataDir, '纯那第五轮突破探索.json'), document);
write(path.join(here, '.tmp-fifth-before.json'), {revision: live.revision, data: before});
seed.data = data; seed.revision = live.revision;
seed.history = structuredClone(live.history); seed.undoStack = structuredClone(live.undoStack); seed.redoStack = structuredClone(live.redoStack);
seed.assets = {...seed.assets, ...live.assets}; seed.seedBatches.push(...batches);
seed.appliedSeedBatches = [...new Set([...seed.appliedSeedBatches, ...live.appliedSeedBatches, ...batches.map(b => b.id)])];
write(path.join(here, 'seed.json'), seed);
const trackingFile = path.join(dataDir, '纯那职业卡池设计记录.json'), tracking = read(trackingFile);
tracking.stage = '垃圾桶复盘与第五轮突破探索'; tracking.status = '四方向各新增3张突破候选，另有3张具名关联牌';
tracking.current = {...summary, source: '纯那Mod设计工作台_本地编辑.json'};
tracking.batches.push({id: batchId, date: '2026-11-02', sourceRevision: live.revision, ...summary,
  source: '纯那第五轮突破探索.json', lessons});
tracking.recommendations = {stage: ['JA29'], partner: ['JK23', 'JK24'], growth: ['JG18', 'JG19'], highCost: ['JH01', 'JH03']};
write(trackingFile, tracking);
console.log(JSON.stringify({sourceRevision: live.revision, summary, ids: cards.map(c => c.id),
  liveFileUnchanged: read(liveFile).revision === live.revision}));
