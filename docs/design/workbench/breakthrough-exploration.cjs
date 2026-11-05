const fs = require('node:fs');
const path = require('node:path');
const here = __dirname, dataDir = path.resolve(here, '../data');
const read = file => JSON.parse(fs.readFileSync(file, 'utf8'));
const write = (file, value) => fs.writeFileSync(file, JSON.stringify(value, null, 2) + '\n');
const seed = read(path.join(here, 'seed.json'));
const live = read(path.join(dataDir, '纯那Mod设计工作台_本地编辑.json'));
const batchId = 'junna-breakthrough-exploration-20261101-v1';
const cards = [], partnerGroups = [];
const demoIds = ['JA13', 'JA15', 'JA16', 'JA17', 'JK11', 'JK13', 'JG09'];
const terms = [
  ['SB15','类型边界','星矢原本是独立类型；明确赋予攻击身份，改变类型筛选的适用范围。'],
  ['SB16','完成去向','正常完成后的弃牌去向改为抽牌堆顶；重新打出仍需支付并重新计时。'],
  ['SB17','消耗再用','允许从消耗牌堆直接打出已消耗星矢，另外支付实际生命值。'],
  ['SB18','主动操作','建立不依赖手牌工具的付费加速操作；每次操作消耗真实能量。'],
  ['RB17','打出复制','打出知识牌时，为抽牌堆增加一张该牌的复制牌。'],
  ['RB18','整组修改','将作用于一张知识牌的修改改为整组施加，保留原修改的持续时间。'],
  ['RB19','关联范围','将任意实际手牌临时加入所选知识牌所在的牌组，扩展固定组别。'],
  ['RB20','手牌容量','知识牌不占用手牌上限；涉及牌数的其他读取仍按实际张数。'],
  ['GB15','永久转化','把本场伤害加成转入永久基础伤害，随后清空该份伤害加成。'],
  ['GB16','伤害性质','把伤害加成分量从普通伤害改为生命流失，原伤害中移除相同分量。'],
  ['GB17','资源用途','消耗已有伤害加成换取实际攻击牌在本场战斗中的费用降低。'],
  ['GB18','存量转移','在实际攻击之间重新分配已有存量，总量守恒，转移与新获得分别处理。']
].map(([id,name,definition])=>({uid:'term_'+id,id,name,kind:'突破',definition,template:'',systems:[id.startsWith('SB')?'stage':id.startsWith('RB')?'partner':'growth'],notes:'第二轮规则突破探索。',sources:['纯那第二轮突破探索'],unit:''}));

function add(id, system, type, cost, text, category, oldRule, exception, investment, rules, extra={}) {
  const tags = [category, ...(extra.termIds||[])];
  if (system==='stage') tags.push('KW_STAGE');
  if (system==='partner') tags.push('KW_PARTNER');
  if (text.includes('伤害加成')) tags.push('KW_BONUS');
  if (text.endsWith('消耗。')) tags.push('KW_EXHAUST');
  const card = {
    uid:'draft_card_'+id,id,name:id,system,type,rarity:'稀有',pool:true,
    cost,upgradedCost:extra.upgradedCost??Math.max(0,cost-1),text,upgradedText:text,
    turns:null,upgradedTurns:null,target:extra.target||'自己',
    role:'突破限制牌',direction:extra.direction||terms.find(t=>t.id===category)?.name||exception,
    keywords:[],termIds:[...new Set(tags)],breakthroughIds:[category],archetypeIds:['SYS_'+system],partnerCardIds:[],art:'defaultPortrait',
    naming:{original:'',work:'',url:'',adaptation:'名称待定；当前显示设计编号。'},
    draftStatus:'探索候选',draftBatch:batchId,
    design:{classification:'规则突破',oldRule,exception,investment,rules,roleReason:oldRule+' → '+exception,criterion:'明确改写既有规则；按原资源与原动作反复触发的牌另作常规牌。'},
    notes:['第二轮探索；卡名待定。','原有限制：'+oldRule,'规则变化：'+exception,'保留的投入：'+investment,'复核：'+rules].join('\n')
  };
  cards.push(card); return card;
}

