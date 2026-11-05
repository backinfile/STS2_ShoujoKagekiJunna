const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const here = __dirname, dataDir = path.resolve(here, '../data');
const read = file => JSON.parse(fs.readFileSync(file, 'utf8'));
const write = (file, value) => fs.writeFileSync(file, JSON.stringify(value, null, 2) + '\n');
const batchId = 'junna-complete-pool-draft-20261102-v1';
const liveFile = path.join(dataDir, '纯那Mod设计工作台_本地编辑.json');
const seedFile = path.join(here, 'seed.json');
const live = read(liveFile), seed = read(seedFile), before = structuredClone(live.data);
assert(!live.seedBatches.some(b => b.id.startsWith(batchId)), '本轮已经导入，继续保留工作台编辑。');
const cards = [], groups = [], updates = [];
const routeData = [
  ['RT_ACCEL','加速完成','stage'], ['RT_REPEAT','重放触发','stage'],
  ['RT_LINK','关联调度','partner'], ['RT_KNOWLEDGE','知识运转','partner'],
  ['RT_HIGH','高费配合','utility']
].map(([id,name,system]) => ({uid:'full_pool_'+id,id,name,definition:name,system}));
const terms = [{uid:'full_pool_TR46',id:'TR46',kind:'扳机',name:'受到攻击后',definition:'敌方对自己的一个攻击行动结束后触发一次，多段攻击合计为一次；全被格挡的攻击仍触发，失去生命与状态伤害分别处理。',template:'受到攻击后，一个箭矢加速1回合。',systems:['stage'],unit:'次',notes:'本轮内部事件分类，不新增玩家关键词。',sources:['纯那完整卡池初稿']}];
const routes = {
  speed: {name:'加速完成',ids:['SYS_stage','RT_ACCEL']},
  repeat: {name:'重放触发',ids:['SYS_stage','RT_REPEAT']},
  link: {name:'关联调度',ids:['SYS_partner','RT_LINK']},
  knowledge: {name:'知识运转',ids:['SYS_partner','RT_KNOWLEDGE']},
  bonus: {name:'伤害加成',ids:['SYS_growth']},
  high: {name:'高费配合',ids:['RT_HIGH']},
  general: {name:'体系外',ids:[]}, starter: {name:'起始',ids:[]}
};
const defaultRules = '默认只影响自己。箭矢先正常支付费用和建立布置，完成时结算牌效；重放与额外触发沿已有事件规则分别处理。伤害加成只赋予攻击牌和能造成伤害的箭矢牌。生成、复制单张知识牌只处理指定对象，奖励、发现按知识组配置获得。';
function add(id,type,rarity,cost,text,upgradedText=text,options={}) {
  assert(!before.cards.some(c=>c.id===id) && !cards.some(c=>c.id===id),id);
  const route = options.route || 'general', r = routes[route];
  const systems = options.systems || (['stage','partner','growth'].includes(options.system) ? [options.system] : route==='speed'||route==='repeat'?['stage']:route==='link'||route==='knowledge'?['partner']:route==='bonus'?['growth']:[]);
  const system = options.system || systems[0] || 'utility';
  const base = (options.group?'知识。':'')+text, up = (options.group?'知识。':'')+upgradedText, all = base+up;
  const termIds = new Set(options.termIds||[]), keywords=[];
  const detect = (test,ids) => {if(test)for(const id of ids)termIds.add(id);};
  detect(type==='箭矢',['KW_STAGE','EF28']);
  detect(options.group||all.includes('知识'),['KW_PARTNER']);
  detect(/造成\d+点伤害/.test(all),[/伤害\d+次/.test(all)?'EF02':'EF01']);
  detect(all.includes('所有敌人'),['EF03']);
  detect(/获得\d+点格挡/.test(all),['EF05']);
  detect(/抽\d+张牌|抽取/.test(all),['EF06']);
  detect(all.includes('抽取'),['EF13']);
  detect(/获得\d+点能量/.test(all),['EF09']);
  detect(all.includes('下回合'),['EF10']);
  detect(all.includes('伤害加成'),['KW_BONUS']);
  detect(all.includes('获得')&&all.includes('伤害加成'),['EF39']);
  detect(all.includes('额外造成'),['EF42']);
  detect(all.includes('加速'),['EF29']);
  detect(all.includes('立即加速到完成'),['EF32']);
  detect(all.includes('额外触发'),['EF33']);
  detect(all.includes('重放'),['KW_REPLAY','EF43']);
  detect(all.includes('复制'),['KW_COPY','EF21']);
  detect(all.includes('变化为'),['EF25','RL12']);
  detect(all.includes('给予')&&(all.includes('虚弱')||all.includes('脆弱')||all.includes('易伤')),['EF11']);
  detect(all.includes('虚弱'),['ST_WEAK']);detect(all.includes('脆弱'),['ST_FRAIL']);detect(all.includes('易伤'),['ST_VULNERABLE']);
  for(const [word,term] of [['保留','KW_RETAIN'],['消耗','KW_EXHAUST'],['固有','KW_INNATE'],['虚无','KW_ETHEREAL']]){
    detect(all.includes(word),[term]);
    if(new RegExp('(^|。)'+word+'。').test(base)||new RegExp('(^|。)'+word+'。').test(up))keywords.push(word);
  }
  if(options.group)keywords.unshift('知识');
  if(keywords.includes('消耗'))termIds.add('EF27');
  const archetypeIds = [...new Set([...r.ids,...systems.map(s=>'SYS_'+s).filter(id=>before.archetypes.some(a=>a.id===id)),...(options.archetypeIds||[])])];
  const triggerRole=options.triggerRole || (route==='general'?'功能补洞':'扳机触发');
  const port=options.port || '输出';
  const role=options.role || (rarity==='基础'?'起始牌':route==='general'?'体系外牌':triggerRole==='扳机收益'?'机制核心牌':rarity==='普通'?'机制引导牌':'机制中坚牌');
  const rules=options.rules || defaultRules;
  cards.push({uid:'complete_pool_'+id,id,name:options.name||id,type,system,rarity,pool:options.pool??(rarity!=='基础'&&!options.groupChild),cost,upgradedCost:options.upgradedCost??cost,
    turns:type==='箭矢'?(options.turns??1):null,upgradedTurns:type==='箭矢'?(options.upgradedTurns??options.turns??1):null,
    ...(type==='箭矢'?{arrowSubtype:options.arrowSubtype||(/造成\d+点伤害/.test(base)?'攻击':'技能')}:{}),
    target:options.target||((type==='攻击'||type==='箭矢'&&/造成\d+点伤害/.test(base))?(base.includes('对所有敌人')?'所有敌人':'敌人'):'自己'),
    text:base,upgradedText:up,keywords,termIds:[...termIds],archetypeIds,breakthroughIds:[],partnerCardIds:[],art:'defaultPortrait',character:'星见纯那',role,direction:r.name,
    draftStatus:'完整卡池初稿',draftBatch:batchId,naming:{original:options.name||'',work:options.name?'本体中文卡名':'',url:'',adaptation:options.name?'沿用本体卡名。':'卡名待定，沿用工作台编号展示。'},
    design:{classification:role,route:r.name,triggerRole,systems,systemRoles:options.systemRoles||Object.fromEntries(systems.map(s=>[s,{role:triggerRole,port}])),port,roleReason:options.reason,
      rules,independence:options.independence||'有独立基础效果；相关构筑提高收益。',risk:options.risk||'费用与数值在实战阶段复核。',upgrade:options.upgrade||(options.upgradedCost!==undefined?'降低费用，保持效果。':'提高牌面数值，保持对象与结算时点。'),
      textComplexity:base.length<=34?'低':'中',reference:options.reference||'体系分析流程：来源、收益、端口和连接分别记录。'},
    notes:'定位：'+options.reason+'\n结算约定：'+rules});
}

