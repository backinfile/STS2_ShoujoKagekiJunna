const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const here = __dirname, dataDir = path.resolve(here, '../data');
const read = p => JSON.parse(fs.readFileSync(p, 'utf8'));
const write = (p, value) => fs.writeFileSync(p, JSON.stringify(value, null, 2) + '\n');
const batchId = 'junna-fabled-knowledge-20261102-v1';
const seedFile = path.join(here, 'seed.json'), seed = read(seedFile);
assert(!seed.seedBatches.some(b => b.id.startsWith(batchId)), '本轮已经导入，请保留用户后续编辑。');
const live = read(path.join(dataDir, '纯那Mod设计工作台_本地编辑.json'));
const before = structuredClone(live.data), cards = [], groups = [], terms = [], explorations = [];
const commonRules = '知识牌按组奖励与发现，普通生成或复制沿原单张规则。组内所有牌同样具有知识关键词；引用具体关联牌时使用牌名。同组各牌独立升级。免费打出会建立真实出牌事件并处理卡牌类型及箭矢布置，额外结算与重放不会新建实际出牌记录。伤害加成只赋予攻击牌及能造成伤害的箭矢牌。箭矢子类型预设在内部数据，卡面不展示。';
let current;
function begin(root, index, structure, reference, rules, rarity = '稀有') {
  const termId = 'FK' + String(index).padStart(2, '0');
  current = {root, termId, structure, reference, rules, rarity, cardIds: []};
  explorations.push(current);
  terms.push({uid: 'term_' + termId, id: termId, name: structure, kind: '规则',
    definition: rules, template: '', systems: ['partner'], unit: '',
    notes: '知识组结构探索分类，仅用于设计记录。', sources: ['纯那奇闻结构知识牌探索']});
}
function add(id, type, cost, text, upgradedText = text, options = {}) {
  assert(current && !before.cards.some(c => c.id === id) && !cards.some(c => c.id === id), id);
  const keywords = ['知识'];
  const termIds = ['KW_PARTNER', current.termId];
  for (const [word, term] of [['保留', 'KW_RETAIN'], ['消耗', 'KW_EXHAUST'], ['重放', 'KW_REPLAY']])
    if ((text + upgradedText).includes(word)) {termIds.push(term);if (word !== '重放') keywords.push(word);}
  if (type === '箭矢') termIds.push('KW_STAGE');
  if ((text + upgradedText).includes('伤害加成')) termIds.push('KW_BONUS');
  const root = id === current.root;
  const card = {uid: 'fabled_knowledge_' + id, id, name: id, type, system: 'partner',
    rarity: current.rarity, pool: root, cost, upgradedCost: options.upgradedCost ?? cost,
    turns: type === '箭矢' ? options.turns ?? 1 : null,
    upgradedTurns: type === '箭矢' ? options.upgradedTurns ?? options.turns ?? 1 : null,
    ...(type === '箭矢' ? {arrowSubtype: options.arrowSubtype ?? (text.includes('伤害') ? '攻击' : '技能')} : {}),
    target: options.target ?? ((type === '攻击' || type === '箭矢' && text.includes('造成')) ? '敌人' : '自己'),
    text: '知识。' + text, upgradedText: '知识。' + upgradedText,
    keywords, termIds, archetypeIds: ['SYS_partner'], breakthroughIds: [], partnerCardIds: [],
    art: 'defaultPortrait', character: '星见纯那', role: root ? '知识组核心牌' : '知识牌',
    direction: current.structure, draftStatus: options.userSpecified ? '用户指定' : '结构探索候选', draftBatch: batchId,
    naming: {original: '', work: '', url: '', adaptation: options.userSpecified ? '用户给出的新A/B；为避免覆盖既有A/B暂用新编号。' : '名称待定，当前使用设计编号。'},
    design: {classification: '知识组结构探索', structure: current.structure, reference: current.reference,
      rules: commonRules + '\n' + current.rules + (options.rules ? '\n' + options.rules : ''),
      port: options.port || '输出／防御／运转', roleReason: options.roleReason || (root ? '本组奖励入口及结构核心。' : '本组的独立知识牌组件。')},
    notes: (options.userSpecified ? '用户指定的新A/B，既有A/B保留。' : '奇闻结构探索；费用与数值为候选。') + '\n参考结构：' + current.structure + '\n结算约定：' + current.rules + (options.rules ? '\n' + options.rules : '')};
  cards.push(card); current.cardIds.push(id); return card;
}
function end() {
  assert(current.cardIds[0] === current.root);
  const ids = current.cardIds.slice(1);
  groups.push({uid: 'fabled_group_' + current.root, id: 'GROUP_' + current.root, title: current.root,
    mainCardId: current.root, grantedCardIds: ids, grantedCardCounts: Object.fromEntries(ids.map(id => [id, 1]))});
  for (const id of current.cardIds) cards.find(c => c.id === id).partnerCardIds = id === current.root ? ids : [current.root];
}

