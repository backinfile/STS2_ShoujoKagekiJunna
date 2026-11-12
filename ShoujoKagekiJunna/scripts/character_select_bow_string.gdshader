shader_type canvas_item;
render_mode unshaded;

varying vec2 art_position;
uniform vec2 string_start;
uniform vec2 string_end;

void vertex() {
	art_position = VERTEX;
}

void fragment() {
	vec2 direction = string_end - string_start;
	float along = clamp(dot(art_position - string_start, direction) / dot(direction, direction), 0.0, 1.0);
	float distance = length(art_position - string_start - direction * along);
	float line_alpha = 1.0 - smoothstep(0.9, 1.6, distance);
	// Occlusion uses the fixed collar edge in art coordinates. The line
	// remains straight while its visible endpoint slides behind the fur.
	float fur_edge;
	if (art_position.y < 680.0) {
		fur_edge = 1230.0 + (art_position.y - 650.0) * 0.6333333;
	} else if (art_position.y < 720.0) {
		fur_edge = 1249.0 + (art_position.y - 680.0) * 0.6;
	} else {
		fur_edge = 1273.0 + (art_position.y - 720.0) * 0.65;
	}
	COLOR.a *= line_alpha * (1.0 - smoothstep(fur_edge - 1.0, fur_edge + 1.0, art_position.x));
}
