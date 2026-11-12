"""Run the frozen per-card effect cases against an isolated, already launched game."""
import argparse
import json
import time
import urllib.request
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--port', type=int, required=True)
parser.add_argument('--scope', choices=('effects', 'full'), default='effects')
parser.add_argument('--id')
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
base = f'http://127.0.0.1:{args.port}/api/v1'


def request(path, body=None):
    req = urllib.request.Request(base + path,
        data=None if body is None else json.dumps(body).encode(),
        headers={'Content-Type': 'application/json'})
    with urllib.request.urlopen(req, timeout=30) as response:
        return json.load(response)


def action(name, **values):
    result = request('/singleplayer', dict(action=name, **values))
    if result.get('status') == 'error' or result.get('error'):
        raise RuntimeError(result)
    return result


def wait_state(predicate, timeout=90):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        state = request('/singleplayer?format=json')
        if predicate(state):
            return state
        time.sleep(.2)
    raise TimeoutError(state)


settings = request('/settings')
assert settings['silent_test'] and not settings.get('silent_test_error'), settings
state = request('/singleplayer?format=json')
if state['state_type'] == 'menu':
    action('menu_select', option='singleplayer')
    wait_state(lambda s: s.get('menu_screen') == 'character_select')
    action('menu_select', option='JUNNA')
    action('menu_select', option='confirm')
    state = wait_state(lambda s: s.get('player') and s['state_type'] in ('map', 'combat', 'event'))
    action('run_command', command='unlock all')
if state['state_type'] == 'map':
    state = wait_state(lambda s: bool(s.get('map', {}).get('next_options')))
    # An early vote may precede the map input becoming ready; retry only while still on the map.
    target = state['map']['next_options'][0]
    deadline = time.monotonic() + 60
    while time.monotonic() < deadline:
        state = request('/singleplayer?format=json')
        if state.get('battle', {}).get('is_play_phase'):
            break
        current_map = state.get('map', {})
        candidate = next((n for n in current_map.get('next_options', [])
            if n['col'] == target['col'] and n['row'] == target['row']), None)
        if candidate and not current_map.get('is_traveling'):
            action('choose_map_node', index=candidate['index'])
        time.sleep(2)
wait_state(lambda s: s.get('battle', {}).get('is_play_phase'))

started = time.time()
if args.scope == 'effects':
    result_file = root / 'artifacts/latest-card-pool/results/latest-effects.json'
    command = 'junna_latest_effects check'
else:
    result_file = root / ('out/tmp/full-card-tests/results/completed' + ('-' + args.id if args.id else '') + '.json')
    command = 'junna_full_audit check' + (' ' + args.id if args.id else '')
action('run_command', command=command)
print('Started', command, flush=True)
deadline = time.monotonic() + 7200
last_count = -1
while time.monotonic() < deadline:
    if result_file.exists() and result_file.stat().st_mtime >= started:
        try:
            result = json.loads(result_file.read_text(encoding='utf-8'))
        except json.JSONDecodeError:
            time.sleep(.2)
            continue
        if result.get('completed'):
            if args.scope == 'effects':
                failures = [c for c in result['results'] if not c['pass']]
                print(json.dumps({'completed': True, 'cases': len(result['results']), 'assertions': result['assertions'], 'failures': failures}, ensure_ascii=False), flush=True)
            else:
                files = list(result_file.parent.glob((args.id + '-' if args.id else '*') + '*-result.json'))
                rows = [json.loads(p.read_text(encoding='utf-8')) for p in files if p.stat().st_mtime >= started]
                failures = [r for r in rows if any(not c['pass'] for c in r['checks'])]
                summary = {'completed': True, 'revision': result['revision'], 'scenarios': len(rows),
                    'assertions': sum(len(r['checks']) for r in rows),
                    'failures': [{'id': r['id'], 'upgraded': r['upgraded'], 'variant': r['variant'],
                        'checks': [c for c in r['checks'] if not c['pass']]} for r in failures]}
                (result_file.parent / 'summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
                print(json.dumps(summary, ensure_ascii=False), flush=True)
            raise SystemExit(1 if failures else 0)
    if args.scope == 'full':
        count = sum(p.stat().st_mtime >= started for p in result_file.parent.glob('*-result.json'))
        if count != last_count:
            print('Completed scenarios', count, flush=True)
            last_count = count
    time.sleep(1)
raise TimeoutError('Inspect game log and partial per-card results before retrying.')
