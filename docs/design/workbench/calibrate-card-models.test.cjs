'use strict';
const test=require('node:test'),assert=require('node:assert/strict');
const {calibrateCardModels}=require('./calibrate-card-models.cjs');
function fixture(){
  const spec=[['SLICE',0,'attack',6,9,1,[]],['CINDER',2,'attack',18,24,1,[]],['REAP',3,'attack',27,33,1,['保留']],['SOW',1,'aoe',8,11,1,['保留']],['DEFLECT',0,'block',4,7,1,[]],['LEAP',1,'block',9,12,1,[]],['TWIN_STRIKE',1,'attack',5,7,2,[]],['DAGGER_SPRAY',1,'aoe',4,6,2,[]]];
  const native={source:{sha256:'native-fixture'},cards:spec.map(([id,cost,kind,base,upgraded,hits,keywords])=>({id,cost,upgradedCost:cost,rarity:'普通',keywords,hitCount:kind==='block'?null:hits,text:kind==='block'?'获得'+base+'点格挡。':'造成'+base+'点伤害。',upgradedText:kind==='block'?'获得'+upgraded+'点格挡。':'造成'+upgraded+'点伤害。',metric:{kind,value:base,hits,total:base*hits},upgradedMetric:{kind,value:upgraded,hits,total:upgraded*hits}}))};
  native.cards.push({id:'THE_BOMB',name:'炸弹',cost:2,upgradedCost:2,rarity:'罕见',type:'技能',keywords:[],text:'在3回合结束后，对所有敌人造成40点伤害。',upgradedText:'在3回合结束后，对所有敌人造成50点伤害。'});
  native.cards.push({id:'CELESTIAL_MIGHT',name:'天穹之力',cost:2,upgradedCost:2,rarity:'普通',keywords:[],hitCount:3,text:'造成6点伤害3次。',upgradedText:'造成6点伤害4次。',metric:{kind:'attack',value:6,hits:3,total:18},upgradedMetric:{kind:'attack',value:6,hits:4,total:24}});
  const cards=['attack','aoe','block'].flatMap(kind=>[0,1,2,3,4].flatMap(cost=>[0,1,2,3].map(turns=>({uid:kind+cost+turns,kind,cost,turns,name:kind,text:'旧牌面',upgradedText:'旧升级',modelValue:999,upgradedModelValue:999,rarity:'普通',pool:false}))));
  return{previous:{id:'old',cards,basis:{},scope:'modelTabOnly'},native};
}
test('native two-hit attacks keep native upgrade values independently of the single-hit curve',()=>{
  const {previous,native}=fixture(),model=calibrateCardModels(previous,native),point=model.parameters.aoe[1].byHits[2].byTurns[0];
  assert.equal(point.perHitBase,4);assert.equal(point.perHitUpgraded,6);assert.equal(point.upgraded,12);
  assert.equal(model.parameters.aoe[1].upgraded,13);
  assert.equal(model.parameters.aoe[1].byTurns[0].sourceKind,'derived');
  assert.equal(point.sourceKind,'native');
  assert.deepEqual([model.parameters.attack[1].byHits[2].byTurns[0].base,model.parameters.attack[1].byHits[2].byTurns[0].upgraded],[10,14]);
});
test('all card faces use the corrected table and retain their cost, wait and acquisition scope',()=>{
  const {previous,native}=fixture(),before=JSON.stringify(previous),model=calibrateCardModels(previous,native);
  assert.equal(JSON.stringify(previous),before);assert.equal(model.cards.length,60);assert.equal(model.scope,'modelTabOnly');
  for(const card of model.cards){const point=model.parameters[card.kind][card.cost].byTurns[card.turns];assert(card.text.includes(String(point.base)));assert(card.upgradedText.includes(String(point.upgraded)));assert.equal(card.pool,false);assert.equal(card.rarity,'普通');}
  assert.equal(model.parameters.attack[2].upgraded,30);assert.equal(model.parameters.attack[3].upgraded,45);
  assert.equal(model.parameters.attack[4].reference.status,'extrapolation');
  assert.deepEqual([0,1,2,3].map(t=>model.parameters.attack[1].byTurns[t].base),[11,19,26,52]);
});
test('native currency or keyword drift rejects a mismatched anchor',()=>{
  const {previous,native}=fixture();native.cards.find(c=>c.id==='DAGGER_SPRAY').starCost=1;
  assert.throws(()=>calibrateCardModels(previous,native),/star cost/);
});

test('two-cost two-turn group damage uses Bomb numbers and three turns retain a separate jump',()=>{
  const {previous,native}=fixture(),model=calibrateCardModels(previous,native);
  assert.equal(model.parameters.aoe[2].byTurns[2].base,40);
  assert.equal(model.parameters.aoe[2].byTurns[2].upgraded,50);
  assert.equal(model.parameters.attack[2].byTurns[2].base,50);
  assert.equal(model.parameters.attack[2].byTurns[2].upgraded,58);
  assert.deepEqual([1,2,3].map(t=>model.parameters.block[1].byTurns[t].base),[12,16,27]);
  assert.equal(model.parameters.attack[1].byTurns[3].reference.status,'designBoost');
  for(const kind of ['attack','aoe','block'])for(const cost of [0,1,2,3,4])for(const hits of kind==='block'?[1]:[1,2,3]){
    const p=model.parameters[kind][cost],points=hits===1?p.byTurns:p.byHits[hits].byTurns;
    for(const key of ['base','upgraded']){
      assert(points[1][key]>points[0][key]);assert(points[2][key]>points[1][key]);
      assert(points[3][key]-points[2][key]>=2*(points[2][key]-points[1][key]),kind+' '+cost+' '+hits+' '+key+' third-turn premium');
    }
  }
});

