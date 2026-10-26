"""Create a static wide battle stance inspired by the supplied pose reference."""

import datetime
import math
import os
import tempfile

import bpy
from mathutils import Matrix, Quaternion, Vector


def update(arm):
    arm.update_tag()
    bpy.context.view_layer.update()


def set_position(arm, name, position):
    bone = arm.pose.bones[name]
    matrix = bone.matrix.copy()
    matrix.translation = Vector(position)
    bone.matrix = matrix
    update(arm)


def set_orientation(arm, name, rotation):
    bone = arm.pose.bones[name]
    matrix = rotation.to_matrix().to_4x4()
    matrix.translation = bone.head
    bone.matrix = matrix


def vertices(mesh):
    obj = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    data = obj.to_mesh()
    points = [v.co.copy() for v in data.vertices]
    obj.to_mesh_clear()
    return points


def create_pre_battle():
    arm = bpy.data.objects['Armature']
    mesh = bpy.data.objects['CharacterMesh']
    scene = bpy.context.scene
    animation = arm.animation_data
    assert 'PreBattle' not in bpy.data.actions, 'PreBattle already exists'
    assert not animation.use_tweak_mode
    if bpy.context.screen and bpy.context.screen.is_animation_playing:
        bpy.ops.screen.animation_cancel(restore_frame=False)
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    backup = os.path.join(tempfile.gettempdir(), 'pickleknight-before-pre-battle-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S') + '.blend')
    bpy.ops.wm.save_as_mainfile(filepath=backup, copy=True)
    animation.use_nla = False
    animation.action = bpy.data.actions['TPose']
    arm.data.pose_position = 'POSE'
    for bone in arm.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
    scene.frame_set(1)
    action = bpy.data.actions.new('PreBattle')
    action.use_fake_user = True
    action['StaticPose'] = True
    action['PoseDescription'] = 'Wide low battle stance, left fist extended, right arm raised, head turned left. Reference adapted to short limbs and armor.'
    animation.action = action
    for bone in arm.pose.bones:
        for constraint in bone.constraints:
            constraint.influence = 1
    hip = arm.pose.bones['HipControl']
    matrix = hip.matrix.copy()
    matrix.translation += Vector((0, -0.05, -0.12))
    hip.matrix = matrix
    update(arm)
    chest = arm.pose.bones['ChestIk']
    set_orientation(arm, chest.name, Quaternion((0, 0, 1), math.radians(12)) @ chest.matrix.to_quaternion())
    update(arm)
    for side, y, x, yaw in (('Left', 0.28, -0.005, 35), ('Right', -0.25, 0.015, -35)):
        target = arm.pose.bones[side + 'FootIk']
        matrix = (Quaternion((0, 0, 1), math.radians(yaw)).to_matrix().to_4x4() @ target.bone.matrix_local)
        matrix.translation = Vector((x, y, target.bone.head_local.z))
        target.matrix = matrix
    set_position(arm, 'RightKneePole', (0.20, -0.35, 0.12))
    set_position(arm, 'LeftKneePole', (0.15, 0.27, 0.14))
    update(arm)
    left_shoulder = arm.pose.bones['LeftUpperArm'].head.copy()
    right_shoulder = arm.pose.bones['RightUpperArm'].head.copy()
    set_position(arm, 'LeftHandIk', left_shoulder + Vector((0.025, 0.232, 0.005)))
    left_hand_rotation = arm.data.bones['LeftHand'].matrix_local.to_quaternion()
    set_orientation(arm, 'LeftHandIk', Quaternion((1, 0, 0), math.radians(10)) @ left_hand_rotation)
    set_position(arm, 'LeftElbowPole', left_shoulder + Vector((0.15, 0.12, -0.12)))
    set_position(arm, 'RightHandIk', right_shoulder + Vector((-0.015, -0.03, 0.14)))
    right_hand_rotation = arm.data.bones['RightHand'].matrix_local.to_quaternion()
    set_orientation(arm, 'RightHandIk', Quaternion((1, 0, 0), math.radians(-135)) @ right_hand_rotation)
    set_position(arm, 'RightElbowPole', right_shoulder + Vector((-0.04, -0.24, 0.13)))
    update(arm)
    head = arm.pose.bones['HeadControl']
    set_orientation(arm, head.name, Quaternion((0, 0, 1), math.radians(35)) @ head.matrix.to_quaternion())
    update(arm)
    contacts = {side: [v.index for v in mesh.data.vertices if abs(v.co.z) < 1e-5 and (v.co.y > 0 if side == 'Left' else v.co.y < 0)] for side in ('Left', 'Right')}
    for _ in range(3):
        points = vertices(mesh)
        for side, indices in contacts.items():
            target = arm.pose.bones[side + 'FootIk']
            matrix = target.matrix.copy()
            matrix.translation.z -= min(points[i].z for i in indices)
            target.matrix = matrix
        update(arm)
    for bone in arm.pose.bones:
        rotation = {'QUATERNION': 'rotation_quaternion', 'AXIS_ANGLE': 'rotation_axis_angle'}.get(bone.rotation_mode, 'rotation_euler')
        for path in ('location', rotation, 'scale'):
            bone.keyframe_insert(data_path=path, frame=1, group=bone.name)
        for constraint in bone.constraints:
            constraint.keyframe_insert(data_path='influence', frame=1, group=bone.name)
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for key in curve.keyframe_points:
                        key.interpolation = 'CONSTANT'
    track = animation.nla_tracks.new()
    track.name = 'PreBattle'
    strip = track.strips.new('PreBattle', 1, action)
    strip.action_slot = animation.action_slot
    strip.extrapolation = 'NOTHING'
    track.mute = True
    scene.use_preview_range = False
    scene.frame_start = scene.frame_end = 1
    scene.frame_set(1)
    update(arm)
    reference = vertices(mesh)
    static_error = 0
    for frame in (1, 15, 60):
        scene.frame_set(frame)
        static_error = max(static_error, max((a - b).length for a, b in zip(reference, vertices(mesh))) * 1000)
    scene.frame_set(1)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='POSE')
    for bone in arm.pose.bones:
        bone.select = False
    arm.pose.bones['HipControl'].select = True
    arm.data.bones.active = arm.data.bones['HipControl']
    feet = {side: {'lowest_contact_mm': min(reference[i].z for i in ids) * 1000, 'contact_span_mm': (max(reference[i].z for i in ids) - min(reference[i].z for i in ids)) * 1000, 'ik_error_mm': (arm.pose.bones[side + 'Foot'].head - arm.pose.bones[side + 'FootIk'].head).length * 1000} for side, ids in contacts.items()}
    return {'backup': backup, 'action': action.name, 'static_mesh_error_mm': static_error, 'feet': feet, 'lowest_mesh_z_mm': min(v.z for v in reference) * 1000}


if __name__ == '__main__':
    print(create_pre_battle())
