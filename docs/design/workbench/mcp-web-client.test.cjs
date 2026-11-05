'use strict';
const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const os=require('node:os');
const path=require('node:path');
const {randomUUID}=require('node:crypto');
const {WorkbenchStore}=require('./mcp-store.cjs');
const web=require('./mcp-web-client.js');

function fixture(t){
  const dir=fs.mkdtempSync(path.join(os.tmpdir(),'junna-web-race-')),file=path.join(dir,'state.json');
  const seed={schemaVersion:1,workspaceId:'race-test',revision:1,history:[],assets:{},data:{title:'测试',meta:{},cards:[{uid:'a',id:'A',name:'原名',text:'原文',cost:1,type:'技能'}],terms:[],mechanisms:[],partnerGroups:[],archetypes:[],styles:[],fitPool:[]}};
  fs.writeFileSync(file,JSON.stringify(seed));t.after(()=>fs.rmSync(dir,{recursive:true,force:true}));
  const store=new WorkbenchStore({file,seed,workspaceId:seed.workspaceId});
  let hold=null,bundle;
  const originalFetch=global.fetch;
  global.fetch=async(_url,options)=>{
    const request=JSON.parse(options.body);
    if(request.method==='initialize')return {ok:true,json:async()=>({result:{}})};
    const {name,arguments:args}=request.params;
    try{
      let value;
      if(name==='workbench_get_state')value=store.state(args);
      else if(name==='workbench_get_entity')value=store.get(args);
      else if(name==='workbench_commit')value=await store.commit(args);
      else if(name==='workbench_get_assets')value={assets:{}};
      else throw Error(name);
      if(name==='workbench_get_state'&&hold){const gate=hold;hold=null;gate.captured();await gate.wait;}
      return {ok:true,json:async()=>({result:{structuredContent:value}})};
    }catch(error){return {ok:true,json:async()=>({result:{isError:true,structuredContent:{code:error.code,message:error.message,...error.details}}})};}
  };
  t.after(()=>{global.fetch=originalFetch;});
  const controller=web.create({connection:{baseURL:'http://127.0.0.1:1',token:'test'},clientId:'web',uuid:randomUUID,getAssets:()=>bundle.assets,onSnapshot:value=>{bundle=value;},onCommit:()=>{},onStatus:()=>{},onDraft:()=>{}});
  async function remote(collection,uid,updates){const current=store.get({collection,uid});return store.commit({requestId:randomUUID(),clientId:'other',label:'其他条目修改',changes:[{collection,uid,expectedVersion:current.version,before:current.entity,after:{...current.entity,...updates}}]});}
  async function edit(updates,base){const before=base||structuredClone(bundle.data.cards[0]),after={...before,...updates};bundle.data.cards[0]=after;controller.enqueue({id:randomUUID(),kind:'编辑',label:'卡牌编辑',patches:[{collection:'cards',uid:'a',before,after}]});await controller.flush();}
  async function delayedPull(){let release,captured;const wait=new Promise(resolve=>{release=resolve;}),started=new Promise(resolve=>{captured=resolve;});hold={wait,captured};const pending=controller.pull();await started;return {release,done:pending};}
  return {store,controller,remote,edit,delayedPull,get bundle(){return bundle;}};
}

test('a delayed sync cannot rewind a card version after its successful save',async t=>{
  const f=fixture(t);await f.controller.start();
  await f.remote('root','meta',{otherEntry:'changed'});
  const sync=await f.delayedPull();
  await f.edit({name:'已保存的卡名'});
  sync.release();await sync.done;
  assert.equal(f.bundle.data.cards[0].name,'已保存的卡名','旧同步响应覆盖了刚保存的卡牌');
  await f.edit({text:'继续编辑'});
  assert.equal(f.controller.status().conflict,null,'自己的成功提交不应导致下一次编辑版本冲突');
  assert.equal(f.store.get({collection:'cards',uid:'a'}).entity.text,'继续编辑');
});

for(const resolution of ['server','merge'])test('old sync cannot resurrect a conflict after choosing '+resolution,async t=>{
  const f=fixture(t);await f.controller.start();
  await f.remote('root','meta',{otherEntry:'changed'});
  const sync=await f.delayedPull();
  await f.remote('cards','a',{name:'服务器卡名'});
  await f.edit({text:'本地牌效'});
  assert.equal(f.controller.status().conflict.code,'VERSION_CONFLICT');
  if(resolution==='server')await f.controller.replace([]);
  else {const proposals=await f.controller.proposals();await f.controller.replace(proposals.map(({collection,uid,expectedVersion,before,after})=>({collection,uid,expectedVersion,before,after})));}
  sync.release();await sync.done;
  assert.equal(f.bundle.data.cards[0].name,'服务器卡名','旧同步响应回退了冲突处理后的条目');
  await f.edit({cost:2});
  assert.equal(f.controller.status().conflict,null,'点击冲突处理后继续编辑又发生冲突');
  const card=f.store.get({collection:'cards',uid:'a'}).entity;
  assert.equal(card.name,'服务器卡名');assert.equal(card.cost,2);
  assert.equal(card.text,resolution==='merge'?'本地牌效':'原文');
});

test('starting a field edit protects its unsubmitted draft from an earlier sync',async t=>{
  const f=fixture(t);await f.controller.start();await f.remote('root','meta',{otherEntry:'changed'});
  const sync=await f.delayedPull(),base=structuredClone(f.bundle.data.cards[0]);f.controller.invalidateSnapshots();f.bundle.data.cards[0].text='尚未到自动保存时点的输入';
  sync.release();await sync.done;
  assert.equal(f.bundle.data.cards[0].text,'尚未到自动保存时点的输入');
  await f.edit({text:'完成输入'},base);assert.equal(f.controller.status().conflict,null);
});
