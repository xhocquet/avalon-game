shader_type spatial;
render_mode cull_disabled;

uniform vec3 hair_color : source_color = vec3(0.16, 0.085, 0.035);
uniform float lock_length = 5.5;
uniform float lock_width = 1.0;
uniform float motion = 0.65;
uniform float phase = 0.0;

vec2 bend(float t) {
    float envelope = sin(t * 3.14159265);
    float clock = TIME * 2.4 + phase;
    return vec2(
        sin(t * 6.5 - clock) * 0.045 + sin(t * 11.0 + clock * 0.7) * 0.015,
        cos(t * 5.0 - clock * 0.8) * 0.032
    ) * envelope * lock_length * motion;
}

void vertex() {
    float t = VERTEX.z;
    vec2 slope = (bend(t + 0.001) - bend(t - 0.001)) / (0.002 * lock_length);
    NORMAL = normalize(vec3(NORMAL.xy / lock_width, NORMAL.z / lock_length - dot(NORMAL.xy / lock_width, slope)));
    VERTEX.xy *= lock_width;
    VERTEX.xy += bend(t);
    VERTEX.z *= lock_length;
}

void fragment() {
    float seed = COLOR.r * 18.0;
    float across = UV.x + sin(UV.y * lock_length * 1.8 + seed) * 0.012;
    float fibers = 0.5 + 0.5 * sin(across * 100.53096 + seed * 3.0);
    float grooves = pow(0.5 + 0.5 * sin(across * 37.69911 + seed), 12.0);
    float variation = 0.82 + 0.28 * fract(sin(seed * 127.1) * 43758.5453);
    ALBEDO = hair_color * variation * mix(0.75, 1.3, fibers) * (1.0 - grooves * 0.48);
    ROUGHNESS = 0.44 + fibers * 0.17;
    SPECULAR = 0.38;
}
