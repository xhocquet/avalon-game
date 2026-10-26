"""Drive the trimmed source Idle through grounded, forward-facing foot IK."""

import datetime
import math
import os
import tempfile

import bpy


SIDES = ('Left', 'Right')


def curves(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                yield from bag.fcurves


def update(arm):
    arm.update_tag()
    bpy.context.view_layer.update()


def shuffle_lifts(arm, source, start, end):
    lifts = {}
    for frame in range(start, end + 1):
        lifts[frame] = {}
        for side in SIDES:
            lo, hi = max(start, frame - 4), min(end, frame + 4)
            speed = (source[hi][side]['Foot'].xy - source[lo][side]['Foot'].xy).length / (hi - lo)
            t = min(1.0, max(0.0, (speed - 0.0006) / 0.0024))
            lift = 0.008 * t * t * (3 - 2 * t)
            hip = source[frame][side]['Thigh']
            ankle = source[frame][side]['Foot']
            length = arm.data.bones[side + 'Thigh'].length + arm.data.bones[side + 'Calf'].length
            lowest = hip.z - math.sqrt(max(0, length * length - (ankle.xy - hip.xy).length_squared))
            reach_lift = max(0, lowest + 0.0015 - arm.data.bones[side + 'Foot'].head_local.z)
            lifts[frame][side] = max(lift, reach_lift)
    return lifts


def tidy_idle_feet():
    arm = bpy.data.objects['Armature']
    mesh = bpy.data.objects['CharacterMesh']
    scene = bpy.context.scene
    idle = bpy.data.actions['Idle']
    assert not idle.get('GroundedFootIk'), 'Idle already corrected'
    assert not arm.animation_data.use_tweak_mode
    assert all(abs(arm.matrix_world[i][j] - (i == j)) < 1e-6 for i in range(4) for j in range(4))
    if bpy.context.screen and bpy.context.screen.is_animation_playing:
        bpy.ops.screen.animation_cancel(restore_frame=False)
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    backup = os.path.join(tempfile.gettempdir(), 'pickleknight-before-idle-foot-tidy-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S') + '.blend')
    bpy.ops.wm.save_as_mainfile(filepath=backup, copy=True)
    arm.animation_data.action = idle
    arm.animation_data.use_nla = False
    arm.data.pose_position = 'POSE'
    start, end = map(round, idle.frame_range)
    frames = range(start, end + 1)
    source = {}
    for frame in frames:
        scene.frame_set(frame)
        source[frame] = {
            side: {suffix: arm.pose.bones[side + suffix].head.copy() for suffix in ('Thigh', 'Calf', 'Foot')}
            for side in SIDES
        }
    upper_names = [b.name for b in arm.data.bones if b.use_deform and not any(word in b.name for word in ('Thigh', 'Calf', 'Foot', 'Toe'))]
    upper = {}
    for frame in (start, 70, 105, 175, 210, end):
        scene.frame_set(frame)
        upper[frame] = {name: arm.pose.bones[name].matrix.copy() for name in upper_names}

    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    for side in SIDES:
        support = arm.data.edit_bones.new(side + 'AnkleSupport')
        support.length = 0.035
        support.matrix = arm.data.edit_bones[side + 'CalfTwist02'].matrix.copy()
        support.parent = arm.data.edit_bones[side + 'FootIk']
        support.use_deform = False
    bpy.ops.object.mode_set(mode='OBJECT')
    constraints = []
    for side in SIDES:
        support = arm.data.bones[side + 'AnkleSupport']
        arm.data.collections['Deform'].assign(support)
        support.hide = True
        heel = arm.pose.bones[side + 'CalfTwist02'].constraints.new('COPY_TRANSFORMS')
        heel.name = 'FootIkHeel'
        heel.target = arm
        heel.subtarget = support.name
        heel.owner_space = 'WORLD'
        heel.target_space = 'WORLD'
        constraints.extend((heel, arm.pose.bones[side + 'Calf'].constraints['FootIk'], arm.pose.bones[side + 'Foot'].constraints['FootIkRotation']))
    for action in bpy.data.actions:
        arm.animation_data.action = action
        for constraint in constraints:
            constraint.influence = float(action == idle)
            for frame in (round(action.frame_range[0]), round(action.frame_range[1])):
                constraint.keyframe_insert(data_path='influence', frame=frame, group='FootIk')
    arm.animation_data.action = idle
    for curve in curves(idle):
        if 'constraints[' in curve.data_path and 'FootIk' in curve.data_path:
            for key in curve.keyframe_points:
                key.co.y = 1.0
                key.handle_left.y = 1.0
                key.handle_right.y = 1.0
            curve.update()

    contacts = {side: [v.index for v in mesh.data.vertices if abs(v.co.z) < 1e-5 and (v.co.y > 0 if side == 'Left' else v.co.y < 0)] for side in SIDES}
    planned_lift = shuffle_lifts(arm, source, start, end)
    for frame in frames:
        scene.frame_set(frame)
        for side in SIDES:
            sample = source[frame][side]
            lift = planned_lift[frame][side]
            target = arm.pose.bones[side + 'FootIk']
            matrix = target.bone.matrix_local.copy()
            matrix.translation = sample['Foot']
            matrix.translation.z = arm.data.bones[side + 'Foot'].head_local.z + lift
            target.matrix = matrix
            hip, knee, ankle = (sample[name] for name in ('Thigh', 'Calf', 'Foot'))
            axis = (ankle - hip).normalized()
            bend = knee - (hip + axis * (knee - hip).dot(axis))
            pole = arm.pose.bones[side + 'KneePole']
            matrix = pole.bone.matrix_local.copy()
            matrix.translation = knee + bend.normalized() * 0.22
            pole.matrix = matrix
            toe = arm.pose.bones[side + 'ToeBase']
            toe.location = (0, 0, 0)
            toe.rotation_quaternion = (1, 0, 0, 0)
            toe.scale = (1, 1, 1)
            for path in ('location', 'rotation_quaternion', 'scale'):
                toe.keyframe_insert(data_path=path, frame=frame, group=toe.name)
        update(arm)
        for _ in range(2):
            evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
            data = evaluated.to_mesh()
            errors = {side: lift - min(data.vertices[i].co.z for i in contacts[side]) for side, lift in planned_lift[frame].items()}
            evaluated.to_mesh_clear()
            for side, error in errors.items():
                target = arm.pose.bones[side + 'FootIk']
                matrix = target.matrix.copy()
                matrix.translation.z += error
                target.matrix = matrix
            update(arm)
        for side in SIDES:
            for suffix in ('FootIk', 'KneePole'):
                bone = arm.pose.bones[side + suffix]
                for path in ('location', 'rotation_quaternion', 'scale'):
                    bone.keyframe_insert(data_path=path, frame=frame, group=bone.name)
    for curve in curves(idle):
        if any('"' + side + suffix + '"' in curve.data_path for side in SIDES for suffix in ('FootIk', 'KneePole', 'ToeBase')):
            for key in curve.keyframe_points:
                key.interpolation = 'LINEAR'
            curve.update()

    checks = {'sole_span_mm': 0.0, 'ground_error_mm': 0.0, 'ankle_xy_error_mm': 0.0, 'upper_matrix_error': 0.0, 'forward_rotation_error_deg': 0.0}
    for frame in frames:
        scene.frame_set(frame)
        evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
        data = evaluated.to_mesh()
        for side in SIDES:
            heights = [data.vertices[i].co.z for i in contacts[side]]
            checks['sole_span_mm'] = max(checks['sole_span_mm'], (max(heights) - min(heights)) * 1000)
            checks['ground_error_mm'] = max(checks['ground_error_mm'], abs(min(heights) - planned_lift[frame][side]) * 1000)
            foot = arm.pose.bones[side + 'Foot']
            checks['ankle_xy_error_mm'] = max(checks['ankle_xy_error_mm'], (foot.head.xy - source[frame][side]['Foot'].xy).length * 1000)
            rotation = foot.matrix.to_quaternion().rotation_difference(foot.bone.matrix_local.to_quaternion()).angle
            checks['forward_rotation_error_deg'] = max(checks['forward_rotation_error_deg'], math.degrees(rotation))
        evaluated.to_mesh_clear()
        if frame in upper:
            for name, matrix in upper[frame].items():
                checks['upper_matrix_error'] = max(checks['upper_matrix_error'], max(abs(arm.pose.bones[name].matrix[i][j] - matrix[i][j]) for i in range(4) for j in range(4)))
    idle['GroundedFootIk'] = True
    arm['FootIkHelp'] = 'Idle uses FootIk rings and KneePole diamonds. G/R foot controls; G knee poles; key changes. Run retains FK.'
    scene.frame_start, scene.frame_end = start, end
    scene.frame_set(start)
    arm.data.collections['Deform'].is_visible = False
    arm.data.collections['Controls'].is_visible = True
    bpy.ops.object.mode_set(mode='POSE')
    for bone in arm.pose.bones:
        bone.select = False
    arm.pose.bones['LeftFootIk'].select = True
    arm.data.bones.active = arm.data.bones['LeftFootIk']
    return {'backup': backup, 'frames': [start, end], 'max_shuffle_lift_mm': 8, 'checks': checks}


if __name__ == '__main__':
    print(tidy_idle_feet())