test('every template and hit-count variant records its source and actual calculation',()=>{
  const {previous,native}=fixture(),model=calibrateCardModels(previous,native);
  for(const card of model.cards){assert.match(card.notes,/计算来源：/);assert.match(card.notes,/基础/);assert.match(card.notes,/升级/);}
  for(const kind of ['attack','aoe'])for(const cost of [0,1,2,3,4])for(const hits of [2,3])for(const turns of [0,1,2,3]){
    const point=model.parameters[kind][cost].byHits[hits].byTurns[turns];assert.match(point.notes,point.sourceKind==='native'?/计算来源：数值直接来自本体/:/计算来源：推算/);assert(point.notes.includes(String(point.base)));assert(point.notes.includes(String(point.upgraded)));
  }
  const bomb=model.cards.find(c=>c.kind==='aoe'&&c.cost===2&&c.turns===2);
  assert.match(bomb.notes,/数值直接来自本体/);assert.match(bomb.notes,/炸弹/);assert.match(bomb.notes,/罕见/);assert.match(bomb.notes,/回合结束/);
  assert.match(model.parameters.attack[4].byHits[2].byTurns[0].notes,/21＋2×（21−11）＝41/);
  assert.match(model.parameters.attack[4].byHits[2].byTurns[1].notes,/21＋2×（21−11）＝41/);
  assert.match(model.parameters.attack[1].byHits[3].byTurns[1].notes,/段数扣减为设计参数/);
});

test('matched formulas do not depend on the damage of cards with auxiliary effects',()=>{
  const {previous,native}=fixture();
  const cinder=native.cards.find(c=>c.id==='CINDER');cinder.text='造成99点伤害。';cinder.upgradedText='造成120点伤害。';
  const sow=native.cards.find(c=>c.id==='SOW');sow.metric.total=77;sow.upgradedMetric.total=99;
  const model=calibrateCardModels(previous,native),system=model.basis.calibration.referenceSystem;
  assert.deepEqual(system.formulas.scope.ids,['TWIN_STRIKE','DAGGER_SPRAY']);
  assert.equal(system.formulas.scope.base.ratio,10/8);assert.equal(system.formulas.scope.upgraded.ratio,14/12);
  assert.equal(system.confirmedModels.length,5);
  assert.equal(model.parameters.attack[2].byTurns[2].base,50);assert.equal(model.parameters.attack[2].byTurns[2].upgraded,58);
  assert.deepEqual(Object.values(model.parameters.block).map(p=>[p.base,p.upgraded]),[[4,7],[9,12],[14,17],[19,22],[24,27]]);
});

test('progressive hit premium preserves native anchors and discounts multi-hit totals before rounding',()=>{
  const {previous,native}=fixture(),model=calibrateCardModels(previous,native);
  assert.deepEqual(model.basis.calibration.referenceSystem.formulas.hitPenalty.byHits,{1:0,2:1,3:3,4:6});
  assert.equal(model.basis.calibration.referenceSystem.formulas.hitPenalty.validation,'notIdentifiedFromNative');
  assert.match(model.parameters.attack[1].byTurns[0].notes,/属于拟合/);
  assert.deepEqual([model.parameters.attack[1].base,model.parameters.attack[1].upgraded],[11,15]);
  assert.deepEqual([model.parameters.attack[2].base,model.parameters.attack[2].upgraded],[21,30]);
  assert.equal(model.parameters.attack[2].byHits[3].byTurns[0].base,18);
  assert.equal(model.parameters.attack[2].upgraded-6,24);
  for(const kind of ['attack','aoe'])for(const cost of [0,1,2,3,4])for(const turns of [0,1,2,3]){
    const row=model.parameters[kind][cost],single=row.byTurns[turns],two=row.byHits[2].byTurns[turns],three=row.byHits[3].byTurns[turns];
    for(const key of ['base','upgraded']){
      assert(single[key]>two[key]&&two[key]>three[key],kind+' '+cost+' '+turns+' '+key+' hit ordering');
      for(const [hits,point,penalty] of [[2,two,1],[3,three,3]])assert.equal(point[key],Math.floor((single[key]-penalty)/hits)*hits);
    }
    assert.equal(two.hitPenalty,1);assert.equal(three.hitPenalty,3);
    assert.deepEqual(two.roundingLoss,[single.base-1-two.base,single.upgraded-1-two.upgraded]);
    assert.deepEqual(three.roundingLoss,[single.base-3-three.base,single.upgraded-3-three.upgraded]);
    assert.match(three.notes,/段数扣减为设计参数/);assert.match(three.notes,/3点/);
  }
});
