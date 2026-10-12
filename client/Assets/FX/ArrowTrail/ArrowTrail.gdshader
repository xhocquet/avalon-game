shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

uniform vec4 tint : source_color = vec4(0.15, 0.8, 1.0, 1.0);
uniform float intensity : hint_range(0.0, 5.0) = 1.5;
uniform float opacity : hint_range(0.0, 1.0) = 1.0;

void fragment() {
    ALBEDO = tint.rgb * intensity;
    ALPHA = tint.a * opacity;
}
