"""Generate runtime metadata and portraits from a versioned workbench MCP export.

The export is read-only. Effects are implemented in the concrete card classes.
"""
import argparse
import runpy
import base64
from difflib import SequenceMatcher
import json
import os
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CORE_ROOT = Path(os.environ.get('SHOUJO_CORE_ROOT', ROOT.parent / 'STS2_ShoujoKagekiCore'))
CATALOG_PATH = ROOT / 'data/card-pool.json'
EXISTING = {
    'JB01': ('JunnaStrike', 'JUNNA_STRIKE'), 'JB02': ('JunnaDefend', 'JUNNA_DEFEND'),
    'JB03': ('JunnaFullDraw', 'JUNNA_FULL_DRAW'),
}

# Export formatting for values the workbench text does not bind to a native variable.
CARD_EXPORT_RULES = {
    'JK82': {'dynamicEnergy': True},
    'JK206': {'fixedValues': [('Damage', 0, 8)], 'damageCapable': True,
              'descriptionSuffix': '{InCombat:\n（造成{CalculatedDamage:diff()}点伤害）|}'},
    'JK37': {'unprintedDamage': (50, 60), 'damageCapable': True},
    'JA81B': {'generatedDamage': True},
    'JA82': {'damageCapable': False},
    'JK400B': {'damageCapable': False},
    'JH30': {'damageCapable': True},
    'JA108': {'descriptionSuffix': '{InCombat:\n（共{ArrowCount}张）|}'},
    'U_CARD_030': {'damageCapable': False, 'descriptionSuffix': '{InCombat:\n（共{MeteorCount}张）|}'},
    'U_CARD_017': {'descriptionDamageVar': 'CalculatedDamage'},
    'U_CARD_184': {'energyDiscount': True},
    'U_CARD_186': {'damageCapable': False},
    'U_CARD_187': {'damageCapable': False},
    'U_CARD_188': {'powerAmountField': 'N1'},
    'JK314': {'descriptionSuffix': '{InCombat:\n（共{CalculatedHits:diff()}段）|}'},
}

SELECTION_PROMPTS = {
    "U_CARD_010": "选择{Cards}张{Category}牌放入手牌。",
    "U_CARD_178": "选择{N1}张弃牌堆中的牌。",
    "JK206B": "选择{Cards}张知识牌。",
    "JA84": "选择{Cards}张手牌，作为{Turns}回合的飞矢打出。",
    "JA87": "选择至多{Cards}张手牌，作为{Turns}回合的飞矢打出。",
    "JA88": "选择任意数量的手牌，作为{Turns}回合的飞矢打出。",
    "JK84": "选择至多{Cards}张手牌，作为{Turns}回合的飞矢打出。",
    "JG37": "选择{Cards}张手牌，作为{Turns}回合的飞矢打出。",
    "JK11": "选择1张驳论。",
    "JK11B": "选择1张立论。",
    "JK53": "选择1张知识牌。",
    "JA65": "选择1张飞矢。",
    "JK73B": "选择1张反证。",
    "JK126": "选择2张关联牌。",
    "JK127": "选择至多1张牌。",
}

def native_keyword(text, keyword):
    return bool(re.search(r'(?:^|[。\n])\s*' + re.escape(keyword) + r'。', text))


