// BotW-style explosion — ported to Godot 4 from
// https://github.com/nekotogd/Godot_BotW_Explosion (CC0).
// Meant to be used as the material on a GPUParticles3D draw-pass quad:
// it manually billboards each particle and rotates it by INSTANCE_CUSTOM.x.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, unshaded;

uniform vec4 fire_color : source_color = vec4(0.99, 0.31, 0.01, 1.0);
uniform float fire_strength = 4.3;
uniform float warble_strength = 0.2;
uniform float time_scale = 1.0;
uniform float alpha_scissor = 0.23;
uniform sampler2D red_texture : source_color;
uniform sampler2D green_texture : source_color;
uniform sampler2D blue_texture : source_color;
uniform sampler2D noise_texture : source_color;

vec3 lerp(vec3 a, vec3 b, float t) {
	return a * (1.0 - t) + b * t;
}

void vertex() {
	// Billboard toward the camera while preserving the instance's world scale,
	// then spin around view-Z by the per-particle angle in INSTANCE_CUSTOM.x.
	mat4 mat_world = mat4(
		normalize(INV_VIEW_MATRIX[0]) * length(MODEL_MATRIX[0]),
		normalize(INV_VIEW_MATRIX[1]) * length(MODEL_MATRIX[0]),
		normalize(INV_VIEW_MATRIX[2]) * length(MODEL_MATRIX[2]),
		MODEL_MATRIX[3]);
	mat_world = mat_world * mat4(
		vec4(cos(INSTANCE_CUSTOM.x), -sin(INSTANCE_CUSTOM.x), 0.0, 0.0),
		vec4(sin(INSTANCE_CUSTOM.x), cos(INSTANCE_CUSTOM.x), 0.0, 0.0),
		vec4(0.0, 0.0, 1.0, 0.0),
		vec4(0.0, 0.0, 0.0, 1.0));
	MODELVIEW_MATRIX = VIEW_MATRIX * mat_world;
}

void fragment() {
	vec2 base_uv = UV;
	vec3 r = texture(red_texture, base_uv).rgb;
	vec3 g = texture(green_texture, base_uv).rgb;
	vec3 b = texture(blue_texture, base_uv).rgb;
	ALPHA = clamp(b.b * COLOR.a, 0.0, 1.0) + texture(noise_texture, UV + TIME * time_scale).r * warble_strength;
	ALPHA_SCISSOR_THRESHOLD = alpha_scissor;
	ALBEDO = lerp(g * fire_color.rgb * fire_strength, r, COLOR.r);
}