// 箭矢：稳定载荷、主动加速、被动来源、完成收益与重放利用。
add('JA38','箭矢','普通',1,'造成12点伤害。','造成16点伤害。',{route:'speed',turns:1,reason:'低等待的伤害载荷，单独取得也能使用。'});
add('JA39','箭矢','普通',2,'获得20点格挡。','获得26点格挡。',{route:'speed',turns:2,port:'防御',reason:'值得主动加速的高额防御载荷。'});
add('JA40','技能','普通',0,'获得4点格挡。一个箭矢加速1回合。消耗。','获得7点格挡。一个箭矢加速1回合。消耗。',{route:'speed',port:'防御／运转',termIds:['OB13'],reason:'用一次性低费动作补启动防御并推进布置。'});
add('JA41','箭矢','普通',2,'造成5点伤害3次。','造成7点伤害3次。',{route:'speed',turns:1,systems:['stage','growth'],port:'输出',reason:'快速完成的多段载荷，承接分散的伤害加成。',systemRoles:{stage:{role:'扳机触发',port:'输出'},growth:{role:'扳机收益',port:'输出'}}});
add('JA42','攻击','普通',2,'造成14点伤害。目标的所有箭矢加速2回合。','造成18点伤害。目标的所有箭矢加速2回合。',{route:'speed',reason:'集中同一敌人的多个布置，区别于全体敌人的分散加速。',rules:'先结算当前攻击，再快照自己箭矢牌堆中以该敌人为目标的有效布置，各加速2回合；目标死亡后沿已有目标失效与完成规则处理。'});
add('JA43','技能','罕见',2,'所有箭矢加速2回合。消耗。',undefined,{route:'speed',upgradedCost:1,port:'运转',reason:'以有限加速量兑现整场布置，保留长倒计时对象的等待差别。'});
add('JA44','技能','罕见',1,'一个箭矢加速2回合。抽1张牌。消耗。','一个箭矢加速3回合。抽1张牌。消耗。',{route:'speed',port:'运转',reason:'精确加速并补回手牌，消耗限制持续抽牌。',rules:'先处理所选有效箭矢的加速及其完成队列，再抽1张牌。满手沿已有规则，抽牌不要求该箭矢实际完成。'});
add('JA45','能力','罕见',2,'箭矢完成后，获得3点格挡。','箭矢完成后，获得4点格挡。',{route:'speed',triggerRole:'扳机收益',port:'防御',termIds:['TR28'],reason:'把完成次数接到防御，支持频繁布置与快速完成。',rules:'仅响应自己的每个布置唯一一次正常完成事件；重放和额外触发不另记完成次数。'});
add('JA46','攻击','罕见',2,'造成12点伤害。本回合每完成一个箭矢，额外造成1次伤害。','造成16点伤害。本回合每完成一个箭矢，额外造成1次伤害。',{route:'speed',triggerRole:'扳机收益',reason:'完成多个箭矢后兑现攻击次数，兼容单牌伤害加成。',rules:'伤害指令开始时读取本回合自己的正常完成布置数C，本指令基础次数为1+C；与JG03、JG23等次数修正相加。每段依次读取伤害修正，重放重新执行指令但不递归增加本卡的完成计数。'});
add('JA47','技能','罕见',1,'获得8点格挡。本回合每完成一个箭矢，额外获得3点格挡。','获得12点格挡。本回合每完成一个箭矢，额外获得3点格挡。',{route:'speed',triggerRole:'扳机收益',port:'防御',reason:'完成后的即时防线，未铺场时仍保留基础格挡。',rules:'指令执行时读取本回合自己的完成布置数C，合并为一次8+3C格挡指令，升级为12+3C；格挡修正作用一次。'});
add('JA48','能力','罕见',2,'受到攻击后，一个箭矢加速1回合。',undefined,{route:'speed',upgradedCost:1,port:'运转',termIds:['TR46'],reason:'以敌方攻击提供被动加速来源，增加主动出牌之外的推进方式。',rules:'敌方一个对自己的攻击行动全部结束后，选择自己的一个有效布置加速1回合；多段攻击合计一次，全被格挡仍触发。没有可选布置时跳过。自己的攻击、状态伤害和直接失去生命分别处理。'});
add('JA49','箭矢','罕见',1,'造成8点伤害。将此牌的一张复制加入手牌。消耗。','造成11点伤害。将此牌的一张复制加入手牌。消耗。',{route:'repeat',turns:2,port:'输出／运转',reason:'完成后补充后续箭矢，重放提高补充数量。',rules:'每份牌效先伤害，再复制当前实际牌到手牌，继承升级与有效修改，但不继承当前布置进度；重新打出建立新布置。整批后来源牌消耗，复制品各自保留消耗。JA22沿既有消耗优先规则处理。'});
add('JA50','技能','罕见',2,'额外触发一个箭矢。使其获得重放1。消耗。',undefined,{route:'repeat',upgradedCost:1,port:'运转',reason:'先取得当前牌效，再提高未来完成时的结算价值。',rules:'对同一有效布置先按当前重放记录额外触发一次，整批完成后若仍有效，再为该实际牌及布置记录增加重放1。额外触发本身不完成或移除布置；牌效自身的明确去向仍生效。'});
add('JA51','箭矢','罕见',2,'造成10点伤害3次。','造成14点伤害3次。',{route:'repeat',turns:3,systems:['stage','growth'],reason:'少量高质量布置的重复触发目标，代价为更长等待。',systemRoles:{stage:{role:'扳机触发',port:'输出'},growth:{role:'扳机收益',port:'输出'}}});
add('JA52','能力','罕见',2,'回合结束时，剩余回合数最多的箭矢加速1回合。','回合结束时，剩余回合数最多的箭矢加速2回合。',{route:'speed',port:'运转',termIds:['TR03'],reason:'按场面进度分配被动加速，帮助较慢的高价值箭矢。',rules:'每份能力在自己的回合结束时快照有效布置，选择剩余计数最大的一个；并列时由玩家选择。本次来源只推进这一个布置，完成后不重新寻找下一个对象。'});
add('JA53','箭矢','普通',1,'获得8点格挡。抽取1张箭矢牌。','获得11点格挡。抽取1张箭矢牌。',{route:'speed',turns:1,port:'防御／运转',reason:'用完成后的防御和定向抽取维持箭矢密度。',rules:'正常完成或明确额外触发时，先获得格挡，再从自己的抽牌堆定向抽取1张当前为箭矢类型的实际牌；不检索弃牌堆或触发洗牌。'});
add('JA54','攻击','罕见',1,'造成7点伤害。下1张箭矢牌获得重放1。','造成10点伤害。下1张箭矢牌获得重放1。',{route:'repeat',reason:'准备下一次高质量布置，保留当回合基础输出。',rules:'伤害后建立一次准备；自己的下一张实际打出的箭矢在建立布置前获得重放1，并消耗准备。准备跨回合保留，沿已有下一张规则叠加；额外触发和场内复制不消耗准备。'});
add('JA55','技能','罕见',2,'获得12点格挡。一个箭矢获得重放2。消耗。','获得16点格挡。一个箭矢获得重放2。消耗。',{route:'repeat',port:'防御／运转',reason:'保护投资回合，并把额外结算集中给一个已有布置。',rules:'先获得格挡，再选择自己的一个有效布置，使实际牌与布置的重放记录均增加2；不重新打出、不改变剩余计数。'});
add('JA56','能力','稀有',3,'箭矢额外触发后，使其获得2点伤害加成。',undefined,{route:'repeat',upgradedCost:2,systems:['stage','growth'],triggerRole:'跨体系联动',reason:'让反复额外触发同一伤害箭矢产生长期输出投资。',termIds:['TR32'],systemRoles:{stage:{role:'扳机收益',port:'输出'},growth:{role:'扳机触发',port:'输出'}},rules:'每次根额外触发批次结束后，对该实际牌赋予2点伤害加成；该批次的重放不重复产生根事件。普通完成及其重放不触发本能力；没有伤害指令的箭矢不获得加成。增量只影响之后的伤害。'});
add('JA57','箭矢','罕见',1,'抽2张牌。获得1点能量。','抽2张牌。获得2点能量。',{route:'repeat',turns:2,port:'运转',reason:'为额外触发提供明确资源目标，正常等待也能补充展开。',rules:'按正常完成或明确额外触发的时点抽牌、获得能量；每份重放分别执行。满手按已有规则，能量在当前执行回合获得，沿本体回合资源清理处理。'});

