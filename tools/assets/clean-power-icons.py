"""Apply the installed Artifact Cleaner to Power icons without moving their alpha edges."""
from pathlib import Path
import argparse
import hashlib
import json
import sys
import time

ROOT=Path(__file__).resolve().parents[2]
CONFIG=json.loads((ROOT/'output/artifact-cleanup/2026-11-02/config.json').read_text(encoding='utf-8'))
sys.path.insert(0,CONFIG['dependencies'])
sys.path.insert(0,CONFIG['tool_root'])
import numpy as np
import torch
from PIL import Image
from diffusers import AutoencoderKLFlux2
from modeling import LatentR,build_fns,restore

parser=argparse.ArgumentParser();parser.add_argument('--keys',default='');parser.add_argument('--base',default='docs/design/assets/powers/2026-11-05')
args=parser.parse_args();BASE=ROOT/args.base
requested=set(filter(None,args.keys.split(',')))
manifest=json.loads((BASE/'manifest.json').read_text(encoding='utf-8'))
specs={entry['key']:entry for entry in manifest['icons']}
STRENGTH=.25
torch.set_num_threads(4)
device='cuda' if torch.cuda.is_available() else 'cpu'
checkpoint=torch.load(Path(CONFIG['tool_root'])/'weights/latent_residual.pt',map_location='cpu',weights_only=True)
residual=LatentR(32)
residual.load_state_dict(checkpoint['R_ema'])
residual.to(device).eval().requires_grad_(False)
vae=AutoencoderKLFlux2.from_pretrained(CONFIG['vae_root'],local_files_only=True).to(device).eval().requires_grad_(False)
enc,dec=build_fns(vae,False)
out=BASE/'icons';out.mkdir(parents=True,exist_ok=True)
report=BASE/'cleanup.json'
rows=[row for row in json.loads(report.read_text(encoding='utf-8'))['assets'] if row['key'] not in requested] if requested and report.exists() else []
for source in sorted((BASE/'originals').glob('*.png')):
    if requested and source.stem not in requested:continue
    start=time.monotonic()
    original=np.asarray(Image.open(source).convert('RGBA'))
    native=bool(specs.get(source.stem,{}).get('nativeSource'))
    rgb=original[...,:3] if native else restore(original[...,:3],enc,dec,residual,STRENGTH,device,torch.bfloat16 if device=='cuda' else torch.float32)
    # Solid vector contours and antialias pixels stay exact; the cleaner works on colored interiors.
    interior=(original[...,3]==255)&(original[...,:3].max(axis=2)>75)
    merged=original.copy()
    merged[interior,:3]=np.rint(.75*original[interior,:3]+.25*rgb[interior]).clip(0,255).astype(np.uint8)
    target=out/source.name
    Image.fromarray(merged).save(target)
    assert original.shape==merged.shape and np.array_equal(original[...,3],merged[...,3])
    assert np.array_equal(original[~interior],merged[~interior])
    row=dict(key=source.stem,source=str(source.relative_to(ROOT)).replace('\\','/'),file=str(target.relative_to(ROOT)).replace('\\','/'),
             size=[original.shape[1],original.shape[0]],strength=0 if native else STRENGTH,interior_blend=0 if native else .25,mode='native-reuse' if native else 'artifact-cleaner',alpha_identical=True,contours_identical=True,
             source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
             seconds=round(time.monotonic()-start,3))
    rows.append(row)
    print(json.dumps(dict(key=source.stem,seconds=row['seconds'])),flush=True)
    (BASE/'cleanup.json').write_text(json.dumps(dict(tool=CONFIG['tool'],tool_url=CONFIG['tool_url'],local_only=True,device=device,
        tool_root=CONFIG['tool_root'],vae_root=CONFIG['vae_root'],originals_preserved=True,assets=rows),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
from importlib.util import spec_from_file_location,module_from_spec
spec=spec_from_file_location('power_icons',ROOT/('tools/assets/refine-power-icons-native.py' if manifest.get('styleReference') else 'tools/assets/generate-power-icons.py'))
module=module_from_spec(spec);spec.loader.exec_module(module)
module.contact(out,BASE/'contact.png')
print(f'Cleaned {len(rows)} Power icons; alpha, contours and dimensions preserved.',flush=True)
