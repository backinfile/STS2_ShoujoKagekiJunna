from pathlib import Path
from PIL import Image, ImageOps, ImageDraw, ImageFont
import json, base64, io, os, re, unicodedata, hashlib

ROOT=Path(__file__).resolve().parents[3]
BASE=Path(__file__).parent
PLAN=json.loads((BASE/'art-plan.json').read_text(encoding='utf-8'))
SNAP=json.loads((BASE/'current-before.json').read_text(encoding='utf-8'))
CARDS={x['id']:x for x in SNAP['data']['cards']}
ASSETS=json.loads((ROOT/'docs/design/workbench/assets.json').read_text(encoding='utf-8'))
FONT=Path(os.environ['WINDIR'])/'Fonts'
OUT=BASE/'portraits';OUT.mkdir(exist_ok=True)
PREV=BASE/'previews';PREV.mkdir(exist_ok=True)

def font(size,bold=True):return ImageFont.truetype(str(FONT/('msyhbd.ttc' if bold else 'msyh.ttc')),size)
def asset(key):return Image.open(io.BytesIO(base64.b64decode(ASSETS[key].split(',',1)[1]))).convert('RGBA')
def reference(key):
    files=list((BASE/'references').glob(key+'.*'))
    if not files:raise ValueError('Missing reference '+key)
    return Image.open(files[0]).convert('RGB')
def source_path(src):
    if src.startswith('../../../design/'):return ROOT/'docs/design'/src.split('../../../design/',1)[1]
    if src.startswith('../../xiansheng-'):return BASE.parent/src.split('../../',1)[1]
    return BASE/src

norm=lambda s:re.sub(r'[\s\W_+]+','',unicodedata.normalize('NFKC',s))
karen=json.loads((ROOT.parent/'STS2_ShoujoKagekiAijoKaren/ShoujoKagekiAijoKaren/localization/zhs/cards.json').read_text(encoding='utf-8'))
checks=[]
for row in PLAN:
    c=CARDS[row['id']]
    if row.get('name'):
        n=norm(row['name'])
        own=[x['id'] for x in CARDS.values() if x['id']!=row['id'] and norm(x['name'])==n]
        foreign=[v for k,v in karen.items() if k.endswith('.title') and norm(v)==n]
        assert not own and not foreign,(row['name'],own,foreign)
        checks.append({'id':row['id'],'name':row['name'],'junnaMatches':own,'karenMatches':foreign})
    if row.get('source'):
        src=source_path(row['source'])
        im=Image.open(src).convert('RGB');left,top,right,bottom=row['crop']
        assert 0<=left<right<=im.width and 0<=top<bottom<=im.height,(row['id'],im.size,row['crop'])
        portrait=im.crop((left,top,right,bottom)).resize((960,720),Image.Resampling.LANCZOS)
        portrait.save(OUT/(row['id']+'.jpg'),quality=95,subsampling=0)
        row['sourceSize']=list(im.size)
        row['sourceFile']=str(src.relative_to(ROOT)).replace('\\','/')
        row['outputFile']=str((OUT/(row['id']+'.jpg')).relative_to(ROOT)).replace('\\','/')
        row['sha256']=hashlib.sha256((OUT/(row['id']+'.jpg')).read_bytes()).hexdigest()
    else:
        portrait=reference(row.get('reuse',c['art']))
        portrait.save(OUT/(row['id']+'.jpg'),quality=95,subsampling=0)

energy=Image.open(ROOT/'docs/design/assets/arrow/2026-11-02/junna-energy-workbench-v4.png').convert('RGBA')
def wrap(text,draw,f,max_width):
    lines=[]
    for para in text.split('\n'):
        line=''
        for ch in para:
            if line and draw.textlength(line+ch,font=f)>max_width:
                lines.append(line);line=ch
            else:line+=ch
        lines.append(line)
    return lines
