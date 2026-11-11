from pathlib import Path
from PIL import Image,ImageOps,ImageDraw,ImageFont
import json,base64,html,re,os
root=Path(__file__).parent
repo=Path.cwd()
data=json.loads((root/'cards-after.json').read_text(encoding='utf-8'))
assets=json.loads((repo/'docs/design/workbench/assets.json').read_text(encoding='utf-8'))
def fileurl(p,mime='image/png'):return 'data:'+mime+';base64,'+base64.b64encode(p.read_bytes()).decode()
assets['energy-junna']=fileurl(repo/'docs/design/assets/arrow/2026-11-02/junna-energy-workbench-v4.png')
assets['portrait-stage-junna']=fileurl(repo/'docs/design/assets/arrow/2026-11-02/arrow-portrait-workbench-clean-v5.png')
for c in data: assets[c['art']]=fileurl(root/'portraits'/(c['id']+'.jpg'),'image/jpeg')
def layer(cls,key):return '<img class="native-layer '+cls+'" src="'+assets[key]+'">'
def card(c,up):
 typ={'攻击':'attack','能力':'power'}.get(c['type'],'skill');arr=c['type']=='箭矢';kind='stage' if arr else typ;rar={'罕见':'uncommon','稀有':'rare'}.get(c['rarity'],'common')
 if arr:
  pic='<svg class="native-layer native-arrow-portrait" viewBox="0 0 1456 1080" preserveAspectRatio="none"><image href="'+assets[c['art']]+'" width="1456" height="1080" preserveAspectRatio="xMidYMid slice" clip-path="url(#junna-arrow-window)"/></svg>'
 else:pic=layer('native-portrait',c['art'])
 body=c['upgradedText'] if up else c['text'];cost=c['upgradedCost'] if up else c['cost']
 layers=layer('native-shadow','frame-'+typ)+pic+layer('native-frame','frame-'+typ)+layer('native-class-frame','frame-'+typ)+layer('native-arrow-portrait-border','portrait-stage-junna') if arr else layer('native-shadow','frame-'+typ)+pic+layer('native-frame','frame-'+typ)+layer('native-class-frame','frame-'+typ)+layer('native-portrait-border','portrait-'+typ+'-'+rar)
 layers+=layer('native-banner','banner-'+rar)
 return '<div class="game-card '+('is-upgraded' if up else '')+'" data-type="'+kind+'" data-rarity="'+rar+'">'+layers+'<div class="native-title"><span>'+html.escape(c['name'])+('+' if up else '')+'</span></div><div class="native-type" style="--plaque:url('+assets['plaque-'+rar]+')">'+c['type']+'</div><div class="native-description"><div>'+html.escape(body)+'</div></div><div class="native-energy">'+layer('native-energy-art','energy-junna')+'<span>'+str(cost)+'</span></div>'+('<div class="native-arrow-turns">'+str(c['upgradedTurns'] if up else c['turns'])+'</div>' if arr else '')+'</div>'
css=(repo/'docs/design/workbench/workbench.css').read_text(encoding='utf-8').replace('__REGULAR_FONT__',assets['game-regular']).replace('__BOLD_FONT__',assets['game-bold'])
shell=(repo/'docs/design/workbench/shell.html').read_text(encoding='utf-8');defs=shell[shell.index('<svg'):shell.index('</svg>')+6]
rows=''.join('<div class="pair">'+card(c,False)+card(c,True)+'<p>'+c['id']+'</p></div>' for c in data)
frame=fileurl(repo/'ShoujoKagekiJunna/images/floating_arrow_c1.svg','image/svg+xml')
arrows=''
for c in data:
 if c['type']!='箭矢':continue
 x=c.get('arrowFocus',{}).get('x',.5);y=c.get('arrowFocus',{}).get('y',.5)
 with Image.open(root/'portraits'/(c['id']+'.jpg')) as im:w,h=im.size
 scale=max(240/(w*2*min(x,1-x)),416/(h*2*min(y,1-y)));left=w*x-240/scale/2;top=h*y-416/scale/2
 arrows+='<div><svg width="120" height="208" viewBox="0 0 240 416"><defs><clipPath id="clip'+c['id']+'"><polygon points="120,30 216,138 120,364 24,138"/></clipPath></defs><g clip-path="url(#clip'+c['id']+')"><image href="'+assets[c['art']]+'" x="'+str(-left*scale)+'" y="'+str(-top*scale)+'" width="'+str(w*scale)+'" height="'+str(h*scale)+'"/></g><image href="'+frame+'" width="240" height="416"/></svg><p>'+c['name']+'</p></div>'
js="""document.fonts.ready.then(()=>{for(const el of document.querySelectorAll('.game-card')){const scale=el.clientWidth/300;for(const [node,max,min] of [[el.querySelector('.native-title span'),26,11],[el.querySelector('.native-description>div'),21,12]]){for(let size=max;size>=min;size--){node.style.fontSize=size*scale+'px';if(node.scrollHeight<=node.parentElement.clientHeight+1&&node.scrollWidth<=node.parentElement.clientWidth+1)break;}}}document.body.dataset.ready='true';});"""
out='<!doctype html><meta charset="utf-8"><title>卡面检查</title><style>'+css+'html,body{height:auto;overflow:visible}body{padding:25px}.pairs{display:grid;grid-template-columns:repeat(4,380px);gap:15px 14px}.pair{display:grid;grid-template-columns:repeat(2,180px);gap:6px}.pair p{grid-column:1/-1;text-align:center;margin:0}.arrows{display:flex;gap:50px;margin-top:35px}.arrows p{text-align:center}</style>'+defs+'<div class="pairs">'+rows+'</div><div class="arrows">'+arrows+'</div><script>'+js+'</script>'
(root/'card-review.html').write_text(out,encoding='utf-8')
font=ImageFont.truetype(str(Path(os.environ['WINDIR'])/'Fonts/msyh.ttc'),18);out=Image.new('RGB',(1280,1160),'#eee');d=ImageDraw.Draw(out)
for i,c in enumerate(data):
 im=Image.open(root/'portraits'/(c['id']+'.jpg'));out.paste(ImageOps.fit(im,(312,234)),((i%4)*320+4,(i//4)*290));d.text(((i%4)*320+4,(i//4)*290+242),c['id']+' '+c['name'],fill='black',font=font)
out.save(root/'selected-art.jpg',quality=92)
