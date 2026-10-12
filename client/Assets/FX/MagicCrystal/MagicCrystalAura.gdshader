shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

uniform vec4 tint : source_color = vec4(0.4, 0.1, 1.0, 1.0);
uniform float intensity : hint_range(0.0, 5.0) = 1.0;
uniform float opacity : hint_range(0.0, 1.0) = 0.25;
uniform bool sphere = false;

void fragment() {
    float mask = 1.0;
    if (sphere) {
        mask = pow(1.0 - abs(dot(normalize(NORMAL), normalize(VIEW))), 3.0);
    }
    ALBEDO = tint.rgb * intensity;
    ALPHA = mask * tint.a * opacity;
}
