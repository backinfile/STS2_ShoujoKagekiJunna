'use strict';
const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const filters=require('./filter-state.js');

test('具体选项按筛选、排除、取消循环，多项筛选和排除共存',()=>{
  let selection='all';
  selection=filters.cycle(selection,'攻击');
  selection=filters.cycle(selection,'技能');
  assert.deepEqual(filters.read(selection),{included:['攻击','技能'],excluded:[]});
  selection=filters.cycle(selection,'攻击');
  assert.deepEqual(filters.read(selection),{included:['技能'],excluded:['攻击']});
  assert(filters.matches(selection,['技能','知识']));
  assert(!filters.matches(selection,['技能','攻击']));
  selection=filters.cycle(selection,'攻击');
  assert.deepEqual(filters.read(selection),{included:['技能'],excluded:[]});
  selection=filters.cycle(selection,'技能');
  assert.deepEqual(filters.read(selection),{included:[],excluded:['技能']});
  assert.equal(filters.cycle(selection,'技能'),'all');
  assert.equal(filters.cycle(selection,'all'),'all');
});

test('同组按任意项匹配，并以排除优先处理多标签卡牌',()=>{
  const value={included:['A','B'],excluded:['C','D']};
  assert(filters.matches(value,'A'));assert(filters.matches(value,'B'));
  assert(!filters.matches(value,'E'));assert(!filters.matches(value,['A','C']));
  assert(filters.matches({excluded:['C']},[]));
  assert.deepEqual(filters.read({included:['A','A','C'],excluded:['C','C']}),{included:['A'],excluded:['C']});
});

test('旧单选值、URL往返、无效选项清理和排除态标记保持一致',()=>{
  assert.deepEqual(filters.read('稀有'),{included:['稀有'],excluded:[]});
  const selection={included:['普通','罕见'],excluded:['稀有']};
  assert.deepEqual(filters.decode(filters.encode(selection)),selection);
  assert.equal(filters.decode('攻击'),'攻击');
  assert.deepEqual(filters.prune(selection,['罕见','稀有']),{included:['罕见'],excluded:['稀有']});
  assert.equal(filters.prune('旧值',['稀有']),'all');
  const attributes=filters.attributes(selection,'稀有','稀有');
  assert.match(attributes,/data-filter-state="exclude"/);assert.match(attributes,/aria-pressed="mixed"/);assert.match(attributes,/稀有 · 排除/);
});

test('关联已有牌搜索按牌名和编号查找，排除自己、已关联与垃圾桶',()=>{
  const source=fs.readFileSync(require.resolve('./app.js'),'utf8');
  const code=source.slice(source.indexOf('  function grantOptions('),source.indexOf('  function relatedHTML('));
  const card={id:'MAIN'},data={cards:[card,{id:'JK122B',name:'谬误'},{id:'LINKED',name:'已关联'},{id:'RESERVE',name:'备用牌',reservedAt:'today'},{id:'TRASH',name:'已删除',trashedAt:'today'}]};
  const before=JSON.stringify(data),context=vm.createContext({state:{data},groupsFor:()=>[{grantedCardIds:['LINKED']}],escape:String});
  vm.runInContext(code,context);
  assert.match(context.grantOptions(card,'谬误'),/value="JK122B"/);
  assert.match(context.grantOptions(card,'jk122b'),/value="JK122B"/);
  assert.match(context.grantOptions(card,'RESERVE'),/备用牌/);
  const all=context.grantOptions(card);
  for(const id of ['MAIN','LINKED','TRASH'])assert.doesNotMatch(all,new RegExp('value="'+id+'"'));
  assert.equal(JSON.stringify(data),before);
});


test('Power来源与体系、契合主题与实装状态支持三态组合',()=>{
  const context=vm.createContext({JunnaFilters:filters});
  vm.runInContext(fs.readFileSync(require.resolve('./power-view.js'),'utf8'),context);
  const ui={search:'',powerOrigin:'all',powerSystem:'all'},data={meta:{powers:[
    {uid:'a',name:'甲',id:'A',origin:'junna',system:'stage'},
    {uid:'b',name:'乙',id:'B',origin:'junna',system:'partner'},
    {uid:'c',name:'丙',id:'C',origin:'base',system:'stage'}
  ]},cards:[]};
  const view=context.JunnaPowers.create({data:()=>data,ui:()=>ui,flush(){},render(){}});
  const click=(key,value)=>view.sidebarClick({target:{closest(){return{dataset:{powerFilter:key,value}}}}});
  click('powerSystem','stage');click('powerSystem','partner');click('powerSystem','stage');
  assert.deepEqual(Array.from(view.filtered(),item=>item.uid),['b']);
  click('powerSystem','stage');assert.deepEqual(Array.from(view.filtered(),item=>item.uid),['b']);
  click('powerSystem','all');click('powerOrigin','base');click('powerOrigin','base');
  assert.deepEqual(Array.from(view.filtered(),item=>item.uid),['a','b']);
  const fits=require('./fit-pool-view.js'),entries=[{name:'星',theme:'星星',cardIds:['A']},{name:'箭',theme:'箭矢',cardIds:[]},{name:'书',theme:'知识',cardIds:[]}];
  const result=fits.filterEntries(entries,[{id:'A'}],{search:'',fitTheme:{included:['星星','箭矢'],excluded:['知识']},fitStatus:{excluded:['used']}});
  assert.deepEqual(result.map(entry=>entry.name),['箭']);
});
