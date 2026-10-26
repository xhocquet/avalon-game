"""Run in Blender's Text Editor on the normalized pickleknight rig."""

import math

import bpy
from mathutils import Quaternion, Vector


def action_curves(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                yield from bag.fcurves


def create_idle():
    arm = bpy.data.objects['Armature']
    scene = bpy.context.scene
    animation = arm.animation_data
    assert not animation.use_tweak_mode, 'Exit NLA tweak mode first'
    assert 'IdleSource' not in bpy.data.actions, 'New idle already created'
    source = bpy.data.actions['Idle']
    if bpy.context.screen and bpy.context.screen.is_animation_playing:
        bpy.ops.screen.animation_cancel(restore_frame=False)

    animation.use_nla = False
    animation.action = source
    scene.frame_set(150)
    base = {
        bone.name: (bone.location.copy(), bone.rotation_quaternion.copy(), bone.scale.copy())
        for bone in arm.pose.bones
    }
    mesh = bpy.data.objects['CharacterMesh']
    evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    geometry = evaluated.to_mesh()
    floor = min((evaluated.matrix_world @ vertex.co).z for vertex in geometry.vertices)
    evaluated.to_mesh_clear()
    root = arm.pose.bones['Root']
    offset = root.bone.matrix_local.to_3x3().inverted() @ arm.matrix_world.to_3x3().inverted() @ Vector((0, 0, -floor))
    root_loc, root_rot, root_scale = base['Root']
    base['Root'] = (root_loc + offset, root_rot, root_scale)
    lateral = Vector((0, 1, 0))
    forward = Vector((1, 0, 0))
    axes = {
        bone.name: (
            (arm.matrix_world @ bone.matrix).to_3x3().inverted() @ lateral,
            (arm.matrix_world @ bone.matrix).to_3x3().inverted() @ forward,
        )
        for bone in arm.pose.bones
    }

    source.name = 'IdleSource'
    source.use_fake_user = True
    for track in animation.nla_tracks:
        for strip in track.strips:
            if strip.action == source:
                strip.name = source.name
                track.name = source.name

    action = bpy.data.actions.new('Idle')
    action.use_fake_user = True
    animation.action = action
    animation.action_blend_type = 'REPLACE'
    animation.action_influence = 1.0
    cycle = 48

    # Frame 49 duplicates frame 1; preview excludes the duplicate.
    for frame in range(1, cycle + 2):
        phase = 2 * math.pi * (frame - 1) / cycle
        breath = math.sin(phase)
        delayed = math.sin(phase - 0.4)
        for bone in arm.pose.bones:
            loc, rot, scale = base[bone.name]
            bone.location = loc
            bone.rotation_quaternion = rot
            bone.scale = scale
            pitch_axis, roll_axis = axes[bone.name]
            pitch = roll = 0.0
            if bone.name == 'Spine01':
                pitch = 0.65 * breath
            elif bone.name == 'Spine02':
                pitch = 0.85 * breath
                bone.scale = scale * (1.0 + 0.003 * breath)
            elif bone.name == 'Head':
                pitch = -0.7 * delayed
                roll = 0.35 * delayed
            elif bone.name in ('LeftUpperArm', 'RightUpperArm'):
                side = 1 if bone.name.startswith('Left') else -1
                pitch = 0.65 * delayed * side
            elif bone.name in ('LeftForeArm', 'RightForeArm'):
                pitch = 0.4 * delayed
            bone.rotation_quaternion = rot @ Quaternion(pitch_axis, math.radians(pitch)) @ Quaternion(roll_axis, math.radians(roll))
            for path in ('location', 'rotation_quaternion', 'scale'):
                bone.keyframe_insert(data_path=path, frame=frame, group=bone.name)

    for curve in action_curves(action):
        for key in curve.keyframe_points:
            key.interpolation = 'LINEAR'
        curve.modifiers.new('CYCLES')

    scene.frame_start = 1
    scene.frame_end = cycle
    scene.use_preview_range = False
    scene.frame_set(1)
    return {'action': action.name, 'source': source.name, 'preview': [1, cycle], 'keys': [1, cycle + 1]}


if __name__ == '__main__':
    print(create_idle())
