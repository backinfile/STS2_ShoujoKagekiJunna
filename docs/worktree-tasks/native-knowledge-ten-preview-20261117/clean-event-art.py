"""Clean one generated event illustration using the installed local Artifact Cleaner."""
import argparse, hashlib, json, sys, time
from pathlib import Path
parser=argparse.ArgumentParser()
parser.add_argument("--config", required=True)
parser.add_argument("--strength", type=float, default=.25)
args=parser.parse_args()
root=Path(__file__).resolve().parents[3]
cfg=json.loads(Path(args.config).read_text(encoding="utf-8"))
sys.path.insert(0,cfg["dependencies"])
sys.path.insert(0,cfg["tool_root"])
import numpy as np
import torch
from PIL import Image, ImageOps
from diffusers import AutoencoderKLFlux2
from modeling import LatentR, build_fns, restore
source=root/"docs/assets/events/knowledge-possibility-original.png"
cleaned=root/"out/tmp/knowledge-possibility-art/cleaned-native.png"
target=root/"ShoujoKagekiJunna/images/events/knowledge_possibility.png"
report=Path(__file__).with_name("event-art-cleanup.json")
torch.set_num_threads(4)
device="cuda" if torch.cuda.is_available() else "cpu"
start=time.monotonic()
residual=LatentR(32)
checkpoint=torch.load(Path(cfg["tool_root"])/"weights/latent_residual.pt",map_location="cpu",weights_only=True)
residual.load_state_dict(checkpoint["R_ema"])
residual.to(device).eval().requires_grad_(False)
vae=AutoencoderKLFlux2.from_pretrained(cfg["vae_root"],local_files_only=True).to(device).eval().requires_grad_(False)
enc,dec=build_fns(vae,False)
original=np.asarray(Image.open(source).convert("RGBA")).copy()
rgb=restore(original[...,:3],enc,dec,residual,args.strength,device,torch.bfloat16 if device=="cuda" else torch.float32)
merged=np.dstack([rgb,original[...,3]]).astype(np.uint8)
assert merged.shape==original.shape
assert np.array_equal(merged[...,3],original[...,3])
cleaned.parent.mkdir(parents=True,exist_ok=True)
Image.fromarray(merged).save(cleaned)
target.parent.mkdir(parents=True,exist_ok=True)
ImageOps.fit(Image.fromarray(merged),(3440,1616),method=Image.Resampling.LANCZOS,centering=(.5,.5)).save(target)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
summary=dict(tool="GPT Image 2 Artifact Cleaner",tool_url="https://github.com/Larryvrh/gpt-image-2-artifact-cleaner",strength=args.strength,device=device,source=str(source.relative_to(root)).replace("\\","/"),source_size=[original.shape[1],original.shape[0]],source_sha256=sha(source),cleaned=str(cleaned.relative_to(root)).replace("\\","/"),cleaned_size=[merged.shape[1],merged.shape[0]],alpha_identical=True,mean_absolute_rgb_change=round(float(np.abs(merged[...,:3].astype(np.int16)-original[...,:3].astype(np.int16)).mean()),4),final=str(target.relative_to(root)).replace("\\","/"),final_size=list(Image.open(target).size),final_sha256=sha(target),fit="center crop to aspect ratio, then Lanczos to native event canvas",seconds=round(time.monotonic()-start,3))
report.write_text(json.dumps(summary,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
print(json.dumps(summary,ensure_ascii=False),flush=True)
