shader_type canvas_item;
render_mode unshaded;

uniform float glow_width_px = 120.0;

void fragment() {
  vec2 edge_distance_px = min(SCREEN_UV, vec2(1.0) - SCREEN_UV) / SCREEN_PIXEL_SIZE;
  float glow = 1.0 - smoothstep(0.0, glow_width_px, min(edge_distance_px.x, edge_distance_px.y));
  COLOR = vec4(1.0, 0.0, 0.015, glow * 0.93) * COLOR;
}
