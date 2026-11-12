extends Node2D

const ART_SIZE = Vector2(1672.0, 941.0)
const ORIGINAL = preload("res://ShoujoKagekiJunna/images/character_select.png")
const BACKGROUND = preload("res://ShoujoKagekiJunna/images/character_select/background.png")
const SUBJECT = preload("res://ShoujoKagekiJunna/images/character_select/subject.png")
const UNDERPAINT = preload("res://ShoujoKagekiJunna/images/character_select/underpaint.png")
const PARTS = preload("res://ShoujoKagekiJunna/images/character_select/parts.svg")
const ARM_CUTOUT = preload("res://ShoujoKagekiJunna/images/character_select/arm_bow.png")
const ARM_BACKING = preload("res://ShoujoKagekiJunna/images/character_select/arm_backing.png")
const ARM_BACKING_COLOR = preload("res://ShoujoKagekiJunna/images/character_select/arm_backing_color.png")
const FOREARM_DETAIL = preload("res://ShoujoKagekiJunna/images/character_select/forearm_detail.svg")
const LAYER_SHADER = preload("res://ShoujoKagekiJunna/scripts/character_select_layer.gdshader")
const STRING_SHADER = preload("res://ShoujoKagekiJunna/scripts/character_select_bow_string.gdshader")
const STAR_SCRIPT = preload("res://ShoujoKagekiJunna/scripts/character_select_effects.gd")
const BREATH_PERIOD = 5.6
const BREATH_INHALE = 1.9
const BREATH_EXHALE = 3.3
const BREATH_ANCHOR = Vector2(1165.0, 900.0)
const ARM_PIVOT = Vector2(1060.0, 685.0)
const ARM_SWAY_DEGREES = 0.225
const ARM_REST_DEGREES = 0.06
const ARM_SWAY_OFFSET = Vector2(-1.0, -4.5)
const ARM_ATTACHMENT = [Vector2(980, 715), Vector2(1000, 686), Vector2(1030, 671),
	Vector2(1060, 663), Vector2(1090, 661), Vector2(1100, 678), Vector2(1108, 710), Vector2(1106, 739)]
const RIGHT_STRING_START = Vector2(1110.0, 722.2)
const RIGHT_STRING_END = Vector2(1300.0, 674.36)
const BOW_POINTS = [Vector2(337, 906), Vector2(416, 871), Vector2(600, 871),
	Vector2(800, 870), Vector2(930, 844), Vector2(1120, 779), Vector2(1290, 736)]
const PIECES = [
	{"name": "Fringe", "kind": 1, "rect": Rect2(918, 355, 86, 82), "amplitude": Vector2(8, 4), "period": 4.8, "phase": 0.0},
	{"name": "Ponytail", "kind": 2, "rect": Rect2(1386, 355, 92, 84), "amplitude": Vector2(11, 5), "period": 4.8, "phase": -0.5},
	{"name": "CapeRim", "kind": 3, "rect": Rect2(1466, 346, 210, 103), "amplitude": Vector2(5, 13), "period": 5.4, "phase": 0.2},
	{"name": "LowerCape", "kind": 5, "rect": Rect2(1330, 530, 366, 250), "amplitude": Vector2(4, 10), "period": 6.4, "phase": 1.4 + PI},
	{"name": "FurHem", "kind": 4, "rect": Rect2(1461, 741, 179, 103), "amplitude": Vector2(4, 9), "period": 5.4, "phase": -0.7}]

@export_range(0.0, 2.0, 0.1) var breathing_strength = 1.0
@export_range(0.0, 2.0, 0.1) var arm_sway_strength = 1.0

var visible_art_rect = Rect2(Vector2.ZERO, ART_SIZE)
var _elapsed = 0.0
var _pieces: Array[Dictionary] = []
var _body: Dictionary = {}
var _arm_layer: Dictionary = {}
var _arm_weights = PackedFloat32Array()
var _right_string: Polygon2D
var _character: Node2D

