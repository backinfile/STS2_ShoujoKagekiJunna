'use strict';
const test=require('node:test');
const assert=require('node:assert/strict');
const domain=require('./workbench-domain.js');
const makeCard=(id,pool)=>({uid:'card_'+id,id,name:id,type:'技能',rarity:'罕见',pool,cost:1,upgradedCost:0,text:'知识。获得1点格挡。',upgradedText:'知识。获得2点格挡。',keywords:['知识'],termIds:[],archetypeIds:[],partnerCardIds:[],art:'portrait_'+id,naming:{original:id}});
function fixture(){return {cards:[makeCard('A',true),makeCard('B',false),makeCard('C',false)],partnerGroups:[{uid:'group_A',id:'GROUP_A',title:'A',mainCardId:'A',grantedCardIds:['B','C'],grantedCardCounts:{B:2,C:1}}],meta:{startingDeck:[{cardId:'A',count:1}]}};}
const quantities=group=>Object.fromEntries([group.mainCardId,...group.grantedCardIds].sort().map(id=>[id,domain.groupCardCount(group,id)]));

test('知识关联牌分别保留诅咒和衍生分类，卡池归属与入池状态独立',()=>{
  const data=fixture();
  Object.assign(data.cards[0],{cardPool:'角色'});
  Object.assign(data.cards[1],{type:'诅咒',rarity:'诅咒',cardPool:'诅咒'});
  Object.assign(data.cards[2],{rarity:'衍生',cardPool:'衍生'});
  const normalize=domain.create({seed:{data:structuredClone(data)},uid:()=>assert.fail('不应生成新编号'),JunnaFitPool:{normalizeEntry:entry=>entry}});
  const reloaded=normalize(JSON.parse(JSON.stringify(normalize(data))));
  assert.deepEqual(reloaded.cards.map(({rarity,cardPool,pool})=>[rarity,cardPool,pool]),[['罕见','角色',true],['诅咒','诅咒',false],['衍生','衍生',false]]);
  assert.deepEqual(reloaded.cards[0].partnerCardIds,['B','C']);
  assert.throws(()=>normalize({...data,cards:[{...data.cards[0],cardPool:'未知'}]}),/卡池/);
});

test('本体特殊稀有度与卡牌类型经过标准化和导出读取后保留，入池身份独立',()=>{
  const classifications=[['技能','事件'],['攻击','衍生'],['状态','状态'],['诅咒','诅咒'],['任务','任务']];
  const cards=classifications.map(([type,rarity],i)=>({...makeCard('S'+i,false),type,rarity,keywords:[],text:'',upgradedText:''}));
  const data={cards,partnerGroups:[]};
  const normalize=domain.create({seed:{data:structuredClone(data)},uid:()=>assert.fail('不应生成新编号'),JunnaFitPool:{normalizeEntry:entry=>entry}});
  const reloaded=normalize(JSON.parse(JSON.stringify(normalize(data))));
  assert.deepEqual(reloaded.cards.map(({type,rarity,pool})=>[type,rarity,pool]),classifications.map(([type,rarity])=>[type,rarity,false]));
  const root={...makeCard('root',true),partnerCardIds:cards.map(c=>c.id)};
  const reference=require('./pool-reference.js').compute({...reloaded,cards:[root,...reloaded.cards]},{includeAll:true});
  assert.equal(reference.rarities.reduce((sum,row)=>sum+row.current,0),cards.length+1);
  assert.equal(reference.unknownTypes,0);
  for(const [,rarity] of classifications)assert.ok(require('./route-statistics.js').rarities.includes(rarity));
});

test('设置主牌保留组内每张牌的数量、其他牌面字段与初始牌组',()=>{
  const data=fixture(),before=structuredClone(data),counts=quantities(data.partnerGroups[0]);
  assert.equal(domain.setGroupMain(data,'group_A','card_B'),true);
  assert.equal(data.partnerGroups[0].mainCardId,'B');
  assert.deepEqual(data.partnerGroups[0].grantedCardIds,['A','C']);
  assert.deepEqual(quantities(data.partnerGroups[0]),counts);
  for(const card of data.cards){const old=before.cards.find(entry=>entry.id===card.id);assert.deepEqual(card,{...old,pool:card.id==='B'});}
  assert.deepEqual(data.meta,before.meta);
  assert.equal(domain.setGroupMain(data,'group_A','card_B'),false);
});

