shader_type canvas_item;
render_mode unshaded;

uniform sampler2D source_mask : repeat_disable, filter_linear;
uniform vec2 direction = vec2(1.0, 0.0);

void fragment() {
    vec2 step_uv = direction / vec2(textureSize(source_mask, 0));
    float value = texture(source_mask, UV).r * 0.22702703;
    value += texture(source_mask, UV + step_uv * 1.38461538).r * 0.31621622;
    value += texture(source_mask, UV - step_uv * 1.38461538).r * 0.31621622;
    value += texture(source_mask, UV + step_uv * 3.23076923).r * 0.07027027;
    value += texture(source_mask, UV - step_uv * 3.23076923).r * 0.07027027;
    COLOR = vec4(vec3(value), 1.0);
}
