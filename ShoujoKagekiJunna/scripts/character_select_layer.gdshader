shader_type canvas_item;
render_mode unshaded;

uniform sampler2D subject_alpha : filter_linear, repeat_disable;
uniform sampler2D underpaint : filter_linear, repeat_disable;
uniform sampler2D parts : filter_linear, repeat_disable;
uniform int layer_kind = 0;
uniform bool repair_bow_string = false;
uniform bool separate_arm_layer = false;
uniform sampler2D arm_cutout : filter_linear, repeat_disable;
uniform sampler2D arm_backing : filter_linear, repeat_disable;
uniform sampler2D arm_backing_color : filter_linear, repeat_disable;
uniform sampler2D waist_backing : filter_linear, repeat_disable;
uniform sampler2D original_art : filter_linear, repeat_disable;
uniform sampler2D forearm_detail : filter_linear, repeat_disable;
varying vec4 layer_modulate;

void vertex() {
	layer_modulate = COLOR;
}

float left_cord_mask(vec2 uv, vec2 pixel_size) {
	vec2 point = uv / pixel_size;
	float distance = abs(point.y + point.x * 0.29726316 - 989.6) / 1.043249;
	vec3 color = texture(original_art, uv).rgb;
	// Preserve the warm wooden bow and gold cuff where the cord crosses them.
	float cord_color = 1.0 - smoothstep(0.04, 0.08, color.g - color.b);
	return (1.0 - smoothstep(5.0, 8.0, distance)) * cord_color
		* (1.0 - smoothstep(982.0, 990.0, point.x));
}

float arm_coverage(vec2 uv, vec2 pixel_size) {
	vec3 color = texture(original_art, uv).rgb;
	vec2 point = uv / pixel_size;
	// The source sleeve, skin, wood and strings are warm. Reject blue cloth
	// along the generated matte's fringe so no skirt texture joins the arm.
	float warm = smoothstep(0.0, 0.035, color.r - color.b);
	float jewel = step(290.0, point.x) * (1.0 - step(385.0, point.x)) * step(875.0, point.y);
	float lining = texture(forearm_detail, uv).a;
	vec2 margin = pixel_size * 4.0;
	float edge = max(texture(forearm_detail, uv + vec2(margin.x, 0.0)).a, texture(forearm_detail, uv - vec2(margin.x, 0.0)).a);
	edge = max(edge, texture(forearm_detail, uv + vec2(0.0, margin.y)).a);
	edge = max(edge, texture(forearm_detail, uv - vec2(0.0, margin.y)).a);
	edge = max(edge, texture(forearm_detail, uv + margin * 0.75).a);
	edge = max(edge, texture(forearm_detail, uv - margin * 0.75).a);
	edge = max(edge, texture(forearm_detail, uv + vec2(margin.x, -margin.y) * 0.75).a);
	edge = max(edge, texture(forearm_detail, uv - vec2(margin.x, -margin.y) * 0.75).a);
	float coverage = max(texture(arm_cutout, uv).a * max(warm, jewel), max(lining, edge * warm));
	// Preserve the solid cuff edge while removing the separately drawn cord.
	float distance = abs(point.y + point.x * 0.2517857 - 1002.3) / 1.03121;
	float string_mask = (1.0 - smoothstep(3.0, 5.0, distance)) * (1.0 - lining)
		* smoothstep(1105.0, 1111.0, point.x) * (1.0 - smoothstep(1253.0, 1260.0, point.x));
	return coverage * (1.0 - string_mask) * (1.0 - left_cord_mask(uv, pixel_size));
}

