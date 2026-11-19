shader_type canvas_item;

uniform vec4 team_color : source_color = vec4(0.25, 0.75, 0.95, 1.0);
varying vec4 tint;

void vertex() {
  tint = COLOR;
}

void fragment() {
  float radius = length(UV - vec2(0.5));
  float aa = max(fwidth(radius), 0.001);
  float face = 1.0 - smoothstep(0.35 - aa, 0.35 + aa, radius);
  float border = 1.0 - smoothstep(0.378 - aa, 0.378 + aa, radius);
  float edge = 1.0 - smoothstep(0.39 - aa, 0.39 + aa, radius);
  float glow = exp(-max(radius - 0.378, 0.0) / 0.065);
  glow *= 1.0 - smoothstep(0.47, 0.5, radius);
  vec2 dimensions = vec2(textureSize(TEXTURE, 0));
  vec2 portrait_uv = (UV - vec2(0.5)) / 0.7;
  portrait_uv = portrait_uv * min(dimensions.x, dimensions.y) / dimensions + vec2(0.5);
  vec4 artwork = texture(TEXTURE, portrait_uv);
  vec3 color = mix(team_color.rgb, vec3(0.025, 0.03, 0.04), edge);
  color = mix(color, team_color.rgb, border);
  color = mix(color, artwork.rgb, face * artwork.a);
  COLOR = tint * vec4(color, max(edge, glow * 0.45));
}
