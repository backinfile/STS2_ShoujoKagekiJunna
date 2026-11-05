const fs = require('node:fs');
const path = require('node:path');
const roleReview = require('./card-role-review.cjs');
const here = __dirname;
const dataDir = path.resolve(here, '../data');
const seed = JSON.parse(fs.readFileSync(path.join(here, 'seed.json'), 'utf8'));
const batchId = 'junna-first-card-draft-20261101-v1';
const cards = [], groups = [];
const add = (id, system, rarity, type, cost, text, upgradedText, config = {}) => {
  const ids = [...(config.terms || []), ...(config.breakthroughs || [])];
  for (const [word, term] of [['消耗', 'KW_EXHAUST'], ['保留', 'KW_RETAIN'], ['重放', 'KW_REPLAY'], ['伤害加成', 'KW_BONUS']]) if (text.includes(word)) ids.push(term);
  if (type === '星矢') ids.push('KW_STAGE');
  if (text.startsWith('知识。')) ids.push('KW_PARTNER');
  const card = {
    uid: 'draft_card_' + id, id, name: id, system, rarity, type, cost,
    upgradedCost: config.upgradedCost ?? cost,
    turns: type === '星矢' ? config.turns : null,
    upgradedTurns: type === '星矢' ? config.upgradedTurns ?? config.turns : null,
    ...(type === '星矢' ? {arrowSubtype: config.arrowSubtype || '技能'} : {}),
    target: config.target || (type === '攻击' ? '敌人' : '自己'),
    text, upgradedText: upgradedText || text,
    pool: config.pool ?? true,
    keywords: text.startsWith('知识。') ? ['知识'] : [],
    termIds: [...new Set(ids)], archetypeIds: ['SYS_' + system],
    breakthroughIds: config.breakthroughs || [], partnerCardIds: [], art: 'defaultPortrait',
    role: config.role || (config.breakthroughs?.length ? '突破限制牌' : '机制基础牌'),
    direction: config.direction || '',
    naming: {original: '', work: '', url: '', adaptation: '名称待定；当前显示设计编号。'},
    notes: ['本轮初稿；卡名待定。', config.limit && '突破的限制：' + config.limit, config.port && '端口：' + config.port, config.investment && '保留的投入：' + config.investment, config.risk && '复核：' + config.risk, config.reference && '结构参照：' + config.reference].filter(Boolean).join('\n'),
    draftStatus: '候选', draftBatch: batchId,
    design: {limit: config.limit || '', port: config.port || '', investment: config.investment || '', risk: config.risk || '', reference: config.reference || ''}
  };
  cards.push(card);
  return card;
};
const group = (mainId, grantedIds) => {
  groups.push({uid: 'draft_group_' + mainId, id: 'GROUP_' + mainId, title: mainId, mainCardId: mainId, grantedCardIds: grantedIds, grantedCardCounts: Object.fromEntries(grantedIds.map(id => [id, 1]))});
  for (const id of grantedIds) {
    cards.find(card => card.id === mainId).partnerCardIds.push(id);
    cards.find(card => card.id === id).partnerCardIds.push(mainId);
  }
};
const rare = (id, system, type, cost, text, config = {}) => add(id, system, '稀有', type, cost, text, config.upgradedText, config);
const grant = (id, type, cost, text, upgradedText, config = {}) => add(id, 'partner', '附带', type, cost, '知识。' + text, '知识。' + upgradedText, {...config, pool: false, role: '知识牌'});