begin('JK32', 1, '循环减费', '圣者麦迪文／圣杖埃提耶什／圣地卡拉赞',
  '当前条件构成C在场→A免费、A能力生效→B免费、B在场→C免费的环。能力生效指该玩家已安装对应能力，箭矢在场指对应实际布置仍有效。费用在支付时读取。每回合第1张或前2张的计数按实际打出箭矢统计，重放指额外1次执行。');
add('JK32', '能力', 3, '《JK32C》在场时，耗能为0。每回合第1张箭矢牌获得重放1。', '《JK32C》在场时，耗能为0。每回合前2张箭矢牌获得重放1。');
add('JK32B', '箭矢', 3, '《JK32》生效时，耗能为0。造成18点伤害。', '《JK32》生效时，耗能为0。造成24点伤害。');
add('JK32C', '箭矢', 3, '《JK32B》在场时，耗能为0。获得18点格挡。', '《JK32B》在场时，耗能为0。获得24点格挡。', {arrowSubtype: '技能'});
end();

begin('JK33', 2, '使用记录增强', '风行者三姐妹',
  '记录本场战斗实际使用过的牌名，每张只检查另外两种牌。重复使用同一种不增加不同成员数。当前出牌的基础效果与额外次数在本次结算前确定；组内记录跨回合保留。伤害加成选择继续按合法载体筛选。');
add('JK33', '攻击', 1, '造成6点伤害。本场战斗每使用过《JK33B》《JK33C》中的一种牌，重复一次。', '造成8点伤害。本场战斗每使用过《JK33B》《JK33C》中的一种牌，重复一次。');
add('JK33B', '技能', 1, '获得5点格挡。本场战斗每使用过《JK33》《JK33C》中的一种牌，重复一次。', '获得7点格挡。本场战斗每使用过《JK33》《JK33C》中的一种牌，重复一次。');
add('JK33C', '技能', 1, '选择一张牌获得2点伤害加成。本场战斗每使用过《JK33》《JK33B》中的一种牌，重复一次。', '选择一张牌获得3点伤害加成。本场战斗每使用过《JK33》《JK33B》中的一种牌，重复一次。');
end();

begin('JK34', 3, '十牌套组', '时空大盗拉法姆及9张附带牌',
  '本组实际发放10张不同的知识牌。终结条件统一检查本场实际使用过的不同知识牌模型，组外知识也能推进，不区分奖励入口与关联牌；同模型升级和重复使用不另算一种。JK34只有满足9种条件后可打出，消灭效果沿敌人死亡及首领特殊规则结算。巨人的减费同样读取不同知识模型数。终结数值与十张牌的牌组占位作为候选单独评价。');
add('JK34', '技能', 3, '保留。本场战斗使用过9种知识牌后才能打出。消灭所有敌人。消耗。', undefined, {upgradedCost: 2, roleReason: '大量不同知识牌的使用门槛与战斗终结。'});
add('JK34B', '技能', 0, '将弃牌堆中的1张知识牌放入手牌。消耗。', '将弃牌堆中的2张知识牌放入手牌。消耗。');
add('JK34C', '技能', 1, '所有手牌获得1点伤害加成。', '所有手牌获得2点伤害加成。');
add('JK34D', '技能', 1, '你打出的下1张知识牌耗能减少1。', '你打出的下2张知识牌耗能减少1。');
add('JK34E', '箭矢', 1, '从抽牌堆中选择1张知识牌放入手牌。', '从抽牌堆中选择2张知识牌放入手牌。', {arrowSubtype: '技能'});
add('JK34F', '技能', 2, '获得12点格挡。手牌中有其他知识牌时，额外获得6点格挡。', '获得16点格挡。手牌中有其他知识牌时，额外获得8点格挡。');
add('JK34G', '攻击', 2, '对所有敌人造成12点伤害。本场战斗每使用过一种知识牌，伤害增加1。', '对所有敌人造成16点伤害。本场战斗每使用过一种知识牌，伤害增加1。');
add('JK34H', '箭矢', 2, '获得10点格挡。手牌中有知识牌时，重复一次。', '获得14点格挡。手牌中有知识牌时，重复一次。', {arrowSubtype: '技能'});
add('JK34I', '攻击', 3, '造成24点伤害。本场战斗每使用过一种知识牌，耗能减少1。', '造成30点伤害。本场战斗每使用过一种知识牌，耗能减少1。');
add('JK34J', '技能', 2, '所有箭矢立即加速到完成。消耗。', undefined, {upgradedCost: 1});
end();