add('JA19','stage','能力',2,'你可以查看抽牌堆顶牌，并直接打出牌堆顶的星矢牌。','SB05',
  '星矢需要先进入手牌，才可主动打出。','允许查看牌堆顶，并从牌堆顶直接打出星矢。',
  '正常支付费用、选择目标和建立倒计时；顶牌不是星矢时需要其他动作移走它。',
  '这是玩家主动打出，计一次出牌；不会先移入手牌，不触发进入手牌事件。', {termIds:['OB10','EF51','RL07'],direction:'打出位置'});
add('JA20','stage','能力',2,'你的星矢牌不消耗能量，改为失去其耗能3倍的生命值。','SB13',
  '正常布置星矢占用当回合能量。','用实际生命值替代星矢的能量支付。',
  '每次布置实际失去生命，倒计时与目标维持原规则。',
  '按其他费用修正生效后的耗能计算，替换支付方式并不把耗能字段设为0。X费按本次选定X计算。', {termIds:['EF49','EF68'],direction:'支付方式'});
add('JA21','stage','能力',2,'星矢牌也视为攻击牌。','SB15',
  '星矢独立于攻击类型，攻击筛选与攻击牌增伤通常不覆盖星矢。','在保留星矢身份的同时赋予攻击身份。',
  '仍需正常布置和等待；不含伤害指令的星矢不会凭空造成伤害。',
  '攻击打出事件只在布置时发生一次，完成和重放都不算再次打出。攻击来源的类型筛选可以选择星矢。', {termIds:['RL07','RL22'],direction:'类型边界'});
add('JA22','stage','能力',2,'不带有消耗的星矢完成后，改为放到抽牌堆顶部。','SB16',
  '正常完成的星矢进入弃牌堆，需要经历牌组循环。','把正常弃置去向替换为抽牌堆顶。',
  '整批牌效结算后才移动；再次打出需重新支付并建立新的倒计时。',
  '带消耗的星矢继续按消耗规则处理。牌效中的抽牌先于本牌置顶，无法在本次结算中抽回自身。', {termIds:['EF20','RL09'],direction:'完成去向'});
add('JA23','stage','能力',2,'你可以从消耗牌堆打出星矢牌。每次这样打出时，失去3点生命值。','SB17',
  '已消耗的星矢离开普通可打出区域。','开放从消耗牌堆主动打出的权限。',
  '正常能量费用外，每次另失去3点生命值，并重新等待。',
  '只选择已在消耗牌堆中的实际牌。新布置不保留旧计数；这3点生命值不随耗能归0而免除。', {termIds:['OB12','EF51','EF68','RL11'],direction:'消耗再用'});
add('JA24','stage','能力',1,'你可以花费1点能量，使一个星矢加速1回合。','SB18',
  '主动加速需要通过手中的牌效或已存在的事件触发器执行。','增加一个可主动支付能量的加速操作。',
  '每次操作真实花费1点能量；还需要有效的等待中星矢。',
  '操作本身不是打出卡牌，按真实推进量结算加速事件。这与每次打出或每回合自动推进的触发牌分开。', {termIds:['EF29','RL35'],direction:'主动操作'});

add('JK15','partner','能力',2,'知识。打出知识牌时，将其一张复制牌放入抽牌堆。','RB17',
  '打出的知识牌按原去向处理，后续使用依赖现有牌组。','打出知识牌时，将其一张复制牌放入抽牌堆。',
  '仍支付打出费用；复制牌需要重新抵达手牌。',
  '按实际打出事件复制该知识牌的有效修改；复制牌进入抽牌堆，按其正常费用与规则使用。', {termIds:['EF54','RL11','RL17'],direction:'打出复制'});
