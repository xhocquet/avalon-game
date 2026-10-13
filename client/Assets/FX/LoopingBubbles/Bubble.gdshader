shader_type spatial;
render_mode blend_mix, cull_back, depth_draw_never;

uniform sampler2D alpha_texture : repeat_disable, filter_linear_mipmap;
uniform vec4 bubble_color : source_color = vec4(0.62, 0.88, 1.0, 0.8);
uniform float opacity : hint_range(0.0, 1.0) = 1.0;
uniform float rim_strength : hint_range(0.0, 2.0) = 0.9;

void fragment() {
    float rim = pow(1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), 2.0);
    float mask = texture(alpha_texture, UV).r;
    ALBEDO = bubble_color.rgb * COLOR.rgb;
    EMISSION = ALBEDO * (0.5 + rim * 1.5);
    ALPHA = mask * (0.15 + rim * rim_strength) * bubble_color.a * COLOR.a * opacity;
    ROUGHNESS = 0.12;
    SPECULAR = 0.8;
}
