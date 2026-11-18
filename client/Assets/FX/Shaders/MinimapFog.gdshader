shader_type canvas_item;
render_mode blend_mul;
uniform sampler2D vision_texture : repeat_disable, filter_linear;
uniform vec2 vision_origin;
uniform vec2 vision_size;
uniform vec2 map_origin;
uniform vec2 map_right;
uniform vec2 map_down;
void fragment() {
  vec2 world = map_origin + UV.x * map_right + UV.y * map_down;
  vec2 uv = (world - vision_origin) / vision_size;
  float in_bounds = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
  float vision = texture(vision_texture, clamp(uv, vec2(0.0), vec2(1.0))).r * in_bounds;
  COLOR = vec4(vec3(mix(0.25, 1.0, vision)), 1.0);
}