// 星矢：围绕同一个布置过程，改变等待、费用、来源、结算次数和端口。
rare('JA01', 'stage', '能力', 2, '所有星矢牌获得重放1。', {upgradedCost: 1, breakthroughs: ['SB04'], terms: ['EF43'], direction: '重放与额外触发', limit: '一次完成只执行一份牌效。', port: '输出／防御／运转', investment: '仍支付布置费用、等待倒计时，并维持原目标。', reference: '回响形态的额外执行结构；原候选再演。', risk: '重放与额外触发均不增加完成对象数；同批追加只计算一次。'});
rare('JA02', 'stage', '能力', 3, '所有星矢牌耗能变为0，并获得消耗。', {upgradedCost: 2, breakthroughs: ['SB13'], terms: ['EF66', 'EF27'], direction: '布置与加速', limit: '能量限制同回合的布置数量。', port: '运转', investment: '等待照常，实际卡牌在完成整批牌效后消耗。', reference: '腐化式免费与消耗交换。', risk: '与即时完成、抽牌星矢组合时，用实际卡牌的消耗承担代价；额外触发未到期对象不提前消耗。'});
rare('JA03', 'stage', '能力', 2, '每当你打出一张星矢牌时，其他星矢加速1回合。', {upgradedCost: 1, breakthroughs: ['SB03'], terms: ['TR12', 'EF29'], direction: '布置与加速', limit: '加速工具需要反复抽到与支付。', port: '输出／防御／运转', investment: '需要先有场面，再打出新星矢。', risk: '场内复制与额外触发不算打出；新布置的自身不响应这次推进。', reference: '用户已确认的布置推动既有场面结构。'});
rare('JA04', 'stage', '技能', 1, '本回合，你打出的星矢牌立即加速到完成。消耗。', {upgradedCost: 0, breakthroughs: ['SB01', 'SB11'], terms: ['EF32', 'EF27'], direction: '布置与加速', limit: '本回合的新星矢仍需等待。', port: '输出／防御／运转', investment: '一次性窗口与每张牌正常费用。', risk: '按剩余回合数作一次完整加速；不改写基础初始回合数。', reference: '零号站的一回合即时窗口。'});
rare('JA05', 'stage', '技能', 2, '星矢立即加速到完成。消耗。', {upgradedCost: 1, breakthroughs: ['SB01', 'SB02'], terms: ['EF32', 'EF51', 'EF27'], direction: '布置与加速', limit: '不同倒计时难以在同一回合兑现。', port: '输出／防御／运转', investment: '提前布置的场面与本牌费用。', risk: '处理执行前的有效对象快照；过程中新建的星矢不加入这批。', reference: '整批兑现准备资源的突破模式。'});
rare('JA06', 'stage', '能力', 2, '在你的回合结束时，额外触发一个星矢。', {upgradedCost: 1, breakthroughs: ['SB04'], terms: ['TR03', 'EF33'], direction: '重放与额外触发', limit: '每次额外执行都需要再找到一张工具。', port: '输出／防御／运转', investment: '安装能力并保有有效星矢；原倒计时继续推进。', risk: '额外触发不产生完成事件；选择失败时跳过。', reference: '周期工具；原候选再生赞美曲。'});
rare('JA07', 'stage', '能力', 2, '每当一个星矢完成时，在你的下个回合开始时获得1点能量。', {upgradedCost: 1, breakthroughs: ['SB07'], terms: ['TR28', 'EF09', 'EF10'], direction: '等待过程提供收益', limit: '完成只兑现载荷，无法支付下一轮布置。', port: '运转', investment: '先完成实际布置，能量在下回合到达。', risk: '按完成对象计次，重放与额外触发不重复预约能量。', reference: '完成资源转换为下一轮运转。'});
rare('JA08', 'stage', '能力', 2, '在你的回合开始时，将一张随机星矢牌加入你的手牌。', {upgradedCost: 1, breakthroughs: ['SB05'], terms: ['TR02', 'EF22'], direction: '布置与加速', limit: '每一轮来源都依赖从牌组抽到。', port: '运转', investment: '每回合只在既定时点产生新来源，仍需支付并等待。', risk: '随机池只含普通、罕见、稀有入池星矢；副本为未升级版本。', reference: '创造性AI式持续来源；原候选Circle of the Revue。'});
rare('JA09', 'stage', '星矢', 1, '复制目标的一个星矢。', {upgradedCost: 0, turns: 1, target: '敌人或玩家', breakthroughs: ['SB10'], terms: ['EF21'], direction: '重放与额外触发', limit: '一份布置与投资只有一个实际对象。', port: '输出／防御／运转', investment: '本牌的费用、等待与目标范围。', risk: '复制时选择仍有效且当前计数大于0的对象；新对象不记为打出，当前回合不自动计数。', reference: '场内复制已有投资。'});
rare('JA10', 'stage', '技能', 2, '本回合，下一个完成的星矢额外触发2次。消耗。', {upgradedCost: 1, breakthroughs: ['SB04'], terms: ['EF65', 'EF27'], direction: '重放与额外触发', limit: '关键载荷到期只能兑现基础份数。', port: '输出／防御／运转', investment: '把指定载荷安排在本回合下一次完成的位置。', risk: '同回合来源的追加次数相加；追加执行不再次消耗或复制本规则。', reference: '一次性额外执行窗口；原候选终幕。'});
rare('JA11', 'stage', '能力', 1, '你的星矢牌获得保留。', {upgradedCost: 0, breakthroughs: ['SB14'], terms: ['KW_RETAIN', 'EF26'], direction: '布置与加速', limit: '来源抽到的回合与适合布置的回合不一致。', port: '运转', investment: '占用手牌空间，费用与等待保持原样。', risk: '保留只作用于手牌中的卡牌，不延长场内星矢的倒计时。', reference: '幻影之刃式类型保留。'});
rare('JA12', 'stage', '能力', 2, '每当一个星矢加速1回合时，获得2点格挡。', {upgradedCost: 1, breakthroughs: ['SB07'], terms: ['TR26', 'EF05'], direction: '等待过程提供收益', limit: '延迟投资在兑现前缺乏持续防御。', port: '防御', investment: '需要场面与真实的计数减少。', risk: '自然计数、主动加速与立即完成都按实际减少量计；复制计数与重复牌效不凭空产生推进。', reference: '来源事件覆盖防御端；原候选闪耀所在。'});

