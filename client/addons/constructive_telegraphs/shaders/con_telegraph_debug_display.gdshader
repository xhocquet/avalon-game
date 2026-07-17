shader_type spatial;
render_mode unshaded;
#include "res://addons/constructive_telegraphs/shaders/inc/con_telegraph_debug_display.gdshaderinc"

uniform sampler2D data_transfer_texture;
uniform sampler2D depth_texture : source_color, hint_depth_texture;


void vertex() {
    POSITION = vec4(VERTEX.xy, 1., 1.);
}


void fragment() {
    float linear_depth = get_linear_depth(depth_texture, SCREEN_UV, INV_PROJECTION_MATRIX);
    vec3 ray_dir = get_ray_dir(SCREEN_UV, INV_VIEW_MATRIX, INV_PROJECTION_MATRIX);
    vec3 ray_origin = INV_VIEW_MATRIX[3].xyz;
    vec4 color = raymarch_color(data_transfer_texture, ray_origin, ray_dir, linear_depth);
    ALBEDO = color.rgb;
    ALPHA = color.a;
}
