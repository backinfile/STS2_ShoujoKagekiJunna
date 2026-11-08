"""Produce native-size cleanup comparisons and a compact shipped-game style comparison."""
from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageFont
import numpy as np

BASE=Path(__file__).resolve().parent
ROOT=BASE.parents[4]
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',14)
plan=json.loads((BASE/'design-plan.json').read_text(encoding='utf-8'))
snapshot=json.loads((BASE/'source-snapshot.json').read_text(encoding='utf-8'))
report=[]
(BASE/'comparisons').mkdir(exist_ok=True)
for spec in plan['icons']:
    key=spec['key']
    raw=Image.open(BASE/'generated'/(key+'.png')).convert('RGBA')
    clean=Image.open(BASE/'cleaned-full'/(key+'.png')).convert('RGBA')
    icon=Image.open(BASE/'icons'/(key+'.png')).convert('RGBA')
    arr=np.asarray(icon)
    assert icon.size==(256,256)
    alpha_equal=np.array_equal(np.asarray(raw)[...,3],np.asarray(clean)[...,3])
    if key=='JA35':
        changed=np.asarray(raw)[...,3]!=np.asarray(clean)[...,3]
        allowed=np.zeros(changed.shape,dtype=bool)
        allowed[380:940,540:950]=True
        assert not changed[~allowed].any()
        assert np.asarray(clean)[470:820,580:870,3].min()==255
    else:
        assert alpha_equal
    assert arr[0,0,3]==arr[-1,-1,3]==0
    bbox=icon.getbbox()
    assert bbox[0]>=5 and bbox[1]>=5 and bbox[2]<=251 and bbox[3]<=251
    report.append(dict(key=key,size=list(icon.size),alpha_preserved=alpha_equal,external_alpha_preserved=True,transparent=True,bbox=list(bbox)))
    comparison=Image.new('RGB',(raw.width*2,raw.height+40),'#182128')
    comparison.paste(raw,(0,40),raw)
    comparison.paste(clean,(raw.width,40),clean)
    d=ImageDraw.Draw(comparison)
    d.text((12,8),'生成原图 · 100%',font=font,fill='white')
    d.text((raw.width+12,8),'Artifact Cleaner · 100%',font=font,fill='white')
    comparison.save(BASE/'comparisons'/(key+'-native-size.png'))

# Fixed-size side-by-side reference: native 128/32 and new 128/32.
native=['strength','dexterity','weak','vulnerable','accuracy','ritual','barricade','artifact','buffer','afterimage','loop','child_of_the_stars']
selected=['JA04','JK10','JA22','A','JK32','JG31','JH19','JH23','JA70','JK81','JG23','ENERGY_RESERVE']
sheet=Image.new('RGB',(1000,6*182+50),'#182128')
d=ImageDraw.Draw(sheet)
d.text((240,10),'本体',font=font,fill='white',anchor='mt')
d.text((740,10),'重绘',font=font,fill='white',anchor='mt')
for i,(nk,key) in enumerate(zip(native,selected)):
    y=(i//2)*182+45
    col=i%2
    for x,p,label in [(col*240, ROOT/'artifacts/power-icon-native-style/native'/(nk+'_power.png'),nk),(500+col*240,BASE/'icons'/(key+'.png'),next(s['name'] for s in plan['icons'] if s['key']==key))]:
        im=Image.open(p).convert('RGBA')
        for size,dx in [(128,12),(32,157)]:
            tile=im.resize((size,size),Image.Resampling.LANCZOS)
            sheet.paste(tile,(x+dx,y+(128-size)//2),tile)
        d.text((x+14,y+137),label,font=small,fill='white')
sheet.save(BASE/'native-comparison.png')

# Full old/new comparison, keeping the original workbench order.
sheet=Image.new('RGB',(1200,12*170),'#182128')
d=ImageDraw.Draw(sheet)
for i,spec in enumerate(plan['icons']):
    x,y=(i%3)*400,(i//3)*170
    entry=snapshot[i]['entity']
    old=ROOT/entry.get('iconPath','__missing__')
    if not old.exists() and entry.get('art'):
        candidate=next((s['entity'].get('iconPath') for s in snapshot if s['entity'].get('art')==entry['art'] and s['entity'].get('iconPath')),None)
        if candidate: old=ROOT/candidate
    for ox,p in [(10,old),(206,BASE/'icons'/(spec['key']+'.png'))]:
        if p.exists():
            im=Image.open(p).convert('RGBA').resize((112,112),Image.Resampling.LANCZOS)
            sheet.paste(im,(x+ox,y+2),im)
    d.text((x+10,y+120),spec['name'],font=font,fill='white')
    d.text((x+10,y+146),'原图 → 重绘',font=small,fill='#9daeb9')
sheet.save(BASE/'before-after.png')
(BASE/'validation.json').write_text(json.dumps(dict(count=len(report),assets=report),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(dict(icons=len(report),external_alpha_preserved=True,interior_alpha_repairs=['JA35'],transparent=True,comparisons=len(report)),ensure_ascii=False))
