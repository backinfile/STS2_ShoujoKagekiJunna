'use strict';
const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const logic=require('./route-statistics.js'),effects=require('./effect-statistics.js'),defaults=require('./route-statistics-defaults.json');
const stage={id:'SYS_stage',uid:'archetype_SYS_stage',name:'箭矢',level:'主流派',effectRoles:{}};
const partner={id:'SYS_partner',uid:'archetype_SYS_partner',name:'知识',level:'次主流派',effectRoles:{}};
const base={cost:1,rarity:'罕见',pool:true,type:'技能',text:'获得4点格挡。',keywords:[],termIds:[],partnerCardIds:[],archetypeIds:['SYS_stage']};
const a={...base,id:'a',uid:'a',name:'来源',type:'箭矢',text:'造成5点伤害。'};
const b={...base,id:'b',uid:'b',name:'收益',text:'所有目标为自己的箭矢加速1回合。'};
const c={...base,id:'c',uid:'c',name:'复合',type:'箭矢',role:'突破限制牌',archetypeIds:['SYS_stage','SYS_partner'],design:{routeRoles:{[stage.uid]:{role:'both',cross:true,ports:['operation']},[partner.uid]:{role:'trigger',cross:true,ports:['operation']}}}};
const data={cards:[a,b,c],archetypes:[stage,partner],partnerGroups:[]};
test('只保留四种标签，触发和收益按同时满足筛选，排除命中任一标签即隐藏',()=>{
 assert.deepEqual(Object.values(logic.positions),['触发','收益','跨流派','突破限制']);
 assert(logic.matchesPosition(c,[stage,partner],defaults,['trigger','payoff']));
 assert(!logic.matchesPosition(a,[stage,partner],defaults,['trigger','payoff']));
 assert(!logic.matchesPosition(c,[stage,partner],defaults,['trigger'],'all',['breakthrough','payoff']));
 assert(logic.matchesPosition(b,[stage,partner],defaults,['payoff'],'all',['cross','breakthrough']));
 assert.deepEqual(logic.normalizePositions('both,tool,load,unknown'),['trigger','payoff']);
});
test('突破限制识别牌面定位并支持勾选覆盖，包含和排除同一标签互斥',()=>{
 assert(logic.isBreakthrough(c));
 assert(!logic.isBreakthrough({...c,design:{...c.design,breakthrough:false}}));
 assert.deepEqual(logic.togglePositionFilter(['trigger'],['payoff'],'include','payoff',true),{included:['trigger','payoff'],excluded:[]});
 assert.deepEqual(logic.togglePositionFilter(['trigger','payoff'],[],'exclude','payoff',true),{included:['trigger'],excluded:['payoff']});
});
test('统计钻取使用重叠标签，反向筛选影响实际成员并保留无流派牌',()=>{
 const all=logic.compute(data,defaults),r=all.routes.find(r=>r.id===stage.id);
 assert.deepEqual(logic.membersFor(r,'trigger').map(c=>c.id),['a','c']);
 assert.deepEqual(logic.membersFor(r,'payoff').map(c=>c.id),['b','c']);
 assert.deepEqual(logic.membersFor(r,'breakthrough').map(c=>c.id),['c']);
 assert.deepEqual(logic.membersFor(r,'roleRarity:breakthrough:罕见').map(c=>c.id),['c']);
 const selected=logic.compute(data,defaults,{position:['payoff'],excludedPositions:['breakthrough']});
 assert.deepEqual(selected.cards.map(c=>c.id),['b']);assert.deepEqual(selected.routes[0].members.map(c=>c.id),['b']);
 const neutral={...base,id:'n',uid:'n',archetypeIds:[]};
 assert.equal(logic.compute({...data,cards:[neutral]},defaults,{excludedPositions:['breakthrough']}).cards.length,1);
});
function viewHarness(){
 const nodes=new Map(),dom=id=>{if(!nodes.has(id))nodes.set(id,{innerHTML:'',scrollTop:20,setAttribute(){},querySelector(){return null},querySelectorAll(){return[]},removeAttribute(){}});return nodes.get(id)};
 const ui={flowScope:'cards',flowRoute:'all',flowOnlyRewards:false,flowDrill:{route:stage.uid,key:'trigger'},routePositions:[],routeExcludedPositions:[]};
 const context=vm.createContext({JunnaRouteStatistics:logic,JunnaEffectStatistics:effects,console});
 vm.runInContext(fs.readFileSync(require.resolve('./route-statistics-view.js'),'utf8')+'\nglobalThis.createView=createJunnaRouteStatisticsView;',context);
 const table=(name,headers,rows)=>'<table><caption>'+name+'</caption><thead>'+headers.map(h=>'<th>'+h+'</th>').join('')+'</thead><tbody>'+rows.join('')+'</tbody></table>';
 const view=context.createView({data:()=>data,ui:()=>ui,defaults,dom,escape:String,table,flush(){},render(){},transact(){}});
 return{view,ui,nodes,dom};
}
test('包含和排除各渲染四个原生复选框，统计及定位编辑不再出现旧选项',()=>{
 const{view,dom}=viewHarness(),html=view.positionFilters();
 assert.equal((html.match(/type="checkbox"/g)||[]).length,8);assert(html.includes('流派定位'));assert(html.includes('排除定位'));
 for(const name of ['触发','收益','跨流派','突破限制'])assert.equal(html.split('>'+name+'</span>').length-1,2);
 assert(!html.includes('<select'));view.workspace();const rendered=dom('work-area').innerHTML;
 assert(!rendered.includes('undefined'));assert(rendered.includes('定位分布'));
 assert(!/<th>(兼具|工具|载体)<\/th>/.test(rendered));
 const fields=view.fields(c);assert.equal((fields.match(/data-flow-tag=/g)||[]).length,4);assert(!fields.includes('data-flow-role'));
});
test('旧筛选链接迁入新标签，新链接保存排除条件并支持重置',()=>{
 const source=fs.readFileSync(require.resolve('./app.js'),'utf8'),read=source.split('\n').find(line=>line.startsWith('  function readHash()')),save=source.split('\n').find(line=>line.startsWith('  function saveUI('));
 const context=vm.createContext({JunnaRouteStatistics:logic,URLSearchParams,ui:{tab:'cards',routePositions:null,routeExcludedPositions:null,routePosition:'all',breakthrough:'all',pool:'all'},state:{data:{terms:[]}},location:{hash:'#tab=cards&routePosition=both&breakthrough=ordinary'},previewMode:false,uiKey:'ui',localStorage:{setItem(){}},history:{replaceState(_a,_b,value){context.savedHash=value}}});
 vm.runInContext(read+'\n'+save+'\nreadHash();saveUI();',context);
 assert.equal(JSON.stringify(context.ui.routePositions),JSON.stringify(['trigger','payoff']));
 assert.equal(JSON.stringify(context.ui.routeExcludedPositions),JSON.stringify(['breakthrough']));
 assert(context.savedHash.includes('positions=trigger%2Cpayoff'));assert(context.savedHash.includes('excludePositions=breakthrough'));assert(!context.savedHash.includes('routePosition='));
 context.location.hash='#tab=cards';vm.runInContext('readHash()',context);assert.equal(context.ui.routePositions.length,0);assert.equal(context.ui.routeExcludedPositions.length,0);
});

