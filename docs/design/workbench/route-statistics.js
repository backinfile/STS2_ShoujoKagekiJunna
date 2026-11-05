/* THESIS: Extend the existing workbench with live route composition tables.
 * OWN-WORLD: Reuse the dark surfaces, cyan links, restrained tables and fixed inspector.
 * STORY: Compare routes, inspect a count, then classify or edit its actual cards.
 * FIRST VIEWPORT: Scope and routes at left; composition and distributions centrally;
 * current pool totals at right. FORM: Local extension of the incumbent overview. */
const JunnaRouteStatistics = (() => {
  'use strict';
  const effectStatistics=typeof module!=='undefined'&&module.exports?require('./effect-statistics.js'):JunnaEffectStatistics;
  const roles = {load:'载体',trigger:'触发',payoff:'收益',both:'兼具',tool:'工具',unclassified:'待分类'};
  const ports = {output:'输出',defense:'防御',operation:'运转'};
  const rarities = ['基础','普通','罕见','稀有','远古'];
  const cardScope = card => card.trashedAt ? 'trash' : card.reservedAt ? 'reserve' : 'cards';
  const isReward = card => card.pool && ['普通','罕见','稀有'].includes(card.rarity);
  function inferProfile(card,route) {
    const text=String(card.text||'').replace(/^知识[。．]?/,''),knowledge=(card.keywords||[]).includes('知识')||/^知识[。．]/.test(card.text||'');
    let trigger=false,payoff=false;
    if(route.id==='SYS_stage') {
      // 打出箭矢才提供触发；加速、复制、转型和调度依赖已有对象。
      trigger=card.type==='箭矢';
      payoff=/箭矢[^。]*(?:完成时|完成后|额外触发时|额外触发后)|打出[^。]*箭矢牌(?:时|后)|(?:每当|每次)[^。]*箭矢[^。]*加速[^。]*(?:时|后)|箭矢数量|每有[^。]*箭矢|(?:在场|布置)期间|有布置中?的箭矢时/.test(text);
    } else if(route.id==='SYS_partner') {
      trigger=knowledge||/(?:抽取|复制|加入|放入|取回)[^。]*知识牌/.test(text);
      payoff=/(?:打出|抽到)[^。]*知识牌[^。]*(?:后|时)|每[^。]*知识牌|知识牌[^。]*(?:进入手牌|时|后)|本场战斗打出过|若[^。]*关联/.test(text);
    } else if(route.id==='SYS_growth') {
      trigger=/获得[^。]*伤害加成|选择[^。]*(?:额外造成|伤害次数)/.test(text);
      payoff=/伤害加成[^。]*(?:倍|每|生效)|每拥有[^。]*伤害加成|每有[^。]*伤害加成/.test(text);
    } else if(route.id==='SYS_energy') {
      const xCost=String(card.cost).toUpperCase()==='X';
      trigger=xCost||typeof card.cost==='number'&&card.cost>=2||/(?:获得|保留)[^。]*能量|耗能增加|费用增加/.test(text);
      payoff=xCost||/(?:每有|每拥有|每消耗|每支付|剩余)[^。]*能量|(?:等同|等量)[^。]*耗能|每[^。]*耗能不低于|(?:第\d+次|每次)[^。]*耗能不低于/.test(text);
    }
    const role=trigger&&payoff?'both':trigger?'trigger':payoff?'payoff':'tool',portText=String(card.design?.port||'');
    const inferredPorts=[];
    if(/输出/.test(portText)||/造成.*伤害|伤害加成|伤害次数/.test(text))inferredPorts.push('output');
    if(/防御/.test(portText)||/格挡|虚弱|回血|生命值/.test(text))inferredPorts.push('defense');
    if(/运转/.test(portText)||/抽.*牌|能量|耗能|放入|加入|复制|保留|重放/.test(text))inferredPorts.push('operation');
    return {role,cross:(card.archetypeIds||[]).length>1,ports:inferredPorts};
  }
  function profile(card,route,defaults) {
    const base = defaults.routes.find(entry=>entry.uid===route.uid)?.members[card.uid];
    const override = card.design?.routeRoles?.[route.uid];
    const value = {...inferProfile(card,route),...base,...override};
    if(route.id==='SYS_stage') {
      const payoff=value.role==='payoff'||value.role==='both';
      if(card.type==='箭矢')value.role=payoff?'both':'trigger';
      else if(value.role==='trigger'||value.role==='both')value.role=payoff?'payoff':'tool';
    }
    return {role:Object.hasOwn(roles,value.role)?value.role:'unclassified',cross:Boolean(value.cross),ports:[...new Set((Array.isArray(value.ports)?value.ports:[]).filter(port=>Object.hasOwn(ports,port)))]};
  }
  function matchesPosition(card,routes,defaults,position='all',routeId='all') {
    if(position!=='cross'&&!Object.hasOwn(roles,position))return true;
    return routes.some(route=>card.archetypeIds.includes(route.id)&&(routeId==='all'||route.id===routeId)&&(position==='cross'?profile(card,route,defaults).cross:profile(card,route,defaults).role===position));
  }
  function compute(data,defaults,options={}) {
    const scope = ['cards','reserve','trash'].includes(options.scope)?options.scope:'cards';
    const scopedCards = data.cards.filter(card=>cardScope(card)===scope&&(!options.onlyRewards||isReward(card)));
    const order = new Map(defaults.routes.map((route,index)=>[route.uid,index]));
    const routes = data.archetypes.slice().sort((a,b)=>(order.get(a.uid)??999)-(order.get(b.uid)??999)||a.id.localeCompare(b.id,undefined,{numeric:true})).map(route=>{
      const members = scopedCards.filter(card=>card.archetypeIds.includes(route.id)&&matchesPosition(card,[route],defaults,options.position));
      const buckets = Object.fromEntries(Object.keys(roles).map(role=>[role,[]]));
      const cross = [], payoffPorts = Object.fromEntries(Object.keys(ports).map(port=>[port,[]])), unassignedPorts = [];
      for(const card of members) {
        const value = profile(card,route,defaults);
        buckets[value.role].push(card);
        if(value.cross)cross.push(card);
        if(['payoff','both'].includes(value.role)) {
          if(!value.ports.length)unassignedPorts.push(card);
          for(const port of value.ports)payoffPorts[port].push(card);
        }
      }
      const result={...route,members,buckets,cross,payoffPorts,unassignedPorts,rewards:members.filter(isReward),rarity:Object.fromEntries(rarities.map(rarity=>[rarity,members.filter(card=>card.rarity===rarity)])),cost:{zero:members.filter(card=>card.cost===0),one:members.filter(card=>card.cost===1),two:members.filter(card=>card.cost===2),threePlus:members.filter(card=>typeof card.cost==='number'&&card.cost>=3),x:members.filter(card=>card.cost==='X')}};
      result.effects=effectStatistics.entries(result,data,defaults,profile);
      return result;
    });
    const memberUids=new Set(routes.flatMap(route=>route.members.map(card=>card.uid)));
    const cards=options.position==='cross'||Object.hasOwn(roles,options.position)?scopedCards.filter(card=>memberUids.has(card.uid)):scopedCards;
    const active = data.cards.filter(card=>cardScope(card)==='cards');
    const comparable = active.filter(card=>card.pool||card.rarity==='基础');
    const standard = defaults.standard.map(row=>({...row,current:comparable.filter(card=>card.rarity===row.rarity).length}));
    return {scope,cards,routes,summary:{faces:active.length,rewards:active.filter(isReward).length,related:active.filter(card=>!card.pool&&card.rarity!=='基础').length,comparable:comparable.length,standard:standard.reduce((n,row)=>n+row.count,0)},standard};
  }
  function membersFor(route,key) {
    if(key.startsWith('effect:'))return route.effects?.find(effect=>effect.id===key.slice(7))?.members||[];
    if(key==='all')return route.members;
    if(key==='rewards')return route.rewards;
    if(key==='cross')return route.cross;
    if(key.startsWith('roleRarity:')){const [,role,rarity]=key.split(':');return (route.buckets[role]||[]).filter(card=>card.rarity===rarity);}
    if(key.startsWith('rarity:'))return route.rarity[key.slice(7)]||[];
    if(key.startsWith('port:'))return route.payoffPorts[key.slice(5)]||[];
    if(key.startsWith('cost:'))return route.cost[key.slice(5)]||[];
    if(key==='unassignedPorts')return route.unassignedPorts;
    return route.buckets[key]||[];
  }
  function recommend(route,defaults) {
    const plan=defaults.recommendations?.routes.find(entry=>entry.id===route.id);if(!plan)return null;
    const keys=['load','trigger','payoff','both','tool'],size=route.members.length;
    const weights=keys.map(role=>Number(plan.weights[role]||0)),total=weights.reduce((sum,value)=>sum+value,0);
    if(total<=0||weights.some(value=>!Number.isFinite(value)||value<0))return null;
    const quotas=weights.map(value=>size*value/total),target=Object.fromEntries(keys.map((role,index)=>[role,Math.floor(quotas[index])]));
    const order=keys.map((role,index)=>({role,index,remainder:quotas[index]-target[role]})).sort((a,b)=>b.remainder-a.remainder||a.index-b.index);
    const remaining=size-Object.values(target).reduce((sum,value)=>sum+value,0);for(let i=0;i<remaining;i++)target[order[i].role]++;
    const current=Object.fromEntries(keys.map(role=>[role,route.buckets[role].length]));
    const sides=counts=>({trigger:counts.trigger+counts.both,payoff:counts.payoff+counts.both});
    const currentSides=sides(current),targetSides=sides(target);
    return {size,current,target,currentSides,targetSides,pending:route.buckets.unclassified.length,
      share:Object.fromEntries(keys.map(role=>[role,size?target[role]/size:0])),
      difference:{trigger:targetSides.trigger-currentSides.trigger,payoff:targetSides.payoff-currentSides.payoff},
      nominalSides:sides(plan.weights),rationale:plan.rationale};
  }
  function roleRatio(trigger,payoff) {
    if(!trigger&&!payoff)return '—';let a=trigger,b=payoff;while(b){const rest=a%b;a=b;b=rest;}return trigger/a+'∶'+payoff/a;
  }
  return {roles,ports,rarities,cardScope,isReward,inferProfile,profile,matchesPosition,compute,membersFor,recommend,roleRatio};
})();
if(typeof module!=='undefined'&&module.exports)module.exports=JunnaRouteStatistics;
