from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
scene = ['[gd_scene load_steps=10 format=3]']
for i, name in enumerate(['portrait', 'aim'], 1):
    scene.append(f'[ext_resource type="Texture2D" path="res://ShoujoKagekiJunna/images/{name}.png" id="{i}"]')

def animation(name, texture_ids, times, duration, positions=None, loop=False):
    scene.extend([f'[sub_resource type="Animation" id="{name}"]', f'resource_name = "{name}"',
                  f'length = {duration}', f'loop_mode = {1 if loop else 0}',
                  'tracks/0/type = "value"', 'tracks/0/path = NodePath("Visuals:texture")',
                  'tracks/0/interp = 1', 'tracks/0/enabled = true',
                  'tracks/0/keys = {"times": PackedFloat32Array(' + ','.join(map(str,times)) +
                  '), "transitions": PackedFloat32Array(' + ','.join(['1']*len(times)) +
                  '), "update": 1, "values": [' + ','.join(f'ExtResource("{i}")' for i in texture_ids) + ']}'])
    if positions:
        scene.extend(['tracks/1/type = "value"', 'tracks/1/path = NodePath("Visuals:position")',
                      'tracks/1/interp = 1', 'tracks/1/enabled = true',
                      'tracks/1/keys = {"times": PackedFloat32Array(' + ','.join(map(str,times)) +
                      '), "transitions": PackedFloat32Array(' + ','.join(['1']*len(times)) +
                      '), "update": 0, "values": [' + ','.join(f'Vector2({x}, {y})' for x,y in positions) + ']}'])

animation('Idle', [1,1,1], [0,1.4,2.8], 2.8, [(0,-153),(0,-155),(0,-153)], True)
animation('Attack', [2,2,1], [0,.15,.42], .5, [(0,-153),(14,-153),(0,-153)])
animation('Cast', [1,1], [0,.38], .45)
animation('Hit', [1,1], [0,.22], .3, [(-8,-153),(0,-153)])
animation('Dead', [1], [0], 1.5)
animation('Relaxed', [1], [0], 1, loop=True)
scene.extend(['[sub_resource type="AnimationLibrary" id="library"]',
              '_data = {' + ','.join(f'"{n}": SubResource("{n}")' for n in ['Idle','Attack','Cast','Hit','Dead','Relaxed']) + '}',
              '[node name="JunnaCombat" type="Node2D"]',
              '[node name="Visuals" type="Sprite2D" parent="."]', 'unique_name_in_owner = true',
              'position = Vector2(0, -153)', 'scale = Vector2(0.2, 0.2)', 'texture = ExtResource("1")',
              '[node name="Bounds" type="Control" parent="."]', 'unique_name_in_owner = true',
              'offset_left = -90.0', 'offset_top = -300.0', 'offset_right = 95.0', 'offset_bottom = 0.0', 'mouse_filter = 2',
              '[node name="AnimationPlayer" type="AnimationPlayer" parent="."]',
              'autoplay = "Idle"', 'libraries = {"": SubResource("library")}'])
(ROOT / 'ShoujoKagekiJunna/scenes/junna_combat.tscn').write_text('\n'.join(scene) + '\n', encoding='utf-8')
