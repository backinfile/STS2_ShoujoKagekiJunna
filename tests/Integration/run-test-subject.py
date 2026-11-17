"""Targeted Test Subject knockout regression and normal-play recording."""
import argparse
import json
import time
import urllib.request
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("--port", type=int, required=True)
parser.add_argument("--mode", choices=["check", "prepare", "inspect"], default="check")
parser.add_argument("--scenario", choices=["single", "group", "support", "pending"], default="single")
parser.add_argument("--record", action="store_true")
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
out = root / "out/tmp/experiment-death-zero-arrows"
out.mkdir(parents=True, exist_ok=True)
base = f"http://127.0.0.1:{args.port}/api/v1"


def request(path, body=None):
    req = urllib.request.Request(
        base + path,
        data=None if body is None else json.dumps(body).encode(),
        headers={"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(req, timeout=30) as response:
        return json.load(response)


def act(action, **values):
    result = request("/singleplayer", dict(action=action, **values))
    if result.get("error") or result.get("status") == "error":
        raise RuntimeError(result)
    return result


def wait(predicate, timeout=35):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        state = request("/singleplayer?format=json")
        if state.get("is_tutorial") and state.get("ready"):
            request("/ui/prompt", dict(option="advance", prompt_id=state["prompt_id"]))
        elif predicate(state):
            return state
        time.sleep(.15)
    raise TimeoutError(state)


def probe(mode):
    path = out / ("prepared.json" if mode == "prepare" else "results.json")
    path.unlink(missing_ok=True)
    act("run_command", command=f"junna_test_subject {mode} {args.scenario}")
    deadline = time.monotonic() + 35
    while time.monotonic() < deadline:
        if path.exists():
            return json.loads(path.read_text(encoding="utf-8-sig"))
        time.sleep(.1)
    raise TimeoutError("Probe did not complete")


settings = request("/settings")
assert settings["silent_test"] and not settings.get("silent_test_error"), settings
state = request("/singleplayer?format=json")
if state["state_type"] == "menu":
    if "continue" in state.get("options", []):
        act("menu_select", option="continue")
    else:
        if state.get("menu_screen") == "main":
            act("menu_select", option="singleplayer")
            wait(lambda s: s.get("menu_screen") == "character_select")
        act("menu_select", option="JUNNA")
        act("menu_select", option="confirm")
    wait(lambda s: s["state_type"] in ("map", "event") or bool(s.get("battle")))
    # The starting event is visible before its native room initialization finishes.
    time.sleep(5)
act("run_command", command="unlock all")
if args.mode != "inspect":
    act("run_command", command="fight TEST_SUBJECT_BOSS")
    time.sleep(2)
    wait(lambda s: s.get("battle", {}).get("is_play_phase") and any(
        e["entity_id"].startswith("TEST_SUBJECT_") for e in s["battle"]["enemies"]))

if not args.record:
    result = probe(args.mode)
    (out / f"{args.scenario}-{args.mode}.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    raise SystemExit(0 if args.mode == "prepare" or result["pass"] else 1)

probe("prepare")
state = wait(lambda s: any(c["id"] == "JUNNA_JA71" and c["can_play"] for c in s.get("player", {}).get("hand", [])))
index = next(c["index"] for c in state["player"]["hand"] if c["id"] == "JUNNA_JA71")
request("/recording/start", dict(output_dir=str(out / "fixed-video"), fps=15, width=1280,
    height=720, audio=True, max_duration_seconds=35))
try:
    time.sleep(1)
    act("hover_card", card_index=index)
    time.sleep(1)
    act("unhover_card")
    act("play_card", card_index=index)
    time.sleep(3)
    with urllib.request.urlopen(base + "/screenshot") as response:
        (out / "fixed-knockout.png").write_bytes(response.read())
    knockout = request("/singleplayer?format=json")
    assert knockout["player"]["discard_pile_count"] == 3, knockout
    (out / "recorded-knockout.json").write_text(json.dumps(knockout, ensure_ascii=False, indent=2), encoding="utf-8")
    previous_round = state["battle"]["round"]
    act("end_turn")
    state = wait(lambda s: s.get("battle", {}).get("round", 0) > previous_round and s["battle"]["is_play_phase"])
    time.sleep(2)
    state = request("/singleplayer?format=json")
finally:
    request("/recording/stop", {})
    for _ in range(150):
        recording = request("/recording")
        if recording["status"] in ("completed", "failed"):
            break
        time.sleep(.2)
    (out / "fixed-recording.json").write_text(json.dumps(recording, indent=2), encoding="utf-8")
assert recording["status"] == "completed" and recording.get("audio_source") == "engine_mix", recording
with urllib.request.urlopen(base + "/recording/frames?frame_count=12&columns=4&frame_width=320") as response:
    (out / "fixed-frames.png").write_bytes(response.read())
result = probe("inspect")
assert result["pass"] and all(c["pile"] in ("Hand", "Draw", "Discard") and c["remaining"] is None for c in result["cards"]), result
assert state["battle"]["enemies"][0]["hp"] == 200, state
(out / "recorded-state.json").write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(dict(recording=recording, result=result), ensure_ascii=False), flush=True)
