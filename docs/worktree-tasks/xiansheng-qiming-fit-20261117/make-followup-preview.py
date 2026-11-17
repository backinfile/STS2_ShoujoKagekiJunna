from pathlib import Path
from PIL import Image,ImageOps,ImageDraw,ImageFont
import base64,json,io,os
repo=Path.cwd(); base=Path(__file__).resolve().parent
assets=json.loads((repo/'docs/design/workbench/assets.json').read_text(encoding='utf-8'))
fonts=Path(os.environ['WINDIR'])/'Fonts'
def asset(key): return Image.open(io.BytesIO(base64.b64decode(assets[key].split(',')[1]))).convert('RGBA')
def font(size,bold=True):return ImageFont.truetype(str(fonts/('msyhbd.ttc' if bold else 'msyh.ttc')),size)
energy=Image.open(repo/'docs/design/assets/arrow/2026-11-02/junna-energy-workbench-v4.png').convert('RGBA')
candidates=json.loads((base/'followup-candidates.json').read_text(encoding='utf-8'))
for c,src in zip(candidates,['JS33-tv11-planning.jpg','U_CARD_025-tv11-return.png']):
 Image.open(base/'sources'/src).crop(c['crop']).resize((960,720),Image.Resampling.LANCZOS).save(base/c['art'])
Image.open(base/'sources/JS33-tv11-book.png').crop((80,0,1520,1080)).resize((960,720),Image.Resampling.LANCZOS).save(base/'JS33-book-candidate.png')
def make(c,up=False):
 card=Image.new('RGBA',(600,844))
 pic=ImageOps.fit(Image.open(base/c['art']).convert('RGBA'),(500,380),centering=(.5,.5))
 card.alpha_composite(pic,(50,86)); card.alpha_composite(asset('frame-skill').resize((600,844),Image.Resampling.LANCZOS))
 for key,box in [('portrait-skill-uncommon',(25,94,550,420)),('banner-uncommon',(-26,8,654,166)),('plaque-uncommon',(239,424,122,74))]:
  x,y,w,h=box;card.alpha_composite(asset(key).resize((w,h),Image.Resampling.LANCZOS),(x,y))
 card.alpha_composite(energy.resize((128,128),Image.Resampling.LANCZOS),(-32,-32))
 draw=ImageDraw.Draw(card)
 def line(text,y,size=37): draw.text((300,y),text,font=font(size),anchor='mm',fill='#fff6e2',stroke_width=1,stroke_fill='#413833')
 draw.text((300,67),c['name']+('+' if up else ''),font=font(41),anchor='mm',fill='#8dec83' if up else '#fff6e2',stroke_width=2,stroke_fill='#473214')
 draw.text((300,461),'技能',font=font(32,False),anchor='mm',fill='#231e18')
 draw.text((26,28),'0',font=font(48),anchor='mm',fill='#fff6e2',stroke_width=2,stroke_fill='#243132')
 if c['id']=='JS33':
  n='3' if up else '2'
  for i,t in enumerate(['选择：从抽牌堆','抽取'+n+'张飞矢牌，',n+'张知识牌，','或者'+n+'张闪耀牌。','消耗。']): line(t,536+i*46,35)
 else:
  tokens=[('获得','text'),('', 'energy')]+([('','energy')] if up else [])+[('。','text')]
  widths=[48 if kind=='energy' else int(draw.textlength(t,font=font(39))) for t,kind in tokens]
  x=(600-sum(widths))/2
  for (t,kind),width in zip(tokens,widths):
   if kind=='energy':card.alpha_composite(energy.resize((48,48),Image.Resampling.LANCZOS),(int(x),550))
   else:draw.text((x,574),t,font=font(39),anchor='lm',fill='#fff6e2',stroke_width=1,stroke_fill='#413833')
   x+=width
  line('然后作为5回合的',634,36);line('飞矢打出。',690,39)
 return card
for c in candidates:
 canvas=Image.new('RGB',(1300,940),'#202632')
 for x,up in [(30,False),(670,True)]:
  card=make(c,up);canvas.paste(card,(x,60),card)
 canvas.save(base/(c['id']+'-preview.png'))
combined=Image.new('RGB',(1300,940),'#202632')
for x,c in zip([30,670],candidates):
 card=make(c);combined.paste(card,(x,60),card)
combined.save(base/'followup-preview.png')
# Reproduce JunnaArrowFocus.layout and its full-frame diamond clip.
pic=Image.open(base/'U_CARD_025-stage-candidate.png').convert('RGBA')
w,h=pic.size; fx,fy=.37,.5
scale=max(240/(w*2*min(fx,1-fx)),416/(h*2*min(fy,1-fy)))
left=w*fx-240/scale/2;top=h*fy-416/scale/2
canvas=pic.transform((480,832),Image.Transform.AFFINE,(1/(scale*2),0,left,0,1/(scale*2),top),Image.Resampling.BICUBIC)
poly=[(240,60),(432,276),(240,728),(48,276)]
mask=Image.new('L',canvas.size);ImageDraw.Draw(mask).polygon(poly,fill=255);canvas.putalpha(mask)
proof=Image.new('RGB',canvas.size,'#202632');proof.paste(canvas,(0,0),canvas)
ImageDraw.Draw(proof).line(poly+[poly[0]],fill='#ead7ae',width=4)
proof.save(base/'U_CARD_025-arrow-preview.png')
print('Rendered two base/upgrade pairs, combined comparison and native arrow layout mask.')