cards.find(c=>c.id==='JK15').keywords=['知识'];
const tool = {
  uid:'draft_card_JK15B',id:'JK15B',name:'JK15B',system:'partner',type:'技能',rarity:'附带',pool:false,cost:0,upgradedCost:0,
  text:'知识。获得8点格挡。消耗。',upgradedText:'知识。获得14点格挡。消耗。',turns:null,upgradedTurns:null,target:'自己',
  role:'知识牌',direction:'知识牌防御演示',keywords:['知识'],termIds:['EF05','KW_EXHAUST','KW_PARTNER'],breakthroughIds:[],archetypeIds:['SYS_partner'],partnerCardIds:['JK15'],art:'defaultPortrait',
  naming:{original:'',work:'',url:'',adaptation:'名称待定；当前显示设计编号。'},draftStatus:'机制演示',draftBatch:batchId,
  notes:'与JK15同组的知识牌。获得8点格挡，升级后获得14点格挡。使用后消耗。'
};
cards.push(tool); cards.find(c=>c.id==='JK15').partnerCardIds=['JK15B'];
partnerGroups.push({uid:'draft_group_JK15',id:'GROUP_JK15',title:'JK15',mainCardId:'JK15',grantedCardIds:['JK15B'],grantedCardCounts:{JK15B:1}});
add('JK16','partner','能力',1,'你可以将手牌中的知识牌变为同组的其他知识牌，持续整场战斗。','RB10',
  '同组知识牌的实际对象、用途和持有比例固定。','开放同组知识牌之间的主动转化。',
  '原牌用途被让出，转化后的牌支付对应牌面的正常费用。',
  '转化不生成额外对象，也不发放永久牌。按知识牌牌面、类型与初始倒计时使用；原有效修改按变形继承规则处理，战斗结束还原永久模型。', {termIds:['EF25','RL12','RL17'],direction:'组内形态'});
add('JK17','partner','能力',3,'你对知识牌施加的升级、费用变化和伤害加成，改为作用于其整组牌。','RB18',
  '同组知识牌分别升级、分别承受费用与伤害加成修改。','把之后的单牌修改扩展为整组修改。',
  '仍需取得整组、支付安装费用并提供修改来源；修改时长沿用原效果。',
  '先合并实际目标并去重，再各施加一次。组内扩展不递归触发自身，也不追溯安装之前的修改。', {termIds:['EF52','RL29'],direction:'整组修改'});
add('JK18','partner','技能',1,'选择你手牌中的一张知识牌和另一张牌。本场战斗中，后者也视为同组的知识牌。消耗。','RB19',
  '知识牌的来源由固定关联模型决定，其他实际牌无法被指定为本组工具。','把任意所选实际手牌临时加入指定知识关联。',
  '知识牌与候选工具需要同时在手；本牌消耗，临时关联随战斗结束清理。',
  '不重复永久获取流程。所选实际牌获得知识关键词与该组关系；普通生成的同名牌不继承，实际复制依照有效修改继承。原模型身份保留。', {termIds:['EF69','RL17','RL18'],direction:'关联范围'});
add('JK19','partner','能力',2,'知识牌不计入手牌上限。','RB20',
  '每张在手的知识牌都占用一份手牌容量。','从手牌上限的占用统计中排除知识牌。',
  '知识牌仍需抵达、持有并正常支付，其他手牌继续占用容量。',
  '只改变接牌时的容量判断；按手牌张数计算的效果仍读取实际张数。', {termIds:['RL19','NV45'],direction:'手牌容量'});

add('JG13','growth','技能',3,'选择你手牌中一张来自永久牌组的攻击牌。将其伤害加成永久加入基础伤害，并清空伤害加成。消耗。','GB15',
  '伤害加成仅在本场战斗有效，且与基础伤害分开记录。','把已有存量转入永久基础伤害，结束该份临时存量。',
  '先积累伤害加成，支付转换费用并消耗本牌；原存量随转换清空。',
  '只选择可映射回永久牌组的实际牌。临时生成物不具备永久落点；转换不产生新的获得伤害加成事件。永久增长属于高幅度备选，需单独比较成品卡位。', {termIds:['EF59','RL23','RL33'],direction:'永久转化'});