test('多张主牌经过标准化、导出读取及切回后仍保留原数量',()=>{
  let data=fixture();
  const normalize=domain.create({seed:{data:structuredClone(data)},uid:()=>assert.fail('不应生成新编号'),JunnaFitPool:{normalizeEntry:entry=>entry}});
  data=normalize(data);const before=structuredClone(data);
  domain.setGroupMain(data,'group_A','card_B');data=normalize(JSON.parse(JSON.stringify(data)));
  assert.equal(data.partnerGroups[0].mainCardCount,2);
  assert.deepEqual(quantities(data.partnerGroups[0]),{A:1,B:2,C:1});
  assert.equal(data.cards.filter(card=>card.pool).length,1);
  for(const card of data.cards)assert.equal(card.rarity,'罕见');
  domain.setGroupMain(data,'group_A','card_A');data=normalize(data);
  assert.deepEqual(data,before);
});

test('同一种知识牌三张经过标准化和导出读取后保留同一模型及数量',()=>{
  const data=fixture();
  data.cards=data.cards.slice(0,1);
  Object.assign(data.partnerGroups[0],{mainCardCount:3,grantedCardIds:[],grantedCardCounts:{}});
  const normalize=domain.create({seed:{data:structuredClone(data)},uid:()=>assert.fail('不应生成新编号'),JunnaFitPool:{normalizeEntry:entry=>entry}});
  const normalized=normalize(data);
  const group=normalized.partnerGroups[0];
  assert.equal(normalized.cards.length,1);
  assert.equal(domain.groupCardCount(group,'A'),3);
  assert.deepEqual(group.grantedCardIds,[]);
  assert.deepEqual(normalized.cards[0].partnerCardIds,[]);
  assert.ok(normalized.cards[0].keywords.includes('知识'));
  assert.equal(normalized.cards[0].pool,true);
  assert.equal(domain.cardGroup(normalized,'card_A').cards.length,1);
  assert.deepEqual(normalize(JSON.parse(JSON.stringify(normalized))),normalized);
  group.mainCardCount=1;
  assert.equal(normalize(normalized).partnerGroups.length,0);
});

test('切换主牌保留备用与删除状态及自定义组名',()=>{
  const data=fixture();data.cards[0].trashedAt='2026-11-01';data.cards[1].reservedAt='2026-11-02';data.partnerGroups[0].title='阅读组';
  domain.setGroupMain(data,'group_A','card_B');
  assert.equal(data.cards[0].trashedAt,'2026-11-01');
  assert.equal(data.cards[1].reservedAt,'2026-11-02');
  assert.equal(data.partnerGroups[0].title,'阅读组');
  const before=structuredClone(data);
  assert.throws(()=>domain.setGroupMain(data,'group_A','card_A'),/请选择/);
  assert.deepEqual(data,before);
});

test('切换主牌保留其他列表，允许共享关联，已有列表的牌返回错误',()=>{
  const data=fixture();data.cards.push(makeCard('D',true),makeCard('E',false));
  data.partnerGroups.push({uid:'group_D',id:'GROUP_D',mainCardId:'D',grantedCardIds:['E'],grantedCardCounts:{E:1}});
  const other=structuredClone(data.partnerGroups[1]);
  domain.setGroupMain(data,'group_A','card_B');
  assert.deepEqual(data.partnerGroups[1],other);
  assert.throws(()=>domain.setGroupMain(data,'group_A','card_E'),/不属于/);
  data.partnerGroups[1].grantedCardIds.push('C');data.partnerGroups[1].grantedCardCounts.C=1;
  const before=structuredClone(data);
  assert.equal(domain.setGroupMain(data,'group_A','card_C'),true);
  assert.deepEqual(data.partnerGroups[1],before.partnerGroups[1]);
  data.partnerGroups[0].grantedCardIds.push('D');
  const owned=structuredClone(data);
  assert.throws(()=>domain.setGroupMain(data,'group_A','card_D'),/已有自己的关联牌列表/);
  assert.deepEqual(data,owned);
});


