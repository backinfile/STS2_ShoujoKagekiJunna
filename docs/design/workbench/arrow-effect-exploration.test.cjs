'use strict';
const test=require('node:test'),assert=require('node:assert/strict');
const logic=require('./route-statistics.js'),effects=require('./effect-statistics.js'),defaults=require('./route-statistics-defaults.json');
const routes=['stage','partner','energy','growth'].map(system=>({id:'SYS_'+system,uid:'archetype_SYS_'+system,system,effectRoles:{}}));
const route=id=>routes.find(r=>r.id==='SYS_'+id);
const card=(id,text,extra={})=>({id,uid:id,name:id,type:'技能',cost:1,rarity:'罕见',pool:true,keywords:[],archetypeIds:['SYS_stage','SYS_energy'],text,...extra});
test('固定等待同时提供箭矢来源与高费代付收益，保留旧转型的收益定位',()=>{
 const source=card('source','将1张手牌作为5回合的箭矢打出。消耗。');
 assert.equal(logic.profile(source,route('stage'),defaults).role,'trigger');
 assert.equal(logic.profile(source,route('energy'),defaults).role,'payoff');
 assert.equal(logic.profile({...source,cost:2},route('energy'),defaults).role,'both');
 assert.equal(logic.profile(card('convert','选择一张手牌，变为箭矢牌。'),route('stage'),defaults).role,'payoff');
 assert.equal(effects.handArrowPlay(card('old','将1张手牌作为4回合的箭矢打出。')),false);
});
test('旧工具在全部流派计入收益，箭矢自身兼有收益计为兼具',()=>{
 for(const r of routes){
  const c=card('legacy','原有调度。',{archetypeIds:[r.id],design:{routeRoles:{[r.uid]:{role:'tool',ports:['operation']}}}});
  assert.equal(logic.profile(c,r,defaults).role,'payoff');
  if(r.id==='SYS_stage')assert.equal(logic.profile({...c,type:'箭矢'},r,defaults).role,'both');
 }
});
test('效果计数区分两种目标、一次与批量预约，读取基础牌面',()=>{
 const cards=[
  card('single','将1张手牌作为5回合的箭矢打出。'),
  card('batch','将至多2张手牌作为5回合的箭矢打出。'),
  card('knowledge','知识。将手牌中的1张知识牌作为5回合的箭矢打出。'),
  card('enemy','所有目标为敌人的箭矢加速1回合。'),
  card('self','所有目标为自己的箭矢加速X回合。'),
  card('buff','所有目标为敌人的箭矢获得2点伤害加成，然后加速1回合。'),
  card('target','目标敌人的箭矢加速1回合。'),
  card('upgrade','获得5点格挡。',{upgradedText:'所有目标为敌人的箭矢加速1回合。'})
 ];
 const r={...route('stage'),members:cards,effectRoles:{trigger:{effects:[{id:'arrow.hand-as-five-turn-play'}]},payoff:{effects:[{id:'arrow.enemy-accelerate'},{id:'arrow.self-accelerate'}]}}};
 const found=effects.entries(r,{cards,partnerGroups:[]},defaults,logic.profile);
 assert.deepEqual(found.map(e=>[e.id,e.members.map(c=>c.id)]),[
  ['arrow.hand-as-five-turn-play',['single','batch','knowledge']],
  ['arrow.enemy-accelerate',['enemy','buff']],
  ['arrow.self-accelerate',['self']]
 ]);
 const energy={...route('energy'),members:[cards[0]],effectRoles:{payoff:{effects:[{id:'energy.auto-play'}]}}};
 assert.equal(effects.entries(energy,{cards,partnerGroups:[]},defaults,logic.profile)[0].members.length,1);
});
