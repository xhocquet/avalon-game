// Terrain splatmap shader: blends 2 full PBR materials (albedo/normal/AO/roughness/height)
// using an RG splatmap. Areas not covered by the splatmap fall back to a flat
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

uniform vec3 ground_color : source_color;

void fragment() {
	vec2 splat = texture(splatmap, UV).rg;
	float amount_ground = clamp(1.0 - (splat.r + splat.g), 0.0, 1.0);

	vec2 uv_r = UV * resolution_r;
	vec2 uv_g = UV * resolution_g;

	float h_r = texture(height_r, uv_r).r;
	float h_g = texture(height_g, uv_g).r;

	// Height-biased blend: whichever material is "taller" at a texel wins more
	// of the weight, producing jagged/natural boundaries instead of a flat
	// linear crossfade. blend_sharpness controls how strongly height matters
	// relative to the splatmap paint (0 = pure height, 1 = pure splatmap).
	vec2 weighted = vec2(
		splat.r * (h_r + blend_sharpness),
		splat.g * (h_g + blend_sharpness)
	);
	float weight_sum = weighted.r + weighted.g;
	vec2 w = weight_sum > 0.0001 ? weighted / weight_sum : vec2(0.0);
	w *= (1.0 - amount_ground);

	vec3 albedo_blend = texture(albedo_r, uv_r).rgb * w.r
		+ texture(albedo_g, uv_g).rgb * w.g
		+ ground_color * amount_ground;

	vec3 normal_blend = texture(normal_r, uv_r).rgb * w.r
		+ texture(normal_g, uv_g).rgb * w.g
		+ vec3(0.5, 0.5, 1.0) * amount_ground;

	float ao_blend = texture(ao_r, uv_r).r * w.r
		+ texture(ao_g, uv_g).r * w.g
		+ 1.0 * amount_ground;

	float roughness_blend = texture(roughness_r, uv_r).r * w.r
		+ texture(roughness_g, uv_g).r * w.g
		+ 1.0 * amount_ground;

	ALBEDO = albedo_blend;
	NORMAL_MAP = normal_blend;
	AO = ao_blend;
	ROUGHNESS = roughness_blend;
}