begin('JK35', 4, '分支取舍', '艾萨拉女士／辛艾萨莉／永恒之井',
  '选择本场实际存在的指定牌名方向，为所选方向的实际牌给予本场重放1修正，移出另一方向的实际牌。移出只影响本场战斗，战后恢复永久牌组。所选箭矢已经在场时同步修改该布置的重放记录，保持原剩余回合数；不重置等待。候选按组内牌名方向处理同名复制，移出时取消未执行的对应布置。');
add('JK35', '技能', 1, '选择《JK35B》或《JK35C》，使其获得重放1，移除另一张牌。消耗。', undefined, {upgradedCost: 0});
add('JK35B', '箭矢', 1, '造成12点伤害。', '造成16点伤害。', {turns: 2});
add('JK35C', '箭矢', 1, '获得8点格挡。', '获得11点格挡。', {arrowSubtype: '技能'});
end();

begin('JK36', 5, '目标培养与继承', '墓地尊主塔兰吉／邦桑迪／赞达拉的惨象',
  '培养修正作用于本场指定牌名JK36B的实际对象，选择重放1或伤害加成4，沿已有修改规则记录。同名生成与复制的继承按既有规则。召回只访问抽牌堆和弃牌堆，不撤回等待中的布置。JK36B每次执行伤害后，把当前重放修正和伤害加成存量分别加给选择的合法手牌，本体存量保留；重放导致的多次执行分别执行此收益。该赋予动作不解释为新的出牌或完成。');
add('JK36', '技能', 1, '将抽牌堆或弃牌堆中的《JK36B》放入手牌。使其获得重放1或4点伤害加成。消耗。', undefined, {upgradedCost: 0});
add('JK36B', '箭矢', 2, '造成10点伤害。将此牌的重放与伤害加成赋予1张手牌。', '造成14点伤害。将此牌的重放与伤害加成赋予1张手牌。', {turns: 2});
add('JK36C', '技能', 1, '获得7点格挡。使《JK36B》获得重放1或4点伤害加成。', '获得10点格挡。使《JK36B》获得重放1或4点伤害加成。');
end();

begin('JK37', 6, '分段完成解锁', '布洛克斯加／阿古斯传送门／塞纳留斯之斧',
  '战斗开始时将实际JK37保存到战斗外暂存区，战后仍恢复永久牌组。JK37B的根生成链保存阶段1至4，前3个阶段每次正常完成后生成下一阶段的实际复制到抽牌堆，并提高该副本的基础伤害4或升级6；继承升级与有效修改，阶段同时加1。第4阶段正常完成后取回暂存的JK37，不生成第5阶段。额外触发与重放只重复伤害，不推进完成阶段。JK37B整批后消耗，已完成对象不重置或重新布置。分支复制保存自身阶段，奖励解锁按战斗记录处理一次。JK37C以实际攻击出牌事件从抽牌堆取对应实际对象，不触发普通抽牌事件。');
add('JK37', '攻击', 0, '保留。战斗开始时移除此牌。造成40点伤害。', '保留。战斗开始时移除此牌。造成56点伤害。');
add('JK37B', '箭矢', 0, '造成4点伤害。完成后，将伤害增加4点的复制放入抽牌堆；第4次完成时改为将《JK37》放入手牌。消耗。', '造成6点伤害。完成后，将伤害增加6点的复制放入抽牌堆；第4次完成时改为将《JK37》放入手牌。消耗。');
add('JK37C', '能力', 1, '打出攻击牌后，将抽牌堆中的《JK37B》放入手牌。', undefined, {upgradedCost: 0});
end();

