// Ground of M0: flat colour with a line every cell and a stronger line every 8 cells.
shader_type spatial;

uniform vec3 base_color : source_color = vec3(0.34, 0.36, 0.32);
uniform vec3 line_color : source_color = vec3(0.27, 0.29, 0.26);
uniform vec3 major_color : source_color = vec3(0.19, 0.2, 0.18);

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
	vec3 color = base_color;
	color = mix(line_color, color, grid(world_position.xz, 1.0));
	color = mix(major_color, color, grid(world_position.xz, 8.0));
	ALBEDO = color;
	ROUGHNESS = 0.95;
}