// 知识：固定取得关系保持不变，突破发生在抵达、调度和兑现环节。
rare('JK01', 'partner', '能力', 3, '知识。在你的回合开始时，不论何处，将一张《JK01B》放入你的手牌，其本回合耗能变为0。', {upgradedCost: 2, breakthroughs: ['RB01', 'RB04'], terms: ['KW_PARTNER', 'EF18', 'EF48'], direction: '知识牌反复抵达', limit: '组件已获得却未到手，且每轮都要支付工具费用。', port: '输出／运转', investment: '3费安装；固定一张《JK01B》，不制造新副本。', risk: '召回实际《JK01B》，保留原有召回范围。', reference: '持续定向工具；原候选星祭之夜。'});
grant('JK01B', '攻击', 2, '造成12点伤害。目标敌人的一个星矢加速1回合。', '造成16点伤害。目标敌人的一个星矢加速1回合。', {terms: ['EF01', 'EF29'], target: '敌人', port: '输出／运转'});
group('JK01', ['JK01B']);
rare('JK02', 'partner', '能力', 2, '知识。每当《JK02B》进入你的手牌时，将其升级，其本回合耗能减少1。', {upgradedCost: 1, breakthroughs: ['RB13'], terms: ['KW_PARTNER', 'EF24', 'EF47'], direction: '知识牌扩大收益', limit: '工具到手仍需另找升级与费用支持。', port: '输出／运转', investment: '需要真实进入手牌；工具仍有剩余费用。', risk: '升级作用于实际卡；永久获得知识牌仍按基础规则发放未升级知识牌。', reference: '抵达收益；原候选舞台准备。'});
grant('JK02B', '攻击', 2, '造成16点伤害。', '造成22点伤害。', {terms: ['EF01'], target: '敌人', port: '输出'});
group('JK02', ['JK02B']);
rare('JK03', 'partner', '能力', 2, '知识。你的《JK03B》获得重放1。', {upgradedCost: 1, breakthroughs: ['RB08'], terms: ['KW_PARTNER', 'EF43'], direction: '知识牌扩大收益', limit: '指定工具每轮只有基础的一份结算。', port: '防御／运转', investment: '取得、布置并等待自己的关联星矢。', risk: '重放属于同一布置的同批牌效，整批只处理一次去向。', reference: '固定工具追加执行；原候选那一天。'});
grant('JK03B', '星矢', 1, '获得6点格挡。抽1张牌。', '获得9点格挡。抽1张牌。', {turns: 1, terms: ['EF05', 'EF06'], port: '防御／运转'});
group('JK03', ['JK03B']);
rare('JK04', 'partner', '能力', 2, '知识。每当你打出《JK04B》时，你的格挡在下个回合开始时不会消失。', {upgradedCost: 1, breakthroughs: ['RB12'], terms: ['KW_PARTNER', 'EF61'], direction: '知识牌扩大收益', limit: '本回合积累的格挡在回合边界清空。', port: '防御', investment: '安装此能力，并在需要保留格挡的回合实际打出《JK04B》。', risk: '只保留下一个回合开始的格挡；不是永久壁垒。', reference: '固定知识动作赋予格挡保留；原候选香蕉蛋糕。'});
grant('JK04B', '技能', 1, '获得8点格挡。保留。', '获得11点格挡。保留。', {terms: ['EF05', 'KW_RETAIN'], port: '防御'});
group('JK04', ['JK04B']);
rare('JK05', 'partner', '技能', 2, '将你抽牌堆、弃牌堆和消耗牌堆中的所有知识牌放入手牌。消耗。', {upgradedCost: 1, breakthroughs: ['RB02', 'RB07'], terms: ['EF19', 'EF27'], direction: '知识牌互相调度', limit: '调度一次只能找到一张，已消耗的工具离开循环。', port: '运转', investment: '一次性调度费用、手牌上限与各工具自己的费用。', risk: '不移动场内等待中的星矢，也不选择执行中的牌；满手按现有规则处理。', reference: '批量召回实际工具。'});
rare('JK06', 'partner', '能力', 2, '每当你打出一张知识牌时，同组的其他知识牌本回合耗能减少1。', {upgradedCost: 1, breakthroughs: ['RB14'], terms: ['EF47'], direction: '知识牌互相调度', limit: '同时拿到组件仍要分别支付整套费用。', port: '运转', investment: '先支付并打出知识牌，减费只服务它的关联模型。', risk: '费用下限为0；不改永久费用，不让其他无关知识组减费。', reference: '知识牌行动为知识牌支付一部分成本。'});
rare('JK07', 'partner', '能力', 2, '每当你打出一张知识牌时，将你弃牌堆中一张同组的其他知识牌放入手牌。', {upgradedCost: 1, breakthroughs: ['RB15'], terms: ['EF15'], direction: '知识牌互相调度', limit: '取回只朝工具单向运作，知识牌用后仍要等待。', port: '运转', investment: '先打出工具；知识牌仍需支付，不召回已安装的能力。', risk: '只查弃牌堆中的实际同组知识牌；执行中的知识牌不参与。', reference: '把固定关系接成双向调度，保留每次行动费用。'});
rare('JK08', 'partner', '能力', 2, '在你的回合开始时，每有2张知识牌在你的手牌中，获得1点能量。', {upgradedCost: 1, breakthroughs: ['RB16'], terms: ['TR02', 'EF09'], direction: '知识牌反复抵达', limit: '聚齐组件只消耗手牌空间，没有启动资源回报。', port: '运转', investment: '在回合开始保有实际工具；计数按张、向下取整。', risk: '在常规抽牌后的既定能力时点读取手牌；本回合稍后回手不追溯补能量。', reference: '从组件抵达与保留中取得下一轮能量。'});
rare('JK09', 'partner', '技能', 1, '选择你手牌中的一张知识牌。将2张它的复制品加入你的手牌。消耗。', {upgradedCost: 0, breakthroughs: ['RB08'], terms: ['EF21', 'EF27'], direction: '知识牌扩大收益', limit: '固定取得关系只交付有限数量的工具。', port: '输出／防御／运转', investment: '原工具必须在手，复制品仍需支付。', risk: '复制实际牌的有效修改；不触发永久知识发放，满手照常处理。', reference: '有限副本扩大同回合行动；实际复制与生成分别结算。'});
rare('JK10', 'partner', '能力', 2, '知识。每当你用《JK10B》击杀敌人时，将其放回手牌，其本回合耗能变为0。', {upgradedCost: 1, breakthroughs: ['RB05'], terms: ['KW_PARTNER', 'EF18', 'EF48'], direction: '知识牌扩大收益', limit: '输出工具每轮只有一次抵达机会。', port: '输出／运转', investment: '第一次支付与击杀条件，接续次数受存活敌人数限制。', risk: '整次打出完成后回手；未击杀即停止免费接续。', reference: '击杀接续；原候选wi(l)d-screen baroque。'});
grant('JK10B', '攻击', 2, '造成18点伤害。保留。', '造成24点伤害。保留。', {terms: ['EF01', 'KW_RETAIN'], target: '敌人', port: '输出'});
group('JK10', ['JK10B']);