begin('JK38', 7, '完成接力', '血斗士洛戈什／布罗尔／瓦莉拉',
  '以箭矢正常完成代替随从死亡，完成后的接力监听每个布置仅结算一次，伤害及格挡牌效仍按重放执行。选择手牌中另外两个指定模型的一个实际对象，先施加重放1或伤害加成4修正，再免费实际打出；JK38C则在免费打出建立布置后立即加速到完成。目标按新牌类型选择，形成新的独立布置。接力只访问实际手牌，复制品也按实际对象计数。没有手中后继对象时本次接力结束。');
add('JK38', '箭矢', 2, '造成12点伤害。完成后，使手牌中的1张《JK38B》或《JK38C》获得重放1，并免费打出。', '造成16点伤害。完成后，使手牌中的1张《JK38B》或《JK38C》获得重放1，并免费打出。');
add('JK38B', '箭矢', 2, '对所有敌人造成6点伤害。完成后，使手牌中的1张《JK38》或《JK38C》获得4点伤害加成，并免费打出。', '对所有敌人造成8点伤害。完成后，使手牌中的1张《JK38》或《JK38C》获得4点伤害加成，并免费打出。');
add('JK38C', '箭矢', 2, '造成8点伤害。获得8点格挡。完成后，免费打出手牌中的1张《JK38》或《JK38B》，立即加速到完成。', '造成11点伤害。获得11点格挡。完成后，免费打出手牌中的1张《JK38》或《JK38B》，立即加速到完成。');
end();

begin('JK39', 8, '加成跨载体流转', '高山之王穆拉丁／高山之王的战锤／天神下凡形态',
  'JK39从一个指定模型JK39B的实际对象借入伤害加成存量，清空该来源，执行本牌伤害后将本牌当时存量转回同一实际对象。来源可以在合法牌区及等待中的布置中，修改存量不改变其位置或计数；无实际来源时只执行本牌基础伤害。JK39B正常完成后增加伤害加成2并进入抽牌堆，明确去向覆盖默认弃置及通用回手，消耗仍按已有优先规则处理。JK39C提供本回合伤害计算贡献倍率，不向存量永久乘写；转移中仍只搬运存量。');
add('JK39', '攻击', 2, '将《JK39B》的伤害加成转移到此牌。造成8点伤害。将此牌的伤害加成转回《JK39B》。', '将《JK39B》的伤害加成转移到此牌。造成12点伤害。将此牌的伤害加成转回《JK39B》。');
add('JK39B', '箭矢', 0, '造成6点伤害。完成后获得2点伤害加成，放回抽牌堆。', '造成8点伤害。完成后获得2点伤害加成，放回抽牌堆。');
add('JK39C', '技能', 1, '本回合，《JK39》和《JK39B》的伤害加成按2倍生效。', '本回合，《JK39》和《JK39B》的伤害加成按3倍生效。');
end();

begin('JK40', 9, '牌区窗口', '半兽人迦罗娜／莱恩国王／弑君者',
  '把目标藏在敌方牌库的结构改为己方抽牌堆底。战斗开始时先完成常规初始牌组处理，再将实际JK40B置于抽牌堆底，不重置其他牌的顺序。JK40C按底部连续实际取牌，沿满手规则进入手牌，受等待约束。JK40在执行到条件时读取JK40B是否仍在手牌，移出一个实际目标后执行追加伤害；目标由玩家选择。移出仅本场战斗。打出JK40B可取得防御，但会把它洗回抽牌堆，从而关闭当下的追加伤害窗口。');
add('JK40', '攻击', 2, '造成8点伤害。《JK40B》在手牌中时，将其移出本场战斗，额外造成32点伤害。', '造成12点伤害。《JK40B》在手牌中时，将其移出本场战斗，额外造成40点伤害。');
add('JK40B', '技能', 0, '战斗开始时，置于抽牌堆底。获得6点格挡，将此牌洗入抽牌堆。', '战斗开始时，置于抽牌堆底。获得9点格挡，将此牌洗入抽牌堆。');
add('JK40C', '箭矢', 1, '将抽牌堆底的2张牌放入手牌。', '将抽牌堆底的3张牌放入手牌。', {arrowSubtype: '技能'});
end();

