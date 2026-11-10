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


test('共享关联牌显示自己的列表，来源入口保持各自发放数量',()=>{
  const {context,card,group,state}=renderContext();
  const fallacy={...card,uid:'fallacy',id:'FALLACY',name:'谬误',pool:false};
  const other={...card,uid:'other',id:'OTHER',name:'另一入口',pool:true};
  state.data.cards.push(fallacy,other);
  Object.assign(group,{mainCardCount:1,grantedCardIds:['FALLACY'],grantedCardCounts:{FALLACY:1}});
  state.data.partnerGroups.push({mainCardId:'OTHER',grantedCardIds:['FALLACY'],grantedCardCounts:{FALLACY:2}});
  assert.equal(context.groupsFor(fallacy).length,0);
  assert.equal(context.knowledgeButton(fallacy),'');
  assert.match(context.knowledgeButton(fallacy,undefined,card.uid),/knowledge-size">2张/);
  assert.match(context.knowledgeButton(fallacy,undefined,other.uid),/knowledge-size">3张/);
  assert.deepEqual(Array.from(context.knowledgeEntries(group),entry=>entry.card.id),[card.id,'FALLACY']);
  const source=fs.readFileSync(require.resolve('./app.js'),'utf8');
  const related=source.slice(source.indexOf('  function grantOptions('),source.indexOf('  function arrowFocusEditor('));
  Object.assign(context,{cardTab:()=> 'cards',energyText:String});
  vm.runInContext(related,context);
  const html=context.relatedHTML(fallacy);
  assert.match(html,/data-add-grant/);
  assert.match(html,/value="OTHER"/);
  assert.match(html,/value="CLAUSE"/);
  assert.doesNotMatch(html,/data-remove-grant/);
});

test('查看关联牌显示其入池状态，离开条目后恢复入口牌面',()=>{
  const {context,card,group,state}=renderContext();
  card.pool=true;
  const companion={...card,uid:'score',id:'SCORE',name:'乐谱',pool:false,partnerCardIds:[]};
  state.data.cards.push(companion);
  Object.assign(group,{mainCardCount:1,grantedCardIds:['SCORE'],grantedCardCounts:{SCORE:2}});
  const entry=context.knowledgeEntries(group).find(item=>item.card.uid===companion.uid);
  Object.assign(context,{
    knowledgeFaces:new Map([[card.uid,entry.key]]),
    cardStackHTML:face=>'<span>'+face.name+'</span>'
  });
  const source=fs.readFileSync(require.resolve('./app.js'),'utf8');
  vm.runInContext(source.slice(source.indexOf('  function cardEntryHTML('),source.indexOf('  function cycleKnowledgeEntry(')),context);
  context.ui.selected={collection:'cards',uid:companion.uid,cardOrigin:card.uid};
  const selected=context.cardEntryHTML(card,false);
  assert.match(selected,/不入池/);
  assert.match(selected,new RegExp(card.name));
  context.ui.selected=null;
  const restored=context.cardEntryHTML(card,false);
  assert.match(restored,/data-card-face="clause"/);
  assert.doesNotMatch(restored,/data-card-face="score"/);
});
