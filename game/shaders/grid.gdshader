// Ground of the terrain. Each cell takes the texture of its land type (TerrainTextures.cs): snow, frozen grass, gravel, rock,
// ice, and open water with a ripple from the water normal map. While the textures are off, every cell takes the flat colour of
// its land type from the simulation map (one texel per cell, cliffs in rock), with a faint line every cell, a stronger line every
// 8 cells and a line at each height level: the symbolic look of docs/implementation/conventions.md, section 12. M7 step 9: the
// normal is the face's own, so every plane of the relief reads apart and a cliff is a wall. The debug cell overlay (F7, M2 step
// 14) blends one RGBA texel per cell over the ground in both modes; bucket_lines adds the 4x4 index grid.
shader_type spatial;

uniform sampler2D land_map : source_color, filter_nearest;
uniform vec3 fallback_color : source_color = vec3(0.34, 0.36, 0.32);
uniform sampler2D type_map : filter_nearest;
uniform sampler2D overlay_map : source_color, filter_nearest;
uniform bool overlay_enabled = false;
uniform bool bucket_lines = false;
uniform bool use_textures = false;
uniform float level_height = 0.40625;
uniform float tile_cells = 12.0;
uniform sampler2D tex_snow : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D tex_grass : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D tex_gravel : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D tex_rock : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D tex_ice : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D tex_water_normal : filter_linear_mipmap, repeat_enable;

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
	ivec2 cell = clamp(ivec2(floor(world_position.xz)), ivec2(0), size - 1);
	vec3 color = fallback_color;
	float roughness = 0.95;
	if (use_textures) {
		// The texture index of the cell, see TerrainTextures.IndexOf: 0 clear, 1 rough, 2 road, 3 beach, 4 shallow, 5 deep,
		// 6 rock, 7 ice.
		int index = int(floor(texelFetch(type_map, cell, 0).r * 255.0 + 0.5));
		vec2 uv = world_position.xz / tile_cells;
		if (index == 0) {
			color = texture(tex_snow, uv).rgb;
		} else if (index == 1) {
			color = texture(tex_grass, uv).rgb;
		} else if (index == 2) {
			// No road texture yet: a road is the snow, darker.
			color = texture(tex_snow, uv).rgb * 0.55;
		} else if (index == 3) {
			color = texture(tex_gravel, uv).rgb;
		} else if (index == 4 || index == 7) {
			color = texture(tex_ice, uv).rgb;
			roughness = 0.6;
		} else if (index == 5) {
			// Open water (a polynya or deep water): the water colour, with the ripple below.
			color = vec3(0.10, 0.24, 0.56);
			roughness = 0.25;
		} else {
			color = texture(tex_rock, uv).rgb;
		}
		if (index == 4 || index == 5 || index == 7) {
			vec3 n = texture(tex_water_normal, uv).xyz * 2.0 - 1.0;
			// Brightness from how far the ripple tilts towards the light (flat water is 0.918 in this direction).
			float facing = dot(normalize(vec3(n.x, 1.0, n.y)), normalize(vec3(-0.35, 1.0, -0.25)));
			color *= clamp(1.0 + (facing - 0.918) * 2.5, 0.8, 1.2);
		}
	} else if (size.x > 1) {
		color = texelFetch(land_map, cell, 0).rgb;
	}
	if (overlay_enabled) {
		ivec2 overlay_size = textureSize(overlay_map, 0);
		ivec2 overlay_cell = clamp(ivec2(floor(world_position.xz)), ivec2(0), overlay_size - 1);
		vec4 overlay = texelFetch(overlay_map, overlay_cell, 0);
		color = mix(color, overlay.rgb, overlay.a);
	}
	if (!use_textures) {
		color *= mix(0.85, 1.0, grid(world_position.xz, 1.0));
	}
	if (bucket_lines) {
		color *= mix(0.35, 1.0, grid(world_position.xz, 4.0));
	}
	if (!use_textures) {
		color *= mix(0.7, 1.0, grid(world_position.xz, 8.0));
		float level = world_position.y / level_height;
		float level_change = fwidth(level);
		if (level_change > 0.001) {
			float to_line = abs(fract(level - 0.5) - 0.5) / level_change;
			color *= mix(0.55, 1.0, clamp(to_line, 0.0, 1.0));
		}
	}
	NORMAL = normalize(cross(dFdy(VERTEX), dFdx(VERTEX)));
	ALBEDO = color;
	ROUGHNESS = roughness;
}
