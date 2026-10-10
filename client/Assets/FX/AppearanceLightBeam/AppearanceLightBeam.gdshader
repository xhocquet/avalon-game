shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

uniform sampler2D effect_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform vec4 tint : source_color = vec4(1.0, 0.75, 0.3, 1.0);
uniform float intensity : hint_range(0.0, 5.0) = 1.5;
uniform float opacity : hint_range(0.0, 1.0) = 1.0;
uniform bool face_camera = false;

void vertex() {
	if (face_camera) {
		mat4 facing = mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
		facing[0].xyz *= length(MODEL_MATRIX[0].xyz);
		facing[1].xyz *= length(MODEL_MATRIX[1].xyz);
		facing[2].xyz *= length(MODEL_MATRIX[2].xyz);
		MODELVIEW_MATRIX = VIEW_MATRIX * facing;
	}
}

void fragment() {
	float mask = texture(effect_texture, UV).r;
	if (face_camera) {
		float edge = min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y));
		mask *= smoothstep(0.0, 0.02, edge);
	}
	ALBEDO = tint.rgb * intensity;
	ALPHA = mask * tint.a * opacity;
}
