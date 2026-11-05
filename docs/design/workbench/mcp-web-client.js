(function(root){
  'use strict';
  const clone=value=>JSON.parse(JSON.stringify(value));
  const same=(a,b)=>JSON.stringify(a)===JSON.stringify(b);
  function create({connection,clientId,uuid,getAssets,onSnapshot,onCommit,onStatus,onDraft}){
    let versions={},knownAssets=new Set(),outbox=[],revision=-1,ready=false,error=null,conflict=null,initialized=false,busy=null,rpcId=0;
    let snapshotRequest=0,editGeneration=0;
    const invalidateSnapshots=()=>{editGeneration++;};
    const status=()=>({ready,error,conflict,busy:!!busy,pending:outbox.length,revision});
    const draft=()=>({clientId,versions:clone(versions),outbox:clone(outbox)});
    const changed=()=>{onStatus(status());onDraft(draft());};
    async function rpc(method,params){const response=await fetch(connection.baseURL+'/mcp',{method:'POST',headers:{Authorization:'Bearer '+connection.token,'Content-Type':'application/json',Accept:'application/json, text/event-stream','MCP-Protocol-Version':'2025-03-26'},body:JSON.stringify({jsonrpc:'2.0',id:++rpcId,method,params}),signal:AbortSignal.timeout(connection.timeoutMs||20000)});if(!response.ok)throw Error('本地 MCP 未连接');const message=await response.json();if(message.error)throw Error(message.error.message);return message.result;}
    async function call(name,args={}){if(!initialized){await rpc('initialize',{protocolVersion:'2025-03-26',capabilities:{},clientInfo:{name:'junna-workbench-web',version:'1.0.0'}});initialized=true;}const result=await rpc('tools/call',{name,arguments:args}),value=result.structuredContent||JSON.parse(result.content[0].text);if(result.isError){const e=Error(value.message);Object.assign(e,value);throw e;}return value;}
    async function snapshot({initial=false}={}){
      const request=++snapshotRequest,generation=editGeneration;
      const stale=()=>request!==snapshotRequest||generation!==editGeneration;
      try{
        const result=await call('workbench_get_state',{includeHistory:true,includeAssets:initial,...(!initial?{knownRevision:revision}:{})});
        if(stale()||result.unchanged)return false;
        const missing=result.assetIds.filter(id=>!knownAssets.has(id));
        if(!initial&&missing.length){const assets={};for(let i=0;i<missing.length;i+=100)Object.assign(assets,(await call('workbench_get_assets',{ids:missing.slice(i,i+100)})).assets);result.bundle.assets=assets;}
        if(stale())return false;
        versions=result.versions;knownAssets=new Set(result.assetIds);revision=result.revision;onSnapshot(result.bundle);return true;
      }catch(e){if(stale())return false;throw e;}
    }
    async function start(recovered){try{await snapshot({initial:true});ready=true;error=null;if(recovered?.outbox?.length){versions=recovered.versions;outbox=recovered.outbox;for(const entry of outbox)for(const [id]of Object.entries(entry.assets||{}))knownAssets.delete(id);}changed();if(outbox.length)flush();return true;}catch(e){error=e;ready=false;changed();return false;}}
    function enqueue(record){invalidateSnapshots();const assets=Object.fromEntries(Object.entries(getAssets()).filter(([id])=>!knownAssets.has(id)));outbox.push({record:clone(record),assets});changed();flush();}
    async function drain(){while(outbox.length&&!conflict){const item=outbox[0];if(!item.payload)item.payload={requestId:item.record.id,clientId,label:item.record.label,kind:item.record.kind,reference:item.record.reference||null,changes:item.record.patches.map(p=>({collection:p.collection,uid:p.uid,before:clone(p.before),after:clone(p.after),...(Number.isInteger(p.afterIndex)?{afterIndex:p.afterIndex}:{}),expectedVersion:p.expectedVersion||versions[p.collection+'/'+p.uid]||'0:missing'})),assets:item.assets};changed();try{const result=await call('workbench_commit',item.payload);invalidateSnapshots();Object.assign(versions,result.versions);for(const id of result.assetIds)knownAssets.add(id);revision=-1;outbox.shift();ready=true;error=null;onCommit(item.record.id,result);changed();}catch(e){error=e;if(e.code&&e.code!=='STORE_BUSY'){conflict=e;ready=true;}else ready=false;changed();break;}}}
    function flush(){if(busy)return busy;if(conflict)return Promise.resolve(false);busy=drain().finally(()=>{busy=null;changed();});changed();return busy;}
    async function pull(){if(busy||outbox.length||conflict)return false;try{const updated=await snapshot();ready=true;error=null;changed();return updated;}catch(e){ready=false;error=e;changed();return false;}}
    async function proposals(){const map=new Map();for(const {record}of outbox)for(const p of record.patches){const key=p.collection+'/'+p.uid,old=map.get(key);map.set(key,{...p,before:old?old.before:p.before});}const list=[];for(const p of map.values()){const current=await call('workbench_get_entity',{collection:p.collection,uid:p.uid});let merged=clone(current.entity);if(current.entity===null||p.after===null||p.before===null||typeof p.after!=='object'||p.collection==='root')merged=same(current.entity,p.before)?clone(p.after):clone(current.entity);else{for(const field of new Set([...Object.keys(p.before),...Object.keys(p.after)]))if(!same(p.before[field],p.after[field])&&same(current.entity?.[field],p.before[field])){if(Object.hasOwn(p.after,field))merged[field]=clone(p.after[field]);else delete merged[field];}}list.push({...p,local:p.after,before:current.entity,after:merged,expectedVersion:current.version});}return list;}
    async function replace(changes){invalidateSnapshots();const backup=draft();onDraft(backup,true);outbox=[];conflict=null;error=null;await snapshot({initial:true});if(changes.length){const record={id:uuid(),label:'合并版本冲突',kind:'编辑',patches:changes};outbox.push({record,assets:Object.assign({},...backup.outbox.map(e=>e.assets||{}))});}changed();await flush();if(!outbox.length)await pull();}
    return {start,enqueue,flush,pull,proposals,replace,status,draft,call,invalidateSnapshots};
  }
  const api={create};if(typeof module==='object'&&module.exports)module.exports=api;else root.JunnaWorkbenchMcp=api;
})(typeof globalThis==='object'?globalThis:this);
