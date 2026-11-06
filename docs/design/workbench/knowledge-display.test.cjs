'use strict';
const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const domain=require('./workbench-domain.js');

function renderContext(){
  const source=fs.readFileSync(require.resolve('./app.js'),'utf8');
  const textFunctions=source.slice(source.indexOf('  function energyText('),source.indexOf('  const fittedCards='));
  const knowledgeFunctions=source.slice(source.indexOf('  function knowledgeEntries('),source.indexOf('  function cardStackHTML('));
  const groupsFunction=source.match(/  function groupsFor\(card\)\{[^\n]+/)[0];
  const card={uid:'clause',id:'CLAUSE',name:'分句',type:'箭矢',rarity:'罕见',cost:0,upgradedCost:0,turns:2,upgradedTurns:2,text:'知识。造成8点伤害。抽1张牌。闪耀9。',upgradedText:'知识。造成12点伤害。抽1张牌。闪耀9。',keywords:['知识','闪耀'],partnerCardIds:[],art:'portrait'};
  const group={mainCardId:card.id,mainCardCount:3,grantedCardIds:[],grantedCardCounts:{}};
  const state={data:{cards:[card],partnerGroups:[group]}};
  const context=vm.createContext({
    state,ui:{upgraded:false,tab:'cards'},JunnaWorkbenchDomain:domain,
    find:(collection,id)=>state.data[collection].find(entry=>entry.id===id||entry.uid===id),
    escape:String,assetURL:id=>id,
    JunnaCardReferences:{referencesFor:()=>[],protect:text=>({text,values:[]})},
    JunnaEnergyIcons:{protect:text=>({text,values:[]}),restore:text=>text}
  });
  vm.runInContext(textFunctions+knowledgeFunctions+groupsFunction,context);
  return {context,card,group,state};
}

test('同模型三张保留牌面知识关键词，仅在知识入口显示知识×3',()=>{
  const {context,card,group,state}=renderContext();
  for(const upgraded of [false,true]){
    const markup=context.cardHTML(card,upgraded);
    assert.match(markup,/<span class="text-gold">知识<\/span>/);
    assert.doesNotMatch(markup,/知识×/);
    assert.doesNotMatch(markup,/text-green[^>]*>3<\/span>/);
  }
  const button=context.knowledgeButton(card);
  assert.match(button,/<span>知识×3<\/span>/);
  assert.doesNotMatch(button,/knowledge-copy-position|knowledge-size/);
  assert.equal(context.knowledgeEntries(group).length,3);
  assert.equal(card.text,'知识。造成8点伤害。抽1张牌。闪耀9。');
  const companion={...card,id:'COMPANION',uid:'companion',name:'关联牌'};
  state.data.cards.push(companion);
  Object.assign(group,{mainCardCount:1,grantedCardIds:[companion.id],grantedCardCounts:{COMPANION:2}});
  assert.doesNotMatch(context.cardHTML(card),/知识×/);
  assert.match(context.knowledgeButton(card),/knowledge-size">3张/);
});
