// Ground of the terrain (docs/implementation/M7_maps.md, 4.12). With the textures of the climate, every cell takes the material
// of the map's texture layer (MaterialSlots.cs: the slot of its material and its water, two bytes a cell): the albedo comes from
// one layer of ground_albedo, tiled, tinted and rough by the material's entry of the arrays below. Neighbouring materials blend
// over a border that noise bends and the brighter texture wins (a height blend), so the cells never show as squares; water gets a
// ripple that drifts and is lit by the sun, foam along the shore, light moving over shallow water, and fades to the deep colour on
// deep water. The material map covers the skirt too (MapSkirt.cs: the land
// beyond the edge), from map_origin; the lines and the debug overlay stay on the map. While the textures are off, every cell takes the flat colour
// of its land type from the simulation map (one texel per cell, cliffs in rock), with a faint line every cell, a stronger line
// every 8 cells and a line at each height level: the symbolic look of docs/implementation/conventions.md, section 12. M7 step 9:
// the normal is the face's own, so every plane of the relief reads apart and a cliff is a wall. The debug cell overlay (F7, M2
// step 14) blends one RGBA texel per cell over the ground in both modes; bucket_lines adds the 4x4 index grid. The walls of
// the cliffs (TerrainHeights.cs: a cliff cell is a steep wall between its two levels) take the climate's cliff material from
// the side wherever the face is steep, projected on the planes that stand upright (triplanar without the top), so the rock is
// not stretched down the wall; above and below the wall the cell keeps its own ground.
shader_type spatial;

const int MAX_MATERIALS = 16;

uniform sampler2D land_map : source_color, filter_nearest;
uniform vec3 fallback_color : source_color = vec3(0.34, 0.36, 0.32);
uniform sampler2D material_map : filter_nearest;
// The world cell of the material map's first texel: the north-west corner of the skirt.
uniform vec2 map_origin = vec2(0.0);
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
// The slot of the climate's cliff material, for the walls; -1 without one.
uniform int cliff_slot = -1;
// The rock's grain runs down the wall: the texture is this much longer upright than across.
uniform float cliff_stretch = 2.5;
uniform vec3 frost_color : source_color = vec3(0.88, 0.91, 0.96);
uniform sampler2D tex_water_normal : filter_linear_mipmap, repeat_enable;
uniform vec3 deep_water_color : source_color = vec3(0.10, 0.24, 0.56);
// The foam along the shore, and how much light moves over shallow water.
uniform vec3 foam_color : source_color = vec3(0.90, 0.94, 0.96);
uniform float caustic_strength = 0.14;
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

// Light moving over the bottom of shallow water: a web of bright lines along the borders of cells whose points drift.
float caustic(vec2 p, float t) {
	// Slow waves bend the cells, so the lines of light curve the way they do on a real surface.
	p += 0.35 * vec2(sin(p.y * 1.3 + t * 0.7), cos(p.x * 1.1 - t * 0.5));
	vec2 i = floor(p);
	vec2 f = fract(p);
	float d1 = 8.0;
	float d2 = 8.0;
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			vec2 cell = vec2(float(x), float(y));
			vec2 site = cell + 0.5 + 0.4 * sin(t * 0.8 + 6.2831 * hash(i + cell) + vec2(0.0, 1.57));
			float d = length(site - f);
			d2 = d < d1 ? d1 : min(d2, d);
			d1 = min(d1, d);
		}
	}
	return 1.0 - smoothstep(0.0, 0.1, d2 - d1);
}

