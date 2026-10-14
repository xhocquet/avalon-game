// CC0: LeLu, based on OlisUnfinishedProjects' decal shader.
// https://godotshaders.com/shader/decal-shader-y_fading-particle-alpha-scissor-treshold/
shader_type spatial;
render_mode unshaded, blend_mix, cull_front, depth_draw_never, depth_test_disabled;

uniform sampler2D depth_texture : hint_depth_texture, repeat_disable, filter_nearest;
uniform vec4 slime_color : source_color = vec4(0.27, 0.65, 0.035, 1.0);
uniform vec4 highlight_color : source_color = vec4(0.66, 0.88, 0.12, 1.0);
uniform float erosion : hint_range(0.0, 1.0) = 0.0;
uniform float seed = 0.0;
uniform bool enable_y_fade = true;
uniform float fade_start : hint_range(0.0, 1.0) = 0.35;
uniform float fade_end : hint_range(0.0, 1.0) = 0.9;
uniform float fade_power = 2.0;
uniform bool use_particle_alpha_scissor = false;
uniform bool cone_shape = false;
uniform float cone_angle_degrees = 60.0;
uniform float reveal : hint_range(0.0, 1.0) = 1.0;
uniform float texture_scale = 1.0;

varying mat4 inverse_model;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x),
               mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void vertex() {
    inverse_model = inverse(MODEL_MATRIX);
}

void fragment() {
    float depth = texture(depth_texture, SCREEN_UV).r;
    #if CURRENT_RENDERER == RENDERER_COMPATIBILITY
    vec3 ndc = vec3(SCREEN_UV, depth) * 2.0 - 1.0;
    #else
    vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, depth);
    #endif
    vec4 world = INV_VIEW_MATRIX * INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
    vec3 local = (inverse_model * vec4(world.xyz / world.w, 1.0)).xyz;
    if (any(greaterThan(abs(local), vec3(0.5)))) {
        discard;
    }

    vec2 uv = local.xz * 2.0;
    if (cone_shape) {
        uv = vec2(local.x * 2.0, local.z - 0.5);
    }
    float grain = noise(uv * 5.5 * texture_scale + vec2(seed));
    float edge = length(uv) + (noise(uv * 7.0 + vec2(seed)) - 0.5) * 0.14;
    float mask = 1.0 - smoothstep(0.78, 1.0, edge);
    if (cone_shape) {
        float angle = abs(atan(uv.x, -uv.y));
        float half_angle = radians(cone_angle_degrees) * 0.5;
        mask *= 1.0 - smoothstep(max(half_angle - 0.035, 0.0), half_angle, angle);
        mask *= 1.0 - smoothstep(max(reveal - 0.08, 0.0), max(reveal, 0.001), length(uv));
    }
    float y_fade = 1.0;
    if (enable_y_fade) {
        y_fade = 1.0 - pow(smoothstep(fade_start, fade_end, local.y + 0.5), fade_power);
    }
    float alpha = mask * mix(0.55, 1.0, grain) * y_fade;
    float threshold = use_particle_alpha_scissor ? COLOR.a : mix(0.025, 1.01, erosion);
    ALBEDO = mix(slime_color.rgb, highlight_color.rgb, grain * grain) * COLOR.rgb;
    ALPHA = alpha;
    ALPHA_SCISSOR_THRESHOLD = threshold;
    ROUGHNESS = 0.28;
}
