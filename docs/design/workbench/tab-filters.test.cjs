'use strict';
const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync(require.resolve('./app.js'),'utf8');
const logic=require('./route-statistics.js');
const functions=source.split('\n').filter(line=>/^  (const TAB_FILTER_DEFAULTS=|function (rememberTabFilters|selectTab|saveUI|readHash|changeTab|openLinkedCard)\()/.test(line)).join('\n');
const actions=source.slice(source.indexOf('  function routeAction('),source.indexOf("  $('work-area').addEventListener('click',event=>{const button=event.target.closest('[data-trash-page]')"));
const clone=value=>JSON.parse(JSON.stringify(value));

function fixture(saved={tab:'cards',pool:'pool'}){
  const storage=new Map(),cards=[{uid:'reserved',reservedAt:'today'}];
  const context=vm.createContext({JunnaFilters:require('./filter-state.js'),
    ui:{},storedUI:saved,clone,URLSearchParams,JunnaRouteStatistics:logic,
    state:{data:{terms:[]}},location:{hash:''},uiKey:'test',previewMode:false,
    localStorage:{setItem(key,value){storage.set(key,value);}},
    history:{replaceState(_a,_b,hash){context.location.hash=hash;},pushState(_a,_b,hash){context.location.hash=hash;}},
    searchTimer:null,clearTimeout(){},trashPage:0,flushEdit(){},
    mobileLayout:{scrollToTop(){}},renderAll(){context.saveUI();},
    overviewDimensions:()=>[],
    current:()=>({id:'keyword',kind:'关键词',name:'知识'}),
    find:(_collection,key)=>cards.find(card=>card.uid===key),
    cardTab:card=>card.reservedAt?'reserve':'cards',
    openEditor(){},CSS:{escape:String},document:{querySelector(){return null;}}
  });
  vm.runInContext(functions+'\nObject.assign(ui,clone(TAB_FILTER_DEFAULTS),storedUI);\n'+actions,context);
  return{context,ui:context.ui,storage};
}

test('card and reserve tabs independently retain every shared filter and sorting choice',()=>{
  const{context,ui}=fixture();
  const choices={search:'星',kind:'关键词',type:'箭矢',rarity:'稀有',cardPool:'角色',pool:'granted',cost:'3+',archetype:'knowledge',term:'keyword',routePositions:['trigger'],routeExcludedPositions:['cross'],sort:'cost',descending:true,upgraded:true};
  Object.assign(ui,clone(choices));
  context.changeTab('reserve');
  assert.equal(ui.search,'');assert.equal(ui.type,'all');assert.equal(ui.pool,'all');assert.equal(ui.sort,'natural');assert.equal(ui.descending,false);
  ui.search='训练';ui.type='技能';ui.sort='type';ui.routePositions=['payoff'];
  context.changeTab('cards');
  for(const[key,value]of Object.entries(choices))assert.deepEqual(clone(ui[key]),value,key);
  context.changeTab('reserve');
  assert.equal(ui.search,'训练');assert.equal(ui.type,'技能');assert.equal(ui.sort,'type');assert.deepEqual(clone(ui.routePositions),['payoff']);
  context.changeTab('cards');ui.routePositions.push('payoff');
  context.changeTab('reserve');assert.deepEqual(clone(ui.routePositions),['payoff']);
});

test('all tabs retain their own searches, term categories and position filters',()=>{
  const{context,ui}=fixture();
  const tabs=['cards','reserve','trash','terms','mechanisms','powers','relics','fits','flows','models','styles','overview','initial'];
  for(const tab of tabs){context.changeTab(tab);assert.equal(ui.search,'');ui.search=tab;ui.kind=tab;ui.routeExcludedPositions=[tab];context.saveUI();}
  for(const tab of tabs){context.changeTab(tab);assert.equal(ui.search,tab);assert.equal(ui.kind,tab);assert.deepEqual(clone(ui.routeExcludedPositions),[tab]);}
});

test('clicking the current tab and clearing its filters preserve other tabs',()=>{
  const{context,ui}=fixture();
  ui.search='卡池';ui.pool='all';context.changeTab('reserve');ui.search='备用';ui.sort='cost';
  context.changeTab('cards');context.changeTab('cards');
  assert.equal(ui.search,'卡池');assert.equal(ui.pool,'all');
  context.routeAction('clear-filters');assert.equal(ui.search,'');assert.equal(ui.pool,'pool');
  context.changeTab('reserve');assert.equal(ui.search,'备用');assert.equal(ui.sort,'cost');
});

test('saved browser state restores each tab after a reload, including arrays and sort direction',()=>{
  const{context,ui,storage}=fixture();
  ui.search='卡池';ui.sort='cost';ui.descending=true;ui.routePositions=['trigger'];
  context.changeTab('reserve');ui.search='备用';ui.pool='granted';context.saveUI();
  const restored=fixture(JSON.parse(storage.get('test')));
  restored.context.readHash();
  assert.equal(restored.ui.search,'备用');assert.equal(restored.ui.pool,'granted');
  restored.context.changeTab('cards');
  assert.equal(restored.ui.search,'卡池');assert.equal(restored.ui.sort,'cost');assert.equal(restored.ui.descending,true);assert.deepEqual(clone(restored.ui.routePositions),['trigger']);
});

test('legacy browser preferences migrate only into the active tab',()=>{
  const{context,ui}=fixture({tab:'reserve',search:'旧搜索',type:'技能',sort:'rarity',descending:true});
  context.readHash();context.changeTab('cards');
  assert.equal(ui.search,'');assert.equal(ui.type,'all');assert.equal(ui.pool,'pool');assert.equal(ui.sort,'natural');
  context.changeTab('reserve');assert.equal(ui.search,'旧搜索');assert.equal(ui.type,'技能');assert.equal(ui.sort,'rarity');assert.equal(ui.descending,true);
});

test('hash navigation applies only the target tab and omitted fields restore link defaults',()=>{
  const{context,ui}=fixture();
  ui.search='卡池';ui.type='箭矢';ui.sort='cost';
  context.location.hash='#tab=reserve&search=linked&sort=name&descending=1&positions=payoff';context.readHash();
  assert.equal(ui.search,'linked');assert.equal(ui.type,'all');assert.equal(ui.sort,'name');assert.equal(ui.descending,true);assert.deepEqual(clone(ui.routePositions),['payoff']);
  context.location.hash='#tab=reserve';context.readHash();
  assert.equal(ui.search,'');assert.equal(ui.sort,'natural');assert.equal(ui.descending,false);assert.equal(ui.routePositions.length,0);
  context.changeTab('cards');assert.equal(ui.search,'卡池');assert.equal(ui.type,'箭矢');assert.equal(ui.sort,'cost');
});

test('linked cards and term drilldowns change destination filters without clearing the source tab',()=>{
  const{context,ui}=fixture();
  context.changeTab('powers');ui.search='力量';ui.sort='name';
  context.openLinkedCard('reserved');assert.equal(ui.tab,'reserve');assert.equal(ui.search,'');assert.equal(ui.pool,'all');
  context.changeTab('powers');assert.equal(ui.search,'力量');assert.equal(ui.sort,'name');
  context.changeTab('terms');ui.search='知识';ui.kind='关键词';
  context.routeAction('cards-by-term');assert.equal(ui.tab,'cards');assert.equal(ui.term,'keyword');
  context.changeTab('terms');assert.equal(ui.search,'知识');assert.equal(ui.kind,'关键词');
});


test('三态多选和排除条件在URL、刷新与切页后恢复，搜索原文保持原值',()=>{
  const {context,ui}=fixture();
  const selection={included:['攻击','技能'],excluded:['能力']};
  ui.type=clone(selection);ui.cost={included:['0','1'],excluded:['X']};ui.search='{"query":"原文"}';context.saveUI();
  const hash=context.location.hash,restored=fixture();
  restored.context.location.hash=hash;restored.context.readHash();
  assert.deepEqual(clone(restored.ui.type),selection);assert.equal(restored.ui.search,ui.search);
  restored.context.changeTab('reserve');assert.equal(restored.ui.type,'all');
  restored.context.changeTab('cards');assert.deepEqual(clone(restored.ui.type),selection);
});
