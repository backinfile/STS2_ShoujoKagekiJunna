'use strict';
const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const logic=require('./route-statistics.js'),defaults=require('./route-statistics-defaults.json');
function fixture(){
  const route={id:'SYS_stage',uid:'archetype_SYS_stage',name:'箭矢',effectRoles:{}};
  const card=(id,role,pool=true)=>({id,uid:id,name:id,archetypeIds:[route.id],pool,rarity:'普通',cost:1,type:'技能',text:'',keywords:[],design:{routeRoles:{[route.uid]:{role,ports:['operation']}}}});
  const data={cards:[card('source','trigger'),card('payoff','payoff'),card('both','both'),card('companion','trigger',false)],archetypes:[route],partnerGroups:[]};
  const ui={flowScope:'cards',flowRoute:'all',flowOnlyRewards:false,routePositions:[],routeExcludedPositions:[]};
  const nodes=new Map(),dom=id=>{if(!nodes.has(id))nodes.set(id,{innerHTML:'',setAttribute(){},querySelector(){return null}});return nodes.get(id);};
  const context=vm.createContext({JunnaFilters:require('./filter-state.js'),JunnaRouteStatistics:logic});vm.runInContext(fs.readFileSync(require.resolve('./route-statistics-view.js'),'utf8'),context);
  const table=(name,headers,rows)=>'<table><caption>'+name+'</caption><thead>'+headers.map(h=>'<th>'+h+'</th>').join('')+'</thead><tbody>'+rows.join('')+'</tbody></table>';
  const view=context.createJunnaRouteStatisticsView({data:()=>data,ui:()=>ui,defaults,dom,escape:String,table,flush(){},render(){}});
  return{view,ui,dom,route};
}
test('flow page reports actual overlapping roles and removes unsupported recommendations',()=>{
  const f=fixture();f.view.workspace();const html=f.dom('work-area').innerHTML;
  assert.match(html,/定位分布/);assert.match(html,/触发∶收益/);assert.match(html,/3∶2/);
  assert.doesNotMatch(html,/建议触发|建议收益|建议比例|推荐定位|数量建议|触发牌差额|收益牌差额/);
  assert.equal(defaults.recommendations,undefined);assert.equal(logic.recommend,undefined);
});
test('reward and position filters determine both the counts and the displayed ratio',()=>{
  const f=fixture();f.ui.flowOnlyRewards=true;f.view.workspace();assert.match(f.dom('work-area').innerHTML,/1∶1/);
  f.ui.routePositions=['payoff'];f.view.workspace();assert.match(f.dom('work-area').innerHTML,/1∶2/);
});
test('clicking a role drills into the same actual cards that were counted',()=>{
  const f=fixture();f.view.workClick({target:{closest(){return{dataset:{flowRoute:f.route.uid,flowKey:'trigger'}};}}});f.view.workspace();
  const html=f.dom('work-area').innerHTML;
  for(const id of ['source','both','companion'])assert(html.includes('data-uid="'+id+'"'));
  assert(!html.includes('data-uid="payoff"'));
});
