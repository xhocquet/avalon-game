// Terrain splatmap shader: blends 3 full PBR materials (albedo/normal/AO/roughness/height)
// using an RGB splatmap. Areas not covered by the splatmap fall back to a flat
// ground color. Height maps bias the blend so boundaries look natural instead
// of a flat linear crossfade.
shader_type spatial;

uniform sampler2D splatmap;
uniform float blend_sharpness : hint_range(0.0, 1.0) = 0.3;

uniform sampler2D albedo_r : source_color;
uniform sampler2D normal_r : hint_normal;
uniform sampler2D ao_r;
uniform sampler2D roughness_r;
uniform sampler2D height_r;
uniform float resolution_r = 1.0;

uniform sampler2D albedo_g : source_color;
uniform sampler2D normal_g : hint_normal;
uniform sampler2D ao_g;
uniform sampler2D roughness_g;
uniform sampler2D height_g;
uniform float resolution_g = 1.0;

uniform sampler2D albedo_b : source_color;
uniform sampler2D normal_b : hint_normal;
uniform sampler2D ao_b;
uniform sampler2D roughness_b;
uniform sampler2D height_b;
uniform float resolution_b = 1.0;

uniform vec3 ground_color : source_color;

void fragment() {
	vec3 splat = texture(splatmap, UV).rgb;
	float amount_ground = clamp(1.0 - (splat.r + splat.g + splat.b), 0.0, 1.0);

	vec2 uv_r = UV * resolution_r;
	vec2 uv_g = UV * resolution_g;
	vec2 uv_b = UV * resolution_b;

	float h_r = texture(height_r, uv_r).r;
	float h_g = texture(height_g, uv_g).r;
	float h_b = texture(height_b, uv_b).r;

	// Height-biased blend: whichever material is "taller" at a texel wins more
	// of the weight, producing jagged/natural boundaries instead of a flat
	// linear crossfade. blend_sharpness controls how strongly height matters
	// relative to the splatmap paint (0 = pure height, 1 = pure splatmap).
	vec3 weighted = vec3(
		splat.r * (h_r + blend_sharpness),
		splat.g * (h_g + blend_sharpness),
		splat.b * (h_b + blend_sharpness)
	);
	float weight_sum = weighted.r + weighted.g + weighted.b;
	vec3 w = weight_sum > 0.0001 ? weighted / weight_sum : vec3(0.0);
	w *= (1.0 - amount_ground);

	vec3 albedo_blend = texture(albedo_r, uv_r).rgb * w.r
		+ texture(albedo_g, uv_g).rgb * w.g
		+ texture(albedo_b, uv_b).rgb * w.b
		+ ground_color * amount_ground;

	vec3 normal_blend = texture(normal_r, uv_r).rgb * w.r
		+ texture(normal_g, uv_g).rgb * w.g
		+ texture(normal_b, uv_b).rgb * w.b
		+ vec3(0.5, 0.5, 1.0) * amount_ground;

	float ao_blend = texture(ao_r, uv_r).r * w.r
		+ texture(ao_g, uv_g).r * w.g
		+ texture(ao_b, uv_b).r * w.b
		+ 1.0 * amount_ground;

	float roughness_blend = texture(roughness_r, uv_r).r * w.r
		+ texture(roughness_g, uv_g).r * w.g
		+ texture(roughness_b, uv_b).r * w.b
		+ 1.0 * amount_ground;

	ALBEDO = albedo_blend;
	NORMAL_MAP = normal_blend;
	AO = ao_blend;
	ROUGHNESS = roughness_blend;
}
