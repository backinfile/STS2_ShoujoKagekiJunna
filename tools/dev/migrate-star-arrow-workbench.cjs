'use strict';
const assert=require('node:assert/strict');
const crypto=require('node:crypto');
const fs=require('node:fs');
const {createRequire}=require('node:module');
const workbenchRequire=createRequire(require.resolve('../../docs/design/workbench/mcp-client.cjs'));
const {Client}=workbenchRequire('@modelcontextprotocol/sdk/client/index.js');
const {StreamableHTTPClientTransport}=workbenchRequire('@modelcontextprotocol/sdk/client/streamableHttp.js');
const old='\u7bad\u77e2',word='星矢';
const collections=['mechanisms','terms','cards','partnerGroups','archetypes','styles','fitPool','powers','relics'];
const contains=value=>typeof value==='string'?value.includes(old):Array.isArray(value)?value.some(contains):value&&typeof value==='object'?Object.entries(value).some(([key,value])=>key.includes(old)||contains(value)):false;
const rename=value=>typeof value==='string'?value.replaceAll(old,word):Array.isArray(value)?value.map(rename):value&&typeof value==='object'?Object.fromEntries(Object.entries(value).map(([key,value])=>[key.replaceAll(old,word),rename(value)])):value;
const rootMeta=meta=>Object.fromEntries(Object.entries(meta).filter(([key])=>!['powers','relics'].includes(key)));
async function main(){
 const option=name=>{const index=process.argv.indexOf(name);return index<0?undefined:process.argv[index+1]};
 const url=option('--url')||process.env.JUNNA_WORKBENCH_MCP_URL;
 const token=option('--token-file')?fs.readFileSync(option('--token-file'),'utf8').trim():process.env.JUNNA_WORKBENCH_MCP_TOKEN;
 if(!url||!token)throw Error('请提供 --url（或 JUNNA_WORKBENCH_MCP_URL）以及 JUNNA_WORKBENCH_MCP_TOKEN（或 --token-file）。');
 const client=new Client({name:'junna-star-arrow-migration',version:'1.0.0'});
 await client.connect(new StreamableHTTPClientTransport(new URL(url),{requestInit:{headers:{Authorization:'Bearer '+token}}}));
 try{
  const call=async(name,args={})=>{const result=await client.callTool({name,arguments:args});const value=result.structuredContent||JSON.parse(result.content[0].text);if(result.isError)throw Object.assign(Error(value.message),value);return value};
  const before=await call('workbench_get_state',{includeAssets:false,includeHistory:false}),data=before.bundle.data;
  if(data.cards.some(card=>card.type===old))throw Error('请先重启此工作台的 MCP 服务，加载新版类型校验。');
  const targets=[];for(const collection of collections)for(const entity of (['powers','relics'].includes(collection)?data.meta?.[collection]:data[collection])||[])if(contains(entity))targets.push({collection,uid:entity.uid});
  for(const uid of ['title','meta'])if(contains(uid==='meta'?rootMeta(data.meta):data.title))targets.push({collection:'root',uid});
  const changes=[];for(const target of targets){const read=await call('workbench_get_entity',target);const entity=target.collection==='root'&&target.uid==='meta'?rootMeta(read.entity):read.entity;changes.push({...target,expectedVersion:read.version,before:entity,after:rename(entity)});if(changes.length%100===0)process.stdout.write('已读取 '+changes.length+'/'+targets.length+' 个条目\n');}
  const saved=await call('workbench_commit',{requestId:crypto.randomUUID(),clientId:'star-arrow-wording',label:'统一星矢术语',kind:'术语统一',changes});
  const after=await call('workbench_get_state',{includeAssets:false,includeHistory:false});
  for(const change of changes){
    const key=change.collection+'/'+change.uid;
    if(after.versions[key]!==saved.versions[key])continue; // A later edit keeps its own version.
    const current=change.collection==='root'?(change.uid==='meta'?rootMeta(after.bundle.data.meta):after.bundle.data[change.uid]):(change.collection==='powers'||change.collection==='relics'?after.bundle.data.meta[change.collection]:after.bundle.data[change.collection]).find(entity=>entity.uid===change.uid);
    assert.deepEqual(current,change.after,'已提交条目应仅替换术语：'+key);
  }
  assert.equal(contains(after.bundle.data),false,'当前内容仍有旧术语，请基于最新版本重试。');
  console.log(JSON.stringify({revision:saved.revision,changed:changes.length,cards:after.bundle.data.cards.length,starCards:after.bundle.data.cards.filter(card=>card.type===word).length}));
 }finally{await client.close()}
}
main().catch(error=>{console.error(JSON.stringify({message:error.message,code:error.code,conflicts:error.conflicts}));process.exitCode=1});