/// Set this to the maximum number of outline groups you need to support simultaneously. Must be defined at compile time since array length requires a positive integer constant.
const int OUTLINE_LIMIT = 10;

// --- Family flags ---

const int FLAG_F_OUTLINE_PIXEL = 1 << 0;
const int FLAG_F_OUTLINE_SPATIAL = 1 << 1;
const int FLAG_F_OUTLINE_GROUPED = 1 << 2;
const int FLAG_F_OUTLINE_INDIVIDUAL = 1 << 3;

// --- Instance flags ---

const int FLAG_I_BORDER_NOISE = 1 << 4;

const int FLAG_I_EFFECT_FLICKER = 1 << 5;

const int FLAG_I_PATTERN_NOISE = 1 << 7;
const int FLAG_I_PATTERN_1 = 1 << 8;

// --- Area flags ---

const int FLAG_A_OP_UNION = 1 << 12;
const int FLAG_A_OP_INTERSECTION = 1 << 13;
const int FLAG_A_OP_SUBTRACTION = 1 << 14;

const int FLAG_A_SHAPE_SPHERE = 1 << 15;
const int FLAG_A_SHAPE_BOX = 1 << 16;
const int FLAG_A_SHAPE_CYLINDER = 1 << 17;

const int FLAG_A_FILL_IN_TO_OUT = 1 << 18;
const int FLAG_A_FILL_FORWARD = 1 << 19;
const int FLAG_A_FILL_INVERTED = 1 << 20;

// --- Family data coordinates ---

const int F_BASE_RGB = 1;
const int F_BASE_A = 2;
const int F_BASE_OUTLINE_RGB = 3;
const int F_BASE_OUTLINE_A = 4;
const int F_BASE_OUTLINE_1 = 5;
const int F_BORDER_NOISE_1 = 6;
const int F_BORDER_NOISE_2 = 7;
const int F_EFFECT_FLICKER = 8;
const int F_FILL_RGB = 10;
const int F_FILL_A = 11;
const int F_FILL_HIGHLIGHT_RGB = 12;
const int F_FILL_HIGHLIGHT_A = 13;
const int F_FILL_HIGHLIGHT_1 = 14;
const int F_PATTERN_NOISE_1 = 15;
const int F_PATTERN_1 = 16;
const int F_1 = 18;

// --- Instance data coordinates ---

const int I_ANIM_1 = 20;
const int I_ANIM_2 = 21;
const int I_BASIS_X = 22;
const int I_BASIS_Y = 23;
const int I_BASIS_Z = 24;
const int I_ORIGIN = 25;
const int I_1 = 26;

// --- Area data coordinates ---

const int A_BASIS_X = 29;
const int A_BASIS_Y = 30;
const int A_BASIS_Z = 31;
const int A_ORIGIN = 32;
const int A_1 = 33;
const int A_EXTENTS = 35;
const int A_SPHERE = 36;

// --- Struct definitions ---

struct SceneData {
    int family_count;
    int telegraph_count;
    int area_count;
    int outline_count;
};


struct FamilyData {
    vec4 base_color;
    vec4 base_outline_color;
    float base_outline_width;
    float border_noise_1_amplitude;
	float border_noise_1_frequency;
	float border_noise_1_speed;
	float border_noise_2_amplitude;
	float border_noise_2_frequency;
	float border_noise_2_speed;
	float effect_flicker_amplitude;
	float effect_flicker_speed;
	vec4 fill_color;
	vec4 fill_highlight_color;
	float fill_highlight_trail;
	float fill_highlight_width;
	int flags;
    float normal_clip_angle_deg;
	int outline_idx;
	float pattern_1_amplitude;
	float pattern_1_frequency;
	float pattern_1_speed;
	float pattern_noise_amplitude;
	float pattern_noise_frequency;
	float pattern_noise_speed;
};

struct OutlineData {
    vec4 base_outline_color;
    float base_outline_width;
    int flags;
    bool is_outline_grouped;
    bool is_outline_pixel;
    bool is_outline_spatial;
};


