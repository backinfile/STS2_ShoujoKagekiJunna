"""Revise Junna's editable vectors to the shipped game's flat Power icon language."""
from pathlib import Path
import importlib.util
import json
import math
import shutil
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'docs/design/assets/powers/2026-11-05/native-v3'
REFERENCE=ROOT/'artifacts/power-icon-native-style'
spec=importlib.util.spec_from_file_location('previous_icons',ROOT/'tools/assets/generate-power-icons.py')
old=importlib.util.module_from_spec(spec);spec.loader.exec_module(old)
p,l,g,circle,star,book,page,nib,glasses=old.path,old.line,old.group,old.circle,old.star,old.book,old.page,old.nib,old.glasses
ICONS=dict(old.ICONS)

def arrow(rotation=0,scale=1,dx=0,dy=0,color='mint'):
    body=p('M12 54H37L26 42H44L62 58H86L80 36L119 64L80 92L86 70H62L44 86H26L37 74H12L24 64Z',f'url(#{color})')
    return g(body,f'translate({dx} {dy}) rotate({rotation} 64 64) translate(64 64) scale({scale}) translate(-64 -64)')

def replace(key,fit,body):ICONS[key]=(ICONS[key][0],fit,body)

replace('JA04','饱满的青绿箭头与两道短尾迹，表现下一箭迅速完成。',
        l('M12 23H45M7 37H29','#63d27b',7)+arrow(-19,.91,0,1))
replace('JK10','眼镜与摊开的记录本组成单一剪影，表现观摩与记录。',book(.84,0,21)+g(glasses(),'translate(0 -20)'))
replace('JA22','金色弓身与粗实蓝色回转箭头，表现射击完成后重新上弦。',
        p('M67 9Q131 64 67 119L79 64Z','url(#gold)')+l('M67 10L43 64L67 118','#182d36',7)+l('M67 10L43 64L67 118','#fff3cc',3)+
        p('M45 26C18 27 7 52 14 75C18 91 30 103 48 106L50 91C25 89 20 66 30 50Q36 41 49 41L46 54L65 33L42 13Z','url(#blue)'))
replace('JK15','一册研习笔记配粗实回转箭头，表现重复学习同一内容。',
        p('M20 23H78V105H20Q11 99 16 87V29Z','url(#mint)')+p('M22 87H78V105H22Q11 97 22 87Z','url(#paper)')+
        l('M31 40H62M31 55H63','#fff3cc',5)+p('M86 99Q112 89 105 54L93 62L99 28L123 51L113 50Q126 97 91 114Z','url(#gold)'))
replace('JA35','饱满的话语气泡和接续的字句，表现娓娓道来与言叶推进。',
        p('M12 25Q12 15 25 15H101Q115 15 115 29V83Q115 96 101 96H59L32 117L36 96H25Q12 96 12 82Z','url(#mint)')+
        l('M31 39H94M31 56H95M31 73H76','#fff3cc',7))
wheel=''.join(g(l('M64 11V117','#182d36',14)+l('M64 11V117','#ffc229',8),f'rotate({a} 64 64)') for a in (0,60,120))
replace('JK32','金色船舵使用黑描边、宽轮缘和六根粗辐条，突出航海物件。',wheel+circle(64,64,37,'url(#gold)',5)+circle(64,64,25,'#111818',3)+
        ''.join(g(l('M64 37V91','#ffc229',7),f'rotate({a} 64 64)') for a in (0,60,120))+circle(64,64,12,'url(#mint)',4))
replace('JA45','弓弦上扣住粗实箭杆，保留上弦准备的识别轮廓。',
        p('M57 12Q105 64 57 116L70 64Z','url(#gold)')+l('M57 12L31 64L57 116','#182d36',7)+l('M57 12L31 64L57 116','#fff3cc',3)+arrow(0,.86,0,0))
replace('JG31','两枚前后相接的彩色台步足迹，表现连贯完成动作的练习。',
        g(p('M30 18Q51 7 58 28L53 59L26 62L23 38Z','url(#red)')+p('M25 72L50 69L47 88Q29 101 24 85Z','url(#gold)'),'rotate(-20 39 55)')+
        g(p('M69 39Q91 24 99 47L96 80L69 84L65 59Z','url(#mint)')+p('M70 93L95 89L93 108Q75 121 70 108Z','url(#gold)'),'rotate(14 82 75)'))
