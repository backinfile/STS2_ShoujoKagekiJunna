'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

const source = fs.readFileSync(require.resolve('./app.js'), 'utf8');
const constants = source.slice(source.indexOf('  const TYPES='), source.indexOf('\n', source.indexOf('  const TYPES=')));
const filtering = source.slice(source.indexOf('  function filteredCards('), source.indexOf('  function filteredTerms('));
const card = (id, extra = {}) => ({
  uid: id, id, name: id, type: '攻击', rarity: '普通', cost: 1, upgradedCost: 1,
  pool: true, partnerCardIds: [], termIds: [], archetypeIds: [], ...extra
});

function fixture(cards, overrides = {}) {
  const context = vm.createContext({JunnaFilters:require('./filter-state.js'),
    ui: {
      tab: 'cards', sort: 'natural', descending: false, upgraded: false, search: '',
      type: 'all', rarity: 'all', cardPool:'all', pool: 'all', cost: 'all', archetype: 'all', term: 'all',
      ...overrides
    },
    state: { data: { cards, archetypes: [] }, history: [] },
    cardTab: value => value.trashedAt ? 'trash' : value.reservedAt ? 'reserve' : 'cards',
    JunnaRouteStatistics: { matchesPosition: () => true }, flowDefaults: {},
    localStorage: { setItem() {} }, uiKey: 'test', previewMode: false,
    clone: value => JSON.parse(JSON.stringify(value)),
    location: { hash: '' }, history: { replaceState(_state, _title, hash) { context.location.hash = hash; } },
    URLSearchParams
  });
  const save = source.slice(source.indexOf('  function saveUI('), source.indexOf('\n', source.indexOf('  function saveUI(')));
  const tabState = source.split('\n').filter(line => /^  (const TAB_FILTER_DEFAULTS=|function rememberTabFilters\()/.test(line)).join('\n');
  vm.runInContext(constants + '\n' + tabState + '\n' + filtering + '\n' + save, context);
  return { context, ids: () => Array.from(context.filteredCards(), value => value.id) };
}

test('natural sorting compares rarity, cost, type and numeric card ID in order', () => {
  const { ids } = fixture([
    card('C10'), card('C2'), card('C3', { cost: 0 }),
    card('S', { type: '技能', cost: 0 }), card('P', { type: '能力', cost: 0 }),
    card('A', { type: '箭矢', cost: 0 }), card('U', { rarity: '罕见', cost: 0 }),
    card('R', { rarity: '稀有', cost: 0 }), card('B', { rarity: '基础', cost: 9 }),
    card('E', { rarity: '远古', cost: 0 })
  ]);
  assert.deepEqual(ids(), ['B', 'C3', 'S', 'P', 'A', 'C2', 'C10', 'U', 'R', 'E']);
});

test('special rarities follow ancient cards and remain independently filterable by type and pool', () => {
  const {context,ids}=fixture([
    card('Curse',{type:'诅咒',rarity:'诅咒',pool:false}),
    card('Status',{type:'状态',rarity:'状态',pool:false}),
    card('Token',{rarity:'衍生',pool:false}),
    card('Quest',{type:'任务',rarity:'任务',pool:false}),
    card('Event',{rarity:'事件',pool:false}),card('Common'),card('Ancient',{rarity:'远古'})
  ]);
  assert.deepEqual(ids(),['Common','Ancient','Event','Token','Status','Curse','Quest']);
  context.ui.rarity='衍生';context.ui.type='攻击';context.ui.pool='granted';
  assert.deepEqual(ids(),['Token']);
  context.ui.type='技能';assert.deepEqual(ids(),[]);
});

test('card-pool filters distinguish character companions, generated tokens and curses',()=>{
  const {context,ids}=fixture([card('reward'),card('companion',{pool:false,cardPool:'角色'}),card('token',{pool:false,rarity:'衍生',cardPool:'衍生'}),card('curse',{pool:false,type:'诅咒',rarity:'诅咒',cardPool:'诅咒'})]);
  context.ui.cardPool='角色';assert.deepEqual(ids(),['companion','reward']);
  context.ui.pool='granted';assert.deepEqual(ids(),['companion']);
  context.ui.pool='all';context.ui.cardPool='衍生';assert.deepEqual(ids(),['token']);
  context.ui.cardPool='诅咒';assert.deepEqual(ids(),['curse']);
  context.saveUI();assert.equal(new URLSearchParams(context.location.hash.slice(1)).get('cardPool'),'诅咒');
});

test('natural and cost sorting keep base-cost order when upgraded faces are toggled', () => {
  const { context, ids } = fixture([
    card('X10', { cost: 'X', upgradedCost: 0 }),
    card('X2', { cost: 'X', upgradedCost: 'X' }),
    card('High', { cost: 100, upgradedCost: 'X' }),
    card('Low', { cost: 0, upgradedCost: 1 })
  ]);
  for (const sort of ['natural', 'cost']) {
    context.ui.sort = sort;
    for (const upgraded of [false, true, false]) {
      context.ui.upgraded = upgraded;
      context.ui.descending = false;
      assert.deepEqual(ids(), ['Low', 'High', 'X2', 'X10']);
      context.ui.descending = true;
      assert.deepEqual(ids(), ['X10', 'X2', 'High', 'Low']);
    }
  }
});

test('natural sorting supports reserve and filters while other sort modes stay selectable', () => {
  const { context, ids } = fixture([
    card('C1'), card('C2', { reservedAt: 'today', rarity: '稀有' }),
    card('C3', { reservedAt: 'today' }), card('C4', { reservedAt: 'today', type: '技能' })
  ], { tab: 'reserve' });
  assert.deepEqual(ids(), ['C3', 'C4', 'C2']);
  context.ui.type = '攻击';
  assert.deepEqual(ids(), ['C3', 'C2']);
  context.ui.sort = 'id';
  assert.deepEqual(ids(), ['C2', 'C3']);
});

test('explicit ID sorting remains in shared URLs and natural sorting is the default', () => {
  const { context } = fixture([], { sort: 'id' });
  context.saveUI();
  assert.equal(new URLSearchParams(context.location.hash.slice(1)).get('sort'), 'id');
  context.ui.sort = 'natural';
  context.saveUI();
  assert.equal(new URLSearchParams(context.location.hash.slice(1)).has('sort'), false);
  assert.match(source, /breakthrough:'all',sort:'natural'/);
  assert.match(fs.readFileSync(require.resolve('./build-readonly.cjs'), 'utf8'), /sort: 'natural'/);
  assert.match(fs.readFileSync(require.resolve('./deploy/build-editor.cjs'), 'utf8'), /ui:\{\.\.\.snapshot.ui,sort:'natural'\}/);
});


test('实际卡牌筛选组合多选、排除和不同分组，并保留X费与3费以上边界',()=>{
  const cards=[card('attack',{type:'攻击',cost:1}),card('skill',{type:'技能',cost:3}),card('power',{type:'能力',cost:4}),card('x',{type:'技能',cost:'X'})];
  const {context}=fixture(cards,{type:{included:['攻击','技能'],excluded:['能力']},cost:{included:['1','3+'],excluded:['X']}});
  assert.deepEqual(Array.from(context.filteredCards(),c=>c.id),['attack','skill']);
  context.ui.type={included:[],excluded:['攻击','能力']};context.ui.cost='all';
  assert.deepEqual(Array.from(context.filteredCards(),c=>c.id),['skill','x']);
});


test('trash uses every shared sort mode and direction instead of deletion time',()=>{
  const {context,ids}=fixture([
    card('C10',{name:'Zulu',type:'技能',rarity:'稀有',cost:3,turns:4,trashedAt:'2026-11-12'}),
    card('C2',{name:'Alpha',cost:1,turns:2,trashedAt:'2026-11-10'}),
    card('C1',{name:'Mike',type:'箭矢',cost:0,turns:1,trashedAt:'2026-11-11'}),
    card('active'),card('reserve',{reservedAt:'today'})
  ],{tab:'trash'});
  context.state.cardUpdatedAt={C1:30,C10:20};
  context.state.history=[{seq:40,patches:[{collection:'cards',uid:'C2'}]}];
  const expected={natural:['C1','C2','C10'],id:['C1','C2','C10'],name:['C2','C1','C10'],cost:['C1','C2','C10'],rarity:['C1','C2','C10'],type:['C2','C10','C1'],turns:['C1','C2','C10'],updated:['C2','C1','C10']};
  for(const [sort,order] of Object.entries(expected)){
    context.ui.sort=sort;context.ui.descending=false;assert.deepEqual(ids(),order,sort);
    context.ui.descending=true;assert.deepEqual(ids(),order.slice().reverse(),sort+' descending');
  }
});

test('trash applies every shared filter, combined exclusions, search and upgraded costs',()=>{
  const {context,ids}=fixture([
    card('C10',{name:'训练',text:'抽牌',type:'技能',rarity:'稀有',cost:3,upgradedCost:1,cardPool:'无色',archetypeIds:['a'],termIds:['k'],trashedAt:'today'}),
    card('C2',{cost:1,pool:false,archetypeIds:['b'],termIds:['l'],trashedAt:'today'}),
    card('C1',{type:'箭矢',cost:'X',cardPool:'衍生',archetypeIds:['a','b'],termIds:['k'],trashedAt:'today',design:{breakthrough:true}}),
    card('active',{name:'训练',type:'技能',rarity:'稀有',cost:3})
  ],{tab:'trash'});
  context.state.data.archetypes=[{id:'a'},{id:'b'}];
  for(const [key,value,expected] of [
    ['type','技能',['C10']],['rarity','稀有',['C10']],['cardPool','无色',['C10']],
    ['pool','granted',['C2']],['cost','3+',['C10']],['cost','X',['C1']],
    ['archetype','b',['C2','C1']],['term','l',['C2']],['search','训练',['C10']]
  ]){context.ui[key]=value;assert.deepEqual(ids(),expected,key);context.ui[key]=key==='search'?'':'all';}
  context.ui.type={included:['攻击','技能'],excluded:['箭矢']};
  context.ui.rarity={excluded:['普通']};context.ui.cost='3+';assert.deepEqual(ids(),['C10']);
  context.ui.upgraded=true;assert.deepEqual(ids(),[]);context.ui.cost='1';assert.deepEqual(ids(),['C10']);
  context.ui.type=context.ui.rarity=context.ui.cost='all';
  context.state.data.archetypes=[];
  context.JunnaRouteStatistics=require('./route-statistics.js');context.flowDefaults={routes:[]};
  context.ui.routePositions=['breakthrough'];assert.deepEqual(ids(),['C1']);
  context.ui.routePositions=[];context.ui.routeExcludedPositions=['breakthrough'];assert.deepEqual(ids(),['C2','C10']);
});