func _ready():
	_fit_to_viewport()
	# Native select UI is a later sibling. Keep the entire background at its
	# parent's Z and use child order so no art escapes above those controls.
	_add_sprite("Background", BACKGROUND)
	var stars = Node2D.new()
	stars.name = "Stars"
	stars.set_script(STAR_SCRIPT)
	add_child(stars)
	_character = Node2D.new()
	_character.name = "Character"
	add_child(_character)
	_add_body()
	_add_arm_layer()
	for definition in PIECES:
		_add_piece(definition)
	# The original stage rail and the fixed garment below its occlusion line.
	var stage = Polygon2D.new()
	stage.name = "StageForeground"
	stage.texture = ORIGINAL
	stage.polygon = PackedVector2Array([Vector2(950, 941), Vector2(1672, 788), Vector2(1672, 941)])
	stage.uv = stage.polygon
	add_child(stage)

func _add_sprite(node_name: String, texture: Texture2D) -> Sprite2D:
	var sprite = Sprite2D.new()
	sprite.name = node_name
	sprite.centered = false
	sprite.texture = texture
	add_child(sprite)
	return sprite

func _layer_material(kind: int) -> ShaderMaterial:
	var result = ShaderMaterial.new()
	result.shader = LAYER_SHADER
	result.set_shader_parameter("subject_alpha", SUBJECT)
	result.set_shader_parameter("underpaint", UNDERPAINT)
	result.set_shader_parameter("parts", PARTS)
	result.set_shader_parameter("layer_kind", kind)
	result.set_shader_parameter("repair_bow_string", kind == 0)
	result.set_shader_parameter("arm_cutout", ARM_CUTOUT)
	result.set_shader_parameter("arm_backing", ARM_BACKING)
	result.set_shader_parameter("arm_backing_color", ARM_BACKING_COLOR)
	result.set_shader_parameter("forearm_detail", FOREARM_DETAIL)
	result.set_shader_parameter("original_art", ORIGINAL)
	result.set_shader_parameter("separate_arm_layer", kind == 0)
	return result

func _grid_patch(node_name: String, x_cuts: PackedFloat32Array, y_cuts: PackedFloat32Array) -> Dictionary:
	var patch = Polygon2D.new()
	patch.name = node_name
	patch.texture = ORIGINAL
	var columns = x_cuts.size() - 1
	var rows = y_cuts.size() - 1
	var rest = PackedVector2Array()
	var breath = PackedVector2Array()
	for y in y_cuts:
		for x in x_cuts:
			var point = Vector2(x, y)
			rest.append(point)
			breath.append(_breath_vector(point))
	var cells: Array[PackedInt32Array] = []
	for row in range(rows):
		for column in range(columns):
			var a = row * (columns + 1) + column
			cells.append(PackedInt32Array([a, a + 1, a + columns + 2, a + columns + 1]))
	patch.polygon = rest
	patch.uv = rest
	patch.polygons = cells
	_character.add_child(patch)
	return {"node": patch, "rest": rest, "breath": breath}

func _add_body():
	# This mesh carries breathing only. The outfit never receives arm weights.
	var x_cuts = PackedFloat32Array([0.0])
	var y_cuts = PackedFloat32Array([0.0])
	for x in range(280, 1461, 20):
		x_cuts.append(x)
	for y in range(330, 931, 20):
		y_cuts.append(y)
	x_cuts.append(ART_SIZE.x)
	y_cuts.append(ART_SIZE.y)
	_body = _grid_patch("Actor", x_cuts, y_cuts)
	_body.node.material = _layer_material(0)
	_right_string = Polygon2D.new()
	_right_string.name = "RightBowString"
	_right_string.color = Color(0.48, 0.36, 0.39, 1.0)
	_right_string.material = ShaderMaterial.new()
	_right_string.material.shader = STRING_SHADER
	_character.add_child(_right_string)

func _add_arm_layer():
	# A cutout has no skirt pixels: shared vertices cannot pull the outfit.
	var x_cuts = PackedFloat32Array()
	var y_cuts = PackedFloat32Array()
	for x in range(280, 1361, 20):
		x_cuts.append(x)
		if x >= 940 and x < 1140:
			x_cuts.append(x + 10)
	for y in range(610, 931, 20):
		y_cuts.append(y)
		if y >= 650 and y < 810:
			y_cuts.append(y + 10)
	y_cuts.append(ART_SIZE.y)
	_arm_layer = _grid_patch("ArmAndBow", x_cuts, y_cuts)
	_arm_layer.node.material = _layer_material(6)
	for point in _arm_layer.rest:
		_arm_weights.append(_arm_weight(point))

func _segment_weight(point: Vector2, start: Vector2, finish: Vector2, inner: float, outer: float) -> float:
	var direction = finish - start
	var along = clampf((point - start).dot(direction) / direction.length_squared(), 0.0, 1.0)
	return 1.0 - smoothstep(inner, outer, point.distance_to(start + direction * along))