void fragment() {
	vec4 original = texture(TEXTURE, UV);
	if (layer_kind == 0 && repair_bow_string) {
		// Repair the thin original string on the dark uniform. A separate
		// rigid line can then disappear behind the stationary fur without kinking.
		vec2 point = UV / TEXTURE_PIXEL_SIZE;
		float distance = abs(point.y + point.x * 0.2517857 - 1002.3) / 1.03121;
		float repair = (1.0 - smoothstep(3.0, 5.0, distance))
			* smoothstep(1105.0, 1111.0, point.x) * (1.0 - smoothstep(1253.0, 1260.0, point.x))
			* (1.0 - smoothstep(0.48, 0.57, original.g));
		float fur_edge = point.y < 720.0 ? 1249.0 + (point.y - 680.0) * 0.6 : 1273.0 + (point.y - 720.0) * 0.65;
		repair *= 1.0 - smoothstep(fur_edge - 1.0, fur_edge + 1.0, point.x);
		vec2 normal = normalize(vec2(0.2517857, 1.0)) * 8.0 * TEXTURE_PIXEL_SIZE;
		vec3 backing = texture(TEXTURE, UV + normal).rgb;
		original.rgb = mix(original.rgb, backing, repair);
	}
	float silhouette = clamp(texture(subject_alpha, UV).a / 0.97, 0.0, 1.0);
	vec4 region = texture(parts, UV);
	vec4 painted = vec4(original.rgb, silhouette);
	if (layer_kind == 0) {
		// Original RGB preserves the face, glasses, hands and bow pixel for pixel.
		// Repaired alpha removes the resting outline underneath moving tips.
		vec4 repaired = texture(underpaint, UV);
		repaired.a = min(clamp(repaired.a / 0.97, 0.0, 1.0), silhouette);
		float hair = 1.0 - smoothstep(0.13, 0.23, original.r - original.b);
		// Only the ponytail reveals another part of the character behind it.
		float backing_cape = smoothstep(0.13, 0.23, repaired.r - repaired.b);
		repaired.rgb = mix(original.rgb, repaired.rgb, region.g * hair * backing_cape);
		float repair_coverage = smoothstep(0.01, 0.15, region.a) * mix(1.0, hair, region.g);
		painted = mix(painted, repaired, repair_coverage);
		if (separate_arm_layer && UV.y > 630.0 * TEXTURE_PIXEL_SIZE.y
				&& UV.x > 270.0 * TEXTURE_PIXEL_SIZE.x && UV.x < 1330.0 * TEXTURE_PIXEL_SIZE.x) {
			float mask = texture(arm_cutout, UV).a;
			// Erase a small antialiasing fringe as well, so the old arm cannot
			// remain as a stationary outline when the cutout moves away.
			for (int radius = 1; radius <= 5; radius++) {
				vec2 margin = TEXTURE_PIXEL_SIZE * float(radius * 3);
				float nearby = texture(arm_cutout, UV + vec2(margin.x, 0.0)).a;
				nearby = max(nearby, texture(arm_cutout, UV - vec2(margin.x, 0.0)).a);
				nearby = max(nearby, texture(arm_cutout, UV + vec2(0.0, margin.y)).a);
				nearby = max(nearby, texture(arm_cutout, UV - vec2(0.0, margin.y)).a);
				nearby = max(nearby, texture(arm_cutout, UV + margin * 0.75).a);
				nearby = max(nearby, texture(arm_cutout, UV - margin * 0.75).a);
				nearby = max(nearby, texture(arm_cutout, UV + vec2(margin.x, -margin.y) * 0.75).a);
				nearby = max(nearby, texture(arm_cutout, UV - vec2(margin.x, -margin.y) * 0.75).a);
				mask = max(mask, nearby * min(1.0, 1.5 - float(radius) * 0.25));
			}
			// Native pixel coordinates also preserve a one-pixel canvas crop
			// in the generated backing without stretching its clothing texture.
			vec2 pixel = UV / TEXTURE_PIXEL_SIZE;
			vec4 backing = texture(arm_backing, pixel / vec2(textureSize(arm_backing, 0)));
			backing.rgb = texture(arm_backing_color, pixel / vec2(textureSize(arm_backing_color, 0))).rgb;
			// This cropped underpainting carries the narrower pleats and the
			// empty space the raised bow exposes along their left edge.
			vec2 waist_uv = (pixel - vec2(650.0, 670.0)) / vec2(textureSize(waist_backing, 0));
			if (all(greaterThanEqual(waist_uv, vec2(0.0))) && all(lessThan(waist_uv, vec2(1.0)))) {
				backing = texture(waist_backing, waist_uv);
			}
			backing.a = clamp(backing.a / 0.97, 0.0, 1.0);
			painted = mix(painted, backing, smoothstep(0.02, 0.98, mask));
		}
		if (separate_arm_layer && UV.x > 890.0 * TEXTURE_PIXEL_SIZE.x && UV.x < 1170.0 * TEXTURE_PIXEL_SIZE.x
				&& UV.y > 595.0 * TEXTURE_PIXEL_SIZE.y && UV.y < 835.0 * TEXTURE_PIXEL_SIZE.y) {
			vec2 point = UV / TEXTURE_PIXEL_SIZE;
			float sleeve = smoothstep(890.0, 905.0, point.x) * (1.0 - smoothstep(1150.0, 1170.0, point.x))
				* smoothstep(595.0, 615.0, point.y) * (1.0 - smoothstep(815.0, 835.0, point.y));
			// Keep the original visible upper sleeve. Only the actual arm and
			// bow footprint can reveal the generated hidden-cloth backing here.
			float moving = smoothstep(0.04, 0.96, arm_coverage(UV, TEXTURE_PIXEL_SIZE));
			painted = mix(painted, vec4(original.rgb, silhouette), sleeve * (1.0 - moving));
		}
		if (separate_arm_layer && UV.x > 350.0 * TEXTURE_PIXEL_SIZE.x && UV.x < 990.0 * TEXTURE_PIXEL_SIZE.x
				&& UV.y > 600.0 * TEXTURE_PIXEL_SIZE.y) {
			// Remove the resting cord from both the silhouette and the sleeve.
			// Neighbour alpha distinguishes empty background from solid cloth.
			float string_mask = left_cord_mask(UV, TEXTURE_PIXEL_SIZE);
			vec2 normal = normalize(vec2(0.29726316, 1.0)) * 10.0 * TEXTURE_PIXEL_SIZE;
			vec3 above = texture(TEXTURE, UV - normal).rgb;
			vec3 below = texture(TEXTURE, UV + normal).rgb;
			float above_alpha = texture(subject_alpha, UV - normal).a;
			float below_alpha = texture(subject_alpha, UV + normal).a;
			vec3 cloth = mix(below, above, above_alpha / max(0.001, above_alpha + below_alpha));
			float cloth_alpha = min(silhouette, clamp((above_alpha + below_alpha) / 1.94, 0.0, 1.0));
			painted = mix(painted, vec4(cloth, cloth_alpha), string_mask);
		}
	} else {
		float coverage = 0.0;
		if (layer_kind == 1) { coverage = region.r * region.a; }
		if (layer_kind == 2) { coverage = region.g * region.a; }
		if (layer_kind == 3) { coverage = region.b * region.a; }
		if (layer_kind == 4) { coverage = max(0.0, region.a - max(region.r, max(region.g, region.b))); }
		if (layer_kind == 5) {
			vec2 point = UV / TEXTURE_PIXEL_SIZE;
			float hem = 702.0 + (point.x - 1400.0) * 0.21;
			coverage = smoothstep(1340.0, 1490.0, point.x) * smoothstep(545.0, 605.0, point.y)
				* (1.0 - smoothstep(hem - 24.0, hem, point.y));
		}
		painted.a *= coverage;
		if (layer_kind == 6) {
			painted.a = min(silhouette, smoothstep(0.04, 0.96, arm_coverage(UV, TEXTURE_PIXEL_SIZE)));
		}
		// The ponytail end overlaps the red cape. Keep its original red backing
		// on the repaired static layer instead of moving it with the hair.
		if (layer_kind == 2) {
			painted.a *= 1.0 - smoothstep(0.13, 0.23, original.r - original.b);
		}
	}
	COLOR = painted * layer_modulate;
}
