"""Drive the real Architect proceed button and require the native victory screen."""
import argparse
import json
import time
import urllib.request

parser = argparse.ArgumentParser()
parser.add_argument("--base-url", required=True, help="MCP /api/v1 URL for the isolated test game")
parser.add_argument("--timeout", type=float, default=12)
args = parser.parse_args()

def request(data=None):
    path = "/singleplayer?format=json" if data is None else "/singleplayer"
    body = None if data is None else json.dumps(data).encode("utf-8")
    req = urllib.request.Request(args.base_url.rstrip("/") + path, body, {"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=15) as response:
        return json.load(response)

state = request()
assert state.get("state_type") == "event" and state.get("event", {}).get("event_id") == "THE_ARCHITECT", state.get("state_type")
chosen = False
end = time.monotonic() + args.timeout
while time.monotonic() < end:
    state = request()
    if state.get("state_type") == "game_over":
        assert state.get("game_over", {}).get("victory") is True, state.get("game_over")
        print(json.dumps({"pass": True, "state_type": "game_over", "game_over": state["game_over"]}))
        break
    options = state.get("event", {}).get("options", [])
    if options and not chosen:
        result = request({"action": "choose_event_option", "index": options[0]["index"]})
        assert result.get("status") == "ok", result
        chosen = True
    time.sleep(0.2)
else:
    raise AssertionError(json.dumps({"pass": False, "state_type": state.get("state_type"), "event": state.get("event")}))