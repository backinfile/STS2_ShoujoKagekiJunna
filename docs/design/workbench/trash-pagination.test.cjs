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
  const f=fixture();f.render();vm.runInContext('trashPage=18',f.context);f.render();
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
  assert.match(f.nodes['work-area'].innerHTML,/没有匹配的卡牌/);
});

test('card pool and reserve rendering retain their existing list behavior',()=>{
  for(const tab of ['cards','reserve']) {
    const f=fixture(50);f.context.ui.tab=tab;f.render();
    assert.equal(f.rendered.length,50);
    assert.doesNotMatch(f.nodes['work-area'].innerHTML,/data-trash-page/);
  }
});


test('changing any trash filter or sort resets pagination while unchanged choices retain the page',()=>{
  const changes={type:'技能',rarity:'稀有',cardPool:'无色',pool:'granted',cost:'3+',archetype:{included:['a'],excluded:['b']},term:'keyword',routePositions:['trigger'],routeExcludedPositions:['cross'],sort:'cost',descending:true,upgraded:true};
  for(const [key,value]of Object.entries(changes)){
    const f=fixture();f.render();vm.runInContext('trashPage=10',f.context);f.render();
    assert.equal(f.rendered[0],'card-240',key+' unchanged');
    f.context.ui[key]=value;f.render();assert.equal(f.rendered[0],'card-0',key+' changed');
  }
});

test('trash exposes shared sorting and upgraded faces while retaining empty and restore behavior',()=>{
  const f=fixture();
  f.context.sortControls=()=>'<select data-sort-select><option>自然排序</option></select>';
  f.context.cardEntryHTML=card=>'<article><button data-restore-card="'+card.uid+'">恢复</button></article>';
  f.render();
  assert.match(f.nodes['page-actions'].innerHTML,/data-sort-select/);
  assert.match(f.nodes['page-actions'].innerHTML,/data-action="upgrade"/);
  assert.doesNotMatch(f.nodes['page-actions'].innerHTML,/data-action="add"/);
  assert.match(f.nodes['work-area'].innerHTML,/data-restore-card="card-0"/);
  const empty=fixture(0);empty.render();assert.match(empty.nodes['work-area'].innerHTML,/垃圾桶为空/);
});

test('trash sidebar exposes all normal card filters and prunes obsolete keyword and route selections',()=>{
  const sidebar={innerHTML:''},context=vm.createContext({
    ui:{tab:'trash',search:'',archetype:{included:['valid','old'],excluded:[]},term:'obsolete'},
    state:{data:{cards:[],archetypes:[{id:'valid',name:'流派'}],terms:[{id:'keyword',name:'知识',kind:'关键词'}]}},
    JunnaFilters:require('./filter-state.js'),TYPES:['攻击','技能','箭矢'],RARITIES:['普通','稀有'],CARD_POOLS:['角色','无色'],
    escape:String,$:()=>sidebar,flowView:{positionFilters:()=>'<fieldset data-position-filter></fieldset>'}
  });
  const start=source.indexOf('  function renderSidebar(');
  vm.runInContext(source.slice(start,source.indexOf('  function cardLinks(',start)),context);
  context.renderSidebar();
  for(const key of ['cardPool','pool','type','rarity','cost','archetype','term'])assert.match(sidebar.innerHTML,new RegExp('data-filter-group="'+key+'"'),key);
  assert.match(sidebar.innerHTML,/data-position-filter/);assert.match(sidebar.innerHTML,/data-action="clear-filters"/);
  assert.match(sidebar.innerHTML,/placeholder="卡名、牌面、词条"/);
  assert.deepEqual(Array.from(context.ui.archetype.included),['valid']);assert.equal(context.ui.term,'all');
});
