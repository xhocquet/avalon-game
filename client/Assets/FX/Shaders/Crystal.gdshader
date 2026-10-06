shader_type spatial;
render_mode unshaded, cull_disabled;

uniform vec3 crystal_color : source_color = vec3(0.78, 0.67, 0.27);
uniform vec3 light_direction = vec3(-0.35, 0.8, 0.45);
uniform float emission_energy : hint_range(0.0, 3.0, 0.1) = 0.25;
uniform vec3 edge_color : source_color = vec3(0.055, 0.045, 0.025);
uniform float edge_width : hint_range(0.5, 6.0, 0.1) = 2.5;

void fragment() {
    float diffuse = max(dot(normalize(NORMAL), normalize(light_direction)), 0.0);
    vec3 color = crystal_color * (0.65 + diffuse * 0.45);
    float nearest_edge = min(COLOR.r, min(COLOR.g, COLOR.b));
    float edge = 1.0 - smoothstep(0.0, fwidth(nearest_edge) * edge_width, nearest_edge);

    ALBEDO = mix(color, edge_color, edge);
    EMISSION = color * emission_energy * (1.0 - edge);
}
