shader_type spatial;

uniform vec4 aoe_color : source_color = vec4(1.0, 0.0, 0.0, 0.3);
uniform float width : hint_range(0.001, 0.1, 0.001) = 0.01;
uniform float fill_value : hint_range(0.0, 1.0, 0.1) = 0.5;

void fragment() {
	float d = distance(UV, vec2(0.5));
	ALPHA = 0.0;

	// External circle
	if (d >= 0.5 - 2.0* width && d <= 0.5)
		ALPHA = 1.0 * aoe_color.a;

	// Internal circle
	if (d < fill_value * 0.5)
		ALPHA = 1.0 * aoe_color.a;

	ALBEDO = aoe_color.xyz;
}