struct TelegraphData {
    float alpha_multiplier;
    mat3 basis;
    int family_idx;
    float fill_progress;
    int flags;
    bool has_border_noise;
    bool has_effect_flicker;
    bool has_pattern_noise;
    bool has_pattern_1;
    int last_area_idx;
    vec3 origin;
};


struct AreaData {
    float angle;
    mat3 basis;
    vec3 extents;
    float fill_progress;
    int flags;
    float min_radius;
    vec3 origin;
    float radius;
};

// --- Read functions ---

vec3 fetch(sampler2D from, int x, int y) {
    return texelFetch(from, ivec2(x, y), 0).rgb;
}


void read_scene_data(out SceneData scene, sampler2D from) {
    vec3 texel = fetch(from, 0, 0).rgb;
    scene.family_count = int(texel.r);
    scene.telegraph_count = int(texel.g);
    scene.area_count = int(texel.b);
    texel = fetch(from, 1, 0).rgb;
    scene.outline_count = int(texel.r);
}


void read_family_data(out FamilyData family, sampler2D from, int idx) {
    vec3 f_border_noise_1 = fetch(from, idx, F_BORDER_NOISE_1);
    vec3 f_border_noise_2 = fetch(from, idx, F_BORDER_NOISE_2);
    vec3 f_effect_flicker = fetch(from, idx, F_EFFECT_FLICKER);
    vec3 f_fill_highlight_1 = fetch(from, idx, F_FILL_HIGHLIGHT_1);
    vec3 f_pattern_noise_1 = fetch(from, idx, F_PATTERN_NOISE_1);
	vec3 f_pattern_1 = fetch(from, idx, F_PATTERN_1);
    vec3 f_1 = fetch(from, idx, F_1);
    family.base_color.rgb = fetch(from, idx, F_BASE_RGB);
    family.base_color.a = fetch(from, idx, F_BASE_A).r;
    family.base_outline_color.rgb = fetch(from, idx, F_BASE_OUTLINE_RGB);
    family.base_outline_color.a = fetch(from, idx, F_BASE_OUTLINE_A).r;
    family.base_outline_width = fetch(from, idx, F_BASE_OUTLINE_1).r;
    family.border_noise_1_amplitude = f_border_noise_1.r;
    family.border_noise_1_frequency = f_border_noise_1.g;
    family.border_noise_1_speed = f_border_noise_1.b;
    family.border_noise_2_amplitude = f_border_noise_2.r;
    family.border_noise_2_frequency = f_border_noise_2.g;
    family.border_noise_2_speed = f_border_noise_2.b;
    family.effect_flicker_amplitude = f_effect_flicker.r;
    family.effect_flicker_speed = f_effect_flicker.b;
    family.fill_color.rgb = fetch(from, idx, F_FILL_RGB);
    family.fill_color.a = fetch(from, idx, F_FILL_A).r;
    family.fill_highlight_color.rgb = fetch(from, idx, F_FILL_HIGHLIGHT_RGB);
    family.fill_highlight_color.a = fetch(from, idx, F_FILL_HIGHLIGHT_A).r;
    family.fill_highlight_trail = f_fill_highlight_1.r;
    family.fill_highlight_width = f_fill_highlight_1.g;
    family.flags = int(f_1.r);
    family.normal_clip_angle_deg = f_1.g;
    family.outline_idx = int(f_1.b);
	family.pattern_1_amplitude = f_pattern_1.r;
	family.pattern_1_frequency = f_pattern_1.g;
	family.pattern_1_speed = f_pattern_1.b;
    family.pattern_noise_amplitude = f_pattern_noise_1.r;
    family.pattern_noise_frequency = f_pattern_noise_1.g;
    family.pattern_noise_speed = f_pattern_noise_1.b;
}


void read_outline_data(out OutlineData outline, sampler2D from, int idx) {
    vec3 f_1 = fetch(from, idx, F_1);
    outline.base_outline_color.rgb = fetch(from, idx, F_BASE_OUTLINE_RGB);
    outline.base_outline_color.a = fetch(from, idx, F_BASE_OUTLINE_A).r;
    outline.base_outline_width = fetch(from, idx, F_BASE_OUTLINE_1).r;
    outline.flags = int(f_1.r);
}


