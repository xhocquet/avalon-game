shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

uniform vec4 tint : source_color = vec4(0.05, 0.65, 1.0, 1.0);
uniform float opacity : hint_range(0.0, 1.0) = 1.0;
uniform float phase = 0.0;
uniform float thickness : hint_range(0.0, 0.05) = 0.01;

void vertex() {
	VERTEX += NORMAL * thickness;
}

void fragment() {
	float pulse = 0.75 + 0.25 * sin(TIME * 73.0 + phase);
	ALBEDO = tint.rgb * 2.0;
	ALPHA = tint.a * opacity * pulse;
}
