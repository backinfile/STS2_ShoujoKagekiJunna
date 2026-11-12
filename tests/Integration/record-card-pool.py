"""Record main resolutions using legal game actions after fixture preparation."""
import argparse
import json
import time
import urllib.request
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--port', type=int, required=True)
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
base = f'http://127.0.0.1:{a.port}/api/v1'
output = a.output.resolve()
output.mkdir(parents=True, exist_ok=True)
checks = []


def req(path, body=None):
    r = urllib.request.Request(base + path, data=None if body is None else json.dumps(body).encode(),
        headers={'Content-Type': 'application/json'})
    with urllib.request.urlopen(r, timeout=30) as response:
        return json.load(response)


def act(action, **kw):
    r = req('/singleplayer', dict(action=action, **kw))
    if r.get('status') == 'error' or r.get('error'):
        raise RuntimeError(r)
    return r


def state():
    return req('/singleplayer?format=json')


def wait(predicate, timeout=30):
    end = time.monotonic() + timeout
    while time.monotonic() < end:
        s = state()
        if predicate(s):
            return s
        time.sleep(.1)
    raise TimeoutError(s)


def check(name, actual, expected):
    checks.append(dict(name=name, actual=actual, expected=expected, passed=actual == expected))
    if actual != expected:
        raise AssertionError(checks[-1])


def play(design_id, draw=0):
    s = state()
    index = next(i for i, c in enumerate(s['player']['hand']) if c['id'] == 'JUNNA_' + design_id)
    before_hand = len(s['player']['hand'])
    before_draw = s['player']['draw_pile_count']
    target = {'target': s['battle']['enemies'][0]['entity_id']} if s['player']['hand'][index]['target_type'] == 'AnyEnemy' else {}
    act('hover_card', card_index=index)
    time.sleep(.7)
    act('unhover_card')
    act('play_card', card_index=index, **target)
    s = wait(lambda s: s['player']['draw_pile_count'] == before_draw - draw
        and len(s['player']['hand']) == before_hand - 1 + draw)
    time.sleep(.7)
    return s


for scenario, source in [('response', 'U_CARD_016'), ('strike', 'U_CARD_017'), ('tutor', 'JS33')]:
    # Developer setup ends before recording begins.
    act('run_command', command='junna_latest_effects prepare ' + scenario)
    expected_hand, expected_draw = {'response': (4, 24), 'strike': (2, 0), 'tutor': (1, 3)}[scenario]
    wait(lambda s: len(s['player']['hand']) == expected_hand and s['player']['draw_pile_count'] == expected_draw
        and any(c['id'] == 'JUNNA_' + source for c in s['player']['hand']))
    time.sleep(1)
    rec = req('/recording/start', dict(output_dir=str(output / scenario), fps=15,
        width=1280, height=720, audio=True, max_duration_seconds=120))
    try:
        time.sleep(.3)
        if scenario == 'response':
            play(source)
            for cycle in range(2):
                for card in ['JK49B', 'JA38', 'JS35']:
                    before = state()['player']['draw_pile_count']
                    after = play(card, draw=1)
                    check(f'cycle {cycle + 1} {card} draws exactly one', before - after['player']['draw_pile_count'], 1)
                if cycle == 0:
                    old_round = state()['battle']['round']
                    act('end_turn')
                    wait(lambda s: s.get('battle', {}).get('is_play_phase') and s['battle']['round'] > old_round)
                    time.sleep(.4)
        elif scenario == 'strike':
            hp = state()['battle']['enemies'][0]['hp']
            play(source)
            after = wait(lambda s: s['battle']['enemies'][0]['hp'] == hp - 24)
            check('one triple-category hand card produces four hits', hp - after['battle']['enemies'][0]['hp'], 24)
        else:
            s = state()
            index = next(i for i, c in enumerate(s['player']['hand']) if c['id'] == 'JUNNA_JS33')
            act('hover_card', card_index=index)
            time.sleep(.8)
            act('unhover_card')
            act('play_card', card_index=index)
            choice = wait(lambda s: s['state_type'] == 'card_select')
            options = choice['card_select']['cards']
            index = next(i for i, c in enumerate(options) if c['id'] == 'JUNNA_PREPARE_SHINE')
            deadline = time.monotonic() + 20
            while time.monotonic() < deadline:
                current = state()
                if current['state_type'] != 'card_select':
                    break
                act('select_card', index=index)
                if current['card_select'].get('can_confirm'):
                    act('confirm_selection')
                time.sleep(.5)
            after = wait(lambda s: s.get('player') and len(s['player']['hand']) == 3)
            check('upgraded shine option draws three actual shine cards',
                sum(c['id'] == 'JUNNA_JS35' for c in after['player']['hand']), 3)
            time.sleep(1)
    finally:
        req('/recording/stop', {})
        end = time.monotonic() + 45
        while time.monotonic() < end:
            rec = req('/recording')
            if rec['status'] in ('completed', 'failed'):
                break
            time.sleep(.2)
        (output / (scenario + '-recording.json')).write_text(json.dumps(rec, indent=2), encoding='utf-8')
        if rec['status'] != 'completed':
            raise RuntimeError(rec)
    print(scenario, 'completed', flush=True)
(output / 'legal-actions-results.json').write_text(json.dumps(checks, indent=2), encoding='utf-8')