// 伤害加成：维持输出小体系；增量、存量、贡献倍率与命中次数分别处理。
rare('JG01', 'growth', '技能', 1, '将你手牌中一张攻击牌或能造成伤害的星矢牌在本场战斗中获得的伤害加成变为3倍。消耗。', {upgradedCost: 0, breakthroughs: ['GB11'], terms: ['EF41', 'EF27'], direction: '集中投资单张攻击', limit: '已有投资只按固定加值线性积累。', port: '输出', investment: '先把实际攻击养大，再使用一次性倍率。', risk: '只设置已存伤害加成；不把倍率结果当作新获得事件。', reference: '触媒式已有存量倍率；原候选再生产。'});
rare('JG02', 'growth', '能力', 2, '本场战斗中，你的攻击牌和能造成伤害的星矢牌每次获得的伤害加成变为2倍。', {upgradedCost: 1, breakthroughs: ['GB08'], terms: ['EF45'], direction: '集中投资单张攻击', limit: '后续每次投入只产生原始增量。', port: '输出', investment: '还需抓增伤来源与实际攻击载体。', risk: '固定额外量先相加，再应用倍率；存量设置、复制继承不再翻倍。', reference: '投资效率倍率；原候选情热。'});
rare('JG03', 'growth', '技能', 2, '选择你手牌中的一张攻击牌。本场战斗中，该牌每获得一次伤害加成，伤害次数就增加1。消耗。', {upgradedCost: 1, breakthroughs: ['GB13'], terms: ['EF69', 'EF27'], direction: '改变伤害加成的兑现方式', limit: '单张攻击的伤害次数固定。', port: '输出', investment: '选定实际载体，再反复制造真实的新增增伤事件。', risk: '设置存量、继承或复制不增加次数；仅增加该牌伤害指令的次数。', reference: '从投资事件扩展命中次数；原候选Star Divine。'});
rare('JG04', 'growth', '技能', 2, '选择你手牌中的一张攻击牌，将其复制品加入手牌。本场战斗中，新生成的同名牌继承它的伤害加成。消耗。', {upgradedCost: 1, breakthroughs: ['GB03', 'GB07'], terms: ['EF21', 'EF46', 'EF27'], direction: '扩大投资与继承范围', limit: '后生成的同名牌从0开始，成熟载体只有一份。', port: '输出', investment: '模板先有投资，新对象仍需抵达和支付。', risk: '生成时读取模板当时的加成；实际复制已继承的部分不重复叠加。', reference: '模板继承；原候选Star Diamond。'});
rare('JG05', 'growth', '能力', 3, '你改变伤害加成的效果，改为作用于本场战斗中的所有攻击牌和能造成伤害的星矢牌。', {upgradedCost: 2, breakthroughs: ['GB01', 'GB02'], terms: ['EF52'], direction: '扩大投资与继承范围', limit: '一份投资只覆盖同手的一个对象。', port: '输出', investment: '先安装高费能力，再提供正常增伤来源。', risk: '目标集合去重；附加目标不递归传播。星矢仍按独立类型筛选。', reference: '共享投资范围；原候选星之对话。'});
rare('JG06', 'growth', '攻击', 1, '造成6点伤害。计算伤害时，此牌在本场战斗中获得的伤害加成变为3倍。', {upgradedText: '造成9点伤害。计算伤害时，此牌在本场战斗中获得的伤害加成变为3倍。', breakthroughs: ['GB08'], terms: ['EF01', 'EF70'], direction: '改变伤害加成的兑现方式', limit: '已有伤害加成只按一份计入每段伤害。', port: '输出', investment: '该实际攻击需要先获得投资并实际打出。', risk: '只放大存储加成的贡献，不改基础伤害、力量或存量。', reference: '重刃式按特定资源提高伤害贡献；原候选合刃。'});
rare('JG07', 'growth', '攻击', 2, '造成10点伤害。其他敌人失去等同于此牌在本场战斗中获得的伤害加成的生命值。', {upgradedCost: 1, breakthroughs: ['GB14'], terms: ['EF01', 'EF68'], direction: '改变伤害加成的兑现方式', limit: '单体投资只通过原攻击伤害兑现。', port: '输出', investment: '成熟的实际攻击、打出费用与其他存活敌人。', risk: '独立生命流失不是伤害段，不吃力量或易伤，也不被伤害次数增加复制。', reference: '存量开启其他敌人的独立出口；原候选散刃。'});
rare('JG08', 'growth', '能力', 2, '在你的回合开始时，选择你任意牌区的一张攻击牌或能造成伤害的星矢牌，使其在本场战斗中获得3点伤害加成。', {upgradedCost: 1, breakthroughs: ['GB01', 'GB12'], terms: ['TR02', 'EF39'], direction: '集中投资单张攻击', limit: '每轮必须重新抽到增伤工具，且投资要求同手。', port: '输出', investment: '一次性安装成本与后续战斗回合。', risk: '选择实际存在的攻击实例；不修改永久牌组，也不把星矢作为攻击。', reference: '跨牌区周期投资；原候选Top Star。'});

