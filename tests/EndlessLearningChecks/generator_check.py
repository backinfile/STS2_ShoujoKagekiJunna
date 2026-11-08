"""Check that catalog generation preserves learning's live energy value."""
import importlib.util
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('catalog_generator', root / 'tools/assets/generate-card-catalog.py')
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)
catalog = json.loads((root / 'data/card-pool.json').read_text(encoding='utf-8'))
card = next(card for card in catalog['cards'] if card['id'] == 'JK82')
texts = [re.sub(r'消耗。', '', card[field]) for field in ('text', 'upgradedText')]
normal, values = generator.bind_numbers(texts[0])
improved, upgraded_values = generator.bind_numbers(texts[1], values)
for value, upgraded_value in zip(values, upgraded_values):
    value['upgraded'] = upgraded_value['base']
names = {entry['name'] for entry in catalog['cards']}
description = generator.upgrade_description(
    generator.display_description(card, normal, names, values),
    generator.display_description(card, improved, names, values, upgraded=True))
assert '{N0:diff()}' in description, f'Energy was frozen during generation: {description}'
for language in ('zhs', 'eng'):
    localized = json.loads((root / f'ShoujoKagekiJunna/localization/{language}/cards.json').read_text(encoding='utf-8'))
    assert localized['JUNNA_JK82.description'] == description, language
print('PASS: generated learning description retains its live energy value in both languages.')
