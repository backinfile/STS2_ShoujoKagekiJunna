'use strict';
const fs = require('node:fs');
const path = require('node:path');
const out = path.resolve(process.argv[2] || 'out/tmp/native-knowledge-gallery');
const fixtureName = process.argv[3] || 'groups.json';
const fixture = JSON.parse(fs.readFileSync(path.join(__dirname, fixtureName), 'utf8'));
const selection = process.argv[4] ? JSON.parse(fs.readFileSync(path.join(__dirname, process.argv[4]), 'utf8')) : null;
const report = JSON.parse(fs.readFileSync(path.join(out, 'report.json'), 'utf8'));
if (!report.completed || report.rows.length !== fixture.length || report.rows.some(row => !row.pass)) throw new Error('A complete passing native UI tour is required.');
for(const group of fixture){
 const row=report.rows.find(row=>row.Id===group.id);
 if(!row || [...row.actual].sort().join('|')!==[...group.cardIds].sort().join('|'))throw new Error('Rendered group mismatch: '+group.id);
}
function imageGroup(group,directory){return {id:group.id,title:group.title,count:group.cardIds.length,
 image:'data:image/png;base64,'+fs.readFileSync(path.join(directory,group.id+'-base.png')).toString('base64')};}
const saved = selection ? selection.groups.map(group=>imageGroup(group,path.resolve(__dirname,'../../..',group.screenshotDirectory||selection.screenshotDirectory))) : [];
const savedIds=new Set(saved.map(group=>group.id));
const newGroups=fixture.filter(group=>!savedIds.has(group.id));
const groups = [...saved,...newGroups.map(group=>imageGroup(group,out))];
let html = fs.readFileSync(path.join(__dirname,'review-template.html'),'utf8')
 .replace('/*__GROUPS__*/',JSON.stringify(groups).replace(/</g,'\\u003c'))
 .replace('/*__SAVED__*/',JSON.stringify(saved.map(group=>group.id)))
 .replace('/*__STORAGE_KEY__*/',JSON.stringify(selection?'junna-native-knowledge-round2-v2':'junna-native-knowledge-20-v1'))
 .replace('/*__DEFAULT_CURRENT__*/',String(saved.length));
if(selection)html=html.replaceAll('本体知识 · 20组','本体知识 · 第二批');
new Function(html.match(/<script>([\s\S]*?)<\/script>/)[1]);
fs.writeFileSync(path.join(out,'review.html'),html);
console.log(JSON.stringify({candidateGroups:fixture.length,newGroups:newGroups.length,savedGroups:saved.length,embeddedImages:groups.length,bytes:Buffer.byteLength(html)}));