add('JG14','growth','能力',2,'你的攻击牌和能造成伤害的星矢牌结算伤害时，伤害加成改为使目标失去等量生命值。','GB16',
  '伤害加成与其他伤害分量一起经过目标的伤害处理。','将伤害加成分量从普通伤害中取出，改为独立生命流失。',
  '仍需实际获得加成并打出攻击，原基础伤害继续按普通伤害结算。',
  '每段分别处理，原伤害中移除被转换的分量；目标的伤害增减只作用于普通伤害部分。生命流失不触发造成伤害事件，也不再次获得加成。', {termIds:['EF68','RL22','RL33'],direction:'伤害性质'});
add('JG15','growth','技能',1,'选择你手牌中的一张攻击牌或能造成伤害的星矢牌。移除其全部伤害加成，每移除3点，其耗能在本场战斗中减少1。消耗。','GB17',
  '伤害加成只能保留在载体上用于后续伤害，无法支付载体的使用成本。','花掉伤害加成存量，换取同一实际牌的本场费用降低。',
  '让出已有输出投资，按实际移除量换算；本牌消耗。',
  '每3点向下取整，费用最低0，固定耗能牌作为当前验证范围。消耗存量不是负数的新获得事件。', {termIds:['RL23','RL33'],direction:'资源用途'});
add('JG16','growth','技能',1,'重新分配你手牌中所有攻击牌和能造成伤害的星矢牌的伤害加成。消耗。','GB18',
  '已有伤害加成绑定各个实际牌实例。','允许把同一总额在手中的实际攻击之间重新分配。',
  '投资总量保持不变，两端实际攻击需要同时在手，本牌消耗。',
  '例如+8与+2可重分为+0与+10。按存量设置执行，转移不产生新获得事件，因此不会增加JG03的伤害次数。', {termIds:['RL33'],direction:'存量转移'});

