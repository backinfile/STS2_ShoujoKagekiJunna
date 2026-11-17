"""Play JF13 through MCP and assert native Burning Sticks' Shine boundary."""
import argparse
import json
import time
import urllib.request
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--port', type=int, required=True)
p.add_argument('--remaining', type=int, choices=[1, 2], default=1)
p.add_argument('--upgrade', action='store_true')
p.add_argument('--record', action='store_true')
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
root = Path(__file__).resolve().parents[2]
live = root / 'out/tmp/burning-sticks/live.json'
out = a.output.resolve()
out.mkdir(parents=True, exist_ok=True)
base = f'http://127.0.0.1:{a.port}/api/v1'


def req(path, body=None):
    r = urllib.request.Request(base + path, data=None if body is None else json.dumps(body).encode(),
        headers={'Content-Type': 'application/json'})
    with urllib.request.urlopen(r, timeout=30) as response:
        value = json.load(response)
    if value.get('status') == 'error' or value.get('error'):
        raise RuntimeError(value)
    return value


def act(action, **kw):
    return req('/singleplayer', dict(action=action, **kw))


def wait(predicate, timeout=20):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        state = req('/singleplayer?format=json')
        if predicate(state):
            return state
        time.sleep(.1)
    raise TimeoutError(state)


started = time.time()
act('run_command', command=f'junna_burning_sticks prepare {a.remaining}' + (' upgrade' if a.upgrade else ''))
s = wait(lambda s: live.exists() and live.stat().st_mtime >= started and
    len(s.get('player', {}).get('hand', [])) == 1 and s['player']['hand'][0]['id'] == 'JUNNA_JF13')
before = json.loads(live.read_text(encoding='utf-8'))
recording = None
try:
    if a.record:
        req('/recording/start', dict(output_dir=str(out / 'video'), fps=15, width=1280, height=720,
            audio=True, max_duration_seconds=30))
    act('hover_card', card_index=0)
    time.sleep(.8 if a.record else .1)
    act('unhover_card')
    act('play_card', card_index=0)
    wait(lambda s: all(c['id'] != 'JUNNA_JF13' or len(s['player']['hand']) > 1 for c in s['player']['hand']))
    time.sleep(2 if a.record else .6)
    s = req('/singleplayer?format=json')
    if a.record and s['player']['hand']:
        act('hover_card', card_index=len(s['player']['hand']) - 1)
        time.sleep(1.5)
        act('unhover_card')
finally:
    if a.record:
        req('/recording/stop', {})
        deadline = time.monotonic() + 30
        while time.monotonic() < deadline:
            recording = req('/recording')
            if recording['status'] in ('completed', 'failed'):
                break
            time.sleep(.2)
        (out / 'recording.json').write_text(json.dumps(recording, indent=2), encoding='utf-8')

act('run_command', command='junna_burning_sticks inspect')
after = json.loads(live.read_text(encoding='utf-8'))
checks = []


def check(name, actual, expected):
    checks.append(dict(name=name, actual=actual, expected=expected, passed=actual == expected))


shell = [c for c in after['hand'] if c['id'] == 'SHOUJOKAGEKI-CORE_EMPTY_SHELL']
copies = [c for c in after['hand'] if c['id'] == 'JUNNA_JF13']
check('native block resolves', after['block'] - before['block'], 8 if a.upgrade else 5)
check('native draw resolves', sum(c['id'] == 'DEFEND_IRONCLAD' for c in after['hand']), 1)
check('Shine spent exactly once', after['sourceRemaining'], a.remaining - 1)
check('Burning Sticks is consumed', after['relicUsed'], True)
check('one empty shell only at last Shine', len(shell), 1 if a.remaining == 1 else 0)
check('remaining Shine produces one live copy', len(copies), 0 if a.remaining == 1 else 1)
check('last Shine removes the combat source', after['sourceRemoved'], a.remaining == 1)
check('last Shine removes the permanent source', after['permanentRemoved'], a.remaining == 1)
check('ordinary exhaust keeps its native destination', 'JUNNA_JF13' in after['exhaust'], a.remaining > 1)
if copies:
    check('copy retains remaining Shine', copies[0]['remaining'], 1)
    check('copy retains upgrade', copies[0]['IsUpgraded'], a.upgrade)
if a.record:
    check('recording completes', recording['status'], 'completed')
    check('recording keeps internal audio', recording.get('audio_source'), 'engine_mix')
(out / 'results.json').write_text(json.dumps(dict(before=before, after=after, checks=checks,
    state=s), ensure_ascii=False, indent=2), encoding='utf-8')
failed = [c for c in checks if not c['passed']]
print(json.dumps(dict(assertions=len(checks), failures=failed), ensure_ascii=False), flush=True)
raise SystemExit(bool(failed))
