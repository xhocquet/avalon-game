shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, depth_test_disabled, cull_disabled, fog_disabled, shadows_disabled;

uniform sampler2D mask_texture : source_color, filter_nearest, repeat_disable;
uniform sampler2D scene_depth : hint_depth_texture, filter_nearest, repeat_disable;
uniform vec4 outline_color : source_color = vec4(1.0, 0.0, 0.0, 1.0);
uniform float outline_width : hint_range(0.0, 8.0) = 2.0;
uniform float glow_width : hint_range(0.0, 20.0) = 8.0;
uniform float glow_opacity : hint_range(0.0, 1.0) = 0.4;
uniform float depth_range = 128.0;
uniform vec4 screen_bounds = vec4(0.0, 0.0, 1.0, 1.0);

void vertex() {
  POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

float world_depth(vec2 uv, mat4 inverse_projection) {
  float raw = textureLod(scene_depth, uv, 0.0).r;
  #if CURRENT_RENDERER == RENDERER_COMPATIBILITY
  vec3 ndc = vec3(uv, raw) * 2.0 - 1.0;
  #else
  vec3 ndc = vec3(uv * 2.0 - 1.0, raw);
  #endif
  vec4 view = inverse_projection * vec4(ndc, 1.0);
  return -view.z / view.w;
}

float visible_mask(vec2 uv, float output_depth, mat4 inverse_projection) {
  if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0)))) return 0.0;
  vec3 mask = textureLod(mask_texture, uv, 0.0).rgb;
  if (mask.b < 0.5) return 0.0;
  float depth = dot(mask.rg, vec2(1.0, 1.0 / 255.0)) * depth_range;
  float tolerance = 0.15 + depth * 0.003;
  return depth <= min(world_depth(uv, inverse_projection), output_depth) + tolerance ? 1.0 : 0.0;
}

void fragment() {
  if (any(lessThan(SCREEN_UV, screen_bounds.xy)) || any(greaterThan(SCREEN_UV, screen_bounds.zw))) discard;
  float center = textureLod(mask_texture, SCREEN_UV, 0.0).b;
  if (center > 0.5) discard;
  float output_depth = world_depth(SCREEN_UV, INV_PROJECTION_MATRIX);
  vec2 pixel = 1.0 / VIEWPORT_SIZE;
  float edge = 0.0;
  float glow = 0.0;
  for (int i = 0; i < 16; i++) {
    float angle = float(i) * 6.28318530718 / 16.0;
    vec2 direction = vec2(cos(angle), sin(angle)) * pixel;
    edge = max(edge, visible_mask(SCREEN_UV + direction * outline_width, output_depth, INV_PROJECTION_MATRIX));
    glow += visible_mask(SCREEN_UV + direction * glow_width * 0.5, output_depth, INV_PROJECTION_MATRIX) * 0.65;
    glow += visible_mask(SCREEN_UV + direction * glow_width, output_depth, INV_PROJECTION_MATRIX) * 0.35;
  }
  float alpha = max(edge, glow / 16.0 * glow_opacity) * outline_color.a;
  if (alpha < 0.001) discard;
  ALBEDO = outline_color.rgb;
  ALPHA = alpha;
}
