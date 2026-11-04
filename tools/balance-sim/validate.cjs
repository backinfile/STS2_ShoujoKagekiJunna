const fs=require('node:fs'),path=require('node:path');
const E=require('./engine.cjs');
const root=path.resolve(__dirname,'../../output/balance-sim/2026-11-03');
const pool=E.loadPool(path.join(root,'main-final/input-pool.json'));
const samples=64,avg=xs=>xs.reduce((a,b)=>a+b,0)/xs.length;
const curves=[{id:'fixed6u8',byCost:[[6,8],[6,8],[6,8]]},{id:'fixed3u4',byCost:[[3,4],[3,4],[3,4]]},{id:'byCost',byCost:[[2,3],[3,5],[5,7]]}];
const environments=[{id:'standard',energy:3,draw:5,shellUpgraded:false},{id:'energy4',energy:4,draw:5,shellUpgraded:false},{id:'draw7',energy:3,draw:7,shellUpgraded:false},{id:'upgradedShell',energy:3,draw:5,shellUpgraded:true}];
const rows=[],raw=[];
function deck(options,curve,reference=false) {
  const cards=E.buildDeck({...options,cost:1,bonus:0,immediate:reference},pool);
  let index=0;
  for(const card of cards)if(card.probe) {
    card.cost=[0,1,2,1,0][index++];
    card.damage=E.baseline(card.cost,options.upgraded)+(reference?0:curve.byCost[card.cost][options.upgraded?1:0]*options.wait);
    card.name='受测牌'+card.cost+'费';
  }
  return cards;
}
for(const env of environments)for(const context of ['starter','normal','batch','instant'])for(const upgraded of [false,true])for(const density of [2,5])for(const wait of [1,2,3])for(const encounter of E.config.encounters) {
  const options={context,upgraded,density,wait,shellUpgraded:env.shellUpgraded};
  const referenceDeck=deck(options,null,true);
  const reference=Array.from({length:samples},(_,i)=>E.fight(referenceDeck,encounter,E.hash(20261103,700,i),E.config.policies.balanced,env));
  for(const curve of curves) {
    const candidateDeck=deck(options,curve);
    const results=Array.from({length:samples},(_,i)=>E.fight(candidateDeck,encounter,E.hash(20261103,700,i),E.config.policies.balanced,env));
    const deltaLoss=results.map((r,i)=>r.loss-reference[i].loss),deltaTurns=results.map((r,i)=>r.turns-reference[i].turns);
    rows.push({environment:env.id,context,upgraded,density,wait,encounter:encounter.id,group:encounter.group,curve:curve.id,samples,
      winRate:avg(results.map(r=>Number(r.win))),referenceWinRate:avg(reference.map(r=>Number(r.win))),deltaLoss:avg(deltaLoss),deltaTurns:avg(deltaTurns),
      loss:avg(results.map(r=>r.loss)),referenceLoss:avg(reference.map(r=>r.loss))});
    for(let i=0;i<samples;i++)raw.push({environment:env.id,context,upgraded,density,wait,encounter:encounter.id,group:encounter.group,curve:curve.id,seedIndex:i,deltaLoss:deltaLoss[i],deltaTurns:deltaTurns[i],win:Number(results[i].win),referenceWin:Number(reference[i].win)});
  }
}
const summaries=[];
for(const env of environments)for(const curve of curves) {
  const values=rows.filter(row=>row.environment===env.id&&row.curve===curve.id&&row.context!=='instant'&&row.group==='calibration');
  const clusters=Array.from({length:samples},()=>[]);
  for(const row of raw)if(row.environment===env.id&&row.curve===curve.id&&row.context!=='instant'&&row.group==='calibration')clusters[row.seedIndex].push(row.deltaLoss);
  const means=clusters.map(avg),mean=avg(means),se=Math.sqrt(means.reduce((sum,x)=>sum+(x-mean)**2,0)/(samples-1)/samples);
  summaries.push({environment:env.id,curve:curve.id,battles:values.length*samples,deltaLoss:avg(values.map(r=>r.deltaLoss)),ci95:[mean-1.96*se,mean+1.96*se],deltaTurns:avg(values.map(r=>r.deltaTurns)),winRate:avg(values.map(r=>r.winRate))});
}
const report={version:1,sourceRevision:pool.revision,samples,totalBattles:rows.length*samples*(1+1/curves.length),curves,environments,summaries,rows};
fs.writeFileSync(path.join(root,'mixed-validation.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({done:true,battles:report.totalBattles,summaries}));
