"""Record the first-slice UI check through the MCP mod in an already running isolated combat."""
import json
import os
import subprocess
import time
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'artifacts/dev-game'
BASE = 'http://127.0.0.1:15727/api/v1'
events = []
started = time.monotonic()

def request(path, data=None):
    body = None if data is None else json.dumps(data).encode()
    req = urllib.request.Request(BASE + path, body, {'Content-Type': 'application/json'})
    with urllib.request.urlopen(req, timeout=15) as response:
        return json.load(response)

def state():
    return request('/singleplayer?format=json')

def action(name, **kwargs):
    result = request('/singleplayer', {'action': name, **kwargs})
    assert result.get('status') == 'ok', result
    return result

def command(text):
    return action('run_command', command=text)

def wait(predicate, seconds=12):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        current = state()
        if predicate(current):
            return current
        time.sleep(.15)
    raise AssertionError(current)

def note(name, current=None):
    current = current or state()
    events.append({'time': round(time.monotonic() - started, 3), 'name': name, 'state': current})

def screenshot(name):
    with urllib.request.urlopen(BASE + '/screenshot', timeout=15) as response:
        (OUT / name).write_bytes(response.read())

def play(card_id, target=None):
    current = state()
    card = next(c for c in current['player']['hand'] if c['id'] == card_id)
    params = {'card_index': card['index']}
    if target:
        params['target'] = target
    action('play_card', **params)
    time.sleep(.7)

def select_arrow():
    current = wait(lambda s: s['state_type'] == 'hand_select')
    arrow = next(c for c in current['hand_select']['cards'] if c['id'] == 'JUNNA_FULL_DRAW')
    action('combat_select_card', card_index=arrow['index'])
    current = wait(lambda s: s['state_type'] != 'hand_select' or s['hand_select']['can_confirm'])
    if current['state_type'] == 'hand_select':
        action('combat_confirm_selection')
    wait(lambda s: s['state_type'] != 'hand_select')
    time.sleep(.6)

current = state()
assert current.get('battle', {}).get('is_play_phase'), current.get('state_type')
target = current['battle']['enemies'][0]['entity_id']
command('energy 10')
for model in ['JUNNA_FULL_DRAW', 'JUNNA_WARM_UP', 'JUNNA_REPEATED_PRACTICE']:
    command('card ' + model)
    time.sleep(.3)
request('/recording/start', {'fps':30, 'width':1280, 'height':720, 'max_duration_seconds':60, 'audio':True})
started = time.monotonic()
try:
    time.sleep(1)
    screenshot('idle-04.png')
    note('idle_04')
    play('JUNNA_STRIKE', target)
    note('attack_02')
    play('JUNNA_WARM_UP')
    select_arrow()
    note('warm_up_arrow')
    play('JUNNA_REPEATED_PRACTICE')
    select_arrow()
    current = state()
    arrow = next(c for c in current['player']['hand'] if c['id'] == 'JUNNA_FULL_DRAW')
    assert '伤害加成 +2' in arrow['description'] and '额外伤害 +1次' in arrow['description'], arrow
    action('hover_card', card_index=arrow['index'])
    time.sleep(1.2)
    screenshot('strengthened-arrow.png')
    action('unhover_card')
    play('JUNNA_FULL_DRAW', target)
    time.sleep(1)
    screenshot('arrow-deployed.png')
    note('arrow_deployed_3')
    hp = state()['battle']['enemies'][0]['hp']
    for count in (2, 1, 0):
        old_round = state()['battle']['round']
        action('end_turn')
        current = wait(lambda s: s.get('battle',{}).get('is_play_phase') and s['battle']['round'] > old_round)
        time.sleep(1)
        current = state()
        note('arrow_count_' + str(count), current)
        expected = hp if count else hp - 54
        assert current['battle']['enemies'][0]['hp'] == expected, (count, current['battle']['enemies'][0])
    screenshot('arrow-resolved.png')
    time.sleep(1)
finally:
    request('/recording/stop', {})
    deadline = time.monotonic() + 15
    while time.monotonic() < deadline:
        recording = request('/recording')
        if recording['status'] not in ('recording', 'stopping'):
            break
        time.sleep(.25)
    (OUT / 'visual-check.json').write_text(json.dumps({'events':events, 'recording':recording,
        'settings':request('/settings')},ensure_ascii=False,indent=2),encoding='utf-8')
    if recording['status'] == 'completed':
        # Windows Store Python redirects AppData reads; PowerShell uses the recorder's actual path.
        subprocess.run(['powershell.exe', '-NoProfile', '-Command',
            'Copy-Item -LiteralPath $env:JUNNA_RECORDING_SOURCE -Destination $env:JUNNA_RECORDING_DESTINATION'],
            env={**os.environ, 'JUNNA_RECORDING_SOURCE':recording['output_path'],
                 'JUNNA_RECORDING_DESTINATION':str(OUT / 'junna-first-slice.mp4')},
            check=True, creationflags=subprocess.CREATE_NO_WINDOW)
print(json.dumps({'video':str(OUT / 'junna-first-slice.mp4'), 'events':len(events), 'recording':recording},ensure_ascii=False))
