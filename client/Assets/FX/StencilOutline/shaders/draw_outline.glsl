#[compute]
#version 450

// Invocations in the (x, y, z) dimension.
layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

// Our textures.
layout(rg32f, set = 0, binding = 0) uniform image2D u_src_image;
layout(rgba16f, set = 0, binding = 1) uniform image2D u_dest_image;

// Our push PushConstant.
layout(push_constant, std430) uniform Params {
    vec4 color;
    uint dist;
    float outline_width;
    float glow_opacity;
} params;

// perform a single pass of jump flood from image_0 to image_1
//
// Arguments:
//  stride: distance around UV to sample
void main() {
    ivec2 image_size = imageSize(u_src_image);
    ivec2 coord = ivec2(gl_GlobalInvocationID.xy);
    if (any(greaterThanEqual(coord, image_size))) return;
    vec4 output_color = imageLoad(u_dest_image, coord);

    // pull the jump-flood value for this point
    vec4 value = imageLoad(u_src_image, coord);
    vec2 delta = vec2(coord) - value.rg;
    float distance_squared = dot(delta, delta);
    if (value.r >= 0 && distance_squared > 0 && distance_squared <= params.dist){
        float distance = sqrt(distance_squared);
        float edge = 1.0 - smoothstep(params.outline_width - 0.5, params.outline_width + 0.5, distance);
        float glow = params.glow_opacity * pow(max(0.0, 1.0 - distance / sqrt(float(params.dist))), 2.0);
        output_color.rgb = mix(output_color.rgb, params.color.rgb, max(edge, glow) * params.color.a);
    }

    imageStore(u_dest_image, coord, output_color);
}
