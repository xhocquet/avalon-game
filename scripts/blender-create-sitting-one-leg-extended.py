"""Create the static SittingOneLegExtended reference pose in the open rig."""

import datetime
import json
import os
import tempfile
import bpy
from mathutils import Matrix, Vector


def update(arm):
    arm.update_tag()
    bpy.context.view_layer.update()


def place(arm, name, position, rotation=None):
    bone = arm.pose.bones[name]
    matrix = bone.matrix.copy() if rotation is None else rotation.to_matrix().to_4x4()
    matrix.translation = Vector(position)
    bone.matrix = matrix
    update(arm)


def points(mesh):
    obj = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    data = obj.to_mesh()
    result = [v.co.copy() for v in data.vertices]
    obj.to_mesh_clear()
    return result


def create_pose():
    name = 'SittingOneLegExtended'
    arm = bpy.data.objects['Armature']
    mesh = bpy.data.objects['CharacterMesh']
    scene = bpy.context.scene
    animation = arm.animation_data
    assert name not in bpy.data.actions
    assert not animation.use_tweak_mode
    if bpy.context.screen.is_animation_playing:
        bpy.ops.screen.animation_cancel(restore_frame=False)
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    backup = os.path.join(tempfile.gettempdir(), 'pickleknight-before-sitting-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S') + '.blend')
    bpy.ops.wm.save_as_mainfile(filepath=backup, copy=True)
    animation.use_nla = False
    animation.action = bpy.data.actions['TPose']
    arm.data.pose_position = 'POSE'
    for bone in arm.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
    scene.frame_set(1)
    update(arm)
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    action['StaticPose'] = True
    action['PoseDescription'] = 'Seated with right leg extended, left knee raised, right hand supporting the lean and left hand resting by the knee.'
    animation.action = action
    with open(os.path.join(os.path.dirname(__file__), 'blender-sitting-one-leg-extended-pose.json')) as source:
        pose = json.load(source)
    for bone_name, state in pose.items():
        bone = arm.pose.bones[bone_name]
        bone.matrix_basis = Matrix(state['matrix_basis'])
        for constraint_name, influence in state['constraints'].items():
            bone.constraints[constraint_name].influence = influence
    update(arm)
    key_pose(arm, action)
    track = animation.nla_tracks.new()
    track.name = name
    strip = track.strips.new(name, 1, action)
    strip.action_slot = animation.action_slot
    strip.extrapolation = 'NOTHING'
    track.mute = True
    scene.frame_start = scene.frame_end = 1
    scene.use_preview_range = False
    scene.frame_set(1)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='POSE')
    for bone in arm.pose.bones:
        bone.select = False
    arm.data.bones.active = None
    for area in bpy.context.screen.areas:
        if area.type == 'VIEW_3D':
            region = area.spaces.active.region_3d
            region.view_rotation = Vector((1, -1.2, .5)).to_track_quat('Z', 'Y')
            region.view_location = Vector((.08, 0, .28))
            region.view_distance = 1.4
            region.view_perspective = 'ORTHO'
            area.spaces.active.overlay.show_overlays = False
    return {'backup': backup, 'action': name, 'lowest_mesh_z_mm': min(v.z for v in points(mesh))*1000, 'targets_mm': {side+limb: (arm.pose.bones[side+limb].head-arm.pose.bones[side+limb+'Ik'].head).length*1000 for side in ['Left','Right'] for limb in ['Hand','Foot']}}


def key_pose(arm, action):
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


if __name__ == '__main__':
    print(create_pose())
