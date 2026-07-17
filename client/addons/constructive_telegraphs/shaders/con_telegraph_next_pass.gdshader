shader_type spatial;
render_mode unshaded;
#include "res://addons/constructive_telegraphs/shaders/inc/con_telegraph_next_pass.gdshaderinc"

uniform sampler2D data_transfer_texture;
uniform sampler2D noise_texture;
uniform sampler2D pattern_1_texture;

varying vec3 world_normal;
varying vec3 world_position;


void vertex() {
    world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}


void fragment() {
    vec3 dfdx = dFdx(world_position);
    vec3 dfdy = dFdy(world_position);
    vec4 decal_color = fn_decal_color(
        data_transfer_texture,
        world_normal,
        world_position,
        dfdx,
        dfdy,
        noise_texture,
        pattern_1_texture
    );
    ALBEDO = decal_color.rgb;
	ALPHA = decal_color.a;
}
