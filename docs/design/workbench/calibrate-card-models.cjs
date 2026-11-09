'use strict';
const assert=require('node:assert/strict');

function calibrateCardModels(previous,native){
  const cards=new Map(native.cards.map(card=>[card.id,card]));
  const get=id=>{const card=cards.get(id);assert(card,'Missing native reference: '+id);return card;};
  const pair=values=>values.join('／'),round=values=>values.map(Math.round);
  const damage=id=>{const c=get(id);return[c.text,c.upgradedText].map(text=>Number(text.match(/造成(\d+)点伤害/)[1]));};
  const total=id=>{const c=get(id);assert(c.metric&&c.upgradedMetric,id+' metric');return[c.metric.total,c.upgradedMetric.total];};
  const checked=(id,cost,rarity,hits,kind)=>{
    const c=get(id);assert.equal(c.cost,cost,id+' cost');assert.equal(c.upgradedCost,cost,id+' upgraded cost');assert.equal(c.rarity,rarity,id+' rarity');assert(!c.starCost&&!c.upgradedStarCost,id+' star cost');assert.deepEqual(c.keywords,[],id+' keywords');assert.equal(c.metric.kind,kind,id+' target/effect');if(hits){assert.equal(c.metric.hits,hits,id+' hits');assert.equal(c.upgradedMetric.hits,hits,id+' upgraded hits');}return c;
  };
  checked('SLICE',0,'普通',1,'attack');checked('DEFLECT',0,'普通',null,'block');checked('LEAP',1,'普通',null,'block');
  checked('TWIN_STRIKE',1,'普通',2,'attack');checked('DAGGER_SPRAY',1,'普通',2,'aoe');
  const celestial=get('CELESTIAL_MIGHT');assert.equal(celestial.cost,2);assert.equal(celestial.upgradedCost,2);assert.equal(celestial.rarity,'普通');assert(!celestial.starCost&&!celestial.upgradedStarCost);assert.deepEqual(celestial.keywords,[]);assert.equal(celestial.metric.kind,'attack');assert.equal(celestial.metric.hits,3);assert.equal(celestial.upgradedMetric.hits,4);
  const bomb=get('THE_BOMB');assert.equal(bomb.cost,2);assert.equal(bomb.upgradedCost,2);assert.equal(bomb.rarity,'罕见');assert(!bomb.starCost&&!bomb.upgradedStarCost);

  // Matched scope pair: identical cost, rarity, hit count and keywords.
  const twin=total('TWIN_STRIKE'),dagger=total('DAGGER_SPRAY'),scope=twin.map((v,i)=>v/dagger[i]);
  const slice=total('SLICE'),deflect=total('DEFLECT'),leap=total('LEAP'),celestialTotal=total('CELESTIAL_MIGHT'),bombTotal=damage('THE_BOMB');
  const hitPenalty=hits=>hits*(hits-1)/2;
  const twinSingle=twin.map(v=>v+hitPenalty(2)),celestialSingle=celestialTotal.map((v,i)=>v+hitPenalty(i===0?3:4));
  const single=[slice,twinSingle,celestialSingle,...[3,4].map(cost=>celestialSingle.map((v,i)=>v+(cost-2)*(v-twinSingle[i])))];
  // The native scope ratio is measured at two hits. Remove that shape's
  // premium before applying the ratio, then restore the single-hit budget.
  const aoe=single.map(values=>values.map((v,i)=>Math.round((v-hitPenalty(2))/scope[i])+hitPenalty(2)));
  const block=[0,1,2,3,4].map(cost=>deflect.map((v,i)=>v+cost*(leap[i]-v)));
  const instant={attack:single,aoe,block};
  const idsForCost=cost=>cost===0?['SLICE']:cost===1?['TWIN_STRIKE']:['TWIN_STRIKE','CELESTIAL_MIGHT'];
  const unique=ids=>[...new Set(ids)];
  const names=ids=>unique(ids).map(id=>{const c=get(id);return(c.name||id)+'（'+c.rarity+'，'+c.cost+'费）';}).join('、');
  const ref=(ids,status,note)=>({ids:unique(ids),status,note});
  const costSources={attack:[],aoe:[],block:[]};
  for(const cost of [0,1,2,3,4]){
    let note;
    if(cost===0)note='切割为0费普通单段，基础'+slice[0]+'，升级'+slice[1]+'，无附效。';
    else if(cost===1)note='双重打击为1费普通两段：'+damage('TWIN_STRIKE')[0]+'×2＝'+twin[0]+'，升级'+damage('TWIN_STRIKE')[1]+'×2＝'+twin[1]+'。段数扣减为设计参数：p(n)＝n×(n−1)÷2。反推单段预算：基础'+twin[0]+'＋p(2)＝'+twinSingle[0]+'；升级'+twin[1]+'＋p(2)＝'+twinSingle[1]+'。';
    else if(cost===2)note='天穹之力为2费普通，基础'+celestial.metric.value+'×3＝'+celestialTotal[0]+'，升级'+celestial.upgradedMetric.value+'×4＝'+celestialTotal[1]+'。段数扣减为设计参数：p(n)＝n×(n−1)÷2。按原生段数反推单段预算：基础'+celestialTotal[0]+'＋p(3)＝'+celestialSingle[0]+'；升级'+celestialTotal[1]+'＋p(4)＝'+celestialSingle[1]+'。固定段数模板再按各自段数扣减。';
    else note='先按段数扣减设计参数p(n)＝n×(n−1)÷2，将双重打击两段总伤'+pair(twin)+'加回1点，得到1费单段预算'+pair(twinSingle)+'；将天穹之力基础三段总伤'+celestialTotal[0]+'加回3点、升级四段总伤'+celestialTotal[1]+'加回6点，得到2费单段预算'+pair(celestialSingle)+'。沿最后一个费用增量外推：基础'+celestialSingle[0]+'＋'+(cost-2)+'×（'+celestialSingle[0]+'−'+twinSingle[0]+'）＝'+single[cost][0]+'；升级'+celestialSingle[1]+'＋'+(cost-2)+'×（'+celestialSingle[1]+'−'+twinSingle[1]+'）＝'+single[cost][1]+'。';
    costSources.attack[cost]=ref(idsForCost(cost),cost===0?'nativeExact':cost>2?'extrapolation':'estimate',note);
    costSources.aoe[cost]=ref([...idsForCost(cost),'TWIN_STRIKE','DAGGER_SPRAY'],'estimate',note+'\n单／群换算来自同条件两段牌：双重打击总伤'+pair(twin)+'，匕首雨总伤'+pair(dagger)+'。先减去两段扣减p(2)＝1，按两段配对比换算，再加回1得到单段预算。基础群伤＝round(（'+single[cost][0]+'−1）×'+dagger[0]+'÷'+twin[0]+')＋1＝'+aoe[cost][0]+'；升级群伤＝round(（'+single[cost][1]+'−1）×'+dagger[1]+'÷'+twin[1]+')＋1＝'+aoe[cost][1]+'。段数扣减为设计参数；将配对比迁移到其他费用或等待属于推算。');
    costSources.block[cost]=ref(cost===0?['DEFLECT']:cost===1?['LEAP']:['DEFLECT','LEAP'],cost<2?'nativeExact':'extrapolation',cost<2?(cost===0?'偏折':'飞跃')+'为普通即时纯格挡，基础'+block[cost][0]+'，升级'+block[cost][1]+'。':'用偏折0费'+pair(deflect)+'、飞跃1费'+pair(leap)+'建立格挡费用函数：基础'+deflect[0]+'＋'+cost+'×（'+leap[0]+'−'+deflect[0]+'）＝'+block[cost][0]+'；升级'+deflect[1]+'＋'+cost+'×（'+leap[1]+'−'+deflect[1]+'）＝'+block[cost][1]+'。高费为外推，不采用自损价值的固定扣减。');
  }

  // Normalize through a nonzero instant budget, rather than cost / 2, so a
  // zero-energy card retains its card-slot value. These transfers are estimates.
  const twoAoe=aoe.map(values=>round(values.map((v,i)=>bombTotal[i]*v/aoe[2][i])));
  const twoSingle=twoAoe.map(values=>values.map((v,i)=>Math.round((v-hitPenalty(2))*scope[i])+hitPenalty(2)));
  const blockFactors=[1,4/3,1.8,3],budgets={attack:[],aoe:[],block:[]};
  for(const kind of ['attack','aoe','block'])for(const cost of [0,1,2,3,4]){
    const two=kind==='attack'?twoSingle[cost]:kind==='aoe'?twoAoe[cost]:null;
    budgets[kind][cost]=[0,1,2,3].map(turns=>kind==='block'?round(block[cost].map(v=>v*blockFactors[turns])):turns===0?instant[kind][cost]:turns===1?round(instant[kind][cost].map((v,i)=>(v+two[i])/2)):turns===2?two:two.map(v=>v*2));
  }
  const bombNote='炸弹为2费罕见技能，基础'+bombTotal[0]+'、升级'+bombTotal[1]+'，每名敌人单次伤害。打出当回合即计数一次，跨过两次敌方行动后在再下回合结束时结算，计作2回合延迟；模板为普通箭矢，在再下回合开始结算。';
  function twoSource(kind,cost){
    const groupRef=cost===2?ref(['THE_BOMB'],'nativeNumeric',bombNote):ref([...costSources.aoe[cost].ids,...costSources.aoe[2].ids,'THE_BOMB'],'estimate',costSources.aoe[cost].note+'\n2费即时群伤归一基准：'+pair(aoe[2])+'。'+bombNote+'\n按即时费用预算比例迁移2回合群伤：基础'+bombTotal[0]+'×'+aoe[cost][0]+'÷'+aoe[2][0]+'四舍五入＝'+twoAoe[cost][0]+'；升级'+bombTotal[1]+'×'+aoe[cost][1]+'÷'+aoe[2][1]+'四舍五入＝'+twoAoe[cost][1]+'。该费用迁移为推算。');
    if(kind==='aoe')return groupRef;
    return ref([...groupRef.ids,'TWIN_STRIKE','DAGGER_SPRAY'],'estimate',groupRef.note+'\n单／群比例来自1费普通、两段、无附效的双重打击与匕首雨。先减去两段扣减p(2)＝1，换算后加回1。基础单体＝round(（'+twoAoe[cost][0]+'−1）×'+twin[0]+'÷'+dagger[0]+')＋1＝'+twoSingle[cost][0]+'；升级单体＝round(（'+twoAoe[cost][1]+'−1）×'+twin[1]+'÷'+dagger[1]+')＋1＝'+twoSingle[cost][1]+'。段数扣减为设计参数；将即时配对比迁移到延迟牌属于推算。');
  }
  function sourceFor(kind,cost,turns,hits){
    if(turns===0){
      if(cost===1&&hits===2&&kind!=='block')return ref([kind==='attack'?'TWIN_STRIKE':'DAGGER_SPRAY'],'nativeExact',kind==='attack'?'直接采用双重打击：基础5×2＝10；升级7×2＝14。':'直接采用匕首雨：基础4×2＝8；升级6×2＝12，每名敌人。');
      const source=costSources[kind][cost];return hits===1?source:ref(source.ids,'estimate',source.note+'\n固定'+hits+'段由上述单段预算扣除段数收益后推算。');
    }
    if(kind==='block')return ref([...costSources.block[cost].ids,'DODGE_AND_ROLL','GLITTERSTREAM'].filter(id=>cards.has(id)),turns===3?'designBoost':'designAssumption',costSources.block[cost].note+'\n等待参数为设计假设：1／2／3回合取即时格挡的4/3／1.8／3倍；闪躲翻滚与流光溢彩含即时及下回合两部分，不能直接确定纯延迟格挡倍率。\n基础'+block[cost][0]+'×'+Number(blockFactors[turns].toFixed(4))+'四舍五入＝'+budgets.block[cost][turns][0]+'；升级'+block[cost][1]+'×'+Number(blockFactors[turns].toFixed(4))+'四舍五入＝'+budgets.block[cost][turns][1]+'。');
    const two=twoSource(kind,cost);
    if(turns===2)return hits===1?two:ref(two.ids,'estimate',two.note+'\n固定多段由上述单段总预算推算。');
    if(turns===1)return ref([...costSources[kind][cost].ids,...two.ids],'estimate',costSources[kind][cost].note+'\n'+two.note+'\n1回合缺少同条件纯载荷锚点，取即时与2回合的中点：基础（'+instant[kind][cost][0]+'＋'+budgets[kind][cost][2][0]+'）÷2四舍五入＝'+budgets[kind][cost][1][0]+'；升级（'+instant[kind][cost][1]+'＋'+budgets[kind][cost][2][1]+'）÷2四舍五入＝'+budgets[kind][cost][1][1]+'。中点为设计插值。');
    return ref(two.ids,'designBoost',two.note+'\n3回合按用户要求独立增强，取2回合预算的2倍：基础'+budgets[kind][cost][2][0]+'×2＝'+budgets[kind][cost][3][0]+'；升级'+budgets[kind][cost][2][1]+'×2＝'+budgets[kind][cost][3][1]+'。');
  }
  function makePoint(kind,cost,turns,hits=1){
    const budget=budgets[kind][cost][turns],source=sourceFor(kind,cost,turns,hits),penalty=hitPenalty(hits),adjusted=budget.map(v=>Math.max(0,v-penalty)),perHit=adjusted.map(v=>Math.floor(v/hits));
    const point={base:perHit[0]*hits,upgraded:perHit[1]*hits,reference:source,sourceKind:['nativeExact','nativeNumeric'].includes(source.status)?'native':'derived'};
    const lines=['计算来源：'+(point.sourceKind==='native'?'数值直接来自本体。':'推算。'),'参照：'+names(source.ids)+'。',source.note];
    if(hits>1){Object.assign(point,{perHitBase:perHit[0],perHitUpgraded:perHit[1],instantBudget:[...instant[kind][cost]],hitPenalty:penalty,roundingLoss:adjusted.map((v,i)=>v-perHit[i]*hits)});lines.push('分段计算：段数扣减为设计参数p(n)＝n×(n−1)÷2，2／3／4段分别扣1／3／6点；'+hits+'段扣'+penalty+'点，再逐段向下取整。基础（'+budget[0]+'−'+penalty+'）÷'+hits+'取'+perHit[0]+'，'+perHit[0]+'×'+hits+'＝'+point.base+'；升级（'+budget[1]+'−'+penalty+'）÷'+hits+'取'+perHit[1]+'，'+perHit[1]+'×'+hits+'＝'+point.upgraded+'。扣减后取整另减少'+pair(point.roundingLoss)+'点。');}
    else lines.push('结果：基础'+point.base+'点'+(kind==='block'?'格挡':'伤害')+'；升级'+point.upgraded+'点'+(kind==='block'?'格挡':'伤害')+'。'+(kind==='aoe'?'数值为每名敌人受到的伤害。':''));
    if(kind!=='block'&&(hits>1||source.status!=='nativeExact'))lines.push('本体核对：缺少同费用、同稀有度、同目标方式且无额外牌面效果的独立单段对照，1／3／6点扣减尚不能由本体确定。将本体多段加回扣减反推单段属于拟合，不能用来独立验证扣减规则。');
    point.notes=lines.join('\n');return point;
  }
  const parameters={};
  for(const kind of ['attack','aoe','block']){parameters[kind]={};for(const cost of [0,1,2,3,4]){const row={base:instant[kind][cost][0],upgraded:instant[kind][cost][1],reference:costSources[kind][cost],byTurns:Object.fromEntries([0,1,2,3].map(t=>[t,makePoint(kind,cost,t)]))};if(kind!=='block')row.byHits=Object.fromEntries([2,3].map(h=>[h,{reference:sourceFor(kind,cost,0,h),byTurns:Object.fromEntries([0,1,2,3].map(t=>[t,makePoint(kind,cost,t,h)]))}]));parameters[kind][cost]=row;}}

  const referenceSystem={
    anchorIds:['SLICE','DEFLECT','LEAP','TWIN_STRIKE','DAGGER_SPRAY','CELESTIAL_MIGHT','THE_BOMB','BLUDGEON','BURY'].filter(id=>cards.has(id)),
    confirmedModels:[{label:'0费即时单段伤害',values:pair(slice),id:'SLICE'},{label:'0费即时格挡',values:pair(deflect),id:'DEFLECT'},{label:'1费即时格挡',values:pair(leap),id:'LEAP'},{label:'1费即时两段单体',values:'5×2／7×2',id:'TWIN_STRIKE'},{label:'1费即时两段群伤',values:'4×2／6×2',id:'DAGGER_SPRAY'}],
    formulas:{hitPenalty:{status:'designAssumption',expression:'n*(n-1)/2',byHits:{1:0,2:1,3:3,4:6}},scope:{ids:['TWIN_STRIKE','DAGGER_SPRAY'],condition:'1费、普通、两段、无附效；单段转换先扣p(2)，换算后加回',base:{numerator:twin[0],denominator:dagger[0],ratio:scope[0]},upgraded:{numerator:twin[1],denominator:dagger[1],ratio:scope[1]}},blockZero:{ids:['SLICE','DEFLECT'],condition:'0费、普通、单段',base:{numerator:deflect[0],denominator:slice[0]},upgraded:{numerator:deflect[1],denominator:slice[1]}},blockOne:{ids:['TWIN_STRIKE','LEAP'],condition:'1费、普通；伤害为两段总伤',base:{numerator:leap[0],denominator:twin[0]},upgraded:{numerator:leap[1],denominator:twin[1]}}},
    formulaRows:[['两段单体＝两段群伤×配对比','×10/8＝×1.25','×14/12≈×1.1667','双重打击／匕首雨；迁移到其他费用或等待为推算'],['单段单体，由单段群伤A推算','round((A−1)×10/8)＋1','round((A−1)×14/12)＋1','先扣两段扣减1点，套配对比，再加回1点'],['单段群伤，由单段单体S推算','round((S−1)×8/10)＋1','round((S−1)×12/14)＋1','同一两段配对比的逆换算'],['0费格挡＝单段伤害×比例','×4/6','×7/9','切割／偏折，仅此费用条件'],['1费格挡＝两段总伤×比例','×9/10','×12/14','双重打击／飞跃，仅此费用与段数条件'],['格挡费用函数，c为费用','4＋5c','7＋5c','由偏折与飞跃确定两点，2—4费为外推'],['普通单体费用预算','6、11、21；高费21＋10(c−2)','9、15、30；高费30＋15(c−2)','原生多段加回段数扣减后反推；3—4费外推'],['2回合群伤费用迁移','round(40×本费即时群伤÷2费即时群伤)','round(50×本费即时群伤÷2费即时群伤)','炸弹锚点；2费直接取值，其他费用推算'],['1回合伤害','round((即时＋2回合)/2)','同式','缺少同条件纯载荷牌，使用设计插值'],['3回合伤害','2×2回合伤害','同式','用户指定增强档'],['段数扣减p(n)，n为段数','n×(n−1)÷2','同式','设计参数；2／3／4段分别扣1／3／6点'],['固定多段总伤','floor((单段预算−p(n))÷n)×n','同式','先扣段数收益，再逐段向下取整；基础与升级同式'],['格挡等待参数','1／2／3回合：4/3／1.8／3倍','同式','设计假设，未由本体分回合格挡牌唯一确定']],
    excluded:[{id:'CINDER',reason:'随机消耗手牌，不能当作无附效单体配对。'},{id:'SOW',reason:'带保留，不能与无附效攻击直接提取目标范围价格。'},{id:'PREDATOR',reason:'带下回合抽牌，不能当作纯伤锚点。'},{id:'REAP',reason:'带保留，不能当作纯伤锚点。'},{id:'BLOOD_WALL',reason:'支付生命；不据此假定1生命固定兑换1格挡。'},{id:'BLUDGEON',reason:'罕见纯伤基准，独立保留，不混入普通费用拟合。'},{id:'BURY',reason:'罕见纯伤基准，独立保留，不混入普通费用拟合。'}].filter(entry=>cards.has(entry.id)),
    assumptions:['段数扣减p(n)＝n×(n−1)÷2为设计参数；新增第2、3、4段分别扣1、2、3点，再逐段向下取整。','单／群目标范围比只由匹配的两段原生牌提取；转换单段时先扣两段扣减、换算后加回，向其他条件迁移为推算。','高费格挡与3—4费普通伤害为费用外推。','1回合伤害中点、3回合增强和格挡等待倍率为设计假设。']
  };
  Object.assign(referenceSystem.formulas.hitPenalty,{validation:'notIdentifiedFromNative',independentMatchedSinglePairs:0});
  referenceSystem.formulas.scope.condition='1费、普通、两段、无额外牌面效果；跨角色且双重打击有打击标签，比例迁移为推算';
  referenceSystem.formulaRows[0][3]='跨角色配对；双重打击另有打击标签，比例迁移为推算';
  const hitRow=referenceSystem.formulaRows.find(row=>row[0].startsWith('段数扣减'));
  hitRow[3]='设计参数；2／3／4段扣1／3／6点，缺少独立同条件单段对照，本体未确认此曲线';
  referenceSystem.formulaRows.find(row=>row[0]==='普通单体费用预算')[3]='由段数扣减假设反推，不能反过来验证扣减；3—4费外推';
  referenceSystem.assumptions.unshift('本体没有独立的同条件单段对照来确认1／3／6点扣减；加回扣减再还原多段属于拟合，不是独立验证。');
  if(previous.basis?.calibration?.referenceSystem?.multiHitAudit?.nativeSourceSha256===native.source.sha256)referenceSystem.multiHitAudit=JSON.parse(JSON.stringify(previous.basis.calibration.referenceSystem.multiHitAudit));
  const next=JSON.parse(JSON.stringify(previous));Object.assign(next,{schemaVersion:7,id:'junna-vanilla-models-20261108-progressive-hits-v8',date:'2026-11-08',parameters,formula:'先确认本体基准，再按费用、段数与单／群／格挡公式推算。多段从单段预算扣除p(n)＝n×(n−1)÷2，再逐段向下取整；本体多段先加回扣减反推单段。2回合群伤锚定炸弹；1回合伤害取即时与2回合的中点，3回合伤害取2回合的2倍。各张备注列明来源、计算和设计参数。'});
  next.basis={calibration:{date:'2026-11-08',previousModelId:previous.id,nativeSourceSha256:native.source.sha256,scope:'普通、非消耗、0—4费、等待0—3回合，固定1／2／3段。',referenceSystem,explicitAnchor:{kind:'aoe',cost:2,turns:2,base:bombTotal[0],upgraded:bombTotal[1],sourceId:'THE_BOMB'},waitStatus:'nativeReferenceEstimate',rarityRule:'罕见锚点独立记录，不使用统一稀有度倍率。'},attack:{status:'paperEstimate',method:'matchedReferences',rules:'0费切割；1费双重打击、2费天穹之力先加回各自段数扣减反推单段；3—4费沿最后费用增量外推。'},aoe:{status:'paperEstimate',method:'matchedReferences',rules:'即时群伤按双重打击／匕首雨的两段配对比并计入段数扣减推算；2回合锚定炸弹40／50。'},block:{status:'paperEstimate',method:'matchedReferences',rules:'偏折4／7、飞跃9／12建立费用函数；高费外推，等待参数为设计假设。'},cardRules:'只在模型页签显示；五种同条件模板可直接确认，炸弹是数值锚点，其余为带明示假设的推算。固定段数不冒充天穹之力升级增加段数的原生形态。'};
  for(const card of next.cards){const p=parameters[card.kind][card.cost].byTurns[card.turns];Object.assign(card,{modelValue:p.base,upgradedModelValue:p.upgraded,notes:p.notes,sourceKind:p.sourceKind});const text=value=>card.kind==='block'?'获得'+value+'点格挡。':(card.kind==='aoe'?'对所有敌人':'')+'造成'+value+'点伤害。';card.text=text(p.base);card.upgradedText=text(p.upgraded);}
  return next;
}
module.exports={calibrateCardModels};
