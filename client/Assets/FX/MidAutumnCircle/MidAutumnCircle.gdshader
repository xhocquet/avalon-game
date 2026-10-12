shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

uniform sampler2D effect_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform vec4 tint : source_color = vec4(1.0, 0.7, 0.25, 1.0);
uniform float intensity : hint_range(0.0, 5.0) = 1.3;
uniform float opacity : hint_range(0.0, 1.0) = 1.0;
uniform vec4 texture_region = vec4(0.0, 0.0, 1.0, 1.0);
uniform vec2 scroll_speed = vec2(0.0);
uniform bool generated_plane_uv = false;
uniform bool face_camera = false;
uniform bool wall = false;

void vertex() {
    if (generated_plane_uv) {
        UV = VERTEX.xy + vec2(0.5);
    }
    if (face_camera) {
        mat4 facing = mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
        facing[0].xyz *= length(MODEL_MATRIX[0].xyz);
        facing[1].xyz *= length(MODEL_MATRIX[1].xyz);
        facing[2].xyz *= length(MODEL_MATRIX[2].xyz);
        MODELVIEW_MATRIX = VIEW_MATRIX * facing;
    }
}

void fragment() {
    vec2 uv = UV;
    uv.x += TIME * scroll_speed.x;
    if (wall) {
        uv.x = fract(uv.x);
    }
    uv = texture_region.xy + uv * texture_region.zw;
    vec4 sample_color = texture(effect_texture, uv);
    float mask = sample_color.r * sample_color.a;
    if (wall) {
        mask *= sin(clamp(UV.y, 0.0, 1.0) * 3.14159265);
    } else {
        float edge = min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y));
        mask *= smoothstep(0.0, 0.025, edge);
    }
    ALBEDO = tint.rgb * intensity;
    ALPHA = mask * tint.a * opacity;
}
