"""Run targeted pile-animation checks in an isolated game with WorkbenchFitSync."""
import argparse
import json
import time
import urllib.request
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--port', type=int, required=True)
parser.add_argument('--label', default='related-check')
args = parser.parse_args()
output = Path(__file__).resolve().parents[2] / 'out/tmp/study-animation'
completed = output / 'related.completed'
completed.unlink(missing_ok=True)
deadline = time.monotonic() + 30
while True:
    with urllib.request.urlopen(f'http://127.0.0.1:{args.port}/api/v1/singleplayer?format=json', timeout=15) as response:
        state = json.load(response)
    if state.get('battle', {}).get('is_play_phase'):
        break
    if time.monotonic() >= deadline:
        raise TimeoutError('Game did not reach the player play phase')
    time.sleep(.1)
request = urllib.request.Request(f'http://127.0.0.1:{args.port}/api/v1/singleplayer',
    data=json.dumps({'action': 'run_command', 'command': 'junna_pile_animation check'}).encode(),
    headers={'Content-Type': 'application/json'})
with urllib.request.urlopen(request, timeout=15) as response:
    result = json.load(response)
if result.get('status') != 'ok':
    raise RuntimeError(result)
deadline = time.monotonic() + 180
while not completed.exists():
    if time.monotonic() >= deadline:
        raise TimeoutError('Pile animation probe did not finish')
    time.sleep(.2)
rows = json.loads((output / 'related.json').read_text(encoding='utf-8'))
(output / (args.label + '.json')).write_text(json.dumps(rows, indent=2), encoding='utf-8')
for row in rows:
    print(json.dumps(row, ensure_ascii=False))
passed = sum(row['pass'] for row in rows)
print(f'{passed}/{len(rows)} passed')
raise SystemExit(0 if len(rows) == 11 and passed == len(rows) else 1)