'use strict';
const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const domain=require('./workbench-domain.js');
function fixture(){return {cards:[
  {uid:'card_A',id:'A',name:'埋头学习',pool:true,rarity:'罕见'},
  {uid:'card_B',id:'B',name:'心得',pool:false,rarity:'罕见'}
],partnerGroups:[
  {uid:'group_A',mainCardId:'A',grantedCardIds:['B'],grantedCardCounts:{B:2}},
  {uid:'group_B',mainCardId:'B',mainCardCount:2,grantedCardIds:['A'],grantedCardCounts:{A:1}}
]};}
function mainButton(data,cardUid,originUid=cardUid){
  const source=fs.readFileSync(require.resolve('./app.js'),'utf8'),start=source.indexOf('  function renderInspector(){');
  const prefix=source.slice(start,source.indexOf("    $('inspector-title').textContent=",start));
  const nodes=new Map(),$=id=>{if(!nodes.has(id))nodes.set(id,{dataset:{},removeAttribute(){},querySelectorAll(){return [];}});return nodes.get(id);};
  const context=vm.createContext({$,state:{data},ui:{tab:'cards',selected:{collection:'cards',uid:cardUid,cardOrigin:originUid}},current:()=>data.cards.find(c=>c.uid===cardUid),mobileLayout:{update(){}},JunnaWorkbenchDomain:domain});
  vm.runInContext(prefix+'\n}\nrenderInspector();',context);
  return $('set-group-main');
}

test('独立关联入口的主牌按钮按入池身份显示，不受打开来源影响',()=>{
  const data=fixture(),before=structuredClone(data);
  for(const origin of ['card_A','card_B'])for(const card of data.cards){
    const button=mainButton(data,card.uid,origin);
    assert.equal(button.textContent,card.pool?'当前主牌':'设置为主牌',card.name+' / '+origin);
    assert.equal(button.disabled,card.pool,card.name+' / '+origin);
  }
  assert.deepEqual(data,before);
});

test('直接打开心得可设置主牌并切回埋头学习，保留一张加两张及独立列表',()=>{
  const data=fixture(),before=structuredClone(data);
  assert.equal(domain.setGroupMain(data,'group_B','card_B'),true);
  assert.deepEqual(data,{...before,cards:before.cards.map(card=>({...card,pool:card.id==='B'}))});
  for(const card of data.cards)assert.equal(mainButton(data,card.uid).disabled,card.pool);
  assert.equal(domain.setGroupMain(data,'group_A','card_A'),true);
  assert.deepEqual(data,before);
});

test('直接入口没有同组入池牌时可入池，保留其他独立入口身份',()=>{
  for(const hasOtherEntry of [false,true]){
    const data=fixture();
    if(hasOtherEntry)data.partnerGroups[0].grantedCardIds=[];
    else data.cards[0].pool=false;
    const before=structuredClone(data);
    assert.equal(domain.setGroupMain(data,'group_B','card_B'),true);
    assert.deepEqual(data,{...before,cards:before.cards.map(card=>card.id==='B'?{...card,pool:true}:card)});
    assert.equal(domain.setGroupMain(data,'group_B','card_B'),false);
  }
});
