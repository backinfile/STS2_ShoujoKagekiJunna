"""Pack cleaned relics with a shared transform; inspect native and small-size output."""
import argparse, hashlib, json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import numpy as np

TASK=Path(__file__).resolve().parent
ROOT=TASK.parents[2]
parser=argparse.ArgumentParser()
parser.add_argument('--base',default='art-v2')
parser.add_argument('--independent',action='store_true')
args=parser.parse_args()
BASE=TASK/args.base
OUT=ROOT/'ShoujoKagekiJunna/images/relics'
keys=['green_gem','burning_gem']
native=[Image.open(BASE/'cleaned-native'/f'{key}.png').convert('RGBA') for key in keys]
boxes=[im.getchannel('A').point(lambda a:255 if a>8 else 0).getbbox() for im in native]
if not args.independent:
    assert native[0].size==native[1].size
    box=(min(b[0] for b in boxes),min(b[1] for b in boxes),max(b[2] for b in boxes),max(b[3] for b in boxes))
    boxes=[box,box]
icons=[];transforms={}
for key,im,box in zip(keys,native,boxes):
    scale=112/max(box[2]-box[0],box[3]-box[1])
    size=tuple(round(v*scale) for v in (box[2]-box[0],box[3]-box[1]))
    offset=((128-size[0])//2,(128-size[1])//2)
    icon=Image.new('RGBA',(128,128))
    icon.alpha_composite(im.crop(box).resize(size,Image.Resampling.LANCZOS),offset)
    icon.save(OUT/f'{key}.png');icons.append(icon)
    transforms[key]={'crop':box,'scale':scale,'offset':offset}
# Independently drawn gems need their own physical hover silhouettes.
for key,icon in zip(keys,icons):
    outline=Image.new('RGBA',(128,128),'white')
    outline.putalpha((icon if args.independent else icons[0]).getchannel('A'))
    outline.save(OUT/f'{key}_outline.png')
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
board=Image.new('RGB',(840,530),'#172020');draw=ImageDraw.Draw(board)
for col,(key,title,im) in enumerate(zip(keys,['绿色宝石','燃烧宝石'],icons)):
    x=col*420
    draw.text((x+130,14),title,font=font,fill='#e7eedc')
    board.paste(im.resize((256,256),Image.Resampling.NEAREST),(x+82,54),im.resize((256,256),Image.Resampling.NEAREST))
    for row,bg in enumerate(['#253c36','#e3d7c1']):
        draw.rectangle((x+16,340+row*85,x+404,420+row*85),fill=bg)
        for i,s in enumerate([32,48,64,128]):
            show=im.resize((s,s),Image.Resampling.LANCZOS)
            if s==128:continue
            board.paste(show,(x+55+i*120,346+row*85+(64-s)//2),show)
            draw.text((x+54+i*120,407+row*85),str(s),fill='#829b92',font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',13))
board.save(BASE/'preview.png')
# Compare cleanup at original pixel scale in each central cut-facet region.
native_board=Image.new('RGB',(1200,700),'#172020');d=ImageDraw.Draw(native_board)
for row,key in enumerate(keys):
    for col,folder in enumerate(['originals','cleaned-native']):
        im=Image.open(BASE/folder/f'{key}.png').convert('RGBA')
        cx,cy=im.width//2,im.height//2
        crop=im.crop((cx-280,cy-145,cx+280,cy+145))
        native_board.paste(crop,(col*600+20,row*350+45),crop)
        d.text((col*600+20,row*350+10),f'{key} / {folder} / 1:1',font=font,fill='white')
native_board.save(BASE/'cleanup-native-comparison.png')
# A compact visual index of all nine official weapon reference sheets.
refs=TASK/'research/weapons'
names=['华恋','光','真昼','克洛迪娜','真矢','纯那','奈奈','双叶','香子']
weapons=Image.new('RGB',(1500,1440),'#eeece5');d=ImageDraw.Draw(weapons)
for n,name in enumerate(names):
    page=27+n;im=Image.open(refs/f'tv-preview-{page}.webp').convert('RGB');im.thumbnail((486,426))
    x=(n%3)*500;y=(n//3)*480
    weapons.paste(im,(x+(500-im.width)//2,y+44))
    d.text((x+20,y+8),f'{name} · {page}',font=font,fill='#172020')
weapons.save(refs/'all-nine-weapons.jpg',quality=93)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
report={'dimensions':[128,128],'format':'RGBA PNG','transforms':transforms,
        'outline':'individual gemstone alpha' if args.independent else 'shared physical gemstone alpha',
        'checks':{},'files':[]}
for key in keys:
    for suffix in ['', '_outline']:
        p=OUT/f'{key}{suffix}.png';im=Image.open(p).convert('RGBA');a=np.array(im)[...,3]
        assert im.size==(128,128) and a.max()==255 and a.min()==0
        assert a[0].max()==0 and a[-1].max()==0 and a[:,0].max()==0 and a[:,-1].max()==0
        report['files'].append({'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p),'bbox':im.getbbox()})
if args.independent:
    assert not np.array_equal(np.array(icons[0])[...,3],np.array(icons[1])[...,3])
    for key,icon in zip(keys,icons):
        assert np.array_equal(np.array(icon)[...,3],np.array(Image.open(OUT/f'{key}_outline.png'))[...,3])
report['checks']={'transparentMargins':True,'independentSilhouettes':args.independent,'eachOutlineMatchesGem':True,'onlyGemstone':True}
(BASE/'validation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report['checks']))