begin('JK41', 10, '双引擎与重建', '碧蓝女王辛达苟萨／碧蓝之王玛里苟斯／碧蓝誓言石',
  '两张箭矢作为少量允许驻场期间生效的例外：JK41与JK41B同时在场时，各自提供知识费用减免与重放增加。条件实时读取，离场后该来源的持续修正停止；布置时已记录的重放按既有规则保留。每个有效实例各提供一次修正，不从其自身持续规则重复读取并乘写。自然推进正常，玩家可以选择尽快完成或利用短暂的重叠窗口。JK41C只从弃牌堆免费实际打出指定模型的实际牌，各模型最多一个，不撤回已有布置。');
add('JK41', '箭矢', 1, '在场期间，《JK41B》在场时，知识牌耗能减少1。完成时，获得1点能量。', '在场期间，《JK41B》在场时，知识牌耗能减少1。完成时，获得2点能量。', {turns: 2, arrowSubtype: '技能'});
add('JK41B', '箭矢', 1, '在场期间，《JK41》在场时，知识牌获得重放1。完成时，造成12点伤害。', '在场期间，《JK41》在场时，知识牌获得重放1。完成时，造成16点伤害。', {turns: 2});
add('JK41C', '技能', 2, '将弃牌堆中的《JK41》和《JK41B》免费打出。消耗。', undefined, {upgradedCost: 1});
end();

begin('JK42', 11, '牌库集中部署', '明日巨匠格尔宾／侏儒光环／梅卡托克的光环',
  '把牌库内不同光环集中部署改为不同知识箭矢集中部署，并把回库入口写在启动工具上。先将实际手牌中的知识箭矢放回抽牌堆，再对当时抽牌堆建立不同模型快照，各选一个实际对象并免费打出。后续过程中生成或复制的新模型留给后续动作，不动态扩展本次快照。每个对象正常选择目标、记录已有重放并建立布置；自然推进仍从下一玩家方回合开始。泛指所有知识箭矢，不限本组三张。');
add('JK42', '技能', 3, '将手牌中的知识箭矢牌放回抽牌堆，然后免费打出抽牌堆中每种知识箭矢牌各1张。消耗。', undefined, {upgradedCost: 2});
add('JK42B', '箭矢', 1, '获得10点格挡。', '获得14点格挡。', {arrowSubtype: '技能'});
add('JK42C', '箭矢', 1, '造成16点伤害。', '造成22点伤害。', {turns: 2});
end();

begin('JK43', 12, '保留终结与战斗减费', '用户指定的新A／B',
  '原有A/B另有设计，本组以JK43及JK43B暂代新A/B，获得时各1张。JK43为攻击牌，基础与升级均5费，保留，每段5点伤害，基础4次、升级5次。JK43B为0费技能，格挡3或升级6，并使本场战斗内同名JK43的实际对象耗能减少1，最低0；升级只提高格挡。减费是战斗内费用修正，不改永久牌组费用，不增加消耗或其他附带限制。', '罕见');
add('JK43', '攻击', 5, '保留。造成5点伤害4次。', '保留。造成5点伤害5次。', {userSpecified: true, port: '输出'});
add('JK43B', '技能', 0, '获得3点格挡。《JK43》在本场战斗内耗能-1。', '获得6点格挡。《JK43》在本场战斗内耗能-1。', {userSpecified: true, port: '防御／运转'});
end();

assert.equal(cards.length, 42);assert.equal(groups.length, 12);
assert.equal(explorations.slice(0, 11).reduce((n, g) => n + g.cardIds.length, 0), 40);
const data = structuredClone(before);data.cards.push(...cards);data.terms.push(...terms);data.partnerGroups.push(...groups);
const app = fs.readFileSync(path.join(here, 'app.js'), 'utf8');
const context = {seed: {data}, clone: structuredClone, uid: () => {throw Error('不应临时补编号');},
  COLLECTIONS: ['mechanisms', 'terms', 'cards', 'partnerGroups', 'archetypes', 'styles'],
  TYPES: ['攻击', '技能', '能力', '箭矢'], RARITIES: ['基础', '普通', '罕见', '稀有', '远古']};
