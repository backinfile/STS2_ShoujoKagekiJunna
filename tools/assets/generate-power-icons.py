"""Reproducible transparent 128 px Power icon originals, based on Junna's motifs."""
from pathlib import Path
import json
import math
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docs/design/assets/powers/2026-11-05'
RAW = OUT / 'originals'
SVG = OUT / 'svg'
INK = '#182d36'

def path(d, fill='url(#gold)', stroke=INK, width=4):
    return f'<path d="{d}" fill="{fill}" stroke="{stroke}" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round"/>'

def line(d, color='#fff0bb', width=4):
    return path(d, 'none', color, width)

def circle(x, y, r, fill='url(#gold)', width=4):
    return f'<circle cx="{x}" cy="{y}" r="{r}" fill="{fill}" stroke="{INK}" stroke-width="{width}"/>'

def group(body, transform):
    return f'<g transform="{transform}">{body}</g>'

def star(x=64, y=55, r=31, color='gold', points=4):
    pts=[]
    for i in range(points*2):
        angle=-math.pi/2+i*math.pi/points
        radius=r if i%2==0 else r*.31
        pts.append((x+math.cos(angle)*radius,y+math.sin(angle)*radius))
    d='M'+'L'.join(f'{a:.1f} {b:.1f}' for a,b in pts)+'Z'
    return path(d,f'url(#{color})')+path(f'M{x} {y-r*.68}L{x+r*.16} {y-r*.14}L{x} {y}L{x-r*.16} {y-r*.14}Z','#fff8df','none',0)

def arrow(rot=0, scale=1, dx=0, dy=0, color='mint'):
    body=path('M18 57L39 57L30 49L44 49L57 61L93 61L88 48L117 64L88 80L93 67L57 67L44 79L30 79L39 71L18 71Z',f'url(#{color})')
    body+=line('M54 63L99 63','#fff3cc',2.5)
    return group(body,f'translate({dx} {dy}) rotate({rot} 64 64) translate(64 64) scale({scale}) translate(-64 -64)')

def book(scale=1, dx=0, dy=0):
    body=path('M13 45Q37 34 64 47Q90 34 115 45L113 99Q88 90 64 104Q38 91 15 99Z','url(#mint)')
    body+=path('M18 35Q42 28 64 42Q86 28 110 35L108 88Q86 83 64 96Q43 83 20 88Z','url(#paper)')
    body+=line('M64 44L64 94','#b18c55',3)
    body+=line('M28 48Q44 45 54 52M28 60Q42 57 54 64M75 51Q88 44 101 47M75 64Q89 56 101 59','#c8ac77',3)
    return group(body,f'translate({dx} {dy}) translate(64 64) scale({scale}) translate(-64 -64)')

def page(dx=0,dy=0,scale=1):
    body=path('M31 14H82L102 34V111H31Z','url(#paper)')+path('M82 15V35H101','#d6b47b')
    return group(body,f'translate({dx} {dy}) translate(64 64) scale({scale}) translate(-64 -64)')

def nib(rot=0, scale=1, dx=0,dy=0):
    body=path('M65 14L89 66L65 109L41 66Z','url(#gold)')+path('M65 14L65 109L41 66Z','#c6a055',INK,2)
    body+=circle(65,62,6,'#203944',2)+line('M65 69L65 96','#fff1c6',3)
    return group(body,f'translate({dx} {dy}) rotate({rot} 64 64) translate(64 64) scale({scale}) translate(-64 -64)')

def shield(color='mint'):
    return path('M21 29Q64 36 107 29L102 72Q94 99 64 115Q33 99 26 72Z',f'url(#{color})')+line('M33 45Q64 50 94 45M64 51V96','#e0efd2',3)

def glasses():
    return path('M17 48L52 42L58 52L70 52L76 42L111 48L104 78Q85 88 74 72L70 60H58L54 72Q42 88 24 78Z','url(#mint)')+line('M26 52L39 49M84 49L98 52','#e6f8df',3)

