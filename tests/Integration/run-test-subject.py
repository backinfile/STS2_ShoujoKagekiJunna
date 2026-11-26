"""Targeted Test Subject knockout regression and normal-play recording."""
import argparse
import json
import time
import urllib.request
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("--port", type=int, required=True)
parser.add_argument("--mode", choices=["check", "prepare", "inspect"], default="check")
parser.add_argument("--scenario", choices=[
    "single", "group", "support", "pending", "conversion", "stardust", "meteor", "stardust-chain", "large-prop",
], default="single")
parser.add_argument("--record", action="store_true")
args = parser.parse_args()
if args.record and (args.mode != "check" or args.scenario not in ("single", "conversion", "stardust", "stardust-chain")):
    parser.error("--record requires check mode and the single, conversion, stardust or stardust-chain scenario")
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


def wait_for_hand_card(card_id):
    state = wait(lambda s: any(c["id"] == card_id and c["can_play"] for c in s.get("player", {}).get("hand", [])))
    return next(c["index"] for c in state["player"]["hand"] if c["id"] == card_id)


def end_turn(round_number, select=None):
    def next_round(state):
        battle = state.get("battle", {})
        return battle.get("round", 0) > round_number and battle["is_play_phase"]

    act("end_turn")
    state = wait(lambda s: s["state_type"] == "hand_select" or next_round(s))
    if state["state_type"] == "hand_select":
        if select is not None:
            card = next(c for c in state["hand_select"]["cards"] if c["id"] == select)
            act("combat_select_card", card_index=card["index"])
            time.sleep(.4)
        act("combat_confirm_selection")
    else:
        assert select is None, state
    return wait(next_round)


def record_single():
    index = wait_for_hand_card("JUNNA_JA71")
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
    state = end_turn(knockout["battle"]["round"])
    time.sleep(2)
    assert state["battle"]["enemies"][0]["hp"] == 200, state
    return state


def record_stardust():
    act("play_card", card_index=wait_for_hand_card("JUNNA_JA71"))
    knockout = wait(lambda s: s.get("battle", {}).get("enemies") == [] and
        s.get("player", {}).get("exhaust_pile_count") == 1)
    time.sleep(2)
    state = end_turn(knockout["battle"]["round"])
    assert state["battle"]["enemies"][0]["hp"] == 200, state
    time.sleep(1)
    return state


def record_stardust_chain():
    card_id = "JUNNA_U_CARD_005"
    target = request("/singleplayer?format=json")["battle"]["enemies"][0]["entity_id"]
    act("play_card", card_index=wait_for_hand_card(card_id), target=target)
    state = wait(lambda s: s.get("player", {}).get("exhaust_pile_count") == 8)
    assert state["battle"]["enemies"][0]["hp"] == 40, state
    time.sleep(2)
    return state


def record_conversion():
    act("play_card", card_index=wait_for_hand_card("JUNNA_JA87"))
    wait(lambda s: any(p["id"] == "JUNNA_JA87_POWER" for p in s["player"]["status"]))
    act("play_card", card_index=wait_for_hand_card("JUNNA_JA71"))
    knockout = wait(lambda s: not s["battle"]["enemies"] and s["player"]["discard_pile_count"] == 3)
    time.sleep(1)
    state = end_turn(knockout["battle"]["round"], select="SHIV")
    assert state["battle"]["enemies"][0]["hp"] == 200, state
    assert all(c["id"] != "SHIV" for c in state["player"]["hand"]), state
    assert not any(p["id"] == "WEAK_POWER" for p in state["battle"]["enemies"][0]["status"]), state
    time.sleep(1)
    state = end_turn(state["battle"]["round"])
    assert state["battle"]["enemies"][0]["hp"] == 200, state
    time.sleep(1)
    state = end_turn(state["battle"]["round"])
    enemy = state["battle"]["enemies"][0]
    assert enemy["hp"] == 196, state
    assert any(p["id"] == "WEAK_POWER" and p["amount"] == 1 for p in enemy["status"]), state
    time.sleep(2)
    return state


settings = request("/settings")
assert settings["silent_test"] and not settings.get("silent_test_error"), settings
state = request("/singleplayer?format=json")
if state["state_type"] == "menu":
    if "continue" in state.get("options", []):
        act("menu_select", option="continue")
    else:
        if state.get("menu_screen") == "main":
            act("menu_select", option="singleplayer")
            state = wait(lambda s: s.get("menu_screen") in ("singleplayer", "character_select"))
        if state.get("menu_screen") == "singleplayer":
            act("menu_select", option="standard")
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
wait_for_hand_card("JUNNA_U_CARD_005" if args.scenario == "stardust-chain" else "JUNNA_JA71")
request("/recording/start", dict(output_dir=str(out / "fixed-video"), fps=15, width=1280,
    height=720, audio=True, max_duration_seconds=45))
try:
    time.sleep(1)
    record_scenario = {
        "single": record_single, "conversion": record_conversion, "stardust": record_stardust,
        "stardust-chain": record_stardust_chain,
    }
    state = record_scenario[args.scenario]()
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
assert result["pass"] and all(c["remaining"] is None for c in result["cards"]), result
if args.scenario in ("stardust", "stardust-chain"):
    expected_count, remaining = (9, 1) if args.scenario == "stardust-chain" else (2, 2)
    assert len(result["deployed"]) == expected_count and all(
        c["id"] == "U_CARD_005" and c["target"] == "TEST_SUBJECT" and c["Remaining"] == remaining
        for c in result["deployed"]), result
elif args.scenario == "conversion":
    assert any(c["id"] == "SHIV" and c["pile"] == "Exhaust" for c in result["cards"]), result
else:
    assert all(c["pile"] in ("Hand", "Draw", "Discard") for c in result["cards"]), result
(out / "recorded-state.json").write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(dict(recording=recording, result=result), ensure_ascii=False), flush=True)
