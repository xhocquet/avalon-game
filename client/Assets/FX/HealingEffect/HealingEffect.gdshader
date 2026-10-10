shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

uniform sampler2D gradient_lines : source_color, filter_linear_mipmap, repeat_enable;
uniform vec4 tint : source_color = vec4(0.3, 1.0, 0.45, 1.0);
uniform float intensity : hint_range(0.0, 5.0) = 1.5;

void fragment() {
	float mask = texture(gradient_lines, UV).r;
	ALBEDO = tint.rgb * intensity;
	ALPHA = mask * tint.a;
}