// 少量基础关系牌：给上面的候选提供可观察的来源、工具和收益。
add('JA13', 'stage', '普通', '星矢', 1, '造成14点伤害。', '造成19点伤害。', {turns: 2, arrowSubtype: '攻击', target: '敌人', terms: ['EF01'], role: '机制基础牌', direction: '重放与额外触发', port: '输出', investment: '支付1能量并等待2回合。'});
add('JA14', 'stage', '普通', '星矢', 1, '获得8点格挡。', '获得11点格挡。', {turns: 1, terms: ['EF05'], role: '机制基础牌', direction: '等待过程提供收益', port: '防御', risk: '格挡在完成时给予记录目标玩家。'});
add('JA15', 'stage', '普通', '星矢', 1, '抽2张牌。', '抽3张牌。', {turns: 1, terms: ['EF06'], role: '机制基础牌', direction: '布置与加速', port: '运转', risk: '本轮唯一入池非稀有抽牌来源；抽牌在正常完成或明确额外触发时发生。'});
add('JA16', 'stage', '普通', '技能', 1, '获得6点格挡。一个星矢加速1回合。', '获得9点格挡。一个星矢加速1回合。', {terms: ['EF05', 'EF29'], role: '机制引导牌', direction: '布置与加速', port: '防御／运转', investment: '正常付费，同时解决当前防御与一份场面推进。'});
add('JA17', 'stage', '罕见', '攻击', 1, '造成6点伤害。额外触发目标的一个星矢。', '造成9点伤害。额外触发目标的一个星矢。', {terms: ['EF01', 'EF33'], role: '机制中坚牌', direction: '重放与额外触发', port: '输出／防御／运转', risk: '额外触发只选择与该敌人对应的有效星矢；原倒计时保持。'});
add('JA18', 'stage', '罕见', '攻击', 1, '造成6点伤害。每有一个星矢，多造成2点伤害。', '造成9点伤害。每有一个星矢，多造成2点伤害。', {terms: ['EF01', 'EF40'], role: '机制引导牌', direction: '等待过程提供收益', port: '输出', risk: '在执行该指令时读取共享场面数量；基础伤害仍可单独使用。'});

