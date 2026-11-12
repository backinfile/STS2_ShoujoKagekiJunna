"""Observe a last-shine JS19 play, native victory, reward selection and deck flight."""
import argparse
import json
import time
import urllib.request
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--port', type=int, required=True)
p.add_argument('--upgrade', action='store_true')
p.add_argument('--record', action='store_true')
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
out = a.output.resolve()
out.mkdir(parents=True, exist_ok=True)
live_file = Path(__file__).resolve().parents[2] / 'artifacts/latest-card-pool/copy-reward/live.json'
base = f'http://127.0.0.1:{a.port}/api/v1'
checks = []


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


def snapshot():
    s = req('/singleplayer?format=json')
    try:
        v = json.loads(live_file.read_text(encoding='utf-8'))
    except (FileNotFoundError, json.JSONDecodeError):
        v = None
    return s, v


def wait(predicate, timeout=45):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        s, v = snapshot()
        if predicate(s, v):
            return s, v
        time.sleep(.15)
    (out / 'timeout.json').write_text(json.dumps({'state': s, 'live': v}, ensure_ascii=False, indent=2), encoding='utf-8')
    raise TimeoutError('See timeout.json')


def check(name, actual, expected):
    checks.append(dict(name=name, actual=actual, expected=expected, passed=actual == expected))


s, _ = wait(lambda s, v: s['state_type'] == 'menu' or (s.get('player') and s['state_type'] not in ('menu', 'loading')))
if s['state_type'] == 'menu':
    act('menu_select', option='continue')
    s, _ = wait(lambda s, v: s.get('player') and s['state_type'] not in ('menu', 'loading'))
if not s.get('battle', {}).get('is_play_phase'):
    if s['state_type'] == 'rewards':
        act('proceed')
        wait(lambda s, v: s['state_type'] == 'map')
    act('run_command', command='fight NIBBITS_WEAK')
    wait(lambda s, v: s.get('battle', {}).get('is_play_phase'))
start = time.time()
act('run_command', command='junna_copy_reward prepare' + (' upgrade' if a.upgrade else ''))
s, v = wait(lambda s, v: live_file.exists() and live_file.stat().st_mtime >= start
    and v and not v['sourceDepleted'] and v['upgraded'] == a.upgrade and len(s['player']['hand']) == 2)
time.sleep(.6)
recording = False
try:
    if a.record:
        req('/recording/start', dict(output_dir=str(out / 'video'), fps=15, width=1280, height=720, audio=True, max_duration_seconds=120))
        recording = True
    index = next(i for i, c in enumerate(s['player']['hand']) if c['id'] == 'JUNNA_JS19')
    act('hover_card', card_index=index)
    time.sleep(.7 if a.record else .1)
    act('unhover_card')
    act('play_card', card_index=index, target=s['battle']['enemies'][0]['entity_id'])
    s, v = wait(lambda s, v: v and v['sourceDepleted'])
    check('depletion adds a pending knowledge-copy power', v['pendingPower'], 1)
    check('depletion queues exactly one versioned reward', v['rewardMaximums'], [2 if a.upgrade else 1])
    check('depletion does not copy before reward selection', v['copyCount'], 0)
    (out / 'after-depletion.json').write_text(json.dumps({'state': s, 'live': v}, ensure_ascii=False, indent=2), encoding='utf-8')
    if a.record:
        time.sleep(1)
    index = next(i for i, c in enumerate(s['player']['hand']) if c['id'] == 'JUNNA_JK275')
    act('play_card', card_index=index)
    s, v = wait(lambda s, v: s['state_type'] == 'rewards')
    (out / 'rewards.json').write_text(json.dumps(s, ensure_ascii=False, indent=2), encoding='utf-8')
    reward = next(r for r in s['rewards']['items'] if r['type'] == 'knowledgecopyreward')
    time.sleep(.5)
    act('claim_reward', index=reward['index'])
    s, _ = wait(lambda s, v: s['state_type'] == 'card_select')
    expected = 2 if a.upgrade else 1
    targets = []
    for card_id in ['JUNNA_JK122', 'JUNNA_JK49B'][:expected]:
        index = next(i for i, c in enumerate(s['card_select']['cards']) if c['id'] == card_id)
        targets.append(index)
    time.sleep(.5)
    for index in targets:
        act('select_card', index=index)
        time.sleep(.15)
    act('confirm_selection')
    s, v = wait(lambda s, v: v and v['copyReturns'] == 1)
    time.sleep(3)
    s, v = snapshot()
    check('selected knowledge cards enter permanent deck', v['copyCount'], expected)
    check('copied cards use Deck destination', [c['pile'] for c in v['copies']], ['Deck'] * expected)
    check('original upgraded card is copied upgraded', next(c['IsUpgraded'] for c in v['copies'] if c['id'] == 'JK122'), True)
    check('each copy receives a visible preview', v['previews'], expected)
    check('each copy flies into the deck', v['flies'], expected)
    check('each deck flight finishes', v['completedFlies'], expected)
    check('deck counter receives completion notifications', v['addFinished'], expected)
    check('copy resolution waits for deck flights', v['completedAtReturn'], expected)
    (out / 'after-copy.json').write_text(json.dumps({'state': s, 'live': v}, ensure_ascii=False, indent=2), encoding='utf-8')
finally:
    if recording:
        req('/recording/stop', {})
        deadline = time.monotonic() + 45
        while time.monotonic() < deadline:
            r = req('/recording')
            if r['status'] in ('completed', 'failed'):
                break
            time.sleep(.2)
        (out / 'recording.json').write_text(json.dumps(r, indent=2), encoding='utf-8')
    (out / 'results.json').write_text(json.dumps(checks, indent=2), encoding='utf-8')
failed = [c for c in checks if not c['passed']]
print(json.dumps({'assertions': len(checks), 'failures': failed}, ensure_ascii=False), flush=True)
raise SystemExit(bool(failed))
