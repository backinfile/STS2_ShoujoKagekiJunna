"""Generate runtime metadata and portraits from a versioned workbench MCP export.

The export is read-only. Effects are implemented explicitly in CardEffects.cs.
"""
import argparse
import base64
from difflib import SequenceMatcher
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

SELECTION_PROMPTS = {
    "JG03": "选择1张手牌。",
    "JG09": "选择1张手牌。",
    "JG16": "选择1张手牌。",
    "JT02": "选择1张非能力手牌。",
    "JA84": "选择1张手牌。",
    "JA87": "选择至多1张手牌。",
    "JA88": "选择任意张手牌。",
    "JK84": "选择{Cards}张手牌。",
    "JG37": "选择1张手牌。",
    "JH01": "选择2张手牌。",
    "JK11": "选择1张驳论。",
    "JK11B": "选择1张立论。",
    "JK50B": "选择1张雄辩。",
    "JK53": "选择1张知识牌。",
    "JG27": "选择1张牌。",
    "JA65": "选择1张箭矢。",
    "JA66": "选择至多{Cards}张牌。",
    "JK73B": "选择1张反证。",
    "JK78": "选择至多{Cards}张牌。",
    "JK78D": "选择至多{Cards}张牌。",
    "JK78E": "选择至多{Cards}张牌。",
    "JK37C": "选择1张箭矢。",
    "JK64": "选择1张箭矢。"
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
    highlighted = {'格挡', '易伤', '虚弱', '知识', '箭矢', '伤害加成', '保留', '升级', '手牌', '抽牌堆', '弃牌堆', '消耗牌堆'}
    # Only actual named references need emphasis; all replacements run once to avoid nested tags.
    highlighted.update(card_names)
    highlighted.update(name + '+' for name in card_names)
    pattern = '|'.join(re.escape(word) for word in sorted(highlighted, key=len, reverse=True))
    text = re.sub(pattern, lambda m: '[gold]' + m[0] + '[/gold]', text)
    return text


def upgrade_description(normal, improved):
    """Keep shared wording outside upgrade-only spans, preserving formatter/tag boundaries."""
    token = re.compile(r'\{(?:[^{}]|\{[^{}]*\})*\}|\[gold\].*?\[/gold\]|\[[^\]]+\]|.', re.S)
    before, after = token.findall(normal), token.findall(improved)
    parts = []
    for operation, a, b, c, d in SequenceMatcher(a=before, b=after, autojunk=False).get_opcodes():
        old, new = ''.join(before[a:b]), ''.join(after[c:d])
        parts.append(old if operation == 'equal' else '{IfUpgraded:show:' + new + '|' + old + '}')
    return ''.join(parts)


def bind_numbers(text, prior=None):
    values = []
    used = set()

    def replace(match):
        index = len(values)
        tail = text[match.end():]
        if prior and index < len(prior):
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
              if c['type'] == '箭矢' and not c.get('trashedAt') and not c.get('reservedAt')}
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
        # Upgrades can replace a fixed count with "all" (e.g. 论据).
        if len(upgraded_values) > len(values):
            raise ValueError('Upgrade introduces unmatched numbers: ' + cid)
        for index, value in enumerate(values):
            value['upgraded'] = upgraded_values[index]['base'] if index < len(upgraded_values) else value['base']
        if cid == 'JA81B':
            values.append({'name': 'Damage', 'base': 0, 'upgraded': 0})
            base = base.replace('X', '{Damage:diff()}')
            upgrade = upgrade.replace('X', '{Damage:diff()}')
        if card['type'] == '箭矢':
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
        if cid in SELECTION_PROMPTS:
            localization[model_id + '.selectionScreenPrompt'] = SELECTION_PROMPTS[cid]
        normal = display_description(card, base, card_names, values)
        improved = display_description(card, upgrade, card_names, values, upgraded=True)
        localization[model_id + '.description'] = upgrade_description(normal, improved)
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
        target = {'自己': 'Self', '敌人': 'AnyEnemy', '所有敌人': 'AllEnemies',
                  '无': 'None', '无需目标': 'None', '所有玩家': 'AllAllies',
                  '玩家': 'AnyPlayer', '队友': 'AnyAlly'}.get(card.get('target'))
        if target is None:
            raise ValueError('Unknown target: ' + cid + '/' + str(card.get('target')))
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
    groups = [g for g in bundle['data']['partnerGroups'] if g['mainCardId'] in ids and not g.get('trashedAt') and not g.get('reservedAt')]
    for group in groups:
        if any(cid not in ids for cid in group['grantedCardIds']):
            raise ValueError('Inactive knowledge group member: ' + group['id'])
    out = {'revision': bundle['revision'], 'cards': definitions, 'groups': groups,
           'startingDeck': bundle['data']['meta']['startingDeck']}
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
        keywords_path = ROOT / f'ShoujoKagekiJunna/localization/{lang}/card_keywords.json'
        keywords = json.loads(keywords_path.read_text(encoding='utf-8'))
        knowledge = next(item for item in bundle['data']['mechanisms'] if item['id'] == 'ME_PARTNER')
        if lang == 'zhs':
            keywords['SHOUJOKAGEKIJUNNA-KNOWLEDGE.title'] = knowledge['name']
            keywords['SHOUJOKAGEKIJUNNA-KNOWLEDGE.description'] = knowledge['definition']
        keywords_path.write_text(json.dumps(keywords, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        powers_path = ROOT / f'ShoujoKagekiJunna/localization/{lang}/powers.json'
        powers = json.loads(powers_path.read_text(encoding='utf-8'))
        power_ids = {c['id'] for c in cards if c['type'] == '能力' and c['id'] != 'JG23'} | ({'JH19', 'JK80', 'JG31'} & ids)
        for cid in sorted(power_ids):
            powers[f'JUNNA_{cid}_POWER.title'] = next(c['name'] for c in cards if c['id'] == cid)
        for power in bundle['data'].get('powers', []):
            if power.get('trashedAt'):
                continue
            for suffix, text in power.get('localization', {}).items():
                powers[power['id'] + '.' + suffix] = text
        powers.setdefault('JUNNA_CATALOG.smartDescription', '{Effects}')
        powers_path.write_text(json.dumps(powers, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        ui_path = ROOT / f'ShoujoKagekiJunna/localization/{lang}/gameplay_ui.json'
        ui = json.loads(ui_path.read_text(encoding='utf-8'))
        ui = {k: v for k, v in ui.items() if not k.startswith('JUNNA.KNOWLEDGE_BACKGROUND.')}
        for card in cards:
            tips = [t for t in card.get('extraHoverTips', []) if t.get('id') == 'junna_knowledge_background']
            if tips:
                ui['JUNNA.KNOWLEDGE_BACKGROUND.' + card['id']] = '\n'.join(t['description'] if 'description' in t else t.get('body', '') for t in tips)
        ui_path.write_text(json.dumps(ui, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    if not args.text_only and not args.rules_only:
        (ROOT / 'src/Models/CatalogPowers.Generated.cs').write_text(header + '\n'.join(
            f'[CustomID("JUNNA_{cid}_POWER")]\npublic sealed class JunnaPower{cid}() : CatalogPower("{cid}");\n'
            for cid in sorted(power_ids)), encoding='utf-8')
    if args.text_only:
        print(f'Updated display text for {len(cards)} cards; runtime values, models and portraits unchanged.')
    elif args.rules_only:
        print(f'Updated {len(cards)} runtime rules and descriptions at revision {bundle["revision"]}; portraits and models preserved.')
    else:
        print(f'Generated {len(cards)} card definitions, {len(wrappers)} new models and {len(groups)} knowledge groups at revision {bundle["revision"]}.')


if __name__ == '__main__':
    main()
