"""Targeted automatic enemy targeting regression and legal-action Ambush recording."""
import argparse
import json
import time
import urllib.request
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument("--port", type=int, required=True)
p.add_argument("--record", action="store_true")
a = p.parse_args()
root = Path(__file__).resolve().parents[2]
out = root / "out/tmp/auto-target"
out.mkdir(parents=True, exist_ok=True)
base = f"http://127.0.0.1:{a.port}/api/v1"

def req(path, body=None):
    request = urllib.request.Request(base + path, data=None if body is None else json.dumps(body).encode(),
        headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)

def act(action, **values):
    result = req("/singleplayer", dict(action=action, **values))
    if result.get("error") or result.get("status") == "error":
        raise RuntimeError(result)
    return result

def wait(predicate, timeout=30):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        state = req("/singleplayer?format=json")
        if predicate(state):
            return state
        time.sleep(.1)
    raise TimeoutError(state)

settings = req("/settings")
assert settings["silent_test"] and not settings.get("silent_test_error"), settings
state = req("/singleplayer?format=json")
if state["state_type"] == "menu":
    if "continue" in state.get("options", []):
        act("menu_select", option="continue")
    else:
        if state.get("menu_screen") == "main":
            act("menu_select", option="singleplayer")
            wait(lambda s: s.get("menu_screen") == "character_select")
        act("menu_select", option="JUNNA")
        act("menu_select", option="confirm")
    state = wait(lambda s: s["state_type"] in ("map", "event") or bool(s.get("battle")))
    act("run_command", command="unlock all")
if state["state_type"] == "map":
    state = wait(lambda s: bool(s.get("map", {}).get("next_options")))
    node = state["map"]["next_options"][0]
    for _ in range(20):
        state = req("/singleplayer?format=json")
        if state.get("battle", {}).get("is_play_phase"):
            break
        if state["state_type"] == "map" and not state["map"].get("is_traveling"):
            act("choose_map_node", index=node["index"])
        time.sleep(.5)
state = wait(lambda s: s.get("battle", {}).get("is_play_phase"))
if len(state["battle"]["enemies"]) < 2:
    act("run_command", command="fight BOWLBUGS_WEAK")
    time.sleep(2)
    state = wait(lambda s: s.get("battle", {}).get("is_play_phase") and len(s["battle"]["enemies"]) >= 2)
if not a.record:
    result_file = out / "results.json"
    if result_file.exists():
        result_file.unlink()
    act("run_command", command="junna_auto_target check")
    deadline = time.monotonic() + 45
    while time.monotonic() < deadline:
        if result_file.exists():
            result = json.loads(result_file.read_text(encoding="utf-8-sig"))
            if result.get("completed"):
                failures = [r for r in result["results"] if not r["pass"]]
                print(json.dumps(dict(cases=len(result["results"]), failures=failures), ensure_ascii=False), flush=True)
                raise SystemExit(bool(failures))
        time.sleep(.1)
    raise TimeoutError("Probe did not complete")
live = out / "live.json"
if live.exists():
    live.unlink()
act("run_command", command="junna_auto_target prepare")
wait(lambda s: live.exists() and len(s.get("player", {}).get("hand", [])) == 1 and s["player"]["hand"][0]["id"] == "JUNNA_JA65" and s["player"]["hand"][0]["can_play"])
time.sleep(.8)
req("/recording/start", dict(output_dir=str(out / "video"), fps=15, width=1280, height=720,
    audio=True, max_duration_seconds=45))
try:
    act("hover_card", card_index=0)
    time.sleep(1)
    act("unhover_card")
    act("play_card", card_index=0)
    time.sleep(2)
    previous_round = req("/singleplayer?format=json")["battle"]["round"]
    act("end_turn")
    state = wait(lambda s: s["state_type"] == "card_select" or
        s.get("battle", {}).get("round", 0) > previous_round and s["battle"]["is_play_phase"])
    if state["state_type"] == "card_select":
        act("select_card", index=0)
        time.sleep(.6)
        act("confirm_selection")
    state = wait(lambda s: s.get("battle", {}).get("is_play_phase") and s["state_type"] != "card_select")
    time.sleep(3)
    (out / "recorded-state.json").write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding="utf-8")
finally:
    req("/recording/stop", {})
    for _ in range(150):
        recording = req("/recording")
        if recording["status"] in ("completed", "failed"):
            break
        time.sleep(.2)
    (out / "recording.json").write_text(json.dumps(recording, indent=2), encoding="utf-8")
assert recording["status"] == "completed" and recording.get("audio_source") == "engine_mix", recording
act("run_command", command="junna_auto_target inspect")
inspection = json.loads(live.read_text(encoding="utf-8-sig"))
assert any(p["id"] == "JB03" and p["target"] is not None and p["Remaining"] == 3 for p in inspection["plays"]), inspection
print(json.dumps(dict(recording=recording, inspection=inspection), ensure_ascii=False), flush=True)
