'use strict';
const fs=require('node:fs');
const path=require('node:path');
const {Server}=require('@modelcontextprotocol/sdk/server/index.js');
const {ListToolsRequestSchema,CallToolRequestSchema}=require('@modelcontextprotocol/sdk/types.js');
const {ENTITY_COLLECTIONS:COLLECTIONS,entityList}=require('./workbench-domain.js');
const {WorkbenchError}=require('./mcp-store.cjs');
const string={type:'string'},boolean={type:'boolean'},integer={type:'integer'};
const object=(properties,required=[])=>({type:'object',properties,required,additionalProperties:false});
const collection={type:'string',enum:[...COLLECTIONS,'root']};
const change=object({collection,uid:string,expectedVersion:string,before:{},after:{},updates:{type:'object'},unset:{type:'array',items:string},afterIndex:integer},['collection','uid','expectedVersion']);
const definitions=[
  ['workbench_status','查看工作台地址、连接状态和条目数量。',object({}),true],
  ['workbench_query','查找条目，返回每条内容及其版本。编辑前使用 get_entity 读取完整内容。',object({collection,search:string,limit:integer,offset:integer,fields:{type:'array',items:string},includeArchived:boolean}),true],
  ['workbench_get_entity','按 uid 或 id 读取完整条目和版本；新条目 uid 返回 missing 版本。',object({collection,uid:string,id:string},['collection']),true],
  ['workbench_get_state','读取完整工作台；网页同步使用。工具日常编辑优先 query/get_entity。',object({includeAssets:boolean,includeHistory:boolean,knownRevision:integer}),true],
  ['workbench_get_assets','读取指定图片。',object({ids:{type:'array',items:string}},['ids']),true],
  ['workbench_history','查询修改记录，可读取 before/after 用于版本校验后的恢复。',object({search:string,limit:integer,offset:integer,includePatches:boolean}),true],
  ['workbench_commit','原子提交多个条目。每条必须提供读取时的 expectedVersion。updates 仅替换指定字段；整体替换提供 before/after。版本冲突整批返回 VERSION_CONFLICT 和当前内容；读取后人工合并，再用新的 requestId 提交。',object({requestId:string,clientId:string,label:string,kind:string,reference:{type:['string','null']},changes:{type:'array',items:change},assets:{type:'object',additionalProperties:string}},['requestId','clientId','label','changes']),false],
  ['workbench_restore','撤销或恢复一条历史记录。提供每个受影响条目的当前版本；内容与该历史端点不一致时保留现状并返回冲突。',object({requestId:string,clientId:string,recordId:string,side:{type:'string',enum:['before','after']},expectedVersions:{type:'object',additionalProperties:string}},['requestId','clientId','recordId','side','expectedVersions']),false],
  ['workbench_import_asset','从本地图片导入不可变资产，可同时替换一张卡牌或契合条目的配图。替换时提供条目版本。',object({requestId:string,clientId:string,label:string,assetId:string,sourcePath:string,collection,uid:string,expectedVersion:string},['requestId','clientId','label','assetId','sourcePath']),false],
  ['workbench_export','导出当前数据或游戏用知识背景提示。返回数据；outputPath 可保存副本。',object({format:{type:'string',enum:['bundle','knowledgeTips']},outputPath:string}),false]
].map(([name,description,inputSchema,readOnlyHint])=>({name,description,inputSchema,annotations:{readOnlyHint,destructiveHint:false,idempotentHint:true,openWorldHint:false}}));
function createToolHandlers(store,{url}){
  return {
    workbench_status:async()=>{const b=store.read();return {connected:true,serverType:'junna-workbench-mcp',url,workspaceId:b.workspaceId,revision:b.revision,counts:Object.fromEntries(COLLECTIONS.map(k=>[k,entityList(b.data,k).length]))};},
    workbench_query:async args=>store.query(args),
    workbench_get_entity:async args=>store.get(args),
    workbench_get_state:async args=>store.state(args),
    workbench_get_assets:async({ids})=>{if(!Array.isArray(ids)||ids.length>100)throw new WorkbenchError('INVALID_ARGUMENT','图片数量无效');const b=store.read();return {assets:Object.fromEntries(ids.filter(id=>Object.hasOwn(b.assets,id)).map(id=>[id,b.assets[id]]))};},
    workbench_history:async args=>store.history(args),
    workbench_commit:async args=>store.commit(args),
    workbench_restore:async({recordId,side,expectedVersions,...args})=>store.commit({...args,label:(side==='before'?'撤销':'重做')+' · '+recordId,kind:side==='before'?'撤销':'重做',reference:recordId,restore:{recordId,side,expectedVersions}}),
    workbench_import_asset:async({sourcePath,assetId,collection,uid,expectedVersion,...args})=>{const ext=path.extname(sourcePath).toLowerCase(),mime={'.png':'png','.jpg':'jpeg','.jpeg':'jpeg','.webp':'webp','.gif':'gif'}[ext];if(!mime||fs.statSync(sourcePath).size>16*1024*1024)throw new WorkbenchError('INVALID_ARGUMENT','请选择 16MB 以内的 PNG、JPEG、WebP 或 GIF');const value='data:image/'+mime+';base64,'+fs.readFileSync(sourcePath).toString('base64');return store.commit({...args,changes:uid?[{collection,uid,expectedVersion,updates:{art:assetId}}]:[],assets:{[assetId]:value}});},
    workbench_export:async({format='bundle',outputPath}={})=>{const b=store.read();let value;if(format==='knowledgeTips')value={schemaVersion:1,locale:'zh-CN',tipId:'junna_knowledge_background',cards:Object.fromEntries(b.data.cards.filter(c=>c.extraHoverTips?.some(t=>t.id==='junna_knowledge_background')).map(c=>[c.id,{name:c.name,extraHoverTips:c.extraHoverTips}]))};else{const {mcpRequests,...bundle}=b;value=bundle;}if(outputPath){fs.writeFileSync(path.resolve(outputPath),JSON.stringify(value,null,2)+'\n');return {saved:true,path:path.resolve(outputPath),revision:b.revision};}return {data:value,revision:b.revision};}
  };
}
function createProtocolServer(handlers){
  const server=new Server({name:'junna-workbench',version:'1.0.0'},{capabilities:{tools:{}},instructions:'纯那设计工作台内容统一通过此 MCP 编辑。先 query/get_entity 读取条目及 version，再 commit 并提供 expectedVersion、唯一 requestId 和 clientId。不同条目可并行，同条目版本冲突整批拒绝并返回当前内容。保留草稿，比较后合并；勿直接改写本地编辑 JSON、seed 或调用旧 /file 接口。网页与 MCP 共享数据、版本和历史。'});
  server.setRequestHandler(ListToolsRequestSchema,async()=>({tools:definitions}));
  server.setRequestHandler(CallToolRequestSchema,async request=>{try{const handler=handlers[request.params.name];if(!handler)throw new WorkbenchError('NOT_FOUND','工具不存在');const value=await handler(request.params.arguments||{});return {content:[{type:'text',text:['workbench_get_state','workbench_get_assets','workbench_export'].includes(request.params.name)?JSON.stringify({ok:true,revision:value.revision,unchanged:value.unchanged,saved:value.saved,path:value.path}):JSON.stringify(value)}],structuredContent:value};}catch(error){const value={ok:false,code:error.code||'INTERNAL_ERROR',message:error.message,...error.details};return {isError:true,content:[{type:'text',text:JSON.stringify(value)}],structuredContent:value};}});
  return server;
}
module.exports={definitions,createToolHandlers,createProtocolServer};