def card(row,up=False):
    c=CARDS[row['id']];name=row.get('name',c['name'])
    kind='attack' if c['type']=='攻击' or c.get('arrowSubtype')=='攻击' else 'power' if c['type']=='能力' else 'skill'
    rarity={'普通':'common','罕见':'uncommon','稀有':'rare','远古':'ancient'}[c['rarity']]
    out=Image.new('RGBA',(600,844));pic=ImageOps.fit(Image.open(OUT/(row['id']+'.jpg')).convert('RGBA'),(500,380),centering=(.5,.5))
    out.alpha_composite(pic,(50,86));out.alpha_composite(asset('frame-'+kind).resize((600,844),Image.Resampling.LANCZOS))
    for key,box in [('portrait-'+kind+'-'+rarity,(25,94,550,420)),('banner-'+rarity,(-26,8,654,166)),('plaque-'+rarity,(239,424,122,74))]:
        x,y,w,h=box;out.alpha_composite(asset(key).resize((w,h),Image.Resampling.LANCZOS),(x,y))
    out.alpha_composite(energy.resize((128,128),Image.Resampling.LANCZOS),(-32,-32))
    draw=ImageDraw.Draw(out);size=44
    while draw.textlength(name+('+' if up else ''),font=font(size))>495:size-=1
    draw.text((300,68),name+('+' if up else ''),font=font(size),anchor='mm',fill='#85f88a' if up else '#fff6e2',stroke_width=2,stroke_fill='#473214')
    draw.text((300,461),c['type'],font=font(32,False),anchor='mm',fill='#25211b')
    draw.text((25,28),str(c.get('upgradedCost' if up else 'cost',0)),font=font(50),anchor='mm',fill='#fff6e2',stroke_width=2,stroke_fill='#243132')
    lines=wrap(c['upgradedText' if up else 'text'],draw,font(35),510)
    y=620-(len(lines)-1)*23
    for t in lines:
        draw.text((300,y),t,font=font(35),anchor='mm',fill='#fff6e2',stroke_width=1,stroke_fill='#302c29');y+=46
    return out

for row in PLAN:
    pair=Image.new('RGB',(1200,880),'#222733')
    for i,up in enumerate([False,True]):
        im=card(row,up);pair.paste(im,(i*600,20),im)
    pair.save(PREV/(row['id']+'-versions.jpg'),quality=93)

for page in range((len(PLAN)+5)//6):
    rows=PLAN[page*6:(page+1)*6];sheet=Image.new('RGB',(1200,((len(rows)+2)//3)*590),'#222733');dr=ImageDraw.Draw(sheet)
    for i,row in enumerate(rows):
        im=card(row).resize((390,549),Image.Resampling.LANCZOS);x=(i%3)*400;y=(i//3)*590
        sheet.paste(im,(x+5,y+30),im);dr.text((x+200,y+10),row['id'],font=font(18,False),anchor='mm',fill='#ddd')
    sheet.save(BASE/f'card-review-{page+1}.jpg',quality=94)

art_sheet=Image.new('RGB',(1440,((len(PLAN)+3)//4)*305),'#222733');dr=ImageDraw.Draw(art_sheet)
for i,row in enumerate(PLAN):
    im=Image.open(OUT/(row['id']+'.jpg')).resize((352,264),Image.Resampling.LANCZOS);x=i%4*360;y=i//4*305
    art_sheet.paste(im,(x,y+36));dr.text((x+176,y+17),row.get('name',CARDS[row['id']]['name']),font=font(22),anchor='mm',fill='#eee')
art_sheet.save(BASE/'art-review.jpg',quality=95)

for row in PLAN:
    c=CARDS[row['id']]
    if c['type']=='飞矢' or c['id'] in ['U_CARD_018','U_CARD_021']:
        pic=Image.open(OUT/(row['id']+'.jpg')).convert('RGBA');w,h=pic.size;fx,fy=row.get('focus',[.5,.5]);scale=max(240/(w*2*min(fx,1-fx)),416/(h*2*min(fy,1-fy)))
        left=w*fx-240/scale/2;top=h*fy-416/scale/2
        out=pic.transform((480,832),Image.Transform.AFFINE,(1/(scale*2),0,left,0,1/(scale*2),top),Image.Resampling.BICUBIC)
        poly=[(240,60),(432,276),(240,728),(48,276)];mask=Image.new('L',out.size);ImageDraw.Draw(mask).polygon(poly,fill=255)
        bg=Image.new('RGB',out.size,'#222733');bg.paste(out,(0,0),mask);ImageDraw.Draw(bg).line(poly+[poly[0]],fill='#ead7ae',width=3)
        bg.save(PREV/(row['id']+'-arrow.jpg'),quality=94)
(BASE/'prepared-plan.json').write_text(json.dumps(PLAN,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(BASE/'name-checks.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'cards':len(PLAN),'newPortraits':sum('source' in r for r in PLAN),'nameChecks':len(checks)},ensure_ascii=False))