ICONS={
 'JA04': ('速射', '向右疾射的实箭与三条短促尾迹，强调下一箭立即命中。', line('M10 39H49M5 49H34M10 92H49','#6fa899',5)+arrow(-16,.93,0,-3)),
 'JK10': ('观摩', '眼镜置于记录本上，以观看与记录对应从演出中学习。', book(.8,0,22)+group(glasses(),'translate(0 -21)')+nib(40,.43,31,18)),
 'JA22': ('再上弦', '金色弓身与回转箭头，突出完成射击后再次上弦。', path('M43 19Q117 64 43 109L57 64Z','url(#gold)')+line('M43 19L24 64L43 109','#dce6ce',3)+path('M13 80Q-2 35 30 18L28 36L49 14L19 9L27 18Q-3 42 13 80Z','url(#mint)')),
 'JK15': ('反复研习', '两张错叠的研习笔记与回转箭头，重复阅读同一内容。', page(-13,8,.72)+page(7,-1,.72)+line('M48 47H73M48 59H83M48 71H78','#b7935d',4)+path('M84 96Q112 85 109 58L101 65L108 40L123 61L114 59Q122 92 89 107Z','url(#mint)')),
 'A': ('埋头学习', '摊开的书本与高耸的书写笔尖，表现持续阅读并形成心得。', book(1,0,5)+nib(20,.72,21,-25)),
 'JK27': ('关联阅读', '两册相邻的资料书由金色环相连，强调关联查阅。', path('M12 30L42 25L49 108L19 112Z','url(#mint)')+path('M76 21L107 28L101 112L71 105Z','url(#red)')+line('M22 41L35 39M82 39L98 43','#fff0cb',4)+path('M40 49Q63 29 85 52L79 65Q59 47 47 64Z','url(#gold)')+path('M43 66Q66 47 87 69L80 82Q61 66 50 81Z','url(#gold)')),
 'JA35': ('娓娓道来', '三道接续向前的言叶飘带，末端化作明确的进发方向。', path('M15 27H71Q95 27 104 47L85 45Q79 39 64 40H15Z','url(#paper)')+path('M14 53H76L104 66L75 80H14L30 66Z','url(#mint)')+path('M15 94H61Q78 94 87 84L106 85Q93 110 63 110H15Z','url(#paper)')+line('M31 65H78','#fff4d4',4)),
 'JK32': ('掌舵', '木金色船舵与青绿轮心，直接取航海知识的具体物件。', ''.join(group(line('M64 9V119','#dfa864',8),f'rotate({a} 64 64)') for a in (0,60,120))+circle(64,64,35,'url(#gold)',5)+circle(64,64,23,'#294c54',4)+''.join(group(line('M64 38V90','#c99a56',5),f'rotate({a} 64 64)') for a in (0,60,120))+circle(64,64,12,'url(#mint)',4)),
 'JK37C': ('字斟句酌', '书页上的字句由金色放大镜圈选，强调仔细选择和推敲表达。', page(-15,8,.8)+line('M32 60H60M32 76H65M32 92H56','#b09368',4)+path('M96 60L106 59L121 91Q124 101 113 102L96 72Z','url(#gold)')+circle(81,43,28,'url(#gold)')+circle(81,43,19,'url(#blue)',3)+line('M69 40H89M72 49H86','#336c70',4)+line('M73 30L81 27','#f4f6dc',3)),
 'JA45': ('搭箭', '箭杆已扣在绷紧的弓弦上，表现发射前的准备状态。', path('M56 12Q100 65 56 116L67 65Z','url(#gold)')+line('M56 12L33 65L56 116','#e6eed6',3)+arrow(0,.84,1,0)),
 'JK64': ('理清思路', '纯那的眼镜与上方理顺的连线，线头顺畅向前。', group(glasses(),'translate(0 19)')+path('M19 35Q38 12 54 29Q64 41 78 28L72 21L107 22L93 47L88 35Q66 56 49 40Q38 28 25 44Z','url(#gold)')),
 'JK67': ('反复推敲', '斜置的书写笔尖在修改稿上重写，突出反复打磨表达。', page(-2,6,.87)+line('M39 75H72M39 91H83','#a18c68',4)+line('M40 58H66','#a66658',5)+nib(45,.8,9,-15)),
 'JG31': ('连贯练习', '前后相接的两枚舞步足迹与连贯路线，体现台步训练。', path('M22 98Q2 54 35 26M95 103Q125 65 108 25','none','#b4a26f',6)+group(path('M30 18Q51 7 58 28L53 59L26 62L23 38Z','url(#red)')+path('M25 72L50 69L47 88Q29 101 24 85Z','url(#gold)'), 'rotate(-20 39 55)')+group(path('M69 39Q91 24 99 47L96 80L69 84L65 59Z','url(#mint)')+path('M70 93L95 89L93 108Q75 121 70 108Z','url(#gold)'), 'rotate(14 82 75)')),
 'JH19': ('双星', '两颗大小相近的青蓝四角星相邻，分别对应两次出手。', line('M27 90Q64 101 104 39','#6ea5a7',5)+star(42,43,32,'mint')+star(88,84,32,'blue')),
 'JH21': ('长明', '单颗持续明亮的金星被一道护光承托，余光形成防护。', path('M16 67Q26 100 64 114Q102 99 112 67L101 72Q93 94 64 101Q35 94 27 72Z','url(#mint)')+star(64,52,43)+line('M17 29L26 36M111 29L102 36','#f3d38a',4)),
 'JG34': ('合唱练习', '三枚相呼应的音符落在练习用琴键上，突出合唱与节拍。', path('M16 84H111V113H16Z','url(#paper)')+line('M39 87V110M63 87V110M87 87V110','#7a7567',3)+path('M32 84H44V101H32ZM57 84H69V101H57ZM81 84H93V101H81Z','#28434d')+path('M30 69V26L62 18V57Q60 70 45 68Q36 58 49 52V35L40 37V65Q37 79 24 76Q16 68 30 63Z','url(#gold)')+path('M86 57V20L108 29V43L95 38V61Q91 75 79 68Q74 58 86 55Z','url(#mint)')),
 'JK75': ('学以致用', '笔记落在正在练习的琴键旁，表现把所学转化为熟练动作。', path('M17 65L105 48L115 101L26 118Z','url(#paper)')+line('M42 65L51 110M65 60L74 106M87 55L96 102','#806e52',3)+path('M37 64L47 62L52 85L42 87ZM61 60L71 58L76 81L66 83ZM84 55L94 53L99 77L89 79Z','url(#mint)')+page(-11,-31,.52)+line('M37 26H52M37 36H57','#9c855e',3)+star(101,22,14)),
 'JH23': ('星芒护持', '三颗星在弧形护光上聚集，表现较大能量投入后的守备。', group(shield('blue'),'translate(9 26) translate(64 64) scale(.75) translate(-64 -64)')+star(36,36,27)+star(82,29,24,'mint')+star(107,57,18)),
 'JA70': ('掩护射击', '出射的斜箭掠过盾面，射击方向与保护动作同时清楚。', group(shield(),'translate(-11 5) translate(64 64) scale(.8) translate(-64 -64)')+arrow(-32,.95,9,-13,'gold')),
 'JH24': ('星光结界', '五颗星以连线围出保护区域，中央亮星持续供能。', path('M64 14L112 50L95 106H33L16 50Z','#254c5d')+line('M64 14L112 50L95 106H33L16 50Z','#78b8bc',4)+star(64,14,12)+star(112,50,12)+star(95,106,12)+star(33,106,12)+star(16,50,12)+star(64,65,25,'blue')),
 'JK80': ('要点', '三条摘记与突出的重点标记，提炼信息后为下一次运用做好准备。', page()+''.join(circle(43,y,4,'url(#mint)',2)+line(f'M55 {y}H84','#ae956c',4) for y in (48,69,90))+path('M13 48L22 30L31 48L22 66Z','url(#red)')),
 'JK81': ('书签', '一本青绿封皮书夹着显眼的红色书签，留下继续阅读的位置。', path('M25 19H103V110H28Q15 105 20 91V30Q20 20 25 19Z','url(#mint)')+path('M28 92H103V110H28Q15 103 28 92Z','url(#paper)')+line('M32 25V83','#d6e1ae',3)+path('M69 20H91V80L80 70L69 80Z','url(#red)')+line('M35 101H96','#b4a174',3)),
 'JZ05': ('即兴', '在交错舞台线条上从一次亮点接出新箭，表现当场接续的出手。', line('M12 97L54 114L112 89L72 73L12 97L12 78L52 61','#719b97',5)+star(39,46,25)+arrow(-30,.85,15,-10)),
 'JZ06': ('晨星', '一颗青白晨星升在弧形地平线上，标记回合开始的星光汇聚。', path('M13 91Q62 65 115 91L109 108Q64 87 19 108Z','url(#blue)')+star(64,43,34,'blue')+line('M21 66L12 60M106 66L116 60','#cde8c0',4)),
 'JH30': ('星芒', '金色中心星向四方射出清晰光芒，对应能量流动引发伤害。', star(64,64,38)+path('M64 7L70 27H58ZM121 64L101 70V58ZM64 121L58 101H70ZM7 64L27 58V70Z','url(#mint)')+line('M24 24L35 35M93 93L104 104M24 104L35 93M93 35L104 24','#e9bd6f',5)),
 'JK46': ('持续阅读', '翻起的新书页和连续露出的页边，表现阅读推进并形成笔锋。', book(.95,0,5)+path('M65 41Q80 17 107 17Q95 34 102 65Q80 67 65 93Z','url(#paper)')+line('M77 43Q85 32 95 30M77 57L94 46','#b89b67',3)),
 'JG23': ('耐力训练', '运动鞋与向前的弧形跑迹，直接采用纯那晨跑的日常训练契合。', path('M16 77L30 42L55 50L62 70L99 79Q116 85 114 104H18Q9 93 16 77Z','url(#red)')+path('M16 96H114V109H17Z','url(#paper)')+line('M41 57L53 61M37 68L56 73M81 81L80 95','#f8e6b2',4)+line('M14 30Q47 9 79 26','#88bbae',5)+path('M72 14L91 30L69 38Z','url(#mint)')),
 'ENERGY_RESERVE': ('能量保留', '掌心托住星光，围拢的手指强调将剩余行动力量留到下回合。', path('M15 78L29 68L44 76L65 83L87 77Q99 72 106 81L88 98L59 111L30 98L15 94Z','url(#paper)')+line('M42 78L63 90L85 85','#bc965f',4)+star(64,43,31,'blue')),
 'ENERGY_NEXT_TURN': ('下回合能量', '星光停在有刻度的时间弧中，等待下一回合点亮。', path('M29 28Q69-2 103 33Q130 65 103 101Q68 131 32 105L44 90Q71 112 94 89Q116 61 91 38Q66 14 41 41Z','url(#blue)')+path('M16 16L45 24L30 51Z','url(#gold)')+line('M100 65H109M69 100V110','#e9e8c4',4)+star(61,65,25)),
 'VULNERABLE': ('易伤', '裂开的盾牌被箭头贯穿，以射击暴露防御破绽。', shield('red')+path('M68 34L53 59L73 71L55 99','none','#fff0c4',6)+arrow(-36,.72,11,-25,'gold')),
 'WEAK': ('虚弱', '弯折下垂的箭杆与失去张力的羽翼，表现出手乏力。', path('M25 14L54 29L47 60L72 72L82 58L88 100L48 89L65 81L32 65L37 38L18 29Z','url(#red)')+line('M91 19L108 36M83 34L102 51','#a4bcb0',5)+path('M15 79L28 71L29 95L17 108Z','url(#mint)')),
}