replace('JH19','两颗饱满青蓝四角星，以相邻的双星直接表现两次出手。',star(39,40,34,'mint')+star(88,86,34,'blue'))
replace('JG34','三枚粗实音符组成合唱的声音轮廓，表现持续发声与节拍练习。',
        p('M35 91V26L84 14V80Q81 99 64 91Q53 81 68 72L72 71V43L47 50V98Q41 117 23 108Q12 97 28 89Z','url(#gold)')+
        p('M101 63V28L119 39V56L110 50V72Q106 86 94 79Q86 69 101 63Z','url(#blue)'))
replace('JK75','练习琴键中突出一处亮起的重点，表现把知识落实为熟练动作。',
        p('M13 46L108 29L121 98L26 117Z','url(#paper)')+
        p('M35 43L49 41L54 71L40 74ZM62 38L76 36L82 67L68 69ZM89 33L103 31L108 62L94 64Z','#111818')+
        l('M47 77L53 106M74 72L79 101M99 68L103 96','#182d36',4)+
        p('M38 18L49 8L60 20L78 6L89 19L59 43Z','url(#mint)'))
replace('JA70','粗实斜箭掠过蓝色盾面，以射击维持防护。',
        g(old.shield('blue'),'translate(-10 7) translate(64 64) scale(.84) translate(-64 -64)')+arrow(-34,.85,9,-16,'gold'))
replace('JK80','黄色要点笔记与醒目的红色重点标记，表现整理信息后的准备。',
        page()+''.join(circle(44,y,5,'url(#mint)',2)+l(f'M59 {y}H85','#a88022',5) for y in (49,71,93))+
        p('M10 40L18 32L27 42L42 24L51 33L27 61Z','url(#red)'))
replace('JZ05','一次亮起的出手标记旁接出新箭，表现即兴续接的攻击。',
        star(35,80,31,'gold')+arrow(-33,.9,8,-12))

PALETTE={
 '#182d36':'#111818','#fff0bb':'#fff1a8','#fff3cc':'#fff1a8','#e0efd2':'#bceeff','#e6f8df':'#c8fff5',
 '#b18c55':'#b58b2c','#c8ac77':'#b98b25','#c6a055':'#efa71f','#203944':'#111818',
 '#b7935d':'#ab7e22','#b09368':'#ab7e22','#b89b67':'#ab7e22','#a18c68':'#ab7e22',
 '#a66658':'#ef393a','#b4a26f':'#ffc229','#dce6ce':'#fff3d5','#7a7567':'#111818',
 '#28434d':'#111818','#806e52':'#111818','#9c855e':'#b98b25','#254c5d':'#2e91d5',
 '#78b8bc':'#a5e9ff','#f4f6dc':'#ddf7ff','#e9bd6f':'#ffc229','#88bbae':'#63d27b',
 '#bc965f':'#dc981f','#a4bcb0':'#8addb2','#f3d38a':'#ffc229','#fff0cb':'#fff1a8',
 '#fff4d4':'#fff1a8','#dfa864':'#ffc229','#c99a56':'#e6a612','#294c54':'#111818',
 '#ae956c':'#a47b23','#d6e1ae':'#b5ffcb','#b4a174':'#b98b25','#719b97':'#63d27b',
 '#cde8c0':'#bceeff','#f8e6b2':'#fff3cf','#6fa899':'#63d27b','#6ea5a7':'#55beef',
 '#9c855e':'#b98b25','#d6b47b':'#ffc229','#f4f6dc':'#ddf7ff',
}
NATIVE={'ENERGY_NEXT_TURN':'energy_next_turn_power','VULNERABLE':'vulnerable_power','WEAK':'weak_power'}
DEFS='<defs>'+''.join(f'<linearGradient id="{key}" x1="0" y1="0" x2="1" y2="0"><stop stop-color="{light}"/><stop offset=".5" stop-color="{light}"/><stop offset=".5" stop-color="{base}"/><stop offset="1" stop-color="{base}"/></linearGradient>'
    for key,light,base in [('gold','#ffe386','#ffc229'),('mint','#9cf3b5','#63d27b'),('paper','#fff1b6','#ffd163'),('blue','#b5eaff','#55beef'),('red','#ff8284','#ff494a'),('purple','#d5b3fb','#a56be3')])+'</defs>'

