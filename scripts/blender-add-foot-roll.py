"""Add ankle pitch and toe flex controls without changing the idle shuffle."""

import datetime
import math
import os
import tempfile

import bpy
from mathutils import Matrix, Vector


def update(arm):
    arm.update_tag()
    bpy.context.view_layer.update()


def vertices(mesh):
    obj = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    data = obj.to_mesh()
    points = [v.co.copy() for v in data.vertices]
    obj.to_mesh_clear()
    return points


def shape(collection, name, bone, points, edges):
    data = bpy.data.meshes.new(name)
    data.from_pydata(points, edges, [])
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.hide_render = True
    obj.hide_set(True)
    bone.custom_shape = obj
    bone.use_custom_shape_bone_size = False


def add_foot_roll():
    arm = bpy.data.objects['Armature']
    mesh = bpy.data.objects['CharacterMesh']
    scene = bpy.context.scene
    animation = arm.animation_data
    sides = ('Left', 'Right')
    assert all(side + 'AnkleRoll' not in arm.data.bones for side in sides), 'Foot roll already exists'
    assert not animation.use_tweak_mode
    if bpy.context.screen and bpy.context.screen.is_animation_playing:
        bpy.ops.screen.animation_cancel(restore_frame=False)
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    backup = os.path.join(tempfile.gettempdir(), 'pickleknight-before-foot-roll-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S') + '.blend')
    bpy.ops.wm.save_as_mainfile(filepath=backup, copy=True)
    original_action, original_frame = animation.action, scene.frame_current
    animation.use_nla = False
    arm.data.pose_position = 'POSE'
    references = {}
    for name in ('TPose', 'Idle', 'Run'):
        animation.action = bpy.data.actions[name]
        for bone in arm.pose.bones:
            bone.matrix_basis = Matrix.Identity(4)
        start, end = map(int, animation.action.frame_range)
        for frame in sorted({start, (start + end) // 2, end}):
            scene.frame_set(frame)
            references[name, frame] = vertices(mesh)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for side in sides:
        foot = arm.data.edit_bones[side + 'FootIk']
        ankle = arm.data.edit_bones.new(side + 'AnkleRoll')
        ankle.length = 0.065
        matrix = Matrix(((0, 1, 0, 0), (-1, 0, 0, 0), (0, 0, 1, 0), (0, 0, 0, 1)))
        matrix.translation = foot.head
        ankle.matrix = matrix
        ankle.parent = foot
        ankle.use_deform = False
        target = arm.data.edit_bones.new(side + 'AnkleRollTarget')
        target.length = 0.04
        target.matrix = foot.matrix.copy()
        target.parent = ankle
        target.use_deform = False
        arm.data.edit_bones[side + 'AnkleSupport'].parent = target
        toe = arm.data.edit_bones.new(side + 'ToeRoll')
        toe.length = 0.05
        toe.matrix = arm.data.edit_bones[side + 'ToeBase'].matrix.copy()
        toe.parent = arm.data.edit_bones[side + 'Foot']
        toe.use_deform = False
    bpy.ops.object.mode_set(mode='OBJECT')
    shapes = bpy.data.collections['RigShapes']
    toe_constraints = []
    for side in sides:
        for suffix in ('AnkleRoll', 'ToeRoll'):
            bone = arm.pose.bones[side + suffix]
            arm.data.collections['Controls'].assign(bone.bone)
            bone.rotation_mode = 'XYZ'
            bone.lock_location = bone.lock_scale = (True, True, True)
            bone.lock_rotation = (False, True, True)
            bone.color.palette = 'THEME04' if side == 'Left' else 'THEME03'
        arm.data.bones[side + 'AnkleRollTarget'].hide = True
        ankle = arm.pose.bones[side + 'AnkleRoll']
        points = [(0.055, 0.027 * math.cos(i * math.tau / 32), 0.027 * math.sin(i * math.tau / 32)) for i in range(32)]
        points += [(0, 0, 0), (0.055, 0, 0)]
        shape(shapes, side + 'AnkleRollShape', ankle, points, [(i, (i + 1) % 32) for i in range(32)] + [(32, 33)])
        toe = arm.pose.bones[side + 'ToeRoll']
        shape(shapes, side + 'ToeRollShape', toe, [(-0.035, 0.02, 0.025), (0, 0.06, 0.025), (0.035, 0.02, 0.025), (0.035, 0, 0.025), (-0.035, 0, 0.025)], [(i, (i + 1) % 5) for i in range(5)])
        arm.pose.bones[side + 'Foot'].constraints['FootIkRotation'].subtarget = side + 'AnkleRollTarget'
        constraint = arm.pose.bones[side + 'ToeBase'].constraints.new('COPY_ROTATION')
        constraint.name = 'ToeRoll'
        constraint.target, constraint.subtarget = arm, side + 'ToeRoll'
        constraint.owner_space = constraint.target_space = 'LOCAL'
        constraint.mix_mode = 'AFTER'
        constraint.use_y = constraint.use_z = False
        toe_constraints.append(constraint)
    for name in ('TPose', 'Idle', 'Run'):
        animation.action = bpy.data.actions[name]
        start, end = map(int, animation.action.frame_range)
        for frame in (start, end):
            scene.frame_set(frame)
            for side in sides:
                for suffix in ('AnkleRoll', 'AnkleRollTarget', 'ToeRoll'):
                    bone = arm.pose.bones[side + suffix]
                    bone.matrix_basis = Matrix.Identity(4)
                    rotation = 'rotation_euler' if suffix != 'AnkleRollTarget' else 'rotation_quaternion'
                    for path in ('location', rotation, 'scale'):
                        bone.keyframe_insert(data_path=path, frame=frame, group=bone.name)
                if name == 'TPose':
                    for target, constraint_name in ((side + 'Foot', 'FootIkRotation'), (side + 'CalfTwist02', 'FootIkHeel')):
                        constraint = arm.pose.bones[target].constraints[constraint_name]
                        constraint.influence = 1
                        constraint.keyframe_insert(data_path='influence', frame=frame, group='FootRoll')
            for constraint in toe_constraints:
                constraint.influence = 1
                constraint.keyframe_insert(data_path='influence', frame=frame, group='FootRoll')
        for layer in animation.action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        if any(token in curve.data_path for token in ('AnkleRoll', 'ToeRoll')):
                            for key in curve.keyframe_points:
                                key.interpolation = 'CONSTANT'
    errors = {}
    for (name, frame), points in references.items():
        animation.action = bpy.data.actions[name]
        for bone in arm.pose.bones:
            bone.matrix_basis = Matrix.Identity(4)
        scene.frame_set(frame)
        update(arm)
        error = max((a - b).length for a, b in zip(points, vertices(mesh))) * 1000
        errors[name] = max(error, errors.get(name, 0))
    animation.action = original_action
    scene.frame_start, scene.frame_end = map(int, original_action.frame_range)
    scene.frame_set(original_frame)
    update(arm)
    bpy.ops.object.mode_set(mode='POSE')
    for bone in arm.pose.bones:
        bone.select = False
    arm.pose.bones['LeftAnkleRoll'].select = True
    arm.data.bones.active = arm.data.bones['LeftAnkleRoll']
    arm['FootRollHelp'] = 'Pose Mode: rotate AnkleRoll or ToeRoll around local X (R X X). AnkleRoll pitches at the ankle in Idle/TPose; ToeRoll flexes at the toe base in all clips. FootIk rings still place the feet. Run keeps FK ankles.'
    assert max(errors.values()) < 0.02, errors
    return {'backup': backup, 'controls': [side + suffix for side in sides for suffix in ('AnkleRoll', 'ToeRoll')], 'max_mesh_error_mm': errors, 'bones': len(arm.data.bones)}


if __name__ == '__main__':
    print(add_foot_roll())