// The slot (x) and the water (y: 0 none, 1 shallow, 2 deep) of a cell of the material map.
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
	bool on_map = all(greaterThanEqual(world_position.xz, vec2(0.0))) && all(lessThan(world_position.xz, vec2(size)));
	vec3 color = fallback_color;
	float roughness = 0.95;
	if (use_textures) {
		vec2 p = world_position.xz;
		// The four cells around the point, by their centres; noise bends the borders between them.
		vec2 warp = vec2(noise(p * 1.7), noise(p * 1.7 + vec2(17.3, 9.1))) - 0.5;
		vec2 q = p - 0.5 + warp * 0.5;
		ivec2 base = ivec2(floor(q - map_origin));
		vec2 g = smoothstep(0.2, 0.8, fract(q));
		ivec2 materials = textureSize(material_map, 0);
		ivec2 m00 = material_of(base, materials);
		ivec2 m10 = material_of(base + ivec2(1, 0), materials);
		ivec2 m01 = material_of(base + ivec2(0, 1), materials);
		ivec2 m11 = material_of(base + ivec2(1, 1), materials);
		vec4 w = vec4((1.0 - g.x) * (1.0 - g.y), g.x * (1.0 - g.y), (1.0 - g.x) * g.y, g.x * g.y);
		// The plain blend across the cell edges, before the height blend sharpens it: the shore is soft here.
		vec4 w_bilinear = w;
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
			// Two layers of the ripple drift in different directions and scales, so the glints move and do not repeat at once.
			vec3 n1 = texture(tex_water_normal, p / 12.0 + TIME * vec2(0.02, 0.011)).xyz * 2.0 - 1.0;
			vec3 n2 = texture(tex_water_normal, p / 7.0 - TIME * vec2(0.014, 0.022) + vec2(0.37, 0.61)).xyz * 2.0 - 1.0;
			vec2 tilt = 0.5 * (n1.xy + n2.xy);
			vec3 to_sun = normalize(-sun_direction);
			// Brightness from how far the ripple tilts towards the sun, against flat water.
			float facing = dot(normalize(vec3(tilt.x, 1.0, tilt.y)), to_sun) - to_sun.y;
			color *= mix(1.0, clamp(1.0 + facing * 2.5, 0.8, 1.2), water);

			// Foam along the shore: the water fraction falls from 1 to 0 over the cell edge, so the foam sits where it is near half.
			float edge = dot(w_bilinear, vec4(mat_water[m00.x], mat_water[m10.x], mat_water[m01.x], mat_water[m11.x]));
			float shore = smoothstep(0.1, 0.5, edge) * (1.0 - smoothstep(0.5, 0.9, edge));
			float churn = noise(p * 2.2 + TIME * vec2(0.20, -0.12)) * 0.6 + noise(p * 4.6 - TIME * vec2(0.31, 0.26)) * 0.4;
			color = mix(color, foam_color, shore * smoothstep(0.45, 0.66, churn) * 0.9);

			// Light moving over the bottom of the shallow part of the water.
			float shallow = clamp(water - deep, 0.0, 1.0);
			color += caustic_strength * caustic(p * 1.3, TIME) * shallow * vec3(0.8, 0.95, 1.0);
		}

		// The walls of the cliffs: rock from the side on every steep face, less of it as the face levels out.
		vec3 face = normalize(cross(dFdy(world_position), dFdx(world_position)));
		float steep = smoothstep(0.25, 0.5, 1.0 - abs(face.y));
		if (cliff_slot >= 0 && steep > 0.0) {
			vec2 side = abs(face.xz) + 0.001;
			side /= side.x + side.y;
			float tiling = mat_tiling[cliff_slot];
			vec3 along_x = texture(ground_albedo, vec3(vec2(world_position.x, world_position.y / cliff_stretch) / tiling, float(cliff_slot))).rgb;
			vec3 along_z = texture(ground_albedo, vec3(vec2(world_position.z, world_position.y / cliff_stretch) / tiling, float(cliff_slot))).rgb;
			vec3 rock = (along_x * side.y + along_z * side.x) * mat_tint[cliff_slot];
			color = mix(color, rock, steep);
			roughness = mix(roughness, mat_roughness[cliff_slot], steep);
		}
	} else if (size.x > 1) {
		color = texelFetch(land_map, cell, 0).rgb;
	}
	if (overlay_enabled && on_map) {
		ivec2 overlay_size = textureSize(overlay_map, 0);
		ivec2 overlay_cell = clamp(ivec2(floor(world_position.xz)), ivec2(0), overlay_size - 1);
		vec4 overlay = texelFetch(overlay_map, overlay_cell, 0);
		color = mix(color, overlay.rgb, overlay.a);
	}
	if (!use_textures && on_map) {
		color *= mix(0.85, 1.0, grid(world_position.xz, 1.0));
	}
	if (bucket_lines && on_map) {
		color *= mix(0.35, 1.0, grid(world_position.xz, 4.0));
	}
	if (!use_textures && on_map) {
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
