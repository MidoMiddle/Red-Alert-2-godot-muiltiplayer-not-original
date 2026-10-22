// Ground of M2: every cell takes the flat colour of its land type from the simulation map (one texel per cell, cliffs in
// rock), with a faint line every cell and a stronger line every 8 cells. Symbolic look, docs/implementation/conventions.md,
// section 12. M7 step 9: the normal is the face's own, so every plane of the relief reads apart and a cliff is a wall, and a
// thin line marks every height level where the ground crosses it (on slopes and cliff walls; flat ground has none).
// The debug cell overlay (F7, M2 step 14) blends one RGBA texel per cell over it; bucket_lines adds the 4x4 index grid.
shader_type spatial;

uniform sampler2D land_map : source_color, filter_nearest;
uniform vec3 fallback_color : source_color = vec3(0.34, 0.36, 0.32);
uniform sampler2D overlay_map : source_color, filter_nearest;
uniform bool overlay_enabled = false;
uniform bool bucket_lines = false;
uniform float level_height = 0.40625;

varying vec3 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float grid(vec2 position, float spacing) {
	vec2 p = position / spacing;
	vec2 distance_to_line = abs(fract(p - 0.5) - 0.5) / fwidth(p);
	return clamp(min(distance_to_line.x, distance_to_line.y), 0.0, 1.0);
}

void fragment() {
	ivec2 size = textureSize(land_map, 0);
	vec3 color = fallback_color;
	if (size.x > 1) {
		ivec2 cell = clamp(ivec2(floor(world_position.xz)), ivec2(0), size - 1);
		color = texelFetch(land_map, cell, 0).rgb;
	}
	if (overlay_enabled) {
		ivec2 overlay_size = textureSize(overlay_map, 0);
		ivec2 overlay_cell = clamp(ivec2(floor(world_position.xz)), ivec2(0), overlay_size - 1);
		vec4 overlay = texelFetch(overlay_map, overlay_cell, 0);
		color = mix(color, overlay.rgb, overlay.a);
	}
	color *= mix(0.85, 1.0, grid(world_position.xz, 1.0));
	if (bucket_lines) {
		color *= mix(0.35, 1.0, grid(world_position.xz, 4.0));
	}
	color *= mix(0.7, 1.0, grid(world_position.xz, 8.0));
	float level = world_position.y / level_height;
	float level_change = fwidth(level);
	if (level_change > 0.001) {
		float to_line = abs(fract(level - 0.5) - 0.5) / level_change;
		color *= mix(0.55, 1.0, clamp(to_line, 0.0, 1.0));
	}
	NORMAL = normalize(cross(dFdy(VERTEX), dFdx(VERTEX)));
	ALBEDO = color;
	ROUGHNESS = 0.95;
}