// 知识通用配套：覆盖组内所有成员，不新增“关联牌专属”关键词。
add('JK54','箭矢','普通',1,'造成10点伤害。抽取1张知识牌。','造成14点伤害。抽取1张知识牌。',{route:'knowledge',turns:1,systems:['partner','stage'],port:'输出／运转',triggerRole:'跨体系联动',reason:'用箭矢完成补充任意知识牌，帮助混合构筑进入知识运转。',systemRoles:{stage:{role:'扳机触发',port:'输出'},partner:{role:'扳机触发',port:'运转'}}});
add('JK55','技能','普通',1,'获得7点格挡。将弃牌堆中的1张知识牌放入手牌。','获得10点格挡。将弃牌堆中的1张知识牌放入手牌。',{route:'link',port:'防御／运转',termIds:['EF15'],reason:'提供不依赖固定牌名的知识回收，同时补启动防御。',rules:'选择自己的弃牌堆中1张带知识关键词的实际牌移入手牌；全部组成员同等合法。保存有效修改，沿满手规则处理，不发布抽牌事件或重新发放知识组。'});
add('JK56','能力','罕见',1,'每回合第1次打出知识牌后，获得1点能量。','每回合第1次打出知识牌后，获得2点能量。',{route:'knowledge',triggerRole:'扳机收益',port:'运转',termIds:['TR13'],reason:'为知识牌组每回合启动提供稳定的能量回报。',rules:'每份能力按自己本回合实际打出的第一张知识牌结算一次；安装前的出牌也计入本回合记录。重放、生成和复制不作为额外出牌，组内全部成员使用同一条件。'});
add('JK57','能力','罕见',2,'每打出3张知识牌，获得8点格挡。','每打出3张知识牌，获得11点格挡。',{route:'knowledge',triggerRole:'扳机收益',port:'防御',termIds:['TR13'],reason:'用持续知识连打建立防线，而非监听消耗或弃牌。',rules:'每份已生效能力记录自己的实际知识出牌数，每达到3次结算一次格挡并扣去3次，余数跨回合保留；安装前的出牌不回溯触发。重放不增加计数。'});
add('JK58','技能','罕见',1,'选择1张知识牌，使它及其关联牌获得2点伤害加成。','选择1张知识牌，使它及其关联牌获得3点伤害加成。',{route:'link',systems:['partner','growth'],triggerRole:'跨体系联动',reason:'把一次投资扩展至所选知识组的多个有效伤害载体。',systemRoles:{partner:{role:'扳机收益',port:'输出'},growth:{role:'扳机触发',port:'输出'}},rules:'从自己的本场实际牌中选择1张知识牌，按其直接关联配置合并所选牌名与关联牌名，再快照自己所有对应实际对象并去重；仅合法伤害加成载体获得增量。在场箭矢同步修改，后续生成对象沿原生成规则。'});
add('JK59','攻击','普通',2,'造成15点伤害。手牌中每有1张知识牌，多造成2点伤害。','造成20点伤害。手牌中每有1张知识牌，多造成2点伤害。',{route:'knowledge',triggerRole:'扳机收益',reason:'把知识牌组的手牌密度转为即时输出。',rules:'当前牌离开手牌后，在伤害指令执行时读取手牌知识牌数量K，基础伤害15+2K，升级20+2K；同名多张分别计数，组内身份不作区分。'});
add('JK60','技能','罕见',1,'本回合，知识牌获得保留。',undefined,{route:'knowledge',upgradedCost:0,port:'运转',reason:'把不同回合抽到的知识组件留到一起使用。',termIds:['EF26'],rules:'在自己的当前回合为手牌及之后进入手牌的知识牌提供保留，持续到本回合手牌清理结束；之后停止该临时来源。仅改变保留状态，不移动在场箭矢或其他牌区对象。'});
add('JK61','箭矢','罕见',2,'将弃牌堆中的2张知识牌放入手牌。','将弃牌堆中的3张知识牌放入手牌。',{route:'link',turns:1,systems:['partner','stage'],triggerRole:'跨体系联动',port:'运转',termIds:['EF15'],reason:'把箭矢完成转为一批知识组件回收，承接加速与重放。',systemRoles:{stage:{role:'扳机触发',port:'运转'},partner:{role:'扳机触发',port:'运转'}},rules:'每份牌效分别从当前自己的弃牌堆选择至多2/3张知识实际牌移入手牌；已经移入的对象不重复选取，满手按已有规则。该移动不发布抽牌事件。'});
add('JK62','能力','稀有',2,'回合开始时，将弃牌堆中的1张知识牌放入手牌。',undefined,{route:'link',upgradedCost:1,port:'运转',termIds:['TR02','EF15'],reason:'持续取回关键知识组件，提供与随机抽牌不同的稳定来源。',rules:'每份能力在自己的回合开始时选择弃牌堆中的1张实际知识牌移入手牌；不将手中、执行中或在场牌当成弃牌对象。保存修改，不设为免费，不发布抽牌事件。'});
add('JK63','攻击','罕见',1,'造成6点伤害。本回合每打出一种知识牌，额外造成1次伤害。','造成8点伤害。本回合每打出一种知识牌，额外造成1次伤害。',{route:'knowledge',triggerRole:'扳机收益',systems:['partner','growth'],reason:'奖励使用不同知识组件，以多段输出承接伤害加成。',systemRoles:{partner:{role:'扳机收益',port:'输出'},growth:{role:'扳机收益',port:'输出'}},rules:'伤害指令开始时读取自己本回合实际打出过的不同知识模型数D，基础次数1+D；同名、升级版本和复制不增加种类。当前牌没有独立知识组，也不自带知识关键词。与其他次数修正相加。'});
add('JK64','技能','罕见',2,'本回合，打出知识牌后，一个箭矢加速1回合。消耗。',undefined,{route:'knowledge',upgradedCost:1,systems:['partner','stage'],triggerRole:'跨体系联动',port:'运转',termIds:['TR13'],reason:'让知识连打成为箭矢的主动加速燃料。',systemRoles:{partner:{role:'扳机收益',port:'运转'},stage:{role:'扳机触发',port:'运转'}},rules:'建立到自己当前回合结束的临时监听；之后每次自己实际打出知识牌，选择一个有效己方箭矢加速1回合。当前技能无独立知识组，不带知识关键词；重放与牌效执行不额外发布出牌事件。'});
add('JK65','能力','罕见',2,'知识牌进入手牌时，若其关联牌也在手牌中，使它获得2点伤害加成。','知识牌进入手牌时，若其关联牌也在手牌中，使它获得3点伤害加成。',{route:'knowledge',systems:['partner','growth'],triggerRole:'跨体系联动',termIds:['TR17'],reason:'让调度与集齐组件的动作带来输出投资，保留非出牌触发来源。',systemRoles:{partner:{role:'扳机收益',port:'输出'},growth:{role:'扳机触发',port:'输出'}},rules:'每张知识实际牌进入自己的手牌后，只检查该进入对象的直接关联牌名是否至少有一个也在手中；满足时赋予进入对象2/3点伤害加成，仅合法载体生效。同批进入按实际顺序处理，已在手的其他牌不回溯获得加成。'});

