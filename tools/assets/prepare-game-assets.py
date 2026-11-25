"""Copy existing art from an MCP export; this never edits workbench persistence."""
import base64
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BUNDLE = json.loads((ROOT / '.tools/implementation/workbench.json').read_text(encoding='utf-8'))
OUT = ROOT / 'ShoujoKagekiJunna'
CARDS = {
    'JB01': ('JUNNA_STRIKE', '造成{Damage:diff()}点伤害。'),
    'JB02': ('JUNNA_DEFEND', '获得{Block:diff()}点[gold]格挡[/gold]。'),
    'JB03': ('JUNNA_FULL_DRAW', '造成{Damage:diff()}点伤害。'),
}

def write(relative, content):
    p = OUT / relative
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(content, encoding='utf-8')

loc = {}
sources = []
for card in BUNDLE['data']['cards']:
    if card['id'] not in CARDS:
        continue
    model, description = CARDS[card['id']]
    art = BUNDLE['assets'][card['art']]
    assert art.startswith('data:image/jpeg;base64,'), card['id']
    path = OUT / 'images/cards' / (card['id'] + '.jpg')
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(base64.b64decode(art.split(',', 1)[1]))
    loc[model + '.title'] = card['name']
    loc[model + '.description'] = description.replace('NL', '\n')
    sources.append({k: card.get(k) for k in ('uid','id','name','cost','upgradedCost','text','upgradedText','turns','upgradedTurns','art')})

for filename, source in {
    'portrait.png': 'output/imagegen/junna-redesign-2026-11-04/clean/04-glasses-rest.png',
    'aim.png': 'output/imagegen/junna-redesign-2026-11-04/clean/02-side-draw.png',
    'rest.png': 'output/imagegen/junna-redesign-2026-11-04/clean/04-glasses-rest.png',
    'energy.png': 'output/mi-ripple/2026-11-02/results/ui/ui-energy.png',
}.items():
    shutil.copyfile(ROOT / source, OUT / 'images' / filename)

characters = {
    'JUNNA.title': '星见纯那', 'JUNNA.titleObject': '星见纯那',
    'JUNNA.description': '圣翔音乐学园第99期生。',
    'JUNNA.pronounSubject': '她', 'JUNNA.pronounObject': '她',
    'JUNNA.pronounPossessive': '她的', 'JUNNA.possessiveAdjective': '她的',
    'JUNNA.cardsModifierTitle': '星见纯那卡牌',
    'JUNNA.cardsModifierDescription': '星见纯那卡牌会出现在奖励与商店中。',
    'JUNNA.banter.alive.endTurnPing': '准备好了。',
    'JUNNA.banter.dead.endTurnPing': '……', 'JUNNA.eventDeathPrevention': '我还要继续。',
    'JUNNA.goldMonologue': '……', 'JUNNA.aromaPrinciple': '……',
}
ui = {'JUNNA.SELECT_DAMAGE_CARD': '选择一张牌', 'JUNNA.CARD_TYPE.ARROW': '飞矢'}
for language in ('zhs', 'eng'):
    for table, values in {'cards':loc, 'characters':characters, 'gameplay_ui':ui}.items():
        write(f'localization/{language}/{table}.json', json.dumps(values, ensure_ascii=False, indent=2))
spec = ROOT / 'docs/implementation/first-slice-design.json'
spec.parent.mkdir(parents=True, exist_ok=True)
spec.write_text(json.dumps({'workbenchRevision': BUNDLE['revision'], 'cards': sources,
    'startingDeck': BUNDLE['data']['meta']['startingDeck'],
    'damageModifierRules': BUNDLE['data']['meta']['damageModifierRules']}, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'Copied {len(sources)} card portraits and existing character assets; workbench revision {BUNDLE["revision"]}.')
