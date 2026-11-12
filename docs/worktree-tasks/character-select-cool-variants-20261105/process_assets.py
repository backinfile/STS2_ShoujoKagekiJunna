"""Clean and verify the four selection candidates with the installed Artifact Cleaner."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import sys
import time

BASE = Path(__file__).resolve().parent
ROOT = BASE.parents[2]
STRENGTH = 0.25

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def relative(path):
    return path.relative_to(ROOT).as_posix()

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cleaner-config", type=Path, required=True)
    parser.add_argument("--previews-only", action="store_true")
    args = parser.parse_args()
    cfg = json.loads(args.cleaner_config.read_text(encoding="utf-8"))
    sys.path.insert(0, cfg["dependencies"])
    sys.path.insert(0, cfg["tool_root"])
    import numpy as np
    import torch
    import diffusers
    from PIL import Image, ImageDraw, ImageFont
    from diffusers import AutoencoderKLFlux2
    from modeling import LatentR, build_fns, restore

    torch.set_num_threads(4)
    device = "cuda" if torch.cuda.is_available() else "cpu"
    dtype = torch.bfloat16 if device == "cuda" else torch.float32
    print(json.dumps(dict(device=device, python=sys.version.split()[0],
                          torch=torch.__version__, diffusers=diffusers.__version__)), flush=True)
    checkpoint_path = Path(cfg["tool_root"]) / "weights/latent_residual.pt"
    if not args.previews_only:
        checkpoint = torch.load(checkpoint_path, map_location="cpu", weights_only=True)
        residual = LatentR(32)
        residual.load_state_dict(checkpoint["R_ema"])
        residual.to(device).eval().requires_grad_(False)
        vae = AutoencoderKLFlux2.from_pretrained(
            cfg["vae_root"], local_files_only=True).to(device).eval().requires_grad_(False)
        vae.enable_tiling()
        enc, dec = build_fns(vae, False)
    prompts = json.loads((BASE / "prompts.json").read_text(encoding="utf-8"))
    rows = []
    previous_rows = {r["id"]: r for r in json.loads((BASE / "cleanup.json").read_text(encoding="utf-8"))["assets"]} if args.previews_only else {}
    (BASE / "cleaned").mkdir(exist_ok=True)
    (BASE / "comparisons").mkdir(exist_ok=True)
    font_path = Path(os.environ.get("WINDIR", "")) / "Fonts/msyh.ttc"
    font = ImageFont.truetype(str(font_path), 24)
    small_font = ImageFont.truetype(str(font_path), 18)
    boxes = {
        "01-full-draw": [(1160,140,1480,390),(820,200,1140,420),(1140,330,1460,550),(990,430,1310,650)],
        "02-aerial-shot": [(990,120,1310,370),(1270,180,1590,400),(1220,330,1540,550),(880,410,1200,630)],
        "03-star-orbit": [(1070,110,1390,360),(1260,210,1580,430),(1100,310,1420,530),(1000,450,1320,670)],
        "04-stage-backlight": [(1050,60,1370,310),(1250,240,1570,460),(1130,410,1450,630),(900,400,1220,620)]
    }
    for variant in prompts["variants"]:
        started = time.monotonic()
        key = variant["id"]
        source = BASE / "originals" / (key + ".png")
        target = BASE / "cleaned" / source.name
        source_hash = sha(source)
        original_image = Image.open(source).convert("RGBA")
        original = np.asarray(original_image)
        if not args.previews_only:
            cleaned_rgb = restore(original[..., :3].copy(), enc, dec, residual,
                                  STRENGTH, device, dtype)
            Image.fromarray(np.dstack([cleaned_rgb, original[..., 3]])).save(target)
        result_image = Image.open(target).convert("RGBA")
        result = np.asarray(result_image)
        assert sha(source) == source_hash, key
        assert original.shape == result.shape, key
        assert np.array_equal(original[..., 3], result[..., 3]), key
        assert original_image.size == (1672, 941), (key, original_image.size)
        delta = np.abs(original[..., :3].astype(np.int16) - result[..., :3].astype(np.int16))
        assert delta.mean() > 0, key
        # Full picture plus native-pixel crops; no crop resizing.
        board = Image.new("RGB", (692, 1365), "#101726")
        draw = ImageDraw.Draw(board)
        draw.text((12, 8), variant["name"], font=font, fill="#ffffff")
        for column, (im, label) in enumerate([(original_image, "原图"), (result_image, "清理 0.25")]):
            x = 16 + column * 340
            draw.text((x, 48), label, font=small_font, fill="#d9e6ff")
            thumb = im.convert("RGB")
            thumb.thumbnail((320, 180), Image.Resampling.LANCZOS)
            board.paste(thumb, (x, 80))
            y = 280
            for box, crop_label in zip(boxes[key], ["眼镜 / 发丝 · 100%", "握弓 / 手指 · 100%", "弓弦 / 披风 · 100%", "服装边缘 · 100%"]):
                draw.text((x, y), crop_label, font=small_font, fill="#b6cfff")
                crop = im.crop(box).convert("RGB")
                board.paste(crop, (x, y + 27))
                y += crop.height + 42
        comparison = BASE / "comparisons" / source.name
        board.save(comparison)
        row = dict(id=key, name=variant["name"], source=relative(source), file=relative(target),
                   comparison=relative(comparison), size=list(result_image.size),
                   source_sha256=source_hash, sha256=sha(target), strength=STRENGTH,
                   dimensions_unchanged=True, alpha_identical=True, originals_preserved=True,
                   rgb_mean_absolute_change=round(float(delta.mean()), 4),
                   seconds=previous_rows[key]["seconds"] if args.previews_only else round(time.monotonic()-started, 2))
        rows.append(row)
        manifest = dict(tool=cfg["tool"], tool_url=cfg["tool_url"],
                        mode="local-only", model="LatentR + FLUX.2-VAE",
                        weights_sha256=sha(checkpoint_path),
                        strength=STRENGTH, precision=str(dtype), device=device,
                        torch=torch.__version__, diffusers=diffusers.__version__,
                        original_reference="ShoujoKagekiJunna/images/character_select.png",
                        reference_sha256=sha(ROOT / prompts["reference"]),
                        generation_mode=prompts["mode"], assets=rows,
                        visual_review="pending")
        (BASE / "cleanup.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
        print(json.dumps(dict(id=key, size=row["size"], mean_change=row["rgb_mean_absolute_change"],
                              seconds=row["seconds"])), flush=True)
        if device == "cuda":
            torch.cuda.empty_cache()
    board = Image.new("RGB", (1696, 1056), "#0b101b")
    draw = ImageDraw.Draw(board)
    for index, row in enumerate(rows):
        x, y = 12+(index%2)*848, 12+(index//2)*522
        draw.text((x+8,y+4), str(index+1)+"  "+row["name"], font=font, fill="#e9f1ff")
        im = Image.open(ROOT / row["file"]).convert("RGB")
        im.thumbnail((824, 464), Image.Resampling.LANCZOS)
        board.paste(im, (x,y+42))
    board.save(BASE / "contact-sheet.png")
    print(json.dumps(dict(candidates=len(rows), size=[1672,941],
                          contact="contact-sheet.png", comparisons="comparisons/")), flush=True)

if __name__ == "__main__":
    main()
