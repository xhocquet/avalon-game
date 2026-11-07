# Adapted from dmlary/godot-stencil-based-outline-compositor-effect (MIT).
extends CompositorEffect
class_name StencilBasedOutlineCompositorEffect
@export var outline_color := Color.GOLD
@export_range(0.5, 16.0) var outline_width := 2.0
@export_range(0.0, 1.0) var glow_opacity := 0.4
@export_range(0, 181) var thickness : int= 8 :
    set(value):
        value = clampi(value, 0, 181)
        thickness = value
        _passes = 0
        while value > 0:
            value = value >> 1
            _passes += 1
@export var stencil_value := 1
@export var stencil_mask := 1
var _passes := 4
var _shader_dir = get_script().get_path().get_base_dir() + "/shaders/"
var jf_shader_file = _shader_dir + "jump_flood.glsl"
var sc_shader_file = _shader_dir + "stencil_copy.glsl"
var do_shader_file = _shader_dir + "draw_outline.glsl"

var rd: RenderingDevice
var sc_shader: RID
var sc_uniform_set: RID
var sc_framebuffer: RID
var sc_pipeline: RID
var do_shader: RID
var do_pipeline: RID
var scdo_vertex_format : int
var scdo_vertex_buffer : RID
var scdo_vertex_array : RID
var scdo_uniform_buffer : RID
var jf_shader: RID
var jf_pipeline: RID
var jf_uniform_sets := [RID(), RID()]
var color_texture: RID
var depth_texture: RID
var resolution := Vector2i(1, 1)
var _textures := [RID(), RID()]
var debug_textures := [Texture2DRD.new(), Texture2DRD.new()]
var mutex := Mutex.new()
@export var rebuild_pipelines := true :
    set(value):
        mutex.lock()
        rebuild_pipelines = value
        mutex.unlock()
func _init():
    effect_callback_type = CompositorEffect.EFFECT_CALLBACK_TYPE_POST_TRANSPARENT

func _initialize_device():
    rd = RenderingServer.get_rendering_device()
    var vertex_attr = RDVertexAttribute.new()
    vertex_attr.location = 0
    vertex_attr.format = RenderingDevice.DATA_FORMAT_R32G32B32_SFLOAT
    vertex_attr.stride = 4 * 3
    scdo_vertex_format = rd.vertex_format_create([vertex_attr])
    var vertex_data = PackedVector3Array([
        Vector3(-1, -1, 0),
        Vector3(3, -1, 0),
        Vector3(-1, 3, 0),
    ])
    var vertex_bytes = vertex_data.to_byte_array()
    scdo_vertex_buffer = rd.vertex_buffer_create(vertex_bytes.size(), vertex_bytes)
    scdo_vertex_array = rd.vertex_array_create(3, scdo_vertex_format, [scdo_vertex_buffer])
    var buffer = PackedFloat32Array([1, 1, 0, 0]).to_byte_array()
    scdo_uniform_buffer = rd.uniform_buffer_create(buffer.size(), buffer)
    rebuild_pipelines = true
var _released := false

func release_resources():
    enabled = false
    RenderingServer.call_on_render_thread(_release_resources)

func _release_resources():
    if _released or not rd:
        return
    _released = true
    for texture in debug_textures:
        texture.texture_rd_rid = RID()
    for rid in [sc_framebuffer, jf_shader, sc_shader, do_shader,
            scdo_vertex_array, scdo_vertex_buffer, scdo_uniform_buffer] + _textures:
        if rid.is_valid():
            rd.free_rid(rid)

func _notification(what):
    if what == NOTIFICATION_PREDELETE and not _released and rd:
        for rid in [sc_framebuffer, jf_shader, sc_shader, do_shader,
                scdo_vertex_array, scdo_vertex_buffer, scdo_uniform_buffer] + _textures:
            if rid.is_valid():
                rd.free_rid(rid)
func _load_glsl_from_file(path) -> RDShaderSPIRV:
    var shader_file: RDShaderFile = load(path)
    return shader_file.get_spirv()

