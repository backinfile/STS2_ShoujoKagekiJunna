"""Try local Mi-Ripple on Junna originals, preserving alpha and UI text."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import time

import numpy as np
from PIL import Image
from mi_ripple.pipeline import run
from mi_ripple.common import load_rgb
from mi_ripple.verify import verify, verify_board
from clean_ai_artifacts import cleanup_mask

ROOT = Path(__file__).resolve().parents[2]
CONFIG = json.loads((ROOT / "output/artifact-cleanup/2026-11-02/config.json").read_text(encoding="utf-8"))
OUT = ROOT / "output/mi-ripple/2026-11-02"

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def visible_windows(item, original, diag):
    alpha = original[..., 3]
    def visible(box):
        x1,y1,x2,y2 = box
        return float((alpha[y1:y2, x1:x2] >= 128).mean()) >= .5
    flats = [r['box'] for r in diag.get('granule',{}).get('flat_windows',[]) if visible(r['box'])]
    structures = [r['box'] for r in diag.get('granule',{}).get('oriented_windows',[]) if visible(r['box'])]
    critical = {
        '01-aim': [[410,328,620,530], [350,570,580,765], [850,475,980,610]],
        '02-low-guard': [[620,185,880,435], [605,430,790,670], [550,710,755,850]],
        '03-release-step': [[465,220,680,430], [450,470,655,700], [310,370,480,490]],
        '04-rest-glasses': [[510,100,730,300], [540,370,760,610], [505,155,605,300]],
    }
    structures += critical.get(item['id'], [])
    structures += [[x,y,x+w,y+h] for x,y,w,h in item.get('regions',[])]
    return flats, structures

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--names", nargs="+")
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    manifest_path = OUT / "review.json"
    previous = json.loads(manifest_path.read_text(encoding="utf-8")) if manifest_path.exists() else {}
    rows = {r["id"]: r for r in previous.get("assets", [])}
    for item in CONFIG["items"]:
        if args.names and item["id"] not in args.names:
            continue
        source = Path(item["source"])
        started = time.monotonic()
        evidence = OUT / "evidence" / item["id"]
        raw_report = evidence / (source.stem + '_restored.json')
        result = json.loads(raw_report.read_text(encoding='utf-8')) if raw_report.exists() else None
        if result is None or result['source_sha256'] != sha(source):
            result = run(source, evidence, allow_regen=False, max_regen=0)
        diag_path = evidence / (source.stem + "_input_diag.json")
        diag = json.loads(diag_path.read_text(encoding="utf-8")) if diag_path.exists() else {}
        row = dict(id=item["id"], name=item["name"], source=str(source),
                   source_sha256=sha(source), pipeline_outcome=result["outcome"],
                   actions=[s["action"] for s in result["steps"]],
                   flags=diag.get("flags"), lattice=diag.get("lattice"),
                   granule=diag.get("granule",{}).get("level"),
                   scale_level=diag.get("scale_index",{}).get("level"),
                   scale_pct=diag.get("scale_index",{}).get("scale_tile_pct"),
                   evidence=str(evidence.relative_to(OUT)), adopted_strength=0.0,
                   source_copy=True, status="unchanged", verification=None)
        target = OUT / "results" / item["group"] / (item["id"] + ".png")
        target.parent.mkdir(parents=True, exist_ok=True)
        original = np.asarray(Image.open(source).convert("RGBA"))
        if result.get("final") and not result.get("final_is_source_copy"):
            filtered = np.asarray(Image.open(result["final"]).convert("RGB"))
            assert filtered.shape == original[..., :3].shape
            before = load_rgb(source)
            flats, structures = visible_windows(item, original, diag)
            row['verification_scope'] = 'Visible automatic windows plus explicit face, clothing and UI detail windows'
            mask = cleanup_mask(item, (original.shape[1], original.shape[0]))
            strengths = (1.0, .25, .125) if item.get('protect_board') else (1.0, .25)
            for strength in strengths:
                corrected = np.rint(original[..., :3] * (1-strength) + filtered * strength).clip(0,255)
                if mask is not None:
                    corrected = np.rint(corrected * mask[...,None] + original[..., :3] * (1-mask[...,None]))
                corrected = corrected.astype(np.uint8)
                checks = verify(before, corrected.astype(np.float64)/255, flats, structures)
                grade_path = evidence / f"actual-rgba-{strength:.2f}-verify.json"
                grade_path.write_text(json.dumps(checks, indent=2), encoding="utf-8")
                verify_board(before, corrected.astype(np.float64)/255, checks,
                             evidence / f"actual-rgba-{strength:.2f}-board.png",
                             f'{item["id"]}: actual output, correction={strength:.2f}')
                if checks["passed"]:
                    rgba = np.dstack((corrected, original[...,3]))
                    if mask is not None:
                        assert np.array_equal(rgba[mask == 0], original[mask == 0])
                    Image.fromarray(rgba).save(target)
                    row.update(adopted_strength=strength, source_copy=False,
                               status="filtered_verified", verification=str(grade_path.relative_to(OUT)))
                    break
            else:
                row["status"] = "unchanged_filter_rejected"
        elif result["outcome"] == "needs_human_decision":
            row["status"] = "needs_review_unchanged"
        elif result["outcome"] != "delivered":
            row["status"] = "pipeline_" + result["outcome"] + "_unchanged"
        if row["source_copy"]:
            shutil.copyfile(source, target)
        final = np.asarray(Image.open(target).convert("RGBA"))
        assert final.shape == original.shape
        assert np.array_equal(final[...,3], original[...,3])
        row.update(file=str(target.relative_to(OUT)), sha256=sha(target),
                   alpha_identical=True, dimensions=[final.shape[1],final.shape[0]],
                   pixels_unchanged=bool(np.array_equal(final,original)),
                   seconds=round(time.monotonic()-started,2))
        rows[item["id"]] = row
        manifest = dict(tool="Mi-Ripple 0.1.0", tool_url="https://github.com/miyang-ai/Mi-Ripple",
                        input_set="Original five portraits and selected V4 frame/energy icon",
                        regeneration=False, max_regen=0, native_resolution=True,
                        original_alpha_preserved=True, assets=list(rows.values()))
        manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding="utf-8")
        print(json.dumps({k:row[k] for k in ("id","pipeline_outcome","actions","status","adopted_strength","granule","scale_level","scale_pct","pixels_unchanged","seconds")},ensure_ascii=False),flush=True)

if __name__ == "__main__":
    main()