add('JK11', 'partner', '普通', '技能', 1, '知识。获得6点格挡。将你弃牌堆中的一张《JK11B》放回手牌。', '知识。获得9点格挡。将你弃牌堆中的一张《JK11B》放回手牌。', {terms: ['EF05', 'EF15'], role: '机制引导牌', direction: '知识牌互相调度', port: '防御／运转', risk: '没有可取回对象时仍获得基础格挡。'});
grant('JK11B', '攻击', 1, '造成8点伤害。', '造成11点伤害。', {terms: ['EF01'], target: '敌人', port: '输出'});
group('JK11', ['JK11B']);
add('JK12', 'partner', '普通', '攻击', 1, '知识。造成7点伤害。使你手牌中的一张《JK12B》在本场战斗中获得2点伤害加成。', '知识。造成10点伤害。使你手牌中的一张《JK12B》在本场战斗中获得2点伤害加成。', {terms: ['EF01', 'EF39'], role: '机制引导牌', direction: '知识牌扩大收益', port: '输出', risk: '没有《JK12B》同手时仍有基础伤害；投资只改实际选择对象。'});
grant('JK12B', '攻击', 1, '造成3点伤害3次。保留。', '造成4点伤害3次。保留。', {terms: ['EF02', 'KW_RETAIN'], target: '敌人', port: '输出'});
group('JK12', ['JK12B']);
add('JK13', 'partner', '罕见', '能力', 1, '每当你打出一张知识牌时，获得3点格挡。', '每当你打出一张知识牌时，获得4点格挡。', {terms: ['TR14', 'EF05'], role: '机制核心牌', direction: '知识牌互相调度', port: '防御', risk: '按“知识”关键词判断知识牌，包括同名临时生成与复制的牌。'});
add('JK14', 'partner', '罕见', '技能', 1, '获得6点格挡。将你弃牌堆中的一张知识牌放到抽牌堆顶部。', '获得9点格挡。将你弃牌堆中的一张知识牌放到抽牌堆顶部。', {terms: ['EF05', 'EF20'], role: '机制中坚牌', direction: '知识牌反复抵达', port: '防御／运转', risk: '调整后续普通抽牌的抵达；本动作不算抽牌，也不直接把牌送入手牌。'});

