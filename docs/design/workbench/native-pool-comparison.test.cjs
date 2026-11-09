'use strict';
const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const native=require('./native-card-pools.js'),comparison=require('./native-pool-comparison.js');
const card=(id,extra={})=>({id,rarity:'普通',type:'攻击',cost:1,upgradedCost:1,pool:true,...extra});
const cards=[card('attack'),card('arrow',{type:'箭矢',arrowSubtype:'攻击'}),card('companion',{pool:false,type:'技能',cost:0,upgradedCost:0}),card('basic',{pool:false,rarity:'基础'}),card('ancient',{rarity:'远古'}),card('reserve',{reservedAt:'today'}),card('trash',{trashedAt:'today'})];
const dimensions=[{key:'rarity',label:'稀有度',order:['基础','普通','罕见','稀有','远古']},{key:'type',label:'类型',order:['攻击','技能','能力']}];

test('native snapshot has five distinct unlocked character pools and 82 rewards per class',()=>{
  assert.equal(native.source.cardCount,439);assert.match(native.source.sha256,/^[a-f0-9]{64}$/);
  assert.deepEqual(native.characters.map(c=>c.cards.length),[87,88,88,88,88]);
  for(const character of native.characters){
    assert.equal(new Set(character.cards.map(c=>c.id)).size,character.cards.length);
    assert.equal(character.cards.filter(c=>c.pool).length,82);
    assert.deepEqual(['普通','罕见','稀有'].map(r=>character.cards.filter(c=>c.rarity===r).length),[20,36,26]);
    for(const card of character.cards)assert(card.upgradedCost==='X'||Number.isInteger(card.upgradedCost)&&card.upgradedCost>=0);
  }
});

test('native costs distinguish actual X cards from effects referring to X cards',()=>{
  const all=native.characters.flatMap(c=>c.cards),find=id=>all.find(c=>c.id===id);
  for(const [id,cost,upgradedCost]of [['BULLET_TIME',3,2],['ALL_FOR_ONE',2,2],['SCRAPE',1,1],['BODY_SLAM',1,0],['DUALCAST',1,0],['WHIRLWIND','X','X']]){
    assert.deepEqual([find(id)?.cost,find(id)?.upgradedCost],[cost,upgradedCost],id);
  }
  assert.equal(find('SOVEREIGN_BLADE'),undefined,'independent token pool is excluded');
});

test('reward comparison aligns scope, counts arrow subtypes, and keeps union groups',()=>{
  const value=comparison.compute(cards,{dimensions});
  assert.deepEqual(value.columns.map(c=>c.total),[2,82,82,82,82,82]);
  assert.equal(value.rows.find(r=>r.values.join('/')==='普通/攻击').counts[0],2);
  assert(value.rows.some(r=>r.counts[0]===0&&r.counts.slice(1).some(n=>n>0)));
  assert.equal(value.rows.some(r=>r.values.includes('箭矢')),false);
  for(let i=0;i<6;i++)assert.equal(value.rows.reduce((n,r)=>n+r.counts[i],0),value.columns[i].total);
  assert.equal(cards[1].type,'箭矢');
});

test('all-face comparison includes knowledge companions and excludes reserve and trash',()=>{
  const value=comparison.compute(cards,{dimensions,includeAll:true});
  assert.deepEqual(value.columns.map(c=>c.total),[5,87,88,88,88,88]);
  for(let i=0;i<6;i++)assert.equal(value.rows.reduce((n,r)=>n+r.counts[i],0),value.columns[i].total);
  const cross=comparison.compute(cards,{dimensions:[{key:'cost'},{key:'upgradedCost'}],includeAll:true});
  assert.equal(cross.rows.find(r=>r.values.join('/')==='0/0').counts[0],1);
  assert.equal(cross.rows.find(r=>r.values.join('/')==='1/0').counts[0],0);
});

test('custom dimensions stay out of native grouping; empty Junna pool retains native data',()=>{
  const value=comparison.compute([],{dimensions:[{key:'system'},{key:'role'},{key:'pool'}]});
  assert.equal(value.dimensions.length,0);assert.equal(value.rows.length,1);
  assert.deepEqual(value.rows[0].counts,[0,82,82,82,82,82]);
});

test('overview renders the six classes, matching checkbox scope and selected shared dimensions',()=>{
  const source=fs.readFileSync(require.resolve('./app.js'),'utf8'),part=source.slice(source.indexOf('  function overviewNativeComparison('),source.indexOf('  function renderOverview('));
  let html;
  const ui={overviewAll:false},context=vm.createContext({ui,overviewDimensions:()=>[...dimensions,{key:'system'}],JunnaNativePoolComparison:comparison,escape:String,overviewTable:(title,headers,rows)=>{html={title,headers,rows};return '';}});
  vm.runInContext(part,context);
  context.overviewNativeComparison({scope:'cards',cards});
  assert.equal(html.headers.length,8);assert.equal(html.title,'本体五职业对比 · 奖励牌');assert.match(html.rows.at(-1),/82 · 100.0%/);
  ui.overviewAll=true;context.overviewNativeComparison({scope:'cards',cards});
  assert.equal(html.title,'本体五职业对比 · 职业牌面');assert.match(html.rows.at(-1),/5 · 100.0%/);assert.match(html.rows.at(-1),/87 · 100.0%/);
  assert.equal(context.overviewNativeComparison({scope:'reserve',cards}),'');
  assert.match(source,/overviewComparison\(stats\)\+overviewNativeComparison\(stats\)/);
});
