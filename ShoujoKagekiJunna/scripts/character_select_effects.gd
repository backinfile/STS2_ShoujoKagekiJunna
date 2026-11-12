extends Node2D

const ART_SIZE = Vector2(1672.0, 941.0)
const GLINT_ORIGINS = [
	Vector2(0.138, 0.10), Vector2(0.502, 0.05), Vector2(0.448, 0.185),
	Vector2(0.86, 0.227), Vector2(0.946, 0.075), Vector2(0.315, 0.28),
	Vector2(0.043, 0.416), Vector2(0.498, 0.441), Vector2(0.08, 0.67),
	Vector2(0.49, 0.62), Vector2(0.35, 0.785), Vector2(0.21, 0.845)]

var _elapsed = 0.0
var _stars: Array[Dictionary] = []
var _glow: GradientTexture2D
var _ray: GradientTexture2D
var _visible_art_rect = Rect2(Vector2.ZERO, ART_SIZE)

func _ready():
	var gradient = Gradient.new()
	gradient.offsets = PackedFloat32Array([0.0, 0.08, 0.25, 0.55, 1.0])
	gradient.colors = PackedColorArray([
		Color(1, 1, 1, 1), Color(1, 1, 1, 0.8), Color(1, 1, 1, 0.24),
		Color(1, 1, 1, 0.035), Color(1, 1, 1, 0)])
	_glow = GradientTexture2D.new()
	_glow.gradient = gradient
	_glow.width = 128
	_glow.height = 128
	_glow.fill = GradientTexture2D.FILL_RADIAL
	_glow.fill_from = Vector2(0.5, 0.5)
	_glow.fill_to = Vector2(1.0, 0.5)
	var ray_gradient = Gradient.new()
	ray_gradient.offsets = PackedFloat32Array([0.0, 0.42, 0.8, 1.0])
	ray_gradient.colors = PackedColorArray([
		Color(1, 1, 1, 1), Color(1, 1, 1, 0.7), Color(1, 1, 1, 0.15), Color(1, 1, 1, 0)])
	_ray = _glow.duplicate()
	_ray.gradient = ray_gradient
	# Private, prewarmed phases never consume gameplay RNG.
	var rng = RandomNumberGenerator.new()
	rng.seed = 1060012
	for index in range(96):
		var origin = Vector2((index % 12 + rng.randf_range(0.12, 0.88)) / 12.0,
			(floorf(index / 12.0) + rng.randf_range(0.12, 0.88)) / 8.0)
		_add_star(rng, 0, origin)
	for origin in GLINT_ORIGINS:
		_add_star(rng, 1, origin)
	for origin in [Vector2(0.025, 0.05), Vector2(0.975, 0.045), Vector2(0.97, 0.94)]:
		_add_star(rng, 2, origin)
	_fit_to_art()

func _add_star(rng: RandomNumberGenerator, depth: int, origin: Vector2):
	var warm = rng.randf() < 0.18
	_stars.append({
		"depth": depth, "origin": origin, "phase": rng.randf(),
		"cycle": rng.randf_range(4.5, 8.5),
		"velocity": Vector2(rng.randf_range(10.0, 18.0), rng.randf_range(-12.0, -6.0)) * (1.0 if depth == 0 else (0.7 if depth == 1 else 0.35)),
		"core": rng.randf_range(0.5, 0.95) if depth == 0 else rng.randf_range(0.9, 1.25),
		"halo": rng.randf_range(3.2, 6.0) if depth == 0 else (rng.randf_range(8.0, 11.0) if depth == 1 else rng.randf_range(23.0, 34.0)),
		"ray": rng.randf_range(17.0, 27.0),
		"opacity": rng.randf_range(0.45, 0.8) if depth < 2 else rng.randf_range(0.055, 0.085),
		"tint": Color(1.0, 0.92, 0.78) if warm else Color(0.73, 0.84, 1.0)})

func _process(delta):
	if not is_visible_in_tree():
		return
	_elapsed += delta
	_fit_to_art()
	queue_redraw()

func _fit_to_art():
	_visible_art_rect = get_parent().visible_art_rect

func _star_position(star: Dictionary) -> Vector2:
	var point = _visible_art_rect.position + star.origin * _visible_art_rect.size
	point += star.velocity * _elapsed
	var bounds = _visible_art_rect.grow(40.0)
	return bounds.position + Vector2(fposmod(point.x - bounds.position.x, bounds.size.x),
		fposmod(point.y - bounds.position.y, bounds.size.y))

func _star_visibility(point: Vector2, depth: int) -> float:
	var visibility = 1.0
	# The actual character alpha occludes this layer; no approximate face cutout.
	var screen_uv = (point - _visible_art_rect.position) / _visible_art_rect.size
	if Rect2(0.105, 0.3, 0.375, 0.365).has_point(screen_uv):
		visibility *= 0.45 if depth == 0 else 0.2
	return visibility

func _draw():
	if _glow == null:
		return
	for star in _stars:
		_draw_star_light(star)

func _draw_star_light(star: Dictionary):
	var point = _star_position(star)
	var wave = 0.5 + 0.5 * sin(_elapsed * TAU / star.cycle + star.phase * TAU)
	var twinkle = 0.65 + wave * 0.35 if star.depth == 0 else 0.38 + pow(wave, 3.0) * 0.62
	var opacity = star.opacity * twinkle * _star_visibility(point, star.depth)
	if opacity < 0.005:
		return
	if star.depth == 2:
		_draw_glow(point, Vector2.ONE * star.halo, star.tint, opacity)
		return
	_draw_glow(point, Vector2.ONE * star.halo, star.tint, opacity * 0.46)
	# White luminous cores and tapered optical rays; no polygon/star-icon silhouettes.
	if star.depth == 1:
		_draw_rays(point, star.ray * (0.75 + wave * 0.25), star.tint, opacity * 0.68)
	draw_circle(point, star.core, Color(0.96, 0.98, 1.0, opacity), true, -1.0, true)
	_draw_glow(point, Vector2.ONE * star.core * 2.2, Color.WHITE, opacity * 0.45)

func _draw_glow(point: Vector2, half_size: Vector2, tint: Color, opacity: float):
	draw_texture_rect(_glow, Rect2(point - half_size, half_size * 2.0), false, Color(tint, opacity))

func _draw_rays(point: Vector2, length: float, tint: Color, opacity: float):
	if opacity < 0.005:
		return
	var vertical = Vector2(0.7, length)
	var horizontal = Vector2(length * 0.66, 0.6)
	draw_texture_rect(_ray, Rect2(point - vertical, vertical * 2.0), false, Color(tint, opacity))
	draw_texture_rect(_ray, Rect2(point - horizontal, horizontal * 2.0), false, Color(tint, opacity * 0.9))
