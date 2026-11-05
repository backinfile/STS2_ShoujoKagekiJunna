function createJunnaRouteStatisticsView(deps) {
  const {data,ui,defaults,dom,escape,table,cardScope,flush,render,transact}=deps;
  const formatCardText=deps.formatCardText||escape;
  const logic=JunnaRouteStatistics;
  const scopes={cards:'卡池',reserve:'备用',trash:'垃圾桶'};
  const stats=(scope=ui().flowScope)=>logic.compute(data(),defaults,{scope,onlyRewards:ui().flowOnlyRewards,position:ui().routePositions,excludedPositions:ui().routeExcludedPositions});
  const selectedRoutes=value=>ui().flowRoute==='all'?value.routes:value.routes.filter(route=>route.uid===ui().flowRoute);
  const selectedCards=value=>ui().flowRoute==='all'?value.cards:selectedRoutes(value).flatMap(route=>route.members);
  const focus=value=>value.routes.find(route=>route.uid===ui().flowDrill?.route);
  function label(key) {
    if(key.startsWith('effect:')){
      const route=data().archetypes.find(route=>route.uid===ui().flowDrill?.route);
      return Object.values(route?.effectRoles||{}).flatMap(rule=>rule.effects||[]).find(effect=>typeof effect==='object'&&effect.id===key.slice(7))?.text||'具体效果';
    }
    if(key==='all')return '全部牌面';
    if(key==='rewards')return '奖励入口';
    if(key==='cross')return '跨流派';
    if(key==='unassignedPorts')return '待分配';
    if(key.startsWith('roleRarity:')){const [,role,rarity]=key.split(':');return (logic.positions[role]||logic.roles[role])+' · '+rarity;}
    if(key.startsWith('rarity:'))return key.slice(7);
    if(key.startsWith('port:'))return logic.ports[key.slice(5)];
    if(key.startsWith('cost:'))return {zero:'0费',one:'1费',two:'2费',threePlus:'3费及以上',x:'X费'}[key.slice(5)];
    return (logic.positions[key]||logic.roles[key])||key;
  }
  function number(route,key,list) {
    if(!list.length)return '<td>0</td>';
    const active=ui().flowDrill?.route===route.uid&&ui().flowDrill?.key===key;
    return '<td><button type="button" class="flow-number'+(active?' is-active':'')+'" data-flow-route="'+escape(route.uid)+'" data-flow-key="'+escape(key)+'" aria-label="'+escape(route.name+' · '+label(key)+' · '+list.length+'张')+'" aria-pressed="'+active+'">'+list.length+'</button></td>';
  }
  const nameCell=route=>'<th scope="row"><button type="button" class="flow-route-name" data-flow-route="'+escape(route.uid)+'" data-flow-key="all">'+escape(route.name)+'</button></th>';
  function positionFilters() {
    return [['include','流派定位',ui().routePositions],['exclude','排除定位',ui().routeExcludedPositions]].map(([mode,title,selected])=>
      '<fieldset class="field filter-group position-filter" data-position-group="'+mode+'"><legend class="field-label">'+title+'</legend><div class="position-options">'+Object.entries(logic.positions).map(([tag,name])=>
        '<label class="position-tag"><input type="checkbox" data-position-mode="'+mode+'" data-position-tag="'+tag+'"'+(logic.normalizePositions(selected).includes(tag)?' checked':'')+'><span>'+name+'</span></label>').join('')+'</div></fieldset>').join('');
  }
  function positionChange(event) {
    const input=event.target;if(!input.matches('[data-position-tag]'))return false;
    flush();
    const next=logic.togglePositionFilter(ui().routePositions,ui().routeExcludedPositions,input.dataset.positionMode,input.dataset.positionTag,input.checked);
    ui().routePositions=next.included;ui().routeExcludedPositions=next.excluded;ui().flowDrill=null;ui().selected=null;
    const sidebar=dom('sidebar'),scroll=sidebar.scrollTop,selector='[data-position-mode="'+input.dataset.positionMode+'"][data-position-tag="'+input.dataset.positionTag+'"]';
    render();sidebar.scrollTop=scroll;sidebar.querySelector(selector)?.focus({preventScroll:true});return true;
  }
  function tagHTML(card,route) {
    return '<span class="position-badges">'+logic.positionTags(card,[route],defaults).map(tag=>'<span class="badge">'+logic.positions[tag]+'</span>').join('')+'</span>';
  }
  function sidebar() {
    const value=stats();
    if(ui().flowRoute!=='all'&&!value.routes.some(route=>route.uid===ui().flowRoute))ui().flowRoute='all';
    const routeButton=route=>'<button type="button" class="side-item '+(ui().flowRoute===route.uid?'active':'')+'" data-flow-select="'+escape(route.uid)+'" aria-pressed="'+(ui().flowRoute===route.uid)+'"><span>'+escape(route.name)+'</span><span>'+route.members.length+'</span></button>';
    const main=value.routes.filter(route=>route.level!=='小体系'),minor=value.routes.filter(route=>route.level==='小体系');
    return '<div class="sidebar-section"><p class="sidebar-title">统计范围</p>'+Object.entries(scopes).map(([scope,name])=>'<button type="button" class="side-item '+(ui().flowScope===scope?'active':'')+'" data-flow-scope="'+scope+'" aria-pressed="'+(ui().flowScope===scope)+'"><span>'+name+'</span><span>'+(scope===ui().flowScope?value.cards.length:stats(scope).cards.length)+'</span></button>').join('')+'<label class="check overview-check"><input type="checkbox" data-flow-rewards'+(ui().flowOnlyRewards?' checked':'')+'>仅奖励入口</label>'+positionFilters()+'</div><div class="sidebar-section"><p class="sidebar-title">流派</p><button type="button" class="side-item '+(ui().flowRoute==='all'?'active':'')+'" data-flow-select="all" aria-pressed="'+(ui().flowRoute==='all')+'"><span>全部</span><span>'+value.routes.length+'</span></button>'+main.map(routeButton).join('')+'</div>'+(minor.length?'<div class="sidebar-section"><p class="sidebar-title">小体系</p>'+minor.map(routeButton).join('')+'</div>':'');
  }
  function cardList(value) {
    const route=focus(value);if(!route)return '';
    const key=ui().flowDrill.key,list=logic.membersFor(route,key).slice().sort((a,b)=>a.id.localeCompare(b.id,undefined,{numeric:true}));
    const rows=list.map(card=>'<tr'+(ui().selected?.uid===card.uid?' class="flow-selected-card"':'')+'><th scope="row">'+escape(card.id)+'</th><td class="flow-card-name"><button type="button" class="text-button" data-edit="cards" data-uid="'+escape(card.uid)+'">'+escape(card.name)+'</button></td><td>'+escape(card.rarity)+'</td><td>'+escape(card.type)+'</td><td>'+escape(card.cost)+'</td><td>'+tagHTML(card,route)+'</td></tr>');
    if(!rows.length)rows.push('<tr><td colspan="6" class="overview-empty">暂无卡牌</td></tr>');
    return '<div class="flow-card-list">'+table(route.name+' · '+label(key)+' · '+list.length+'张',['编号','牌名','稀有度','类型','费用','定位'],rows)+'</div>';
  }
  function recommendations() {
    if(ui().flowScope!=='cards')return '';
    const value=logic.compute(data(),defaults,{scope:ui().flowScope,onlyRewards:ui().flowOnlyRewards,position:'all'});
    const plans=selectedRoutes(value).map(route=>({route,plan:logic.recommend(route,defaults)})).filter(entry=>entry.plan);if(!plans.length)return '';
    const difference=(current,target)=>current===target?'一致':(current>target?'多':'少')+Math.abs(current-target)+'张',pending=plans.some(entry=>entry.plan.pending);
    const comparison=plans.map(({route,plan:p})=>'<tr>'+nameCell(route)+'<td>'+p.currentSides.trigger+'张</td><td>'+p.targetSides.trigger+'张</td><td>'+difference(p.currentSides.trigger,p.targetSides.trigger)+'</td><td>'+p.currentSides.payoff+'张</td><td>'+p.targetSides.payoff+'张</td><td>'+difference(p.currentSides.payoff,p.targetSides.payoff)+'</td>'+(pending?'<td>'+p.pending+'</td>':'')+'</tr>');
    const ratios=plans.map(({route,plan:p})=>'<tr>'+nameCell(route)+'<td>'+logic.roleRatio(p.currentSides.trigger,p.currentSides.payoff)+'</td><td>'+logic.roleRatio(p.targetSides.trigger,p.targetSides.payoff)+'</td></tr>');
    const keys=['trigger','payoff'],percent=new Intl.NumberFormat('zh-CN',{style:'percent',maximumFractionDigits:1});
    const allocation=plans.map(({route,plan:p})=>'<tr>'+nameCell(route)+'<td>'+p.size+'</td>'+keys.map(key=>'<td>'+p.targetSides[key]+(p.targetSides[key]?'（'+percent.format(p.size?p.targetSides[key]/p.size:0)+'）':'')+'</td>').join('')+'</tr>');
    return table('数量建议 · 整个流派',['流派','当前触发牌','建议触发牌','触发牌差额','当前收益牌','建议收益牌','收益牌差额',...(pending?['待分类']:[])],comparison)+'<p class="flow-counting-note">同一张牌可同时计入触发牌和收益牌。3∶1表示3张触发牌对应1张收益牌。</p>'+table('触发牌∶收益牌',['流派','当前比例','建议比例'],ratios)+table('推荐定位 · 数量（占比）',['流派','牌面总数',...keys.map(key=>(logic.positions[key]||logic.roles[key]))],allocation);
  }
  function effectClassification() {
    const value=stats(),routes=selectedRoutes(value);
    const rows=routes.flatMap(route=>['trigger','payoff','both','tool'].flatMap(role=>{
      const rule=route.effectRoles?.[role];if(!rule?.effects?.length)return [];
      const candidates=route.members.filter(card=>logic.profile(card,route,defaults).role===role),example=(rule.examples||[]).map(id=>candidates.find(card=>card.id===id)).find(Boolean);
      const exampleHTML=example?'<button type="button" class="text-button" data-edit="cards" data-uid="'+escape(example.uid)+'">'+escape(example.name)+'</button><span class="flow-effect-example">'+formatCardText(example.text,example)+'</span>':'—';
      const effects=route.effects.filter(effect=>effect.role===role).map(effect=>{
        const key='effect:'+effect.id,active=ui().flowDrill?.route===route.uid&&ui().flowDrill?.key===key;
        const count=effect.known?(effect.members.length?'<button type="button" class="flow-number flow-effect-count'+(active?' is-active':'')+'" data-flow-route="'+escape(route.uid)+'" data-flow-key="'+escape(key)+'" aria-label="'+escape(route.name+' · '+effect.text+' · '+effect.members.length+'张')+'" aria-pressed="'+active+'">'+effect.members.length+'张</button>':'<span class="flow-effect-count">0张</span>'):'<span class="flow-effect-count">—</span>';
        return '<li><span>'+escape(effect.text)+'</span>'+count+'</li>';
      }).join('');
      return ['<tr><th scope="row">'+escape(route.name)+'</th><td>'+(role==='both'?'触发、收益':role==='tool'?'收益':escape(logic.roles[role]))+'</td><td class="flow-effect-content"><ul class="flow-effect-list">'+effects+'</ul></td><td class="flow-effect-content">'+exampleHTML+'</td></tr>'];
    }));
    return rows.length?'<details class="flow-effect-rules"'+(ui().flowEffectOpen?' open':'')+'><summary>效果归类</summary>'+table('各流派效果归类',['流派','归类','具体效果与数量','卡池示例'],rows)+'</details>':'';
  }
  function workspace() {
    const effectOpen=dom('work-area').querySelector?.('.flow-effect-rules')?.open;if(typeof effectOpen==='boolean')ui().flowEffectOpen=effectOpen;
    const value=stats(),routes=selectedRoutes(value),pending=routes.some(route=>route.buckets.unclassified.length);
    const majorCount=routes.filter(route=>route.level!=='小体系').length,minorCount=routes.length-majorCount;
    dom('page-title').textContent='流派统计';dom('result-count').textContent=[...(majorCount?[majorCount+' 个流派']:[]),...(minorCount?[minorCount+' 个小体系']:[]),selectedCards(value).length+' 张牌面'].join(' · ');dom('work-area').setAttribute('aria-labelledby','tab-flows');dom('page-actions').innerHTML='';
    const keys=Object.keys(logic.positions);
    const composition=routes.map(route=>'<tr>'+nameCell(route)+number(route,'all',route.members)+number(route,'rewards',route.rewards)+keys.map(key=>number(route,key,logic.membersFor(route,key))).join('')+'</tr>');
    const rarity=routes.map(route=>'<tr>'+nameCell(route)+logic.rarities.map(r=>number(route,'rarity:'+r,route.rarity[r])).join('')+'</tr>');
    const portKeys=Object.keys(logic.ports),missingPorts=routes.some(route=>route.unassignedPorts.length);
    const ports=routes.map(route=>'<tr>'+nameCell(route)+portKeys.map(port=>number(route,'port:'+port,route.payoffPorts[port])).join('')+(missingPorts?number(route,'unassignedPorts',route.unassignedPorts):'')+'</tr>');
    const costKeys=['zero','one','two','threePlus','x'];
    const cost=routes.map(route=>'<tr>'+nameCell(route)+costKeys.map(key=>number(route,'cost:'+key,route.cost[key])).join('')+'</tr>');
    const roleRarity=ui().flowRoute!=='all'&&routes.length===1?table('定位与稀有度',['定位',...logic.rarities],keys.map(key=>'<tr><th scope="row">'+(logic.positions[key]||logic.roles[key])+'</th>'+logic.rarities.map(rarity=>number(routes[0],'roleRarity:'+key+':'+rarity,logic.membersFor(routes[0],'roleRarity:'+key+':'+rarity))).join('')+'</tr>')):'';
    let standard='';
    if(value.scope==='cards'&&ui().flowRoute==='all'&&!ui().routePositions?.length&&!ui().routeExcludedPositions?.length) {
      const difference=n=>n>0?'+'+n:String(n);
      const standardRows=value.standard.slice().sort((a,b)=>logic.rarities.indexOf(a.rarity)-logic.rarities.indexOf(b.rarity)).map(row=>'<tr><th scope="row">'+escape(row.rarity)+'</th><td>'+row.current+'</td><td>'+row.count+'</td><td>'+difference(row.current-row.count)+'</td></tr>');
      standardRows.push('<tr class="overview-total"><th scope="row">合计</th><td>'+value.summary.comparable+'</td><td>'+value.summary.standard+'</td><td>'+difference(value.summary.comparable-value.summary.standard)+'</td></tr>');
      standard=table('当前卡池与标准',['稀有度','当前','标准','差额'],standardRows);
    }
    const effectDrill=ui().flowDrill?.key.startsWith('effect:');
    dom('work-area').innerHTML='<div class="flow-statistics">'+effectClassification()+(effectDrill?cardList(value):'')+recommendations()+'<div class="flow-composition">'+table('定位分布',['流派','牌面','奖励入口',...keys.map(key=>logic.positions[key])],composition)+'</div>'+(effectDrill?'':cardList(value))+'<div class="flow-distributions">'+roleRarity+table('稀有度分布',['流派',...logic.rarities],rarity)+table('收益端口',['流派',...portKeys.map(key=>logic.ports[key]),...(missingPorts?['待分配']:[])],ports)+table('费用分布',['流派','0','1','2','3+','X'],cost)+standard+'</div></div>';
  }
  function inspector() {
    const value=stats(),routes=selectedRoutes(value),cards=selectedCards(value),unclassified=new Set(routes.flatMap(route=>route.buckets.unclassified.map(card=>card.uid))).size,related=new Set(data().partnerGroups.flatMap(group=>group.grantedCardIds));
    const facts=[['当前牌面',cards.length],['流派',routes.filter(route=>route.level!=='小体系').length],['小体系',routes.filter(route=>route.level==='小体系').length],['待分类',unclassified],['常规奖励',cards.filter(logic.isReward).length],['知识附带牌面',cards.filter(card=>related.has(card.id)).length],...(value.scope==='cards'&&ui().flowRoute==='all'&&!ui().routePositions?.length&&!ui().routeExcludedPositions?.length?[['标准比较牌面',value.summary.comparable]]:[])];
    dom('inspector').hidden=false;dom('inspector-title').textContent='统计';dom('close-inspector').hidden=true;dom('preview').innerHTML='';dom('preview').removeAttribute('tabindex');dom('preview').removeAttribute('aria-label');dom('inspector-footer').hidden=true;
    dom('editor-form').innerHTML='<section class="overview-section"><h2>'+scopes[value.scope]+'</h2><dl class="overview-facts">'+facts.map(([name,count])=>'<div><dt>'+name+'</dt><dd>'+count+'</dd></div>').join('')+'</dl></section>';
  }
  function editorRoute(card){
    const routes=data().archetypes.filter(route=>(card.archetypeIds||[]).includes(route.id));
    return routes.find(route=>ui().cardPositionCard===card.uid&&route.uid===ui().cardPositionRoute)||routes.find(route=>ui().tab==='flows'&&route.uid===(ui().flowDrill?.route||ui().flowRoute))||routes.find(route=>route.system===card.system)||routes[0]||null;
  }
  function fields(card) {
    const route=editorRoute(card),routes=data().archetypes.filter(entry=>(card.archetypeIds||[]).includes(entry.id)),tags=logic.positionTags(card,route?[route]:[],defaults);
    const fixedTrigger=route?.id==='SYS_stage'&&(card.type==='箭矢'||JunnaEffectStatistics.handArrowPlay(card));
    const routeButtons=routes.length>1?'<div class="flow-editor-routes" role="group" aria-label="定位所属流派">'+routes.map(entry=>'<button type="button" class="filter-option" data-card-position-route="'+entry.uid+'" aria-pressed="'+(entry.uid===route.uid)+'">'+escape(entry.name)+'</button>').join('')+'</div>':'';
    return '<fieldset class="flow-classification"><legend>流派定位</legend>'+routeButtons+'<div class="position-options">'+Object.entries(logic.positions).map(([tag,name])=>'<label class="position-tag"><input type="checkbox" data-flow-tag="'+tag+'"'+(tags.includes(tag)?' checked':'')+((tag==='cross'||tag==='trigger'&&fixedTrigger||!route&&tag!=='breakthrough')?' disabled':'')+'><span>'+name+'</span></label>').join('')+'</div></fieldset>';
  }
  function editorClick(event){
    const button=event.target.closest('[data-card-position-route]');if(!button)return false;
    const card=data().cards.find(entry=>entry.uid===ui().selected?.uid);if(!card)return true;
    const route=data().archetypes.find(entry=>entry.uid===button.dataset.cardPositionRoute&&(card.archetypeIds||[]).includes(entry.id));if(!route)return true;
    flush();ui().cardPositionCard=card.uid;ui().cardPositionRoute=route.uid;render();return true;
  }
  function sidebarClick(event) {
    const scope=event.target.closest('[data-flow-scope]'),route=event.target.closest('[data-flow-select]');if(!scope&&!route)return false;
    flush();ui().selected=null;ui().flowDrill=null;
    if(scope)ui().flowScope=scope.dataset.flowScope;
    if(route)ui().flowRoute=route.dataset.flowSelect;
    render();return true;
  }
  function sidebarChange(event) {
    const input=event.target;if(!input.matches('[data-flow-rewards]'))return false;
    flush();ui().flowOnlyRewards=input.checked;ui().selected=null;render();return true;
  }
  function workClick(event) {
    const button=event.target.closest('[data-flow-key]');if(!button)return false;
    flush();ui().flowDrill={route:button.dataset.flowRoute,key:button.dataset.flowKey};ui().selected=null;render();if(ui().flowDrill.key.startsWith('effect:'))dom('work-area').querySelector('.flow-card-list')?.scrollIntoView({block:'nearest'});return true;
  }
  function editorChange(event) {
    const input=event.target;if(!input.matches('[data-flow-tag]'))return false;
    const card=data().cards.find(entry=>entry.uid===ui().selected?.uid);if(!card||card.trashedAt||input.disabled)return true;
    const route=editorRoute(card),tag=input.dataset.flowTag;if(tag==='cross'||!route&&tag!=='breakthrough')return true;
    const next=route?logic.profile(card,route,defaults):null;
    if(tag==='trigger'||tag==='payoff'){
      const tags=new Set(logic.positionTags(card,[route],defaults));input.checked?tags.add(tag):tags.delete(tag);
      next.role=tags.has('trigger')?(tags.has('payoff')?'both':'trigger'):(tags.has('payoff')?'payoff':'unclassified');
    }
    transact('流派定位 · '+card.name,d=>{
      const entry=d.cards.find(item=>item.uid===card.uid);
      entry.design={...entry.design,...(tag==='breakthrough'?{breakthrough:input.checked}:{}),...(route?{routeRoles:{...entry.design?.routeRoles,[route.uid]:next}}:{})};
    });return true;
  }
  return {positionFilters,positionChange,sidebar,workspace,inspector,fields,sidebarClick,sidebarChange,workClick,editorClick,editorChange};
}
