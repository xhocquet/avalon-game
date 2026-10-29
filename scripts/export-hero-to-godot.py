"""Export normalized hero actions without changing the Blender source."""

import bpy
import json
import struct
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path(bpy.data.filepath)
HEROES = {
    'crystalwarrior.blend': ('CrystalWarrior', 'CrystalWarrior_Rig', 'CrystalWarrior', 'CrystalWarriors/crystalwarrior.glb'),
    'skinwalker.blend': ('Skinwalker', 'Skinwalker_Rig', 'Skinwalker', 'Skinwalker/skinwalkermergedv1.glb'),
    'snailhead.blend': ('SnailHead', 'Snailhead_Rig', 'Snailhead', 'SnailHead/walking_snailhead_v1.glb'),
    'pickleknight.blend': ('PickleKnight', 'PickleKnight_Rig', 'PickleKnight', 'PickleKnight/pickleknightfinal1.glb'),
}
if SOURCE.name not in HEROES:
    raise RuntimeError(f'Unsupported source: {SOURCE}')
hero, rig_name, mesh_name, destination = HEROES[SOURCE.name]
if globals().get('EXPECTED_HERO', hero) != hero:
    raise RuntimeError(f'Expected {EXPECTED_HERO}, got {hero}')
DEFAULT_OUTPUT = ROOT / 'client/Assets/Heroes' / destination
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
output = Path(args[0]) if args else DEFAULT_OUTPUT
names = {
    'Attack': 'attack', 'Death': 'death', 'HandsOnHips': 'hands_on_hips',
    'Idle': 'idle', 'Pose': 'pose', 'PreBattle': 'pre_battle',
    'Run': 'run', 'Tired': 'tired', 'TPose': 't_pose', 'Walk': 'walk',
    'Cheer': 'cheer', 'Flip': 'flip', 'StandingRelax': 'standing_relax', 'Wait': 'wait',
}
if set(bpy.data.actions.keys()) != set(names):
    raise RuntimeError(f'Unexpected actions: {list(bpy.data.actions.keys())}')
arm = bpy.data.objects[rig_name]
mesh = bpy.data.objects[mesh_name]
original_names = [(action, action.name) for action in bpy.data.actions]
selection = list(bpy.context.selected_objects)
active = bpy.context.view_layer.objects.active
frame, subframe = bpy.context.scene.frame_current, bpy.context.scene.frame_subframe
try:
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    for action, old in original_names:
        action.name = names[old]
    arm.name = 'Armature'
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = arm
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=str(output), export_format='GLB', use_selection=True,
        export_animations=True, export_animation_mode='ACTIONS',
        export_frame_range=False, export_skins=True, export_force_sampling=True,
        export_current_frame=False, export_rest_position_armature=True,
        export_nla_strips=False, export_all_influences=False,
    )
    blob = output.read_bytes()
    size, chunk = struct.unpack_from('<II', blob, 12)
    gltf = json.loads(blob[20:20 + size])
    exported = {a['name'] for a in gltf.get('animations', [])}
    if exported != set(names.values()):
        raise RuntimeError(f'Unexpected exported actions: {sorted(exported)}')
    if len(gltf.get('skins', [])) != 1:
        raise RuntimeError('Expected one skin')
    print('HERO_EXPORT_OK', json.dumps({
        'hero': hero,
        'path': str(output), 'bytes': len(blob), 'actions': sorted(exported),
        'bones': len(gltf['skins'][0]['joints']),
    }))
finally:
    for action, old in original_names:
        action.name = old
    arm.name = rig_name
    bpy.context.scene.frame_set(frame, subframe=subframe)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in selection:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = active