const rejected = [
  {direction:'每回合额外触发、每次加速获得格挡、完成返能',reason:'分别属于周期触发与事件收益，不再作为突破扩充。'},
  {direction:'知识到手升级、打出知识牌减费、增加普通复制份数',reason:'沿用已有来源与动作，只作为常规工具或收益。'},
  {direction:'伤害加成增量翻倍、单牌换算倍率提高',reason:'主要提升投资效率或单牌收益，不继续占用突破候选。'},
  {direction:'取回等待中的星矢并保留进度、设置或重置当前计数、暂停自然计时、通用改目标',reason:'沿用此前已收起的方向，本轮继续留在候选之外。'},
  {direction:'全场星矢完成后自动循环留场',reason:'沿用此前已收起的方向；本轮的置顶与消耗堆打出都要求原物离场后重新支付和布置。'},
  {direction:'把伤害加成扩成独立的格挡成长体系',reason:'继续让伤害加成承担输出小体系定位，本轮只探索其存量、寿命和结算方式。'},
  {direction:'知识永久递归发放、永久重配所有关联组',reason:'本轮关联范围与形态变化均限定在本场战斗，保留既定永久获取结构。'}
];
const known = new Set([...seed.data.terms,...terms].map(t=>t.id));
for (const card of cards) for (const id of card.termIds) if (!known.has(id)) throw Error('未知词条 '+id+' / '+card.id);
const keep = new Set(demoIds);
for (const card of live.data.cards) if (!card.trashedAt && card.role==='突破限制牌') keep.add(card.id);
for (const group of live.data.partnerGroups) if (keep.has(group.mainCardId)) for (const id of group.grantedCardIds) keep.add(id);
const archiveAt = new Date().toISOString();
const archived = live.data.cards.filter(card=>!card.trashedAt && card.draftBatch==='junna-first-card-draft-20261101-v1' && !keep.has(card.id));
const archiveUpdates = archived.map(card=>({collection:'cards',id:card.id,values:{trashedAt:archiveAt,trashReason:'精简常规牌，暂留机制演示所需牌面。'}}));
const data = JSON.parse(JSON.stringify(live.data));
for (const update of archiveUpdates) Object.assign(data.cards.find(card=>card.id===update.id),update.values);
for (const term of terms) if (!data.terms.some(t=>t.id===term.id)) data.terms.push(term);
for (const card of cards) if (!data.cards.some(c=>c.id===card.id)) data.cards.push(card);
for (const group of partnerGroups) if (!data.partnerGroups.some(g=>g.id===group.id)) data.partnerGroups.push(group);
const batches = [
  {id:batchId+'-archive',label:'精简常规牌，保留机制示例牌',updates:archiveUpdates,applyUpdatesOnImport:true},
  {id:batchId+'-terms',label:'补充规则突破分类',collection:'terms',ids:terms.map(t=>t.id)},
  {id:batchId+'-cards',label:'新增第二轮规则突破探索',collection:'cards',ids:cards.map(c=>c.id)},
  {id:batchId+'-groups',label:'补充消耗再用知识牌',collection:'partnerGroups',ids:partnerGroups.map(g=>g.id)}
];
seed.data=data;
for (const batch of batches) {
  if (!seed.seedBatches.some(b=>b.id===batch.id)) seed.seedBatches.push(batch);
  if (!seed.appliedSeedBatches.includes(batch.id)) seed.appliedSeedBatches.push(batch.id);
}
write(path.join(here,'seed.json'),seed);
const active=data.cards.filter(c=>!c.trashedAt);
const summary={newBreakthroughs:cards.filter(c=>c.pool).length,newGrants:cards.filter(c=>!c.pool).length,breakthroughs:active.filter(c=>c.role==='突破限制牌').length,demoPool:demoIds.length,grants:active.filter(c=>!c.pool).length,active:active.length,trash:data.cards.filter(c=>c.trashedAt).length};
const document={schemaVersion:1,id:batchId,date:'2026-11-01',title:'纯那第二轮规则突破探索',criterion:'逐项对照原规则与明确例外，考察费用支付、打出区域、类型、去向、关联、容量、存量用途和持续时间。',summary,demoIds,archivedIds:archived.map(c=>c.id),cards,partnerGroups,terms,rejected,recommendations:{stage:['JA19','JA21','JA24'],partner:['JK15','JK18','JK19'],growth:['JG13','JG15','JG16']},review:{scope:'仅探索候选，不填完整卡池。卡名继续待定，升级暂以费用变化为主。',costs:'生命支付与消耗堆打出保留实际代价；资源转换清空原存量；转移遵守总量守恒。',mutualEffects:'类型扩展、整组修改与全体增伤先求实际目标并集再执行一次；转移、转换与继承不计为新获得事件。',deferred:'费用、永久增长幅度与多张突破叠加的实战强度待单独比较。'}};
write(path.join(dataDir,'纯那第二轮突破探索.json'),document);
const tracking=read(path.join(dataDir,'纯那职业卡池设计记录.json'));
tracking.status='常规牌精简为机制演示，新增第二轮规则突破探索';
tracking.current={...summary,demoIds,source:'纯那第二轮突破探索.json'};
tracking.batches=[...tracking.batches.filter(b=>b.id!==batchId),{id:batchId,...summary,source:'纯那第二轮突破探索.json',recommendations:document.recommendations}];
write(path.join(dataDir,'纯那职业卡池设计记录.json'),tracking);
let readme=fs.readFileSync(path.join(here,'README.md'),'utf8');
readme=readme.replace(/首轮共51张牌面；[^\r\n]+/, '当前保留27张规则突破候选、7张常规示例牌与5张必要知识牌；其余常规牌移入垃圾桶。[第二轮探索](../data/纯那第二轮突破探索.json)逐张记录原有限制、规则变化、投入与复核点。卡名以编号暂代，费用与升级为初稿。');
fs.writeFileSync(path.join(here,'README.md'),readme);
console.log(JSON.stringify({summary,newIds:cards.filter(c=>c.pool).map(c=>c.id),demoIds,archivedIds:archived.map(c=>c.id)}));
