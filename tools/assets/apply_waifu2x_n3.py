"""Apply the selected CUNet noise3 settings to the seven original assets."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import time

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from clean_ai_artifacts import cleanup_mask

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output/waifu2x/2026-11-02"
TOOL = ROOT / ".tools/waifu2x/waifu2x-ncnn-vulkan-20250915-windows"
CFG = json.loads((ROOT / "output/artifact-cleanup/2026-11-02/config.json").read_text(encoding="utf-8"))
SETTINGS = dict(model="CUNet", noise=3, scale=1, tta=False, tile=128)


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def relative(path, base):
    return os.path.relpath(path, base).replace(os.sep, "/")


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


def validate(rows):
    assert len(rows) == len(CFG["items"]) == 7
    for item, row in zip(CFG["items"], rows):
        assert item["id"] == row["id"]
        assert digest(item["source"]) == row["source_sha256"]
        assert digest(OUT / row["file"]) == row["sha256"]
        source = np.asarray(Image.open(item["source"]).convert("RGBA"))
        target = np.asarray(Image.open(OUT / row["file"]).convert("RGBA"))
        assert source.shape == target.shape
        assert np.array_equal(source[..., 3], target[..., 3])
        mask = cleanup_mask(item, (source.shape[1], source.shape[0]))
        if mask is not None:
            assert np.array_equal(source[mask == 0], target[mask == 0])


def batch():
    (OUT / "raw").mkdir(parents=True, exist_ok=True)
    rows = []
    for item in CFG["items"]:
        started = time.monotonic()
        source = Path(item["source"])
        source_hash = digest(source)
        original = np.asarray(Image.open(source).convert("RGBA"))
        raw = OUT / "raw" / f'{item["id"]}.png'
        target = OUT / item["group"] / raw.name
        target.parent.mkdir(exist_ok=True)
        command = [str(TOOL / "waifu2x-ncnn-vulkan.exe"), "-i", str(source), "-o", str(raw),
                   "-n", "3", "-s", "1", "-m", str(TOOL / "models-cunet"),
                   "-t", "128", "-j", "1:1:1", "-g", "0"]
        result = subprocess.run(command, cwd=OUT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                creationflags=subprocess.CREATE_NO_WINDOW)
        raw.with_suffix(".log").write_bytes(result.stdout)
        if result.returncode:
            raise RuntimeError(result.stdout.decode("utf-8", errors="replace")[-2000:])
        rgb = np.asarray(Image.open(raw).convert("RGB"))
        assert rgb.shape == original[..., :3].shape
        mask = cleanup_mask(item, (original.shape[1], original.shape[0]))
        if mask is not None:
            rgb = np.rint(rgb * mask[..., None] + original[..., :3] * (1 - mask[..., None])).clip(0, 255).astype(np.uint8)
        Image.fromarray(np.dstack([rgb, original[..., 3]])).save(target)
        row = dict(id=item["id"], name=item["name"], file=relative(target, OUT),
                   source=str(source), source_sha256=source_hash, sha256=digest(target),
                   size=[original.shape[1], original.shape[0]], alpha_identical=True,
                   protected_regions_identical=True if mask is not None else None,
                   seconds=round(time.monotonic() - started, 3))
        if item["id"] in ("idle", "ui-energy"):
            pilot = ROOT / "output/cleanup-research/2026-11-02/waifu2x" / f'{item["id"]}-n3.png'
            assert np.array_equal(np.asarray(Image.open(pilot).convert("RGBA")), np.asarray(Image.open(target).convert("RGBA")))
            row["matches_approved_pilot"] = True
        rows.append(row)
        print(json.dumps(row, ensure_ascii=False), flush=True)
    validate(rows)
    checks = dict(assets=7, source_hashes_unchanged=True, all_dimensions_unchanged=True,
                  all_alpha_identical=True, card_text_numerals_and_reference_regions_identical=True,
                  idle_and_energy_match_approved_pilots=True)
    write_json(OUT / "batch-results.json", dict(settings=SETTINGS, checks=checks, assets=rows))
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 22)
    board = Image.new("RGB", (1500, 1580), "#e9ecf0")
    draw = ImageDraw.Draw(board)
    for index, row in enumerate(rows[:5] + [rows[6]]):
        col, line = index % 3, index // 3
        im = Image.open(OUT / row["file"]).convert("RGBA")
        im.thumbnail((470, 710), Image.Resampling.LANCZOS)
        board.paste(im, (col * 500 + (500 - im.width) // 2, line * 790 + 8 + (710 - im.height) // 2), im)
        draw.text((col * 500 + 90, line * 790 + 740), row["name"], fill="#272b31", font=font)
    board.save(OUT / "cleaned-assets-preview.png")
    manifest = dict(tool="waifu2x-ncnn-vulkan 20250915", tool_url="https://github.com/nihui/waifu2x-ncnn-vulkan",
                    date="2026-11-02", settings=SETTINGS, regeneration=False, originals_preserved=True,
                    idle="portraits/idle.png", idle_source=relative(CFG["items"][0]["source"], OUT),
                    retention="keep_all", files=[{k: r[k] for k in ("id", "name", "file")} for r in rows[1:5]],
                    ui=[{k: r[k] for k in ("id", "name", "file")} for r in rows[5:]],
                    preview="cleaned-assets-preview.png", validation="batch-results.json",
                    script="../../../tools/assets/apply_waifu2x_n3.py", checks=checks)
    write_json(OUT / "deliverables.json", manifest)
    print(json.dumps(checks), flush=True)


def adopt():
    report = json.loads((OUT / "batch-results.json").read_text(encoding="utf-8"))
    assert report["settings"] == SETTINGS
    validate(report["assets"])
    ref_path = ROOT / "output/imagegen/junna/references.json"
    refs = json.loads(ref_path.read_text(encoding="utf-8"))
    refs["idle"] = relative(OUT / "portraits/idle.png", ref_path.parent)
    refs["pose_set"] = relative(OUT / "deliverables.json", ref_path.parent)
    refs["cleanup"] = dict(tool="waifu2x-ncnn-vulkan 20250915", **SETTINGS, manifest=refs["pose_set"])
    write_json(ref_path, refs)
    ui_path = Path(CFG["items"][5]["source"]).parent / "artifact-cleanup-v1.json"
    ui = dict(tool="waifu2x-ncnn-vulkan 20250915", **SETTINGS, regeneration=False,
              card=relative(OUT / "ui/ui-frame.png", ui_path.parent),
              energy=relative(OUT / "ui/ui-energy.png", ui_path.parent),
              manifest=relative(OUT / "deliverables.json", ui_path.parent),
              originals_preserved=True, alpha_identical=True, protected_board_regions_identical=True)
    write_json(ui_path, ui)
    for index_path, index, fields in [(ref_path, refs, ("idle", "pose_set")), (ui_path, ui, ("card", "energy", "manifest"))]:
        for field in fields:
            assert (index_path.parent / index[field]).is_file()
    print("Selected CUNet noise3 outputs adopted in portrait and UI asset indexes.", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("phase", choices=["batch", "adopt"])
    args = parser.parse_args()
    {"batch": batch, "adopt": adopt}[args.phase]()
