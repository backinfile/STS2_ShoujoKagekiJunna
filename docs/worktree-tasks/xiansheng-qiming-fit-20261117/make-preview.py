from pathlib import Path
from PIL import Image,ImageOps,ImageDraw,ImageFont
import base64,json,io,os
repo=Path.cwd();base=Path(__file__).resolve().parent;assets=json.loads((repo/'docs/design/workbench/assets.json').read_text(encoding='utf-8'));fonts=Path(os.environ['WINDIR'])/'Fonts'
def asset(key):return Image.open(io.BytesIO(base64.b64decode(assets[key].split(',')[1]))).convert('RGBA')
source=Image.open(base/'sources/xenodude-tv02-21.png');source.crop((200,0,1120,690)).resize((960,720),Image.Resampling.LANCZOS).save(base/'stage-lights-candidate.png')
def make(upgraded=False):
 card=Image.new('RGBA',(600,844),(0,0,0,0));pic=ImageOps.fit(Image.open(base/'stage-lights-candidate.png').convert('RGBA'),(500,380),centering=(.5,0));card.alpha_composite(pic,(50,86));card.alpha_composite(asset('frame-skill').resize((600,844),Image.Resampling.LANCZOS))
 for key,box in [('portrait-skill-rare',(25,94,550,420)),('banner-rare',(-26,8,654,166)),('plaque-rare',(239,424,122,74))]:
  x,y,w,h=box;card.alpha_composite(asset(key).resize((w,h),Image.Resampling.LANCZOS),(x,y))
 energy=Image.open(repo/'docs/design/assets/arrow/2026-11-02/junna-energy-workbench-v4.png').convert('RGBA').resize((128,128),Image.Resampling.LANCZOS);card.alpha_composite(energy,(-32,-32));draw=ImageDraw.Draw(card)
 bold=lambda size:ImageFont.truetype(str(fonts/'msyhbd.ttc'),size)
 draw.text((300,67),'开演'+('+' if upgraded else ''),font=bold(44),anchor='mm',fill='#8dec83' if upgraded else '#fff6e2',stroke_width=2,stroke_fill='#473214');draw.text((300,461),'技能',font=ImageFont.truetype(str(fonts/'msyh.ttc'),32),anchor='mm',fill='#231e18');draw.text((26,28),'3',font=bold(48),anchor='mm',fill='#fff6e2',stroke_width=2,stroke_fill='#243132')
 for y,text in [(570,'获得'+('16' if upgraded else '12')+'点格挡。'),(630,'所有飞矢'),(688,'加速到完成。')]:draw.text((300,y),text,font=bold(39),anchor='mm',fill='#fff6e2',stroke_width=1,stroke_fill='#413833')
 return card
canvas=Image.new('RGB',(1300,940),'#202632')
for x,up in [(30,False),(670,True)]:
 card=make(up);canvas.paste(card,(x,60),card)
canvas.save(base/'card-preview.png')