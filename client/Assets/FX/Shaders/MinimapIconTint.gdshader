shader_type canvas_item;
render_mode unshaded;

uniform vec4 team_color : source_color = vec4(1.0);

void fragment() {
  vec4 artwork = texture(TEXTURE, UV);
  float brightness = dot(artwork.rgb, vec3(0.299, 0.587, 0.114));
  float tint = smoothstep(0.25, 0.75, brightness);
  COLOR = vec4(mix(artwork.rgb, artwork.rgb * team_color.rgb, tint), artwork.a * team_color.a);
}