DEFS='''<defs>
<linearGradient id="gold" x1="0" y1="0" x2=".6" y2="1" objectBoundingBox="true"><stop stop-color="#fff0aa"/><stop offset=".45" stop-color="#dfb868"/><stop offset="1" stop-color="#98703e"/></linearGradient>
<linearGradient id="mint" x1="0" y1="0" x2=".7" y2="1"><stop stop-color="#c1e7bf"/><stop offset=".5" stop-color="#6eae98"/><stop offset="1" stop-color="#326e73"/></linearGradient>
<linearGradient id="paper" x1="0" y1="0" x2=".5" y2="1"><stop stop-color="#fff3cf"/><stop offset=".6" stop-color="#e9d5a3"/><stop offset="1" stop-color="#bd9e68"/></linearGradient>
<linearGradient id="blue" x1="0" y1="0" x2=".7" y2="1"><stop stop-color="#e0f1d3"/><stop offset=".45" stop-color="#84c9d0"/><stop offset="1" stop-color="#457d9e"/></linearGradient>
<linearGradient id="red" x1="0" y1="0" x2=".6" y2="1"><stop stop-color="#edc7a2"/><stop offset=".4" stop-color="#c77d68"/><stop offset="1" stop-color="#854f51"/></linearGradient>
</defs>'''

