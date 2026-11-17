import argparse, hashlib, json, sys, time
from pathlib import Path
p=argparse.ArgumentParser()
p.add_argument('--local-config',required=True)
p.add_argument('--source',required=True)
p.add_argument('--output',required=True)
p.add_argument('--report',required=True)
p.add_argument('--alpha',type=float,default=0.25)
a=p.parse_args()
cfg=json.loads(Path(a.local_config).read_text(encoding='utf-8-sig'))
sys.path.insert(0,cfg['dependencies'])
sys.path.insert(0,cfg['tool_root'])
import numpy as np
from PIL import Image
import torch
from diffusers import AutoencoderKLFlux2
from modeling import LatentR, build_fns, restore
torch.set_num_threads(4)
device='cuda' if torch.cuda.is_available() else 'cpu'
started=time.monotonic()
checkpoint=torch.load(Path(cfg['tool_root'])/'weights/latent_residual.pt',map_location='cpu',weights_only=True)
residual=LatentR(32)
residual.load_state_dict(checkpoint['R_ema'])
residual.to(device).eval().requires_grad_(False)
vae=AutoencoderKLFlux2.from_pretrained(cfg['vae_root'],local_files_only=True).to(device).eval().requires_grad_(False)
vae.enable_tiling()
enc,dec=build_fns(vae,False)
source=Image.open(a.source).convert('RGBA')
arr=np.asarray(source)
result=restore(arr[...,:3],enc,dec,residual,a.alpha,device,torch.bfloat16 if device=='cuda' else torch.float32)
assert result.shape==arr[...,:3].shape
Image.fromarray(np.dstack([result,arr[...,3]])).save(a.output)
sha=lambda x:hashlib.sha256(Path(x).read_bytes()).hexdigest()
report={'tool':'GPT Image 2 Artifact Cleaner','toolURL':'https://github.com/Larryvrh/gpt-image-2-artifact-cleaner','alpha':a.alpha,'device':device,'size':list(source.size),'originalPreserved':True,'alphaPreserved':True,'source':Path(a.source).name,'sourceSHA256':sha(a.source),'output':Path(a.output).name,'outputSHA256':sha(a.output),'seconds':round(time.monotonic()-started,2)}
Path(a.report).write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
