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
// A painted material (ClimateLook.cs, the key color): the shader gives its colour, in large soft patches of the second one, and
// the photograph only its light and shade.
uniform float mat_painted[MAX_MATERIALS];
uniform vec3 mat_color[MAX_MATERIALS];
uniform vec3 mat_patches[MAX_MATERIALS];
// The share of the ground under the patches, 0…1.
uniform float mat_patches_share[MAX_MATERIALS];
// The slot of the climate's cliff material, for the walls; -1 without one.
uniform int cliff_slot = -1;
// The rock's grain runs down the wall: the texture is this much longer upright than across.
uniform float cliff_stretch = 2.5;
uniform vec3 frost_color : source_color = vec3(0.88, 0.91, 0.96);
// The markings of the asphalt (RoadMarkings.cs: four bytes a cell of the map, no skirt), their paint, and the slot of the climate's
// grass for the green strip down the middle of a wide road (-1: a plain green).
uniform sampler2D road_map : filter_nearest;
uniform vec3 marking_color : source_color = vec3(0.86, 0.85, 0.78);
uniform int grass_slot = -1;
// The slot of the climate's dirt road, the earth of the ruts worn into the ground (-1: a plain brown).
uniform int rut_slot = -1;
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

// The neighbours of RoadMarkings.Directions, by bit: east first, y (here z) to the south.
const vec2 ROAD_DIRECTIONS[8] = vec2[](vec2(1.0, 0.0), vec2(1.0, 1.0), vec2(0.0, 1.0), vec2(-1.0, 1.0), vec2(-1.0, 0.0),
	vec2(-1.0, -1.0), vec2(0.0, -1.0), vec2(1.0, -1.0));

// 1 on a painted line of half the width half_width at the distance d from its middle, its edge smoothed over one pixel.
float stripe(float d, float half_width) {
	float pixel = fwidth(d);
	return 1.0 - smoothstep(half_width - 0.5 * pixel, half_width + 0.5 * pixel, d);
}

// How far the point is from the segment from a to b.
float segment_distance(vec2 point, vec2 a, vec2 b) {
	vec2 ab = b - a;
	float along = clamp(dot(point - a, ab) / max(dot(ab, ab), 1e-6), 0.0, 1.0);
	return length(point - a - ab * along);
}

// 1 for a cell of asphalt of the map (RoadMarkings 1 to 3), 0 elsewhere — the ruts of a dirt road too — and off the map.
float road_at(ivec2 cell) {
	ivec2 size = textureSize(road_map, 0);
	if (any(lessThan(cell, ivec2(0))) || any(greaterThanEqual(cell, size))) {
		return 0.0;
	}
	float kind = texelFetch(road_map, cell, 0).r * 255.0;
	return step(0.5, kind) * step(kind, 3.5);
}

// The slot (x) and the water (y: 0 none, 1 shallow, 2 deep) of a cell of the material map.
ivec2 material_of(ivec2 cell, ivec2 size) {
	vec2 texel = texelFetch(material_map, clamp(cell, ivec2(0), size - 1), 0).rg;
	return ivec2(floor(texel * 255.0 + 0.5));
}