func _arm_weight(point: Vector2) -> float:
	if point.y <= 610.0:
		return 0.0
	var weight = _segment_weight(point, ARM_PIVOT, Vector2(972, 769), 75.0, 105.0)
	weight = maxf(weight, _segment_weight(point, Vector2(972, 769), Vector2(900, 850), 85.0, 112.0))
	for index in range(BOW_POINTS.size() - 1):
		weight = maxf(weight, _segment_weight(point, BOW_POINTS[index], BOW_POINTS[index + 1], 70.0, 100.0))
	# Both visible string sections share the bow's rigid rotation.
	weight = maxf(weight, _segment_weight(point, Vector2(374, 864), Vector2(1037, 683), 36.0, 64.0))
	weight = maxf(weight, _segment_weight(point, Vector2(1112, 722), Vector2(1250, 686), 36.0, 64.0))
	# The bow disappears behind the fixed fur collar on the right.
	var fur_edge = 1185.0 + (point.y - 600.0) * 0.66
	weight *= 1.0 - smoothstep(fur_edge - 30.0, fur_edge, point.x)
	if point.x > 700.0:
		weight *= 1.0 - smoothstep(904.0, 926.0, point.y)
	weight *= smoothstep(12.0, 60.0, point.distance_to(ARM_PIVOT))
	weight *= _arm_attachment_weight(point)
	return weight * smoothstep(610.0, 650.0, point.y)

func _arm_attachment_weight(point: Vector2) -> float:
	# Anchor the whole curved cuff seam, then spread the motion through the
	# forearm. The hand and bow keep their full amplitude past this blend.
	var distance = INF
	for index in range(ARM_ATTACHMENT.size() - 1):
		var closest = Geometry2D.get_closest_point_to_segment(point, ARM_ATTACHMENT[index], ARM_ATTACHMENT[index + 1])
		distance = minf(distance, point.distance_to(closest))
	var amount = clampf((distance - 6.0) / 100.0, 0.0, 1.0)
	var eased = amount * amount * amount * (amount * (amount * 6.0 - 15.0) + 10.0)
	# End the blend at the forearm's outer side; the nearby wooden bow
	# remains free to move with the grip rather than inheriting the seam pin.
	var forearm = 1.0 - smoothstep(0.0, 8.0, point.x + (point.y - 739.0) * 0.72 - 1106.0)
	var release = smoothstep(12.0, 40.0, distance)
	return lerpf(eased, 1.0, (1.0 - forearm) * release)

func _arm_transform(seconds: float) -> Transform2D:
	# The forearm and grip rise visibly while the bow counter-tilts as one
	# rigid piece. The elbow's pinned weights keep its attachment stationary.
	var wave = sin(seconds * TAU / BREATH_PERIOD - PI * 0.25)
	var degrees = ARM_REST_DEGREES - ARM_SWAY_DEGREES * wave
	var angle = deg_to_rad(degrees) * arm_sway_strength
	var shift = ARM_SWAY_OFFSET * wave * arm_sway_strength
	return Transform2D(angle, ARM_PIVOT - ARM_PIVOT.rotated(angle) + shift)

func _add_piece(definition: Dictionary):
	var bounds: Rect2 = definition.rect
	var columns = ceili(bounds.size.x / 10.0)
	var rows = ceili(bounds.size.y / 10.0)
	var x_cuts = PackedFloat32Array()
	var y_cuts = PackedFloat32Array()
	for column in range(columns + 1):
		x_cuts.append((bounds.position + bounds.size * Vector2(float(column) / columns, 0.0)).x)
	for row in range(rows + 1):
		y_cuts.append((bounds.position + bounds.size * Vector2(0.0, float(row) / rows)).y)
	var piece = _grid_patch(definition.name, x_cuts, y_cuts)
	piece.node.material = _layer_material(definition.kind)
	var weights = PackedFloat32Array()
	for point in piece.rest:
		weights.append(_piece_weight(definition.kind, point))
	piece.weights = weights
	piece.definition = definition
	_pieces.append(piece)

func _breath_vector(point: Vector2) -> Vector2:
	var weight = smoothstep(960.0, 1080.0, point.x) * (1.0 - smoothstep(1340.0, 1450.0, point.x))
	weight *= smoothstep(350.0, 420.0, point.y) * (1.0 - smoothstep(540.0, 640.0, point.y))
	return Vector2((point.x - 1165.0) * 0.0035, -1.8) * weight