def restyle(body,key):
    for before,after in PALETTE.items():body=body.replace(before,after)
    if key=='JK46':body=body.replace('url(#mint)','url(#purple)')
    if key=='JH30':body=body.replace('url(#mint)','url(#red)')
    # A consistent 4.5px outline at the 128-unit design scale, without tiny internal linework.
    body=body.replace('stroke-width="4"','stroke-width="4.5"')
    body=body.replace('stroke-width="2.5"','stroke-width="3"')
    # Keep every icon inside a 4px safe region, like the transparent native textures.
    return g(body,'translate(64 64) scale(.92) translate(-64 -64)')

def contact(folder,destination):
    board=Image.new('RGB',(1120,math.ceil(len(ICONS)/7)*206),'#182129');draw=ImageDraw.Draw(board)
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',16)
    for i,(key,(name,_,_)) in enumerate(ICONS.items()):
        im=Image.open(folder/f'{key}.png').convert('RGBA');large=im.resize((128,128),Image.Resampling.LANCZOS)
        x=i%7*160;y=i//7*206
        board.paste(large,(x+16,y+3),large)
        small=im.resize((32,32),Image.Resampling.LANCZOS);board.paste(small,(x+64,y+164),small)
        draw.text((x+80,y+140),name,anchor='mm',fill='#ece8da',font=font)
    board.save(destination)

def build():
    import cairosvg
    for folder in ('svg','originals','icons'):(BASE/folder).mkdir(parents=True,exist_ok=True)
    native_manifest=json.loads((REFERENCE/'native-manifest.json').read_text(encoding='utf-8'))
    rows=[]
    for key,(name,fit,body) in ICONS.items():
        original=BASE/'originals'/f'{key}.png';vector=None;native_source=None
        if key in NATIVE:
            native=next(e for e in native_manifest['images'] if e['file']==NATIVE[key]+'.png' and '/powers/' in e['path'])
            shutil.copyfile(REFERENCE/'native'/native['file'],original)
            shutil.copyfile(original,BASE/'icons'/f'{key}.png')
            native_source=native['path']
            fit={'ENERGY_NEXT_TURN':'沿用本体黄色闪电图标，保持下回合能量的既有辨识。','VULNERABLE':'沿用本体红色裂心图标，保持易伤的既有辨识。','WEAK':'沿用本体浅绿色断剑图标，保持虚弱的既有辨识。'}[key]
        else:
            vector=BASE/'svg'/f'{key}.svg'
            svg=f'<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 128 128">{DEFS}{restyle(body,key)}</svg>'
            ET.fromstring(svg)
            vector.write_text(svg,encoding='utf-8')
            cairosvg.svg2png(bytestring=svg.encode(),write_to=str(original))
        relative=lambda p:str(p.relative_to(ROOT)).replace('\\','/') if p else None
        rows.append(dict(key=key,name=name,fit=fit,version=3,size=[256,256],nativeSource=native_source,svg=relative(vector),original=relative(original),icon=relative(BASE/'icons'/f'{key}.png'),assetId='power-icon-'+key.lower().replace('_','-')+'-native-v3-20261105'))
    manifest=dict(size=[256,256],style='本体 Power 风格：黑色粗描边、高饱和纯色、硬边双色明暗、饱满透明底剪影。',
                  styleReference='artifacts/power-icon-native-style/native-reference.png',referencePack='SlayTheSpire2.pck',customIcons=28,nativeIcons=3,icons=rows)
    (BASE/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    contact(BASE/'originals',BASE/'originals-contact.png')
    print('Created 28 native-style vector originals and reused 3 exact native Power icons, all 256px.')

if __name__=='__main__':build()
