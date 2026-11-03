shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, depth_test_disabled, fog_disabled;

uniform sampler2D portrait : source_color, filter_linear_mipmap;
uniform vec4 team_color : source_color = vec4(0.14, 0.72, 1.0, 1.0);
uniform float portrait_radius : hint_range(0.1, 0.45) = 0.35;
uniform float border_width : hint_range(0.005, 0.08) = 0.028;
uniform float rim_width : hint_range(0.002, 0.04) = 0.012;
uniform float glow_width : hint_range(0.01, 0.1) = 0.065;
uniform float glow_opacity : hint_range(0.0, 1.0) = 0.45;

void vertex() {
  // Ignore the hero's rotation and scale.
  MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}

void fragment() {
  float radius = length(UV - vec2(0.5));
  float aa = max(fwidth(radius), 0.001);
  float outer_radius = portrait_radius + border_width;
  float face = 1.0 - smoothstep(portrait_radius - aa, portrait_radius + aa, radius);
  float border = 1.0 - smoothstep(outer_radius - aa, outer_radius + aa, radius);
  float edge = 1.0 - smoothstep(outer_radius + rim_width - aa, outer_radius + rim_width + aa, radius);
  float glow = exp(-max(radius - outer_radius, 0.0) / glow_width);
  glow *= 1.0 - smoothstep(0.47, 0.5, radius);
  vec2 portrait_uv = (UV - vec2(0.5)) / (2.0 * portrait_radius) + vec2(0.5);
  vec2 dimensions = vec2(textureSize(portrait, 0));
  portrait_uv = (portrait_uv - vec2(0.5)) * min(dimensions.x, dimensions.y) / dimensions + vec2(0.5);
  vec4 artwork = texture(portrait, portrait_uv);
  vec3 color = mix(team_color.rgb, vec3(0.025, 0.03, 0.04), edge);
  color = mix(color, team_color.rgb, border);
  color = mix(color, artwork.rgb, face * artwork.a);
  ALBEDO = color;
  ALPHA = max(edge, glow * glow_opacity);
}
