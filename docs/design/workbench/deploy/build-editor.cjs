'use strict';
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const [snapshotFile,publicDirectory,destination,configFile]=process.argv.slice(2);
if(!snapshotFile||!publicDirectory||!destination||!configFile)throw Error('Usage: node build-editor.cjs <MCP-export.json> <readonly-directory> <destination> <connection-config.json>');
const here=path.resolve(destination),source=process.env.JUNNA_DEPLOY_SOURCE||path.resolve(__dirname,'..');
const read=name=>fs.readFileSync(path.join(source,name),'utf8');
const json=file=>JSON.parse(fs.readFileSync(file,'utf8').replace(/^\uFEFF/,''));
const safe=value=>JSON.stringify(value).replace(/</g,'\\u003c').replace(/\u2028/g,'\\u2028').replace(/\u2029/g,'\\u2029');
const config=json(configFile);
if(new URL(config.origin).protocol!=='https:'||!/^[a-f0-9]{64}$/.test(config.secret))throw Error('Cloud connection configuration is invalid');
config.prefix=config.prefix||'/edit/'+config.secret;
const snapshot=json(snapshotFile);
const publicHTML=fs.readFileSync(path.join(publicDirectory,'index.html'),'utf8');
const assets=JSON.parse(publicHTML.match(/<script id="workbench-assets" type="application\/json">([\s\S]*?)<\/script>/)[1]);
for(const id of Object.keys(assets))assets[id]='/'+assets[id];
const state={...snapshot,ui:{...snapshot.ui,sort:'natural'},assets:Object.fromEntries(Object.keys(snapshot.assets).map(id=>[id,assets[id]])),history:[],undoStack:[],redoStack:[],seedBatches:[],appliedSeedBatches:[]};
delete state.mcpRequests;
const connection={baseURL:config.origin+config.prefix,token:config.secret,workspaceId:state.workspaceId,timeoutMs:60000,assetURLs:true};
let app=read('app.js');
const before="/^http:\\/\\/127\\.0\\.0\\.1:\\d+$/.test(c.baseURL)";
if(!app.includes(before))throw Error('Connection anchor missing');
app=app.replace(before,'c.baseURL===location.origin+location.pathname.replace(/\\/$/,\'\')');
let transport=read('mcp-web-client.js');
transport=transport.replace('knownAssets=new Set()','knownAssets=new Set(Object.keys(getAssets()))')
  .replace('includeAssets:initial','includeAssets:false').replace('if(!initial&&missing.length)','if(missing.length)');
const script=['arrow-focus.js','energy-wording.js','card-reference-wording.js','effect-statistics.js','route-statistics.js','route-statistics-view.js','pool-reference.js','card-model-view.js','fit-pool-view.js','relic-view.js','power-view.js','card-hover-tips.js','workbench-domain.js','mobile-layout.js'].map(read).concat(transport,app).join('\n');
new vm.Script(script);
const css=['workbench.css','card-model.css','fit-pool.css','relic.css','power.css','card-hover-tips.css','mobile.css'].map(read).join('\n').replaceAll('__REGULAR_FONT__',assets['game-regular']).replaceAll('__BOLD_FONT__',assets['game-bold']);
const inline=(id,value)=>'<script id="'+id+'" type="application/json">'+safe(value)+'</script>';
const optional=name=>{const file=path.join(source,'../data',name);return fs.existsSync(file)?json(file):null;};
let html=read('shell.html').replace('<!--STYLE-->','<style>'+css+'</style>')
  .replace('<!--CONNECTION-->',()=>inline('workbench-connection',connection))
  .replace('<!--ASSETS-->',()=>inline('workbench-assets',assets))
  .replace('<!--DATA-->',()=>'<script id="workbench-data" type="application/json" data-src="/snapshot.json">'+safe({ui:state.ui})+'</script>')
  .replace('<!--ROUTE_DATA-->',()=>inline('workbench-route-data',json(path.join(source,'route-statistics-defaults.json'))))
  .replace('<!--APP-->',()=>inline('workbench-battle-sim',optional('纯那箭矢等待补偿战斗模拟_20261103.json'))+inline('workbench-card-audit',optional('纯那全卡牌数值校验_20261103.json'))+'<script>'+script+'</script>');
fs.mkdirSync(here,{recursive:true});
html=require('./static-runtime.cjs').externalize(html,publicDirectory);
fs.writeFileSync(path.join(here,'editor.html'),html);
console.log(JSON.stringify({revision:snapshot.revision,editorBytes:Buffer.byteLength(html),assets:Object.keys(assets).length}));