add('JG09', 'growth', '普通', '技能', 1, '获得7点格挡。使你手牌中的一张攻击牌或能造成伤害的星矢牌在本场战斗中获得2点伤害加成。', '获得7点格挡。使你手牌中的一张攻击牌或能造成伤害的星矢牌在本场战斗中获得3点伤害加成。', {terms: ['EF05', 'EF39'], role: '机制引导牌', direction: '集中投资单张攻击', port: '防御／输出', risk: '普通攻防兼投资，不按突破牌计数。'});
add('JG10', 'growth', '普通', '攻击', 1, '造成6点伤害。此牌在本场战斗中获得2点伤害加成。', '造成9点伤害。此牌在本场战斗中获得2点伤害加成。', {terms: ['EF01', 'EF39'], role: '机制基础牌', direction: '集中投资单张攻击', port: '输出', risk: '按文字顺序先造成伤害、再获得加成；新加成供后续使用。'});
add('JG11', 'growth', '罕见', '星矢', 1, '使你手牌中的所有攻击牌和能造成伤害的星矢牌在本场战斗中获得2点伤害加成。', '使你手牌中的所有攻击牌和能造成伤害的星矢牌在本场战斗中获得3点伤害加成。', {turns: 1, terms: ['EF39', 'EF51'], role: '机制基础牌', direction: '扩大投资与继承范围', port: '输出', risk: '星矢完成时读取记录玩家当时的攻击手牌；星矢自身不计入攻击集合。'});
add('JG12', 'growth', '罕见', '攻击', 1, '造成3点伤害3次。', '造成4点伤害3次。', {terms: ['EF02'], role: '机制引导牌', direction: '改变伤害加成的兑现方式', port: '输出', risk: '每段分别承接已有伤害加成；单独抓取仍提供基础多段输出。'});

