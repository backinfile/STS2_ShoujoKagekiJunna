function createJunnaCardModelView({data,ui,dom,escape,render,cardHTML,fitCards}) {
  const sections=[['quick','数值速查'],['vanilla','白板牌'],['battle','战斗模拟'],['rarity','稀有度'],['exhaust','消耗'],['effects','附加效果'],['native','本体对照'],['cards','全卡校验']];
  const audit=JSON.parse(document.getElementById('workbench-card-audit')?.textContent||'null');
  const simulation=JSON.parse(document.getElementById('workbench-battle-sim')?.textContent||'null');
  const waitModel=()=>data().meta.vanillaCardModels?.parameters.attack||simulation?.recommendation.byCost||{'0':{base:6,upgraded:9,waitBase:2,waitUpgraded:3},'1':{base:10,upgraded:14,waitBase:3,waitUpgraded:5},'2':{base:20,upgraded:28,waitBase:5,waitUpgraded:7}};
  const estimate=(cost,turns,kind='attack')=>{const model=(kind==='aoe'?vanillaModels()?.parameters.aoe:waitModel())?.[cost],point=model?.byTurns?.[turns];return point?[point.base,point.upgraded]:model&&Number.isFinite(model.waitBase)?[model.base+model.waitBase*turns,model.upgraded+model.waitUpgraded*turns]:null;};
  const modelExamples=()=>{const ordinary=estimate(1,2);return {ordinary,examples:Object.fromEntries(Object.entries({'基础':0.9,'普通':1,'罕见':1.125,'稀有':1.25}).map(([rarity,ratio])=>{const normal=ordinary.map(value=>Math.round(value*ratio));return [rarity,{normal,exhaust:[normal[0]+4,normal[1]+6]}];}))};};
  const pair=values=>values.join('／');
  const table=(title,headers,rows)=>'<section class="model-section"><h2>'+escape(title)+'</h2><div class="model-table-scroll" tabindex="0" role="region" aria-label="'+escape(title)+'"><table class="model-table"><thead><tr>'+headers.map(h=>'<th scope="col">'+escape(h)+'</th>').join('')+'</tr></thead><tbody>'+rows.map(row=>'<tr>'+row.map((cell,i)=>i?'<td>'+escape(cell)+'</td>':'<th scope="row">'+escape(cell)+'</th>').join('')+'</tr>').join('')+'</tbody></table></div></section>';
  const prose=(title,body)=>'<section class="model-section"><h2>'+escape(title)+'</h2>'+body+'</section>';
  const list=items=>'<ul class="model-list">'+items.map(item=>'<li>'+escape(item)+'</li>').join('')+'</ul>';
  const selectedSection=()=>sections.some(([id])=>id===ui().modelSection)?ui().modelSection:'quick';
  const vanillaModels=()=>data().meta.vanillaCardModels;
  const filteredModels=()=> (vanillaModels()?.cards||[]).filter(card=>(!ui().modelKind||ui().modelKind==='all'||card.kind===ui().modelKind)&&(!ui().modelCost||ui().modelCost==='all'||String(card.cost)===ui().modelCost)&&(!ui().modelTurns||ui().modelTurns==='all'||String(card.turns)===ui().modelTurns));
  const selectedModel=()=>{const cards=filteredModels();return cards.find(card=>card.uid===ui().modelSelected)||cards[0];};
  const modelFilter=(label,key,values)=>'<fieldset class="field filter-group"><legend class="field-label">'+escape(label)+'</legend><div class="filter-options">'+[['all','全部'],...values].map(([value,name])=>'<button type="button" class="filter-option" data-model-filter="'+key+'" data-model-value="'+value+'" aria-pressed="'+((ui()[key]||'all')===value)+'">'+escape(name)+'</button>').join('')+'</div></fieldset>';
  function startingDeck(){
    const rows=(data().meta.startingDeck||[]).flatMap(entry=>{const card=data().cards.find(c=>c.id===entry.cardId);return card?[[card.name,entry.count+'张',card.cost+'费',card.text]]:[];});
    const ancientRows=(data().meta.startingDeckRules?.transcendenceUpgrades||[]).flatMap(entry=>{const starter=data().cards.find(c=>c.id===entry.starterCardId),ancient=data().cards.find(c=>c.id===entry.ancientCardId);return starter&&ancient?[[starter.name,ancient.name,ancient.cost+'费 · '+ancient.turns+'回合',ancient.text,ancient.upgradedText]]:[];});
    return table('起始牌组 · 10张',['卡牌','数量','费用','基础效果'],rows)+(ancientRows.length?table('远古替换',['原牌','远古牌','费用／回合','基础效果','升级效果'],ancientRows):'');
  }
  function sidebar(){
    return '<div class="sidebar-section"><p class="sidebar-title">卡牌模型</p>'+sections.map(([id,name])=>'<button type="button" class="side-item '+(selectedSection()===id?'active':'')+'" data-model-section="'+id+'" aria-pressed="'+(selectedSection()===id)+'"><span>'+name+'</span></button>').join('')+'</div>'+(selectedSection()==='vanilla'?modelFilter('效果','modelKind',[['attack','单体攻击'],['aoe','群体攻击'],['block','防御']])+modelFilter('费用','modelCost',[0,1,2,3,4].map(n=>[String(n),n+'费']))+modelFilter('回合','modelTurns',[0,1,2,3].map(n=>[String(n),n+'回合'])):'');
  }
  function vanilla(){
    const cards=filteredModels(),selected=selectedModel(),upgraded=Boolean(ui().modelUpgraded);
    if(!cards.length)return '<p class="empty">暂无模型</p>';
    return [0,1,2,3].flatMap(turns=>{const group=cards.filter(card=>card.turns===turns);return group.length?[prose(turns?turns+'回合':'0回合 · 即时','<div class="card-grid model-vanilla-grid">'+group.map(card=>'<div class="card-entry model-vanilla-card'+(selected?.uid===card.uid?' selected':'')+'"><button type="button" class="card-button" data-model-card="'+escape(card.uid)+'" aria-pressed="'+(selected?.uid===card.uid)+'" aria-label="'+escape(card.name+'，'+card.cost+'费，'+turns+'回合，'+(upgraded?card.upgradedText:card.text))+'">'+cardHTML(card,upgraded)+'</button></div>').join('')+'</div>')]:[];}).join('');
  }
  function vanillaInspector(){
    const card=selectedModel(),upgraded=Boolean(ui().modelUpgraded);
    dom('inspector').hidden=false;dom('inspector-title').textContent='白板模型';dom('close-inspector').hidden=true;dom('inspector-footer').hidden=true;
    dom('preview').removeAttribute('tabindex');dom('preview').removeAttribute('aria-label');
    if(!card){dom('preview').innerHTML='';dom('editor-form').innerHTML='';return;}
    const params=vanillaModels().parameters[card.kind][card.cost],unit=card.kind==='block'?'格挡':'伤害',basis=vanillaModels().basis?.[card.kind]?.status==='paperEstimate'||card.cost>2?'纸面推算':'战斗模拟基准';
    dom('preview').innerHTML='<div class="model-vanilla-card">'+cardHTML(card,upgraded)+'</div>';
    dom('editor-form').innerHTML='<div class="model-example"><p class="model-example-spec">普通 · '+card.cost+'费 · '+card.turns+'回合 · 非消耗'+(card.kind==='aoe'?' · 全体敌人':'')+'</p><div class="model-example-result"><dl class="model-results"><div><dt>基础'+unit+'</dt><dd>'+card.modelValue+'</dd></div><div><dt>升级'+unit+'</dt><dd>'+card.upgradedModelValue+'</dd></div></dl></div><h3>模型参数</h3><dl class="model-breakdown"><div><dt>即时基准</dt><dd>'+pair([params.base,params.upgraded])+'</dd></div><div><dt>累计等待补偿</dt><dd>＋'+pair([card.modelValue-params.base,card.upgradedModelValue-params.upgraded])+'</dd></div><div><dt>等待回合</dt><dd>'+card.turns+'</dd></div><div><dt>基础／升级</dt><dd>'+pair([card.modelValue,card.upgradedModelValue])+'</dd></div></dl><p class="model-note">'+basis+'</p></div>';
  }
  function quick(){
    const starter=data().cards.find(card=>card.id==='JB03');
    const models=waitModel(),costs=Object.keys(models),{examples}=modelExamples();
    const groupTable=vanillaModels()?.parameters.aoe?table('普通 · 非消耗 · 群体伤害（每名敌人）',['等待',...costs.map(cost=>cost+'费伤害')],[0,1,2,3].map(turns=>[turns?turns+'回合':'0回合（立即）',...costs.map(cost=>pair(estimate(cost,turns,'aoe')))])): '';
    return '<p class="model-intro">先看费用与等待，再分配稀有度和消耗预算。表中数值均为<strong>基础／升级</strong>。</p>'+table('普通 · 非消耗 · 纯伤箭矢',['等待',...costs.map(cost=>cost+'费伤害')],[0,1,2,3].map(turns=>[
      turns?turns+'回合':'0回合（立即）',...costs.map(cost=>pair(estimate(cost,turns)))
    ]))+groupTable+table('等待补偿',['费用','0→1回合','1→2回合','2→3回合'],costs.map(cost=>[cost+'费',...[1,2,3].map(turns=>{const before=estimate(cost,turns-1),after=estimate(cost,turns);return '＋'+pair(after.map((value,i)=>value-before[i]));})]))+prose('起算公式','<p>建议伤害＝即时基准＋所处回合档位的累计等待补偿。</p><p>普通、非消耗、单体单段；0—'+costs.at(-1)+'费，等待0—3回合，按自然完成估值。</p>')+table('1费 · 2回合 · 分档建议',['稀有度','非消耗','消耗'],Object.entries(examples).map(([rarity,value])=>[rarity,pair(value.normal),pair(value.exhaust)]))+prose('起始牌 · '+(starter?.name||'弓击'),starter?'<div class="model-starter"><button type="button" class="text-button" data-open-card="'+escape(starter.uid)+'">'+escape(starter.name)+' ↗</button><span>'+escape(starter.cost)+'费 · '+escape(starter.turns)+'回合</span><span>'+escape(starter.text)+'</span><span>升级：'+escape(starter.upgradedText)+'</span></div>'+(estimate(starter.cost,starter.turns)?'<p>普通同费用、同回合数基准：'+pair(estimate(starter.cost,starter.turns))+'伤害。</p>':''):'<p>当前卡池暂无JB03。</p>')+startingDeck();
  }
  function battle(){
    if(!simulation)return prose('战斗模拟','<p>暂无结果。</p>');
    const signed=value=>(value>0?'＋':value<0?'−':'')+Math.abs(value).toFixed(2);
    const curves={fixed6u8:'统一＋6／8',fixed3u4:'统一＋3／4',byCost:'按费用分档'};
    const envs={standard:'3能量 · 抽5张',energy4:'4能量 · 抽5张',draw7:'3能量 · 抽7张',upgradedShell:'其余卡牌全部升级'};
    return '<p class="model-intro">'+simulation.totalBattles.toLocaleString('zh-CN')+'场 · 卡池快照 '+simulation.sourceRevision+' · '+simulation.date+'</p>'+table('历史模拟补偿',['费用','即时基准','每回合补偿'],Object.entries(simulation.recommendation.byCost).map(([cost,value])=>[cost+'费',pair([value.base,value.upgraded]),'＋'+pair([value.waitBase,value.waitUpgraded])]))
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
    const {examples}=modelExamples();
    return '<p class="model-intro">稀有度决定总预算。更高的预算可以分给伤害，也可以分给附加效果。</p>'+table('纯数值牌分档',['稀有度','普通基准的比例','预算用途'],[
      ['基础','约85%—90%','展示机制，保留成长空间'],['普通','100%','独立可用的基准'],['罕见','约110%—115%','提高数值，或增加实用附效'],['稀有','约120%—130%','提高数值，或提供复制、重放、减费等能力']
    ])+prose('把附效计入同一份预算',list(['先估算同费用、同等待、同稀有度的纯伤收益，再为附效留出份额。','知识牌按整组获得方式、额外牌张和实际配合一起估值。','稀有牌已有强力减费或重放时，裸伤可以保持在普通档。']))+prose('本体例子 · 狂宴','<p>稀有，1费，消耗，造成10／12伤害；斩杀时增加3／4点最大生命。它的稀有预算主要用于永久成长。</p>')+table('1费 · 2回合 · 纯伤取整示例',['稀有度','非消耗','消耗'],Object.entries(examples).map(([rarity,value])=>[rarity,pair(value.normal),pair(value.exhaust)]))+'<p class="model-note">比例用于起算，表格为取整后的纸面建议。</p>';
  }
  function exhaust(){
    const {ordinary,examples}=modelExamples();
    return '<p class="model-intro">消耗补偿的是后续使用机会。箭矢等待越久，同一场战斗里再次抽到并完成的机会通常越少。</p>'+table('纯伤箭矢 · 消耗补偿',['实际平均等待','基础伤害增加','升级伤害增加'],[
      ['经常当回合完成','＋6','＋8'],['约1回合','＋5','＋7'],['约2回合','＋4','＋6'],['约3回合','＋2','＋3']
    ])+prose('按实际使用方式调整',list(['经常立即完成的箭矢，使用“当回合完成”档，即使卡面写着3回合。','等待期间可以反复额外触发的箭矢，减少消耗补偿。','消耗在整批完成后处理；本次重放与布置期间的额外触发仍会执行。','精简牌库与消耗联动本身也有收益，应从消耗补偿中扣除。']))+prose('一次计算','<p>普通、1费、2回合：'+pair(ordinary)+'伤害。加入消耗，增加4／6，得到<strong>'+pair(examples['普通'].exhaust)+'伤害</strong>。</p>')+prose('升级移除消耗','<p>本体「深谋远虑」和「彩虹」的升级只移除消耗。选择这条升级路线时，把恢复循环使用的收益计入升级预算，裸数值保持或小幅增加。</p>')+prose('其他效果',list(['循环抽牌与返能牌：消耗会限制后续展开，需要单独计算。','永久强化牌：主要收益在第一次使用，消耗还会精简牌库。','格挡牌：结合预计再次使用的次数，以及兑现回合的防御需求。']));
  }
  function effects(){
    return '<p class="model-intro">先确定纯数值预算，再计算附加效果与配合收益。</p>'+table('调整方向',['效果','怎样计入模型'],[
      ['群体伤害','每名敌人的伤害按同费用、同等待单体模型的80%起算，四舍五入。'],['多段伤害','降低裸伤，为每段都能获得的伤害加成留出预算。'],['抽牌','参考即时抽牌，并计入延迟补偿与完成时的手牌空间。'],['格挡','计入兑现回合的防御需求；可控加速提高兑现的可靠性。'],['获得能量','计算支付后的净收益，以及能否继续启动其他牌。'],['重放／额外触发','整份牌效都会受益；伤害、抽牌、返能分别计入。'],['知识','连同关联牌的获得方式、牌张负担和减费一起估值。'],['10回合等特殊载荷','按实际完成手段单独估值。']
    ])+prose('多段示例','<p>获得3点伤害加成时，单段增加3伤害，三段合计增加9伤害。多段牌的裸伤预算因此更低。</p>')+table('抽牌与格挡 · 起始参考',['效果','条件','基础／升级'],[
      ['抽牌箭矢','罕见，1费，1回合','抽4／5张'],['纯格挡箭矢','普通，1费，1回合','12／16格挡']
    ])+prose('返能参照','<p>本体「唤起」为普通、1费，下回合召唤2／3并获得2／3能量。返能箭矢要同时考虑布置费用、等待、加速成本和重复触发次数。</p>');
  }
  function native(){
    return '<p class="model-intro">本体数值来自本机安装版本 <strong>v0.111.0</strong>。箭矢建议值来自对这些卡牌的纸面估算。</p>'+table('伤害与蓄力',['卡牌','稀有度／费用','基础／升级效果'],[
      ['播种','普通 · 1费','对所有敌人造成8／11伤害，保留'],['匕首雨','普通 · 1费','对所有敌人造成4×2／6×2伤害'],['切割','普通 · 0费','立即造成6／9伤害'],['双重打击','普通 · 1费','立即造成5×2／7×2伤害'],['重锤','罕见 · 3费','立即造成32／42伤害'],['炸弹','罕见 · 2费','第三次己方回合结束，全体40／50伤害'],['漆黑','罕见 · 1费','生成黑暗球，触发全部黑暗球被动一次／两次']
    ])+prose('等待时点','<div class="model-table-scroll" tabindex="0" role="region" aria-label="等待时点"><table class="model-table"><thead><tr><th scope="col">牌</th><th scope="col">第1回合</th><th scope="col">第2回合</th><th scope="col">第3回合</th></tr></thead><tbody><tr><th scope="row">2回合箭矢</th><td>打出，计数2</td><td>开始时减至1</td><td><strong>开始时结算</strong></td></tr><tr><th scope="row">炸弹</th><td>打出，结束减至2</td><td>结束减至1</td><td><strong>结束时结算</strong></td></tr></tbody></table></div><p>两者都经过两次敌方行动，箭矢更早兑现。</p>')+prose('黑暗球的积累','<p>无集中时，基础「漆黑」生成的球从6累积到12，当回合结束到18，再过一次回合结束到24。兑现需要激发。</p>')+table('稀有度与消耗',['卡牌','稀有度／费用','基础／升级效果'],[
      ['快速检索','罕见 · 1费','抽3／4张'],['战略大师','稀有 · 0费 · 消耗','抽3／4张'],['深谋远虑','罕见 · 0费 · 消耗','抽2张，1张手牌放回牌堆顶；升级移除消耗'],['飞跃','普通 · 1费','9／12格挡'],['岿然不动','稀有 · 2费 · 消耗','30／40格挡'],['狂宴','稀有 · 1费 · 消耗','10／12伤害；斩杀增加3／4最大生命'],['唤起','普通 · 1费','下回合召唤2／3，获得2／3能量']
    ])+prose('怎样使用这些参照',list(['快速检索与战略大师体现了稀有度和消耗共同提供的1费差距。','深谋远虑说明，恢复循环使用可以占满一次升级。','炸弹提供延迟补偿的量级；范围伤害、加速与重复触发分别计价。']));
  }
  function cards(){
    const current=data(),models=waitModel(),modelId=vanillaModels()?.id||simulation?.id,byId=new Map((audit?.rows||[]).map(row=>[row.id,row]));
    const keys=['type','rarity','pool','cost','upgradedCost','turns','upgradedTurns','text','upgradedText'];
    const snapshot=c=>c?Object.fromEntries(keys.map(key=>[key,c[key]??null])):null;
    const links=id=>current.partnerGroups.filter(g=>g.mainCardId===id||g.grantedCardIds.includes(id)).map(g=>({main:g.mainCardId,members:[g.mainCardId,...g.grantedCardIds].map(key=>{const c=current.cards.find(c=>c.id===key);return {id:key,count:JunnaWorkbenchDomain.groupCardCount(g,key),scope:!c?'missing':c.trashedAt?'trash':c.reservedAt?'reserve':'active'};})}));
    const same=(a,b)=>JSON.stringify(a)===JSON.stringify(b),metadata=c=>({rules:c.design?.rules||'',notes:c.notes||'',keywords:c.keywords||[]});
    const entries=current.cards.filter(c=>!c.trashedAt).map(c=>{const row=byId.get(c.id),usesWaitModel=c.type==='箭矢'&&c.arrowSubtype==='攻击'&&models[c.cost]&&c.turns<=3,modelFresh=!modelId||!usesWaitModel||row?.waitModelId===modelId,fresh=modelFresh&&row&&row.status!=='已删除'&&same(snapshot(c),row.snapshot)&&same(links(c.id),row.groupSnapshot)&&same(metadata(c),row.metadata)&&(row.dependencies||[]).every(d=>same(snapshot(current.cards.find(c=>c.id===d.id)),d.snapshot));return {card:c,row,status:fresh?row.status:'待复核'};});
    const rank={'待复核':0,'需处理':1,'建议调整':2,'已校正':3,'保留初值':4};entries.sort((a,b)=>rank[a.status]-rank[b.status]||(a.row?.priority||3)-(b.row?.priority||3)||a.card.id.localeCompare(b.card.id));
    const counts=[['在用',entries.filter(e=>!e.card.reservedAt).length],['备用',entries.filter(e=>e.card.reservedAt).length],...Object.keys(rank).map(status=>[status,entries.filter(e=>e.status===status).length])].filter(([,n])=>n);
    const face=(c,upgraded)=>escape(upgraded?c.upgradedCost:c.cost)+'费'+(c.type==='箭矢'?' · '+escape(upgraded?c.upgradedTurns:c.turns)+'回合':'')+'<br>'+escape(upgraded?c.upgradedText:c.text);
    const rows=entries.map(({card:c,row,status})=>{let content=row&&row.status!=='已删除'?'<p>'+escape(row.basis)+'</p>'+(row.recommendation?'<p>'+escape(row.recommendation)+'</p>':''):'';if(status==='待复核'&&content)content='<details class="form-details"><summary>上次结论</summary>'+content+'</details>';return '<tr data-audit-card="'+escape(c.id)+'" data-audit-status="'+escape(status)+'"><th scope="row"><button type="button" class="text-button" data-open-card="'+escape(c.uid)+'">'+escape(c.name)+' ↗</button><span class="model-card-id">'+escape(c.rarity)+' · '+escape(c.id)+(c.reservedAt?' · 备用':'')+'</span></th><td>'+face(c,false)+'<br><span class="model-secondary">升级：'+face(c,true)+'</span></td><td><strong>'+escape(status)+'</strong>'+content+'</td></tr>';});
    const deleted=current.cards.filter(c=>c.trashedAt);
    return '<p class="model-intro">'+counts.map(([name,n])=>escape(name)+' '+n).join(' · ')+'</p><section class="model-section"><h2>全卡数值校验 · '+escape(audit?.date||'')+'</h2><div class="model-table-scroll" tabindex="0" role="region" aria-label="全卡数值校验"><table class="model-table model-card-table"><thead><tr><th scope="col">卡牌</th><th scope="col">当前牌面</th><th scope="col">结论与建议</th></tr></thead><tbody>'+rows.join('')+'</tbody></table></div></section><details class="form-details"><summary>已删除 · '+deleted.length+'</summary>'+deleted.map(c=>'<button type="button" class="text-button" data-open-card="'+escape(c.uid)+'">'+escape(c.id)+' · '+escape(c.name)+'</button>').join('、')+'</details>';
  }
  function workspace(){
    const isVanilla=selectedSection()==='vanilla';
    dom('page-title').textContent='卡牌模型';dom('result-count').textContent=isVanilla?filteredModels().length+'／'+(vanillaModels()?.cards.length||0)+'张 · 普通 · 非消耗':selectedSection()==='battle'?'历史模拟 · '+(simulation?.date||''):'纸面模型 · '+(vanillaModels()?.date||'2026-11-03');dom('page-actions').innerHTML=isVanilla?'<div class="model-version" role="group" aria-label="模型版本">'+[[false,'基础'],[true,'升级']].map(([value,label])=>'<button type="button" class="button small" data-model-version="'+value+'" aria-pressed="'+(Boolean(ui().modelUpgraded)===value)+'">'+label+'</button>').join('')+'</div>':'';dom('work-area').setAttribute('aria-labelledby','tab-models');
    dom('work-area').innerHTML='<article class="model-guide" aria-label="'+sections.find(([id])=>id===selectedSection())[1]+'">'+({quick,vanilla,battle,rarity,exhaust,effects,native,cards}[selectedSection()]())+'</article>';
    if(isVanilla)fitCards();
  }
  function inspector(){
    if(selectedSection()==='vanilla'){vanillaInspector();return;}
    const {ordinary,examples}=modelExamples();
    const rarity=examples[ui().modelRarity]?ui().modelRarity:'普通',consumed=Boolean(ui().modelExhaust),value=examples[rarity],result=consumed?value.exhaust:value.normal;
    const signed=n=>(n>=0?'＋':'−')+Math.abs(n),delta=[value.normal[0]-ordinary[0],value.normal[1]-ordinary[1]];
    dom('inspector').hidden=false;dom('inspector-title').textContent='模型示例';dom('close-inspector').hidden=true;dom('preview').innerHTML='';dom('preview').removeAttribute('tabindex');dom('preview').removeAttribute('aria-label');dom('inspector-footer').hidden=true;
    dom('editor-form').innerHTML='<div class="model-example"><p class="model-example-spec">1费 · 2回合 · 单体单段</p><label class="field"><span class="field-label">稀有度</span><select data-model-rarity>'+Object.keys(examples).map(key=>'<option'+(key===rarity?' selected':'')+'>'+key+'</option>').join('')+'</select></label><label class="check model-exhaust-check"><input type="checkbox" data-model-exhaust'+(consumed?' checked':'')+'>消耗</label><div class="model-example-result" aria-live="polite" aria-atomic="true"><dl class="model-results"><div><dt>基础伤害</dt><dd>'+result[0]+'</dd></div><div><dt>升级伤害</dt><dd>'+result[1]+'</dd></div></dl></div><h3>计算过程</h3><dl class="model-breakdown"><div><dt>普通基准</dt><dd>'+pair(ordinary)+'</dd></div><div><dt>稀有度分档</dt><dd>'+delta.map(signed).join('／')+'</dd></div><div><dt>消耗补偿</dt><dd>'+(consumed?'＋4／＋6':'0／0')+'</dd></div><div><dt>建议伤害</dt><dd>'+pair(result)+'</dd></div></dl><p class="model-note">分档后的取整示例。附加效果另计。</p></div>';
  }
  function sidebarClick(event){const filter=event.target.closest('[data-model-filter]');if(filter){ui()[filter.dataset.modelFilter]=filter.dataset.modelValue;const scroll=dom('sidebar').scrollTop;render();dom('work-area').scrollTop=0;dom('sidebar').scrollTop=scroll;dom('sidebar').querySelector('[data-model-filter="'+filter.dataset.modelFilter+'"][data-model-value="'+filter.dataset.modelValue+'"]')?.focus({preventScroll:true});return true;}const button=event.target.closest('[data-model-section]');if(!button)return false;ui().modelSection=button.dataset.modelSection;render();dom('work-area').scrollTop=0;dom('sidebar').querySelector('[data-model-section="'+ui().modelSection+'"]')?.focus({preventScroll:true});return true;}
  function workClick(event){const button=event.target.closest('[data-model-card],[data-model-version]');if(!button)return false;const scroll=dom('work-area').scrollTop,version=button.dataset.modelVersion;if(version!==undefined)ui().modelUpgraded=version==='true';else ui().modelSelected=button.dataset.modelCard;render();dom('work-area').scrollTop=scroll;const container=version!==undefined?'page-actions':'work-area',selector=version!==undefined?'[data-model-version="'+version+'"]':'[data-model-card="'+button.dataset.modelCard+'"]';dom(container).querySelector(selector)?.focus({preventScroll:true});return true;}
  function editorChange(event){const input=event.target;if(!input.matches('[data-model-rarity],[data-model-exhaust]'))return false;if(input.hasAttribute('data-model-rarity'))ui().modelRarity=input.value;else ui().modelExhaust=input.checked;const selector=input.hasAttribute('data-model-rarity')?'[data-model-rarity]':'[data-model-exhaust]';const scroll=dom('work-area').scrollTop;render();dom('work-area').scrollTop=scroll;dom('editor-form').querySelector(selector)?.focus({preventScroll:true});return true;}
  return {sidebar,workspace,inspector,sidebarClick,workClick,editorChange};
}
