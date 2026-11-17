"""Run only Oath's real in-combat preview and triggered-block regression."""
import argparse
import json
import time
import urllib.request
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("--port", type=int, required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
report = root / "out/tmp/fix-ja118-block-preview-20261117/results.json"
base = f"http://127.0.0.1:{args.port}/api/v1"

def request(path, body=None):
    req = urllib.request.Request(base + path,
        data=None if body is None else json.dumps(body).encode(),
        headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=15) as response:
        return json.load(response)

state = request("/singleplayer?format=json")
assert state.get("battle", {}).get("is_play_phase"), state
settings = request("/settings")
assert settings["silent_test"] and not settings.get("silent_test_error"), settings
started = time.time()
result = request("/singleplayer", {"action": "run_command",
    "command": "junna_power_block_preview check"})
assert result.get("status") != "error" and not result.get("error"), result
deadline = time.monotonic() + 45
while time.monotonic() < deadline:
    if report.exists() and report.stat().st_mtime >= started:
        rows = json.loads(report.read_text(encoding="utf-8"))
        failures = [row for row in rows if not row["pass"]]
        print(json.dumps({"assertions": len(rows), "failures": failures}, ensure_ascii=False))
        raise SystemExit(bool(failures))
    time.sleep(.2)
raise TimeoutError("Preview regression did not finish")