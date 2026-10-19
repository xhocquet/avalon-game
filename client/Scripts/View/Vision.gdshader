shader_type spatial;
render_mode blend_mul, unshaded, fog_disabled, depth_draw_never, depth_test_disabled, cull_disabled;

uniform sampler2D depth_texture : hint_depth_texture, repeat_disable, filter_nearest;
uniform sampler2D vision_texture : repeat_disable, filter_linear;
uniform vec2 vision_origin;
uniform vec2 vision_size;
uniform float hidden_brightness : hint_range(0.0, 1.0) = 0.25;

void vertex() {
    POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

void fragment() {
    float depth = texture(depth_texture, SCREEN_UV).r;
#if CURRENT_RENDERER == RENDERER_COMPATIBILITY
    vec3 ndc = vec3(SCREEN_UV, depth) * 2.0 - 1.0;
#else
    vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, depth);
#endif
    vec4 view_position = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
    view_position /= view_position.w;
    vec3 world_position = (INV_VIEW_MATRIX * view_position).xyz;
    vec2 uv = (world_position.xz - vision_origin) / vision_size;
    float in_bounds = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
    float vision = texture(vision_texture, clamp(uv, vec2(0.0), vec2(1.0))).r * in_bounds;
    float brightness = mix(hidden_brightness, 1.0, vision);
    ALBEDO = vec3(brightness);
    ALPHA = 1.0;
}
