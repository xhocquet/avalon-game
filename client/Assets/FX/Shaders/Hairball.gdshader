shader_type spatial;
render_mode unshaded;

uniform vec3 hair_color : source_color = vec3(0.38, 0.29, 0.18);
uniform vec3 highlight_color : source_color = vec3(0.5, 0.39, 0.25);
uniform vec3 groove_color : source_color = vec3(0.075, 0.055, 0.035);
uniform vec3 magic_color : source_color = vec3(0.2, 0.95, 0.35);
uniform float magic_strength : hint_range(0.0, 2.0) = 0.65;
uniform float magic_amount : hint_range(0.0, 1.0) = 1.0;
instance uniform float strand_seed = 0.0;
instance uniform float curl_cutoff = 1.0;

varying vec3 model_normal;

void vertex() {
    model_normal = NORMAL;
    VERTEX += NORMAL * sin(UV.x * 18.84956 + strand_seed) * sin(UV.y * 6.283185) * 0.003;
}

void fragment() {
    if (UV.x > curl_cutoff) {
        discard;
    }
    float along = UV.x * 6.283185;
    float across = UV.y + sin(along * 3.0 + strand_seed) * 0.028;
    float strand = across * 4.0;
    float groove = smoothstep(0.84, 0.98, 0.5 + 0.5 * cos(strand * 6.283185));
    float fine_strand = sin(across * 110.0 + sin(along * 5.0 + strand_seed)) * 0.012;
    float light = dot(normalize(model_normal), normalize(vec3(-0.35, 0.8, 0.45)));
    vec3 color = mix(hair_color, highlight_color, smoothstep(-0.5, 1.0, light));
    color += fine_strand;
    color = mix(color, groove_color, groove * 0.85);

    float rim = 1.0 - smoothstep(0.05, 0.28, abs(dot(normalize(NORMAL), normalize(VIEW))));
    color = mix(color, groove_color, rim * 0.9);

    float glint = pow(0.5 + 0.5 * sin(along * 3.0 + strand_seed * 2.3), 16.0);
    float seam = pow(0.5 + 0.5 * cos(across * 25.13274 + strand_seed), 32.0);
    float magic = glint * seam * magic_amount;
    ALBEDO = mix(color, magic_color, magic * 0.8);
    EMISSION = magic_color * magic * magic_strength;
}