def display_description(card, text, card_names, values, upgraded=False):
    """Format the workbench wording without substituting independently curated rules."""
    rule = CARD_EXPORT_RULES.get(card['id'], {})
    numbers = {value['name']: value['upgraded' if upgraded else 'base'] for value in values}

    def energy(match):
        # Learning can upgrade repeatedly, so its displayed gain must stay dynamic.
        if rule.get('dynamicEnergy'):
            return match[0].removesuffix('点能量') + '{energyPrefix:energyIcons(1)}'
        value = numbers[match[1]]
        icons = '{energyPrefix:energyIcons(' + str(value if 1 <= value <= 3 else 1) + ')}'
        return icons if 1 <= value <= 3 else match[0].removesuffix('点能量') + icons

    text = re.sub(r'((?:可)?保留)(\{\w+:diff\(\)\}|\d+)点能量',
                  lambda m: m[1] + m[2] + '{energyPrefix:energyIcons(1)}', text)
    text = re.sub(r'(\{\w+:diff\(\)\}|\d+)点能量(?=(?:并|且)(?:可)?保留(?:到|至)下(?:个)?回合)',
                  lambda m: m[1] + '{energyPrefix:energyIcons(1)}', text)
    if rule.get('energyDiscount'):
        text = re.sub(r'(减少\{\w+:diff\(\)\})点(?:能量)?', r'\1{energyPrefix:energyIcons(1)}', text)
    text = re.sub(r'\{(\w+):diff\(\)\}点能量', energy, text)
    text = re.sub(r'(\d+)点能量', lambda m: '{energyPrefix:energyIcons(' + m[1] + ')}', text)
    # Arrow countdowns are rendered on their existing plaque.
    text = re.sub(r'^\[blue\][^\n]*\n', '', text)
    text = re.sub(r'。\s*', '。\n', text).strip()
    highlighted = {'格挡', '易伤', '虚弱', '伶俐', '知识', '连锁知识', '飞矢', '箭矢', '闪耀', '伤害加成', '保留', '升级', '手牌', '抽牌堆', '弃牌堆', '消耗牌堆'}
    # Only actual named references need emphasis; all replacements run once to avoid nested tags.
    highlighted.update(card_names)
    highlighted.update(name + '+' for name in card_names)
    pattern = '|'.join(re.escape(word) for word in sorted(highlighted, key=len, reverse=True))
    # Match the Exhaust keyword and Strength stat in context.
    pattern += r'|(?<=获得)消耗|消耗(?=复制)|(?<=点)力量'
    text = re.sub(pattern, lambda m: '[gold]' + m[0] + '[/gold]', text)
    if damage_var := rule.get('descriptionDamageVar'):
        text = text.replace('{Damage:diff()}', '{' + damage_var + ':diff()}', 1)
    return text + rule.get('descriptionSuffix', '')


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


def portrait_extension(mime, image_bytes):
    # Workbench data URLs can carry stale MIME labels.
    if image_bytes.startswith(b'\x89PNG\r\n\x1a\n'):
        return '.png'
    if image_bytes.startswith(b'\xff\xd8\xff'):
        return '.jpg'
    if image_bytes[:4] == b'RIFF' and image_bytes[8:12] == b'WEBP':
        return '.webp'
    if mime == 'data:image/svg+xml;base64':
        return '.svg'
    raise ValueError('Unsupported workbench portrait format: ' + mime)


