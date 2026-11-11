"""Clean generated potion art with the installed Artifact Cleaner; preserve alpha and contours."""
import argparse, hashlib, json, pathlib, sys, time
parser=argparse.ArgumentParser()
parser.add_argument('--config',required=True)
parser.add_argument('--base',required=True)
parser.add_argument('--expected-count',type=int,default=3)
args=parser.parse_args()
base=pathlib.Path(args.base).resolve()
repo=next(parent for parent in base.parents if (parent/'.git').exists())
config=json.loads(pathlib.Path(args.config).read_text(encoding='utf-8-sig'))
sys.path.insert(0,config['dependencies'])
sys.path.insert(0,config['tool_root'])
import numpy as np
import torch
from PIL import Image,ImageDraw,ImageFont
from diffusers import AutoencoderKLFlux2
from modeling import LatentR,build_fns,restore
torch.set_num_threads(4)
device='cuda' if torch.cuda.is_available() else 'cpu'
checkpoint=torch.load(pathlib.Path(config['tool_root'])/'weights/latent_residual.pt',map_location='cpu',weights_only=True)
residual=LatentR(32)
residual.load_state_dict(checkpoint['R_ema'])
residual.to(device).eval().requires_grad_(False)
vae=AutoencoderKLFlux2.from_pretrained(config['vae_root'],local_files_only=True).to(device).eval().requires_grad_(False)
vae.enable_tiling()
enc,dec=build_fns(vae,False)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
relative=lambda p:p.relative_to(repo).as_posix()
rows=[]
for source in sorted((base/'originals').glob('*.png')):
    started=time.monotonic()
    src=np.asarray(Image.open(source).convert('RGBA'))
    cleaned=restore(src[...,:3],enc,dec,residual,.25,device,torch.bfloat16 if device=='cuda' else torch.float32)
    interior=(src[...,3]>=250)&(src[...,:3].max(axis=2)>75)
    merged=src.copy()
    merged[interior,:3]=np.rint(.75*src[interior,:3]+.25*cleaned[interior]).clip(0,255).astype(np.uint8)
    target=base/'cleaned-native'/source.name
    target.parent.mkdir(parents=True,exist_ok=True)
    Image.fromarray(merged).save(target)
    assert np.array_equal(src[...,3],merged[...,3])
    assert np.array_equal(src[~interior],merged[~interior])
    final=Image.fromarray(merged)
    bbox=final.getchannel('A').point(lambda a:255 if a>8 else 0).getbbox()
    assert bbox
    cropped=final.crop(bbox)
    cropped.thumbnail((232,232),Image.Resampling.LANCZOS)
    icon=Image.new('RGBA',(256,256),(0,0,0,0))
    icon.alpha_composite(cropped,((256-cropped.width)//2,(256-cropped.height)//2))
    icon_path=base/'icons'/source.name
    icon_path.parent.mkdir(parents=True,exist_ok=True)
    icon.save(icon_path)
    visible=src[...,3]>127
    delta=np.abs(src[...,:3].astype(np.int16)-merged[...,:3].astype(np.int16))
    row={'key':source.stem,'source':relative(source),'cleanedNative':relative(target),'file':relative(icon_path),
         'originalSize':[src.shape[1],src.shape[0]],'iconSize':[256,256],'strength':.25,'interior_blend':.25,
         'alpha_identical':True,'contours_identical':True,'eligibleAlphaMinimum':250,'source_sha256':sha(source),'cleaned_sha256':sha(target),
         'sha256':sha(icon_path),'visibleRgbMeanAbsoluteChange':round(float(delta[visible].mean()),4),
         'seconds':round(time.monotonic()-started,3)}
    rows.append(row)
    (base/'cleanup.json').write_text(json.dumps({'tool':config['tool'],'tool_url':config['tool_url'],'local_only':True,
         'device':device,'precision':'bfloat16' if device=='cuda' else 'float32','originals_preserved':True,
         'dimensionsPreservedDuringCleanup':True,'vae':'Flux2 VAE','residual':'LatentR R_ema','assets':rows},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'key':source.stem,'seconds':row['seconds'],'alpha_identical':True,'contours_identical':True}),flush=True)
    if device=='cuda':torch.cuda.empty_cache()
assert len(rows)==args.expected_count
print(f'Cleaned {len(rows)} potion icons at original resolution; final icons are 256x256 RGBA.',flush=True)
