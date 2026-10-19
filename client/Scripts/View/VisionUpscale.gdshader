shader_type canvas_item;
render_mode unshaded;

uniform sampler2D source_mask : repeat_disable, filter_nearest;

float corner(vec2 p) {
    return (0.5 - p.x - p.y) * 0.70710678;
}

// Marching-squares reconstruction of the sixteen binary 2x2 neighborhoods.
float contour(int pattern, vec2 p) {
    if (pattern == 0) return -1.0;
    if (pattern == 15) return 1.0;
    if (pattern == 1) return corner(p);
    if (pattern == 2) return corner(vec2(1.0 - p.x, p.y));
    if (pattern == 4) return corner(vec2(p.x, 1.0 - p.y));
    if (pattern == 8) return corner(vec2(1.0) - p);
    if (pattern == 14) return -corner(p);
    if (pattern == 13) return -corner(vec2(1.0 - p.x, p.y));
    if (pattern == 11) return -corner(vec2(p.x, 1.0 - p.y));
    if (pattern == 7) return -corner(vec2(1.0) - p);
    if (pattern == 3) return 0.5 - p.y;
    if (pattern == 12) return p.y - 0.5;
    if (pattern == 5) return 0.5 - p.x;
    if (pattern == 10) return p.x - 0.5;
    if (pattern == 6) return max(corner(vec2(1.0 - p.x, p.y)), corner(vec2(p.x, 1.0 - p.y)));
    return max(corner(p), corner(vec2(1.0) - p));
}

int bit_at(ivec2 cell, ivec2 size) {
    return int(step(0.5, texelFetch(source_mask, clamp(cell, ivec2(0), size - ivec2(1)), 0).r));
}

void fragment() {
    ivec2 size = textureSize(source_mask, 0);
    vec2 position = UV * vec2(size) - 0.5;
    ivec2 cell = ivec2(floor(position));
    int pattern = bit_at(cell, size) + 2 * bit_at(cell + ivec2(1, 0), size)
        + 4 * bit_at(cell + ivec2(0, 1), size) + 8 * bit_at(cell + ivec2(1, 1), size);
    float coverage = clamp(0.5 + contour(pattern, fract(position)) * 4.0, 0.0, 1.0);
    COLOR = vec4(vec3(coverage), 1.0);
}
