shader_type canvas_item;
render_mode unshaded;

void fragment() {
  vec2 edge = abs(UV - vec2(0.5)) * 2.0;
  float glow = smoothstep(0.2, 0.95, max(edge.x, edge.y));
  COLOR = vec4(1.0, 0.0, 0.015, 0.08 + glow * 0.85) * COLOR;
}