vec3 albedo_of(int slot, vec2 position, float frost_patches) {
	vec3 photo = texture(ground_albedo, vec3(position / mat_tiling[slot], float(slot))).rgb;
	vec3 albedo = photo * mat_tint[slot];
	if (mat_painted[slot] > 0.5) {
		// The photograph's light and shade about its mean (its smallest mip level, one texel), kept within reason so a bright
		// stone of the photograph does not shine; the colour is the material's, in patches a few cells across.
		vec3 luma = vec3(0.3, 0.59, 0.11);
		float mean = dot(textureLod(ground_albedo, vec3(0.5, 0.5, float(slot)), 16.0).rgb, luma);
		// A little more contrast than the photograph's own: RA2's grass is speckled.
		float shade = clamp(1.0 + 1.3 * (dot(photo, luma) / max(mean, 0.001) - 1.0), 0.4, 1.7);
		// Painted water takes nothing of the photograph: its light comes from the ripple alone (RA2's water is even).
		shade = mat_water[slot] > 0.5 ? 1.0 : shade;
		// Half the ground under the patches at a share of 0.5; a smaller share raises the line the noise must cross and sharpens
		// the edge, so a few patches of grass in the snow have clean borders, as in RA2, instead of a haze.
		float share = mat_patches_share[slot];
		float line = 0.5 + 0.6 * (0.5 - share);
		float edge = clamp(0.4 * share, 0.04, 0.2);
		float blotch = smoothstep(line - edge, line + edge, 0.65 * noise(position * 0.17 + vec2(3.7, 1.9)) + 0.35 * noise(position * 0.43 + vec2(8.1, 5.3)));
		albedo = mix(mat_color[slot], mat_patches[slot], blotch) * shade;
	}
	return mix(albedo, frost_color, mat_frost[slot] * frost_patches);
}

