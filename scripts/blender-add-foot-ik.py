"""Add foot IK controls to the normalized pickleknight and its new Idle."""

import math

import bpy
from mathutils import Matrix, Vector


def pole_position(hip, knee, ankle):
    axis = (ankle - hip).normalized()
    projection = hip + axis * (knee - hip).dot(axis)
    bend = knee - projection
    if bend.length < 1e-6:
        bend = Vector((1, 0, 0))
    return knee + bend.normalized() * 0.22


def shape_object(collection, name, points, edges):
    data = bpy.data.meshes.new(name)
    data.from_pydata(points, edges, [])
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.hide_render = True
    return obj


def add_foot_ik():
    arm = bpy.data.objects['Armature']
    animation = arm.animation_data
    scene = bpy.context.scene
    assert arm.mode == 'OBJECT', 'Exit Pose/Edit Mode first'
    assert 'LeftFootIk' not in arm.data.bones, 'Foot IK already exists'
    assert not animation.use_tweak_mode, 'Exit NLA tweak mode first'
    if bpy.context.screen and bpy.context.screen.is_animation_playing:
        bpy.ops.screen.animation_cancel(restore_frame=False)
    idle = bpy.data.actions['Idle']
    original_action = animation.action
    original_frame = scene.frame_current
    animation.use_nla = False
    arm.data.pose_position = 'POSE'
    animation.action = idle

    samples = {}
    for frame in range(1, 50):
        scene.frame_set(frame)
        samples[frame] = {}
        for side in ('Left', 'Right'):
            thigh = arm.pose.bones[side + 'Thigh']
            calf = arm.pose.bones[side + 'Calf']
            foot = arm.pose.bones[side + 'Foot']
            samples[frame][side] = (foot.matrix.copy(), pole_position(thigh.head, calf.head, foot.head))

    animation.action = None
    for bone in arm.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    rest = {name: bone.matrix.copy() for name, bone in arm.pose.bones.items()}
    knees = {side: arm.pose.bones[side + 'Calf'].head.copy() for side in ('Left', 'Right')}
    rest_poles = {side: pole_position(arm.pose.bones[side + 'Thigh'].head, knees[side], arm.pose.bones[side + 'Foot'].head) for side in ('Left', 'Right')}

    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    deform = arm.data.collections.new('Deform')
    controls = arm.data.collections.new('Controls')
    for bone in arm.data.bones:
        deform.assign(bone)
    bpy.ops.object.mode_set(mode='EDIT')
    for side in ('Left', 'Right'):
        foot = arm.data.edit_bones.new(side + 'FootIk')
        foot.length = 0.09
        foot.matrix = rest[side + 'Foot']
        foot.use_deform = False
        pole = arm.data.edit_bones.new(side + 'KneePole')
        pole.head = rest_poles[side]
        pole.tail = pole.head + Vector((0, 0, 0.045))
        pole.use_deform = False
    bpy.ops.object.mode_set(mode='OBJECT')

    shapes = bpy.data.collections.new('RigShapes')
    scene.collection.children.link(shapes)
    shapes.hide_render = True
    constraints = []
    calibration = {}
    for side in ('Left', 'Right'):
        target = arm.pose.bones[side + 'FootIk']
        pole = arm.pose.bones[side + 'KneePole']
        for bone in (target, pole):
            controls.assign(bone.bone)
            bone.rotation_mode = 'QUATERNION'
            bone.lock_scale = (True, True, True)
            bone.use_custom_shape_bone_size = False
            bone.color.palette = 'THEME04' if side == 'Left' else 'THEME03'
        pole.lock_rotation = (True, True, True)
        pole.lock_rotation_w = True

        inverse = target.bone.matrix_local.inverted()
        ankle = target.bone.head_local
        points = []
        for index in range(40):
            angle = 2 * math.pi * index / 40
            point = Vector((ankle.x + 0.035 + 0.10 * math.cos(angle), ankle.y + 0.065 * math.sin(angle), 0.002))
            points.append(inverse @ point)
        points.extend([inverse @ Vector((ankle.x + 0.145, ankle.y, 0.002)), inverse @ Vector((ankle.x + 0.12, ankle.y - 0.012, 0.002)), inverse @ Vector((ankle.x + 0.12, ankle.y + 0.012, 0.002))])
        edges = [(i, (i + 1) % 40) for i in range(40)] + [(40, 41), (40, 42)]
        target.custom_shape = shape_object(shapes, side + 'FootControlShape', points, edges)
        pole.custom_shape = shape_object(shapes, side + 'KneeControlShape', [(0.022, 0, 0), (0, 0.022, 0), (-0.022, 0, 0), (0, -0.022, 0), (0, 0, 0.022), (0, 0, -0.022)], [(0, 1), (1, 2), (2, 3), (3, 0), (0, 4), (2, 4), (1, 5), (3, 5)])

        calf = arm.pose.bones[side + 'Calf']
        constraint = calf.constraints.new('IK')
        constraint.name = 'FootIk'
        constraint.target = arm
        constraint.subtarget = target.name
        constraint.pole_target = arm
        constraint.pole_subtarget = pole.name
        constraint.chain_count = 2
        constraint.use_stretch = False
        constraint.iterations = 128
        arm.pose.bones[side + 'Thigh'].ik_stretch = 0.0
        calf.ik_stretch = 0.0
        for bone_name in (side + 'Thigh', side + 'Calf'):
            arm.pose.bones[bone_name].lock_ik_x = False
            arm.pose.bones[bone_name].lock_ik_y = False
            arm.pose.bones[bone_name].lock_ik_z = False

        constraint.pole_angle = math.pi / 2

        orient = arm.pose.bones[side + 'Foot'].constraints.new('COPY_ROTATION')
        orient.name = 'FootIkRotation'
        orient.target = arm
        orient.subtarget = target.name
        orient.owner_space = 'WORLD'
        orient.target_space = 'WORLD'
        orient.mix_mode = 'REPLACE'
        constraints.extend([constraint, orient])

    for obj in shapes.objects:
        obj.hide_set(True)
    for action in list(bpy.data.actions):
        animation.action = action
        influence = 1.0 if action == idle else 0.0
        for constraint in constraints:
            constraint.influence = influence
            constraint.keyframe_insert(data_path='influence', frame=1, group='FootIk')

    animation.action = idle
    for frame, sides in samples.items():
        scene.frame_set(frame)
        for side, (matrix, pole_point) in sides.items():
            target = arm.pose.bones[side + 'FootIk']
            target.matrix = matrix
            pole = arm.pose.bones[side + 'KneePole']
            matrix = pole.bone.matrix_local.copy()
            matrix.translation = pole_point
            pole.matrix = matrix
            for control in (target, pole):
                for path in ('location', 'rotation_quaternion', 'scale'):
                    control.keyframe_insert(data_path=path, frame=frame, group=control.name)

    scene.frame_set(1)
    for constraint in constraints:
        constraint.mute = True
    arm.update_tag()
    bpy.context.view_layer.update()
    expected = {bone.name: bone.matrix.copy() for bone in arm.pose.bones}
    for constraint in constraints:
        constraint.mute = False
    for side in ('Left', 'Right'):
        constraint = arm.pose.bones[side + 'Calf'].constraints['FootIk']

        def cost(angle):
            constraint.pole_angle = angle
            arm.update_tag()
            bpy.context.view_layer.update()
            return sum(arm.pose.bones[side + suffix].matrix.to_quaternion().rotation_difference(expected[side + suffix].to_quaternion()).angle ** 2 for suffix in ('Thigh', 'Calf'))

        step = math.pi / 18
        best = math.pi / 2
        for _ in range(5):
            best = min((best + j * step / 10 for j in range(-10, 11)), key=cost)
            step /= 10
        constraint.pole_angle = best
        cost(best)
        calibration[side] = {'pole_angle': best, 'knee_error_mm': (arm.pose.bones[side + 'Calf'].head - expected[side + 'Calf'].translation).length * 1000}

    for layer in idle.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    if any(name in curve.data_path for name in ('FootIk', 'KneePole')):
                        for key in curve.keyframe_points:
                            key.interpolation = 'LINEAR'
                        if 'pose.bones' in curve.data_path and 'constraints' not in curve.data_path:
                            values = [key.co.y for key in curve.keyframe_points]
                            if max(values) - min(values) < 1e-6:
                                for index in reversed(range(1, len(curve.keyframe_points) - 1)):
                                    curve.keyframe_points.remove(curve.keyframe_points[index], fast=True)
                                curve.update()
                            curve.modifiers.new('CYCLES')
    arm.show_in_front = True
    for area in bpy.context.screen.areas if bpy.context.screen else ():
        if area.type == 'VIEW_3D':
            area.spaces.active.overlay.show_bones = True
    deform.is_visible = False
    controls.is_visible = True
    animation.action = original_action
    scene.frame_set(original_frame)
    bpy.ops.object.mode_set(mode='POSE')
    for bone in arm.pose.bones:
        bone.select = False
    arm.pose.bones['LeftFootIk'].select = True
    arm.data.bones.active = arm.data.bones['LeftFootIk']
    arm['FootIkHelp'] = 'Pose Mode: move/rotate FootIk rings; move KneePole diamonds to aim knees. Idle uses IK; older clips retain FK.'
    return {'controls': ['LeftFootIk', 'RightFootIk', 'LeftKneePole', 'RightKneePole'], 'calibration': calibration}


if __name__ == '__main__':
    print(add_foot_ik())
