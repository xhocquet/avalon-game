shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

uniform float opacity : hint_range(0.0, 1.0) = 1.0;
uniform float intensity : hint_range(0.0, 5.0) = 1.5;
uniform bool shell = false;
varying vec3 local_position;

float hash(vec3 p) {
    return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453);
}

float noise(vec3 p) {
    vec3 cell = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(mix(hash(cell), hash(cell + vec3(1, 0, 0)), f.x),
                   mix(hash(cell + vec3(0, 1, 0)), hash(cell + vec3(1, 1, 0)), f.x), f.y),
               mix(mix(hash(cell + vec3(0, 0, 1)), hash(cell + vec3(1, 0, 1)), f.x),
                   mix(hash(cell + vec3(0, 1, 1)), hash(cell + vec3(1, 1, 1)), f.x), f.y), f.z);
}

void vertex() {
    local_position = VERTEX;
    if (shell) {
        float flicker = noise(VERTEX * 8.0 - vec3(0.0, TIME * 3.0, 0.0));
        VERTEX += NORMAL * flicker * 0.12;
        VERTEX.y += max(VERTEX.y, 0.0) * flicker * 0.6;
    }
}

void fragment() {
    vec3 p = local_position * 7.0 - vec3(0.0, TIME * 3.0, 0.0);
    float heat = noise(p) * 0.7 + noise(p * 2.1) * 0.3;
    vec3 fire = mix(vec3(1.0, 0.06, 0.005), vec3(1.0, 0.75, 0.12), smoothstep(0.25, 0.8, heat));
    ALBEDO = fire * intensity;
    ALPHA = opacity * (shell ? smoothstep(0.3, 0.7, heat) * 0.65 : 0.85);
}