func _build_sc_pipeline():
    if sc_shader.is_valid():
        rd.free_rid(sc_shader)
        sc_shader = RID()
    var shader_spirv = _load_glsl_from_file(sc_shader_file)
    if not shader_spirv:
        push_error("failed to load stencil copy shader")
        return
    sc_shader = rd.shader_create_from_spirv(shader_spirv)
    assert(sc_shader.is_valid())
    assert(scdo_uniform_buffer.is_valid())
    var uniform = RDUniform.new()
    uniform.uniform_type = RenderingDevice.UNIFORM_TYPE_UNIFORM_BUFFER
    uniform.binding = 0
    uniform.add_id(scdo_uniform_buffer)
    sc_uniform_set = rd.uniform_set_create([uniform], sc_shader, 0)
    assert(_textures[0].is_valid())
    assert(depth_texture.is_valid())

    var attachments = []
    var attachment_format = RDAttachmentFormat.new()
    var texture_format = rd.texture_get_format(_textures[0])
    attachment_format.format = texture_format.format
    attachment_format.usage_flags = texture_format.usage_bits
    attachment_format.samples = RenderingDevice.TEXTURE_SAMPLES_1
    attachments.push_back(attachment_format)
    var depth_format = rd.texture_get_format(depth_texture)
    attachment_format = RDAttachmentFormat.new()
    attachment_format.format = depth_format.format
    attachment_format.usage_flags = depth_format.usage_bits
    attachment_format.samples = RenderingDevice.TEXTURE_SAMPLES_1
    attachments.push_back(attachment_format)

    var format = rd.framebuffer_format_create(attachments)
    sc_framebuffer = rd.framebuffer_create([_textures[0], depth_texture], format)
    assert(sc_framebuffer.is_valid())
    var blend := RDPipelineColorBlendState.new()
    var blend_attachment := RDPipelineColorBlendStateAttachment.new()
    blend.attachments.push_back(blend_attachment)
    var stencil_state = RDPipelineDepthStencilState.new()
    stencil_state.enable_stencil = true
    stencil_state.front_op_compare = RenderingDevice.COMPARE_OP_EQUAL
    stencil_state.front_op_compare_mask = stencil_mask
    stencil_state.front_op_reference = stencil_value
    stencil_state.front_op_fail = RenderingDevice.STENCIL_OP_KEEP
    stencil_state.front_op_pass = RenderingDevice.STENCIL_OP_KEEP
    stencil_state.back_op_compare = RenderingDevice.COMPARE_OP_EQUAL
    stencil_state.back_op_compare_mask = stencil_mask
    stencil_state.back_op_reference = stencil_value
    stencil_state.back_op_fail = RenderingDevice.STENCIL_OP_KEEP
    stencil_state.back_op_pass = RenderingDevice.STENCIL_OP_KEEP
    sc_pipeline = rd.render_pipeline_create(
        sc_shader,
        format,
        scdo_vertex_format,
        RenderingDevice.RENDER_PRIMITIVE_TRIANGLES,
        RDPipelineRasterizationState.new(),
        RDPipelineMultisampleState.new(),
        stencil_state,
        blend,
    )
    assert(sc_pipeline.is_valid())
func _build_do_pipeline():
    if do_shader.is_valid():
        rd.free_rid(do_shader)
        do_shader = RID()
    var shader_spirv = _load_glsl_from_file(do_shader_file)
    if not shader_spirv:
        push_error("failed to load stencil copy shader")
        return
    do_shader = rd.shader_create_from_spirv(shader_spirv)
    assert(do_shader.is_valid())

    do_pipeline = rd.compute_pipeline_create(do_shader)
    assert(do_pipeline.is_valid())

func _build_jf_pipeline():

    if jf_shader.is_valid():
        rd.free_rid(jf_shader)
        jf_shader = RID()
    var shader_spirv = _load_glsl_from_file(jf_shader_file)
    if not shader_spirv:
        push_error("failed to load jump flood shader")
        return
    jf_shader = rd.shader_create_from_spirv(shader_spirv)
    assert(jf_shader.is_valid())
    jf_pipeline = rd.compute_pipeline_create(jf_shader)
    assert(jf_pipeline.is_valid())
    assert(_textures[0].is_valid())
    assert(_textures[1].is_valid())
    for group in [[0, _textures[0], _textures[1]],
                  [1, _textures[1], _textures[0]]]:
        var pass_number = group[0]
        var src_texture = group[1]
        var dest_texture = group[2]
        jf_uniform_sets[pass_number] = [RID(), RID()]

        var src_uniform := RDUniform.new()
        src_uniform.uniform_type = RenderingDevice.UNIFORM_TYPE_IMAGE
        src_uniform.binding = 0
        src_uniform.add_id(src_texture)

        var dest_uniform = RDUniform.new()
        dest_uniform.uniform_type = RenderingDevice.UNIFORM_TYPE_IMAGE
        dest_uniform.binding = 1
        dest_uniform.add_id(dest_texture)

        jf_uniform_sets[pass_number] = rd.uniform_set_create(
            [src_uniform, dest_uniform], jf_shader, 0)
func _build_textures():
    var count = _textures.size()
    var texture_format = RDTextureFormat.new()
    texture_format.texture_type = RenderingDevice.TEXTURE_TYPE_2D
    texture_format.width = resolution.x
    texture_format.height = resolution.y
    texture_format.format = RenderingDevice.DATA_FORMAT_R32G32_SFLOAT
    texture_format.usage_bits = (
        RenderingDevice.TEXTURE_USAGE_COLOR_ATTACHMENT_BIT |
        RenderingDevice.TEXTURE_USAGE_SAMPLING_BIT |
        RenderingDevice.TEXTURE_USAGE_STORAGE_BIT |
        RenderingDevice.TEXTURE_USAGE_CAN_COPY_FROM_BIT
    )

    var texture_view = RDTextureView.new()
    for i in range(count):
        var rid: RID = rd.texture_create(texture_format, texture_view)
        assert(rid.is_valid())
        var old_rid: RID = _textures[i]
        _textures[i] = rid
        debug_textures[i].texture_rd_rid = rid

        if old_rid.is_valid():
            rd.free_rid(old_rid)