const extraTerms = [
  {id: 'SB14', name: '手牌保留', definition: '原限制：星矢来源在抽到的回合未必适合布置。为指定类型赋予保留，等待支付与布置时机。', template: '你的星矢牌获得保留。', systems: ['stage']},
  {id: 'RB14', name: '关联费用', definition: '原限制：一组牌取得后仍需分别支付。打出知识牌后降低同组其他知识牌本回合费用。', template: '每当你打出一张知识牌时，同组的其他知识牌本回合耗能减少N。', systems: ['partner']},
  {id: 'RB15', name: '关联方向', definition: '原限制：知识牌调度工具，知识牌自身仍依赖正常循环。通过知识牌的打出反向接回实际同组知识牌。', template: '每当你打出一张知识牌时，将你弃牌堆中一张同组的其他知识牌放入手牌。', systems: ['partner']},
  {id: 'RB16', name: '抵达转能量', definition: '原限制：工具聚齐只占用手牌空间。将回合开始时实际在手的知识牌数量转成能量。', template: '在你的回合开始时，每有N张知识牌在你的手牌中，获得M点能量。', systems: ['partner']}
].filter(term => !roleReview.removedTermIds.includes(term.id)).map(term => ({...term, uid: 'term_' + term.id, kind: '突破', notes: '', sources: ['纯那首轮牌稿'], unit: ''}));
const breakthroughTermIds = new Set([...seed.data.terms,...extraTerms].filter(term=>term.kind==='突破').map(term=>term.id));
for (const card of cards) roleReview.reviewCard(card, breakthroughTermIds);
const knownTerms = new Set([...seed.data.terms, ...extraTerms].map(term => term.id));
for (const card of cards) {
  for (const id of card.termIds) if (!knownTerms.has(id)) throw Error('未知词条 ' + id + ' / ' + card.id);
}
const draft = {
  schemaVersion: 1, id: batchId, date: '2026-11-01', title: '纯那体系突破候选与基本关系牌',
  cards, partnerGroups: groups, terms: extraTerms,
  recommendations: roleReview.recommendations,
  review: {
    classification: roleReview.criterion,
    loops: ['JA02免费同时给予消耗，按既有整批完成后消耗规则处理。', 'JA07收益推迟到下个回合，避免完成立即自供能。', 'JK07只接回弃牌堆中的知识牌，知识牌仍需支付。', 'JK10只在击杀后接续，数量受敌人限制。', 'JG05目标集合去重；倍率设置与继承不触发JG03的真实新增事件。'],
    tradeoffs: ['JA01与JA10分别为长期重复与一次性集中爆发候选，定稿时比较卡位。', 'JA03加快离场，JA12与JA18奖励等待中的场面；构筑需要决定何时兑现。', 'JK01降低固定工具的使用成本，JK07改善可重复知识牌的抵达；两者服务不同结构。', 'JG01消耗已有成熟投资，JG02提高未来投入效率，JG06只改变兑现贡献。'],
    deferred: ['费用与升级数值为纸面初稿，尚未进行实战调平。', '本轮入池非稀有抽牌星矢只有JA15，完整卡池阶段再校准抽牌数量。', '定位复核后保留12张规则突破候选，其余18张改为常规来源、触发、收益或工具。', '单卡原作名称与出处待单独核对。']
  }
};
fs.writeFileSync(path.join(dataDir, '纯那首轮卡牌候选.json'), JSON.stringify(draft, null, 2) + '\n');
const trackingPath = path.join(dataDir, '纯那职业卡池设计记录.json');
const tracking = JSON.parse(fs.readFileSync(trackingPath, 'utf8'));
const summary = {id: batchId, breakthroughs: cards.filter(card => card.breakthroughIds.length).length, basics: cards.filter(card => card.pool && !card.breakthroughIds.length).length, grants: cards.filter(card => !card.pool).length, total: cards.length, source: '纯那首轮卡牌候选.json', recommendations: draft.recommendations, review: draft.review};
tracking.batches = [...tracking.batches.filter(batch => batch.id !== batchId), summary];
tracking.status = '首轮候选已形成，待逐张评审';
fs.writeFileSync(trackingPath, JSON.stringify(tracking, null, 2) + '\n');
console.log(JSON.stringify(summary));
