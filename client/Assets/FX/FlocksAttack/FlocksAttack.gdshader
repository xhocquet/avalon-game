shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

uniform vec4 tint : source_color = vec4(0.15, 0.4, 1.0, 0.3);
uniform float opacity : hint_range(0.0, 1.0) = 1.0;
uniform float intensity : hint_range(0.0, 5.0) = 1.2;
varying vec3 local_position;

void vertex() {
    local_position = VERTEX;
}

void fragment() {
    float pulse = 0.8 + 0.2 * sin(local_position.z * 12.0 + TIME * 8.0);
    float rim = pow(1.0 - abs(dot(normalize(NORMAL), normalize(VIEW))), 2.0);
    ALBEDO = mix(tint.rgb, vec3(0.35, 0.65, 1.0), rim * 0.3) * intensity * pulse;
    ALPHA = tint.a * opacity;
}
