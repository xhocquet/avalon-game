"""Replace the generated Idle with source frames 101–369, starting at frame 1."""

import bpy


def restore_source_idle():
    arm = bpy.data.objects['Armature']
    animation = arm.animation_data
    scene = bpy.context.scene
    source = bpy.data.actions['IdleSource']
    generated = bpy.data.actions['Idle']
    assert not animation.use_tweak_mode, 'Exit NLA tweak mode first'
    if bpy.context.screen and bpy.context.screen.is_animation_playing:
        bpy.ops.screen.animation_cancel(restore_frame=False)
    animation.use_nla = False
    animation.action = source
    bpy.data.actions.remove(generated, do_unlink=True)

    for layer in source.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    boundary = curve.evaluate(101)
                    has_boundary = any(abs(key.co.x - 101) < 1e-5 for key in curve.keyframe_points)
                    for index in reversed(range(len(curve.keyframe_points))):
                        if curve.keyframe_points[index].co.x < 101:
                            curve.keyframe_points.remove(curve.keyframe_points[index], fast=True)
                    if not has_boundary:
                        curve.keyframe_points.insert(101, boundary, options={'FAST'})
                    for key in curve.keyframe_points:
                        key.co.x -= 100
                        key.handle_left.x -= 100
                        key.handle_right.x -= 100
                    curve.update()

    source.name = 'Idle'
    source.use_fake_user = True
    if source.use_frame_range:
        source.frame_start = 1
        source.frame_end = 269
    for track in animation.nla_tracks:
        for strip in track.strips:
            if strip.action == source:
                strip.name = 'Idle'
                strip.action_frame_start = 1
                strip.action_frame_end = 269
                strip.repeat = 1
                strip.scale = 1
                strip.frame_end = strip.frame_start + 268
                track.name = 'Idle'
    arm.data.pose_position = 'POSE'
    arm['FootIkHelp'] = 'Pose Mode: FootIk rings and KneePole diamonds are available. Current Idle and Run retain FK; enable/key constraint influence when authoring IK.'
    scene.frame_start = 1
    scene.frame_end = 269
    scene.use_preview_range = False
    scene.frame_set(2)
    scene.frame_set(1)
    return {'action': source.name, 'source_frames': [101, 369], 'frames': list(source.frame_range), 'nla_enabled': animation.use_nla}


if __name__ == '__main__':
    print(restore_source_idle())
