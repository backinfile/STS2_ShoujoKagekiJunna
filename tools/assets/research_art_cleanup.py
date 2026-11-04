"""Compare a portable anime denoiser on two native-resolution originals."""
from pathlib import Path
import hashlib
import json
import subprocess
import time
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/"output/cleanup-research/2026-11-02"
TOOL=ROOT/".tools/waifu2x/waifu2x-ncnn-vulkan-20250915-windows"
CFG=json.loads((ROOT/"output/artifact-cleanup/2026-11-02/config.json").read_text(encoding="utf-8"))
FONT=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",22)
OUT.mkdir(parents=True,exist_ok=True)
(OUT/"raw").mkdir(exist_ok=True)
(OUT/"waifu2x").mkdir(exist_ok=True)
rows=[]
for item in CFG["items"]:
    if item["id"] not in ("idle","ui-energy"):
        continue
    source=Path(item["source"])
    original=np.asarray(Image.open(source).convert("RGBA"))
    for level in (1,3):
        raw=OUT/"raw"/f'{item["id"]}-n{level}.png'
        target=OUT/"waifu2x"/raw.name
        started=time.monotonic()
        command=[str(TOOL/"waifu2x-ncnn-vulkan.exe"),"-i",str(source),"-o",str(raw),
                 "-n",str(level),"-s","1","-m",str(TOOL/"models-cunet"),
                 "-t","128","-j","1:1:1","-g","0"]
        result=subprocess.run(command,cwd=OUT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,
                              creationflags=subprocess.CREATE_NO_WINDOW)
        (OUT/"raw"/f'{item["id"]}-n{level}.log').write_bytes(result.stdout)
        if result.returncode:
            raise RuntimeError(result.stdout.decode("utf-8",errors="replace")[-2000:])
        rgb=np.asarray(Image.open(raw).convert("RGB"))
        assert rgb.shape==original[...,:3].shape
        final=np.dstack([rgb,original[...,3]])
        Image.fromarray(final).save(target)
        reloaded=np.asarray(Image.open(target).convert("RGBA"))
        assert np.array_equal(reloaded[...,3],original[...,3])
        valid=original[...,3]>=250
        mae=float(np.abs(reloaded[...,:3].astype(float)-original[...,:3]).mean(axis=2)[valid].mean())
        row=dict(id=item["id"],tool="waifu2x-ncnn-vulkan 20250915 / CUNet",noise=level,
                 scale=1,source=str(source),source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                 file=str(target.relative_to(OUT)),alpha_identical=True,dimensions=Image.open(target).size,
                 visible_rgb_mae=round(mae,4),seconds=round(time.monotonic()-started,2))
        rows.append(row)
        print(json.dumps(row,ensure_ascii=False),flush=True)
    images=[Image.open(p).convert("RGBA") for p in
            [source,OUT/"waifu2x"/f'{item["id"]}-n1.png',OUT/"waifu2x"/f'{item["id"]}-n3.png']]
    board=Image.new("RGB",(1200,1540),"#e9ecf0")
    draw=ImageDraw.Draw(board)
    for col,(im,label) in enumerate(zip(images,["原图","waifu2x 降噪 1","waifu2x 降噪 3"])):
        draw.text((col*400+35,10),label,font=FONT,fill="#272b31")
        thumb=im.copy()
        thumb.thumbnail((380,570),Image.Resampling.LANCZOS)
        board.paste(thumb,(col*400+(400-thumb.width)//2,50),thumb)
        for index,(x,y,w,h) in enumerate(item["regions"]):
            crop=im.crop((x,y,x+w,y+h))
            board.paste(crop,(col*400+20,640+index*290),crop)
    board.save(OUT/f'{item["id"]}-waifu2x-comparison.png')
(OUT/"waifu2x-results.json").write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding="utf-8")
