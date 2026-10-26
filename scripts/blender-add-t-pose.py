"""Capture the current rest pose as a static TPose action and NLA track."""

import datetime
import os
import tempfile

import bpy
from mathutils import Matrix


def add_t_pose():
    arm = bpy.data.objects['Armature']
    mesh = bpy.data.objects['CharacterMesh']
    scene = bpy.context.scene
    animation = arm.animation_data
    assert 'TPose' not in bpy.data.actions, 'TPose already exists'
    assert not animation.use_tweak_mode
    if bpy.context.screen and bpy.context.screen.is_animation_playing:
        bpy.ops.screen.animation_cancel(restore_frame=False)
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    backup = os.path.join(tempfile.gettempdir(), 'pickleknight-before-t-pose-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S') + '.blend')
    bpy.ops.wm.save_as_mainfile(filepath=backup, copy=True)
    animation.use_nla = False
    arm.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    data = evaluated.to_mesh()
    rest = [v.co.copy() for v in data.vertices]
    evaluated.to_mesh_clear()

    action = bpy.data.actions.new('TPose')
    action.use_fake_user = True
    animation.action = action
    for bone in arm.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
        rotation = {'QUATERNION': 'rotation_quaternion', 'AXIS_ANGLE': 'rotation_axis_angle'}.get(bone.rotation_mode, 'rotation_euler')
        for frame in (1, 2):
            for path in ('location', rotation, 'scale'):
                bone.keyframe_insert(data_path=path, frame=frame, group=bone.name)
            for constraint in bone.constraints:
                constraint.influence = 0
                constraint.keyframe_insert(data_path='influence', frame=frame, group=bone.name)
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for key in curve.keyframe_points:
                        key.interpolation = 'CONSTANT'
    track = animation.nla_tracks.new()
    track.name = 'TPose'
    strip = track.strips.new('TPose', 1, action)
    strip.action_slot = animation.action_slot
    strip.extrapolation = 'NOTHING'
    strip.blend_type = 'REPLACE'
    track.mute = True
    arm.data.pose_position = 'POSE'
    scene.frame_start, scene.frame_end = 1, 2
    scene.use_preview_range = False
    arm.update_tag()
    errors = []
    for frame, subframe in ((1, 0), (1, 0.5), (2, 0)):
        scene.frame_set(frame, subframe=subframe)
        evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
        data = evaluated.to_mesh()
        errors.append(max((v.co - expected).length for v, expected in zip(data.vertices, rest)))
        evaluated.to_mesh_clear()
    scene.frame_set(1)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='POSE')
    assert max(errors) < 1e-6, 'Pose does not match rest geometry'
    return {'action': action.name, 'track': track.name, 'frames': list(action.frame_range), 'max_rest_error_mm': max(errors) * 1000, 'backup': backup, 'nla_enabled': animation.use_nla}


if __name__ == '__main__':
    print(add_t_pose())
