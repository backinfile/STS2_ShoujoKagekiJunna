"""Guard generated aliases and hints while preserving versioned gameplay and save data."""
import copy
import importlib.util
import json
from pathlib import Path
import unittest

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('card_catalog_generator', HERE / 'generate-card-catalog.py')
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


class CardMetadataChecks(unittest.TestCase):
    def test_generated_metadata_matches_catalog_and_preserves_rules(self):
        catalog = json.loads(generator.CATALOG_PATH.read_text(encoding='utf-8'))
        for card in catalog['cards'] + catalog.get('retiredCards', []):
            with self.subTest(card=card['id']):
                result = generator.enrich_definition(copy.deepcopy(card))
                self.assertEqual(card, result)
                names = [name for value in result['values'] for name in [value['name']] + value['aliases']]
                self.assertEqual(len(names), len(set(names)))
                original_rules = copy.deepcopy(card)
                for entry in (result, original_rules):
                    entry.pop('tips', None)
                    entry.pop('upgradedTips', None)
                    for value in entry['values']:
                        value.pop('aliases', None)
                self.assertEqual(original_rules, result)

    def test_saved_fields_and_semantic_names_are_independent(self):
        entry = {'id': 'JA35', 'knowledge': False, 'type': '能力', 'text': '', 'upgradedText': '',
                 'values': [{'name': 'N0', 'base': 1, 'upgraded': 2}]}
        result = generator.enrich_definition(entry)
        self.assertEqual(result['values'][0]['name'], 'N0')
        self.assertIn('Turns', result['values'][0]['aliases'])
        self.assertEqual(result, generator.enrich_definition(copy.deepcopy(result)))

    def test_invalid_required_binding_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'JA35 has no field N0'):
            generator.enrich_definition({'id': 'JA35', 'values': []})


if __name__ == '__main__':
    unittest.main()