def arrow_portraits_only(bundle):
    """Sync arrow art and focal points while preserving all runtime game rules."""
    catalog = json.loads(CATALOG_PATH.read_text(encoding='utf-8'))
    source = {c['id']: c for c in bundle['data']['cards']
              if c['type'] == '飞矢' and not c.get('trashedAt') and not c.get('reservedAt')}
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
        mime, data = bundle['assets'][card['art']].split(',', 1)
        image_bytes = base64.b64decode(data)
        extension = portrait_extension(mime, image_bytes)
        entry['portrait'] = card['id'] + extension
        portrait = ROOT / 'ShoujoKagekiJunna/images/cards' / entry['portrait']
        if not portrait.exists() or portrait.read_bytes() != image_bytes:
            portrait.write_bytes(image_bytes)
        count += 1
    missing = source.keys() - {c['id'] for c in catalog['cards']}
    if missing:
        raise ValueError('Arrow cards missing from runtime catalog: ' + ', '.join(sorted(missing)))
    catalog['arrowArtRevision'] = bundle['revision']
    CATALOG_PATH.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
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
    removed_models = json.loads((ROOT / 'data/removed-card-models.json').read_text(encoding='utf-8'))
    cards = [c for c in bundle['data']['cards']
             if c['id'] not in removed_models and not c.get('trashedAt') and not c.get('reservedAt')]
    previous = json.loads(CATALOG_PATH.read_text(encoding='utf-8'))
    existing = previous if args.rules_only else None
    existing_cards = {card['id']: card for card in existing['cards']} if existing else {}
    active_ids = {card['id'] for card in cards}
    legacy = {card['id']: card for card in previous['legacyCards'] if card['id'] not in active_ids}
    legacy.update((card['id'], {**card, 'pool': False}) for card in previous['cards']
                  if card['id'] not in active_ids and card['id'] not in removed_models)
    definitions = []
    wrappers = []
    localization = {}
    power_amounts = {}
    card_names = {c['name'] for c in cards}
    for card in cards:
        cid = card['id']
        cls, model_id = EXISTING.get(cid, ('JunnaCard' + cid, 'JUNNA_' + cid))
        base = card['text'].strip()
        upgrade = card['upgradedText'].strip()
        # Native keywords are rendered by CardModel; knowledge keeps its printed position.
        for keyword in ('保留', '消耗', '固有', '虚无'):
            base = re.sub(r'(^|[。\n])\s*' + re.escape(keyword) + r'。', r'\1', base)
            upgrade = re.sub(r'(^|[。\n])\s*' + re.escape(keyword) + r'。', r'\1', upgrade)
        # Core renders the current Shine budget, including depletion and upgrades.
        base = re.sub(r'闪耀\d+[。.]', '', base)
        upgrade = re.sub(r'闪耀\d+[。.]', '', upgrade)
        rule = CARD_EXPORT_RULES.get(cid, {})
        fixed = rule.get('fixedValues')
        if 'unprintedDamage' in rule and '造成' not in base and '造成' not in upgrade:
            fixed = [('Damage', *rule['unprintedDamage'])]
        if fixed is not None:
            values = [{'name': name, 'base': normal, 'upgraded': improved}
                      for name, normal, improved in fixed]
        else:
            base, values = bind_numbers(base)
            upgrade, upgraded_values = bind_numbers(upgrade, values)
            # Upgrades can replace a fixed count with "all" (e.g. 论据).
            if len(upgraded_values) > len(values):
                raise ValueError('Upgrade introduces unmatched numbers: ' + cid)
            for index, value in enumerate(values):
                value['upgraded'] = upgraded_values[index]['base'] if index < len(upgraded_values) else value['base']
        if rule.get('generatedDamage'):
            values.append({'name': 'Damage', 'base': 0, 'upgraded': 0})
            base = base.replace('X', '{Damage:diff()}')
            upgrade = upgrade.replace('X', '{Damage:diff()}')
        if card['type'] == '飞矢':
            values.append({'name': 'Turns', 'base': card['turns'], 'upgraded': card['upgradedTurns']})
        # Existing implementations use their established variable names.
        if rule.get('literalNumbers'):
            for value in values:
                base = base.replace('{' + value['name'] + ':diff()}', str(value['base']))
                upgrade = upgrade.replace('{' + value['name'] + ':diff()}', str(value['upgraded']))
        localization[model_id + '.title'] = card['name']
        if cid in SELECTION_PROMPTS:
            localization[model_id + '.selectionScreenPrompt'] = SELECTION_PROMPTS[cid]
        normal = display_description(card, base, card_names, values)
        improved = display_description(card, upgrade, card_names, values, upgraded=True)
        localization[model_id + '.description'] = upgrade_description(normal, improved)
        power_amounts[cid] = next((value['base'] for value in values if 'powerAmountField' not in rule or value['name'] == rule['powerAmountField']), 0)
        if args.text_only:
            continue
        if args.rules_only:
            portrait_name = existing_cards[cid]['portrait']
        else:
            art = bundle['assets'].get(card['art'])
            if card['art'] == 'defaultPortrait' and not art:
                assets_path = CORE_ROOT / 'tools/workbench/assets.json'
                art = json.loads(assets_path.read_text(encoding='utf-8'))['defaultPortrait']
            if not art:
                raise ValueError(f"Missing workbench portrait: {cid} ({card['art']})")
            mime, data = art.split(',', 1)
            image_bytes = base64.b64decode(data)
            portrait_name = cid + portrait_extension(mime, image_bytes)
            (ROOT / 'ShoujoKagekiJunna/images/cards' / portrait_name).write_bytes(image_bytes)
        damage = rule.get('damageCapable', bool(re.search(r'造成(?:\d+|X)点伤害', card['text'])))
        target = {'自己': 'Self', '敌人': 'AnyEnemy', '任意敌人': 'AnyEnemy', '所有敌人': 'AllEnemies',
                  '无': 'None', '无需目标': 'None', '所有玩家': 'AllAllies',
                  '玩家': 'AnyPlayer', '队友': 'AnyAlly', '敌人或自己': 'AnyEnemy'}.get(card.get('target'))
        if target is None:
            raise ValueError('Unknown target: ' + cid + '/' + str(card.get('target')))
        pool = bool(card.get('pool'))
        if cid in ('JK84', 'JK84B') and 'cardPool' in card:
            pool = card['cardPool'] == '角色'
        entry = {
            'id': cid, 'modelId': model_id, 'className': cls, 'name': card['name'],
            'cost': 0 if card['cost'] == 'X' else card['cost'],
            'upgradedCost': 0 if card['upgradedCost'] == 'X' else card['upgradedCost'],
            'costsX': card['cost'] == 'X', 'type': card['type'], 'target': target,
            'arrowSubtype': card.get('arrowSubtype', '攻击' if damage else '技能'),
            'rarity': card['rarity'], 'pool': pool,
            'knowledge': any(native_keyword(card['text'], keyword) for keyword in ('知识', '连锁知识')),
            'exhaust': native_keyword(card['text'], '消耗'), 'upgradedExhaust': native_keyword(card['upgradedText'], '消耗'),
            'retain': native_keyword(card['text'], '保留'), 'upgradedRetain': native_keyword(card['upgradedText'], '保留'),
            'innate': native_keyword(card['text'], '固有'), 'upgradedInnate': native_keyword(card['upgradedText'], '固有'),
            'damageCapable': damage, 'portrait': portrait_name, 'values': values,
            'text': card['text'], 'upgradedText': card['upgradedText'],
        }
        if native_keyword(card['text'], '虚无') or native_keyword(card['upgradedText'], '虚无'):
            entry['ethereal'] = native_keyword(card['text'], '虚无')
            entry['upgradedEthereal'] = native_keyword(card['upgradedText'], '虚无')
        if card.get('choiceOnly'):
            entry['choiceOnly'] = True
        if 'arrowFocus' in card:
            entry['arrowFocus'] = card['arrowFocus']
        elif args.rules_only and 'arrowFocus' in existing_cards[cid]:
            entry['arrowFocus'] = existing_cards[cid]['arrowFocus']
        definitions.append(entry)
        if cid not in EXISTING:
            shine = re.search(r'闪耀(\d+)[。.]', card['text'])
            declaration = f'[ShoujoKageki.Core.V1.ShineCard({int(shine[1])})]\n' if shine else ''
            wrappers.append(declaration + f'[CustomID("{model_id}")]\npublic sealed partial class {cls}() : CatalogCard("{cid}");\n')
    for card in legacy.values():
        if card['id'] in EXISTING:
            continue
        shine = re.search(r'闪耀(\d+)[。.]', card.get('text', ''))
        declaration = f'[ShoujoKageki.Core.V1.ShineCard({int(shine[1])})]\n' if shine else ''
        wrappers.append(declaration + f'[CustomID("{card["modelId"]}")]\npublic sealed partial class {card["className"]}() : CatalogCard("{card["id"]}");\n')
    ids = {c['id'] for c in cards}
    source_ids = {card['id'] for card in bundle['data']['cards']}
    groups = [g for g in bundle['data']['partnerGroups'] if g['mainCardId'] in ids and not g.get('trashedAt') and not g.get('reservedAt')]
    for group in groups:
        if not source_ids.issuperset(group['grantedCardIds']):
            raise ValueError('Unknown knowledge group member: ' + group['id'])
    # Complete groups must not grant archived or reserved cards.
    groups = [group for group in groups if ids.issuperset(group['grantedCardIds'])]
    out = {'revision': bundle['revision'], 'cards': definitions, 'groups': groups,
           'startingDeck': bundle['data']['meta']['startingDeck'], 'legacyCards': list(legacy.values()),
           'startingHp': bundle['data']['meta'].get('startingHp', 70),
           'startingGold': bundle['data']['meta'].get('startingGold', 99),
           'startingRelics': bundle['data']['meta'].get('startingRelics', ['JUNNA_GREEN_GEM'])}
    if args.rules_only:
        out['arrowArtRevision'] = existing.get('arrowArtRevision', existing['revision'])
    else:
        out['arrowArtRevision'] = bundle['revision']
    if not args.text_only:
        CATALOG_PATH.write_text(json.dumps(out, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    header = '// Generated by tools/assets/generate-card-catalog.py from an MCP export.\nusing BaseLib.Utils.Attributes;\n\nnamespace ShoujoKagekiJunna.Models;\n\n'
    if not args.text_only and not args.rules_only:
        (ROOT / 'src/Models/CardCatalog.Generated.cs').write_text(header + '\n'.join(wrappers), encoding='utf-8')
    localization.update({
        'JUNNA_ARROW_TARGET_SELF.title': '自己',
        'JUNNA_ARROW_TARGET_SELF.description': '目标为自己的[gold]飞矢[/gold]加速1回合。',
        'JUNNA_ARROW_TARGET_ENEMY.title': '敌人',
        'JUNNA_ARROW_TARGET_ENEMY.description': '目标为敌人的[gold]飞矢[/gold]加速1回合。',
        'JUNNA_PREPARE_ARROWS.title': '飞矢',
        'JUNNA_PREPARE_KNOWLEDGE.title': '知识',
        'JUNNA_PREPARE_SHINE.title': '闪耀',
        'JUNNA_NOTES_DRAW.title': '抽牌',
        'JUNNA_NOTES_DRAW.description': '抽{Amount:diff()}张牌。',
        'JUNNA_NOTES_ENERGY.title': '能量',
        'JUNNA_NOTES_ENERGY.description': '获得{Amount:diff()}{energyPrefix:energyIcons(1)}。',
    })
    for lang in ('eng', 'zhs'):
        prior_localization = json.loads((ROOT / f'ShoujoKagekiJunna/localization/{lang}/cards.json').read_text(encoding='utf-8'))
        for card in legacy.values():
            localization.update({key: value for key, value in prior_localization.items() if key.startswith(card['modelId'] + '.')})
        (ROOT / f'ShoujoKagekiJunna/localization/{lang}/cards.json').write_text(json.dumps(localization, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        keywords_path = ROOT / f'ShoujoKagekiJunna/localization/{lang}/card_keywords.json'
        keywords = json.loads(keywords_path.read_text(encoding='utf-8'))
        knowledge = next(item for item in bundle['data']['mechanisms'] if item['id'] == 'ME_PARTNER')
        if lang == 'zhs':
            keywords['SHOUJOKAGEKIJUNNA-KNOWLEDGE.title'] = knowledge['name']
            keywords['SHOUJOKAGEKIJUNNA-KNOWLEDGE.description'] = knowledge['definition']
            terms = {item['id']: item for item in bundle['data'].get('terms', [])}
            if arrow := terms.get('KW_STAGE'):
                keywords['SHOUJOKAGEKIJUNNA-ARROW.title'] = arrow['name']
                keywords['SHOUJOKAGEKIJUNNA-ARROW.description'] = arrow['definition']
            chain = terms.get('KW_CHAIN_KNOWLEDGE')
            if chain:
                keywords['SHOUJOKAGEKIJUNNA-CHAIN_KNOWLEDGE.title'] = chain['name']
                keywords['SHOUJOKAGEKIJUNNA-CHAIN_KNOWLEDGE.description'] = chain['definition']
        keywords_path.write_text(json.dumps(keywords, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        powers_path = ROOT / f'ShoujoKagekiJunna/localization/{lang}/powers.json'
        powers = json.loads(powers_path.read_text(encoding='utf-8'))
        power_ids = {c['id'] for c in cards if c['type'] == '能力' and c['id'] != 'U_CARD_047'} | ({'JH19', 'JG31'} & ids)
        power_ids.update(card['id'] for card in legacy.values() if card['type'] == '能力')
        for cid in sorted(power_ids):
            powers[f'JUNNA_{cid}_POWER.title'] = next(c['name'] for c in cards + list(legacy.values()) if c['id'] == cid)
        legacy_power_ids = {f'JUNNA_{card["id"]}_POWER' for card in legacy.values() if card['type'] == '能力'}
        removed_power_ids = {f'JUNNA_{cid}_POWER' for cid in removed_models}
        powers = {key: value for key, value in powers.items() if key.split('.')[0] not in removed_power_ids}
        for power in bundle['data']['meta'].get('powers', []):
            if power.get('trashedAt') or power['id'] in legacy_power_ids or power['id'] in removed_power_ids:
                continue
            for suffix, text in power.get('localization', {}).items():
                powers[power['id'] + '.' + suffix] = text
        # Active card text takes precedence over archived power descriptions.
        friend_title = localization.get('JUNNA_U_CARD_008B.title', '朋友！')
        friend_sentence_end = '' if friend_title.endswith(('。', '！', '？', '.', '!', '?')) else '。'
        current_power_text = {
            'JK400B': '回合结束时，打出[blue]{Repeat}[/blue]张造成[blue]{Damage}[/blue]点伤害的[gold]流星[/gold]。',
            'JA105': '每当你打出一张[gold]飞矢[/gold]，使其加速[blue]{Amount}[/blue]回合，并使其获得[gold]消耗[/gold]。',
            'JA118': '打出[gold]飞矢[/gold]时，获得[blue]{Amount}[/blue]点[gold]格挡[/gold]。',
            'JA205': '[gold]飞矢[/gold]造成[blue]{Amount}[/blue]%额外伤害。',
            'U_CARD_188': '打出[gold]流星[/gold]时使其加速[blue]{Turns}[/blue]回合，并且[gold]流星[/gold]会造成[blue]{Amount}[/blue]%额外伤害。',
            'JK235': '打出[gold]知识[/gold]牌后，对随机敌人造成[blue]{Damage}[/blue]点伤害。',
            'JK260': '每回合第一次打出[gold]知识[/gold]牌后，获得[blue]{Amount}[/blue]点[gold]力量[/gold]。',
            'JK37C': '打出耗能为[blue]0[/blue]的牌后，在当回合获得[blue]{Amount}[/blue]点[gold]力量[/gold]和[gold]敏捷[/gold]。',
            'U_CARD_008': f'战斗结束时，向[gold]牌组[/gold]中添加[blue]{{Amount}}[/blue]张[gold]{friend_title}[/gold]{friend_sentence_end}',
            'U_CARD_016': '打出[gold]{Category}[/gold]牌后，抽[blue]{Amount}[/blue]张牌。',
            'U_CARD_010': '回合开始时，从[gold]抽牌堆[/gold]选择[blue]1[/blue]张当前类别的牌放入[gold]手牌[/gold]。\n每回合依次切换为[gold]知识[/gold]牌、[gold]飞矢[/gold]牌、[gold]闪耀[/gold]牌。',
        }
        for cid, text in current_power_text.items():
            if cid not in ids:
                continue
            amount = 1 if cid in ('U_CARD_008', 'U_CARD_010', 'JK37C') else power_amounts[cid]
            powers[f'JUNNA_{cid}_POWER.description'] = text.replace('{Amount}', str(amount)).replace('{Repeat}', '1').replace('{Damage}', str(amount)).replace('{Turns}', '1')
            powers[f'JUNNA_{cid}_POWER.smartDescription'] = text
            if cid == 'U_CARD_016':
                powers[f'JUNNA_{cid}_POWER.description'] = '打出[gold]知识[/gold]牌后，抽[blue]1[/blue]张牌。\n每次在[gold]知识[/gold]、[gold]飞矢[/gold]、[gold]闪耀[/gold]中轮换。'
            if cid == 'U_CARD_010':
                powers[f'JUNNA_{cid}_POWER.smartDescription'] = '下回合开始时，从[gold]抽牌堆[/gold]选择[blue]{Amount}[/blue]张[gold]{Category}[/gold]牌放入[gold]手牌[/gold]。\n每回合依次切换为[gold]知识[/gold]牌、[gold]飞矢[/gold]牌、[gold]闪耀[/gold]牌。'
        powers.setdefault('JUNNA_CATALOG.smartDescription', '{Effects}')
        powers['JUNNA_CATALOG.remainingTriggers'] = '本回合还可触发[blue]{Amount}[/blue]次。'
        powers_path.write_text(json.dumps(powers, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        relics_path = ROOT / f'ShoujoKagekiJunna/localization/{lang}/relics.json'
        relics = json.loads(relics_path.read_text(encoding='utf-8'))
        for relic in bundle['data']['meta'].get('relics', []):
            if relic.get('trashedAt') or not relic.get('text', '').strip():
                continue
            relics[relic['id'] + '.title'] = relic['name']
            relics[relic['id'] + '.description'] = relic['text']
        relics_path.write_text(json.dumps(relics, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        ui_path = ROOT / f'ShoujoKagekiJunna/localization/{lang}/gameplay_ui.json'
        ui = json.loads(ui_path.read_text(encoding='utf-8'))
        ui = {k: v for k, v in ui.items() if not k.startswith('JUNNA.KNOWLEDGE_BACKGROUND.')}
        for card in cards:
            if not any(native_keyword(card[key], keyword)
                       for key in ('text', 'upgradedText') for keyword in ('知识', '连锁知识')):
                continue
            tips = [t for t in card.get('extraHoverTips', []) if t.get('id') == 'junna_knowledge_background']
            if tips:
                ui['JUNNA.KNOWLEDGE_BACKGROUND.' + card['id']] = '\n'.join(t['description'] if 'description' in t else t.get('body', '') for t in tips)
        ui_path.write_text(json.dumps(ui, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    if not args.text_only and not args.rules_only:
        (ROOT / 'src/Models/CatalogPowers.Generated.cs').write_text(header + '\n'.join(
            f'[CustomID("JUNNA_{cid}_POWER")]\npublic sealed partial class JunnaPower{cid}() : CatalogPower("{cid}");\n'
            for cid in sorted(power_ids - {"JK80"})), encoding='utf-8')
    if args.text_only:
        print(f'Updated display text for {len(cards)} cards; runtime values, models and portraits unchanged.')
    elif args.rules_only:
        print(f'Updated {len(cards)} runtime rules and descriptions at revision {bundle["revision"]}; portraits and models preserved.')
    else:
        print(f'Generated {len(cards)} card definitions, {len(wrappers)} new models and {len(groups)} knowledge groups at revision {bundle["revision"]}.')


if __name__ == '__main__':
    main()
    runpy.run_path(str(ROOT / 'tools/assets/generate-card-rules.py'))['generate'](ROOT)
