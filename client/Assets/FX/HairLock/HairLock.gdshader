shader_type spatial;
render_mode cull_disabled;

uniform vec3 hair_color : source_color = vec3(0.16, 0.085, 0.035);
uniform float lock_length = 5.5;
uniform float lock_width = 1.0;
uniform float motion = 0.65;
uniform float messiness = 0.0;
uniform float motion_speed = 1.0;
uniform float roll_speed = 0.0;
uniform float radial_spread = 0.0;
uniform float phase = 0.0;

vec2 bend(float t, float seed) {
    float envelope = sin(t * 3.14159265);
    float clock = TIME * 2.4 * motion_speed + phase;
    vec2 coherent = vec2(
        sin(t * 6.5 - clock) * 0.045 + sin(t * 11.0 + clock * 0.7) * 0.015,
        cos(t * 5.0 - clock * 0.8) * 0.032
    );
    float strand_phase = seed * 2.399963;
    float frequency = 8.0 + 4.0 * sin(strand_phase * 1.7);
    float speed = 0.8 + 0.35 * cos(strand_phase * 2.3);
    vec2 loose = vec2(
        sin(t * frequency - clock * speed + strand_phase) * 0.065
            + sin(t * 19.0 + clock * 1.4 + strand_phase * 2.0) * 0.018,
        cos(t * (frequency + 2.0) - clock * speed * 1.3 + strand_phase) * 0.055
            + sin(t * 15.0 - clock * 1.7 + strand_phase) * 0.018
    );
    float flyaway = mix(1.0, 1.45, step(10.5, seed));
    vec2 offset = (coherent + loose * messiness * flyaway) * envelope * lock_length * motion;
    if (radial_spread > 0.0) {
        offset.y = offset.y * 0.3 + envelope * lock_length * 0.08;
    }
    return offset;
}

void vertex() {
    float t = VERTEX.z;
    float seed = COLOR.r * 18.0;
    vec2 slope = (bend(t + 0.001, seed) - bend(t - 0.001, seed)) / (0.002 * lock_length);
    NORMAL = normalize(vec3(NORMAL.xy / lock_width, NORMAL.z / lock_length - dot(NORMAL.xy / lock_width, slope)));
    VERTEX.xy *= lock_width;
    VERTEX.xy += bend(t, seed);
    VERTEX.z *= lock_length;
    float roll = TIME * roll_speed;
    mat2 rotation = mat2(vec2(cos(roll), sin(roll)), vec2(-sin(roll), cos(roll)));
    VERTEX.xy = rotation * VERTEX.xy;
    NORMAL.xy = rotation * NORMAL.xy;
    if (radial_spread > 0.0) {
        float strand = mod(round(seed) * 7.0, 18.0); // Interleave thick strands and flyaways.
        float angle = (strand / 17.0 - 0.5) * radial_spread;
        mat2 fan = mat2(vec2(cos(angle), sin(angle)), vec2(-sin(angle), cos(angle)));
        VERTEX.xz = fan * VERTEX.xz;
        NORMAL.xz = fan * NORMAL.xz;
    }
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
