shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

uniform vec4 tint : source_color = vec4(0.1, 0.7, 1.0, 1.0);
uniform float intensity : hint_range(0.0, 5.0) = 1.3;
uniform float radius = 0.4;
uniform float width = 0.008;
uniform float segments = 16.0;
uniform float speed = 0.3;
uniform bool wall = false;

void fragment() {
    vec2 p = UV - vec2(0.5);
    float r = length(p);
    float angle = atan(p.y, p.x);
    float phase = fract((angle / 6.2831853 + TIME * speed) * segments);
    float arcs = smoothstep(0.04, 0.1, phase) * (1.0 - smoothstep(0.7, 0.8, phase));
    float ring = 1.0 - smoothstep(width, width * 2.0, abs(r - radius));
    float trim = 1.0 - smoothstep(width * 0.4, width, abs(r - radius + 0.035));
    float mask = max(ring * arcs, trim * 0.45);
    if (wall) {
        mask = (0.45 + 0.55 * step(0.25, fract((UV.x + TIME * speed) * segments))) * sin(UV.y * 3.14159265);
    }
    ALBEDO = tint.rgb * intensity;
    ALPHA = mask * tint.a;
}
