// Inverted-Hull Outline with Group Merge by 42atomys (2026-06-17).
shader_type spatial;
render_mode cull_front, unshaded, blend_mix, depth_draw_opaque, shadows_disabled;

uniform vec3 outline_color : source_color = vec3(1.0, 0.95, 1.0);
uniform float thickness : hint_range(0.0, 0.3) = 0.04;
uniform float outline_energy : hint_range(0.0, 4.0) = 1.0;
uniform float outline_transparency : hint_range(0.0, 1.0) = 1.0;
uniform bool merge_group = false;
uniform float merge_depth_range : hint_range(0.1, 100.0) = 10.0;
uniform sampler2D DEPTH_TEXTURE : hint_depth_texture, filter_linear_mipmap;

varying float v_view_z;

void vertex() {
  vec3 d = VERTEX - vec3(0.0, 0.05, 0.0);
  vec3 dir = length(d) > 0.001 ? normalize(d) : vec3(0.0, -1.0, 0.0);
  vec3 view_n = normalize((MODELVIEW_MATRIX * vec4(dir, 0.0)).xyz);
  vec4 view_pos = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
  view_pos.xyz += view_n * thickness;
  v_view_z = view_pos.z;
  POSITION = PROJECTION_MATRIX * view_pos;
}

void fragment() {
  if (merge_group) {
    float raw = texture(DEPTH_TEXTURE, SCREEN_UV).r;
    #if CURRENT_RENDERER == RENDERER_COMPATIBILITY
    vec3 ndc = vec3(SCREEN_UV, raw) * 2.0 - 1.0;
    #else
    vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, raw);
    #endif
    vec4 vs = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
    float scene_dist = -(vs.xyz / vs.w).z;
    float frag_dist = -v_view_z;
    float gap = scene_dist - frag_dist;
    if (gap > 0.01 && gap < merge_depth_range) {
      discard;
    }
  }
  ALBEDO = outline_color * outline_energy;
  ALPHA = outline_transparency;
}