bool is_outline_grouped(int family_flags) { return (family_flags & FLAG_F_OUTLINE_GROUPED) == FLAG_F_OUTLINE_GROUPED; }
bool is_outline_individual(int family_flags) { return (family_flags & FLAG_F_OUTLINE_INDIVIDUAL) == FLAG_F_OUTLINE_INDIVIDUAL; }
bool is_outline_pixel(int family_flags) { return (family_flags & FLAG_F_OUTLINE_PIXEL) == FLAG_F_OUTLINE_PIXEL; }
bool is_outline_spatial(int family_flags) { return (family_flags & FLAG_F_OUTLINE_SPATIAL) == FLAG_F_OUTLINE_SPATIAL; }


void read_telegraph_data(out TelegraphData telegraph, sampler2D from, int idx) {
    vec3 i_anim_1 = fetch(from, idx, I_ANIM_1);
    vec3 i_1 = fetch(from, idx, I_1);
    telegraph.alpha_multiplier = i_anim_1.g;
    telegraph.basis = mat3(
        fetch(from, idx, I_BASIS_X),
        fetch(from, idx, I_BASIS_Y),
        fetch(from, idx, I_BASIS_Z)
    );
    telegraph.fill_progress = i_anim_1.r;
    telegraph.flags = int(i_1.r);
    telegraph.last_area_idx = int(i_1.g);
    telegraph.origin = fetch(from, idx, I_ORIGIN);
    telegraph.family_idx = int(i_1.b);

    telegraph.has_border_noise = (telegraph.flags & FLAG_I_BORDER_NOISE) == FLAG_I_BORDER_NOISE;

    telegraph.has_effect_flicker = (telegraph.flags & FLAG_I_EFFECT_FLICKER) == FLAG_I_EFFECT_FLICKER;

    telegraph.has_pattern_noise = (telegraph.flags & FLAG_I_PATTERN_NOISE) == FLAG_I_PATTERN_NOISE;
    telegraph.has_pattern_1 = (telegraph.flags & FLAG_I_PATTERN_1) == FLAG_I_PATTERN_1;
}


void read_area_data(out AreaData area, sampler2D from, int idx) {
    vec3 a_1 = fetch(from, idx, A_1);
    vec3 sphere = fetch(from, idx, A_SPHERE);
    area.angle = sphere.g;
    area.basis = mat3(
        fetch(from, idx, A_BASIS_X),
        fetch(from, idx, A_BASIS_Y),
        fetch(from, idx, A_BASIS_Z)
    );
    area.extents = fetch(from, idx, A_EXTENTS);
    area.fill_progress = a_1.g;
    area.flags = int(a_1.r);
    area.min_radius = sphere.b;
    area.origin = fetch(from, idx, A_ORIGIN);
    area.radius = sphere.r;
}


bool is_fill_foward(int area_flags) { return (area_flags & FLAG_A_FILL_FORWARD) == FLAG_A_FILL_FORWARD; }
bool is_fill_in_to_out(int area_flags) { return (area_flags & FLAG_A_FILL_IN_TO_OUT) == FLAG_A_FILL_IN_TO_OUT; }
bool is_fill_inverted(int area_flags) { return (area_flags & FLAG_A_FILL_INVERTED) == FLAG_A_FILL_INVERTED; }


bool is_op_intersection(int area_flags) { return (area_flags & FLAG_A_OP_INTERSECTION) == FLAG_A_OP_INTERSECTION; }
bool is_op_subtraction(int area_flags) { return (area_flags & FLAG_A_OP_SUBTRACTION) == FLAG_A_OP_SUBTRACTION; }
bool is_op_union(int area_flags) { return (area_flags & FLAG_A_OP_UNION) == FLAG_A_OP_UNION; }


bool is_shape_sphere(int area_flags) { return (area_flags & FLAG_A_SHAPE_SPHERE) == FLAG_A_SHAPE_SPHERE; }
bool is_shape_box(int area_flags) { return (area_flags & FLAG_A_SHAPE_BOX) == FLAG_A_SHAPE_BOX; }
bool is_shape_cylinder(int area_flags) { return (area_flags & FLAG_A_SHAPE_CYLINDER) == FLAG_A_SHAPE_CYLINDER; }