function group(root,ids,counts={}) {groups.push({uid:'complete_pool_group_'+root,id:'GROUP_'+root,title:root,mainCardId:root,grantedCardIds:ids,grantedCardCounts:Object.fromEntries(ids.map(id=>[id,counts[id]||1]))});}
const paired = {group:true,route:'link',system:'partner'};
add('JK66','攻击','普通',2,'造成16点伤害。JK66B本回合耗能变为0。','造成22点伤害。JK66B本回合耗能变为0。',{...paired,reason:'攻防接力的付费入口，安排关联牌的本回合使用时机。',rules:'伤害后，使自己本场实际存在的同名JK66B在本回合耗能变为0；未来生成沿原生成规则。组内两张牌各自升级。'});
add('JK66B','技能','普通',1,'获得7点格挡。将弃牌堆中的JK66放到抽牌堆顶。','获得10点格挡。将弃牌堆中的JK66放到抽牌堆顶。',{...paired,groupChild:true,port:'防御／运转',termIds:['EF20'],reason:'接住攻防减费，并为下轮安排固定攻击。',rules:'先获得格挡，再从自己弃牌堆选择1张实际JK66置于抽牌堆顶；没有对象时只获得格挡，不生成或复制新牌。'});group('JK66',['JK66B']);
add('JK67','能力','罕见',2,'回合开始时，JK67B获得2点伤害加成。','回合开始时，JK67B获得3点伤害加成。',{...paired,route:'repeat',systems:['partner','stage','growth'],reason:'以指定多段箭矢为长期投资对象，补充等待期间的收益选择。',triggerRole:'跨体系联动',termIds:['TR02'],rules:'自己的回合开始时，快照本场所有实际JK67B并各赋予2/3点伤害加成；手牌、抽牌堆、弃牌堆及有效布置沿现有作用范围处理，同一对象只修改一次，之后新生成对象不追溯继承。'});
add('JK67B','箭矢','罕见',2,'造成8点伤害3次。','造成10点伤害3次。',{...paired,route:'repeat',groupChild:true,turns:3,systems:['partner','stage','growth'],triggerRole:'扳机收益',reason:'把同组定向投资按每段伤害兑现，也可用额外触发提前利用。'});group('JK67',['JK67B']);
const historyRules='本场记录自己实际打出过的JK68B、JK68C模型；在JK68每个伤害指令开始时检查两者，满足则基础次数3，否则1。升级与同名复制共用对应模型记录。B的出牌在其加速指令前已经记入，C消耗后记录保留；升级不改变解锁条件。';
add('JK68','箭矢','罕见',2,'造成14点伤害。本场战斗打出过JK68B和JK68C后，额外造成2次伤害。','造成18点伤害。本场战斗打出过JK68B和JK68C后，额外造成2次伤害。',{...paired,route:'knowledge',turns:1,systems:['partner','stage'],triggerRole:'触发+收益',reason:'按完整组内使用记录解锁多段终结，补回使用记录结构。',rules:historyRules});
add('JK68B','攻击','罕见',1,'造成7点伤害。JK68加速1回合。','造成10点伤害。JK68加速1回合。',{...paired,route:'knowledge',groupChild:true,systems:['partner','stage'],reason:'同时提供组内使用记录与指定箭矢的即时兑现。',rules:historyRules+'加速时选择自己的一个有效JK68布置；没有布置时正常造成攻击伤害。'});
add('JK68C','技能','罕见',0,'获得3点格挡。JK68B本回合耗能减少1。消耗。','获得5点格挡。JK68B本回合耗能减少1。消耗。',{...paired,route:'knowledge',groupChild:true,port:'防御／运转',reason:'一次性开启组内出牌顺序，并降低最后一个启动组件的费用。',rules:historyRules+'对自己本场当前存在的所有实际JK68B施加本回合减费1，最低0；消耗的是本牌。'});group('JK68',['JK68B','JK68C']);
const transformRules='选择自己手牌中的1张实际JK69B，转换为JK69C；保留所选B自身的升级状态与有效修改，JK69的升级只提高自身格挡。转换不产生新出牌或知识发放，实际对象数保持，目标随之后重新打出时选择。战后恢复永久牌组原始模型。';
add('JK69','技能','罕见',1,'获得8点格挡。将手牌中的1张JK69B变化为JK69C。','获得12点格挡。将手牌中的1张JK69B变化为JK69C。',{...paired,port:'防御／运转',termIds:['EF25','RL12'],reason:'让关联攻击在即时输出和延迟多段之间选择，同时保护转换回合。',rules:transformRules});
add('JK69B','攻击','罕见',2,'造成22点伤害。消耗。','造成30点伤害。消耗。',{...paired,groupChild:true,reason:'同组的即时输出形态，可独立使用或转为箭矢。',rules:transformRules+'直接打出本牌时按攻击牌正常支付并消耗。'});
add('JK69C','箭矢','罕见',2,'造成14点伤害2次。消耗。','造成18点伤害2次。消耗。',{...paired,groupChild:true,turns:1,systems:['partner','stage','growth'],reason:'同组延迟多段形态，承接加速、重放和伤害加成。',rules:transformRules+'本牌基础伤害按各段处理，整批后消耗。'});group('JK69',['JK69B','JK69C']);
add('JK70','箭矢','普通',1,'获得9点格挡。将弃牌堆中的JK70B放到抽牌堆顶。','获得13点格挡。将弃牌堆中的JK70B放到抽牌堆顶。',{...paired,turns:1,port:'防御／运转',systems:['partner','stage'],termIds:['EF20'],reason:'用箭矢完成安排下次抽牌，组成到手配合。',rules:'每份牌效先获得格挡，再从自己的弃牌堆选择1张实际JK70B置于抽牌堆顶；已在手或执行中的对象不参与。每组初始各获得1张，生成单张不附送整组。'});
add('JK70B','攻击','普通',1,'造成8点伤害。若本回合从抽牌堆进入手牌，获得1点能量。','造成11点伤害。若本回合从抽牌堆进入手牌，获得1点能量。',{...paired,groupChild:true,triggerRole:'触发+收益',port:'输出／运转',reason:'奖励按组内安排从抽牌堆取得组件，提供回收后的支付回报。',rules:'打出本牌并结算伤害后，检查这个实际对象本回合是否从自己的抽牌堆进入过手牌；满足则获得1能量。本句是本牌使用时的条件收益，不是进入手牌时立即发能量。普通抽牌和从抽牌堆定向入手均满足，新生成、弃牌回收和发现入手不满足。'});group('JK70',['JK70B']);

// 伤害加成只扩充输出投资与兑现，防御、抽牌和能量由其他体系承担。
add('JG27','技能','普通',1,'选择1张牌获得4点伤害加成。消耗。','选择1张牌获得6点伤害加成。消耗。',{route:'bonus',reason:'简单的集中投资来源，区别于JG09的防御兼投资。',rules:'从自己的本场实际牌中选择1张合法伤害加成载体，赋予4/6点本场增量；含伤害指令的箭矢也可选择，在场对象同步修改。'});
add('JG28','攻击','罕见',2,'造成3点伤害4次。伤害加成按2倍生效。','造成4点伤害4次。伤害加成按2倍生效。',{route:'bonus',triggerRole:'扳机收益',reason:'多段与贡献倍率共同承接集中投资，保持纯输出定位。',termIds:['EF70'],rules:'每段基础伤害3/4，加上该实际牌当前伤害加成存量S的2倍后，再处理本体适用的动态伤害修正。贡献倍率不乘写存量，不改变JG23按S计算的派生次数；本轮多个贡献倍率设置取最大有效值，与JG29同时生效时取3倍。'});
add('JG29','能力','罕见',2,'每回合第1张结算伤害的牌，伤害加成按3倍生效。',undefined,{route:'bonus',upgradedCost:1,triggerRole:'扳机收益',reason:'通过安排结算顺序放大一次关键输出，区别于全场存量翻倍。',termIds:['EF70'],rules:'每份能力在自己每个回合区间的第一个实际卡牌伤害结算批次锁定来源；在下个自己回合开始时重置。该根批次全部伤害指令和重放的伤害加成贡献按3倍读取。箭矢完成与额外触发也参与，布置本身不消耗资格。无伤害指令的牌跳过；本轮多个贡献倍率设置取最大有效值，不连乘同类来源。'});
add('JG30','技能','稀有',2,'选择1张手牌，本回合其伤害加成对所有敌人生效。消耗。',undefined,{route:'bonus',upgradedCost:1,triggerRole:'扳机收益',reason:'让集中投资具备群体兑现方式，保持基础伤害与加成职责分开。',termIds:['EF44'],rules:'选择自己的1张合法伤害加成手牌，赋予到自己当前回合结束的范围修正。它每段原有基础伤害按原目标结算，伤害加成贡献改为对当时所有存活敌人各结算一次，原目标的加成只计一次；保持伤害属性及对应动态修正，不改为直接失去生命。延迟箭矢须在有效期内执行才获得该范围。'});

