"""Run one real end-turn regression against an isolated game with WorkbenchFitSync."""
import argparse
import json
import time
import urllib.request
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--port', type=int, required=True)
parser.add_argument('--scenario', choices=['base', 'upgraded', 'stacked', 'empty'], default='base')
parser.add_argument('--label', default='check')
parser.add_argument('--record', action='store_true')
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
output = root / 'out/tmp/study-animation'
output.mkdir(parents=True, exist_ok=True)
base = f'http://127.0.0.1:{args.port}/api/v1/'

def api(path='singleplayer?format=json', body=None):
    payload = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(base + path, data=payload, headers={'Content-Type': 'application/json'})
    with urllib.request.urlopen(req, timeout=15) as response:
        data = json.load(response)
    if data.get('status') == 'error':
        raise RuntimeError(data)
    return data

def wait_for(read, condition, seconds=20):
    deadline = time.monotonic() + seconds
    while True:
        value = read()
        if condition(value):
            return value
        if time.monotonic() >= deadline:
            raise TimeoutError(value)
        time.sleep(.1)

def ready(state):
    return state.get('battle', {}).get('is_play_phase') is True

def observe():
    try:
        return json.loads((output / 'observation.json').read_text(encoding='utf-8'))
    except (FileNotFoundError, json.JSONDecodeError):
        return {}

wait_for(api, ready)
api('singleplayer', {'action': 'run_command', 'command': 'junna_study_animation ' + args.scenario})
expected_sources = 2 if args.scenario == 'stacked' else 1
wait_for(observe, lambda data: data.get('scenario') == args.scenario and data.get('settlements') == 0)
state = wait_for(api, lambda data: ready(data) and len(data['player']['hand']) == expected_sources)
time.sleep(.6)
if args.record:
    recording = api('recording/start', {'output_dir': str(output / args.label), 'fps': 30, 'max_duration_seconds': 30,
                                      'width': 1280, 'height': 720, 'audio': True})
    if recording.get('audio_source') != 'engine_mix':
        raise RuntimeError(recording)
    time.sleep(.5)
for _ in range(expected_sources):
    state = api()
    card = next(card for card in state['player']['hand'] if card['name'].startswith('埋头学习'))
    before = len(state['player']['hand'])
    api('singleplayer', {'action': 'play_card', 'card_index': card['index']})
    wait_for(api, lambda data: ready(data) and len(data['player']['hand']) < before)
    time.sleep(.4)
round_before = api()['battle']['round']
api('singleplayer', {'action': 'end_turn'})
data = wait_for(observe, lambda data: data.get('settlements') == 1)
wait_for(api, lambda state: ready(state) and state['battle']['round'] > round_before)
if data['previews']:
    data = wait_for(observe, lambda data: data.get('completedFlies') == data.get('expected'))
else:
    time.sleep(.5)
    data = observe()
if args.record:
    time.sleep(.5)
    api('recording/stop', {})
    recording = wait_for(lambda: api('recording'), lambda data: data.get('status') in ['completed', 'failed'])
    (output / (args.label + '-recording.json')).write_text(json.dumps(recording, indent=2), encoding='utf-8')
insertion = data['insertion']
expected = data['expected']
checks = {
    'generated_count': len(insertion['generated']) == expected,
    'base_version_random_draw': all(card['id'] == 'B' and not card['upgraded'] and card['position'] == 'Random'
                                   and card['pile'] == 'Draw' and card['index'] >= 0 for card in insertion['generated']),
    'draw_count': insertion['drawCount'] == insertion['originalCount'] + expected,
    'original_order': insertion['originalOrderPreserved'],
    'previews': data['previews'] == expected,
    'flies': data['flies'] == expected,
    'completed_flies': data['completedFlies'] == expected,
}
data['checks'] = checks
data['pass'] = all(checks.values())
(output / (args.label + '.json')).write_text(json.dumps(data, indent=2), encoding='utf-8')
print(json.dumps(data, ensure_ascii=False))
raise SystemExit(0 if data['pass'] else 1)