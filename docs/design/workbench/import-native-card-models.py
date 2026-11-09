"""Export native card references from decompiled code and Chinese localization.

Usage: python import-native-card-models.py <spire-codex> <generate_character_card_doc.py> <output.json>
The shared research parser resolves SmartFormat and upgrade variables; this
export adds card pools, keyword changes, hit counts, and source fingerprints.
"""
from __future__ import annotations

import hashlib
import importlib.util
import json
import re
import sys
from pathlib import Path

sys.dont_write_bytecode = True


def export(codex: Path, parser_path: Path) -> dict:
    spec = importlib.util.spec_from_file_location("native_card_parser", parser_path)
    parser = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = parser
    spec.loader.exec_module(parser)
    parser.RAW_ROOT = codex / "extraction/raw"
    parser.SRC_ROOT = parser.RAW_ROOT / "src/Core"
    parser.CARDS_DIR = parser.SRC_ROOT / "Models/Cards"
    parser.LOC_DIR = parser.RAW_ROOT / "localization/zhs"
    localization = parser.load_json(parser.LOC_DIR / "cards.json")
    # energyPrefix is the single energy icon following a printed amount, or
    # a standalone one-energy icon. The research parser treats it as an option.
    for key, value in localization.items():
        value = value.replace("不能再获得{energyPrefix:energyIcons(1)}", "不能再获得能量")
        value = value.replace("获得{energyPrefix:energyIcons(1)}", "获得1点能量")
        value = value.replace("点{energyPrefix:energyIcons(1)}", "点能量")
        value = re.sub(r"(?<=[\d}])\{energyPrefix:energyIcons\(1\)\}", "点能量", value)
        localization[key] = value.replace("{energyPrefix:energyIcons(1)}", "能量")
    keyword_loc = parser.load_json(parser.LOC_DIR / "card_keywords.json")
    catalog = json.loads((codex / "data/cards.json").read_text(encoding="utf-8"))
    sources = {parser.slugify_model_name(p.stem): p for p in parser.CARDS_DIR.glob("*.cs")}
    pools = {"ironclad": "铁甲战士", "silent": "静默猎手", "defect": "故障机器人", "necrobinder": "亡灵契约师", "regent": "储君", "colorless": "无色", "event": "事件", "token": "衍生", "status": "状态", "curse": "诅咒", "quest": "任务", "unknown": "其他"}
    digest = hashlib.sha256()
    for p in [Path(__file__), parser_path, codex / "data/cards.json", parser.LOC_DIR / "cards.json", parser.LOC_DIR / "card_keywords.json", *sorted(sources.values())]:
        digest.update(p.name.encode())
        digest.update(p.read_bytes())

    def cost(raw):
        return int(raw) if re.fullmatch(r"\d+", str(raw)) else raw

    def keywords(values):
        return [parser.strip_tags(keyword_loc.get(parser.keyword_loc_key(k) + ".title", k)) for k in sorted(values)]

    def direct(text):
        damage = re.fullmatch(r"(对所有敌人)?造成(\d+)点伤害(?:(两次)|(\d+)次)?。", text)
        block = re.fullmatch(r"获得(\d+)点格挡。", text)
        if damage:
            hits = 2 if damage[3] else int(damage[4] or 1)
            return {"kind": "aoe" if damage[1] else "attack", "value": int(damage[2]), "hits": hits, "total": int(damage[2]) * hits}
        if block:
            return {"kind": "block", "value": int(block[1]), "hits": 1, "total": int(block[1])}
        return None

    rows = []
    for entry in catalog:
        source = sources[entry["id"]]
        card = parser.parse_card(source.stem)
        code = source.read_text(encoding="utf-8-sig")
        body = parser.extract_upgrade_body(code)
        base_cost, _, upgrade_cost = card.cost.partition("->")
        absolute = re.search(r"UpgradeEnergyCost\((\d+)\)", body)
        upgrade_cost = absolute[1] if absolute else upgrade_cost or base_cost
        texts = [parser.collect_description(card, localization, u) for u in [False, True]]
        # These cards change target/type at runtime. Their default target is single.
        if entry["id"] in {"SHIV", "SOVEREIGN_BLADE"}:
            texts = [re.sub(r"\{TargetType:choose\(AllEnemies\):对所有敌人\|\}", "", t) for t in texts]
        if entry["id"] == "MAD_SCIENCE":
            texts = ["效果由事件中选择的类型与词缀决定。"] * 2
        assert all("{" not in t and "}" not in t for t in texts), entry["id"]
        tags = []
        combined = "\n".join(texts)
        for tag, pattern in [("伤害", r"伤害|敌人.*失去.*生命"), ("格挡", "格挡"), ("抽牌", r"抽.*张牌"), ("能量", "能量|免费|耗能"), ("延迟", "下回合|回合结束后|下个回合"), ("成长", "力量|敏捷|集中|铸造|锻造|最大生命|翻倍"), ("生成", "加入|放入|召唤|生成"), ("状态", "虚弱|易伤|中毒|灾厄")]:
            if re.search(pattern, combined):
                tags.append(tag)
        if not tags:
            tags.append("其他")
        base_keywords, upgraded_keywords = keywords(card.keywords_base), keywords(card.keywords_upgraded)
        metrics = [direct(t) for t in texts]
        hit_text = re.search(r"伤害(两次|\d+次)", texts[0])
        hit_count = 2 if hit_text and hit_text[1] == "两次" else int(hit_text[1][:-1]) if hit_text else metrics[0]["hits"] if metrics[0] and metrics[0]["kind"] != "block" else None
        if hit_count is None and code.count("DamageCmd.Attack(") == 1 and ".WithHitCount(" not in code:
            hit_count = 1
        star_cost = entry.get("star_cost")
        upgraded_star_cost = star_cost
        star_delta = re.search(r"StarCost\.UpgradeBy\((-?\d+)\)", body)
        if star_delta and isinstance(star_cost, int):
            upgraded_star_cost += int(star_delta[1])
        rows.append({"id": card.loc_key, "className": source.stem, "name": parser.card_title(localization, card.loc_key), "pool": pools[entry["color"]], "rarity": parser.RARITY_ZH.get(card.rarity, card.rarity), "type": parser.TYPE_ZH.get(card.type_name, card.type_name), "cost": cost(base_cost), "upgradedCost": cost(upgrade_cost), "starCost": "X" if entry.get("is_x_star_cost") else star_cost, "upgradedStarCost": "X" if entry.get("is_x_star_cost") else upgraded_star_cost, "text": texts[0], "upgradedText": texts[1], "keywords": base_keywords, "upgradedKeywords": upgraded_keywords, "tags": tags, "hitCount": hit_count, "metric": metrics[0], "upgradedMetric": metrics[1], "source": "extraction/raw/src/Core/Models/Cards/" + source.name})
    assert len(rows) == len({r["id"] for r in rows}) == len(catalog)
    return {"schemaVersion": 1, "date": "2026-11-08", "source": {"kind": "本机反编译源码与简体中文本地化", "sha256": digest.hexdigest(), "cards": len(rows)}, "cards": rows}


if __name__ == "__main__":
    codex, parser_path, destination = map(Path, sys.argv[1:])
    result = export(codex, parser_path)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"cards": len(result["cards"]), "directReferences": sum(bool(r["metric"]) for r in result["cards"]), "destination": str(destination)}, ensure_ascii=False))
