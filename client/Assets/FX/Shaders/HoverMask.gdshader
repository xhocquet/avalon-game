shader_type spatial;
render_mode unshaded, fog_disabled;

uniform float depth_range = 128.0;
varying float view_depth;

void vertex() {
  view_depth = -(MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).z;
}

void fragment() {
  float depth = clamp(view_depth / depth_range, 0.0, 0.99999);
  vec2 packed_depth = fract(depth * vec2(1.0, 255.0));
  packed_depth.x -= packed_depth.y / 255.0;
  ALBEDO = vec3(packed_depth, 1.0); // RG: view depth, B: silhouette.
}
