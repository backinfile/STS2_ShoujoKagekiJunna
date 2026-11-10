'use strict';
const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const reference=require('./pool-reference.js');
const card=(id,extra={})=>({id,uid:id,name:id,cost:1,type:'攻击',rarity:'普通',pool:true,archetypeIds:['SYS_partner'],termIds:[],...extra});
const data={cards:[card('main',{partnerCardIds:['companion','companion']}),card('companion',{pool:false,cost:0,type:'技能'}),card('basic',{pool:false,rarity:'基础',archetypeIds:[]}),card('generated',{pool:false,type:'箭矢',arrowSubtype:'技能',archetypeIds:[]}),card('reserve',{reservedAt:'today'}),card('trash',{trashedAt:'today'})],partnerGroups:[{mainCardId:'main',grantedCardIds:['companion','companion','reserve','trash'],grantedCardCounts:{companion:3}}],archetypes:[{id:'SYS_partner',name:'知识',level:'次主流派'}],terms:[]};

test('all-face overview counts include knowledge companions in every distribution',()=>{
  const value=reference.compute(data,{includeAll:true});
  assert.equal(value.total,2);
  for(const rows of [value.costs,value.rarities,value.types])assert.equal(rows.reduce((sum,r)=>sum+r.current,0),2);
  assert.equal(value.routes[0].current,2);
  assert.equal(value.structure.find(r=>r.name==='总牌数').current,2);
});

test('all faces are the deduplicated union of pool cards and their related cards',()=>{
  const extra={...data,cards:[...data.cards,card('other',{partnerCardIds:['companion']})]};
  assert.deepEqual(reference.poolFaces(extra).map(c=>c.id),['main','companion','other']);
  const source=fs.readFileSync(require.resolve('./app.js'),'utf8');
  const part=source.slice(source.indexOf('  function overviewStats('),source.indexOf('  function overviewTable('));
  const ui={overviewScope:'cards',overviewAll:true};
  const context=vm.createContext({state:{data:extra},ui,JunnaPoolReference:reference,cardTab:c=>c.trashedAt?'trash':c.reservedAt?'reserve':'cards'});
  vm.runInContext(part,context);
  assert.deepEqual(Array.from(context.overviewStats().cards,c=>c.id),['main','companion','other']);
  ui.overviewAll=false;
  assert.deepEqual(Array.from(context.overviewStats().cards,c=>c.id),['main','other']);
});

test('default complete-pool reference keeps reward and basic cards and excludes inactive cards',()=>{
  const value=reference.compute(data);
  assert.equal(value.total,2);assert.equal(value.routes[0].current,1);
  assert.equal(value.rarities.find(r=>r.name==='普通').min,20);
});

test('overview checkbox reaches the reference tables and all faces use matching labels',()=>{
  const source=fs.readFileSync(require.resolve('./app.js'),'utf8');
  const part=source.slice(source.indexOf('  function overviewReference('),source.indexOf('  function overviewComparison('));
  const ui={overviewAll:true};
  const table=(title,headers,rows)=>'<table><caption>'+title+'</caption>'+headers.map(h=>'<th>'+h+'</th>').join('')+rows.join('')+'</table>';
  const context=vm.createContext({state:{data},ui,JunnaPoolReference:reference,escape:String,overviewTable:table});
  vm.runInContext(part,context);
  let html=context.overviewReference({scope:'cards'});
  assert.match(html,/全部牌面统计 · 2张/);
  assert.match(html,/<th>当前<\/th><th>建议<\/th><th>差额<\/th>/);
  assert.match(html,/<th>当前牌面<\/th><th>建议牌面<\/th><th>当前突破牌<\/th><th>建议突破牌<\/th>/);
  assert.match(html,/<th scope="row">普通<\/th><td>2张<\/td><td>20张<\/td><td>少18张<\/td>/);
  ui.overviewAll=false;html=context.overviewReference({scope:'cards'});
  assert.match(html,/完整卡池参考 · 2张/);assert.match(html,/建议奖励牌/);
});

test('overview type groups merge arrows into attack and skill across scopes and related faces',()=>{
  const source=fs.readFileSync(require.resolve('./app.js'),'utf8');
  const stats=source.slice(source.indexOf('  function overviewStats('),source.indexOf('  function overviewTable('));
  const comparison=source.slice(source.indexOf('  function overviewComparison('),source.indexOf('  function renderOverview('));
  const scopes=['cards','reserve','trash'];
  const cards=scopes.flatMap(scope=>{
    const flags=scope==='reserve'?{reservedAt:'today'}:scope==='trash'?{trashedAt:'today'}:{};
    return[
      card(scope+'-attack',{...flags,partnerCardIds:[scope+'-companion']}),
      card(scope+'-attack-arrow',{...flags,type:'箭矢',arrowSubtype:'攻击'}),
      card(scope+'-skill-arrow',{...flags,type:'箭矢',arrowSubtype:'技能'}),
      card(scope+'-companion',{...flags,type:'技能',pool:false})
    ];
  });
  const before=JSON.stringify(cards),ui={overviewScope:'cards',overviewAll:false},data={cards,partnerGroups:[],terms:[],meta:{}};
  let rendered,dimensions;
  const context=vm.createContext({
    state:{data},ui,JunnaPoolReference:reference,escape:String,
    cardTab:c=>c.trashedAt?'trash':c.reservedAt?'reserve':'cards',
    overviewDimensions:()=>dimensions,
    overviewTable:(title,headers,rows)=>{rendered={title,headers,rows};return '';}
  });
  vm.runInContext(stats+'\n'+comparison,context);
  for(const scope of scopes)for(const all of [false,true])for(const withRarity of [false,true]){
    ui.overviewScope=scope;ui.overviewAll=all;
    dimensions=[...(withRarity?[{key:'rarity',label:'稀有度'}]:[]),{key:'type',label:'卡牌类型',order:['攻击','技能','能力','箭矢']}];
    context.overviewComparison(context.overviewStats());
    const attack=rendered.rows.find(row=>row.includes('>攻击</')),skill=rendered.rows.find(row=>row.includes('>技能</'));
    assert.equal(rendered.rows.length,3,scope);
    assert.match(attack,/<td>2<\/td>/);
    assert.match(skill,new RegExp('<td>'+(all?2:1)+'</td>'));
    assert(attack.includes(all?'50.0%':'66.7%'));
    assert(skill.includes(all?'50.0%':'33.3%'));
    assert(!rendered.rows.join('').includes('箭矢'));
    assert.match(rendered.rows.at(-1),new RegExp('<td>'+(all?4:3)+'</td>'));
  }
  assert.equal(JSON.stringify(cards),before);
});


test('全部牌面沿入池牌的直接关联统计，共用成员不会引入其他入口',()=>{
  const data={cards:[card('A',{pool:false}),card('B',{pool:true}),card('C',{pool:false})],partnerGroups:[
    {mainCardId:'A',grantedCardIds:['B','C']},{mainCardId:'B',grantedCardIds:['C']}
  ]};
  assert.deepEqual(reference.poolFaces(data).map(c=>c.id),['B','C']);
});
