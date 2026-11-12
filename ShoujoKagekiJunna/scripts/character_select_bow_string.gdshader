shader_type canvas_item;
render_mode unshaded;

varying vec2 art_position;
uniform vec2 string_start;
uniform vec2 string_end;
uniform bool left_string = false;
uniform float breath_amount = 0.0;

void vertex() {
	art_position = VERTEX;
}

void fragment() {
	vec2 direction = string_end - string_start;
	float along = clamp(dot(art_position - string_start, direction) / dot(direction, direction), 0.0, 1.0);
	float distance = length(art_position - string_start - direction * along);
	float line_alpha = 1.0 - smoothstep(0.9, 1.6, distance);
	// Convert to the sleeve/collar's resting coordinates for occlusion.
	// These lower attachment regions use only the chest breathing field.
	vec2 resting = art_position;
	for (int pass = 0; pass < 2; pass++) {
		float weight = smoothstep(890.0, 1040.0, resting.x) * (1.0 - smoothstep(1380.0, 1500.0, resting.x));
		weight *= 1.0 - smoothstep(580.0, 820.0, resting.y);
		resting = art_position - vec2((resting.x - 1165.0) * 0.0022, -3.4) * weight * breath_amount;
	}
	float occlusion;
	if (left_string) {
		occlusion = 1.0 - smoothstep(982.0, 990.0, resting.x);
	} else {
		// The cord remains straight as its endpoint slides behind fur.
		float fur_edge;
		if (resting.y < 680.0) {
			fur_edge = 1230.0 + (resting.y - 650.0) * 0.6333333;
		} else if (resting.y < 720.0) {
			fur_edge = 1249.0 + (resting.y - 680.0) * 0.6;
		} else {
			fur_edge = 1273.0 + (resting.y - 720.0) * 0.65;
		}
		occlusion = 1.0 - smoothstep(fur_edge - 1.0, fur_edge + 1.0, resting.x);
	}
	COLOR.a *= line_alpha * occlusion;
}
