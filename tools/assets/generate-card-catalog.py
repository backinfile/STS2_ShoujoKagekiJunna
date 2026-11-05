"""Generate runtime metadata and portraits from a versioned workbench MCP export.

The export is read-only. Effects are implemented explicitly in CardEffects.cs.
"""
import argparse
import base64
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EXISTING = {
    'JB01': ('JunnaStrike', 'JUNNA_STRIKE'), 'JB02': ('JunnaDefend', 'JUNNA_DEFEND'),
    'JB03': ('JunnaFullDraw', 'JUNNA_FULL_DRAW'), 'JG09': ('JunnaWarmUp', 'JUNNA_WARM_UP'),
    'JG03': ('JunnaRepeatedPractice', 'JUNNA_REPEATED_PRACTICE'),
    'JA16': ('JunnaGuidingStar', 'JUNNA_GUIDING_STAR'),
    'JA17': ('JunnaBreakEncirclement', 'JUNNA_BREAK_ENCIRCLEMENT'),
    'JH10': ('JunnaTurningSlash', 'JUNNA_TURNING_SLASH'),
    'JG23': ('JunnaEnduranceTraining', 'JUNNA_ENDURANCE_TRAINING'),
}

def display_description(card, text, card_names, values, upgraded=False):
    """Format the workbench wording without substituting independently curated rules."""
    numbers = {value['name']: value['upgraded' if upgraded else 'base'] for value in values}

    def energy(match):
        value = numbers[match[1]]
        icons = '{energyPrefix:energyIcons(' + str(value if 1 <= value <= 3 else 1) + ')}'
        return icons if 1 <= value <= 3 else match[0].removesuffix('点能量') + icons

    text = re.sub(r'((?:可)?保留)(\{\w+:diff\(\)\}|\d+)点能量',
                  lambda m: m[1] + m[2] + '{energyPrefix:energyIcons(1)}', text)
    text = re.sub(r'(\{\w+:diff\(\)\}|\d+)点能量(?=(?:并|且)(?:可)?保留(?:到|至)下(?:个)?回合)',
                  lambda m: m[1] + '{energyPrefix:energyIcons(1)}', text)
    text = re.sub(r'\{(\w+):diff\(\)\}点能量', energy, text)
    text = re.sub(r'(\d+)点能量', lambda m: '{energyPrefix:energyIcons(' + m[1] + ')}', text)
    # Arrow countdowns are rendered on their existing plaque.
    text = re.sub(r'^\[blue\][^\n]*\n', '', text)
    text = re.sub(r'。\s*', '。\n', text).strip()
    highlighted = {'格挡', '易伤', '虚弱', '知识', '星矢', '伤害加成', '保留', '升级', '手牌', '抽牌堆', '弃牌堆', '消耗牌堆'}
    # Only actual named references need emphasis; all replacements run once to avoid nested tags.
    highlighted.update(card_names)
    highlighted.update(name + '+' for name in card_names)
    pattern = '|'.join(re.escape(word) for word in sorted(highlighted, key=len, reverse=True))
    text = re.sub(pattern, lambda m: '[gold]' + m[0] + '[/gold]', text)
    return text


def bind_numbers(text, prior=None):
    values = []
    used = set()

    def replace(match):
        index = len(values)
        tail = text[match.end():]
        if prior:
            name = prior[index]['name']
        elif tail.startswith('点伤害加成'):
            name = 'Bonus'
        elif tail.startswith('点伤害'):
            name = 'Damage'
        elif tail.startswith('点格挡'):
            name = 'Block'
        elif tail.startswith('次') and text[:match.start()].endswith('点伤害'):
            name = 'Repeat'
        else:
            name = f'N{index}'
        if name in used:
            name = f'N{index}'
        used.add(name)
        values.append({'name': name, 'base': int(match.group())})
        return '{' + name + ':diff()}'

    result = re.sub(r'\d+', replace, text)
    return result, values