// 跨流派牌均写明两个体系各自的职责。
add('JX01','技能','普通',1,'获得7点格挡。一个箭矢获得2点伤害加成，并加速1回合。','获得10点格挡。一个箭矢获得2点伤害加成，并加速1回合。',{route:'speed',systems:['stage','growth'],triggerRole:'跨体系联动',port:'防御／输出／运转',reason:'先投资选中的箭矢，再用同一次操作推进兑现。',systemRoles:{stage:{role:'扳机触发',port:'运转'},growth:{role:'扳机触发',port:'输出'}},rules:'先获得格挡，再选择自己的一个有效箭矢：若为合法伤害加成载体，先赋予2点加成，然后加速1回合。非伤害箭矢仍可加速，伤害加成部分按资格跳过。'});
add('JX02','攻击','罕见',2,'造成12点伤害。本回合，手牌中的知识箭矢牌耗能减少1。','造成16点伤害。本回合，手牌中的知识箭矢牌耗能减少1。',{route:'knowledge',systems:['partner','stage'],triggerRole:'跨体系联动',port:'输出／运转',reason:'让知识组中的高费箭矢在同回合展开，费用让渡服务实际牌种。',systemRoles:{partner:{role:'扳机收益',port:'运转'},stage:{role:'扳机触发',port:'运转'}},rules:'伤害后快照自己手牌中同时为知识与箭矢的实际牌，对每张施加本回合减费1，最低0；之后新入手对象不追溯。'});
add('JX03','能力','罕见',2,'知识箭矢完成后，将弃牌堆中的1张与其关联的牌放入手牌。',undefined,{route:'link',upgradedCost:1,systems:['partner','stage'],triggerRole:'跨体系联动',port:'运转',reason:'用一个组件的完成取回另一个组件，连接箭矢节奏与知识调度。',systemRoles:{stage:{role:'扳机收益',port:'运转'},partner:{role:'扳机触发',port:'运转'}},rules:'每个己方知识箭矢布置正常完成后，按其模型的直接关联配置选择自己弃牌堆中的1张对应实际牌移入手牌。组内入口和附带成员同等处理；完成事件一次，重放不另算完成；不自动打出取回对象。'});
add('JX04','技能','罕见',1,'复制手牌中的1张知识牌，使复制品获得4点伤害加成。消耗。','复制手牌中的1张知识牌，使复制品获得6点伤害加成。消耗。',{route:'knowledge',systems:['partner','growth'],triggerRole:'跨体系联动',port:'输出／运转',reason:'将知识复制转为新的投资载体，避免只复制整组扩大数量。',systemRoles:{partner:{role:'扳机触发',port:'运转'},growth:{role:'扳机触发',port:'输出'}},rules:'选择自己的1张知识手牌，复制为新的实际对象并加入手牌，继承升级与有效修改；若复制品为合法伤害加成载体，再获得4/6点增量。非伤害知识牌仍可复制，仅跳过加成；只生成这一张复制，不发放整组。'});
add('JX05','箭矢','罕见',2,'本回合每打出过1张知识牌，此牌获得1点伤害加成。造成6点伤害3次。','本回合每打出过1张知识牌，此牌获得1点伤害加成。造成8点伤害3次。',{route:'knowledge',turns:1,systems:['partner','stage','growth'],triggerRole:'跨体系联动',reason:'把知识连打积累转换为箭矢多段伤害，主动加速决定兑现回合。',systemRoles:{partner:{role:'扳机收益',port:'输出'},stage:{role:'扳机触发',port:'输出'},growth:{role:'触发+收益',port:'输出'}},rules:'每份牌效执行时读取自己当前回合实际知识出牌次数K，先为此实际牌增加K点伤害加成，再执行3段伤害。重放分别执行，因此会再次读取K并增加存量；正常布置时不提前执行这些指令。本牌无独立知识组，不自带知识关键词。'});
add('JX06','能力','罕见',2,'箭矢完成后，手牌中的知识牌获得2点伤害加成。',undefined,{route:'speed',upgradedCost:1,systems:['stage','partner','growth'],triggerRole:'跨体系联动',reason:'用箭矢完成为下一批知识输出投资，连接两个体系的先后节奏。',termIds:['TR28'],systemRoles:{stage:{role:'扳机收益',port:'输出'},partner:{role:'扳机收益',port:'输出'},growth:{role:'扳机触发',port:'输出'}},rules:'每个己方布置正常完成后，快照当时手牌中的知识实际对象，仅合法伤害加成载体各获得2点增量；重放与额外触发不另记完成。'});
add('JX07','箭矢','普通',2,'获得14点格挡。手牌中耗能不低于2的牌获得保留。','获得19点格挡。手牌中耗能不低于2的牌获得保留。',{route:'high',turns:1,systems:['stage'],archetypeIds:['RT_ACCEL'],triggerRole:'跨体系联动',port:'防御／运转',reason:'用完成后的防御保护高费组件，允许跨回合安排支付。',rules:'每份牌效先获得格挡，再按当时当前耗能快照自己的手牌；耗能不低于2者获得本场保留。X费按现有X费比较规则不作为固定数值2处理，之后入手对象不追溯。'});
add('JX08','技能','罕见',2,'所有箭矢获得等同于其耗能的伤害加成。消耗。','所有箭矢获得等同于其耗能2倍的伤害加成。消耗。',{route:'high',systems:['stage','growth'],triggerRole:'跨体系联动',reason:'让在场高费箭矢的费用字段成为输出投资依据。',systemRoles:{stage:{role:'扳机收益',port:'输出'},growth:{role:'扳机触发',port:'输出'}},rules:'快照自己所有有效箭矢布置并按实际牌去重，读取每张当前修正耗能C，合法伤害载体获得C或2C点加成。读取的是当前耗能字段，不是此前实际支付量；0费为0，X费不转为固定数值。'});
add('JX09','攻击','罕见',2,'造成16点伤害。手牌中每有1张耗能不低于2的知识牌，额外造成1次伤害。','造成21点伤害。手牌中每有1张耗能不低于2的知识牌，额外造成1次伤害。',{route:'high',systems:['partner','growth'],triggerRole:'跨体系联动',reason:'奖励保留高费知识组件，通过伤害次数利用集中投资。',systemRoles:{partner:{role:'扳机收益',port:'输出'},growth:{role:'扳机收益',port:'输出'}},rules:'伤害指令执行时，读取自己手牌中知识且当前固定耗能不低于2的实际对象数H，基础次数1+H；当前牌本身已离手，X费不按固定2费处理。与JG03、JG23等有效次数增量相加。'});
add('JX10','能力','稀有',3,'每回合打出的第1张知识箭矢牌获得重放1，并立即加速到完成。',undefined,{route:'repeat',upgradedCost:2,systems:['stage','partner'],triggerRole:'跨体系联动',reason:'把知识组的箭矢组件变成每回合一次的即时重复兑现入口。',systemRoles:{partner:{role:'扳机收益',port:'运转'},stage:{role:'扳机收益',port:'输出／防御／运转'}},rules:'按每回合实际知识箭矢出牌序号判断，安装前的出牌也计入。满足条件的牌正常支付并建立布置，先增加重放1，再立即加速到完成；该完成及额外执行不产生新的出牌序号。每个有效来源沿已有规则处理。'});

// 高费是费用倾向与连接副轴，不扩张成独立资源体系。
add('JH10','攻击','普通',3,'造成26点伤害。下回合获得1点能量。','造成34点伤害。下回合获得1点能量。',{route:'high',port:'输出／运转',reason:'以当前高费输出换取下回合展开空间，提供可独立抓取的高费触发物。'});
add('JH11','技能','普通',2,'获得16点格挡。下回合获得1点能量。','获得22点格挡。下回合获得1点能量。',{route:'high',port:'防御／运转',reason:'支付高费准备后续回合，补慢启动防线。'});
add('JH12','技能','罕见',1,'选择1张手牌，本场战斗耗能增加1并获得重放1。消耗。',undefined,{route:'high',upgradedCost:0,systems:['stage'],archetypeIds:['RT_REPEAT'],triggerRole:'跨体系联动',port:'运转',reason:'主动提高费用来换取重复牌效，连接高费触发与关键载荷。',rules:'选择自己的1张实际手牌，记录本场费用增量+1和重放增量+1。普通攻击、技能沿额外牌效规则，箭矢在之后布置时记录并延迟执行；能力沿现有重放安装规则。实际支付读取之后当前费用，不立刻打出目标牌。'});
add('JH13','攻击','罕见',2,'造成8点伤害。每有1点能量，额外造成1次伤害。','造成11点伤害。每有1点能量，额外造成1次伤害。',{route:'high',triggerRole:'扳机收益',reason:'把跨回合能量与当前能量储备转为多段输出。',rules:'本牌支付费用后，在伤害指令开始时读取当前剩余能量E，基础次数1+E；不消耗这些额外能量，与其他次数修正相加。'});
add('JH14','技能','罕见',0,'下1张耗能不低于2的牌耗能减少2。消耗。','下1张耗能不低于2的牌耗能减少3。消耗。',{route:'high',port:'运转',reason:'一次性打开高费组件的启动窗口。',rules:'建立一次跨回合准备；支付自己的下一张牌之前，以未应用此准备的当前耗能判断是否不低于2，满足才应用减费并消耗准备，最低0。不因减少到2以下而撤销资格；免费打出和X费沿现有支付规则处理。'});
add('JH15','能力','罕见',2,'每回合第1次打出耗能不低于3的牌后，抽2张牌。',undefined,{route:'high',upgradedCost:1,triggerRole:'扳机收益',port:'运转',reason:'用高费实际出牌承接后续手牌，限制持续抽牌频率。',rules:'以该次实际出牌的当前费用字段判断，不读取实际支付能量；本回合安装前的符合条件出牌也计入序号。每份能力仅响应第1次，免费但当前费用仍不低于3的牌满足，X费另按原X费条件处理。'});
add('JH16','技能','稀有','X','本回合，你打出的下X张牌获得重放1。消耗。','本回合，你打出的下X+1张牌获得重放1。消耗。',{route:'high',systems:['stage'],archetypeIds:['RT_REPEAT'],port:'运转',reason:'将额外能量变成后续关键牌的结算机会，提供X费承接。',rules:'按本体X费支付规则确定X，建立本回合剩余次数X或X+1的准备。随后每张实际打出的牌消耗一次准备并获得重放1；不包含当前JH16本身。箭矢在布置前记录，重放不再次消耗准备。回合结束清理剩余次数。'});
add('JH17','箭矢','罕见',3,'获得24点格挡。下回合获得2点能量。','获得32点格挡。下回合获得2点能量。',{route:'high',turns:2,systems:['stage'],archetypeIds:['RT_ACCEL'],port:'防御／运转',reason:'高费防御载荷与未来能量结合，为加速提供不同于伤害的目标。',rules:'格挡在箭矢实际牌效执行时获得；随后预约该次执行之后的下个自己回合获得2能量，不按最初布置回合计算。重放各预约一次，正常完成后沿原去向。'});

