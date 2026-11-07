'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

function fixture() {
  const source = fs.readFileSync(require.resolve('./app.js'), 'utf8');
  const sorting = source.slice(source.indexOf('  const CARD_SORTS='), source.indexOf('  function renderWorkspace('));
  const calls = { renders: 0, saves: 0, scrolls: 0, focus: null };
  const context = vm.createContext({
    ui: { sort: 'rarity', descending: false },
    flushEdit() {},
    renderWorkspace() { calls.renders++; },
    saveUI() { calls.saves++; },
    mobileLayout: { scrollToTop() { calls.scrolls++; } },
    $: () => ({ querySelector: selector => ({ focus: () => { calls.focus = selector; } }) })
  });
  vm.runInContext(sorting, context);
  return { context, calls };
}

test('phone sorting exposes the same options and selected value as desktop controls', () => {
  const { context } = fixture();
  const markup = context.sortControls();
  assert.match(markup, /<select class="mobile-sort-select" data-sort-select aria-label="排序">/);
  assert.equal((markup.match(/<option /g) || []).length, 8);
  assert.match(markup, /<option value="rarity" selected>稀有度<\/option>/);
  assert.match(markup, /<option value="natural">自然排序<\/option>/);
  assert.match(markup, /data-sort-key="natural"/);
  assert.equal((markup.match(/data-sort-key=/g) || []).length, 8);
});

test('selecting a phone sort updates shared state, resets scrolling and restores focus', () => {
  const { context, calls } = fixture();
  const select = { value: 'cost' };
  const changed = context.changeSort({ type: 'change', target: { closest: selector => selector === '[data-sort-select]' ? select : null } });
  assert.equal(changed, true);
  assert.equal(context.ui.sort, 'cost');
  assert.equal(context.ui.descending, false);
  assert.equal(calls.renders, 1);
  assert.equal(calls.saves, 1);
  assert.equal(calls.scrolls, 1);
  assert.equal(calls.focus, '[data-sort-select]');
});

test('opening the phone selector preserves its native picker', () => {
  const { context, calls } = fixture();
  const handled = context.changeSort({ type: 'click', target: { closest: () => null } });
  assert.equal(handled, false);
  assert.equal(calls.renders, 0);
});

test('desktop sort buttons and the compact direction button retain their behavior', () => {
  const { context, calls } = fixture();
  const button = { dataset: { sortKey: 'name' } };
  context.changeSort({ type: 'click', target: { closest: () => button } });
  assert.equal(context.ui.sort, 'name');
  assert.equal(calls.focus, '[data-sort-key="name"]');
  button.dataset = {};
  context.changeSort({ type: 'click', target: { closest: () => button } });
  assert.equal(context.ui.descending, true);
  assert.equal(calls.focus, '[data-sort-direction]');
});