test('流派与关键词使用筛选按钮，排序选项位于列表顶部',()=>{
 const source=fs.readFileSync(require.resolve('./app.js'),'utf8'),start=source.indexOf('  function renderSidebar(){'),end=source.indexOf('  function cardLinks(',start),sortStart=source.indexOf('  const CARD_SORTS='),sortEnd=source.indexOf('  function renderWorkspace(){',sortStart);
 const nodes={sidebar:{innerHTML:''}},ui={tab:'cards',search:'',archetype:'all',term:'all',pool:'all',type:'all',rarity:'all',cost:'all',sort:'cost',descending:false};
 const context=vm.createContext({ui,state:{data:{archetypes:[stage,partner],terms:[{id:'K',name:'知识',kind:'关键词'}]}},$:(id)=>nodes[id],escape:String,TYPES:['攻击','技能','能力','箭矢'],RARITIES:['基础','普通','罕见','稀有'],flowView:{positionFilters(){return '<fieldset>流派定位</fieldset>'}}});
 vm.runInContext(source.slice(start,end)+'\n'+source.slice(sortStart,sortEnd)+'\nrenderSidebar();globalThis.topSort=sortControls();',context);
 const sidebar=nodes.sidebar.innerHTML;assert(sidebar.includes('data-filter="archetype" data-filter-value="SYS_stage"'));assert(sidebar.includes('data-filter="term" data-filter-value="K"'));
 assert(!sidebar.includes('<select'));assert(!sidebar.includes('data-filter="sort"'));assert(!sidebar.includes('倒序'));
 assert.equal((context.topSort.match(/data-sort-key=/g)||[]).length,8);assert(context.topSort.includes('data-sort-direction'));assert(context.topSort.includes('data-sort-key="cost" aria-pressed="true"'));
});
test('顶部排序按钮切换排序项和方向，并保存状态及返回列表顶部',()=>{
 const source=fs.readFileSync(require.resolve('./app.js'),'utf8'),a=source.indexOf('  const CARD_SORTS='),b=source.indexOf('  function renderWorkspace(){',a);
 const ui={sort:'id',descending:false},nodes={'work-area':{scrollTop:100},'page-actions':{querySelector(){return{focus(){}}}}};let rendered=0,saved=0;
 const context=vm.createContext({ui,$:id=>nodes[id],mobileLayout:{scrollToTop(){nodes['work-area'].scrollTop=0;}},flushEdit(){},renderWorkspace(){rendered++},saveUI(){saved++}});
 vm.runInContext(source.slice(a,b)+'\nglobalThis.change=changeSort;',context);
 const event=dataset=>({target:{closest(){return{dataset}}}});
 assert(context.change(event({sortKey:'cost'})));assert.equal(ui.sort,'cost');assert.equal(nodes['work-area'].scrollTop,0);
 assert(context.change(event({})));assert.equal(ui.descending,true);assert.equal(rendered,2);assert.equal(saved,2);
 assert(!context.change(event({sortKey:'unknown'})));
 assert(source.includes("addEventListener('click',event=>{if(changeSort(event))return;"));
});

