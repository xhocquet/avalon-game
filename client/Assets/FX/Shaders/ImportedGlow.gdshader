shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

uniform sampler2D effect_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform vec4 tint : source_color = vec4(1.0);
uniform float intensity : hint_range(0.0, 5.0) = 1.0;
uniform float opacity : hint_range(0.0, 1.0) = 1.0;
uniform bool texture_color = false;
uniform vec2 scroll_speed = vec2(0.0);

void fragment() {
    vec4 sample_color = texture(effect_texture, UV + TIME * scroll_speed);
    float mask = texture_color ? sample_color.a : sample_color.r;
    ALBEDO = (texture_color ? sample_color.rgb : vec3(1.0)) * tint.rgb * intensity;
    ALPHA = mask * tint.a * opacity;
}