func _update_common_buffers():
    assert(scdo_uniform_buffer.is_valid())
    var buffer = PackedFloat32Array([resolution.x, resolution.y, 0, 0]).to_byte_array()
    rd.buffer_update(scdo_uniform_buffer, 0, buffer.size(), buffer)
func _render_callback(_p_effect_callback_type, p_render_data):
    var rebuild := false

    if _released or thickness == 0:
        return
    if not rd:
        _initialize_device()
    if not rd:
        return
    var render_scene_buffers: RenderSceneBuffersRD = p_render_data.get_render_scene_buffers()
    if not render_scene_buffers:
        return
    var size = render_scene_buffers.get_internal_size()
    if size.x == 0 or size.y == 0:
        return
    if rebuild_pipelines:
        mutex.lock()
        rebuild_pipelines = false
        mutex.unlock()
        rebuild = true
    if resolution != size:
        resolution = size
        rebuild = true
    var color_tex = render_scene_buffers.get_color_layer(0)
    if color_tex != color_texture:
        color_texture = color_tex
        rebuild = true
    var depth_tex = render_scene_buffers.get_depth_layer(0)
    if depth_tex != depth_texture:
        depth_texture = depth_tex
        rebuild = true

    if rebuild:
        _build_textures()
        _update_common_buffers()
        _build_sc_pipeline()
        _build_jf_pipeline()
        _build_do_pipeline()
    var draw_list := rd.draw_list_begin(
        sc_framebuffer,
        RenderingDevice.DRAW_CLEAR_COLOR_0,
        [Color(-1, -1, 2**15, -1)],
        1.0,
        0,
        Rect2(),
        RenderingDevice.OPAQUE_PASS)
    rd.draw_list_bind_render_pipeline(draw_list, sc_pipeline)
    rd.draw_list_bind_vertex_array(draw_list, scdo_vertex_array)
    rd.draw_list_bind_uniform_set(draw_list, sc_uniform_set, 0)
    rd.draw_list_draw(draw_list, false, 3)
    rd.draw_list_end()
    @warning_ignore("integer_division")
    var x_groups : int = (resolution.x - 1) / 8 + 1
    @warning_ignore("integer_division")
    var y_groups : int = (resolution.y - 1) / 8 + 1
    var push_constant := PackedByteArray()
    push_constant.resize(16) # Must be a multiple of 16 bytes
    var compute_list := rd.compute_list_begin()
    rd.compute_list_bind_compute_pipeline(compute_list, jf_pipeline)

    for i in range(_passes):
        var stride = (1<<(_passes-i-1))
        push_constant.encode_u32(0, stride)
        rd.compute_list_set_push_constant(compute_list, push_constant, push_constant.size())
        rd.compute_list_bind_uniform_set(
            compute_list,
            jf_uniform_sets[i & 0x1],
            0)
        rd.compute_list_dispatch(compute_list, x_groups, y_groups, 1)
        rd.compute_list_add_barrier(compute_list)

    rd.compute_list_end()
    var src_uniform := RDUniform.new()
    src_uniform.uniform_type = RenderingDevice.UNIFORM_TYPE_IMAGE
    src_uniform.binding = 0
    src_uniform.add_id(_textures[_passes & 0x1])
    var dest_uniform = RDUniform.new()
    dest_uniform.uniform_type = RenderingDevice.UNIFORM_TYPE_IMAGE
    dest_uniform.binding = 1
    dest_uniform.add_id(color_texture)
    var uniform_set = UniformSetCacheRD.get_cache(do_shader, 0, [src_uniform, dest_uniform])
    assert(uniform_set.is_valid())
    var do_push_constant = PackedByteArray()
    do_push_constant.resize(32)
    do_push_constant.encode_float(0, outline_color.r)
    do_push_constant.encode_float(4, outline_color.g)
    do_push_constant.encode_float(8, outline_color.b)
    do_push_constant.encode_float(12, outline_color.a)
    do_push_constant.encode_u32(16, thickness**2)
    do_push_constant.encode_float(20, outline_width)
    do_push_constant.encode_float(24, glow_opacity)

    compute_list = rd.compute_list_begin()
    rd.compute_list_bind_compute_pipeline(compute_list, do_pipeline)
    rd.compute_list_bind_uniform_set(compute_list, uniform_set, 0)
    rd.compute_list_set_push_constant(compute_list, do_push_constant, do_push_constant.size())
    rd.compute_list_dispatch(compute_list, x_groups, y_groups, 1)
    rd.compute_list_end()
