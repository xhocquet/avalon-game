// Adapted from QbieShay's MIT spatial particle shader; see Source.md.
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

uniform sampler2D depth_texture : hint_depth_texture, repeat_disable, filter_nearest;

uniform float emission_energy = 0.0;
uniform sampler2D emission_over_life: hint_default_white;
uniform float alpha = 1.0;
uniform sampler2D texture1 : hint_default_white;
uniform sampler2D texture2 : hint_default_white;
uniform vec2 texture1_offset;
uniform vec2 texture1_scale = vec2(1.0);
uniform vec2 texture1_pan_direction = vec2(1.0, 0.0);
uniform float texture1_pan_speed = 0.0;
uniform vec2 texture2_offset;
uniform vec2 texture2_scale = vec2(1.0);
uniform vec2 texture2_pan_direction = vec2(1.0, 0.0);
uniform float texture2_pan_speed = 0.0;
uniform int mix_mode = 0;
uniform bool alpha_erosion = true;
uniform sampler2D dissolve_texture : hint_default_black;
uniform float erosion_smoothness = 1.0;
uniform float particle_billboard_blend = 1.0;
uniform bool use_heightmap = false;
uniform float height_amount = 0.3;
uniform sampler2D height_texture;
uniform sampler2D normal_texture;
uniform bool use_index_offset = false;
uniform bool use_proximity_fade = false;
uniform float proximity_fade_distance = 0.3;
uniform bool use_particle_animation = false;
uniform bool particle_animation_resize = true;
uniform float particles_anim_h_frames = 1.0;
uniform float particles_anim_v_frames = 1.0;
uniform bool particles_anim_loop = false;
uniform bool rotate_y = false;
uniform bool camera_fade = false;
uniform float distance_fade_min = 0.0;
uniform float distance_fade_max = 2.0;

varying float lifetime;
varying float index;


mat4 rot_y(float angle){
	float cos_a = cos(angle);
	float sin_a = sin(angle);
	return mat4(
		vec4(cos_a, 0.0, sin_a, 0.0),
		vec4(0.0, 1.0, 0.0, 0.0),
		vec4(-sin_a, 0.0, cos_a, 0.0),
		vec4(0.0, 0.0, 0.0, 1.0)
	);
}

void vertex(){
	
	float h_frames = use_particle_animation ? max(particles_anim_h_frames, 1.0) : 1.0;
	float v_frames = use_particle_animation ? max(particles_anim_v_frames, 1.0) : 1.0;
	if (particle_animation_resize){
		VERTEX.xy /= vec2(h_frames, v_frames);
	}
	float particle_total_frames = (h_frames * v_frames);
	float particle_frame = floor(INSTANCE_CUSTOM.z * float(particle_total_frames));
	if (!particles_anim_loop) {
		particle_frame = clamp(particle_frame, 0.0, particle_total_frames - 1.0);
	} else {
		particle_frame = mod(particle_frame, particle_total_frames);
	}	UV /= vec2(h_frames, v_frames);
	UV += vec2(mod(particle_frame, h_frames) / h_frames, floor(particle_frame / h_frames) / v_frames);
	if (rotate_y){
		VERTEX = (rot_y(INSTANCE_CUSTOM.x) * vec4(VERTEX, 1.0)).xyz;
	}
	if (use_heightmap){
		VERTEX += NORMAL * height_amount * texture(height_texture, UV).r;
	}
	
	mat4 mat_world = mat4(
		mix(normalize(INV_VIEW_MATRIX[0])*length(MODEL_MATRIX[0]), MODEL_MATRIX[0], particle_billboard_blend),
		mix(normalize(INV_VIEW_MATRIX[1])*length(MODEL_MATRIX[0]), MODEL_MATRIX[1], particle_billboard_blend),
		mix(normalize(INV_VIEW_MATRIX[2])*length(MODEL_MATRIX[2]), MODEL_MATRIX[2], particle_billboard_blend),
		MODEL_MATRIX[3]);
	mat_world = mat_world * mat4( vec4(cos(INSTANCE_CUSTOM.x),-sin(INSTANCE_CUSTOM.x), 0.0, 0.0), vec4(sin(INSTANCE_CUSTOM.x), cos(INSTANCE_CUSTOM.x), 0.0, 0.0),vec4(0.0, 0.0, 1.0, 0.0),vec4(0.0, 0.0, 0.0, 1.0));
	MODELVIEW_MATRIX = VIEW_MATRIX * mat_world;
	
	lifetime = INSTANCE_CUSTOM.y;
	index = float(INSTANCE_ID);
}

void fragment(){
	vec2 additional_offset = vec2(0.0);
	if (use_index_offset){
		additional_offset += 0.37 * float(index);
	}
	vec4 color1 = texture(texture1, vec2(UV*texture1_scale + texture1_offset + additional_offset + normalize(texture1_pan_direction) * texture1_pan_speed * lifetime));
	vec4 color2 = texture(texture2, vec2(UV*texture2_scale + texture2_offset + additional_offset +normalize(texture2_pan_direction) * texture2_pan_speed * lifetime));
	vec4 final_color;
	if (mix_mode == 0){
		final_color = color1 * color2;
	} else if (mix_mode == 1){
		final_color = color1 + color2;
	}
	ALBEDO = max(COLOR.rgb * final_color.rgb, vec3(0.0));
	ALPHA = final_color.a;
	if (alpha_erosion){
		float erosion = texture(dissolve_texture, UV).r;
		ALPHA *= alpha * 
			smoothstep(
				1.0 - erosion_smoothness,
				1.0,
				(erosion + 1.0) - (1.0 - COLOR.a) * 2.0
				);
		ALPHA = clamp(ALPHA, 0.0, 1.0);
	} else {
		ALPHA *= alpha * COLOR.a;
	}
	
	if(use_proximity_fade ){
		float depth_tex = textureLod(depth_texture,SCREEN_UV,0.0).r;
		#if CURRENT_RENDERER == RENDERER_COMPATIBILITY
		vec3 ndc = vec3(SCREEN_UV, depth_tex) * 2.0 - 1.0;
#else
		vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, depth_tex);
#endif
		vec4 world_pos = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
		world_pos.xyz/=world_pos.w;
		ALPHA*=clamp(1.0-smoothstep(world_pos.z+proximity_fade_distance,world_pos.z,VERTEX.z),0.0,1.0);
	}
	if (camera_fade){
		ALPHA*=clamp(smoothstep(distance_fade_min,distance_fade_max,-VERTEX.z),0.0,1.0);
	}
	if(use_heightmap){
		NORMAL_MAP = texture(normal_texture, UV).rgb;
	}
	ALPHA = clamp(ALPHA, 0.0, 1.0);
	EMISSION = ALBEDO * emission_energy * texture(emission_over_life, vec2(lifetime)).r * ALPHA;
	
}