vm.createContext(context);vm.runInContext(app.slice(app.indexOf('  function normalizeData('), app.indexOf('  function patchesBetween(')), context);
const normalized = context.normalizeData(data);
for (const old of before.cards) {
  const found = normalized.cards.find(c => c.id === old.id);
  const allowedRarity = before.partnerGroups.some(g => g.grantedCardIds.includes(old.id)) ? normalized.cards.find(c => c.id === before.partnerGroups.find(g => g.grantedCardIds.includes(old.id)).mainCardId).rarity : old.rarity;
  assert.deepEqual(JSON.parse(JSON.stringify(found)), {...old, rarity: allowedRarity}, '保留旧牌及用户修改 ' + old.id);
}
for (const key of ['mechanisms', 'archetypes', 'styles', 'meta', 'title']) assert.deepEqual(JSON.parse(JSON.stringify(normalized[key])), before[key], '保留 ' + key);
for (const g of normalized.partnerGroups) for (const id of g.grantedCardIds) {
  const c = normalized.cards.find(c => c.id === id), main = normalized.cards.find(c => c.id === g.mainCardId);
  assert.equal(c.rarity, main.rarity);assert.equal(c.pool, false);
}
const batches = [
  {id: batchId + '-terms', label: '知识组结构分类', collection: 'terms', ids: terms.map(t => t.id), entries: terms},
  {id: batchId + '-cards', label: '十一种结构知识牌与用户指定A/B', collection: 'cards', ids: cards.map(c => c.id), entries: cards},
  {id: batchId + '-groups', label: '新增十二组知识关系', collection: 'partnerGroups', ids: groups.map(g => g.id), entries: groups}
];
const summary = {newGroups: groups.length, referenceGroups: 11, userGroups: 1, newCards: cards.length,
  active: normalized.cards.filter(c => !c.trashedAt).length, trash: normalized.cards.filter(c => c.trashedAt).length,
  total: normalized.cards.length, normalizedRelatedRarities: before.cards.filter(c => c.rarity !== normalized.cards.find(n => n.id === c.id).rarity).map(c => c.id)};
write(path.join(dataDir, '纯那奇闻结构知识牌探索.json'), {schemaVersion: 1, id: batchId, date: '2026-11-02', title: '纯那奇闻结构知识牌探索',
  sourceRevision: live.revision, source: 'D:/Github/STS2_Card_Reserch/docs/炉石传说奇闻套组设计结构.json', summary,
  styleSnapshot: before.styles, explorations, cards, groups, terms,
  review: {scope: '按11种参考结构各一组，另加入用户指定的双牌组。',
    adaptation: '死亡接力改为箭矢完成；双方牌库追捕改为己方牌区窗口；十牌组使用知识模型数作为统一门槛；保持原有A/B与用户全部编辑。',
    display: '知识关联牌的稀有度跟随奖励入口，使用相同职业卡框渲染，卡牌类型决定各自框形；入池属性独立。',
    candidateBalance: ['十牌组的战斗终结、免费接力、双引擎和大批部署为本轮强度探索，尚未做战斗模拟。', '各牌升级分开生效，组奖励沿当前约定发放其余基础版本。']}});
write(path.join(here, '.tmp-fabled-before.json'), {revision: live.revision, data: before});
seed.data = JSON.parse(JSON.stringify(normalized));seed.revision = live.revision;
for (const key of ['history', 'undoStack', 'redoStack']) seed[key] = structuredClone(live[key]);
seed.assets = {...seed.assets, ...live.assets};seed.seedBatches.push(...batches);
seed.appliedSeedBatches = [...new Set([...seed.appliedSeedBatches, ...live.appliedSeedBatches, ...batches.map(b => b.id)])];
write(seedFile, seed);
const trackingFile = path.join(dataDir, '纯那职业卡池设计记录.json'), tracking = read(trackingFile);
tracking.stage = '奇闻结构知识组探索';tracking.status = '新增11组参考结构与用户指定双牌组，统一知识关联牌稀有度';
tracking.current = {...summary, source: '纯那Mod设计工作台_本地编辑.json'};
tracking.batches.push({id: batchId, date: '2026-11-02', sourceRevision: live.revision, ...summary, source: '纯那奇闻结构知识牌探索.json'});
write(trackingFile, tracking);
console.log(JSON.stringify({sourceRevision: live.revision, ...summary, userCards: ['JK43', 'JK43B']}));
