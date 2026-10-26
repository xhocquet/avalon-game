"""Create a single-frame standing A-pose named Idle; retain the motion source."""

import datetime
import math
import os
import tempfile

import bpy
from mathutils import Matrix, Quaternion


def vertices(mesh):
    obj = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    data = obj.to_mesh()
    points = [v.co.copy() for v in data.vertices]
    obj.to_mesh_clear()
    return points


def create_idle_pose():
    arm = bpy.data.objects['Armature']
    mesh = bpy.data.objects['CharacterMesh']
    scene = bpy.context.scene
    animation = arm.animation_data
    assert 'IdleSource' not in bpy.data.actions, 'IdleSource already exists; inspect before repeating'
    assert 'TPose' in bpy.data.actions and 'Idle' in bpy.data.actions
    assert not animation.use_tweak_mode
    if bpy.context.screen and bpy.context.screen.is_animation_playing:
        bpy.ops.screen.animation_cancel(restore_frame=False)
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    backup = os.path.join(tempfile.gettempdir(), 'pickleknight-before-static-idle-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S') + '.blend')
    bpy.ops.wm.save_as_mainfile(filepath=backup, copy=True)
    animation.use_nla = False
    arm.data.pose_position = 'POSE'
    animation.action = bpy.data.actions['TPose']
    for bone in arm.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
    scene.frame_set(1)
    feet_before = {side: arm.pose.bones[side + 'Foot'].matrix.copy() for side in ('Left', 'Right')}
    lowering = {}
    for side, sign in (('Left', -1), ('Right', 1)):
        pivot = arm.pose.bones[side + 'UpperArm'].head.copy()
        target = arm.pose.bones[side + 'HandIk']
        vector = target.head - pivot
        angle = math.radians(45) - math.atan2(-vector.z, abs(vector.y))
        transform = Matrix.Translation(pivot) @ Quaternion((1, 0, 0), sign * angle).to_matrix().to_4x4() @ Matrix.Translation(-pivot)
        matrices = {suffix: transform @ arm.pose.bones[side + suffix].matrix for suffix in ('HandIk', 'ElbowPole')}
        for suffix, matrix in matrices.items():
            arm.pose.bones[side + suffix].matrix = matrix
        lowering[side] = math.degrees(angle)
    arm.update_tag()
    bpy.context.view_layer.update()
    basis = {b.name: b.matrix_basis.copy() for b in arm.pose.bones}
    influences = {(b.name, c.name): c.influence for b in arm.pose.bones for c in b.constraints}
    source = bpy.data.actions['Idle']
    source.name = 'IdleSource'
    source.use_fake_user = True
    for track in animation.nla_tracks:
        for strip in track.strips:
            if strip.action == source:
                strip.name = 'IdleSource'
                track.name = 'IdleSource'
    action = bpy.data.actions.new('Idle')
    action.use_fake_user = True
    action['StaticPose'] = True
    action['PoseDescription'] = 'Standing A-pose; arms approximately 45 degrees below horizontal; no motion.'
    animation.action = action
    for bone in arm.pose.bones:
        bone.matrix_basis = basis[bone.name]
        rotation = {'QUATERNION': 'rotation_quaternion', 'AXIS_ANGLE': 'rotation_axis_angle'}.get(bone.rotation_mode, 'rotation_euler')
        for path in ('location', rotation, 'scale'):
            bone.keyframe_insert(data_path=path, frame=1, group=bone.name)
        for constraint in bone.constraints:
            constraint.influence = influences[bone.name, constraint.name]
            constraint.keyframe_insert(data_path='influence', frame=1, group=bone.name)
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for key in curve.keyframe_points:
                        key.interpolation = 'CONSTANT'
    track = animation.nla_tracks.new()
    track.name = 'Idle'
    strip = track.strips.new('Idle', 1, action)
    strip.action_slot = animation.action_slot
    strip.extrapolation = 'NOTHING'
    track.mute = True
    scene.use_preview_range = False
    scene.frame_start = scene.frame_end = 1
    scene.frame_set(1)
    arm.update_tag()
    bpy.context.view_layer.update()
    reference = vertices(mesh)
    errors = []
    for frame in (1, 15, 60):
        scene.frame_set(frame)
        errors.append(max((a - b).length for a, b in zip(reference, vertices(mesh))) * 1000)
    scene.frame_set(1)
    feet_error = max(max(abs(arm.pose.bones[side + 'Foot'].matrix[i][j] - matrix[i][j]) for i in range(4) for j in range(4)) for side, matrix in feet_before.items())
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='POSE')
    for bone in arm.pose.bones:
        bone.select = False
    arm.pose.bones['LeftHandIk'].select = True
    arm.data.bones.active = arm.data.bones['LeftHandIk']
    assert max(errors) < 0.001 and feet_error < 1e-6, (errors, feet_error)
    return {'backup': backup, 'action': action.name, 'frames': list(action.frame_range), 'source': source.name, 'additional_arm_lowering_degrees': lowering, 'static_mesh_error_mm': max(errors), 'foot_matrix_error': feet_error}


if __name__ == '__main__':
    print(create_idle_pose())
