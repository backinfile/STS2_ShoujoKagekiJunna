const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const E=require('./engine.cjs');
const root=path.resolve(__dirname,'../..'),folder=path.join(root,'output/balance-sim/2026-11-03');
const read=file=>JSON.parse(fs.readFileSync(file,'utf8'));
const main=read(path.join(folder,'main-final/results.json')),policies=read(path.join(folder,'policies/results.json'));
const refinement=read(path.join(folder,'refinement/results.json')),mixed=read(path.join(folder,'mixed-validation.json'));
const avg=xs=>xs.reduce((a,b)=>a+b,0)/xs.length;
const fixedComparison=main.parameters.candidates.map(candidate=>{
  const groups=main.summary.filter(s=>s.candidate===candidate.id&&['normal','batch'].includes(s.context));
  const version=id=>{const values=groups.filter(s=>s.version===id);return {loss:avg(values.map(s=>s.loss-s.referenceLoss)),turns:avg(values.map(s=>s.deltaTurns))};};
  return {id:candidate.id,base:candidate.base,upgraded:candidate.upgraded,baseResult:version('base'),upgradedResult:version('upgraded')};
});
const recommendation={
  id:'junna-arrow-wait-sim-20261103-v1',
  byCost:{'0':{base:6,upgraded:9,waitBase:2,waitUpgraded:3},'1':{base:10,upgraded:14,waitBase:3,waitUpgraded:5},'2':{base:20,upgraded:28,waitBase:5,waitUpgraded:7}},
  scope:'普通、非消耗、单体单段纯伤；费用0—2，等待0—3回合。',
  rationale:'按费用区分等待的机会成本；0费采用偏保守初值，较高费用保留更多等待补偿。'
};
const totalBattles=main.totalBattles+policies.totalBattles+refinement.totalBattles+mixed.totalBattles;
const pressureComparison=mixed.curves.map(curve=>{
  const rows=mixed.rows.filter(row=>row.environment==='standard'&&row.curve===curve.id&&row.encounter==='sculptor'&&row.context!=='instant');
  return {curve:curve.id,winRate:avg(rows.map(row=>row.winRate)),referenceWinRate:avg(rows.map(row=>row.referenceWinRate))};
});
const report={
  schemaVersion:1,id:recommendation.id,date:'2026-11-03',sourceRevision:main.sourceRevision,totalBattles,
  recommendation,fixedComparison,mixedComparison:mixed.summaries,pressureComparison,
  checks:{ruleCases:9,zeroTurnEquivalence:true,instanceConservation:true,noFutureDrawLeak:true,terminalKillPreference:true},
  runs:[
    {name:'正式比较',battles:main.totalBattles,path:'output/balance-sim/2026-11-03/main-final/results.json'},
    {name:'出牌策略与速射',battles:policies.totalBattles,path:'output/balance-sim/2026-11-03/policies/results.json'},
    {name:'1、2费细化',battles:refinement.totalBattles,path:'output/balance-sim/2026-11-03/refinement/results.json'},
    {name:'新种子混合费用验证',battles:mixed.totalBattles,path:'output/balance-sim/2026-11-03/mixed-validation.json'}
  ],
  setup:{player:E.config.player,deckSize:17,probeCounts:[2,5],mainSeeds:main.samplesPerCell,validationSeeds:mixed.samples,
    baseDeckFixed:true,onlyProbeUpgraded:true,policy:'当前手牌两步搜索；抽牌前只估计抽牌收益，揭示后重新决策。',
    rules:['能量正常支付，回合开始刷新。','箭矢按自己的回合开始递减，结算前离开正常抽弃牌循环。','单体加速、同目标加速、群体加速、速射按当前工作台快照处理。','箭矢锁定目标，目标死亡后该伤害落空；记录溢出伤害。','保留牌占用真实手牌，抽牌上限为10。'],
    metric:'同费用即时攻击为参照，承伤差与击杀回合差分别比较。压力场景单列。',
    scope:['伤害','格挡','抽牌','保留','消耗工具','箭矢等待','加速','速射'],
    extensions:['知识组获得与连锁','伤害加成成长','额外触发与重放','遗物、药水及多人结算']
  },
  encounters:E.config.encounters,sources:main.sources,
  programs:['engine.cjs','run.cjs','validate.cjs','scenarios.json'].map(file=>({file:'tools/balance-sim/'+file,sha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(__dirname,file))).digest('hex')}))
};
const reportPath=path.join(root,'docs/design/data/纯那箭矢等待补偿战斗模拟_20261103.json');
fs.writeFileSync(reportPath,JSON.stringify(report,null,2)+'\n');
const auditPath=path.join(root,'docs/design/data/纯那全卡牌数值校验_20261103.json'),audit=read(auditPath);
audit.policy.model='普通非消耗、单体单段纯伤：0费即时6／9，每回合+2／3；1费即时10／14，每回合+3／5；2费即时20／28，每回合+5／7。适用等待0—3回合。';
audit.policy.waitModelId=recommendation.id;
audit.policy.waitModelSource=path.basename(reportPath);
fs.writeFileSync(auditPath,JSON.stringify(audit,null,2)+'\n');
console.log(JSON.stringify({report:reportPath,totalBattles,recommendation,mixed:mixed.summaries}));