void fragment() {
	ivec2 size = textureSize(land_map, 0);
	ivec2 cell = clamp(ivec2(floor(world_position.xz)), ivec2(0), size - 1);
	bool on_map = all(greaterThanEqual(world_position.xz, vec2(0.0))) && all(lessThan(world_position.xz, vec2(size)));
	vec3 color = fallback_color;
	float roughness = 0.95;
	// How much of the asphalt is here: it is lit by the smooth normal of the mesh, not by its face's.
	float paved_light = 0.0;
	if (use_textures) {
		vec2 p = world_position.xz;
		// The four cells around the point, by their centres; noise bends the borders between them.
		vec2 warp = vec2(noise(p * 1.7), noise(p * 1.7 + vec2(17.3, 9.1))) - 0.5;
		// A road keeps straight edges: by the asphalt the noise hardly bends the borders.
		ivec2 straight = ivec2(floor(p - 0.5));
		float by_road = max(max(road_at(straight), road_at(straight + ivec2(1, 0))), max(road_at(straight + ivec2(0, 1)), road_at(straight + ivec2(1, 1))));
		warp *= mix(1.0, 0.12, by_road);
		vec2 q = p - 0.5 + warp * 0.5;
		ivec2 base = ivec2(floor(q - map_origin));
		vec2 g = smoothstep(0.2, 0.8, fract(q));
		ivec2 materials = textureSize(material_map, 0);
		ivec2 m00 = material_of(base, materials);
		ivec2 m10 = material_of(base + ivec2(1, 0), materials);
		ivec2 m01 = material_of(base + ivec2(0, 1), materials);
		ivec2 m11 = material_of(base + ivec2(1, 1), materials);
		vec4 w = vec4((1.0 - g.x) * (1.0 - g.y), g.x * (1.0 - g.y), (1.0 - g.x) * g.y, g.x * g.y);
		// The marked asphalt of the four cells (RoadMarkings.cs); the road map has no skirt.
		ivec2 corner = base + ivec2(map_origin);
		vec4 paved = vec4(road_at(corner), road_at(corner + ivec2(1, 0)), road_at(corner + ivec2(0, 1)), road_at(corner + ivec2(1, 1)));
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
		// The asphalt is dark but holds its whole cell: it wins the blend as the brightest would.
		vec4 h = w + 0.35 * vec4(dot(a00, luma), dot(a10, luma), dot(a01, luma), dot(a11, luma)) + 0.35 * paved;
		float top = max(max(h.x, h.y), max(h.z, h.w));
		w = max(h - (top - 0.2), vec4(0.0));
		w /= w.x + w.y + w.z + w.w;
		// The shore is rounded (the owner, 2026-10-30: land does not go into the water in squares): the water of the four cells
		// blended straight across, without the plateaus of g, is a smooth field, and the shore is where it crosses one half — a
		// staircase of cells becomes a slanting line, the corner of a beach an arc, and a cell of the shore is land in part only.
		// Its centre stays what it is: the rules see whole cells. Land and water each blend among their own cells.
		vec4 wet = vec4(mat_water[m00.x], mat_water[m10.x], mat_water[m01.x], mat_water[m11.x]);
		vec2 f = fract(q);
		float level = dot(vec4((1.0 - f.x) * (1.0 - f.y), f.x * (1.0 - f.y), (1.0 - f.x) * f.y, f.x * f.y), wet);
		level += (noise(p * 3.1 + vec2(5.3, 2.9)) - 0.5) * 0.12 * step(0.01, level) * step(level, 0.99);
		float water = smoothstep(0.46, 0.54, level);
		// A little of the plain blend in each half, so a half the height blend left empty still has a colour.
		vec4 w_land = (w + 0.001 * w_bilinear) * (1.0 - wet);
		vec4 w_water = (w + 0.001 * w_bilinear) * wet;
		w_land /= max(w_land.x + w_land.y + w_land.z + w_land.w, 1e-6);
		w_water /= max(w_water.x + w_water.y + w_water.z + w_water.w, 1e-6);
		w = mix(w_land, w_water, water);
		color = a00 * w.x + a10 * w.y + a01 * w.z + a11 * w.w;
		roughness = dot(w, vec4(mat_roughness[m00.x], mat_roughness[m10.x], mat_roughness[m01.x], mat_roughness[m11.x]));
		// The wet sand just above the water line.
		color *= 1.0 - 0.14 * smoothstep(0.2, 0.46, level) * (1.0 - water);
		// Large soft patches against the tiling of the photographs.
		color *= mix(0.9, 1.08, noise(p * 0.11));

		// The deep water over the plain blend of the cells, so shallow and deep cells side by side fade into each other instead
		// of showing as blotches.
		float deep = dot(w_bilinear, vec4(equal(ivec4(m00.y, m10.y, m01.y, m11.y), ivec4(2)))) * water;
		color = mix(color, deep_water_color, deep * 0.85);
		float painted_water = dot(w, vec4(mat_painted[m00.x] * mat_water[m00.x], mat_painted[m10.x] * mat_water[m10.x],
			mat_painted[m01.x] * mat_water[m01.x], mat_painted[m11.x] * mat_water[m11.x]));
		if (water > 0.0) {
			// Two layers of the ripple drift in different directions and scales, so the glints move and do not repeat at once.
			vec3 n1 = texture(tex_water_normal, p / 12.0 + TIME * vec2(0.02, 0.011)).xyz * 2.0 - 1.0;
			vec3 n2 = texture(tex_water_normal, p / 7.0 - TIME * vec2(0.014, 0.022) + vec2(0.37, 0.61)).xyz * 2.0 - 1.0;
			vec2 tilt = 0.5 * (n1.xy + n2.xy);
			if (painted_water > 0.0) {
				// Painted water (RA2's) has small waves: three layers of the ripple a cell or two across, turned against each other
				// and at scales that never line up, so neither blotches nor the tiles of the ripple show.
				vec2 a = mat2(vec2(0.80, 0.60), vec2(-0.60, 0.80)) * p;
				vec2 b = mat2(vec2(0.39, -0.92), vec2(0.92, 0.39)) * p;
				vec3 s1 = texture(tex_water_normal, a * 0.43 + TIME * vec2(0.05, 0.03)).xyz * 2.0 - 1.0;
				vec3 s2 = texture(tex_water_normal, b * 0.71 - TIME * vec2(0.04, 0.06) + vec2(0.37, 0.61)).xyz * 2.0 - 1.0;
				vec3 s3 = texture(tex_water_normal, p * 0.29 + TIME * vec2(-0.03, 0.02) + vec2(0.71, 0.13)).xyz * 2.0 - 1.0;
				tilt = mix(tilt, (s1.xy + s2.xy + s3.xy) / 3.0, painted_water);
			}
			vec3 to_sun = normalize(-sun_direction);
			// Brightness from how far the ripple tilts towards the sun, against flat water.
			float facing = dot(normalize(vec3(tilt.x, 1.0, tilt.y)), to_sun) - to_sun.y;
			float swing = mix(0.2, 0.07, painted_water);
			color *= mix(1.0, clamp(1.0 + facing * 2.5, 1.0 - swing, 1.0 + swing), water);

			// Foam along the shore: a band on the water's side of the line where the water field crosses one half.
			float shore = smoothstep(0.48, 0.54, level) * (1.0 - smoothstep(0.58, 0.8, level));
			float churn = noise(p * 2.2 + TIME * vec2(0.20, -0.12)) * 0.6 + noise(p * 4.6 - TIME * vec2(0.31, 0.26)) * 0.4;
			color = mix(color, foam_color, shore * smoothstep(0.45, 0.66, churn) * 0.9);

			// Light moving over the bottom of the shallow part of the water.
			float shallow = clamp(water - deep, 0.0, 1.0);
			// Painted water (RA2's) keeps a faint trace of it only.
			color += caustic_strength * mix(1.0, 0.3, painted_water) * caustic(p * 1.3, TIME) * shallow * vec3(0.8, 0.95, 1.0);
		}

		// The markings of the asphalt (RoadMarkings.cs; the owner, 2026-10-30, as in RA2): by the cell itself, faded out where the
		// blend of the cells shows another ground. A road one cell wide has a dashed line along its middle, half a cell of paint
		// across each border with its next cell; a band solid lines along its edges, and from three cells a narrow green strip down
		// the middle with a dashed line in each half, from two a dashed line between its lanes.
		if (on_map) {
			float on_road = smoothstep(0.55, 0.85, dot(w, paved));
			paved_light = smoothstep(0.2, 0.6, dot(w, paved));
			vec4 mark = floor(texelFetch(road_map, cell, 0) * 255.0 + 0.5);
			float stroke = 0.024;
			float paint = 0.0;
			float strip = 0.0;
			if (on_road > 0.0 && mark.r == 1.0) {
				vec2 local = fract(p) - 0.5;
				int links = int(mark.g);
				for (int bit = 0; bit < 8; bit++) {
					if (((links >> bit) & 1) == 1) {
						vec2 towards = normalize(ROAD_DIRECTIONS[bit]);
						float reach = length(ROAD_DIRECTIONS[bit]) * 0.5;
						float t = dot(local, towards);
						float off = length(local - t * towards);
						paint = max(paint, step(reach * 0.5, t) * step(t, reach) * stripe(off, stroke));
					}
				}
			} else if (on_road > 0.0 && mark.r == 2.0) {
				bool by_x = mark.g == 0.0;
				float u = mark.b + (by_x ? fract(p.y) : fract(p.x));
				float along = by_x ? p.x : p.y;
				float width = mark.a;
				float half_width = width * 0.5;
				float dashes = step(fract(along), 0.5);
				float edge = min(abs(u - 0.25), abs(u - (width - 0.25)));
				paint = stripe(edge, stroke);
				if (width >= 3.0) {
					float middle = abs(u - half_width);
					strip = stripe(middle, 0.12);
					paint = max(paint, 0.6 * stripe(abs(middle - 0.14), 0.01));
					float lane = 0.25 + (half_width - 0.42) * 0.5;
					paint = max(paint, dashes * stripe(min(abs(u - lane), abs(u - (width - lane))), stroke));
				} else {
					paint = max(paint, dashes * stripe(abs(u - half_width), stroke));
				}
			}
			if (mark.r == 4.0) {
				// A dirt road is the ground with two ruts worn into it and its middle a little trodden, a path one narrow trodden
				// line (the owner, 2026-10-30). The ruts follow the line from the cell's first neighbour through its centre to its
				// second, each offset to one side, and turn where the two offset lines meet, so a bend has no crossing ruts; they
				// wander a little and are broken here and there.
				vec2 local = fract(p) - 0.5 + (noise(p * 2.3 + vec2(4.1, 7.7)) - 0.5) * 0.06;
				int links = int(mark.g);
				int first = -1;
				int second = -1;
				for (int bit = 0; bit < 8; bit++) {
					if (((links >> bit) & 1) == 1) {
						if (first < 0) {
							first = bit;
						} else {
							second = bit;
						}
					}
				}
				vec2 a = normalize(ROAD_DIRECTIONS[first]);
				vec2 end_a = ROAD_DIRECTIONS[first] * 0.5;
				vec2 b = second >= 0 ? normalize(ROAD_DIRECTIONS[second]) : -a;
				vec2 end_b = second >= 0 ? ROAD_DIRECTIONS[second] * 0.5 : vec2(0.0);
				float worn = max(stripe(segment_distance(local, vec2(0.0), end_a), 0.24), stripe(segment_distance(local, vec2(0.0), end_b), 0.24));
				float rut = 0.0;
				if (mark.b == 1.0) {
					rut = max(stripe(segment_distance(local, vec2(0.0), end_a), 0.045), stripe(segment_distance(local, vec2(0.0), end_b), 0.045));
					worn = 0.0;
				} else {
					for (int k = 0; k < 2; k++) {
						float off = k == 0 ? 0.17 : -0.17;
						vec2 side_a = off * vec2(a.y, -a.x);
						vec2 side_b = off * vec2(-b.y, b.x);
						float turn = -a.x * b.y + a.y * b.x;
						vec2 meet = abs(turn) < 0.001 ? side_a
							: side_a - a * (((side_b - side_a).x * b.y - (side_b - side_a).y * b.x) / turn);
						rut = max(rut, stripe(segment_distance(local, meet, end_a + side_a), 0.045));
						if (second >= 0) {
							rut = max(rut, stripe(segment_distance(local, meet, end_b + side_b), 0.045));
						}
					}
				}
				vec3 earth = rut_slot >= 0 ? albedo_of(rut_slot, p, frost_patches) : vec3(0.42, 0.3, 0.2);
				color = mix(color, mix(color, earth, 0.5), worn * 0.35);
				color = mix(color, earth * 0.7, rut * mix(0.6, 1.0, noise(p * 4.3)));
			}
			if (strip > 0.0) {
				vec3 median = grass_slot >= 0 ? albedo_of(grass_slot, p, frost_patches) : vec3(0.32, 0.42, 0.2);
				color = mix(color, median, strip * on_road);
			}
			color = mix(color, marking_color * mix(0.85, 1.0, noise(p * 6.0)), paint * on_road * 0.9);
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
			vec3 grain = along_x * side.y + along_z * side.x;
			vec3 rock = grain * mat_tint[cliff_slot];
			if (mat_painted[cliff_slot] > 0.5) {
				// A painted rock (the desert's orange sandstone, RA2) keeps only the photograph's crevices and ledges.
				vec3 luma = vec3(0.3, 0.59, 0.11);
				float mean = dot(textureLod(ground_albedo, vec3(0.5, 0.5, float(cliff_slot)), 16.0).rgb, luma);
				rock = mat_color[cliff_slot] * clamp(1.0 + 1.3 * (dot(grain, luma) / max(mean, 0.001) - 1.0), 0.35, 1.8);
			}
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
	// Every face by its own normal, so each plane of the relief reads apart; the asphalt by the smooth one (the owner, 2026-10-30:
	// a road goes up and down a hill as an even slope, without the bumps of a dirt road).
	NORMAL = normalize(mix(normalize(cross(dFdy(VERTEX), dFdx(VERTEX))), normalize(NORMAL), paved_light));
	ALBEDO = color;
	ROUGHNESS = roughness;
}
