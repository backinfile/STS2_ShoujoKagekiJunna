"""Compare approved CUNet denoising with TTA and newer SwinUNet v3."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import time

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/"output/cleanup-research/2026-11-02/advanced"
OLD=OUT.parent/"waifu2x"
CFG=json.loads((ROOT/"output/artifact-cleanup/2026-11-02/config.json").read_text(encoding="utf-8"))
ITEMS=[x for x in CFG["items"] if x["id"] in ("idle","ui-energy")]
TOOL=ROOT/".tools/waifu2x/waifu2x-ncnn-vulkan-20250915-windows"
OUT.mkdir(parents=True,exist_ok=True)
(OUT/"raw").mkdir(exist_ok=True)
REPORT=OUT/"results.json"
rows=json.loads(REPORT.read_text(encoding="utf-8")) if REPORT.exists() else []

def save(item, rgb, method, seconds, params):
    original=np.asarray(Image.open(item["source"]).convert("RGBA"))
    assert rgb.shape==original[...,:3].shape
    rgba=np.dstack([rgb, original[...,3]])
    target=OUT/f'{item["id"]}-{method}.png'
    Image.fromarray(rgba).save(target)
    check=np.asarray(Image.open(target))
    assert np.array_equal(check[...,3],original[...,3])
    row=dict(id=item["id"],method=method,params=params,file=target.name,
             source=item["source"],source_sha256=hashlib.sha256(Path(item["source"]).read_bytes()).hexdigest(),
             dimensions=list(Image.open(target).size),alpha_identical=True,seconds=round(seconds,2))
    global rows
    rows=[r for r in rows if (r["id"],r["method"])!=(item["id"],method)]+[row]
    REPORT.write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding="utf-8")
    print(json.dumps(row,ensure_ascii=False),flush=True)

def tta():
    for item in ITEMS:
        started=time.monotonic()
        raw=OUT/"raw"/f'{item["id"]}-cunet3-tta.png'
        cmd=[str(TOOL/"waifu2x-ncnn-vulkan.exe"),"-i",item["source"],"-o",str(raw),
             "-n","3","-s","1","-m",str(TOOL/"models-cunet"),"-t","128",
             "-j","1:1:1","-g","0","-x"]
        p=subprocess.run(cmd,cwd=OUT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,
                         creationflags=subprocess.CREATE_NO_WINDOW)
        (raw.with_suffix(".log")).write_bytes(p.stdout)
        if p.returncode:
            raise RuntimeError(p.stdout.decode("utf-8",errors="replace")[-2000:])
        save(item,np.asarray(Image.open(raw).convert("RGB")),"cunet3-tta",
             time.monotonic()-started,dict(noise=3,scale=1,tta=True,model="CUNet"))

def swin():
    sys.path.insert(0,str(ROOT/".tools/nunif-source/nunif-master"))
    import torch
    from waifu2x import Waifu2x
    torch.set_num_threads(4)
    model_dir=ROOT/".tools/nunif-models-v3/pretrained_models/swin_unet_v3/art"
    ctx=Waifu2x(str(model_dir),[0])
    ctx.load_model("noise",3)
    print("SwinUNet v3 noise3 loaded",flush=True)
    for item in ITEMS:
        started=time.monotonic()
        image=np.asarray(Image.open(item["source"]).convert("RGB")).copy()
        tensor=torch.from_numpy(image).permute(2,0,1).float().div(255)
        with torch.inference_mode():
            rgb,_=ctx.convert(tensor,None,"noise",3,tile_size=256,batch_size=1,
                              tta=False,enable_amp=True,output_device="cpu")
        arr=rgb.permute(1,2,0).float().clamp(0,1).mul(255).round().numpy().astype(np.uint8)
        save(item,arr,"swin-v3-3",time.monotonic()-started,
             dict(noise=3,scale=1,tta=False,tile=256,amp=True,model="SwinUNet v3/art"))
    del ctx
    torch.cuda.empty_cache()

def review():
    font=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",22)
    for item in ITEMS:
        paths=[Path(item["source"]),OLD/f'{item["id"]}-n3.png',
               OUT/f'{item["id"]}-cunet3-tta.png',OUT/f'{item["id"]}-swin-v3-3.png']
        images=[Image.open(p).convert("RGBA") for p in paths]
        board=Image.new("RGB",(1600,1540),"#e9ecf0")
        draw=ImageDraw.Draw(board)
        labels=["原图","CUNet 3（基准）","CUNet 3 + TTA","SwinUNet v3 3"]
        for col,(im,label) in enumerate(zip(images,labels)):
            draw.text((col*400+30,10),label,font=font,fill="#272b31")
            thumb=im.copy()
            thumb.thumbnail((380,570),Image.Resampling.LANCZOS)
            board.paste(thumb,(col*400+(400-thumb.width)//2,50),thumb)
            for index,(x,y,w,h) in enumerate(item["regions"]):
                crop=im.crop((x,y,x+w,y+h))
                board.paste(crop,(col*400+20,640+index*290),crop)
        board.save(OUT/f'{item["id"]}-comparison.png')

if __name__=="__main__":
    p=argparse.ArgumentParser()
    p.add_argument("phase",choices=["tta","swin","review"])
    phase=p.parse_args().phase
    {"tta":tta,"swin":swin,"review":review}[phase]()
