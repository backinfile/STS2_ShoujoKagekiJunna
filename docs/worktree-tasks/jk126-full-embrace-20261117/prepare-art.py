"""Crop the archived subtitle-free episode 9 frame and render native card layers."""
from pathlib import Path
from PIL import Image, ImageOps, ImageDraw, ImageFont
import base64, io, json, hashlib, subprocess, math, sys, os

ROOT = Path(__file__).resolve().parents[3]
TASK = Path(__file__).parent
SOURCE = TASK / 'source-tv09-hug-closeup.jpg'
TARGET = ROOT / 'ShoujoKagekiJunna/images/cards/JK126.jpg'
CROP = (0, 0, 960, 720)
with Image.open(SOURCE) as source:
    assert source.size == (1280, 720)
    portrait = source.crop(CROP).resize((960, 720), Image.Resampling.LANCZOS)
    portrait.save(TARGET, quality=95, subsampling=0)

assets = json.loads((ROOT / 'docs/design/workbench/assets.json').read_text(encoding='utf-8'))
def asset(key):
    return Image.open(io.BytesIO(base64.b64decode(assets[key].split(',', 1)[1]))).convert('RGBA')
font_path = io.BytesIO(base64.b64decode(assets['game-regular'].split(',', 1)[1]))
def font(size):
    font_path.seek(0)
    return ImageFont.truetype(str(Path(os.environ['WINDIR']) / 'Fonts/msyh.ttc'), size)

def card(picture, upgraded=False):
    out = Image.new('RGBA', (600, 844))
    def layer(im, xy, size):
        out.alpha_composite(im.resize(size, Image.Resampling.LANCZOS), xy)
    layer(ImageOps.fit(picture.convert('RGBA'), (500, 380), centering=(.5, 0)), (50, 86), (500, 380))
    layer(asset('frame-skill'), (0, 0), (600, 844))
    layer(asset('portrait-skill-rare'), (25, 94), (550, 420))
    layer(asset('banner-rare'), (-26, 8), (654, 166))
    layer(asset('plaque-rare'), (239, 424), (122, 74))
    energy = Image.open(ROOT / 'docs/design/assets/arrow/2026-11-02/junna-energy-workbench-v4.png').convert('RGBA')
    layer(energy, (-32, -32), (128, 128))
    draw = ImageDraw.Draw(out)
    draw.text((300, 70), '全都一起带去' + ('+' if upgraded else ''), font=font(49), anchor='mm', fill='#7fff00' if upgraded else '#fff6e2', stroke_width=3, stroke_fill='#6b4b00')
    draw.text((300, 460), '技能', font=font(32), anchor='mm', fill='#24221e')
    draw.text((32, 35), '1', font=font(64), anchor='mm', fill='#fff6e2', stroke_width=4, stroke_fill='#5c5440')
    block = 12 if upgraded else 8
    draw.multiline_text((300, 624), f'连锁知识。\n获得{block}点格挡。\n抽取所有关联牌。', font=font(39), anchor='mm', align='center', spacing=12, fill='#fff6e2', stroke_width=1, stroke_fill='#302d25')
    return out

before = Image.open(io.BytesIO(subprocess.check_output(['git','show','be9ae67470de2021ba25b2a1ab8a119d6d240b52:ShoujoKagekiJunna/images/cards/JK126.jpg'], cwd=ROOT)))
after = Image.open(TARGET)
board = Image.new('RGB', (1200, 930), '#ebe8e0')
draw = ImageDraw.Draw(board)
for x, picture, title in [(0, before, '调整前'), (600, after, '调整后')]:
    draw.text((x+300, 25), title, font=font(32), anchor='mm', fill='#292b30')
    image = card(picture)
    board.paste(image, (x, 70), image)
board.save(TASK / 'card-preview.jpg', quality=93)
small = Image.new('RGB', (660, 490), '#ebe8e0')
for i, upgraded in enumerate([False, True]):
    image = card(after, upgraded).resize((300,422),Image.Resampling.LANCZOS)
    small.paste(image,(20+i*320,30),image)
small.save(TASK / 'card-preview-small.jpg', quality=95)

manifest = dict(cardId='JK126', assetId='original-tv-jk126-hug-closeup-wide-20261117',
    source=str(SOURCE.relative_to(ROOT)).replace('\\','/'), sourceSize=[1280,720],
    sourcePage='https://benigmatica.wordpress.com/2018/09/08/shoujo-kageki-revue-starlight-episode-09/',
    sourceImage='https://i.imgur.com/gJDQd8J.jpg',
    work='TV第9话约21:40—22:03，纯那拥住奈奈的大脸近景',
    sceneReference='docs/design/assets/card-sources/2026-11-02/tv09-2140-new.jpg',
    crop=[0,0,960,720], outputSize=[960,720], resampling='Pillow LANCZOS',
    encoding={'format':'JPEG','quality':95,'subsampling':0},
    output=str(TARGET.relative_to(ROOT)).replace('\\','/'),
    sourceSha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
    outputSha256=hashlib.sha256(TARGET.read_bytes()).hexdigest(),
    processing='原作无字幕近景原帧，按原像素裁切为960×720。')
(TASK/'art-source.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n',encoding='utf-8')
print(json.dumps(manifest, ensure_ascii=False))
