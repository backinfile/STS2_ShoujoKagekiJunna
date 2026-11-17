from pathlib import Path
from PIL import Image, ImageOps, ImageDraw, ImageFont
import argparse, base64, io, json, os
p=argparse.ArgumentParser()
p.add_argument('--assets',required=True)
p.add_argument('--art',required=True)
p.add_argument('--output',required=True)
a=p.parse_args()
assets=json.loads(Path(a.assets).read_text(encoding='utf-8'))
def asset(key):return Image.open(io.BytesIO(base64.b64decode(assets[key].split(',',1)[1]))).convert('RGBA')
font_path=Path(os.environ['WINDIR'])/'Fonts/msyh.ttc'
font=lambda size:ImageFont.truetype(str(font_path),size)
out=Image.new('RGBA',(600,844))
pic=ImageOps.fit(Image.open(a.art).convert('RGBA'),(500,380),centering=(.5,.5))
out.alpha_composite(pic,(50,86))
out.alpha_composite(asset('frame-skill').resize((600,844),Image.Resampling.LANCZOS))
for key,box in [('portrait-skill-common',(25,94,550,420)),('banner-common',(-26,8,654,166)),('plaque-common',(239,424,122,74))]:
 x,y,w,h=box;out.alpha_composite(asset(key).resize((w,h),Image.Resampling.LANCZOS),(x,y))
draw=ImageDraw.Draw(out)
draw.text((300,68),'想太多',font=font(44),anchor='mm',fill='#fff6e2',stroke_width=2,stroke_fill='#473214')
draw.text((300,461),'诅咒',font=font(32),anchor='mm',fill='#25211b')
for i,line in enumerate(['不可打出。','虚无。','灵魂绑定。']):draw.text((300,568+i*48),line,font=font(35),anchor='mm',fill='#fff6e2',stroke_width=1,stroke_fill='#302c29')
out.save(a.output)
print('Rendered workbench card frame preview.')