def build():
    import cairosvg
    RAW.mkdir(parents=True,exist_ok=True); SVG.mkdir(parents=True,exist_ok=True)
    entries=[]
    for key,(name,fit,body) in ICONS.items():
        version=2 if key in {'JH23','JH24','JK37C'} else 1
        if key in {'JH23','JH24'}:body=group(body,'translate(64 64) scale(.9) translate(-64 -64)')
        svg=f'<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128" viewBox="0 0 128 128">{DEFS}{body}</svg>'
        source=SVG/f'{key}.svg'; original=RAW/f'{key}.png'
        source.write_text(svg,encoding='utf-8')
        cairosvg.svg2png(bytestring=svg.encode(),write_to=str(original))
        entries.append(dict(key=key,name=name,fit=fit,version=version,svg=str(source.relative_to(ROOT)).replace('\\','/'),original=str(original.relative_to(ROOT)).replace('\\','/'),icon=f'docs/design/assets/powers/2026-11-05/icons/{key}.png'))
    (OUT/'manifest.json').write_text(json.dumps({'size':[128,128],'style':'透明底、深色实体轮廓、金属与纸张分面；按卡牌当前契合绘制具体物件。','icons':entries},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    contact(RAW,OUT/'originals-contact.png')
    print(f'Created {len(entries)} SVG originals and 128px PNG originals.')

def contact(folder,destination):
    board=Image.new('RGB',(1120,math.ceil(len(ICONS)/7)*206),'#17252d');draw=ImageDraw.Draw(board)
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',16)
    for i,(key,(name,_,_)) in enumerate(ICONS.items()):
        x=(i%7)*160;y=(i//7)*206
        im=Image.open(folder/f'{key}.png').convert('RGBA');board.paste(im,(x+16,y+3),im)
        small=im.resize((32,32),Image.Resampling.LANCZOS);board.paste(small,(x+63,y+164),small)
        draw.text((x+80,y+140),name,anchor='mm',fill='#e9dec0',font=font)
    board.save(destination)

if __name__=='__main__':build()
