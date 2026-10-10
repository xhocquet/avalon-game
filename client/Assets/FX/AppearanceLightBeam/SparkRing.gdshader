shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

uniform sampler2D effect_texture : source_color, filter_linear_mipmap, repeat_disable;
uniform vec4 tint : source_color = vec4(0.7, 0.9, 1.0, 1.0);
uniform float intensity : hint_range(0.0, 5.0) = 1.8;
uniform float opacity : hint_range(0.0, 1.0) = 1.0;

void fragment() {
	float mask = texture(effect_texture, UV).r;
	float edge = min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y));
	mask *= smoothstep(0.0, 0.02, edge);
	ALBEDO = tint.rgb * intensity;
	ALPHA = mask * tint.a * opacity;
}
