"""Add hand IK and elbow poles; preserve FK in existing motion clips."""

import datetime
import math
import os
import tempfile

import bpy
from mathutils import Matrix, Vector


def pole_point(shoulder, elbow, wrist):
    axis = (wrist - shoulder).normalized()
    bend = elbow - (shoulder + axis * (elbow - shoulder).dot(axis))
    if bend.length < 1e-6:
        bend = Vector((-1, 0, 0))
    return elbow + bend.normalized() * 0.16


def shape(collection, name, points, edges):
    data = bpy.data.meshes.new(name)
    data.from_pydata(points, edges, [])
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.hide_render = True
    obj.hide_set(True)
    return obj


def vertices(mesh):
    obj = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    data = obj.to_mesh()
    points = [v.co.copy() for v in data.vertices]
    obj.to_mesh_clear()
    return points


def add_hand_ik():
    arm = bpy.data.objects['Armature']
    mesh = bpy.data.objects['CharacterMesh']
    scene = bpy.context.scene
    animation = arm.animation_data
    assert 'LeftHandIk' not in arm.data.bones, 'Hand IK already exists'
    assert not animation.use_tweak_mode
    if bpy.context.screen and bpy.context.screen.is_animation_playing:
        bpy.ops.screen.animation_cancel(restore_frame=False)
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    backup = os.path.join(tempfile.gettempdir(), 'pickleknight-before-hand-ik-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S') + '.blend')
    bpy.ops.wm.save_as_mainfile(filepath=backup, copy=True)
    animation.use_nla = False
    arm.data.pose_position = 'POSE'
    original_action, original_frame = animation.action, scene.frame_current
    samples, references = {}, {}
    for name in ('TPose', 'Idle', 'Run'):
        animation.action = bpy.data.actions[name]
        for bone in arm.pose.bones:
            bone.matrix_basis = Matrix.Identity(4)
        start, end = map(int, animation.action.frame_range)
        samples[name] = {}
        for frame in range(start, end + 1):
            scene.frame_set(frame)
            samples[name][frame] = {}
            for side in ('Left', 'Right'):
                upper, fore, hand = [arm.pose.bones[side + suffix] for suffix in ('UpperArm', 'ForeArm', 'Hand')]
                samples[name][frame][side] = (hand.matrix.copy(), pole_point(upper.head, fore.head, hand.head))
            if frame in (start, (start + end) // 2, end):
                references[name, frame] = vertices(mesh)

    animation.action = bpy.data.actions['TPose']
    scene.frame_set(1)
    expected = {b.name: b.matrix.copy() for b in arm.pose.bones}
    controls = arm.data.collections['Controls']
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for side in ('Left', 'Right'):
        target = arm.data.edit_bones.new(side + 'HandIk')
        target.length = 0.065
        target.matrix = arm.data.edit_bones[side + 'Hand'].matrix.copy()
        target.use_deform = False
        pole = arm.data.edit_bones.new(side + 'ElbowPole')
        pole.head = samples['TPose'][1][side][1]
        pole.tail = pole.head + Vector((0, 0, 0.035))
        pole.use_deform = False
    bpy.ops.object.mode_set(mode='OBJECT')
    constraints, calibration = [], {}
    shapes = bpy.data.collections['RigShapes']
    for side in ('Left', 'Right'):
        target, pole = [arm.pose.bones[side + suffix] for suffix in ('HandIk', 'ElbowPole')]
        for bone in (target, pole):
            controls.assign(bone.bone)
            bone.rotation_mode = 'QUATERNION'
            bone.lock_scale = (True, True, True)
            bone.use_custom_shape_bone_size = False
            bone.color.palette = 'THEME04' if side == 'Left' else 'THEME03'
        pole.lock_rotation = (True, True, True)
        pole.lock_rotation_w = True
        target.custom_shape = shape(shapes, side + 'HandControlShape', [(0.045 * math.cos(i * math.tau / 40), 0, 0.045 * math.sin(i * math.tau / 40)) for i in range(40)] + [(0, 0, 0), (0, 0.065, 0)], [(i, (i + 1) % 40) for i in range(40)] + [(40, 41)])
        pole.custom_shape = shape(shapes, side + 'ElbowControlShape', [(0.018, 0, 0), (0, 0.018, 0), (-0.018, 0, 0), (0, -0.018, 0), (0, 0, 0.018), (0, 0, -0.018)], [(0, 1), (1, 2), (2, 3), (3, 0), (0, 4), (2, 4), (1, 5), (3, 5)])
        upper, fore, hand = [arm.pose.bones[side + suffix] for suffix in ('UpperArm', 'ForeArm', 'Hand')]
        ik = fore.constraints.new('IK')
        ik.name = 'HandIk'
        ik.target, ik.subtarget = arm, target.name
        ik.pole_target, ik.pole_subtarget = arm, pole.name
        ik.chain_count, ik.iterations, ik.use_stretch = 2, 128, False
        upper.ik_stretch = fore.ik_stretch = 0
        rotation = hand.constraints.new('COPY_ROTATION')
        rotation.name = 'HandIkRotation'
        rotation.target, rotation.subtarget = arm, target.name
        rotation.owner_space = rotation.target_space = 'WORLD'
        rotation.mix_mode = 'REPLACE'
        constraints.extend((ik, rotation))

        def cost(angle):
            ik.pole_angle = angle
            arm.update_tag()
            bpy.context.view_layer.update()
            return sum((arm.pose.bones[side + suffix].matrix.to_3x3() - expected[side + suffix].to_3x3()).col[i].length_squared for suffix in ('UpperArm', 'ForeArm') for i in range(3))

        step = math.tau / 72
        best = min((-math.pi + i * step for i in range(73)), key=cost)
        for _ in range(5):
            best = min((best + j * step / 10 for j in range(-10, 11) if -math.pi <= best + j * step / 10 <= math.pi), key=cost)
            step /= 10
        cost(best)
        calibration[side] = best

    for name, frames in samples.items():
        animation.action = bpy.data.actions[name]
        for frame, sides in frames.items():
            scene.frame_set(frame)
            for constraint in constraints:
                constraint.influence = 1 if name == 'TPose' else 0
                constraint.keyframe_insert(data_path='influence', frame=frame, group='HandIk')
            for side, (matrix, point) in sides.items():
                target = arm.pose.bones[side + 'HandIk']
                target.matrix = matrix
                pole = arm.pose.bones[side + 'ElbowPole']
                pole_matrix = pole.bone.matrix_local.copy()
                pole_matrix.translation = point
                pole.matrix = pole_matrix
                for control in (target, pole):
                    for path in ('location', 'rotation_quaternion', 'scale'):
                        control.keyframe_insert(data_path=path, frame=frame, group=control.name)
        for layer in animation.action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        if any(token in curve.data_path for token in ('HandIk', 'ElbowPole')):
                            for key in curve.keyframe_points:
                                key.interpolation = 'CONSTANT' if name == 'TPose' or 'constraints' in curve.data_path else 'LINEAR'

    errors = {}
    for (name, frame), points in references.items():
        animation.action = bpy.data.actions[name]
        for bone in arm.pose.bones:
            bone.matrix_basis = Matrix.Identity(4)
        scene.frame_set(frame)
        arm.update_tag()
        bpy.context.view_layer.update()
        error = max((a - b).length for a, b in zip(points, vertices(mesh))) * 1000
        errors[name] = max(error, errors.get(name, 0))
    animation.action = original_action
    scene.frame_start, scene.frame_end = map(int, original_action.frame_range)
    scene.frame_set(original_frame)
    arm.update_tag()
    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode='POSE')
    for bone in arm.pose.bones:
        bone.select = False
    arm.pose.bones['LeftHandIk'].select = True
    arm.data.bones.active = arm.data.bones['LeftHandIk']
    arm['HandIkHelp'] = 'TPose uses HandIk rings and ElbowPole diamonds. Idle/Run preserve FK; set HandIk and HandIkRotation influences to 1 and key them to enable on a new pose.'
    assert errors['Idle'] < 0.001 and errors['Run'] < 0.001, errors
    assert errors['TPose'] < 0.5, errors
    return {'backup': backup, 'controls': [s + n for s in ('Left', 'Right') for n in ('HandIk', 'ElbowPole')], 'pole_angles': calibration, 'max_mesh_error_mm': errors, 'bones': len(arm.data.bones)}


if __name__ == '__main__':
    print(add_hand_ik())
