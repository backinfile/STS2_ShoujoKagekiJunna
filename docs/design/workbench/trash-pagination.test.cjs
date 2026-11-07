'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync(require.resolve('./app.js'), 'utf8');

function fixture(count = 437) {
  const cards = Array.from({length:count}, (_,i)=>({uid:'card-'+i, pool:true, trashedAt:'2026-11-07'}));
  const nodes = Object.fromEntries(['page-title','page-actions','work-area','result-count'].map(id=>[id,{innerHTML:'',textContent:'',setAttribute(){}}]));
  const rendered = [];
  const context = vm.createContext({
    ui:{tab:'trash',search:''}, state:{data:{cards}},
    $:id=>nodes[id], filteredCards:()=>cards.filter(card=>!context.ui.search||card.uid===context.ui.search),
    cardTab:card=>card.trashedAt?'trash':'cards', sortControls:()=>'',
    cardEntryHTML(card){rendered.push(card.uid);return '<article>'+card.uid+'</article>';},
    fitCards(){},updateChrome(){}
  });
  const start = source.includes('  const TRASH_PAGE_SIZE=') ? source.indexOf('  const TRASH_PAGE_SIZE=') : source.indexOf('  function renderWorkspace(');
  vm.runInContext(source.slice(start,source.indexOf('  function renderAll(',start)),context);
  return {context,cards,nodes,rendered,render(){rendered.length=0;context.renderWorkspace();}};
}

test('opening trash renders one page while keeping the full result count',()=>{
  const f=fixture();f.render();
  assert.equal(f.rendered.length,24,'trash must not build every card face on tab switch');
  assert.equal(f.nodes['result-count'].textContent,'437 张卡牌');
  assert.match(f.nodes['work-area'].innerHTML,/1 \/ 19/);
});

test('last page and restoring cards clamp to the remaining page',()=>{
  const f=fixture();vm.runInContext('trashPage=18',f.context);f.render();
  assert.deepEqual(f.rendered,['card-432','card-433','card-434','card-435','card-436']);
  f.cards.splice(432);f.render();
  assert.equal(f.rendered.length,24);
  assert.equal(f.rendered[0],'card-408');
  assert.match(f.nodes['work-area'].innerHTML,/18 \/ 18/);
});

test('search covers every page, resets pagination, and handles empty results',()=>{
  const f=fixture();f.render();vm.runInContext('trashPage=18',f.context);
  f.context.ui.search='card-400';f.render();
  assert.deepEqual(f.rendered,['card-400']);
  assert.doesNotMatch(f.nodes['work-area'].innerHTML,/data-trash-page/);
  f.context.ui.search='';f.render();assert.equal(f.rendered[0],'card-0');
  f.context.ui.search='missing';f.render();assert.equal(f.rendered.length,0);
  assert.match(f.nodes['work-area'].innerHTML,/垃圾桶为空/);
});

test('card pool and reserve rendering retain their existing list behavior',()=>{
  for(const tab of ['cards','reserve']) {
    const f=fixture(50);f.context.ui.tab=tab;f.render();
    assert.equal(f.rendered.length,50);
    assert.doesNotMatch(f.nodes['work-area'].innerHTML,/data-trash-page/);
  }
});