// 体系外补洞：使用独立攻防与工具，不添加其他职业的成套触发收益。
add('JF01','攻击','普通',1,'造成11点伤害。','造成15点伤害。',{reason:'前期独立输出，减少必须先启动能力才能战斗的卡位。'});
add('JF02','攻击','普通',1,'对所有敌人造成7点伤害。','对所有敌人造成10点伤害。',{reason:'补充不依赖箭矢场面的低费群体伤害。'});
add('JF03','技能','普通',1,'获得10点格挡。','获得14点格挡。',{port:'防御',reason:'低费用即时防御，保护箭矢等待与知识能力启动。'});
add('JF04','攻击','普通',1,'造成6点伤害。给予1层虚弱。','造成9点伤害。给予1层虚弱。',{reason:'前期进攻与削弱兼用，仅作功能补洞，不构建虚弱流派。'});
add('JF05','技能','罕见',2,'保留。获得18点格挡。','保留。获得24点格挡。',{port:'防御',reason:'能等到需要防御的回合再支付，补延迟防御的时点缺口。'});
add('JF06','技能','罕见',1,'选择1张手牌，使其获得保留。获得1点能量。消耗。',undefined,{upgradedCost:0,port:'运转',reason:'保留关键组件并让渡当回合支付空间，作为一次性工具。',rules:'选择自己的1张手牌赋予本场保留，然后获得1能量；没有合法手牌时保留指令跳过，后续能量照常结算。本牌最终消耗。'});
add('JF07','技能','罕见',2,'抽4张牌。消耗。','抽5张牌。消耗。',{port:'运转',reason:'一次性爆发抽牌，服务需要多个组件同时到手的构筑。'});
add('JF08','攻击','罕见',2,'移除目标的格挡。造成15点伤害。','移除目标的格挡。造成20点伤害。',{termIds:['EF73'],reason:'面对高格挡敌人的独立解法，与次数或成长构筑保持功能差别。',rules:'先移除当前敌人的格挡，再执行本牌伤害指令；不将移除量记为伤害，不发布击杀或伤害加成事件。'});
add('JF09','技能','罕见',3,'获得28点格挡。下回合抽2张牌。消耗。','获得36点格挡。下回合抽2张牌。消耗。',{port:'防御／运转',reason:'用一个高费回合保护生命并准备下一回合，兼作高费触发物。',archetypeIds:['RT_HIGH'],rules:'当前获得格挡，并预约自己的下回合开始时额外抽2张牌。按下回合实际手牌容量处理；本牌消耗，不建立持续抽牌引擎。'});
add('JF10','技能','罕见',1,'移除自己的所有虚弱和脆弱。消耗。',undefined,{upgradedCost:0,port:'防御／输出',termIds:['EF74'],reason:'处理长战中压低攻防效率的负面状态，不增加状态收益流派。',rules:'仅移除自己当前虚弱与脆弱的全部层数，其他状态保持；不因移除产生新的给予状态事件。'});

// 起始体验：先布置，再用基础攻击加速，留出基础防御。
add('JB01','攻击','基础',1,'造成6点伤害。','造成9点伤害。',{route:'starter',name:'打击',reason:'通用基础攻击，起始牌组4张。',pool:false,role:'起始牌'});
add('JB02','技能','基础',1,'获得5点格挡。','获得8点格挡。',{route:'starter',name:'防御',port:'防御',reason:'通用基础防御，起始牌组4张。',pool:false,role:'起始牌'});
add('JB03','箭矢','基础',1,'造成10点伤害。','造成14点伤害。',{route:'speed',turns:1,reason:'起始伤害箭矢，展示正常等待与主动完成的差别。',pool:false,role:'起始牌'});
add('JB04','攻击','基础',1,'造成4点伤害。一个箭矢加速1回合。','造成6点伤害。一个箭矢加速1回合。',{route:'speed',reason:'起始加速来源，与JB03组成2能量的即时兑现。',pool:false,role:'起始牌'});

// 远古牌保持专属获取，覆盖箭矢集中展开与知识组升级两种职业上限。
add('JZ01','技能','远古',3,'将抽牌堆中的所有箭矢牌加入手牌，本回合耗能变为0。消耗。',undefined,{route:'speed',upgradedCost:2,port:'运转',role:'机制中坚牌',reason:'专属获取的集中展开牌，把已有箭矢库存变为一个回合的部署机会。',termIds:['EF14','EF48'],rules:'快照自己抽牌堆中当前为箭矢类型的实际对象，逐张移入手牌并赋予本回合0费修正；按原抽牌堆顺序处理，满手沿已有规则进入弃牌堆。该移动不是抽牌、生成或获得，不触发知识发放。0费只改变能量支付，初始回合数与目标仍正常处理。本牌排除常规卡牌奖励，仅从远古专属获取。'});
add('JZ02','技能','远古',3,'升级所有知识牌。每升级1张，获得1点能量。消耗。',undefined,{route:'knowledge',upgradedCost:2,port:'运转／输出／防御',role:'机制中坚牌',reason:'专属获取的知识组整备，补偿多张组件各自升级的资源需求。',termIds:['EF24','EF09'],rules:'快照自己的手牌、抽牌堆、弃牌堆、消耗牌堆、有效箭矢及已安装能力的知识实际来源牌并去重，对每张可升级牌执行一次标准战斗内升级；实际升级成功才获得1能量。已经升级的牌跳过，不突破升级上限。在场箭矢同步升级载荷，保持剩余计数与目标；已安装能力只同步升级字段，不重新执行安装、回合开始或其他牌效。多个效果指向同一实际来源时只升级并返还能量一次。战后永久牌组保持原升级状态。本牌排除常规卡牌奖励，仅从远古专属获取。'});

