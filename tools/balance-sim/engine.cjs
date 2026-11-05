const fs = require('node:fs');
const path = require('node:path');
const config = require('./scenarios.json');

// Counter-based randomness keeps enemy HP and initial card order paired across variants.
function hash(seed, a, b = 0) {
  let x = (seed ^ Math.imul(a + 1, 0x9e3779b1) ^ Math.imul(b + 1, 0x85ebca6b)) >>> 0;
  x = Math.imul(x ^ (x >>> 16), 0x7feb352d); x = Math.imul(x ^ (x >>> 15), 0x846ca68b);
  return (x ^ (x >>> 16)) >>> 0;
}
const read = file => JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
const baseline = (cost, up) => cost === 0 ? (up ? 9 : 6) : cost * (up ? 14 : 10);
const compactCard = card => ({
  name: card.name, kind: card.type === '星矢' ? 'arrow' : 'attack', cost: Number(card.cost),
  damage: 0, hits: 1, block: 0, turns: 0, draw: 0, advance: 0, retain: false, exhaust: false
});
function loadPool(file = path.resolve(__dirname, '../../docs/design/data/纯那Mod设计工作台_本地编辑.json')) {
  const bundle = read(file), byId = new Map(bundle.data.cards.map(card => [card.id, card]));
  const pick = (id, up = false) => {
    const source = byId.get(id);
    if (!source) throw Error('缺少模拟卡牌：' + id);
    const card = compactCard(source), text = up ? source.upgradedText : source.text;
    card.cost = Number(up ? source.upgradedCost : source.cost);
    card.turns = Number(up ? source.upgradedTurns : source.turns) || 0;
    card.retain = /保留[。\n]/.test(text);
    card.exhaust = /消耗[。\n]/.test(text);
    const damage = /造成(\d+)点伤害(?:(\d+)次)?/.exec(text);
    if (damage) { card.damage = Number(damage[1]); card.hits = Number(damage[2] || 1); }
    const block = /获得(\d+)点格挡/.exec(text);
    if (block) card.block = Number(block[1]);
    if (id === 'JB05') { card.kind = 'advance'; card.advance = 1; card.scope = /所有星矢/.test(text) ? 'target' : 'one'; }
    if (id === 'JA16') { card.kind = 'attackAdvance'; card.advance = 1; card.scope = /所有星矢/.test(text) ? 'target' : 'one'; }
    if (id === 'JA43') { card.kind = 'advance'; card.advance = 2; card.scope = 'all'; }
    if (id === 'JA04') { card.kind = 'instant'; card.credits = up ? 2 : 1; }
    return {...card, modelId: id};
  };
  const ids=new Set(['JB01','JB02','JB03','JB05','JA16','JA43','JA04']);
  return { revision: bundle.revision, pick, startingDeck: bundle.data.meta.startingDeck,
    snapshot:{revision:bundle.revision,data:{cards:bundle.data.cards.filter(card=>ids.has(card.id)),meta:{startingDeck:bundle.data.meta.startingDeck}}} };
}
function buildDeck({cost, wait = 0, bonus = 0, upgraded = false, shellUpgraded = false, context = 'normal', density = 2, immediate = false}, pool) {
  const up = shellUpgraded, cards = [];
  const push = (card, count = 1) => { for (let i = 0; i < count; i++) cards.push({...card}); };
  const attack = {name:'双重打击基准',kind:'attack',cost:1,damage:up?7:5,hits:2};
  const leap = {name:'飞跃',kind:'block',cost:1,block:up?12:9};
  // Fixed 17-card shell; higher probe density replaces Strikes, keeping deck size fixed.
  push(pool.pick('JB01', up), 6 - density);
  push({...pool.pick('JB02', up), kind:'block'}, 4);
  push(pool.pick('JB03', up));
  push(context === 'none' ? {name:'普通格挡对照',kind:'block',cost:1,block:up?10:7} : pool.pick('JB05', up));
  push(leap); push(attack);
  push({name:'后空翻',kind:'block',cost:1,block:up?8:5,draw:2});
  if (context === 'none' || context === 'starter') { push(attack); push(leap); }
  else if (context === 'normal') { push(pool.pick('JA16', up)); push(leap); }
  else if (context === 'batch') { push(pool.pick('JA16', up)); push(pool.pick('JA43', up)); }
  else if (context === 'instant') { push(pool.pick('JA16', up)); push(pool.pick('JA04', up)); }
  else throw Error('未知加速环境：' + context);
  push({name:'受测牌',kind:immediate?'attack':'arrow',cost,damage:baseline(cost,upgraded)+(immediate?0:bonus*wait),hits:1,turns:immediate?0:wait,probe:true}, density);
  return cards.map((card, id) => ({damage:0,hits:1,block:0,turns:0,draw:0,advance:0,retain:false,exhaust:false,...card,id}));
}
function copy(s) {
  return {...s, hand:s.hand.slice(), draw:s.draw.slice(), discard:s.discard.slice(), exhausted:s.exhausted.slice(),
    arrows:s.arrows.map(a=>({...a})), enemies:s.enemies.map(e=>({...e})), stats:{...s.stats}, trace:null};
}
const alive = s => s.enemies.some(enemy => enemy.hp > 0);
function note(s, message) { if (s.trace) s.trace.push({turn:s.turn, message, hp:s.hp, energy:s.energy}); }
function shuffled(ids, seed, epoch) { return ids.slice().sort((a,b)=>hash(seed,epoch,a)-hash(seed,epoch,b)||a-b); }
function drawCards(s, count, speculative = false) {
  if (speculative) { s.ghostDraw += Math.min(count, Math.max(0,config.player.handLimit-s.hand.length)); return; }
  for (let i=0;i<count;i++) {
    if (!s.draw.length) {
      if (!s.discard.length) break;
      s.draw=shuffled(s.discard,s.seed,++s.shuffleEpoch); s.discard=[]; s.stats.shuffles++;
    }
    const id=s.draw.shift(); s.stats.cardsDrawn++;
    if (s.hand.length<config.player.handLimit) s.hand.push(id); else s.discard.push(id);
  }
}
function damageEnemy(s,target,damage,hits=1,probe=false) {
  const enemy=s.enemies[target];
  for(let i=0;i<hits;i++) {
    if (!enemy || enemy.hp<=0) { if(probe)s.stats.probeWaste+=damage; continue; }
    const absorbed=Math.min(enemy.block,damage); enemy.block-=absorbed;
    const amount=Math.min(enemy.hp,damage-absorbed);
    enemy.hp-=amount; s.stats.damageDealt+=amount;
    if(probe) { s.stats.probeDamage+=amount; s.stats.probeWaste+=damage-absorbed-amount; }
  }
}
function complete(s,arrow) {
  s.arrows=s.arrows.filter(a=>a.id!==arrow.id);
  const card=s.cards[arrow.id];
  damageEnemy(s,arrow.target,card.damage,card.hits,card.probe);
  s.stats.completions++;
  if(card.probe)s.stats.probeCompletions++;
  (card.exhaust?s.exhausted:s.discard).push(arrow.id);
  note(s,card.name+'完成，目标'+(arrow.target+1)+'，'+card.damage+'×'+card.hits);
}
function advance(s,ids,amount) {
  for(const id of ids) {
    const arrow=s.arrows.find(a=>a.id===id); if(!arrow)continue;
    const actual=Math.min(arrow.remaining,amount); arrow.remaining-=actual;
    s.stats.acceleration+=actual;
    if(s.cards[id].probe)s.stats.probeAcceleration+=actual;
    if(arrow.remaining<=0)complete(s,arrow);
  }
}
function moveFor(enemy,turn) {
  const opener=enemy.spec.opener||[];
  return turn<=opener.length?opener[turn-1]:enemy.spec.moves[(turn-opener.length-1)%enemy.spec.moves.length];
}
function incoming(s) {
  return s.enemies.reduce((sum,e)=>e.hp<=0?sum:sum+((moveFor(e,s.turn).damage||0)+((moveFor(e,s.turn).damage||0)>0?e.strength:0))*(moveFor(e,s.turn).hits||1),0);
}
function legalActions(s) {
  const actions=[],seen=new Set();
  for(const id of s.hand) {
    const c=s.cards[id]; if(c.cost>s.energy)continue;
    const signature=[c.kind,c.cost,c.damage,c.hits,c.block,c.turns,c.draw,c.advance,c.scope,c.retain,c.exhaust,c.probe].join('/');
    if(seen.has(signature))continue; seen.add(signature);
    const targets=s.enemies.map((e,i)=>e.hp>0?i:-1).filter(i=>i>=0);
    if(c.kind==='attack'||c.kind==='arrow')for(const target of targets)actions.push({id,target});
    else if(c.kind==='attackAdvance')for(const target of targets) {
      const arrows=s.arrows.filter(a=>a.target===target);
      if(c.scope==='target'||!arrows.length)actions.push({id,target,arrow:-1});
      else for(const arrow of arrows)actions.push({id,target,arrow:arrow.id});
    }
    else if(c.kind==='advance') {
      if(c.scope==='all') { if(s.arrows.length)actions.push({id}); }
      else if(c.scope==='target')for(const target of new Set(s.arrows.map(a=>a.target)))actions.push({id,target});
      else for(const arrow of s.arrows)actions.push({id,arrow:arrow.id});
    }
    else if(c.kind==='instant') { if(s.hand.some(other=>s.cards[other].kind==='arrow')||s.energy>c.cost)actions.push({id}); }
    else actions.push({id});
  }
  return actions;
}
function play(s,action,speculative=false) {
  const c=s.cards[action.id],at=s.hand.indexOf(c.id);
  if(at<0||c.cost>s.energy)throw Error('非法出牌');
  s.hand.splice(at,1); s.energy-=c.cost; s.stats.energySpent+=c.cost; s.stats.cardsPlayed++;
  if(c.probe)s.stats.probePlays++;
  note(s,'打出'+c.name+(action.target===undefined?'':'，目标'+(action.target+1)));
  if(c.kind==='arrow') {
    let remaining=c.turns;
    if(s.instant>0) { s.instant--; remaining=0; }
    const arrow={id:c.id,target:action.target,remaining,placed:s.turn};
    s.arrows.push(arrow);
    if(remaining<=0)complete(s,arrow);
    return;
  }
  if(c.damage)damageEnemy(s,action.target,c.damage,c.hits,c.probe);
  if(c.block)s.block+=c.block;
  if(c.kind==='advance'||c.kind==='attackAdvance') {
    const ids=c.scope==='all'?s.arrows.map(a=>a.id):c.scope==='target'?s.arrows.filter(a=>a.target===action.target).map(a=>a.id):[action.arrow];
    advance(s,ids,c.advance);
  }
  if(c.kind==='instant')s.instant+=c.credits;
  if(c.draw)drawCards(s,c.draw,speculative);
  (c.exhaust?s.exhausted:s.discard).push(c.id);
}
function playerStart(s) {
  s.energy=s.maxEnergy; s.block=0; s.instant=0; s.ghostDraw=0;
  for(const arrow of s.arrows.slice()) {
    if(!s.arrows.some(a=>a.id===arrow.id))continue;
    arrow.remaining--; if(arrow.remaining<=0)complete(s,arrow);
    if(!alive(s))break;
  }
  if(alive(s))drawCards(s,s.drawPerTurn);
}
function enemyTurn(s) {
  for(const id of s.hand)if(!s.cards[id].retain)s.discard.push(id);
  s.hand=s.hand.filter(id=>s.cards[id].retain);
  for(const enemy of s.enemies) {
    if(enemy.hp<=0)continue;
    enemy.block=0;
    const move=moveFor(enemy,s.turn);
    if(move.damage)for(let hit=0;hit<(move.hits||1);hit++) {
      const amount=move.damage+enemy.strength;
      const blocked=Math.min(s.block,amount); s.block-=blocked;
      const loss=amount-blocked; s.hp-=loss; s.stats.damageTaken+=loss;
      if(s.hp<=0)break;
    }
    if(move.strength)enemy.strength+=move.strength;
    if(move.block)enemy.block+=move.block;
    if(enemy.ritual)enemy.strength+=enemy.ritual;
    if(move.ritual)enemy.ritual=move.ritual;
    if(s.hp<=0)break;
  }
  note(s,'敌方行动后，累计承伤'+s.stats.damageTaken);
}
function score(s,policy) {
  let queuedValue=0,remaining=0;
  for(let i=0;i<s.enemies.length;i++) {
    const enemy=s.enemies[i]; if(enemy.hp<=0)continue;
    remaining+=enemy.hp;
    // Pending damage is capped by remaining HP; enemy block is never counted as HP progress.
    let left=enemy.hp;
    const arrows=s.arrows.filter(a=>a.target===i).sort((a,b)=>a.remaining-b.remaining||a.id-b.id);
    for(const arrow of arrows) {
      const c=s.cards[arrow.id],amount=Math.min(left,c.damage*c.hits);
      queuedValue+=amount*Math.pow(policy.discount,arrow.remaining); left-=amount;
      if(left<=0)break;
    }
  }
  const threat=Math.max(0,incoming(s)-s.block);
  return -remaining+queuedValue-policy.defenseWeight*threat-(threat>=s.hp?1000:0)+s.ghostDraw*(s.energy>0?1.7:0.35)+s.energy*0.015;
}
function decide(s,policy) {
  const current=score(s,policy),first=[];
  for(const action of legalActions(s)) {
    const state=copy(s); play(state,action,true);
    first.push({action,state,value:score(state,policy)});
  }
  first.sort((a,b)=>b.value-a.value||a.action.id-b.action.id);
  const beam=first.slice(0,policy.beam);
  // Keep setup actions in the two-card lookahead even when their immediate value is zero.
  for(const item of first)if(s.cards[item.action.id].kind==='instant'&&!beam.includes(item))beam.push(item);
  let best=current,bestAction=null,bestFirst=-Infinity;
  for(const item of beam) {
    let value=item.value;
    if(alive(item.state))for(const nextAction of legalActions(item.state)) {
      const next=copy(item.state); play(next,nextAction,true);
      value=Math.max(value,score(next,policy));
    }
    if(value>best+1e-8||(value>current+1e-8&&Math.abs(value-best)<1e-8&&item.value>bestFirst)) {
      best=value; bestAction=item.action; bestFirst=item.value;
    }
  }
  return bestAction;
}
function initialState(cards,encounter,seed,options={}) {
  return {cards,seed,turn:1,hp:options.hp||config.player.hp,maxHp:options.hp||config.player.hp,
    maxEnergy:options.energy||config.player.energy,drawPerTurn:options.draw||config.player.draw,
    energy:0,block:0,instant:0,ghostDraw:0,shuffleEpoch:0,
    draw:shuffled(cards.map(c=>c.id),seed,0),hand:[],discard:[],exhausted:[],arrows:[],
    enemies:encounter.enemies.map((spec,i)=>({spec,hp:spec.hp[0]+hash(seed,9000,i)%(spec.hp[1]-spec.hp[0]+1),block:0,strength:0,ritual:0})),
    stats:{damageTaken:0,damageDealt:0,cardsDrawn:0,cardsPlayed:0,energySpent:0,shuffles:0,completions:0,acceleration:0,probeDamage:0,probeWaste:0,probePlays:0,probeCompletions:0,probeAcceleration:0},
    trace:options.trace?[]:null};
}
function fight(cards,encounter,seed,policy=config.policies.balanced,options={}) {
  const s=initialState(cards,encounter,seed,options);
  const maxTurns=options.maxTurns||config.player.maxTurns;
  for(s.turn=1;s.turn<=maxTurns;s.turn++) {
    playerStart(s);
    if(!alive(s))break;
    let actions=0;
    while(alive(s)) {
      const action=decide(s,policy); if(!action)break;
      play(s,action,false);
      if(++actions>50)throw Error('回合出牌未收敛');
    }
    if(!alive(s))break;
    enemyTurn(s); if(s.hp<=0)break;
  }
  const win=!alive(s),enemyHp=s.enemies.reduce((sum,e)=>sum+Math.max(0,e.hp),0);
  return {win,turns:Math.min(s.turn,maxTurns),loss:s.stats.damageTaken,
    penalty:win?s.stats.damageTaken:config.player.hp*2+enemyHp,
    enemyHp,remainingHp:Math.max(0,s.hp),...s.stats,
    ...(s.trace?{trace:s.trace,deck:cards,enemies:encounter.enemies}:{} )};
}
module.exports={config,hash,baseline,loadPool,buildDeck,initialState,playerStart,enemyTurn,legalActions,play,advance,complete,drawCards,incoming,decide,fight};
