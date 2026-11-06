'use strict';
const test=require('node:test');
const assert=require('node:assert/strict');
const domain=require('./workbench-domain.js');
const makeCard=(id,pool)=>({uid:'card_'+id,id,name:id,type:'技能',rarity:'罕见',pool,cost:1,upgradedCost:0,text:'知识。获得1点格挡。',upgradedText:'知识。获得2点格挡。',keywords:['知识'],termIds:[],archetypeIds:[],partnerCardIds:[],art:'portrait_'+id,naming:{original:id}});
function fixture(){return {cards:[makeCard('A',true),makeCard('B',false),makeCard('C',false)],partnerGroups:[{uid:'group_A',id:'GROUP_A',title:'A',mainCardId:'A',grantedCardIds:['B','C'],grantedCardCounts:{B:2,C:1}}],meta:{startingDeck:[{cardId:'A',count:1}]}};}
const quantities=group=>Object.fromEntries([group.mainCardId,...group.grantedCardIds].sort().map(id=>[id,domain.groupCardCount(group,id)]));

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

test('不修改其他知识组；无效成员与共享关联在变更前返回错误',()=>{
  const data=fixture();data.cards.push(makeCard('D',true),makeCard('E',false));
  data.partnerGroups.push({uid:'group_D',id:'GROUP_D',mainCardId:'D',grantedCardIds:['E'],grantedCardCounts:{E:1}});
  const other=structuredClone(data.partnerGroups[1]);
  domain.setGroupMain(data,'group_A','card_B');
  assert.deepEqual(data.partnerGroups[1],other);
  assert.throws(()=>domain.setGroupMain(data,'group_A','card_E'),/不属于/);
  data.partnerGroups[1].grantedCardIds.push('C');data.partnerGroups[1].grantedCardCounts.C=1;
  const before=structuredClone(data);
  assert.throws(()=>domain.setGroupMain(data,'group_A','card_C'),/其他知识组/);
  assert.deepEqual(data,before);
});
