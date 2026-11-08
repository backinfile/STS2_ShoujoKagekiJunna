"""Clean generated Power artwork locally, then export normalized transparent icons."""
from pathlib import Path
import argparse
import hashlib
import json
import sys
import time

BASE = Path(__file__).resolve().parent
ROOT = BASE.parents[4]
parser = argparse.ArgumentParser()
parser.add_argument('--preview-only', action='store_true')
args = parser.parse_args()
cfg = json.loads((ROOT / 'output/artifact-cleanup/2026-11-02/config.json').read_text(encoding='utf-8'))
sys.path.insert(0, cfg['dependencies'])
sys.path.insert(0, cfg['tool_root'])
import numpy as np
from PIL import Image, ImageDraw, ImageFont

plan = json.loads((BASE / 'design-plan.json').read_text(encoding='utf-8'))
rows = json.loads((BASE / 'cleanup.json').read_text(encoding='utf-8'))['assets'] if (BASE / 'cleanup.json').exists() else []
done = {r['key']:r for r in rows}
for folder in ['cleaned-full', 'icons', 'comparisons']:
    (BASE / folder).mkdir(exist_ok=True)
if not args.preview_only:
    import torch
    from diffusers import AutoencoderKLFlux2
    from modeling import LatentR, build_fns, restore
    torch.set_num_threads(4)
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    checkpoint = torch.load(Path(cfg['tool_root']) / 'weights/latent_residual.pt', map_location='cpu', weights_only=True)
    residual = LatentR(32)
    residual.load_state_dict(checkpoint['R_ema'])
    residual.to(device).eval().requires_grad_(False)
    vae = AutoencoderKLFlux2.from_pretrained(cfg['vae_root'], local_files_only=True).to(device).eval().requires_grad_(False)
    vae.enable_tiling()
    enc, dec = build_fns(vae, False)
    for spec in plan['icons']:
        key = spec['key']
        source = BASE / 'generated' / (key + '.png')
        if not source.exists():
            continue
        if key in done and done[key]['source_sha256'] == hashlib.sha256(source.read_bytes()).hexdigest() and (key != 'JA35' or done[key].get('alphaRepair', {}).get('recipeVersion') == 3):
            continue
        started = time.monotonic()
        raw = np.asarray(Image.open(source).convert('RGBA')).copy()
        source_alpha = raw[..., 3].copy()
        alpha_repair = None
        if key == 'JA35':
            # Local artifact removal: the generator left an accidental transparency
            # island inside the front bubble's otherwise solid right color plane.
            # Reconstruct that plane from its opaque neighbors; preserve every
            # exterior pixel and intentional gap. The untouched generation is kept.
            from scipy import ndimage
            region = raw[380:940, 540:950]
            color = region[..., :3].astype(float)
            plane = (color[..., 1] > 1.8*color[..., 0]) & (color[..., 1] > 1.2*color[..., 2]) & (color[..., 1] > 100)
            labels, _ = ndimage.label(plane)
            seed = int(labels[220,260])
            assert seed, 'Missing front bubble color plane'
            plane = labels == seed
            good = plane & (region[..., 3] >= 250)
            assert good.sum() > 100, 'Insufficient opaque color reference pixels'
            restored = np.rint(np.median(region[good,:3],axis=0)).astype('uint8')
            damaged = ndimage.binary_erosion(plane, iterations=2)
            changed = int(((region[..., 3] != 255) & damaged).sum())
            region[damaged, :3] = restored
            region[damaged, 3] = 255
            alpha_repair = dict(recipeVersion=3, method='local solid color-plane reconstruction from near-opaque neighbors', bounds=[540,380,950,940], repairedAlphaPixels=changed, color=restored.tolist(), exterior_unchanged=True)
        rgb = restore(raw[..., :3], enc, dec, residual, .25, device, torch.bfloat16 if device == 'cuda' else torch.float32)
        interior = (raw[..., 3] == 255) & (raw[..., :3].max(axis=2) > 75)
        cleaned = raw.copy()
        cleaned[interior, :3] = np.rint(.75 * raw[interior, :3] + .25 * rgb[interior]).clip(0, 255).astype(np.uint8)
        assert np.array_equal(raw[..., 3], cleaned[..., 3])
        assert np.array_equal(raw[~interior], cleaned[~interior])
        full = Image.fromarray(cleaned)
        full_path = BASE / 'cleaned-full' / source.name
        full.save(full_path)
        bbox = Image.fromarray((raw[..., 3] > 8).astype('uint8') * 255).getbbox()
        assert bbox, key
        cropped = full.crop(bbox)
        cropped.thumbnail((232, 232), Image.Resampling.LANCZOS)
        icon = Image.new('RGBA', (256, 256))
        icon.alpha_composite(cropped, ((256-cropped.width)//2, (256-cropped.height)//2))
        target = BASE / 'icons' / source.name
        icon.save(target)
        rows = [r for r in rows if r['key'] != key]
        rows.append(dict(key=key, source=str(source.relative_to(ROOT)).replace('\\', '/'), cleaned=str(full_path.relative_to(ROOT)).replace('\\', '/'), file=str(target.relative_to(ROOT)).replace('\\', '/'), originalSize=[raw.shape[1], raw.shape[0]], iconSize=[256,256], strength=.25, interior_blend=.25, alpha_identical=bool(np.array_equal(source_alpha,cleaned[...,3])), external_alpha_identical=True, alphaRepair=alpha_repair, contours_identical=alpha_repair is None, source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(), sha256=hashlib.sha256(target.read_bytes()).hexdigest(), seconds=round(time.monotonic()-started, 2)))
        report=dict(tool=cfg['tool'], tool_url=cfg['tool_url'], local_only=True, device=device, originals_preserved=True, assets=rows)
        (BASE / 'cleanup.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
        print(json.dumps(dict(key=key, seconds=rows[-1]['seconds'])), flush=True)
        if device == 'cuda':
            torch.cuda.empty_cache()

font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 18)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 13)
bg = '#182128'
canvas = Image.new('RGB', (1200, 6*226), bg)
draw = ImageDraw.Draw(canvas)
for i, spec in enumerate(plan['icons']):
    x, y = (i%6)*200, (i//6)*226
    path = BASE/'icons'/(spec['key']+'.png')
    if path.exists():
        im = Image.open(path).convert('RGBA')
        canvas.paste(im.resize((128,128), Image.Resampling.LANCZOS), (x+36,y+4), im.resize((128,128), Image.Resampling.LANCZOS))
        tiny=im.resize((32,32), Image.Resampling.LANCZOS)
        canvas.paste(tiny,(x+84,y+141),tiny)
    draw.text((x+100,y+181),spec['name'],font=font,fill='white',anchor='mt')
    draw.text((x+100,y+206),spec['key'],font=small,fill='#9daeb9',anchor='mt')
canvas.save(BASE/'contact.png')
print(json.dumps(dict(cleaned=len(rows), expected=len(plan['icons']), contact=str(BASE/'contact.png'))), flush=True)
