#include "res://addons/constructive_telegraphs/shaders/inc/con_telegraph_reader.gdshaderinc"
#include "res://addons/constructive_telegraphs/shaders/inc/dist_functions.gdshaderinc"

const int MAX_STEPS = 300;
const float MAX_DISTANCE = 1000.0;
const float MIN_DISTANCE = 0.001;


float get_linear_depth(sampler2D depth_texture, vec2 screen_uv, mat4 inv_projection_matrix) {
    float depth_raw = texture(depth_texture, screen_uv, 0.).r;
    vec4 upos = inv_projection_matrix * vec4(screen_uv * 2. - 1., depth_raw, 1.);
    vec3 pixel_pos = upos.xyz / upos.w;
    return length(pixel_pos);
}


vec3 get_ray_dir(vec2 screen_uv, mat4 inv_view_matrix, mat4 inv_projection_matrix) {
    vec3 ndc = vec3(screen_uv * 2. - 1., 0.);
    vec4 camera = inv_view_matrix * inv_projection_matrix * vec4(ndc, 1);
    return normalize(camera.xyz);
}


/// Mirror the scale component of the basis while preserving the original rotation component.
mat3 fn_basis_with_inverted_scale(mat3 original) {
    float scaleX = length(vec3(original[0][0], original[0][1], original[0][2]));
    float scaleY = length(vec3(original[1][0], original[1][1], original[1][2]));
    float scaleZ = length(vec3(original[2][0], original[2][1], original[2][2]));

	mat3 inverse_scale = mat3(
		vec3(1. / scaleX, 0., 0.),
		vec3(0., 1. / scaleY, 0.),
		vec3(0., 0., 1. / scaleZ)
	);
	return original * inverse_scale * inverse_scale;
}


/// The corresponding local offset of a given world position relative to the local origin and basis.
vec3 fn_local_offset(vec3 origin, mat3 basis, vec3 world_position) {
    return (world_position - origin) * fn_basis_with_inverted_scale(basis);
}


float get_area_sdist(AreaData area, vec3 pos) {
    float area_sdist = 1. / 0.;
    if (is_shape_box(area.flags))
        area_sdist = sdBoxOrthogonal(pos, area.extents);
    if (is_shape_cylinder(area.flags))
        area_sdist = sdCappedCylinder(pos, area.extents.y, area.radius);
    if (is_shape_sphere(area.flags))
        area_sdist = sdSphere(pos, area.radius);

    if (area.min_radius > -1.)
        area_sdist = sdOpSubtraction(sdCircle(pos.xz, area.min_radius), area_sdist);
    if (area.angle > -1.)
        area_sdist = sdOpIntersection(area_sdist, sdAngle(pos.xz, radians(area.angle) * 0.5));

    return area_sdist;
}


float get_scene_distance(sampler2D from, vec3 pos) {
    SceneData scene; TelegraphData telegraph; AreaData area;
    read_scene_data(scene, from);
    int telegraph_idx = 0, area_idx = 0;
    float scene_sdist = 1. / 0.;

    while (telegraph_idx < scene.telegraph_count) {
        read_telegraph_data(telegraph, from, telegraph_idx);
        float telegraph_sdist = 1. / 0.;
        while (area_idx < telegraph.last_area_idx && area_idx < scene.area_count) {
            read_area_data(area, from, area_idx);
			vec3 area_local_offset = fn_local_offset(area.origin, area.basis, pos);
            float area_sdist = get_area_sdist(area, area_local_offset);
            if (is_op_union(area.flags))
                telegraph_sdist = sdOpUnion(telegraph_sdist, area_sdist);
            else if (is_op_intersection(area.flags))
                telegraph_sdist = sdOpIntersection(telegraph_sdist, area_sdist);
            else if (is_op_subtraction(area.flags))
                telegraph_sdist = sdOpSubtraction(telegraph_sdist, area_sdist);
            area_idx += 1;
        }
        scene_sdist = sdOpUnion(scene_sdist, telegraph_sdist);
        telegraph_idx += 1;
    }

    return scene_sdist;
}


vec3 get_normal(sampler2D from, vec3 pos) {
    vec2 e = vec2(1e-3, 0);
    vec3 n = get_scene_distance(from, pos) - vec3(
        get_scene_distance(from, pos - e.xyy),
        get_scene_distance(from, pos - e.yxy),
        get_scene_distance(from, pos - e.yyx)
    );
    return normalize(n);
}


vec4 raymarch_color(sampler2D from, vec3 ray_origin, vec3 ray_dir, float linear_depth) {
    vec4 color = vec4(0.);
    float total_ray_dist = 0.;
    vec3 ray_pos = ray_origin;
    int intersection_count = 0;

    for (int i = 0; i < MAX_STEPS; i++) {
        float sdist = get_scene_distance(from, ray_pos);

        if (intersection_count % 2 == 0) {
            total_ray_dist += sdist;
            ray_pos += sdist * ray_dir;
        }

        if (total_ray_dist > MAX_DISTANCE || total_ray_dist >= linear_depth)
            break;

        if (intersection_count % 2 == 0 && sdist < MIN_DISTANCE) {
            intersection_count += 1;
            color = mix(color, vec4(get_normal(from, ray_pos), 0.3), 1. / float(intersection_count));
        } else if (intersection_count % 2 != 0) {
            if (sdist > MIN_DISTANCE) {
                intersection_count += 1;
                color = mix(color, vec4(get_normal(from, ray_pos), 0.2), 1. / float(intersection_count));
            }
            float length_to_add = max(-sdist, 0.001);
            total_ray_dist += length_to_add;
            ray_pos += length_to_add * ray_dir;
        }
    }

    return color;
}
