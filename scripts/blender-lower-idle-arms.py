"""Lower the static Idle A-pose arms alongside the body."""

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


def lower_idle_arms():
    arm = bpy.data.objects['Armature']
    action = bpy.data.actions['Idle']
    scene = bpy.context.scene
    assert action.get('StaticPose') and not action.get('ArmsDown'), 'Expected the original static A-pose'
    assert not arm.animation_data.use_tweak_mode
    if bpy.context.screen and bpy.context.screen.is_animation_playing:
        bpy.ops.screen.animation_cancel(restore_frame=False)
    arm.animation_data.action = action
    arm.animation_data.use_nla = False
    scene.frame_set(1)
    backup = os.path.join(tempfile.gettempdir(), 'pickleknight-before-arms-down-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S') + '.blend')
    bpy.ops.wm.save_as_mainfile(filepath=backup, copy=True)
    feet = {s: arm.pose.bones[s + 'Foot'].matrix.copy() for s in ('Left', 'Right')}
    for side, sign in (('Left', -1), ('Right', 1)):
        pivot = arm.pose.bones[side + 'UpperArm'].head.copy()
        transform = Matrix.Translation(pivot) @ Quaternion((1, 0, 0), sign * math.radians(35)).to_matrix().to_4x4() @ Matrix.Translation(-pivot)
        matrices = {suffix: transform @ arm.pose.bones[side + suffix].matrix for suffix in ('HandIk', 'ElbowPole')}
        for suffix, matrix in matrices.items():
            matrix.translation.y += -sign * 0.025
            bone = arm.pose.bones[side + suffix]
            bone.matrix = matrix
            for path in ('location', 'rotation_quaternion', 'scale'):
                bone.keyframe_insert(data_path=path, frame=1, group=bone.name)
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for key in curve.keyframe_points:
                        key.interpolation = 'CONSTANT'
    action['ArmsDown'] = True
    action['PoseDescription'] = 'Standing with arms down alongside the body and armor clearance; single frame, no motion.'
    arm.update_tag()
    bpy.context.view_layer.update()
    mesh = bpy.data.objects['CharacterMesh']
    reference = vertices(mesh)
    error = 0
    for frame in (1, 15, 60):
        scene.frame_set(frame)
        error = max(error, max((a - b).length for a, b in zip(reference, vertices(mesh))) * 1000)
    scene.frame_start = scene.frame_end = 1
    scene.frame_set(1)
    foot_error = max(max(abs(arm.pose.bones[side + 'Foot'].matrix[i][j] - matrix[i][j]) for i in range(4) for j in range(4)) for side, matrix in feet.items())
    assert error < 0.001 and foot_error < 1e-6, (error, foot_error)
    return {'backup': backup, 'action': action.name, 'static_mesh_error_mm': error, 'foot_matrix_error': foot_error}


if __name__ == '__main__':
    print(lower_idle_arms())
