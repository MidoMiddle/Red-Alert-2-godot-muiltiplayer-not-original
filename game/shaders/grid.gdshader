// Ground of M2: every cell takes the flat colour of its land type from the simulation map (one texel per cell),
// with a faint line every cell and a stronger line every 8 cells. Symbolic look, docs/implementation/conventions.md, section 12.
shader_type spatial;

uniform sampler2D land_map : source_color, filter_nearest;
uniform vec3 fallback_color : source_color = vec3(0.34, 0.36, 0.32);

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
	color *= mix(0.85, 1.0, grid(world_position.xz, 1.0));
	color *= mix(0.7, 1.0, grid(world_position.xz, 8.0));
	ALBEDO = color;
	ROUGHNESS = 0.95;
}
