"""Add shoulder, neck, and head rotation controls to the cleaned rig."""

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


def control_shape(collection, name, bone, points):
    data = bpy.data.meshes.new(name)
    data.from_pydata(points, [(i, (i + 1) % len(points)) for i in range(len(points))], [])
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.hide_render = True
    obj.hide_set(True)
    bone.custom_shape = obj
    bone.use_custom_shape_bone_size = False


def add_upper_controls():
    arm = bpy.data.objects['Armature']
    mesh = bpy.data.objects['CharacterMesh']
    scene = bpy.context.scene
    animation = arm.animation_data
    spec = {
        'LeftShoulderControl': ('LeftClavicle', 'Spine02'),
        'RightShoulderControl': ('RightClavicle', 'Spine02'),
        'NeckControl': ('NeckTwist01', 'Spine02'),
        'HeadControl': ('Head', 'NeckTwist02'),
    }
    assert all(name not in arm.data.bones for name in spec), 'Upper controls already exist'
    assert not animation.use_tweak_mode
    if bpy.context.screen and bpy.context.screen.is_animation_playing:
        bpy.ops.screen.animation_cancel(restore_frame=False)
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    backup = os.path.join(tempfile.gettempdir(), 'pickleknight-before-upper-controls-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S') + '.blend')
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
            samples[name][frame] = {control: arm.pose.bones[target].matrix.copy() for control, (target, _) in spec.items()}
            if frame in (start, (start + end) // 2, end):
                references[name, frame] = vertices(mesh)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for name, (target, parent) in spec.items():
        bone = arm.data.edit_bones.new(name)
        bone.length = 0.075
        bone.matrix = arm.data.edit_bones[target].matrix.copy()
        bone.parent = arm.data.edit_bones[parent]
        bone.use_deform = False
    bpy.ops.object.mode_set(mode='OBJECT')
    shapes = bpy.data.collections['RigShapes']
    constraints = []
    for name, (target, _) in spec.items():
        bone = arm.pose.bones[name]
        arm.data.collections['Controls'].assign(bone.bone)
        bone.rotation_mode = 'QUATERNION'
        bone.lock_location = bone.lock_scale = (True, True, True)
        bone.color.palette = 'THEME04' if name.startswith('Left') else 'THEME03' if name.startswith('Right') else 'THEME09'
        if 'Shoulder' in name:
            center = arm.data.bones[target].length * 0.8
            points = [Vector((0.045 * math.cos(i * math.tau / 48), center, 0.045 * math.sin(i * math.tau / 48))) for i in range(48)]
        else:
            inverse = bone.bone.matrix_local.inverted()
            center = bone.bone.head_local
            radius, z = (0.105, 0.666) if name == 'NeckControl' else (0.14, 0.93)
            points = [inverse @ Vector((center.x + radius * math.cos(i * math.tau / 64), center.y + radius * math.sin(i * math.tau / 64), z)) for i in range(64)]
        control_shape(shapes, name + 'Shape', bone, points)
        constraint = arm.pose.bones[target].constraints.new('COPY_ROTATION')
        constraint.name = name
        constraint.target, constraint.subtarget = arm, name
        constraint.owner_space = constraint.target_space = 'WORLD'
        constraint.mix_mode = 'REPLACE'
        constraints.append(constraint)
    for name, frames in samples.items():
        animation.action = bpy.data.actions[name]
        for frame, matrices in frames.items():
            scene.frame_set(frame)
            for control, matrix in matrices.items():
                bone = arm.pose.bones[control]
                bone.matrix = matrix
                for path in ('location', 'rotation_quaternion', 'scale'):
                    bone.keyframe_insert(data_path=path, frame=frame, group=control)
            for constraint in constraints:
                constraint.influence = 1 if name == 'TPose' else 0
                constraint.keyframe_insert(data_path='influence', frame=frame, group='UpperControls')
        for layer in animation.action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        if any(control in curve.data_path for control in spec):
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
    arm.pose.bones['HeadControl'].select = True
    arm.data.bones.active = arm.data.bones['HeadControl']
    arm['UpperControlHelp'] = 'Rotate ShoulderControl rings to shrug/roll shoulders; NeckControl tilts the neck and carries the head; HeadControl turns the head independently. Enabled in TPose; Idle/Run retain original FK. Translation and scale locked.'
    assert max(errors.values()) < 0.02, errors
    return {'backup': backup, 'controls': list(spec), 'max_mesh_error_mm': errors, 'bones': len(arm.data.bones)}


if __name__ == '__main__':
    print(add_upper_controls())
