const test=require('node:test');
const assert=require('node:assert/strict');
const E=require('./engine.cjs');
const cards=entries=>entries.map((card,id)=>({damage:0,hits:1,cost:0,block:0,draw:0,turns:0,retain:false,exhaust:false,...card,id}));
const target=(hp=100,damage=7)=>({enemies:[{name:'靶',hp:[hp,hp],moves:[{damage}]}]});
function hand(entries,encounter=target()) {
  const s=E.initialState(cards(entries),encounter,13);s.draw=[];s.hand=s.cards.map(card=>card.id);s.energy=3;return s;
}
test('2回合箭矢实际经过两次敌方行动，期间实体不参加洗牌',()=>{
  const s=hand([{name:'箭',kind:'arrow',cost:1,turns:2,damage:18,probe:true}]);
  E.play(s,{id:0,target:0});assert.deepEqual(s.discard,[]);
  E.enemyTurn(s);s.turn=2;E.playerStart(s);
  assert.equal(s.enemies[0].hp,100);assert.equal(s.arrows[0].remaining,1);assert.equal(s.hand.length,0);
  E.enemyTurn(s);s.turn=3;E.playerStart(s);
  assert.equal(s.stats.damageTaken,14);assert.equal(s.enemies[0].hp,82);assert.equal(s.arrows.length,0);
  assert.deepEqual(s.hand,[0]);
});
test('加速支付费用，实际减少量不超过余下回合，消耗工具退出循环',()=>{
  const s=hand([{name:'箭',kind:'arrow',cost:1,turns:1,damage:18},{name:'群加速',kind:'advance',cost:2,advance:2,scope:'all',exhaust:true}]);
  E.play(s,{id:0,target:0});E.play(s,{id:1});
  assert.equal(s.energy,0);assert.equal(s.stats.acceleration,1);assert.equal(s.enemies[0].hp,82);
  assert.deepEqual(s.exhausted,[1]);assert.deepEqual(s.discard,[0]);
});
test('锁定目标死亡后不把箭矢伤害转移给其他敌人',()=>{
  const encounter={enemies:[{hp:[5,5],moves:[{}]},{hp:[20,20],moves:[{}]}]};
  const s=hand([{name:'箭',kind:'arrow',turns:1,damage:12,probe:true},{name:'击杀',kind:'attack',damage:5}],encounter);
  E.play(s,{id:0,target:0});E.play(s,{id:1,target:0});E.enemyTurn(s);s.turn=2;E.playerStart(s);
  assert.equal(s.enemies[1].hp,20);assert.equal(s.stats.probeWaste,12);
});
test('格挡在自己的下回合开始清空，敌人击杀后不再攻击',()=>{
  const s=hand([{name:'格挡',kind:'block',block:10}]);E.play(s,{id:0});E.enemyTurn(s);
  assert.equal(s.stats.damageTaken,0);assert.equal(s.block,3);
  s.turn=2;E.playerStart(s);assert.equal(s.block,0);s.enemies[0].hp=0;E.enemyTurn(s);assert.equal(s.stats.damageTaken,0);
});
test('未知抽牌顺序不会泄露给出牌策略',()=>{
  const s=hand([{name:'抽牌',kind:'block',cost:1,block:5,draw:2},{name:'攻击',kind:'attack',cost:1,damage:10},{name:'未见牌A',kind:'attack',damage:100},{name:'未见牌B',kind:'block',block:100}]);
  s.hand=[0,1];s.draw=[2,3];const other=structuredClone(s);other.draw=[3,2];
  assert.deepEqual(E.decide(s,E.config.policies.balanced),E.decide(other,E.config.policies.balanced));
});
test('保留牌跨回合占用真实手牌，抽牌技能结算时自身尚未入弃牌堆',()=>{
  const s=hand([{name:'保留',kind:'advance',retain:true,cost:1,advance:1,scope:'one'},{name:'抽牌',kind:'block',cost:1,block:1,draw:2}]);
  E.play(s,{id:1});assert.equal(s.stats.cardsDrawn,0);E.enemyTurn(s);
  assert.deepEqual(s.hand,[0]);s.turn=2;E.playerStart(s);assert.deepEqual(s.hand,[0,1]);
});
test('0回合箭矢与同伤害即时攻击具有相同战斗结果',()=>{
  const pool=E.loadPool(),encounter=E.config.encounters.find(e=>e.id==='worm');
  for(const upgraded of [false,true])for(const cost of [0,1,2])for(const seed of [11,22,33]) {
    const options={cost,wait:0,bonus:0,context:'normal',density:2,upgraded};
    const a=E.fight(E.buildDeck({...options,immediate:true},pool),encounter,seed);
    const b=E.fight(E.buildDeck(options,pool),encounter,seed);
    assert.deepEqual([a.win,a.loss,a.turns,a.cardsDrawn],[b.win,b.loss,b.turns,b.cardsDrawn]);
  }
});
test('每个实际卡牌实例始终只存在于一个牌区',()=>{
  const pool=E.loadPool(),deck=E.buildDeck({cost:0,wait:2,bonus:3,context:'batch',density:5},pool);
  assert.equal(deck.length,17);
  const s=E.initialState(deck,target(500,5),71);
  const check=()=>{
    const all=[...s.draw,...s.hand,...s.discard,...s.exhausted,...s.arrows.map(a=>a.id)];
    assert.equal(all.length,17);assert.equal(new Set(all).size,17);
  };
  for(s.turn=1;s.turn<=8;s.turn++) {
    E.playerStart(s);check();let action;
    while((action=E.decide(s,E.config.policies.balanced))) {E.play(s,action);check();}
    E.enemyTurn(s);check();
  }
});
test('可以当场击杀时，不因格挡和已布置伤害而拒绝结束战斗',()=>{
  const s=hand([{name:'箭',kind:'arrow',turns:1,damage:20},{name:'立即击杀',kind:'attack',cost:1,damage:20}],target(10,0));
  s.enemies[0].block=5;E.play(s,{id:0,target:0});
  assert.deepEqual(E.decide(s,E.config.policies.balanced),{id:1,target:0});
});