const existingRoutes={
  JA02:['RT_ACCEL','RT_REPEAT'],JA04:['RT_ACCEL'],JA13:['RT_ACCEL'],JA15:['RT_ACCEL','RT_REPEAT'],JA16:['RT_ACCEL'],JA17:['RT_ACCEL'],JA22:['RT_ACCEL','RT_REPEAT'],JA35:['RT_ACCEL'],
  JK05:['RT_KNOWLEDGE'],JK10:['RT_HIGH','RT_KNOWLEDGE'],JK10B:['RT_HIGH','RT_KNOWLEDGE'],JK11:['RT_LINK'],JK11B:['RT_LINK'],JK15:['RT_KNOWLEDGE'],JK15B:['RT_KNOWLEDGE'],A:['RT_KNOWLEDGE'],B:['RT_KNOWLEDGE'],
  JK27:['RT_LINK','RT_KNOWLEDGE'],JK32:['RT_LINK','RT_HIGH'],JK32B:['RT_LINK','RT_ACCEL'],JK32C:['RT_LINK'],JK33:['RT_LINK'],JK33B:['RT_LINK'],JK37:['RT_ACCEL'],JK37B:['RT_ACCEL'],JK37C:['RT_KNOWLEDGE'],JK43:['RT_LINK','RT_HIGH'],JK43B:['RT_LINK'],
  JK44:['RT_KNOWLEDGE'],JK44B:['RT_KNOWLEDGE'],JK45:['RT_LINK','RT_ACCEL'],JK45B:['RT_LINK'],JK46:['RT_KNOWLEDGE'],JK46B:['RT_REPEAT','RT_HIGH'],JK47:['RT_LINK'],JK47B:['RT_LINK'],JK48:['RT_LINK'],JK48B:['RT_LINK'],JK49:['RT_LINK','RT_HIGH'],JK49B:['RT_LINK'],JK50:['RT_LINK','RT_HIGH'],JK50B:['RT_LINK','RT_HIGH'],
  JH01:['RT_HIGH'],JH05:['RT_HIGH'],JT01:['RT_ACCEL','RT_HIGH'],JT02:['RT_HIGH'],JT03:['RT_ACCEL']
};
for(const [id,ids] of Object.entries(existingRoutes)){
  const c=before.cards.find(c=>c.id===id);if(!c||c.trashedAt||c.reservedAt)continue;
  const values={archetypeIds:[...new Set([...c.archetypeIds,...ids])]};
  updates.push({collection:'cards',id,values,expected:{archetypeIds:c.archetypeIds,text:c.text,upgradedText:c.upgradedText,type:c.type,cost:c.cost,upgradedCost:c.upgradedCost},expectedAbsent:['trashedAt','reservedAt']});
}
const startingDeck=[{cardId:'JB01',count:4},{cardId:'JB02',count:4},{cardId:'JB03',count:1},{cardId:'JB04',count:1}];
updates.push({collection:'root',id:'meta',values:{startingDeck},expected:{startingDeck:before.meta.startingDeck}});
const data=structuredClone(before);data.cards.push(...cards);data.partnerGroups.push(...groups);data.archetypes.push(...routeData);data.terms.push(...terms);
for(const u of updates){const e=u.collection==='root'?data[u.id]:data[u.collection].find(e=>e.id===u.id);Object.assign(e,structuredClone(u.values));}
const app=fs.readFileSync(path.join(here,'app.js'),'utf8');
const context={seed:{data},clone:structuredClone,uid:()=>{throw Error('缺少稳定编号');},COLLECTIONS:['mechanisms','terms','cards','partnerGroups','archetypes','styles'],TYPES:['攻击','技能','能力','箭矢'],RARITIES:['基础','普通','罕见','稀有','远古']};
vm.createContext(context);vm.runInContext(app.slice(app.indexOf('  function normalizeData('),app.indexOf('  function patchesBetween(')),context);
const normalized=JSON.parse(JSON.stringify(context.normalizeData(data)));
const ids=new Set(normalized.cards.map(c=>c.id)), termIds=new Set(normalized.terms.map(t=>t.id)), archIds=new Set(normalized.archetypes.map(a=>a.id));
assert.equal(cards.length,82);assert.equal(groups.length,5);assert.equal(startingDeck.reduce((n,e)=>n+e.count,0),10);
for(const old of before.cards){const c=normalized.cards.find(c=>c.id===old.id), expected=structuredClone(old);const u=updates.find(u=>u.collection==='cards'&&u.id===old.id);if(u)Object.assign(expected,u.values);assert.deepEqual(c,expected,'保留已有牌面与状态：'+old.id);}
assert.deepEqual(normalized.styles,before.styles);assert.deepEqual(normalized.mechanisms,before.mechanisms);
for(const c of cards){for(const id of c.termIds)assert(termIds.has(id),c.id+' 无效词条 '+id);for(const id of c.archetypeIds)assert(archIds.has(id),c.id+' 无效流派 '+id);assert(c.design.roleReason,c.id);assert(c.design.triggerRole,c.id);if(c.type==='箭矢')assert(['攻击','技能','能力'].includes(c.arrowSubtype),c.id);assert(!(c.type==='能力'&&c.rarity==='普通'),c.id);assert(!c.text.includes('所有玩家'),c.id);assert(c.text.length<100&&c.upgradedText.length<110,c.id);}
for(const g of groups){const root=normalized.cards.find(c=>c.id===g.mainCardId);assert(root.pool);for(const id of g.grantedCardIds){assert(ids.has(id));const c=normalized.cards.find(c=>c.id===id);assert(!c.pool);assert.equal(c.rarity,root.rarity);assert(c.text.startsWith('知识。'));}}
for(const e of startingDeck){assert(ids.has(e.cardId));assert.equal(normalized.cards.find(c=>c.id===e.cardId).rarity,'基础');}
const batches=[
  {id:batchId+'-routes',label:'新增流派筛选：加速、重放、调度、知识运转与高费',collection:'archetypes',ids:routeData.map(e=>e.id),entries:routeData},
  {id:batchId+'-terms',label:'补充受到攻击后的内部事件分类',collection:'terms',ids:terms.map(e=>e.id),entries:terms},
  {id:batchId+'-indices',label:'同步已有卡牌的流派筛选',updates:updates.filter(u=>u.collection==='cards'),applyUpdatesOnImport:true},
  {id:batchId+'-cards',label:'补齐完整卡池：82张牌面',collection:'cards',ids:cards.map(e=>e.id),entries:cards},
  {id:batchId+'-groups',label:'新增五组知识配套',collection:'partnerGroups',ids:groups.map(e=>e.id),entries:groups},
  {id:batchId+'-config',label:'配置十张起始牌组',updates:updates.filter(u=>u.collection==='root'),applyUpdatesOnImport:true}
];
const active=normalized.cards.filter(c=>!c.trashedAt&&!c.reservedAt), pool=active.filter(c=>c.pool&&!['基础','远古'].includes(c.rarity)), count=(list,key)=>Object.fromEntries([...new Set(list.map(c=>c[key]))].map(v=>[v,list.filter(c=>c[key]===v).length]));
const summary={newCards:cards.length,newRewardEntries:cards.filter(c=>c.pool&&!['基础','远古'].includes(c.rarity)).length,newKnowledgeGroups:groups.length,newBasicFaces:4,newAncientFaces:2,activeFaces:active.length,rewardEntries:pool.length,poolEntries:active.filter(c=>c.pool).length,ancientEntries:active.filter(c=>c.rarity==='远古').length,reserve:normalized.cards.filter(c=>!c.trashedAt&&c.reservedAt).length,trash:normalized.cards.filter(c=>c.trashedAt).length,total:normalized.cards.length,byRarity:count(pool,'rarity'),byCost:count(pool,'cost'),byType:count(pool,'type')};
const coverage=[
  {route:'加速完成',sources:['JB03','JB04','JA38','JA40','JA42','JA43','JA44','JA48','JA52'],payoffs:['JA39','JA45','JA46','JA47','JA53'],ports:['输出','防御','运转'],keyExisting:['JA04','JA22','JA35'],bridges:['JX01','JK64','JX06','JH17'],loop:'稳定布置与多来源加速，完成后接防御、次数兑现和补充手牌。'},
  {route:'重放触发',sources:['JA49','JA50','JA54','JA55','JH12'],payoffs:['JA41','JA51','JA57','JK67B','JK69C'],ports:['输出','防御','运转'],keyExisting:['JG03','JA22','JK46B'],bridges:['JA56','JX10','JH16'],loop:'选择高质量载荷，投入重放或额外触发，利用多次牌效收回输出与资源。'},
  {route:'关联调度',sources:['JK55','JK58','JK61','JK62','JK66B','JK69','JK70'],payoffs:['JK59','JK65','JK66','JK68','JK70B'],ports:['输出','防御','运转'],keyExisting:['JK11','JK27','JK32','JK43','JK49','JK50'],bridges:['JK54','JX02','JX03','JX04'],loop:'取回、保留与安排抽牌凑齐组件，用组内顺序与到手条件获得回报。'},
  {route:'知识运转',sources:['JK54','JK55','JK60','JK61','JK62','JK66','JK68B','JK68C'],payoffs:['JK56','JK57','JK59','JK63','JK65','JK68'],ports:['输出','防御','运转'],keyExisting:['A','B','JK15','JK27','JK37C'],bridges:['JK64','JX05','JX06','JX09','JX10'],loop:'知识来源、回收与抽取提供实际出牌，能量和防线支撑连打，不监听消耗或弃牌。'},
  {route:'伤害加成',sources:['JG27','JK58','JK65','JK67','JX01','JX04','JX05','JX06','JX08'],payoffs:['JG28','JG29','JG30','JA41','JA51','JK63','JK67B','JX09'],ports:['输出'],keyExisting:['JG03','JG09','JG16','JG23','JK33'],bridges:['JA56','JK58','JK65','JX01','JX04','JX05','JX06','JX08'],loop:'投资来源主要散布在箭矢与知识牌，少量专用牌负责多段、倍率和范围兑现。'},
  {route:'高费配合',sources:['JH10','JH11','JH12','JH17','JF09','JX07'],payoffs:['JH13','JH15','JH16','JX08','JX09'],ports:['输出','防御','运转'],keyExisting:['JK10','JH01','JH05','JK32','JK43','JK50'],bridges:['JX07','JX08','JX09','JH12','JH17'],loop:'高费牌与主动加费提供条件，预约能量、一次性减费与X费承接组织爆发回合。'}
];
const legacyTriggerRoles={
  JA02:'扳机触发',JA04:'扳机触发',JA13:'扳机触发',JA15:'扳机触发',JA16:'扳机触发',JA17:'扳机触发',JA22:'扳机收益',JA35:'扳机触发',
  JG03:'扳机收益',JG09:'扳机触发',JG16:'扳机触发',JG23:'扳机收益',JH01:'跨体系联动',JH05:'扳机收益',JT01:'跨体系联动',JT02:'跨体系联动',JT03:'触发+收益',
  JK05:'扳机触发',JK10:'扳机收益',JK10B:'扳机触发',JK11:'扳机触发',JK11B:'扳机触发',JK15:'扳机收益',JK15B:'扳机触发',A:'扳机触发',B:'扳机触发',JK27:'触发+收益',
  JK32:'跨体系联动',JK32B:'跨体系联动',JK32C:'扳机触发',JK33:'跨体系联动',JK33B:'跨体系联动',JK37:'扳机触发',JK37B:'扳机触发',JK37C:'扳机收益',
  JK43:'扳机收益',JK43B:'扳机触发',JK44:'扳机触发',JK44B:'扳机触发',JK45:'扳机触发',JK45B:'扳机收益',JK46:'扳机触发',JK46B:'跨体系联动',
  JK47:'扳机触发',JK47B:'扳机触发',JK48:'扳机触发',JK48B:'扳机触发',JK49:'扳机触发',JK49B:'扳机触发',JK50:'扳机收益',JK50B:'跨体系联动'
};
const cardRoles=active.map(c=>({id:c.id,isNew:c.draftBatch===batchId,classification:c.role,triggerRole:c.design?.triggerRole||legacyTriggerRoles[c.id]||'待复核',ports:c.design?.port||(/获得\d+点格挡/.test(c.text)?'防御':/造成\d+点伤害/.test(c.text)?'输出':'运转'),systems:c.design?.systems||c.archetypeIds.filter(id=>id.startsWith('SYS_')).map(id=>id.slice(4)),routes:c.archetypeIds.filter(id=>id.startsWith('RT_')),pool:c.pool,rarity:c.rarity}));
assert(!cardRoles.some(c=>c.triggerRole==='待复核'),'当前牌需要定位记录');
const report={schemaVersion:1,id:batchId,date:'2026-11-02',title:'纯那完整卡池初稿',sourceRevision:live.revision,scope:'补齐流派来源、触发、收益、运转、连接、起始与体系外功能；数量不设预定上限。',sources:[
  'D:/Github/STS2_Card_Reserch/docs/体系分析流程.md','D:/Github/STS2_Card_Reserch/docs/体系驱动卡池设计流程.md','D:/Github/STS2_Card_Reserch/docs/职业卡池设计要求_按五职业规律.md',
  'D:/Godot/Proj/STS2_ShoujoKagekiAijoNana/docs/design/五职业与Karen流派结构复核及奈奈构筑深化.md','纯那Mod设计工作台_本地编辑.json'
],userOverrides:{quantity:'本阶段尽量多生成，不受预定卡牌数量限制。',breakthrough:'以用户给出的修改规则本身为判定，不采用文档中把高数值单独排除的旧推论。',bonus:'伤害加成只负责输出，来源主要分布在其他体系。',style:before.styles},summary,coverage,cardRoles,startingDeck,newRoutes:routeData,newCards:normalized.cards.filter(c=>cards.some(n=>n.id===c.id)),newGroups:groups,
  review:{method:'同一助手依次进行体系、规则、原版结构对照、平衡、文本与关系审稿。',stage:'完整卡池初版',structuralStatus:'来源、收益、端口、跨体系连接及起始功能齐备。',quantityPolicy:'不按88张或各稀有度配额裁剪。',commonAbilities:0,nonRareDraw:active.filter(c=>['普通','罕见'].includes(c.rarity)&&/抽\d+张牌|抽取/.test(c.text)).map(c=>c.id),dedicatedBonusCards:['JG27','JG28','JG29','JG30'],
    paperExamples:[{cards:['JB03','JB04'],cost:2,result:'基础伤害箭矢10，加速攻击4，即时合计14伤害。'},{cards:['JA41','JX01'],cost:3,result:'先布置5点伤害3次，再加成2并加速完成，造成21点伤害并获得7点格挡。'},{cards:['JK68','JK68C','JK68B'],cost:2,result:'先布置A，再用C降费B，B免费打出并加速A；两种使用记录满足后A造成14×3，连同B共49伤害，C提供3格挡。'}],
    relationshipReview:[{cards:['JA17','JA42'],decision:'JA42保留单体集中伤害，目标箭矢加速提高为2；JA17维持群体伤害与全体敌方目标加速1，形成明确选择。'},{cards:['JA16','JF01'],decision:'独立攻击提高至11/15伤害，保留JA16的9/12伤害加速功能，形成即时数值与机制动作取舍。'},{cards:['JK55','JF03'],decision:'纯防御提高至10/14格挡，知识回收保持7/10格挡加回收，形成数值与调度取舍。'},{cards:['JG28','JG29'],decision:'贡献倍率设置取最大值，保持存量与JG23次数读取独立。'}],
    retainedUserStates:'所有旧牌面、升级、费用、垃圾桶和备用状态保持；只补充有效牌的流派筛选。',knownInteraction:'原有JK15、JK37B、JK37C在复制与定向抽牌的事件顺序下可形成高频循环；本轮保留用户牌面，不用逐回合上限改写旧牌。新牌的资源、自然完成、额外触发、重放与实际出牌分别结算。',deferred:['实战数值、难度与稀有度微调','原作正式牌名与出处'],verification:'本轮只进行新增数据、升级、引用、关联、旧编辑保留及工作台导入显示的必要校验；未运行完整游戏测试。'}
};
write(path.join(dataDir,'纯那完整卡池初稿_20261102.json'),report);
write(path.join(here,'.tmp-complete-pool-before.json'),{revision:live.revision,data:before,expected:normalized,batchIds:batches.map(b=>b.id)});
for(const key of ['data','revision','history','undoStack','redoStack','ui','appliedSeedBatches'])if(live[key]!==undefined)seed[key]=structuredClone(live[key]);
seed.assets={...seed.assets,...live.assets};seed.seedBatches=[...live.seedBatches,...batches];write(seedFile,seed);
const trackingFile=path.join(dataDir,'纯那职业卡池设计记录.json'),tracking=read(trackingFile);
tracking.stage='完整卡池初版';tracking.status='触发、收益、跨流派、起始和体系外功能已补齐，等待工作台导入';tracking.requirements=tracking.requirements.map(x=>x.includes('不补起始牌组')?'本阶段完成整池设计，可尽量多生成，数量不受预定卡牌数量限制。':x);
tracking.batches=tracking.batches.filter(b=>b.id!==batchId);tracking.batches.push({id:batchId,date:'2026-11-02',sourceRevision:live.revision,...summary,source:'纯那完整卡池初稿_20261102.json'});
tracking.fullPoolDraft={source:'纯那完整卡池初稿_20261102.json',coverage,summary,startingDeck,verification:report.review.verification};write(trackingFile,tracking);
console.log(JSON.stringify({sourceRevision:live.revision,...summary,oldFacesPreserved:true}));
