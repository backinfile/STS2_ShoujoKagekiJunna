const {test}=require('node:test');
const assert=require('node:assert/strict');
const focus=require('./arrow-focus.js');

test('unset focus preserves the centered cover crop',()=>{
  const image=focus.layout(960,720);
  assert.ok(Math.abs(image.height-416)<1e-8);
  assert.ok(Math.abs(image.x+image.width/2-120)<1e-8);
  assert.ok(Math.abs(image.y)<1e-8);
});

test('an off-center subject stays centered on both axes with no empty image edges',()=>{
  for(const point of [{x:.9,y:.18},{x:.2,y:.8},{x:.4,y:.7}]){
    const image=focus.layout(960,720,point);
    assert.ok(Math.abs(image.x+image.width*point.x-120)<1e-8);
    assert.ok(Math.abs(image.y+image.height*point.y-208)<1e-8);
    assert.ok(image.x<=1e-8&&image.y<=1e-8);
    assert.ok(image.x+image.width>=240-1e-8&&image.y+image.height>=416-1e-8);
  }
});

test('image corners remain finite and malformed coordinates use the center',()=>{
  for(const point of [{x:0,y:0},{x:1,y:1}])assert.ok(Object.values(focus.layout(960,720,point)).every(Number.isFinite));
  for(const point of [null,{x:'0.5',y:.5},{x:NaN,y:0},{x:1.1,y:.5}]){
    assert.equal(Boolean(focus.valid(point)),false);
    assert.deepEqual(focus.normalize(point),{x:.5,y:.5});
  }
});

test('workbench normalization preserves focus and rejects invalid MCP coordinates',()=>{
  const data={cards:[{uid:'arrow',id:'A',name:'箭',type:'箭矢',text:'造成1点伤害。',cost:1,turns:2,arrowFocus:{x:.9,y:.18}}]};
  const normalize=require('./workbench-domain.js').create({seed:{data},uid:()=>assert.fail('Unexpected ID'),JunnaFitPool:{normalizeEntry:e=>e}});
  assert.deepEqual(normalize(data).cards[0].arrowFocus,{x:.9,y:.18});
  for(const value of [null,{x:'0.9',y:.18},{x:1.1,y:.18},{x:.5}]){
    const invalid=structuredClone(data);invalid.cards[0].arrowFocus=value;
    assert.throws(()=>normalize(invalid),/聚焦坐标/);
  }
});
