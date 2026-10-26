"""Flatten the normalized pickleknight's lower soles in Rest Position."""

import bpy


def flatten_soles():
    mesh = bpy.data.objects['CharacterMesh']
    arm = bpy.data.objects['Armature']
    assert mesh.mode == 'OBJECT', 'Exit mesh Edit Mode first'
    assert mesh.data.shape_keys is None, 'Shape keys require a separate pass'
    assert not mesh.data.get('SolesFlattened'), 'Soles already flattened'
    assert all(abs(mesh.matrix_world[i][j] - (1 if i == j else 0)) < 1e-6 for i in range(4) for j in range(4)), 'Expected identity mesh transform'
    if bpy.context.screen and bpy.context.screen.is_animation_playing:
        bpy.ops.screen.animation_cancel(restore_frame=False)
    arm.data.pose_position = 'REST'

    changes = {'Left': 0, 'Right': 0}
    contacts = {'Left': 0, 'Right': 0}
    maximum = 0.0
    for vertex in mesh.data.vertices:
        x, y, z = vertex.co
        if not -0.075 <= x <= 0.12:
            continue
        if y > 0.065:
            side, flat_height, unchanged_height = 'Left', 0.006, 0.035
        elif y < -0.06:
            side, flat_height, unchanged_height = 'Right', 0.014, 0.042
        else:
            continue
        if z >= unchanged_height:
            continue
        if z <= flat_height:
            new_z = 0.0
            contacts[side] += 1
        else:
            t = (z - flat_height) / (unchanged_height - flat_height)
            # Monotone height remap joins the unchanged upper surface smoothly.
            new_z = unchanged_height * t * t * (3 - 2 * t) + (unchanged_height - flat_height) * t * t * (t - 1)
        maximum = max(maximum, abs(new_z - z))
        vertex.co.z = new_z
        changes[side] += 1
    mesh.data.update()
    mesh.data['SolesFlattened'] = True
    return {'changed_vertices': changes, 'flat_contact_vertices': contacts, 'maximum_change_mm': maximum * 1000}


if __name__ == '__main__':
    print(flatten_soles())