test('每张牌的关联列表独立，允许多入口共享和互相引用，卡池身份独立',()=>{
  const data=fixture();
  data.cards.push(makeCard('D',true));
  data.partnerGroups.push(
    {uid:'group_D',id:'GROUP_D',mainCardId:'D',grantedCardIds:['B'],grantedCardCounts:{B:3}},
    {uid:'group_B',id:'GROUP_B',mainCardId:'B',grantedCardIds:['A'],grantedCardCounts:{A:1}}
  );
  const normalize=domain.create({seed:{data:structuredClone(data)},uid:()=>assert.fail('缺少编号'),JunnaFitPool:{normalizeEntry:entry=>entry}});
  const before=normalize(data);
  assert.deepEqual(before.cards.map(c=>[c.id,c.partnerCardIds,c.pool]),[
    ['A',['B','C'],true],['B',['A'],false],['C',[],false],['D',['B'],true]
  ]);
  const changed=structuredClone(before);
  changed.partnerGroups[0].grantedCardIds=['C'];
  changed.partnerGroups[0].grantedCardCounts={C:4};
  const after=normalize(changed);
  for(const id of ['B','C','D'])assert.deepEqual(after.cards.find(c=>c.id===id),before.cards.find(c=>c.id===id));
  assert.deepEqual(after.partnerGroups.slice(1),before.partnerGroups.slice(1));
  assert.deepEqual(normalize(JSON.parse(JSON.stringify(after))),after);
  assert.equal(domain.cardGroup(after,'card_C'),null);
  assert.equal(domain.cardGroup(after,'card_B','card_D').group.mainCardId,'D');
});

test('移除关联保留目标的知识身份和入池状态',()=>{
  const data=fixture();data.cards[1].pool=true;
  const normalize=domain.create({seed:{data:structuredClone(data)},uid:()=>assert.fail('缺少编号'),JunnaFitPool:{normalizeEntry:entry=>entry}});
  const before=normalize(data),changed=structuredClone(before);
  changed.partnerGroups=[];
  const after=normalize(changed);
  assert.deepEqual(after.cards[1],before.cards[1]);
  assert.ok(after.cards[0].keywords.includes('知识'));
  assert.deepEqual(after.cards[0].partnerCardIds,[]);
});


for(const action of ['trash','reserve','pool']){
  test('整组'+action+'保留其他入口仍在引用的共享成员',()=>{
    const data={cards:[makeCard('A',true),makeCard('B',false),makeCard('C',true)],partnerGroups:[
      {uid:'group_A',mainCardId:'A',grantedCardIds:['B']},
      {uid:'group_C',mainCardId:'C',grantedCardIds:['B']}
    ]};
    if(action==='pool')for(const card of data.cards)card.reservedAt='before';
    const shared=structuredClone(data.cards[1]),other=structuredClone(data.cards[2]);
    const changed=domain.moveCards(data,'card_A',action,{group:true,at:'after'});
    assert.deepEqual(changed,['card_A']);
    assert.deepEqual(data.cards[1],shared);
    assert.deepEqual(data.cards[2],other);
  });
}

test('整组操作保留关联牌自己的入口与入池身份，仍处理独有成员',()=>{
  const data=fixture();
  data.cards.push(makeCard('D',false));
  data.cards[1].pool=true;
  data.partnerGroups.push({uid:'group_C',mainCardId:'C',grantedCardIds:['D']});
  const before=structuredClone(data);
  assert.deepEqual(domain.moveCards(data,'card_A','trash',{group:true,at:'after'}),['card_A']);
  for(const id of ['B','C','D'])assert.deepEqual(data.cards.find(c=>c.id===id),before.cards.find(c=>c.id===id));
  const exclusive=fixture();
  assert.deepEqual(domain.moveCards(exclusive,'card_A','trash',{group:true,at:'after'}),['card_A','card_B','card_C']);
  assert.ok(exclusive.cards.every(c=>c.trashedAt==='after'));
});