def arrow_portraits_only(bundle):
    """Sync arrow art and focal points while preserving all runtime game rules."""
    catalog_path = ROOT / 'docs/implementation/card-pool.json'
    catalog = json.loads(catalog_path.read_text(encoding='utf-8'))
    source = {c['id']: c for c in bundle['data']['cards']
              if c['type'] == '星矢' and not c.get('trashedAt') and not c.get('reservedAt')}
    count = 0
    for entry in catalog['cards']:
        card = source.get(entry['id'])
        if card is None:
            continue
        focus = card.get('arrowFocus')
        if focus is not None:
            if not all(isinstance(focus.get(axis), (int, float)) and 0 <= focus[axis] <= 1 for axis in ('x', 'y')):
                raise ValueError('Invalid arrowFocus: ' + card['id'])
            entry['arrowFocus'] = {axis: focus[axis] for axis in ('x', 'y')}
        else:
            entry.pop('arrowFocus', None)
        image_bytes = base64.b64decode(bundle['assets'][card['art']].split(',', 1)[1])
        extension = '.png' if image_bytes.startswith(b'\x89PNG\r\n\x1a\n') else '.jpg'
        entry['portrait'] = card['id'] + extension
        portrait = ROOT / 'ShoujoKagekiJunna/images/cards' / entry['portrait']
        if not portrait.exists() or portrait.read_bytes() != image_bytes:
            portrait.write_bytes(image_bytes)
        count += 1
    missing = source.keys() - {c['id'] for c in catalog['cards']}
    if missing:
        raise ValueError('Arrow cards missing from runtime catalog: ' + ', '.join(sorted(missing)))
    catalog['arrowArtRevision'] = bundle['revision']
    catalog_path.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'Updated {count} arrow portraits and focal points; runtime card rules preserved.')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('snapshot', type=Path)
    parser.add_argument('--text-only', action='store_true', help='Refresh display localization without rewriting card data, models or portraits.')
    parser.add_argument('--rules-only', action='store_true', help='Refresh runtime rules and localization while preserving portraits and model declarations.')
    parser.add_argument('--arrow-portraits-only', action='store_true', help='Refresh arrow portraits and focal points without changing runtime card rules or localization.')
    args = parser.parse_args()
    bundle = json.loads(args.snapshot.read_text(encoding='utf-8-sig'))
    if args.arrow_portraits_only:
        arrow_portraits_only(bundle)
        return
    cards = [c for c in bundle['data']['cards'] if not c.get('trashedAt') and not c.get('reservedAt')]
    existing = json.loads((ROOT / 'docs/implementation/card-pool.json').read_text(encoding='utf-8')) if args.rules_only else None
    existing_cards = {card['id']: card for card in existing['cards']} if existing else {}
    definitions = []
    wrappers = []
    localization = {}
    card_names = {c['name'] for c in cards}
    for card in cards:
        cid = card['id']
        cls, model_id = EXISTING.get(cid, ('JunnaCard' + cid, 'JUNNA_' + cid))
        base = card['text'].strip()
        upgrade = card['upgradedText'].strip()
        # Native keywords are rendered by CardModel; knowledge keeps its printed position.
        for keyword in ('保留', '消耗'):
            base = base.replace(keyword + '。', '')
            upgrade = upgrade.replace(keyword + '。', '')
        base, values = bind_numbers(base)
        upgrade, upgraded_values = bind_numbers(upgrade, values)
        assert len(values) == len(upgraded_values), cid
        for value, improved in zip(values, upgraded_values):
            value['upgraded'] = improved['base']
        if cid == 'JA81B':
            values.append({'name': 'Damage', 'base': 0, 'upgraded': 0})
            base = base.replace('X', '{Damage:diff()}')
            upgrade = upgrade.replace('X', '{Damage:diff()}')
        if card['type'] == '星矢':
            values.append({'name': 'Turns', 'base': card['turns'], 'upgraded': card['upgradedTurns']})
        # Existing implementations use their established variable names.
        if cid in ('JA16', 'JA17'):
            base = re.sub(r'\{N\d+:diff\(\)\}', str(1 if cid == 'JA16' else 2), base)
            upgrade = re.sub(r'\{N\d+:diff\(\)\}', str(1 if cid == 'JA16' else 2), upgrade)
        if cid == 'JG09':
            base = base.replace('{N1:diff()}', '1')
            upgrade = upgrade.replace('{N1:diff()}', '1')
        if cid in ('JG03', 'JG23'):
            for value in values:
                base = base.replace('{' + value['name'] + ':diff()}', str(value['base']))
                upgrade = upgrade.replace('{' + value['name'] + ':diff()}', str(value['upgraded']))
        localization[model_id + '.title'] = card['name']
        normal = display_description(card, base, card_names, values)
        improved = display_description(card, upgrade, card_names, values, upgraded=True)
        localization[model_id + '.description'] = normal if normal == improved else '{IfUpgraded:show:' + improved + '|' + normal + '}'
        if args.text_only:
            continue
        if args.rules_only:
            portrait_name = existing_cards[cid]['portrait']
        else:
            art = bundle['assets'][card['art']]
            mime, data = art.split(',', 1)
            image_bytes = base64.b64decode(data)
            # Some exported data URLs carry a stale MIME label; use the actual file signature.
            extension = '.png' if image_bytes.startswith(b'\x89PNG\r\n\x1a\n') else '.jpg'
            portrait_name = cid + extension
            (ROOT / 'ShoujoKagekiJunna/images/cards' / portrait_name).write_bytes(image_bytes)
        damage = (bool(re.search(r'造成(?:\d+|X)点伤害', card['text'])) or cid == 'JH30') and cid != 'JA82'
        target = 'AllEnemies' if '所有敌人造成' in card['text'] else 'AnyEnemy' if (
            (damage and card['type'] != '能力') or cid in ('JF10', 'JA58')) else 'Self'
        entry = {
            'id': cid, 'modelId': model_id, 'className': cls, 'name': card['name'],
            'cost': 0 if card['cost'] == 'X' else card['cost'],
            'upgradedCost': 0 if card['upgradedCost'] == 'X' else card['upgradedCost'],
            'costsX': card['cost'] == 'X', 'type': card['type'], 'target': target,
            'arrowSubtype': card.get('arrowSubtype', '攻击' if damage else '技能'),
            'rarity': card['rarity'], 'pool': bool(card.get('pool')),
            'knowledge': '知识' in card.get('keywords', []) or '知识。' in card['text'],
            'exhaust': '消耗。' in card['text'], 'upgradedExhaust': '消耗。' in card['upgradedText'],
            'retain': '保留。' in card['text'], 'upgradedRetain': '保留。' in card['upgradedText'],
            'damageCapable': damage, 'portrait': portrait_name, 'values': values,
            'text': card['text'], 'upgradedText': card['upgradedText'],
        }
        if 'arrowFocus' in card:
            entry['arrowFocus'] = card['arrowFocus']
        elif args.rules_only and 'arrowFocus' in existing_cards[cid]:
            entry['arrowFocus'] = existing_cards[cid]['arrowFocus']
        definitions.append(entry)
        if cid not in EXISTING:
            wrappers.append(f'[CustomID("{model_id}")]\npublic sealed class {cls}() : CatalogCard("{cid}");\n')
    ids = {c['id'] for c in cards}
    groups = [g for g in bundle['data']['partnerGroups'] if g['mainCardId'] in ids]
    out = {'revision': bundle['revision'], 'cards': definitions, 'groups': groups}
    if args.rules_only:
        out['arrowArtRevision'] = existing.get('arrowArtRevision', existing['revision'])
    else:
        out['arrowArtRevision'] = bundle['revision']
    if not args.text_only:
        (ROOT / 'docs/implementation/card-pool.json').write_text(json.dumps(out, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    header = '// Generated by tools/assets/generate-card-catalog.py from an MCP export.\nusing BaseLib.Utils.Attributes;\n\nnamespace ShoujoKagekiJunna.Models;\n\n'
    if not args.text_only and not args.rules_only:
        (ROOT / 'src/Models/CardCatalog.Generated.cs').write_text(header + '\n'.join(wrappers), encoding='utf-8')
    for lang in ('eng', 'zhs'):
        (ROOT / f'ShoujoKagekiJunna/localization/{lang}/cards.json').write_text(json.dumps(localization, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        powers_path = ROOT / f'ShoujoKagekiJunna/localization/{lang}/powers.json'
        powers = json.loads(powers_path.read_text(encoding='utf-8'))
        for cid in 'JA04 JK10 JA22 JK15 A JK27 JA35 JK32 JK37C JA45 JK64 JK67 JG31 JH19 JH21 JG34 JK75 JH23 JA70 JH24 JK80 JK81 JZ05 JZ06 JH30 JK46'.split():
            powers[f'JUNNA_{cid}_POWER.title'] = next(c['name'] for c in cards if c['id'] == cid)
        powers.update({'JUNNA_CATALOG.description': '{Effects}', 'JUNNA_ENERGY_RESERVE.title': '能量保留',
                       'JUNNA_ENERGY_RESERVE.description': '下回合保留至多{Amount}点剩余能量。'})
        powers_path.write_text(json.dumps(powers, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    if args.text_only:
        print(f'Updated display text for {len(cards)} cards; runtime values, models and portraits unchanged.')
    elif args.rules_only:
        print(f'Updated {len(cards)} runtime rules and descriptions at revision {bundle["revision"]}; portraits and models preserved.')
    else:
        print(f'Generated {len(cards)} card definitions, {len(wrappers)} new models and {len(groups)} knowledge groups at revision {bundle["revision"]}.')


if __name__ == '__main__':
    main()
