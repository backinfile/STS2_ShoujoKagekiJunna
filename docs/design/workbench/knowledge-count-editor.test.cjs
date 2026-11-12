'use strict';
const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const domain=require('./workbench-domain.js');
function editor(id){
  let nextId=0;
  const raw={cards:[['A','埋头学习',true],['B','心得',false],['JK133','星坠',true],['C','普通牌',true]].map(([id,name,pool])=>({uid:'card_'+id,id,name,pool,type:'技能',rarity:'罕见',cost:0,text:'',keywords:[],termIds:[],archetypeIds:[]})),partnerGroups:[
    {uid:'group_A',id:'GROUP_A',mainCardId:'A',grantedCardIds:['B'],grantedCardCounts:{B:2}},
    {uid:'group_B',id:'GROUP_B',mainCardId:'B',mainCardCount:2,grantedCardIds:['A'],grantedCardCounts:{A:1}},
    {uid:'group_star',id:'GROUP_STAR',mainCardId:'JK133',mainCardCount:3,grantedCardIds:[],grantedCardCounts:{}}
  ]};
  const uid=()=>String(++nextId),normalize=domain.create({seed:{data:raw},uid,JunnaFitPool:{normalizeEntry:entry=>entry}});
  const state={data:normalize(raw)},source=fs.readFileSync(require.resolve('./app.js'),'utf8');
  const context=vm.createContext({state,uid,ui:{tab:'cards'},JunnaWorkbenchDomain:domain,escape:String,energyText:String,current:()=>state.data.cards.find(c=>c.id===id),renderInspector(){},transact(label,mutate){mutate(state.data);state.data=normalize(state.data);}});
  const groups=source.match(/  function groupsFor\(card\)\{[^\n]+/)[0];
  const related=source.slice(source.indexOf('  function grantOptions('),source.indexOf('  function arrowFocusEditor('));
  const start=source.indexOf("if(input.dataset.grantCount){"),end=source.indexOf("if(input.hasAttribute('data-add-grant'))",start);
  const helperStart=source.indexOf('  function changeKnowledgeCount('),helper=helperStart<0?'':source.slice(helperStart,source.indexOf('  function addGrant(',helperStart));
  vm.runInContext(groups+related+helper+'\nfunction onCount(input){'+source.slice(start,end)+'}',context);
  return {context,state,html:()=>context.relatedHTML(state.data.cards.find(c=>c.id===id)),change:(cardId,count)=>context.onCount({dataset:{grantCount:cardId},value:String(count)})};
}

test('星坠关联编辑显示自身总数3，不能移除自身',()=>{
  const {html}=editor('JK133');
  assert.match(html(),/星坠 · 自身/);
  assert.match(html(),/value="3" data-grant-count="JK133"/);
  assert.doesNotMatch(html(),/data-remove-grant="JK133"/);
});

test('修改星坠自身数量保存到自身份数，保持其他字段与列表',()=>{
  const {state,change}=editor('JK133'),before=structuredClone(state.data);
  change('JK133',4);
  assert.deepEqual(state.data,{...before,partnerGroups:before.partnerGroups.map(g=>g.mainCardId==='JK133'?{...g,mainCardCount:4}:g)});
  change('JK133',3);
  assert.deepEqual(state.data,before);
});

test('双方独立入口均显示自身与其他成员的实际份数',()=>{
  const a=editor('A'),b=editor('B');
  assert.match(a.html(),/value="1" data-grant-count="A"/);
  assert.match(a.html(),/value="2" data-grant-count="B"/);
  assert.match(b.html(),/value="2" data-grant-count="B"/);
  assert.match(b.html(),/value="1" data-grant-count="A"/);
  assert.doesNotMatch(a.html(),/data-remove-grant="A"/);
  assert.match(a.html(),/data-remove-grant="B"/);
});

test('自身和其他成员的份数分别修改，各入口数量独立',()=>{
  const {state,change}=editor('A'),before=structuredClone(state.data);
  change('A',2);change('B',4);
  assert.deepEqual(state.data,{...before,partnerGroups:before.partnerGroups.map(g=>g.mainCardId==='A'?{...g,mainCardCount:2,grantedCardCounts:{B:4}}:g)});
});

test('默认自身1张不创建空列表，自身多张可以创建独立列表',()=>{
  const {state,change,html}=editor('C'),before=structuredClone(state.data);
  assert.match(html(),/value="1" data-grant-count="C"/);
  change('C',1);assert.deepEqual(state.data,before);
  change('C',3);
  const group=state.data.partnerGroups.find(g=>g.mainCardId==='C');
  assert.equal(group.mainCardCount,3);assert.deepEqual(group.grantedCardIds,[]);assert.deepEqual(group.grantedCardCounts,{});
  assert.deepEqual(state.data.partnerGroups.slice(0,-1),before.partnerGroups);
});