test('卡牌流派标签为原生多选，关键词调整保留隐藏的效果词条',()=>{
 const source=fs.readFileSync(require.resolve('./app.js'),'utf8'),start=source.indexOf('  function mergePickedIds('),end=source.indexOf('  function checkedPicker(',start),context=vm.createContext({escape:String});
 vm.runInContext(source.slice(start,end)+'\nglobalThis.merge=mergePickedIds;globalThis.picker=cardTagPicker;',context);
 const html=context.picker('流派','archetypeIds',[stage,partner],['SYS_stage','SYS_partner']);
 assert.equal((html.match(/type="checkbox"/g)||[]).length,2);assert.equal((html.match(/ checked/g)||[]).length,2);assert(!html.includes('type="radio"'));
 assert.equal(JSON.stringify(context.merge(['EF28','KW_STAGE','KW_EXHAUST'],['KW_STAGE','KW_EXHAUST'],['KW_STAGE'])),JSON.stringify(['EF28','KW_STAGE']));
 const cross={...c,design:{routeRoles:{[stage.uid]:{role:'both',cross:false}}}};assert(logic.positionTags(cross,[stage,partner],defaults).includes('cross'));
});
test('卡牌编辑区只显示面向牌面的编辑项，并合并流派与四种定位',()=>{
 const source=fs.readFileSync(require.resolve('./app.js'),'utf8'),start=source.indexOf('  function renderInspector(){'),end=source.indexOf('  function nextId(',start),helpers=source.slice(source.indexOf('  function mergePickedIds('),source.indexOf('  function checkedPicker('));
 const card={...c,system:'stage',upgradedCost:1,turns:2,upgradedTurns:1,target:'自己',upgradedText:c.text,naming:{},notes:'',termIds:['KW_STAGE','EF28']};
 const model={...data,cards:[a,b,card],terms:[{id:'KW_STAGE',name:'箭矢',kind:'关键词'},{id:'EF28',name:'布置效果',kind:'效果'}],meta:{}},nodes=new Map(),dom=id=>{if(!nodes.has(id))nodes.set(id,{innerHTML:'',dataset:{},setAttribute(){},removeAttribute(){},querySelectorAll(){return[]},querySelector(){return null}});return nodes.get(id)};
 const ui={tab:'cards',selected:{collection:'cards',uid:card.uid}},markup=(label,key)=>'<label>'+label+'<input data-field="'+key+'"></label>';
 const context=vm.createContext({state:{data:model},ui,current:()=>card,$:dom,escape:String,clone:structuredClone,mobileLayout:{update(){}},JunnaWorkbenchDomain:require('./workbench-domain.js'),field:markup,selectField:markup,area:markup,editorSection:(key,title,content)=>'<details data-editor-section="'+key+'">'+title+content+'</details>',cardHTML(){return''},knowledgeButton(){return''},groupsFor(){return[]},relatedHTML(){return''},arrowFocusEditor(){return''},fitCards(){},refreshArrowFocus(){},TYPES:['技能','攻击','箭矢'],RARITIES:['普通','罕见'],LABELS:{cards:'卡牌'},flowView:{fields(){return '<fieldset>流派定位</fieldset>'}}});
 vm.runInContext(helpers+'\n'+source.slice(start,end)+'\nrenderInspector();',context);
 const html=dom('editor-form').innerHTML;
 for(const field of ['name','type','rarity','cost','upgradedCost','turns','upgradedTurns','text','upgradedText'])assert(html.includes('data-field="'+field+'"'),field);
 for(const field of ['id','role','system','character'])assert(!html.includes('data-field="'+field+'"'),field);
 assert(!html.includes('data-raw-entity'));assert(!html.includes('data-editor-section="terms"'));assert(html.includes('data-pick="archetypeIds"'));assert(html.includes('data-pick="termIds"'));assert(!html.includes('value="EF28"'));assert(html.includes('流派定位'));
});
test('多流派卡牌可以切换定位编辑对象，只修改所选流派',()=>{
 const model=structuredClone(data),nodes=new Map(),dom=id=>{if(!nodes.has(id))nodes.set(id,{innerHTML:'',querySelector(){return null}});return nodes.get(id)},ui={tab:'cards',selected:{uid:'c'}};
 const context=vm.createContext({JunnaRouteStatistics:logic,JunnaEffectStatistics:effects});
 vm.runInContext(fs.readFileSync(require.resolve('./route-statistics-view.js'),'utf8')+'\nglobalThis.create=createJunnaRouteStatisticsView;',context);
 const view=context.create({data:()=>model,ui:()=>ui,defaults,dom,escape:String,table(){return''},flush(){},render(){},transact(_label,mutate){mutate(model)}});
 const card=model.cards.find(c=>c.id==='c'),originalStage=structuredClone(card.design.routeRoles[stage.uid]);
 assert(view.editorClick({target:{closest(){return{dataset:{cardPositionRoute:partner.uid}}}}}));
 assert.equal(ui.cardPositionRoute,partner.uid);
 assert(view.editorChange({target:{matches(){return true},dataset:{flowTag:'payoff'},checked:true,disabled:false}}));
 assert.equal(card.design.routeRoles[partner.uid].role,'both');assert.deepEqual(card.design.routeRoles[stage.uid],originalStage);
});

