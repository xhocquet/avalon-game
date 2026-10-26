"""Point pickleknight's rest feet along +X, retaining sole heights."""

import math

import bpy
from mathutils import Matrix


def straighten_feet():
    arm = bpy.data.objects['Armature']
    mesh = bpy.data.objects['CharacterMesh']
    assert arm.mode == 'OBJECT' and mesh.mode == 'OBJECT', 'Exit Edit/Pose Mode first'
    assert not mesh.data.get('FeetStraightened'), 'Feet already straightened'
    assert mesh.data.shape_keys is None and mesh.data.users == 1
    assert arm.matrix_world == Matrix.Identity(4) and mesh.matrix_world == Matrix.Identity(4)
    if bpy.context.screen and bpy.context.screen.is_animation_playing:
        bpy.ops.screen.animation_cancel(restore_frame=False)
    arm.data.pose_position = 'REST'

    corrections = {}
    for side in ('Left', 'Right'):
        bone = arm.data.bones[side + 'Foot']
        direction = bone.tail_local - bone.head_local
        angle = -math.atan2(direction.y, direction.x)
        pivot = bone.head_local.copy()
        rotation = Matrix.Rotation(angle, 3, 'Z')
        transform = Matrix.Translation(pivot) @ rotation.to_4x4() @ Matrix.Translation(-pivot)
        corrections[side] = (angle, pivot, rotation, transform)

    changed = 0
    for vertex in mesh.data.vertices:
        p = vertex.co.copy()
        if p.z >= 0.14 or abs(p.y) < 0.06 or not -0.1 <= p.x <= 0.15:
            continue
        side = 'Left' if p.y > 0 else 'Right'
        angle, pivot, _, _ = corrections[side]
        t = max(0.0, min(1.0, (p.z - 0.045) / (0.14 - 0.045)))
        influence = 1.0 - t * t * (3.0 - 2.0 * t)
        vertex.co = pivot + Matrix.Rotation(angle * influence, 3, 'Z') @ (p - pivot)
        vertex.co.z = p.z
        changed += 1

    matrices = {name: arm.data.bones[name].matrix_local.copy() for name in ('LeftFoot', 'LeftToeBase', 'RightFoot', 'RightToeBase')}
    active = bpy.context.view_layer.objects.active
    selected = list(bpy.context.selected_objects)
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    try:
        for side in ('Left', 'Right'):
            for suffix in ('Foot', 'ToeBase'):
                name = side + suffix
                arm.data.edit_bones[name].matrix = corrections[side][3] @ matrices[name]
    finally:
        bpy.ops.object.mode_set(mode='OBJECT')
        arm.select_set(False)
        for obj in selected:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = active
    mesh.data.update()
    mesh.data['FeetStraightened'] = True
    return {'yaw_corrections_degrees': {side: math.degrees(data[0]) for side, data in corrections.items()}, 'changed_vertices': changed}


if __name__ == '__main__':
    print(straighten_feet())
