// Ground of the terrain (docs/implementation/M7_maps.md, 4.12). With the textures of the climate, every cell takes the material
// of the map's texture layer (MaterialSlots.cs: the slot of its material and its water, two bytes a cell): the albedo comes from
// one layer of ground_albedo, tiled, tinted and rough by the material's entry of the arrays below. Neighbouring materials blend
// over a border that noise bends and the brighter texture wins (a height blend), so the cells never show as squares; water gets a
// ripple lit by the sun and fades to the deep colour on deep water. While the textures are off, every cell takes the flat colour
// of its land type from the simulation map (one texel per cell, cliffs in rock), with a faint line every cell, a stronger line
// every 8 cells and a line at each height level: the symbolic look of docs/implementation/conventions.md, section 12. M7 step 9:
// the normal is the face's own, so every plane of the relief reads apart and a cliff is a wall. The debug cell overlay (F7, M2
// step 14) blends one RGBA texel per cell over the ground in both modes; bucket_lines adds the 4x4 index grid.
shader_type spatial;

const int MAX_MATERIALS = 16;

uniform sampler2D land_map : source_color, filter_nearest;
uniform vec3 fallback_color : source_color = vec3(0.34, 0.36, 0.32);
uniform sampler2D material_map : filter_nearest;
uniform sampler2D overlay_map : source_color, filter_nearest;
uniform bool overlay_enabled = false;
uniform bool bucket_lines = false;
uniform bool use_textures = false;
uniform float level_height = 0.40625;
uniform sampler2DArray ground_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float mat_tiling[MAX_MATERIALS];
uniform float mat_roughness[MAX_MATERIALS];
uniform vec3 mat_tint[MAX_MATERIALS];
uniform float mat_water[MAX_MATERIALS];
uniform float mat_frost[MAX_MATERIALS];
uniform vec3 frost_color : source_color = vec3(0.88, 0.91, 0.96);
uniform sampler2D tex_water_normal : filter_linear_mipmap, repeat_enable;
uniform vec3 deep_water_color : source_color = vec3(0.10, 0.24, 0.56);
// The direction the sunlight goes in (SceneLighting.cs), for the ripple of the water.
uniform vec3 sun_direction = vec3(0.62, -0.77, 0.15);

varying vec3 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float grid(vec2 position, float spacing) {
	vec2 p = position / spacing;
	vec2 distance_to_line = abs(fract(p - 0.5) - 0.5) / fwidth(p);
	return clamp(min(distance_to_line.x, distance_to_line.y), 0.0, 1.0);
}

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// Smooth value noise, 0…1.
float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

// The slot (x) and the water (y: 0 none, 1 shallow, 2 deep) of a cell.
ivec2 material_of(ivec2 cell, ivec2 size) {
	vec2 texel = texelFetch(material_map, clamp(cell, ivec2(0), size - 1), 0).rg;
	return ivec2(floor(texel * 255.0 + 0.5));
}

vec3 albedo_of(int slot, vec2 position, float frost_patches) {
	vec3 albedo = texture(ground_albedo, vec3(position / mat_tiling[slot], float(slot))).rgb * mat_tint[slot];
	return mix(albedo, frost_color, mat_frost[slot] * frost_patches);
}

void fragment() {
	ivec2 size = textureSize(land_map, 0);
	ivec2 cell = clamp(ivec2(floor(world_position.xz)), ivec2(0), size - 1);
	vec3 color = fallback_color;
	float roughness = 0.95;
	if (use_textures) {
		vec2 p = world_position.xz;
		// The four cells around the point, by their centres; noise bends the borders between them.
		vec2 warp = vec2(noise(p * 1.7), noise(p * 1.7 + vec2(17.3, 9.1))) - 0.5;
		vec2 q = p - 0.5 + warp * 0.5;
		ivec2 base = ivec2(floor(q));
		vec2 g = smoothstep(0.2, 0.8, fract(q));
		ivec2 m00 = material_of(base, size);
		ivec2 m10 = material_of(base + ivec2(1, 0), size);
		ivec2 m01 = material_of(base + ivec2(0, 1), size);
		ivec2 m11 = material_of(base + ivec2(1, 1), size);
		vec4 w = vec4((1.0 - g.x) * (1.0 - g.y), g.x * (1.0 - g.y), (1.0 - g.x) * g.y, g.x * g.y);
		// Frost lies thicker in some places than in others.
		float frost_patches = mix(0.55, 1.0, noise(p * 0.9));
		vec3 a00 = albedo_of(m00.x, p, frost_patches);
		vec3 a10 = albedo_of(m10.x, p, frost_patches);
		vec3 a01 = albedo_of(m01.x, p, frost_patches);
		vec3 a11 = albedo_of(m11.x, p, frost_patches);
		// Height blend: where two materials meet, the brighter texel (snow over dirt, stones over sand) wins a little.
		vec3 luma = vec3(0.3, 0.59, 0.11);
		vec4 h = w + 0.35 * vec4(dot(a00, luma), dot(a10, luma), dot(a01, luma), dot(a11, luma));
		float top = max(max(h.x, h.y), max(h.z, h.w));
		w = max(h - (top - 0.2), vec4(0.0));
		w /= w.x + w.y + w.z + w.w;
		color = a00 * w.x + a10 * w.y + a01 * w.z + a11 * w.w;
		roughness = dot(w, vec4(mat_roughness[m00.x], mat_roughness[m10.x], mat_roughness[m01.x], mat_roughness[m11.x]));
		// Large soft patches against the tiling of the photographs.
		color *= mix(0.9, 1.08, noise(p * 0.11));

		float water = dot(w, vec4(mat_water[m00.x], mat_water[m10.x], mat_water[m01.x], mat_water[m11.x]));
		float deep = dot(w, vec4(equal(ivec4(m00.y, m10.y, m01.y, m11.y), ivec4(2))));
		color = mix(color, deep_water_color, deep * 0.85);
		if (water > 0.0) {
			vec3 n = texture(tex_water_normal, p / 12.0).xyz * 2.0 - 1.0;
			vec3 to_sun = normalize(-sun_direction);
			// Brightness from how far the ripple tilts towards the sun, against flat water.
			float facing = dot(normalize(vec3(n.x, 1.0, n.y)), to_sun) - to_sun.y;
			color *= mix(1.0, clamp(1.0 + facing * 2.5, 0.8, 1.2), water);
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
