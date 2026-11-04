"""Run Karen's local artifact cleaner on Junna assets, keeping source alpha."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys
import time

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[2]
CONFIG_PATH = ROOT / "output/artifact-cleanup/2026-11-02/config.json"
CFG = json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
OUT = Path(CFG["output"])
FONT = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 22)

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def cleanup_mask(item, size):
    if not item.get("protect_board"):
        return None
    assert size == (1536, 1024), size
    mask = Image.new("L", size, 0)
    draw = ImageDraw.Draw(mask)
    # Selected card plus its Junna energy detail; native/Karen references stay exact.
    draw.rectangle((90, 65, 739, 934), fill=255)
    draw.rectangle((855, 98, 1408, 608), fill=255)
    # Preserve the illustration, typography, numerals and rules panel.
    for box in [(178, 211, 668, 481), (221, 481, 324, 532),
                (506, 481, 640, 532), (142, 102, 194, 171),
                (351, 135, 497, 195), (378, 482, 450, 550),
                (369, 554, 464, 602), (183, 610, 662, 852)]:
        draw.rectangle(box, fill=0)
    # Feather inward only, keeping all protected pixels exactly intact.
    raw = np.asarray(mask).copy()
    blur = np.asarray(mask.filter(ImageFilter.GaussianBlur(3)))
    return np.where(raw == 0, 0, blur).astype(np.float32) / 255

def record(item, target, strength, seconds, reused=False):
    before = np.asarray(Image.open(item["source"]).convert("RGBA"))
    after = np.asarray(Image.open(target).convert("RGBA"))
    assert before.shape == after.shape
    assert np.array_equal(before[..., 3], after[..., 3])
    mask = cleanup_mask(item, (before.shape[1], before.shape[0]))
    protected = bool(np.array_equal(before[mask == 0], after[mask == 0])) if mask is not None else None
    if protected is not None:
        assert protected
    visible = before[..., 3] >= 128
    delta = np.abs(after[..., :3].astype(np.int16) - before[..., :3].astype(np.int16))
    return dict(id=item["id"], name=item["name"], source=item["source"],
                source_sha256=sha(item["source"]), file=str(target.relative_to(OUT)),
                sha256=sha(target), strength=strength, size=[before.shape[1], before.shape[0]],
                alpha_identical=True, protected_regions_identical=protected,
                visible_rgb_mean_absolute_change=round(float(delta[visible].mean()), 4),
                seconds=round(seconds, 3), reused_pilot=reused)

def render_pilot(item):
    variants = [Path(item["source"])] + [OUT / "pilot" / f'{item["id"]}-a{s:.2f}.png' for s in (.25, .5)]
    images = [Image.open(p).convert("RGBA") for p in variants]
    board = Image.new("RGB", (1200, 1540), "#e9ecf0")
    draw = ImageDraw.Draw(board)
    for col, (im, label) in enumerate(zip(images, ["原图", "清理 0.25", "清理 0.50"])):
        draw.text((col * 400 + 130, 10), label, fill="#272b31", font=FONT)
        thumb = im.copy()
        thumb.thumbnail((380, 570), Image.Resampling.LANCZOS)
        board.paste(thumb, (col * 400 + (400-thumb.width)//2, 50), thumb)
        for row, box in enumerate(item["regions"]):
            x,y,w,h = box
            crop = im.crop((x,y,x+w,y+h))
            board.paste(crop, (col*400+20, 640+row*290), crop)
    board.save(OUT / "pilot" / f'{item["id"]}-comparison.png')

def finish():
    report = json.loads((OUT / "batch-results.json").read_text(encoding="utf-8"))
    rows = report["assets"]
    assert len(rows) == len(CFG["items"]) == 7
    for item, row in zip(CFG["items"], rows):
        assert row["id"] == item["id"]
        assert sha(item["source"]) == row["source_sha256"], item["id"]
        assert sha(OUT / row["file"]) == row["sha256"], item["id"]
        source = np.asarray(Image.open(item["source"]).convert("RGBA"))
        output = np.asarray(Image.open(OUT / row["file"]).convert("RGBA"))
        assert source.shape == output.shape
        assert np.array_equal(source[..., 3], output[..., 3])
        if item.get("protect_board"):
            mask = cleanup_mask(item, (source.shape[1], source.shape[0]))
            assert np.array_equal(source[mask == 0], output[mask == 0])
    board = Image.new("RGB", (1500, 1580), "#e9ecf0")
    draw = ImageDraw.Draw(board)
    samples = rows[:5] + [rows[6]]
    for index, row in enumerate(samples):
        col, line = index % 3, index // 3
        im = Image.open(OUT / row["file"]).convert("RGBA")
        im.thumbnail((470, 710), Image.Resampling.LANCZOS)
        board.paste(im, (col * 500 + (500-im.width)//2, line * 790 + 8 + (710-im.height)//2), im)
        draw.text((col*500+90, line*790+740), row["name"], fill="#272b31", font=FONT)
    board.save(OUT / "cleaned-assets-preview.png")
    manifest = {
        "tool": CFG["tool"], "tool_url": CFG["tool_url"], "date": CFG["date"],
        "strength": .25, "regeneration": False, "prompt": None,
        "idle": "portraits/idle.png", "idle_source": os.path.relpath(CFG["items"][0]["source"], OUT).replace(os.sep, "/"),
        "retention": "keep_all", "originals_preserved": True,
        "files": [dict(id=r["id"], name=r["name"], file=r["file"].replace(os.sep, "/")) for r in rows[1:5]],
        "ui": [dict(id=r["id"], name=r["name"], file=r["file"].replace(os.sep, "/")) for r in rows[5:]],
        "preview": "cleaned-assets-preview.png",
        "comparisons": ["pilot/idle-comparison.png", "pilot/ui-frame-comparison.png", "pilot/ui-energy-comparison.png"],
        "validation": "batch-results.json", "config": "config.json",
        "script": "../../../tools/assets/clean_ai_artifacts.py",
        "checks": {"assets": 7, "source_hashes_unchanged": True, "all_dimensions_unchanged": True,
                   "all_alpha_identical": True, "card_text_numerals_and_reference_regions_identical": True},
        "selection": "Compared 0.25 and 0.50 on idle, card frame and energy icon; selected 0.25 to retain details."
    }
    (OUT / "deliverables.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    ref_path = ROOT / "output/imagegen/junna/references.json"
    refs = json.loads(ref_path.read_text(encoding="utf-8"))
    refs.setdefault("idle_source", refs["idle"])
    refs.setdefault("pose_set_source", refs["pose_set"])
    refs["idle"] = os.path.relpath(OUT / "portraits/idle.png", ref_path.parent).replace(os.sep, "/")
    refs["pose_set"] = os.path.relpath(OUT / "deliverables.json", ref_path.parent).replace(os.sep, "/")
    refs["cleanup"] = {"tool": CFG["tool"], "strength": .25, "manifest": refs["pose_set"]}
    ref_path.write_text(json.dumps(refs, ensure_ascii=False, indent=2), encoding="utf-8")
    ui_sidecar = Path(CFG["items"][5]["source"]).parent / "artifact-cleanup-v1.json"
    ui_sidecar.write_text(json.dumps({
        "tool": CFG["tool"], "strength": .25, "regeneration": False,
        "card": os.path.relpath(OUT / "ui/ui-frame.png", ui_sidecar.parent).replace(os.sep, "/"),
        "energy": os.path.relpath(OUT / "ui/ui-energy.png", ui_sidecar.parent).replace(os.sep, "/"),
        "manifest": os.path.relpath(OUT / "deliverables.json", ui_sidecar.parent).replace(os.sep, "/"),
        "originals_preserved": True, "alpha_identical": True, "protected_board_regions_identical": True
    }, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(manifest["checks"], ensure_ascii=False), flush=True)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("phase", choices=["pilot", "batch", "finish"])
    parser.add_argument("--strength", type=float, default=.25)
    args = parser.parse_args()
    if args.phase == "finish":
        return finish()
    OUT.mkdir(parents=True, exist_ok=True)
    sys.path.insert(0, CFG["tool_root"])
    import torch
    import diffusers
    from diffusers import AutoencoderKLFlux2
    from modeling import LatentR, build_fns, restore
    torch.set_num_threads(4)
    device = "cuda" if torch.cuda.is_available() else "cpu"
    print(json.dumps(dict(device=device, torch=torch.__version__, diffusers=diffusers.__version__)), flush=True)
    rows = []
    models = None
    for item in CFG["items"]:
        if args.phase == "pilot" and item["id"] not in ("idle", "ui-frame", "ui-energy"):
            continue
        strengths = (.25, .5) if args.phase == "pilot" else (args.strength,)
        for strength in strengths:
            target = OUT / ("pilot" if args.phase == "pilot" else item["group"]) / (f'{item["id"]}-a{strength:.2f}.png' if args.phase == "pilot" else f'{item["id"]}.png')
            target.parent.mkdir(parents=True, exist_ok=True)
            pilot = OUT / "pilot" / f'{item["id"]}-a{strength:.2f}.png'
            prior_path = OUT / "pilot-results.json"
            prior = json.loads(prior_path.read_text(encoding="utf-8"))["assets"] if prior_path.exists() else []
            reuse = args.phase == "batch" and pilot.exists() and any(r["id"] == item["id"] and r["strength"] == strength and r["source_sha256"] == sha(item["source"]) for r in prior)
            started = time.monotonic()
            if reuse:
                shutil.copyfile(pilot, target)
            else:
                if models is None:
                    checkpoint = torch.load(Path(CFG["tool_root"]) / "weights/latent_residual.pt", map_location="cpu", weights_only=True)
                    residual = LatentR(32)
                    residual.load_state_dict(checkpoint["R_ema"])
                    residual.to(device).eval().requires_grad_(False)
                    vae = AutoencoderKLFlux2.from_pretrained(CFG["vae_root"], local_files_only=True).to(device).eval().requires_grad_(False)
                    vae.enable_tiling()
                    enc,dec = build_fns(vae, False)
                    models = (residual, vae, enc, dec)
                residual, vae, enc, dec = models
                src = Image.open(item["source"]).convert("RGBA")
                arr = np.asarray(src)
                rgb = restore(arr[..., :3], enc, dec, residual, strength, device, torch.bfloat16 if device == "cuda" else torch.float32)
                mask = cleanup_mask(item, src.size)
                if mask is not None:
                    rgb = np.rint(rgb * mask[..., None] + arr[..., :3] * (1-mask[..., None])).clip(0,255).astype(np.uint8)
                Image.fromarray(np.dstack([rgb, arr[..., 3]])).save(target)
                if device == "cuda":
                    torch.cuda.empty_cache()
            row = record(item, target, strength, time.monotonic()-started, reuse)
            rows.append(row)
            print(json.dumps(row, ensure_ascii=False), flush=True)
            report = dict(tool=CFG["tool"], tool_url=CFG["tool_url"], phase=args.phase,
                          regeneration=False, prompt=None, exact_source_alpha=True, assets=rows)
            (OUT / f"{args.phase}-results.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        if args.phase == "pilot":
            render_pilot(item)

if __name__ == "__main__":
    main()