test('手动编辑非箭矢牌的触发标签后可保持选择',()=>{
 const card={...b,design:{routeRoles:{[stage.uid]:{role:'both',ports:['operation']}}}};
 assert.equal(logic.profile(card,stage,defaults).role,'both');
 assert(logic.matchesPosition(card,[stage],defaults,['trigger','payoff']));
});

test('修改流派多选后立即刷新对应定位，保留未显示的关联',()=>{
 const source=fs.readFileSync(require.resolve('./app.js'),'utf8'),handler=source.split('\n').find(line=>line.includes("$('editor-form').addEventListener('change',event=>{"));
 const card={archetypeIds:['SYS_stage','hidden'],system:'stage'},picks=[{value:'SYS_stage',checked:true},{value:'SYS_partner',checked:true}],events={};let refreshed=0,saved=0;
 const context=vm.createContext({$:id=>id==='editor-form'?{addEventListener(name,fn){events[name]=fn},querySelectorAll(){return picks}}:null,ui:{tab:'cards'},current:()=>card,state:{data:{archetypes:[{...stage,system:'stage'},{...partner,system:'partner'}]}},previewMode:false,mcpStatus:{ready:true},flowView:{editorChange(){return false}},beginEdit(){},flushEdit(){saved++},renderInspector(){refreshed++}});
 const helpers=source.slice(source.indexOf('  function mergePickedIds('),source.indexOf('  function cardTagPicker('));
 vm.runInContext(helpers+'\n'+handler,context);events.change({target:{dataset:{pick:'archetypeIds'},closest(){return null}}});
 assert.equal(JSON.stringify(card.archetypeIds),JSON.stringify(['hidden','SYS_stage','SYS_partner']));assert.equal(card.system,'stage');assert.equal(saved,1);assert.equal(refreshed,1);
});