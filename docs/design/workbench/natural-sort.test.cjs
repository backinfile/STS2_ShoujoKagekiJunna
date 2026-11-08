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
  const context = vm.createContext({
    ui: {
      tab: 'cards', sort: 'natural', descending: false, upgraded: false, search: '',
      type: 'all', rarity: 'all', pool: 'all', cost: 'all', archetype: 'all', term: 'all',
      ...overrides
    },
    state: { data: { cards, archetypes: [] }, history: [] },
    cardTab: value => value.trashedAt ? 'trash' : value.reservedAt ? 'reserve' : 'cards',
    JunnaRouteStatistics: { matchesPosition: () => true }, flowDefaults: {},
    localStorage: { setItem() {} }, uiKey: 'test', previewMode: false,
    location: { hash: '' }, history: { replaceState(_state, _title, hash) { context.location.hash = hash; } },
    URLSearchParams
  });
  const save = source.slice(source.indexOf('  function saveUI('), source.indexOf('\n', source.indexOf('  function saveUI(')));
  vm.runInContext(constants + '\n' + filtering + '\n' + save, context);
  return { context, ids: () => Array.from(context.filteredCards(), value => value.id) };
}

test('natural sorting compares rarity, type, cost and numeric card ID in order', () => {
  const { ids } = fixture([
    card('C10'), card('C2'), card('C3', { cost: 0 }),
    card('S', { type: '技能', cost: 0 }), card('P', { type: '能力', cost: 0 }),
    card('A', { type: '箭矢', cost: 0 }), card('U', { rarity: '罕见', cost: 0 }),
    card('R', { rarity: '稀有', cost: 0 }), card('B', { rarity: '基础', cost: 9 }),
    card('E', { rarity: '远古', cost: 0 })
  ]);
  assert.deepEqual(ids(), ['B', 'C3', 'C2', 'C10', 'S', 'P', 'A', 'U', 'R', 'E']);
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
