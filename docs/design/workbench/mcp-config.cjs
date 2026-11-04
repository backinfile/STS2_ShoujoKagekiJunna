'use strict';
const fs=require('node:fs');
const path=require('node:path');
const crypto=require('node:crypto');
const configPath=path.join(__dirname,'local-save.config.json');
const dataPath=path.resolve(__dirname,'../data/纯那Mod设计工作台_本地编辑.json');
const workspaceId='junna-mod-workbench-20261101-v1';
function ensureConfig(){
  if(!fs.existsSync(configPath))fs.writeFileSync(configPath,JSON.stringify({port:17842,token:crypto.randomBytes(32).toString('hex')},null,2)+'\n',{flag:'wx'});
  const config=JSON.parse(fs.readFileSync(configPath,'utf8').replace(/^\uFEFF/,''));
  if(!Number.isInteger(config.port)||config.port<1024||config.port>65535||!/^[a-f0-9]{64}$/.test(config.token))throw Error('本地 MCP 配置无效');
  return config;
}
module.exports={ensureConfig,dataPath,workspaceId};
