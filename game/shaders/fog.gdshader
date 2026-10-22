// The fog of war (docs/implementation/M4_combat.md, 4.15, M4 step 12): one pass over the whole screen after the opaque world. From
// the depth of a pixel it finds its point of the world and reads the fog of that cell, one texel per cell with bilinear filtering,
// so the edges are soft: 1 visible (as it is), 0.5 fog (half as bright and almost grey), 0 shroud (black). The HUD is on its own
// layer and is not touched.
shader_type spatial;
render_mode unshaded, depth_draw_never, depth_test_disabled, cull_disabled, shadows_disabled, fog_disabled;

uniform sampler2D depth_texture : hint_depth_texture, filter_nearest;
uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;
uniform sampler2D fog_map : filter_linear;
uniform vec2 map_size = vec2(1.0);

void vertex() {
	// The quad covers the screen whatever the camera does.
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

void fragment() {
	float depth = texture(depth_texture, SCREEN_UV).x;
	vec4 view = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
	view.xyz /= view.w;
	vec3 world = (INV_VIEW_MATRIX * vec4(view.xyz, 1.0)).xyz;
	float seen = texture(fog_map, world.xz / map_size).r;
	vec3 color = texture(screen_texture, SCREEN_UV).rgb;
	float grey = dot(color, vec3(0.299, 0.587, 0.114));
	vec3 fogged = mix(vec3(grey), color, 0.3) * 0.5;
	ALBEDO = seen >= 0.5 ? mix(fogged, color, (seen - 0.5) * 2.0) : fogged * smoothstep(0.0, 0.5, seen);
}
