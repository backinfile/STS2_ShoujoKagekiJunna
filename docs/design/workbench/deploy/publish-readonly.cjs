'use strict';
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),zlib=require('node:zlib');
const template=fs.readFileSync(path.join(__dirname,'public-template.html'),'utf8');
const parse=id=>JSON.parse(template.match(new RegExp('<script id="'+id+'" type="application/json">([\\s\\S]*?)<\\/script>'))[1]);
const baseAssets=parse('workbench-assets'),baseState=parse('workbench-data');
const safe=value=>JSON.stringify(value).replace(/</g,'\\u003c').replace(/\u2028/g,'\\u2028').replace(/\u2029/g,'\\u2029');
const web='/var/www/junna',shared=path.join(web,'assets');
let publishedRevision=null;
function publish(){
  const state=JSON.parse(fs.readFileSync('/var/lib/junna-workbench/state.json','utf8'));
  if(state.revision===publishedRevision)return;
  const exportedAt=new Date().toISOString(),assets={...baseAssets};
  for(const [id,value]of Object.entries(state.assets||{})){
    const match=/^data:([^;,]+);base64,([\s\S]+)$/.exec(value);if(!match)throw Error('Unsupported asset');
    const bytes=Buffer.from(match[2],'base64'),ext={'image/png':'png','image/jpeg':'jpg','image/webp':'webp','image/gif':'gif','image/svg+xml':'svg'}[match[1]]||'bin';
    const name=crypto.createHash('sha256').update(bytes).digest('hex').slice(0,24)+'.'+ext;
    const file=path.join(shared,name);if(!fs.existsSync(file))fs.writeFileSync(file,bytes,{flag:'wx'});assets[id]='assets/'+name;
  }
  const bundle={...baseState,workspaceId:state.workspaceId+':readonly:'+state.revision,data:state.data,revision:state.revision,exportedAt};
  let html=template;
  for(const [id,value]of [['workbench-assets',assets],['workbench-data',bundle]])html=html.replace(new RegExp('(<script id="'+id+'" type="application/json">)[\\s\\S]*?(<\\/script>)'),()=>'<script id="'+id+'" type="application/json">'+safe(value)+'</script>');
  html=html.replace(/只读 · \d{4}-\d{2}-\d{2}/,'只读 · '+new Intl.DateTimeFormat('sv-SE',{timeZone:'Asia/Shanghai',dateStyle:'short'}).format(new Date(exportedAt)));
  const dir=path.join(web,'releases','live-r'+state.revision+'-'+Date.now());fs.mkdirSync(dir);
  fs.symlinkSync(shared,path.join(dir,'assets'));
  fs.writeFileSync(path.join(dir,'index.html'),html);fs.writeFileSync(path.join(dir,'index.html.gz'),zlib.gzipSync(html,{level:6}));
  const manifest={revision:state.revision,exportedAt,counts:Object.fromEntries(Object.entries(state.data).filter(([,v])=>Array.isArray(v)).map(([k,v])=>[k,v.length])),assets:new Set(Object.values(assets)).size,htmlBytes:Buffer.byteLength(html),sha256:crypto.createHash('sha256').update(html).digest('hex')};
  fs.writeFileSync(path.join(dir,'manifest.json'),JSON.stringify(manifest,null,2)+'\n');
  const next=path.join(web,'current.pending');fs.symlinkSync(dir,next);fs.renameSync(next,path.join(web,'current'));
  publishedRevision=state.revision;
  console.log('Published read-only r'+state.revision);
}
let timer;
function start(){publish();fs.watch('/var/lib/junna-workbench',(_,name)=>{if(name==='state.json'){clearTimeout(timer);timer=setTimeout(()=>{try{publish();}catch(error){console.error('Read-only publish failed:',error.message);}},800);}});}
module.exports={publish,start};
