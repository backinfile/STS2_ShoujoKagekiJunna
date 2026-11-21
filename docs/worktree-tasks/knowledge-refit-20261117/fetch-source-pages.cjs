const fs=require('fs'),path=require('path'),https=require('https');
const dir=__dirname;
function get(url,n=0){return new Promise((ok,no)=>{const req=https.get(url,{headers:{'User-Agent':'Mozilla/5.0'}},r=>{if(r.statusCode>=300&&r.statusCode<400&&r.headers.location){r.resume();return ok(get(new URL(r.headers.location,url),n+1));}let a=[];r.on('data',b=>a.push(b));r.on('end',()=>r.statusCode===200?ok(Buffer.concat(a)):no(Error('HTTP '+r.statusCode)));});req.setTimeout(25000,()=>req.destroy(Error('timeout')));req.on('error',no);});}
const posts=[['tv11',27127],['tv07',26546],['tv09',26819],['tv03',25762],['tv01',25399],['special1',62584],['special2',62631],['special3',62666]];
const manifest=[];
async function run(){
 for(const [label,id] of posts){
  try{
   const data=JSON.parse((await get('https://public-api.wordpress.com/rest/v1.1/sites/xenodude.com/posts/'+id+'?fields=URL,title,content')).toString());
   fs.writeFileSync(path.join(dir,label+'-post.json'),JSON.stringify(data,null,2));
   const urls=[...new Set([...data.content.matchAll(/<img[^>]+src=["']([^"']+)/g)].map(m=>m[1].replace(/&amp;/g,'&').split('?')[0]).filter(u=>/\.(?:png|jpe?g)$/i.test(u)))];
   for(let i=0;i<urls.length;i++)manifest.push({label,file:label+'-'+String(i+1).padStart(2,'0')+path.extname(new URL(urls[i]).pathname),url:urls[i],page:data.URL.replace('http:','https:')});
   console.log(label,urls.length);
  }catch(e){console.log(label,e.message);}
 }
 fs.writeFileSync(path.join(dir,'source-frame-index.json'),JSON.stringify(manifest,null,2));
 fs.mkdirSync(path.join(dir,'source-frames'),{recursive:true});
 let i=0,fail=0;async function worker(){while(i<manifest.length){const x=manifest[i++],file=path.join(dir,'source-frames',x.file);if(fs.existsSync(file))continue;try{fs.writeFileSync(file,await get(x.url));}catch(e){fail++;console.log(x.file,e.message);}}}
 await Promise.all([worker(),worker(),worker(),worker()]);console.log(JSON.stringify({images:manifest.length,fail}));
}
run().catch(e=>{console.error(e.message);process.exitCode=1});
