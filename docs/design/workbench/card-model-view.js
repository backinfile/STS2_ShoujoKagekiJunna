function createJunnaCardModelView({data,ui,dom,escape,render}) {
  const sections=[['quick','数值速查'],['battle','战斗模拟'],['rarity','稀有度'],['exhaust','消耗'],['effects','附加效果'],['native','本体对照'],['cards','全卡校验']];
  const audit=JSON.parse(document.getElementById('workbench-card-audit')?.textContent||'null');
  const simulation=JSON.parse(document.getElementById('workbench-battle-sim')?.textContent||'null');
  const waitModel=simulation?.recommendation.byCost||{'0':{base:6,upgraded:9,waitBase:2,waitUpgraded:3},'1':{base:10,upgraded:14,waitBase:3,waitUpgraded:5},'2':{base:20,upgraded:28,waitBase:5,waitUpgraded:7}};
  const estimate=(cost,turns)=>{const model=waitModel[cost];return model?[model.base+model.waitBase*turns,model.upgraded+model.waitUpgraded*turns]:null;};
  const ordinary=estimate(1,2);
  const examples=Object.fromEntries(Object.entries({'基础':0.9,'普通':1,'罕见':1.125,'稀有':1.25}).map(([rarity,ratio])=>{const normal=ordinary.map(value=>Math.round(value*ratio));return [rarity,{normal,exhaust:[normal[0]+4,normal[1]+6]}];}));
  const pair=values=>values.join('／');
  const table=(title,headers,rows)=>'<section class="model-section"><h2>'+escape(title)+'</h2><div class="model-table-scroll" tabindex="0" role="region" aria-label="'+escape(title)+'"><table class="model-table"><thead><tr>'+headers.map(h=>'<th scope="col">'+escape(h)+'</th>').join('')+'</tr></thead><tbody>'+rows.map(row=>'<tr>'+row.map((cell,i)=>i?'<td>'+escape(cell)+'</td>':'<th scope="row">'+escape(cell)+'</th>').join('')+'</tr>').join('')+'</tbody></table></div></section>';
  const prose=(title,body)=>'<section class="model-section"><h2>'+escape(title)+'</h2>'+body+'</section>';
  const list=items=>'<ul class="model-list">'+items.map(item=>'<li>'+escape(item)+'</li>').join('')+'</ul>';
  const selectedSection=()=>sections.some(([id])=>id===ui().modelSection)?ui().modelSection:'quick';
  function startingDeck(){
    const rows=(data().meta.startingDeck||[]).flatMap(entry=>{const card=data().cards.find(c=>c.id===entry.cardId);return card?[[card.name,entry.count+'张',card.cost+'费',card.text]]:[];});
    const ancientRows=(data().meta.startingDeckRules?.transcendenceUpgrades||[]).flatMap(entry=>{const starter=data().cards.find(c=>c.id===entry.starterCardId),ancient=data().cards.find(c=>c.id===entry.ancientCardId);return starter&&ancient?[[starter.name,ancient.name,ancient.cost+'费 · '+ancient.turns+'回合',ancient.text,ancient.upgradedText]]:[];});
    return table('起始牌组 · 10张',['卡牌','数量','费用','基础效果'],rows)+(ancientRows.length?table('远古替换',['原牌','远古牌','费用／回合','基础效果','升级效果'],ancientRows):'');
  }
  function sidebar(){
    return '<div class="sidebar-section"><p class="sidebar-title">卡牌模型</p>'+sections.map(([id,name])=>'<button type="button" class="side-item '+(selectedSection()===id?'active':'')+'" data-model-section="'+id+'" aria-pressed="'+(selectedSection()===id)+'"><span>'+name+'</span></button>').join('')+'</div>';
  }
  function quick(){
    const starter=data().cards.find(card=>card.id==='JB03');
    return '<p class="model-intro">先看费用与等待，再分配稀有度和消耗预算。表中数值均为<strong>基础／升级</strong>。</p>'+table('普通 · 非消耗 · 纯伤箭矢',['等待','0费伤害','1费伤害','2费伤害'],[0,1,2,3].map(turns=>[
      turns?turns+'回合':'0回合（立即）',...['0','1','2'].map(cost=>pair(estimate(cost,turns)))
    ]))+table('起算参数',['费用','即时基准','每回合等待补偿'],Object.entries(waitModel).map(([cost,model])=>[cost+'费',pair([model.base,model.upgraded]),'＋'+pair([model.waitBase,model.waitUpgraded])]))+prose('起算公式','<p>建议伤害＝即时基准＋等待回合×本档补偿。</p><p>普通、非消耗、单体单段；0—2费，等待0—3回合。等待补偿按战斗模拟分档。</p>')+table('1费 · 2回合 · 分档建议',['稀有度','非消耗','消耗'],Object.entries(examples).map(([rarity,value])=>[rarity,pair(value.normal),pair(value.exhaust)]))+prose('起始牌 · '+(starter?.name||'弓击'),starter?'<div class="model-starter"><button type="button" class="text-button" data-open-card="'+escape(starter.uid)+'">'+escape(starter.name)+' ↗</button><span>'+escape(starter.cost)+'费 · '+escape(starter.turns)+'回合</span><span>'+escape(starter.text)+'</span><span>升级：'+escape(starter.upgradedText)+'</span></div>'+(estimate(starter.cost,starter.turns)?'<p>普通同费用、同回合数基准：'+pair(estimate(starter.cost,starter.turns))+'伤害。</p>':''):'<p>当前卡池暂无JB03。</p>')+startingDeck();
  }
  function battle(){
    if(!simulation)return prose('战斗模拟','<p>暂无结果。</p>');
    const signed=value=>(value>0?'＋':value<0?'−':'')+Math.abs(value).toFixed(2);
    const curves={fixed6u8:'统一＋6／8',fixed3u4:'统一＋3／4',byCost:'按费用分档'};
    const envs={standard:'3能量 · 抽5张',energy4:'4能量 · 抽5张',draw7:'3能量 · 抽7张',upgradedShell:'其余卡牌全部升级'};
    return '<p class="model-intro">'+simulation.totalBattles.toLocaleString('zh-CN')+'场 · 卡池快照 '+simulation.sourceRevision+' · '+simulation.date+'</p>'+table('等待补偿建议',['费用','即时基准','每回合补偿'],Object.entries(simulation.recommendation.byCost).map(([cost,value])=>[cost+'费',pair([value.base,value.upgraded]),'＋'+pair([value.waitBase,value.waitUpgraded])]))
      +table('统一补偿比较 · 单体与群体加速',['每回合补偿','基础承伤差','基础回合差','升级承伤差','升级回合差'],simulation.fixedComparison.map(row=>['＋'+pair([row.base,row.upgraded]),signed(row.baseResult.loss),signed(row.baseResult.turns),signed(row.upgradedResult.loss),signed(row.upgradedResult.turns)]))
      +'<p class="model-note">差值相对同费用即时攻击。正值表示承伤更多或结束更晚。</p>'
      +table('混合费用牌组 · 新种子验证',['环境','等待模型','承伤差','回合差'],simulation.mixedComparison.map(row=>[envs[row.environment]||row.environment,curves[row.curve]||row.curve,signed(row.deltaLoss),signed(row.deltaTurns)]))
      +table('虔诚雕刻师 · 模型内增长压力',['等待模型','胜率','即时对照胜率'],simulation.pressureComparison.map(row=>[curves[row.curve],(row.winRate*100).toFixed(1)+'%',(row.referenceWinRate*100).toFixed(1)+'%']))
      +table('运行记录',['比较','战斗数'],simulation.runs.map(run=>[run.name,run.battles.toLocaleString('zh-CN')]))
      +prose('战斗条件',list(['80生命、3能量、每回合抽5张，手牌上限10。','17张牌；受测牌2张或5张，密集组替换3张打击。','基础牌组固定，仅升级受测牌。4能量、抽7张、其余卡牌升级另列验证。','正式比较每个条件'+simulation.setup.mainSeeds+'个配对种子，混合验证使用'+simulation.setup.validationSeeds+'个新种子。','出牌策略只读取当前手牌和敌人意图，搜索两步出牌；抽牌后重新决策。','箭矢布置期间退出抽弃循环，锁定目标死亡后伤害落空，加速正常支付费用。']))
      +table('敌方场景',['场景','类型'],simulation.encounters.map(enemy=>[enemy.label,{native:'本体动作与数值',constructed_native_enemies:'本体敌人组合',synthetic_control:'自设控制场景'}[enemy.kind]]))
      +prose('模拟范围',list(['本轮：'+simulation.setup.scope.join('、')+'。','后续：'+simulation.setup.extensions.join('、')+'。']));
  }
  function rarity(){
    return '<p class="model-intro">稀有度决定总预算。更高的预算可以分给伤害，也可以分给附加效果。</p>'+table('纯数值牌分档',['稀有度','普通基准的比例','预算用途'],[
      ['基础','约85%—90%','展示机制，保留成长空间'],['普通','100%','独立可用的基准'],['罕见','约110%—115%','提高数值，或增加实用附效'],['稀有','约120%—130%','提高数值，或提供复制、重放、减费等能力']
    ])+prose('把附效计入同一份预算',list(['先估算同费用、同等待、同稀有度的纯伤收益，再为附效留出份额。','知识牌按整组获得方式、额外牌张和实际配合一起估值。','稀有牌已有强力减费或重放时，裸伤可以保持在普通档。']))+prose('本体例子 · 狂宴','<p>稀有，1费，消耗，造成10／12伤害；斩杀时增加3／4点最大生命。它的稀有预算主要用于永久成长。</p>')+table('1费 · 2回合 · 纯伤取整示例',['稀有度','非消耗','消耗'],Object.entries(examples).map(([rarity,value])=>[rarity,pair(value.normal),pair(value.exhaust)]))+'<p class="model-note">比例用于起算，表格为取整后的纸面建议。</p>';
  }
  function exhaust(){
    return '<p class="model-intro">消耗补偿的是后续使用机会。箭矢等待越久，同一场战斗里再次抽到并完成的机会通常越少。</p>'+table('纯伤箭矢 · 消耗补偿',['实际平均等待','基础伤害增加','升级伤害增加'],[
      ['经常当回合完成','＋6','＋8'],['约1回合','＋5','＋7'],['约2回合','＋4','＋6'],['约3回合','＋2','＋3']
    ])+prose('按实际使用方式调整',list(['经常立即完成的箭矢，使用“当回合完成”档，即使卡面写着3回合。','等待期间可以反复额外触发的箭矢，减少消耗补偿。','消耗在整批完成后处理；本次重放与布置期间的额外触发仍会执行。','精简牌库与消耗联动本身也有收益，应从消耗补偿中扣除。']))+prose('一次计算','<p>普通、1费、2回合：'+pair(ordinary)+'伤害。加入消耗，增加4／6，得到<strong>'+pair(examples['普通'].exhaust)+'伤害</strong>。</p>')+prose('升级移除消耗','<p>本体「深谋远虑」和「彩虹」的升级只移除消耗。选择这条升级路线时，把恢复循环使用的收益计入升级预算，裸数值保持或小幅增加。</p>')+prose('其他效果',list(['循环抽牌与返能牌：消耗会限制后续展开，需要单独计算。','永久强化牌：主要收益在第一次使用，消耗还会精简牌库。','格挡牌：结合预计再次使用的次数，以及兑现回合的防御需求。']));
  }
  function effects(){
    return '<p class="model-intro">先确定纯数值预算，再计算附加效果与配合收益。</p>'+table('调整方向',['效果','怎样计入模型'],[
      ['多段伤害','降低裸伤，为每段都能获得的伤害加成留出预算。'],['抽牌','参考即时抽牌，并计入延迟补偿与完成时的手牌空间。'],['格挡','计入兑现回合的防御需求；可控加速提高兑现的可靠性。'],['获得能量','计算支付后的净收益，以及能否继续启动其他牌。'],['重放／额外触发','整份牌效都会受益；伤害、抽牌、返能分别计入。'],['知识','连同关联牌的获得方式、牌张负担和减费一起估值。'],['10回合等特殊载荷','按实际完成手段单独估值。']
    ])+prose('多段示例','<p>获得3点伤害加成时，单段增加3伤害，三段合计增加9伤害。多段牌的裸伤预算因此更低。</p>')+table('抽牌与格挡 · 起始参考',['效果','条件','基础／升级'],[
      ['抽牌箭矢','罕见，1费，1回合','抽4／5张'],['纯格挡箭矢','普通，1费，1回合','12／16格挡']
    ])+prose('返能参照','<p>本体「唤起」为普通、1费，下回合召唤2／3并获得2／3能量。返能箭矢要同时考虑布置费用、等待、加速成本和重复触发次数。</p>');
  }
  function native(){
    return '<p class="model-intro">本体数值来自本机安装版本 <strong>v0.111.0</strong>。箭矢建议值来自对这些卡牌的纸面估算。</p>'+table('伤害与蓄力',['卡牌','稀有度／费用','基础／升级效果'],[
      ['切割','普通 · 0费','立即造成6／9伤害'],['双重打击','普通 · 1费','立即造成5×2／7×2伤害'],['重锤','罕见 · 3费','立即造成32／42伤害'],['炸弹','罕见 · 2费','第三次己方回合结束，全体40／50伤害'],['漆黑','罕见 · 1费','生成黑暗球，触发全部黑暗球被动一次／两次']
    ])+prose('等待时点','<div class="model-table-scroll" tabindex="0" role="region" aria-label="等待时点"><table class="model-table"><thead><tr><th scope="col">牌</th><th scope="col">第1回合</th><th scope="col">第2回合</th><th scope="col">第3回合</th></tr></thead><tbody><tr><th scope="row">2回合箭矢</th><td>打出，计数2</td><td>开始时减至1</td><td><strong>开始时结算</strong></td></tr><tr><th scope="row">炸弹</th><td>打出，结束减至2</td><td>结束减至1</td><td><strong>结束时结算</strong></td></tr></tbody></table></div><p>两者都经过两次敌方行动，箭矢更早兑现。</p>')+prose('黑暗球的积累','<p>无集中时，基础「漆黑」生成的球从6累积到12，当回合结束到18，再过一次回合结束到24。兑现需要激发。</p>')+table('稀有度与消耗',['卡牌','稀有度／费用','基础／升级效果'],[
      ['快速检索','罕见 · 1费','抽3／4张'],['战略大师','稀有 · 0费 · 消耗','抽3／4张'],['深谋远虑','罕见 · 0费 · 消耗','抽2张，1张手牌放回牌堆顶；升级移除消耗'],['飞跃','普通 · 1费','9／12格挡'],['岿然不动','稀有 · 2费 · 消耗','30／40格挡'],['狂宴','稀有 · 1费 · 消耗','10／12伤害；斩杀增加3／4最大生命'],['唤起','普通 · 1费','下回合召唤2／3，获得2／3能量']
    ])+prose('怎样使用这些参照',list(['快速检索与战略大师体现了稀有度和消耗共同提供的1费差距。','深谋远虑说明，恢复循环使用可以占满一次升级。','炸弹提供延迟补偿的量级；范围伤害、加速与重复触发分别计价。']));
  }
  function cards(){
    const current=data(),byId=new Map((audit?.rows||[]).map(row=>[row.id,row]));
    const keys=['type','rarity','pool','cost','upgradedCost','turns','upgradedTurns','text','upgradedText'];
    const snapshot=c=>c?Object.fromEntries(keys.map(key=>[key,c[key]??null])):null;
    const links=id=>current.partnerGroups.filter(g=>g.mainCardId===id||g.grantedCardIds.includes(id)).map(g=>({main:g.mainCardId,members:[g.mainCardId,...g.grantedCardIds].map(key=>{const c=current.cards.find(c=>c.id===key);return {id:key,count:JunnaWorkbenchDomain.groupCardCount(g,key),scope:!c?'missing':c.trashedAt?'trash':c.reservedAt?'reserve':'active'};})}));
    const same=(a,b)=>JSON.stringify(a)===JSON.stringify(b),metadata=c=>({rules:c.design?.rules||'',notes:c.notes||'',keywords:c.keywords||[]});
    const entries=current.cards.filter(c=>!c.trashedAt).map(c=>{const row=byId.get(c.id),usesWaitModel=c.type==='箭矢'&&c.arrowSubtype==='攻击'&&waitModel[c.cost]&&c.turns<=3,modelFresh=!simulation||!usesWaitModel||row?.waitModelId===simulation.id,fresh=modelFresh&&row&&row.status!=='已删除'&&same(snapshot(c),row.snapshot)&&same(links(c.id),row.groupSnapshot)&&same(metadata(c),row.metadata)&&(row.dependencies||[]).every(d=>same(snapshot(current.cards.find(c=>c.id===d.id)),d.snapshot));return {card:c,row,status:fresh?row.status:'待复核'};});
    const rank={'待复核':0,'需处理':1,'建议调整':2,'已校正':3,'保留初值':4};entries.sort((a,b)=>rank[a.status]-rank[b.status]||(a.row?.priority||3)-(b.row?.priority||3)||a.card.id.localeCompare(b.card.id));
    const counts=[['在用',entries.filter(e=>!e.card.reservedAt).length],['备用',entries.filter(e=>e.card.reservedAt).length],...Object.keys(rank).map(status=>[status,entries.filter(e=>e.status===status).length])].filter(([,n])=>n);
    const face=(c,upgraded)=>escape(upgraded?c.upgradedCost:c.cost)+'费'+(c.type==='箭矢'?' · '+escape(upgraded?c.upgradedTurns:c.turns)+'回合':'')+'<br>'+escape(upgraded?c.upgradedText:c.text);
    const rows=entries.map(({card:c,row,status})=>{let content=row&&row.status!=='已删除'?'<p>'+escape(row.basis)+'</p>'+(row.recommendation?'<p>'+escape(row.recommendation)+'</p>':''):'';if(status==='待复核'&&content)content='<details class="form-details"><summary>上次结论</summary>'+content+'</details>';return '<tr data-audit-card="'+escape(c.id)+'" data-audit-status="'+escape(status)+'"><th scope="row"><button type="button" class="text-button" data-open-card="'+escape(c.uid)+'">'+escape(c.name)+' ↗</button><span class="model-card-id">'+escape(c.rarity)+' · '+escape(c.id)+(c.reservedAt?' · 备用':'')+'</span></th><td>'+face(c,false)+'<br><span class="model-secondary">升级：'+face(c,true)+'</span></td><td><strong>'+escape(status)+'</strong>'+content+'</td></tr>';});
    const deleted=current.cards.filter(c=>c.trashedAt);
    return '<p class="model-intro">'+counts.map(([name,n])=>escape(name)+' '+n).join(' · ')+'</p><section class="model-section"><h2>全卡数值校验 · '+escape(audit?.date||'')+'</h2><div class="model-table-scroll" tabindex="0" role="region" aria-label="全卡数值校验"><table class="model-table model-card-table"><thead><tr><th scope="col">卡牌</th><th scope="col">当前牌面</th><th scope="col">结论与建议</th></tr></thead><tbody>'+rows.join('')+'</tbody></table></div></section><details class="form-details"><summary>已删除 · '+deleted.length+'</summary>'+deleted.map(c=>'<button type="button" class="text-button" data-open-card="'+escape(c.uid)+'">'+escape(c.id)+' · '+escape(c.name)+'</button>').join('、')+'</details>';
  }
  function workspace(){
    dom('page-title').textContent='卡牌模型';dom('result-count').textContent='纸面初值 · 2026-11-03';dom('page-actions').innerHTML='';dom('work-area').setAttribute('aria-labelledby','tab-models');
    dom('work-area').innerHTML='<article class="model-guide" aria-label="'+sections.find(([id])=>id===selectedSection())[1]+'">'+({quick,battle,rarity,exhaust,effects,native,cards}[selectedSection()]())+'</article>';
  }
  function inspector(){
    const rarity=examples[ui().modelRarity]?ui().modelRarity:'普通',consumed=Boolean(ui().modelExhaust),value=examples[rarity],result=consumed?value.exhaust:value.normal;
    const signed=n=>(n>=0?'＋':'−')+Math.abs(n),delta=[value.normal[0]-ordinary[0],value.normal[1]-ordinary[1]];
    dom('inspector').hidden=false;dom('inspector-title').textContent='模型示例';dom('close-inspector').hidden=true;dom('preview').innerHTML='';dom('preview').removeAttribute('tabindex');dom('preview').removeAttribute('aria-label');dom('inspector-footer').hidden=true;
    dom('editor-form').innerHTML='<div class="model-example"><p class="model-example-spec">1费 · 2回合 · 单体单段</p><label class="field"><span class="field-label">稀有度</span><select data-model-rarity>'+Object.keys(examples).map(key=>'<option'+(key===rarity?' selected':'')+'>'+key+'</option>').join('')+'</select></label><label class="check model-exhaust-check"><input type="checkbox" data-model-exhaust'+(consumed?' checked':'')+'>消耗</label><div class="model-example-result" aria-live="polite" aria-atomic="true"><dl class="model-results"><div><dt>基础伤害</dt><dd>'+result[0]+'</dd></div><div><dt>升级伤害</dt><dd>'+result[1]+'</dd></div></dl></div><h3>计算过程</h3><dl class="model-breakdown"><div><dt>普通基准</dt><dd>'+pair(ordinary)+'</dd></div><div><dt>稀有度分档</dt><dd>'+delta.map(signed).join('／')+'</dd></div><div><dt>消耗补偿</dt><dd>'+(consumed?'＋4／＋6':'0／0')+'</dd></div><div><dt>建议伤害</dt><dd>'+pair(result)+'</dd></div></dl><p class="model-note">分档后的取整示例。附加效果另计。</p></div>';
  }
  function sidebarClick(event){const button=event.target.closest('[data-model-section]');if(!button)return false;ui().modelSection=button.dataset.modelSection;render();dom('work-area').scrollTop=0;dom('sidebar').querySelector('[data-model-section="'+ui().modelSection+'"]')?.focus({preventScroll:true});return true;}
  function editorChange(event){const input=event.target;if(!input.matches('[data-model-rarity],[data-model-exhaust]'))return false;if(input.hasAttribute('data-model-rarity'))ui().modelRarity=input.value;else ui().modelExhaust=input.checked;const selector=input.hasAttribute('data-model-rarity')?'[data-model-rarity]':'[data-model-exhaust]';const scroll=dom('work-area').scrollTop;render();dom('work-area').scrollTop=scroll;dom('editor-form').querySelector(selector)?.focus({preventScroll:true});return true;}
  return {sidebar,workspace,inspector,sidebarClick,editorChange};
}
