const retained = {
  JA01: '整类星矢的正常完成结算增加完整牌效，改变全局结算次数规则。',
  JA02: '将整类星矢的能量支付改为消耗实际卡牌，改变布置的费用约束。',
  JA04: '在一个完整回合内，使之后打出的星矢跳过等待窗口。',
  JA11: '使整类星矢跨回合保留，改变未打出牌在回合结束时离手的规则。',
  JK01: '每轮从任意位置调用固定实际工具并设为0费，改变调用区域与支付约束。',
  JK04: '通过打出《JK04B》保留已有格挡，改变下个回合开始时格挡清空的规则。',
  JK05: '一次召回多个牌区的全部知识牌，并包含消耗牌堆。',
  JK10: '击杀后将实际工具回手并设为0费，开启有条件的免费连续使用。',
  JG01: '直接乘算已有伤害加成存量，使成熟投资具备乘法增长方式。',
  JG03: '让后续伤害加成同时扩展伤害次数，改变载体固定的结算段数。',
  JG04: '为之后生成的同名牌建立继承规则，改变新对象从原始加成开始的规则。',
  JG05: '将每个伤害加成效果的作用对象统一扩展至本场全部攻击牌。'
};
const demoted = {
  JA03: ['机制核心牌', '高频触发', '打出星矢时调用既有加速动作，是持续加速触发器。'],
  JA05: ['机制中坚牌', '集中兑现', '对当前场面批量使用既有立即完成动作，是一次性兑现工具。'],
  JA06: ['机制核心牌', '周期触发', '每回合提供一次既有额外触发动作，是周期触发器。'],
  JA07: ['机制核心牌', '事件收益', '将星矢完成事件兑换为能量，是事件收益牌。'],
  JA08: ['机制基础牌', '持续来源', '每轮提供一张随机星矢，是持续来源牌。'],
  JA09: ['机制中坚牌', '复制工具', '使用既有复制动作增加一个布置，是复制工具。'],
  JA10: ['机制中坚牌', '爆发收益', '为下次完成追加两次既有牌效，是一次性爆发收益。'],
  JA12: ['机制核心牌', '事件收益', '按实际加速量提供格挡，是加速事件的防御收益牌。'],
  JK02: ['机制核心牌', '抵达收益', '指定工具进入手牌后得到升级与减费，是抵达收益牌。'],
  JK03: ['机制核心牌', '知识牌强化', '增加《JK03B》的执行收益，是指定牌强化。'],
  JK06: ['机制核心牌', '事件收益', '打出知识牌后为同组其他知识牌减费，是打出事件的费用收益。'],
  JK07: ['机制核心牌', '高频触发', '打出知识牌时调用既有弃牌堆回手动作，是回收触发器。'],
  JK08: ['机制核心牌', '手牌收益', '按回合开始时在手工具数量提供能量，是手牌数量收益。'],
  JK09: ['机制中坚牌', '复制工具', '使用既有实际卡牌复制动作增加两个工具，是复制工具。'],
  JG02: ['机制核心牌', '增量收益', '把后续每次获得的加成按固定倍率放大，是投入效率收益。'],
  JG06: ['机制核心牌', '伤害兑现', '只提高本牌对伤害加成的伤害换算，是输出收益载体。'],
  JG07: ['机制中坚牌', '群体收益', '将已有伤害加成兑现为其他敌人的生命流失，是群体输出牌。'],
  JG08: ['机制基础牌', '持续来源', '每轮选取一张实际攻击提供加成，是可选牌区的持续来源。']
};
const recommendations = {stage:['JA01','JA02','JA04','JA11'],partner:['JK01','JK04','JK05','JK10'],growth:['JG01','JG03','JG04','JG05']};
const criterion = '先写出原有规则与该牌造成的例外，再判断突破。周期来源、高频触发、事件收益、普通复制与单牌伤害换算按实际功能分类。';
const removedTermIds = ['RB14','RB15','RB16'];

function reviewCard(card, breakthroughIds) {
  const reason = retained[card.id] || demoted[card.id]?.[2];
  if (!reason) return card;
  const keep = Boolean(retained[card.id]);
  card.role = keep ? '突破限制牌' : demoted[card.id][0];
  if (!keep) {
    card.termIds = card.termIds.filter(id => !breakthroughIds.has(id) && !removedTermIds.includes(id));
    card.breakthroughIds = [];
  }
  card.design = {...card.design, classification:keep?'规则突破':demoted[card.id][1], roleReason:reason};
  delete card.design.limit;
  if (keep) card.design.limit = reason;
  const marker = keep ? '突破的限制：' : '定位复核：';
  const lines = String(card.notes || '').split('\n').filter(line => !/^(突破的限制|定位复核)：/.test(line));
  lines.splice(Math.min(1,lines.length),0,marker+reason);
  card.notes = lines.join('\n');
  if (card.id === 'JA07' && !card.text.includes('下个回合')) {
    card.design.investment = '先布置并完成实际星矢，按当前牌面取得当回合能量收益。';
    card.design.risk = '按实际完成对象计数；免费布置、立即完成与抽牌组合需要复核当回合自供能循环。';
    card.notes = card.notes.replace(/^保留的投入：.*$/m,'保留的投入：'+card.design.investment).replace(/^复核：.*$/m,'复核：'+card.design.risk);
  }
  return card;
}
module.exports = {retained,demoted,recommendations,criterion,removedTermIds,reviewCard};