func _breath_amount(seconds: float) -> float:
	var time = fposmod(seconds, BREATH_PERIOD)
	if time < BREATH_INHALE:
		return (0.5 - 0.5 * cos(PI * time / BREATH_INHALE)) * breathing_strength
	if time < BREATH_INHALE + BREATH_EXHALE:
		return (0.5 + 0.5 * cos(PI * (time - BREATH_INHALE) / BREATH_EXHALE)) * breathing_strength
	return 0.0

func _piece_weight(kind: int, point: Vector2) -> float:
	if kind == 1:
		return smoothstep(366.0, 424.0, point.y)
	if kind == 2:
		return smoothstep(365.0, 426.0, point.y) * smoothstep(1390.0, 1415.0, point.x)
	if kind == 3:
		return smoothstep(1470.0, 1550.0, point.x) * (1.0 - smoothstep(370.0, 445.0, point.y))
	if kind == 4:
		return smoothstep(1465.0, 1605.0, point.x) * smoothstep(745.0, 795.0, point.y)
	# A separate, slower wave on the lower red fold. Its attachment to the
	# body and white fur stays still; the existing four motions are unchanged.
	var hem = 702.0 + (point.x - 1400.0) * 0.21
	return smoothstep(1360.0, 1580.0, point.x) * smoothstep(550.0, 650.0, point.y) \
		* (1.0 - smoothstep(hem - 24.0, hem, point.y))

func _process(delta):
	if not is_visible_in_tree():
		return
	_elapsed += delta
	_fit_to_viewport()
	var breath_amount = _breath_amount(_elapsed)
	# One shared transform keeps the face, arm, bow and cape together.
	# Breathing grows gently from the waist; the stage and stars stay fixed.
	_character.scale = Vector2.ONE + Vector2(0.001, 0.0035) * breath_amount
	_character.position = BREATH_ANCHOR * (Vector2.ONE - _character.scale) + Vector2(0.0, -1.2) * breath_amount
	var arm_transform = _arm_transform(_elapsed)
	var string_start = arm_transform * RIGHT_STRING_START
	var string_end = arm_transform * RIGHT_STRING_END
	var tangent = (string_end - string_start).normalized()
	var normal = Vector2(-tangent.y, tangent.x) * 3.0
	_right_string.polygon = PackedVector2Array([string_start - tangent * 2.0 - normal,
		string_end + tangent * 2.0 - normal, string_end + tangent * 2.0 + normal,
		string_start - tangent * 2.0 + normal])
	_right_string.material.set_shader_parameter("string_start", string_start)
	_right_string.material.set_shader_parameter("string_end", string_end)
	var body_points: PackedVector2Array = _body.rest.duplicate()
	for index in range(body_points.size()):
		body_points[index] += _body.breath[index] * breath_amount
	(_body.node as Polygon2D).polygon = body_points
	var arm_points: PackedVector2Array = _arm_layer.rest.duplicate()
	for index in range(arm_points.size()):
		arm_points[index] += _arm_layer.breath[index] * breath_amount
		arm_points[index] += (arm_transform * _arm_layer.rest[index] - _arm_layer.rest[index]) * _arm_weights[index]
	(_arm_layer.node as Polygon2D).polygon = arm_points
	for piece in _pieces:
		var definition: Dictionary = piece.definition
		var points: PackedVector2Array = piece.rest.duplicate()
		var phase = _elapsed * TAU / definition.period + definition.phase
		for index in range(points.size()):
			var wave = phase - (points[index].x - definition.rect.position.x) / definition.rect.size.x * 0.65
			points[index] += Vector2(sin(wave) * definition.amplitude.x,
				sin(wave - 0.35) * definition.amplitude.y) * piece.weights[index]
			points[index] += piece.breath[index] * breath_amount
		(piece.node as Polygon2D).polygon = points

func _fit_to_viewport():
	var viewport_size = get_viewport_rect().size
	var cover = maxf(viewport_size.x / ART_SIZE.x, viewport_size.y / ART_SIZE.y)
	scale = Vector2.ONE * cover
	position = (viewport_size - ART_SIZE * cover) * 0.5
	visible_art_rect = Rect2(-position / cover, viewport_size / cover)
