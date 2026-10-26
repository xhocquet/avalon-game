"""Add a global root, hips, and a chest IK control."""

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


def ring(collection, name, bone, center, radii, z):
    inverse = bone.bone.matrix_local.inverted()
    points = [inverse @ Vector((center.x + radii[0] * math.cos(i * math.tau / 64), center.y + radii[1] * math.sin(i * math.tau / 64), z)) for i in range(64)]
    data = bpy.data.meshes.new(name)
    data.from_pydata(points, [(i, (i + 1) % 64) for i in range(64)], [])
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.hide_render = True
    obj.hide_set(True)
    bone.custom_shape = obj
    bone.use_custom_shape_bone_size = False


def add_body_controls():
    arm = bpy.data.objects['Armature']
    mesh = bpy.data.objects['CharacterMesh']
    scene = bpy.context.scene
    animation = arm.animation_data
    names = ('RootControl', 'HipControl', 'ChestIk')
    assert all(name not in arm.data.bones for name in names), 'Body controls already exist'
    assert not animation.use_tweak_mode
    if bpy.context.screen and bpy.context.screen.is_animation_playing:
        bpy.ops.screen.animation_cancel(restore_frame=False)
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    backup = os.path.join(tempfile.gettempdir(), 'pickleknight-before-body-controls-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S') + '.blend')
    bpy.ops.wm.save_as_mainfile(filepath=backup, copy=True)
    original_action, original_frame = animation.action, scene.frame_current
    animation.use_nla = False
    arm.data.pose_position = 'POSE'
    samples, references = {}, {}
    for name in ('TPose', 'Idle', 'Run'):
        animation.action = bpy.data.actions[name]
        for bone in arm.pose.bones:
            bone.matrix_basis = Matrix.Identity(4)
        start, end = map(int, animation.action.frame_range)
        samples[name] = {}
        for frame in range(start, end + 1):
            scene.frame_set(frame)
            chest = arm.pose.bones['Spine02'].matrix.copy()
            chest.translation = arm.pose.bones['Spine02'].tail
            samples[name][frame] = (arm.pose.bones['Hip'].matrix.copy(), chest)
            if frame in (start, (start + end) // 2, end):
                references[name, frame] = vertices(mesh)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    roots = [b for b in arm.data.edit_bones if b.parent is None]
    root = arm.data.edit_bones.new('RootControl')
    root.head, root.tail = (0, 0, 0), (0, 0, 0.12)
    root.use_deform = False
    for bone in roots:
        bone.parent = root
    hip = arm.data.edit_bones.new('HipControl')
    hip.length = 0.10
    hip.matrix = arm.data.edit_bones['Hip'].matrix.copy()
    hip.parent = root
    hip.use_deform = False
    chest = arm.data.edit_bones.new('ChestIk')
    chest.length = 0.10
    chest_matrix = arm.data.edit_bones['Spine02'].matrix.copy()
    chest_matrix.translation = arm.data.edit_bones['Spine02'].tail
    chest.matrix = chest_matrix
    chest.parent = hip
    chest.use_deform = False
    support = arm.data.edit_bones.new('ChestSupport')
    support.length = 0.04
    support.matrix = arm.data.edit_bones['Spine02'].matrix.copy()
    support.parent = chest
    support.use_deform = False
    bpy.ops.object.mode_set(mode='OBJECT')
    for name in names:
        bone = arm.pose.bones[name]
        arm.data.collections['Controls'].assign(bone.bone)
        bone.rotation_mode = 'QUATERNION'
        bone.lock_scale = (True, True, True)
        bone.color.palette = 'THEME09'
    shapes = bpy.data.collections['RigShapes']
    ring(shapes, 'RootControlShape', arm.pose.bones['RootControl'], Vector((0, 0, 0)), (0.31, 0.31), 0.002)
    hip = arm.pose.bones['HipControl']
    ring(shapes, 'HipControlShape', hip, hip.bone.head_local, (0.15, 0.20), hip.bone.head_local.z)
    chest = arm.pose.bones['ChestIk']
    ring(shapes, 'ChestControlShape', chest, chest.bone.head_local, (0.17, 0.23), chest.bone.head_local.z - 0.055)
    hips = arm.pose.bones['Hip'].constraints.new('COPY_TRANSFORMS')
    hips.name = 'HipControl'
    hips.target, hips.subtarget = arm, 'HipControl'
    hips.owner_space = hips.target_space = 'WORLD'
    arm.data.bones['ChestSupport'].hide = True
    spine = arm.pose.bones['Spine01'].constraints.new('IK')
    spine.name = 'ChestIk'
    spine.target, spine.subtarget = arm, 'ChestSupport'
    spine.chain_count, spine.iterations = 2, 256
    spine.use_stretch = False
    orient = arm.pose.bones['Spine02'].constraints.new('COPY_ROTATION')
    orient.name = 'ChestIkRotation'
    orient.target, orient.subtarget = arm, 'ChestIk'
    orient.owner_space = orient.target_space = 'WORLD'
    orient.mix_mode = 'REPLACE'
    for name in ('Waist', 'Spine01', 'Spine02'):
        arm.pose.bones[name].ik_stretch = 0
    for name, frames in samples.items():
        animation.action = bpy.data.actions[name]
        for frame, (hip_matrix, chest_matrix) in frames.items():
            scene.frame_set(frame)
            root = arm.pose.bones['RootControl']
            root.matrix_basis = Matrix.Identity(4)
            hip = arm.pose.bones['HipControl']
            hip.matrix = hip_matrix
            update(arm)
            arm.pose.bones['ChestIk'].matrix = chest_matrix
            arm.pose.bones['ChestSupport'].matrix_basis = Matrix.Identity(4)
            for control_name in names + ('ChestSupport',):
                control = arm.pose.bones[control_name]
                for path in ('location', 'rotation_quaternion', 'scale'):
                    control.keyframe_insert(data_path=path, frame=frame, group=control_name)
            for constraint in (hips, spine, orient):
                constraint.influence = 1 if name == 'TPose' else 0
                constraint.keyframe_insert(data_path='influence', frame=frame, group='BodyControls')
        for layer in animation.action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        if any(token in curve.data_path for token in names):
                            for key in curve.keyframe_points:
                                key.interpolation = 'CONSTANT' if name == 'TPose' or 'constraints' in curve.data_path else 'LINEAR'
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
    arm.pose.bones['HipControl'].select = True
    arm.data.bones.active = arm.data.bones['HipControl']
    arm.show_in_front = True
    arm['BodyControlHelp'] = 'RootControl moves the entire rig including IK targets. HipControl poses the body. ChestIk moves/rotates the upper spine using a hidden ChestSupport target. Hip/chest enabled in TPose; Idle and Run preserve FK torso.'
    assert errors['Idle'] < 0.01 and errors['Run'] < 0.01, errors
    assert errors['TPose'] < 0.5, errors
    return {'backup': backup, 'controls': names, 'max_mesh_error_mm': errors, 'bones': len(arm.data.bones)}


if __name__ == '__main__':
    print(add_body_controls())
