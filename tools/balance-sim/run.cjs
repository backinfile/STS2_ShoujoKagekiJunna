const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const E=require('./engine.cjs');
const args=Object.fromEntries(process.argv.slice(2).map(arg=>{const [key,...value]=arg.replace(/^--/,'').split('=');return [key,value.join('=')||true];}));
const number=(key,fallback)=>args[key]===undefined?fallback:Number(args[key]);
const filter=(entries,key)=>args[key]?entries.filter(entry=>String(args[key]).split(',').includes(String(entry.id??entry))):entries;
const samples=number('samples',32),policies=filter(Object.entries(E.config.policies).map(([id,value])=>({id,...value})),'policies');
const policyList=args.policies?policies:policies.filter(policy=>policy.id==='balanced');
const contexts=filter(E.config.contexts,'contexts'),encounters=filter(E.config.encounters,'encounters');
const costs=filter([0,1,2],'costs'),waits=filter([1,2,3],'waits'),densities=filter([2,5],'densities');
const versions=filter([{id:'base',upgraded:false},{id:'upgraded',upgraded:true}],'versions');
const candidates=filter(E.config.candidates,'candidates');
const out=path.resolve(String(args.out||'output/balance-sim/2026-11-03'));
const pool=E.loadPool(args.pool?path.resolve(String(args.pool)):undefined),started=Date.now(),cache=new Map(),raw=[],comparisons=[];
fs.mkdirSync(out,{recursive:true});
fs.writeFileSync(path.join(out,'input-pool.json'),JSON.stringify(pool.snapshot,null,2)+'\n');
const avg=xs=>xs.reduce((a,b)=>a+b,0)/xs.length;
function stats(xs) {const mean=avg(xs),variance=xs.length>1?xs.reduce((sum,x)=>sum+(x-mean)**2,0)/(xs.length-1):0;return {mean,se:Math.sqrt(variance/xs.length),ci95:[mean-1.96*Math.sqrt(variance/xs.length),mean+1.96*Math.sqrt(variance/xs.length)]};}
const round=x=>Math.round(x*10000)/10000;
function csv(file,rows) {if(!rows.length)return;const keys=Object.keys(rows[0]);fs.writeFileSync(file,'\uFEFF'+[keys,...rows.map(row=>keys.map(key=>row[key]))].map(row=>row.map(v=>'"'+String(v??'').replaceAll('"','""')+'"').join(',')).join('\n')+'\n');}
function resultMean(results) {return {winRate:avg(results.map(r=>Number(r.win))),loss:avg(results.map(r=>r.loss)),turns:avg(results.map(r=>r.turns)),penalty:avg(results.map(r=>r.penalty)),probePlays:avg(results.map(r=>r.probePlays)),probeDamage:avg(results.map(r=>r.probeDamage)),probeWaste:avg(results.map(r=>r.probeWaste)),probeAcceleration:avg(results.map(r=>r.probeAcceleration)),drawn:avg(results.map(r=>r.cardsDrawn))};}
const seedAt=i=>E.hash(20261103,101,i);
function simulate(options,encounter,policy,reference=false) {
  const deck=E.buildDeck({...options,immediate:reference},pool),results=[];
  for(let index=0;index<samples;index++) {
    const seed=seedAt(index),r=E.fight(deck,encounter,seed,policy,{energy:number('energy',3),draw:number('draw',5)});
    results.push(r);
    raw.push({policy:policy.id,context:options.context,encounter:encounter.id,version:options.upgraded?'upgraded':'base',density:options.density,cost:options.cost,wait:reference?0:options.wait,candidate:reference?'immediate':options.candidate,seedIndex:index,seed,win:Number(r.win),loss:r.loss,turns:r.turns,penalty:r.penalty,probePlays:r.probePlays,probeDamage:r.probeDamage,probeWaste:r.probeWaste,probeAcceleration:r.probeAcceleration,drawn:r.cardsDrawn});
  }
  return results;
}
for(const policy of policyList)for(const context of contexts) {
  for(const density of densities)for(const version of versions)for(const cost of costs)for(const encounter of encounters) {
    const key=[policy.id,context.id,density,version.id,cost,encounter.id].join('/');
    const options={cost,upgraded:version.upgraded,shellUpgraded:Boolean(args['shell-upgraded']),context:context.id,density};
    const reference=simulate(options,encounter,policy,true);cache.set(key,reference);
    for(const wait of waits)for(const candidate of candidates) {
      const bonus=version.upgraded?candidate.upgraded:candidate.base;
      const results=simulate({...options,wait,bonus,candidate:candidate.id},encounter,policy);
      const delta=results.map((r,index)=>r.penalty-reference[index].penalty),mean=resultMean(results),ref=resultMean(reference);
      comparisons.push({policy:policy.id,context:context.id,contextGroup:context.group,encounter:encounter.id,encounterGroup:encounter.group,version:version.id,density,cost,wait,candidate:candidate.id,bonus,samples,
        damage:E.baseline(cost,version.upgraded)+bonus*wait,...Object.fromEntries(Object.entries(mean).map(([k,v])=>[k,round(v)])),referenceWinRate:round(ref.winRate),referenceLoss:round(ref.loss),referenceTurns:round(ref.turns),deltaPenalty:round(avg(delta)),deltaLoss:round(avg(results.map((r,i)=>r.loss-reference[i].loss))),deltaTurns:round(avg(results.map((r,i)=>r.turns-reference[i].turns)))});
    }
  }
  process.stdout.write(JSON.stringify({progress:policy.id+'/'+context.id,battles:raw.length,seconds:round((Date.now()-started)/1000)})+'\n');
}
// Cluster by random seed, so reusing a seed across encounters does not inflate confidence.
const groups=new Map();
for(const row of raw)if(row.candidate!=='immediate') {
  const candidate=E.config.candidates.find(c=>c.id===row.candidate),context=E.config.contexts.find(c=>c.id===row.context),encounter=E.config.encounters.find(e=>e.id===row.encounter);
  const ref=cache.get([row.policy,row.context,row.density,row.version,row.cost,row.encounter].join('/'))[row.seedIndex];
  const key=[row.policy,row.context,row.version,row.candidate].join('/');
  if(!groups.has(key))groups.set(key,{policy:row.policy,context:row.context,version:row.version,candidate:row.candidate,bonus:row.version==='base'?candidate.base:candidate.upgraded,cells:0,seedDeltas:Array.from({length:samples},()=>[]),wins:[],refWins:[],turns:[],losses:[],refLosses:[]});
  const group=groups.get(key);
  if(context.group!=='calibration'||encounter.group!=='calibration')continue;
  group.seedDeltas[row.seedIndex].push(row.penalty-ref.penalty);group.wins.push(row.win);group.refWins.push(Number(ref.win));group.turns.push(row.turns-ref.turns);group.losses.push(row.loss);group.refLosses.push(ref.loss);
}
const summaries=[...groups.values()].filter(group=>group.wins.length).map(group=>({policy:group.policy,context:group.context,version:group.version,candidate:group.candidate,bonus:group.bonus,battles:group.wins.length,deltaPenalty:stats(group.seedDeltas.filter(values=>values.length).map(avg)),winRate:avg(group.wins),referenceWinRate:avg(group.refWins),loss:avg(group.losses),referenceLoss:avg(group.refLosses),deltaTurns:avg(group.turns)}));
const nativeSources=E.config.sourceFiles.map(file=>{const absolute=path.join(E.config.sourceRoot,file);return {file:absolute,sha256:crypto.createHash('sha256').update(fs.readFileSync(absolute)).digest('hex')};});
const report={version:1,createdAt:new Date().toISOString(),sourceRevision:pool.revision,samplesPerCell:samples,totalBattles:raw.length,durationSeconds:round((Date.now()-started)/1000),
  parameters:{energy:number('energy',3),draw:number('draw',5),shellUpgraded:Boolean(args['shell-upgraded']),costs,waits,densities,versions:versions.map(v=>v.id),policies:policyList.map(p=>p.id),contexts:contexts.map(c=>c.id),encounters:encounters.map(e=>e.id),candidates},
  sources:nativeSources,cardSnapshot:pool.snapshot,summary:summaries,comparisons};
fs.writeFileSync(path.join(out,'results.json'),JSON.stringify(report,null,2)+'\n');
csv(path.join(out,'battles.csv'),raw);csv(path.join(out,'comparisons.csv'),comparisons);
const exampleOptions={cost:0,wait:2,bonus:3,context:'normal',density:2,upgraded:false};
const trace=E.fight(E.buildDeck(exampleOptions,pool),E.config.encounters[0],seedAt(0),E.config.policies.balanced,{trace:true});
fs.writeFileSync(path.join(out,'example-trace.json'),JSON.stringify({seed:seedAt(0),options:exampleOptions,...trace},null,2)+'\n');
console.log(JSON.stringify({done:true,battles:raw.length,seconds:report.durationSeconds,output:out}));
