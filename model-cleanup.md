# Model cleanup

Record Blender cleanup steps here as we perform them.

### 2026-10-25 — SittingOneLegExtended draft

- Added `SittingOneLegExtended`, a separate single-frame static action and muted NLA track. Right leg extends forward, left leg tucks in, torso leans toward the supporting right hand. Other actions are untouched.
- Reproduce with `scripts/blender-create-sitting-one-leg-extended.py` and its adjacent `blender-sitting-one-leg-extended-pose.json`. The JSON captures all final bone transforms and constraint influences; the script backs up first and stops if the action exists.
- Rejected the intermediate raised pelvis after the user noted it looked half-standing. Removed the 57.589 mm whole-rig lift and lowered HipControl to Z=85 mm. Extended right ankle target is (310, -110, 40) mm. Left ankle target is (45, 150, 40) mm.
- All transform and constraint keys are constant at frame 1; evaluated mesh is identical at frames 1, 15, and 60. Wrist IK errors are below 0.021 mm. Ground contact remains unfinished: armor/hand geometry reaches approximately 85 mm below the standing ground plane. Do not fix this by raising the entire pelvis back into the rejected half-standing posture.
- Backup: `C:\Users\meesles\AppData\Local\Temp\pickleknight-before-sitting-20261025-213943.blend`. Recovery: `C:\Users\meesles\AppData\Local\Temp\pickleknight-with-sitting-one-leg-extended-recovery.blend`. Original `.blend` remains unsaved. Left the lower seated draft selected for review.

Current action state: `Idle` is a single-frame, static standing pose with arms down alongside the body and no motion. `PreBattle` is a separate static wide battle stance inspired by the user's reference. `IdleSource` preserves the animated source clip at frames 1–269, including its grounded foot IK and shuffle. `TPose` is the static rest-pose action; `Run` retains its original FK animation. `Idle` inherits the posing controls from `TPose`. `RootControl` moves the whole rig in every action. Earlier entries below describe historical intermediate states and use the previous action names.

### 2026-10-25 — PreBattle reference pose

- Reproduction script: `scripts/blender-create-pre-battle.py`. Creates a new `PreBattle` action from the neutral `TPose` control state. Stops if the action already exists. Reference is the supplied wide martial stance with one extended arm and one raised bent arm; adapted to the character's short limbs and armor.
- Lowered the hip 120 mm and shifted it 50 mm toward the bent right leg. Spread foot IK targets to left Y=280 mm and right Y=-250 mm; turned toes outward by +/-35 degrees. Right knee bends deeply; the left leg extends toward the outstretched arm.
- Turned chest 12 degrees and head an additional 35 degrees toward the extended left fist. Extended the left wrist relative to its shoulder by (+25, +232, +5) mm. Raised the right wrist by (-15, -30, +140) mm, with the elbow pointing outward; rotated that hand upward/inward to keep the fist beside the head. Poses remain limited by the existing limb lengths with no stretch.
- Enabled existing rig constraints for this action only. Moved each foot target vertically using three evaluated sole-height correction passes. Lowest sole vertices are within 0.00001 mm of ground, sole-height spread is under 0.015 mm, and no evaluated mesh vertex lies below ground. Foot IK target errors are below 0.052 mm; final wrist target errors are below 0.019 mm.
- Keyed all pose-bone transforms and constraint influences once at frame 1, with constant interpolation and no cycles. Evaluated meshes at frames 1, 15, and 60 match exactly. Added a muted `PreBattle` NLA track and kept NLA evaluation disabled. Other actions are unchanged.
- Inspected the final pose from a front three-quarter view. Left `PreBattle` active at frame 1. Backup: `C:\Users\meesles\AppData\Local\Temp\pickleknight-before-pre-battle-20261025-213353.blend`. Recovery: `C:\Users\meesles\AppData\Local\Temp\pickleknight-with-pre-battle-recovery.blend`. Original `.blend` remains unsaved.

### 2026-10-25 — lower Idle arms alongside body

- Reproduction script: `scripts/blender-lower-idle-arms.py`. Run after `blender-create-idle-pose.py`; the `ArmsDown` action property prevents repeat application.
- Replaced the loose A-pose arm placement in static `Idle`. Rotated each hand IK target and elbow pole a further 35 degrees down around the corresponding shoulder, then offset them 25 mm outward for armor clearance. Body and foot placement are unchanged.
- Updated only the four affected controls' transform keys at frame 1. All curves remain constant, with playback range 1–1. Evaluated meshes at frames 1, 15, and 60 match exactly; foot matrices are unchanged. `IdleSource`, `TPose`, and `Run` are untouched.
- Inspected the arms-down pose in the viewport. Recovery: `C:\Users\meesles\AppData\Local\Temp\pickleknight-with-arms-down-idle-recovery.blend`. Initial backup: `C:\Users\meesles\AppData\Local\Temp\pickleknight-before-arms-down-20261025-213059.blend`. Original `.blend` remains unsaved.

### 2026-10-25 — static standing Idle pose

- Reproduction script: `scripts/blender-create-idle-pose.py`. Run after the control setup, while the animated idle is still named `Idle`. Stops if `IdleSource` already exists.
- Renamed the animated `Idle` action and its NLA track/strip to `IdleSource`; retained its keys, modifiers, and fake user. `Run` and `TPose` remain intact.
- Created a new `Idle` from `TPose`, with both shoulder-to-wrist directions approximately 45 degrees below horizontal. Lowered the left arm by 23.8023 degrees and the right by 26.3848 degrees around their shoulder pivots, rotating the hand IK targets and elbow poles together. Kept the upright body and standing foot placement.
- Keyed all pose-bone transforms and constraint influences once, at frame 1, with constant interpolation. No cycles, breathing, sway, or other motion. Playback range is 1–1. Added a muted `Idle` NLA track for the pose; NLA evaluation remains disabled for active-action preview.
- Evaluated meshes at frames 1, 15, and 60 match exactly. Foot matrices match the starting `TPose` exactly. Inspected the standing A-pose in the viewport and left `Idle` selected at frame 1.
- Backup: `C:\Users\meesles\AppData\Local\Temp\pickleknight-before-static-idle-20261025-212324.blend`. Recovery: `C:\Users\meesles\AppData\Local\Temp\pickleknight-with-static-idle-recovery.blend`. Original `.blend` remains unsaved.

### 2026-10-25 — ankle and toe roll controls

- Reproduction script: `scripts/blender-add-foot-roll.py`. Run after the previous rigging scripts; stops if roll controls exist. Saves a Temp backup before editing.
- Added visible `LeftAnkleRoll`, `RightAnkleRoll`, `LeftToeRoll`, and `RightToeRoll` controls, plus hidden `LeftAnkleRollTarget` and `RightAnkleRollTarget` helpers. Total: 65 bones, including the same 41 deform bones. Original rest bones, geometry, skin weights, and motion channels remain intact.
- Ankle roll: small side ring at each ankle. Parent: corresponding `FootIk`. Rotates around a horizontal transverse axis through the ankle; the hidden target preserves the foot's original rest orientation. `FootIkRotation` now targets this helper. Existing `AnkleSupport` bones are parented to the helper, so heel skinning follows ankle pitch. Calf IK still targets the original `FootIk` ring, keeping the ankle position fixed.
- Toe roll: arrow-shaped control over each toe region. Parent: corresponding `Foot`. A local-space `COPY_ROTATION` with `AFTER` mixing adds X rotation to `ToeBase`, preserving its existing animation. Toe flex does not rotate the foot or move the toe-base pivot.
- Both control types use XYZ Euler rotation with translation, scale, and Y/Z rotation locked. Select a control in Pose Mode and use **R, X, X** for its local roll axis, then key Rotation. Existing large `FootIk` rings still position/orient the whole foot.
- Both roll controls and hidden targets have neutral keys at each existing action's endpoints. Ankle roll is active in `Idle`; enabled `FootIkRotation` and `FootIkHeel` in `TPose` so it also works there. `TPose` calf IK remains off, preserving knee alignment. `Run` keeps FK ankles. Additive toe roll is enabled in all three actions.
- These controls pitch at the ankle and flex at the toe joint. They do not implement a reverse-foot heel/ball ground pivot or automatic ground contact correction; rolling can lift or lower sole vertices.
- Sampled start/middle/end evaluated meshes: maximum differences `TPose` 0.000087 mm, `Idle` 0.000042 mm, `Run` 0.000041 mm. Tested each ankle at 0.2 rad and each toe at 0.25 rad: measured rotations agree within 0.000002 rad, ankle and toe pivots have zero measured drift, heel supports follow ankle pitch, and toe roll leaves foot orientation unchanged.
- Inspected a close viewport preview with ankle/toe rotations, then restored neutral `TPose`, frame 1, and the previous viewport framing.
- Backup: `C:\Users\meesles\AppData\Local\Temp\pickleknight-before-foot-roll-20261025-211653.blend`. Recovery: `C:\Users\meesles\AppData\Local\Temp\pickleknight-with-foot-roll-recovery.blend`. Original `.blend` remains unsaved.

### 2026-10-25 — shoulder, neck, and head controls

- Reproduction script: `scripts/blender-add-upper-controls.py`. Run after body controls; stops if these controls exist. Saves a Temp backup before editing.
- Added four nondeforming rotation controls in `Controls`: `LeftShoulderControl`, `RightShoulderControl`, `NeckControl`, `HeadControl`. Total: 59 bones, including the same 41 deform bones. Rest bones, mesh, and weights remain unchanged.
- Shoulder rings rotate the corresponding clavicles around their inner pivots. Parent: `Spine02`. Hand IK remains independent; shrugging beyond the arm's reach allows wrist drift because arm stretching is disabled.
- `NeckControl` is the lower ring around the neck. It rotates `NeckTwist01` and carries `NeckTwist02` and the head along the existing hierarchy. Parent: `Spine02`.
- `HeadControl` is the halo above the head. It rotates `Head` independently around the head-bone pivot. Parent: `NeckTwist02`, so it follows neck movement. Both neck bones retain their original relationship; no neck IK or stretch was added.
- Each driven bone copies its control's world rotation. Control translation and scale are locked. Constraints are keyed on in `TPose`, off in `Idle` and `Run`. Baked matching control transforms follow existing motion clips, with constant keys for `TPose` and linear keys for motion.
- In Pose Mode, select a ring and rotate it with R; key rotation to retain the pose. For a new static pose, copy `TPose` first. New controls follow the chest, and the head follows the neck. Existing motion clips keep FK unless their named control constraints are enabled and keyed.
- Start/middle/end evaluated-mesh checks: `Idle` and `Run` match exactly; maximum `TPose` change is 0.00022 mm. Shoulder rotations of +/-0.12 rad, neck tilt of 0.15 rad, and head turn of 0.25 rad produce zero measured driven-bone orientation error. Head-only rotation leaves the neck fixed; neck tilt carries the head approximately 7.27 mm. The larger shoulder tests reached arm limits, with wrist drift of 4.06 mm left and 0.43 mm right.
- Visually inspected a combined head-turn/neck-tilt/shoulder-shrug pose, then restored `TPose` frame 1.
- Backup: `C:\Users\meesles\AppData\Local\Temp\pickleknight-before-upper-controls-20261025-211353.blend`. Recovery: `C:\Users\meesles\AppData\Local\Temp\pickleknight-with-upper-controls-recovery.blend`. Original `.blend` remains unsaved.

### 2026-10-25 — root, hip, and chest controls

- Reproduction script: `scripts/blender-add-body-controls.py`. Run after hand IK; stops if body controls exist. Creates a Temp backup before editing.
- Added visible nondeforming `RootControl`, `HipControl`, and `ChestIk` rings in `Controls`, plus hidden nondeforming `ChestSupport`. Total: 55 bones, including the same 41 deform bones.
- `RootControl`: outer ground ring. Move/rotate it to transform the entire skeleton and every hand/foot target and pole together. Existing top-level bones are parented to it with rest matrices preserved. Scale is locked.
- `HipControl`: ring around the pelvis, parented to `RootControl`. Move/rotate it to pose the body. The original `Hip` copies its world transform when the `HipControl` constraint is enabled.
- `ChestIk`: upper torso ring, parented to `HipControl`. Move/rotate it to bend and orient the chest. `Waist` and `Spine01` solve toward hidden `ChestSupport`, which follows the chest ring at the base of `Spine02`. `Spine02` copies the ring's orientation. No stretch; 256 solver iterations. Movement remains limited by spine reach.
- Hip/chest constraint influences are keyed on in `TPose`, off in `Idle` and `Run`. New controls follow the existing clips with baked keys; original motion channels remain intact. `TPose` keeps its previous foot-IK-off setting. Foot planting requires the foot constraints enabled, as they are in `Idle`.
- For a new static pose, copy `TPose`, pose the rings, and key their location/quaternion rotation. To enable torso controls on an existing frame, match controls to the desired body pose, then enable and key `HipControl` on `Hip`, `ChestIk` on `Spine01`, and `ChestIkRotation` on `Spine02`. Existing clips already carry matching sampled control positions.
- Sampled before/after evaluated-mesh differences: `TPose` 0.0034 mm, `Idle` 0.0008 mm, `Run` 0.0004 mm. Checked start/middle/end frames. A root move of (+30, +20, +10) mm moves hips, chest, hands, and feet together. Chest translations of 10 mm and rotation of 0.15 rad follow within 0.012 mm and zero measured orientation error. Enabling the hip control in `Idle` frame 105 and moving (+5, 0, -10) mm left the foot bones within 0.008 mm of their original positions.
- Visually inspected a bent chest pose and restored `TPose` frame 1. Original NLA arrangement remains disabled for direct action preview.
- Initial backup: `C:\Users\meesles\AppData\Local\Temp\pickleknight-before-body-controls-20261025-210831.blend`. Recovery: `C:\Users\meesles\AppData\Local\Temp\pickleknight-with-body-controls-recovery.blend`. Original `.blend` remains unsaved.

### 2026-10-25 — hand IK and elbow poles

- Reproduction script: `scripts/blender-add-hand-ik.py`. Run after foot IK and `TPose` creation; stops if hand IK already exists.
- Added nondeforming `LeftHandIk`, `RightHandIk`, `LeftElbowPole`, and `RightElbowPole` in `Controls`. Rig now has 51 bones: 41 deform bones, eight visible controls, two hidden ankle supports.
- Wrist rings translate/rotate hands; elbow diamonds steer bend direction. Forearm `HandIk` constraints solve two bones without stretching. Hand `HandIkRotation` constraints copy ring orientation in world space. Arm twist bones retain their existing relationships.
- Calibrated pole angles against the rest pose: left -0.5715473335 rad; right 2.5531208624 rad. Maximum sampled `TPose` mesh difference is 0.0061 mm.
- Baked control transforms to follow each existing clip. Hand constraints are keyed on in `TPose`, off in `Idle` and `Run`; sampled before/after meshes for those motion clips match exactly. Source animation channels remain intact.
- To use hands on a new pose copied from `TPose`, move/rotate the rings in Pose Mode and key them; key elbow positions as needed. For an FK clip, first match controls to the desired frame, then set and key both `HandIk` and `HandIkRotation` influences to 1 on each side. Playback evaluates keyed influences again when the frame changes.
- Verified each wrist follows a (+10, 0, -10) mm move within 0.001 mm, rotates by the requested 0.2 rad, and responds to its elbow pole. Returned to `TPose`, frame 1.
- Typical next additions for this rig: root/body, pelvis/chest, shoulder and head controls; foot roll/toe pivots; an IK/FK switch with snapping. Finger posing would require new deform bones and skin weights because this skeleton has no finger bones.
- Backup: `C:\Users\meesles\AppData\Local\Temp\pickleknight-before-hand-ik-20261025-210537.blend`. Recovery: `C:\Users\meesles\AppData\Local\Temp\pickleknight-with-hand-ik-recovery.blend`. Original `.blend` remains unsaved.

## Remove unwanted custom bone shapes

Custom shapes change how bone controls look in Pose Mode. Removing them restores the armature's normal bone display without changing deformation or animation.

1. Inspect hidden collections and their objects before deleting them. Check whether bones reference an object through **Bone Properties → Viewport Display → Custom Shape**.
2. Clear the **Custom Object** reference on each affected pose bone.
3. Delete the helper object once no bones or other data reference it.
4. Delete its collection if empty, and remove the helper's mesh data if it has no users.
5. Verify the character mesh, armature, armature modifier, and animation actions/NLA strips remain.
6. Inspect the rig in Pose Mode, then save the `.blend`.

For a helper named `Icosphere` in `glTF_not_exported`, run this in Blender's Text Editor after inspecting those names and references:

```python
import bpy

shape = bpy.data.objects['Icosphere']
collection = bpy.data.collections['glTF_not_exported']
assert list(collection.objects) == [shape]
assert len(collection.children) == 0

for obj in bpy.data.objects:
    if obj.type == 'ARMATURE':
        for bone in obj.pose.bones:
            if bone.custom_shape == shape:
                bone.custom_shape = None

# Stop if anything besides collection/scene membership still uses the helper.
users = bpy.data.user_map().get(shape, set())
assert all(isinstance(user, (bpy.types.Scene, bpy.types.Collection)) for user in users)

mesh = shape.data
bpy.data.objects.remove(shape, do_unlink=True)
bpy.data.collections.remove(collection)
if mesh.users == 0:
    bpy.data.meshes.remove(mesh)
```

## Normalize object, bone, and animation names

Use flat PascalCase (`TitleCaseNames`): letters and digits only, starting with an uppercase letter. Keep meaningful numbered segments such as `Spine01` and `LeftCalfTwist02`.

1. Rename the generated character mesh object and its mesh data to `CharacterMesh`. Keep `Armature` for the rig object and its data.
2. Rename bones using these substitutions: leading `L_` → `Left`, leading `R_` → `Right`, `Upperarm` → `UpperArm`, and `Forearm` → `ForeArm`. Keep already conforming names such as `Root`, `Hip`, and `Spine01`.
3. Rename corresponding mesh vertex groups to match. Blender may update them automatically when renaming bones; check before applying a second rename.
4. Update action F-curve paths from `pose.bones["OldName"]` to `pose.bones["NewName"]`. Include channel bags in layered actions, not just legacy F-curves. Verify each path resolves against the armature object.
5. Rename `preset:biped:idle` → `Idle` and `preset:biped:run` → `Run`. Give each NLA strip the name of its action, and each single-strip track the same name.
6. If the custom-shape helper collection is still present, rename `glTF_not_exported` → `RigShapes`. `Icosphere` already conforms. Apply the removal procedure above if removing those controls.
7. Verify names are unique within their namespaces, vertex weights are unchanged, and vertex-group names match bone names. Compare evaluated mesh positions at frames 1, 15, 30, and 60 before/after renaming, then restore the original frame.

## Session log

### Capture the rest pose as TPose

- Reproduce: run `scripts/blender-add-t-pose.py` after the rig cleanup. It makes a Temp backup before editing.
- Added action, NLA track, and strip named `TPose`. All bone transforms are keyed to identity at frames 1 and 2 with constant interpolation; all bone constraint influences are keyed to zero.
- Verified evaluated mesh positions exactly match Rest Position at frames 1, 1.5, and 2. Inspected the pose in the viewport.
- Left `TPose` active for preview, playback range 1–2. Its NLA track is muted and NLA evaluation remains disabled, so it does not layer over other actions. Select `Idle` or `Run` in the Action Editor to return to those clips and set the matching playback range.
- Backup: `C:\Users\meesles\AppData\Local\Temp\pickleknight-before-t-pose-20261025-205944.blend`. Original `.blend` still needs saving in Blender.

### Ground the source idle with foot IK

- Reproduce after restoring the trimmed source idle: run `scripts/blender-tidy-idle-feet.py` in Blender. It creates a Temp backup and edits the active file in memory without saving the original.
- Enabled `FootIk` and `FootIkRotation` throughout `Idle`; `Run` remains keyed off. Keyed both foot targets and knee poles over frames 1–269, preserving source ankle XY paths and all body/upper-body curves.
- Foot targets use the straightened rest orientation; toe rotations are neutral. Moving shuffles lift up to 8 mm, with a small reach margin to avoid fully extended knees.
- Added hidden, nondeforming `LeftAnkleSupport` and `RightAnkleSupport` bones parented to their foot controls. `FootIkHeel` constraints make each lower calf twist's heel weights follow the foot; these constraints are enabled only in `Idle`.
- Checked all 269 frames and intervening half frames: sole height spread below 0.011 mm; ground penetration below 0.003 mm. Foot orientation matches the forward rest orientation, and sampled body matrices are unchanged. Translation probes confirmed both foot controls move the feet and heels. Inspected close views at frames 1, 105, and 195.
- Before-edit backup: `C:\Users\meesles\AppData\Local\Temp\pickleknight-before-idle-foot-tidy-20261025-202739.blend`.
- The original `.blend` still needs saving in Blender.

### 2026-10-25 — restore source Idle and trim opening 100 frames

- Reproduction script: `scripts/blender-restore-source-idle.py`. Run while both the generated `Idle` and original `IdleSource` exist.
- Deleted the generated `Idle`, renamed `IdleSource` to `Idle`, and removed keys before source frame 101. Preserved evaluated boundary values for channels without a key at 101, including the zero IK-influence channels.
- Shifted retained keyframes and their handles by -100: source 101 becomes frame 1, and source 369 becomes frame 269. Updated the action's manual range if enabled, its NLA strip range/name, and scene playback range.
- Made restored `Idle` active in Pose Position with NLA evaluation disabled. Foot IK constraints remain keyed off, preserving the source animation. `Run` and the IK rig remain present.
- Verified evaluated mesh positions match exactly at source frames 101, 125, 200, 300, and 369 versus new frames 1, 25, 100, 200, and 269.
- Pre-edit backup: `C:\Users\meesles\AppData\Local\Temp\pickleknight-before-restore-idle-20261025-202234.blend`. Original file remains unsaved.

### 2026-10-25 — introduce foot IK

- Reproduction script: `scripts/blender-add-foot-ik.py`. Run on the normalized rig after the rest-foot edits, before adding these controls. The script stops if `LeftFootIk` already exists.
- Added four non-deforming, unparented control bones in the `Controls` bone collection: `LeftFootIk`, `RightFootIk`, `LeftKneePole`, and `RightKneePole`. They move within armature object space independently of the hips.
- Each calf has a two-bone IK constraint targeting its foot control, with a knee pole and stretching disabled. Each foot copies its control's world rotation. Existing leg hierarchy and skin weights remain intact.
- Matched controls to the current Idle's foot transforms and knee directions. Calibrated pole angles against its original thigh/calf orientations: left approximately 1.601128 radians, right 1.609996 radians.
- Keyed IK influence on for `Idle` and off for `Run` and `IdleSource`, so switching to older clips preserves their FK animation. Original animation channels remain present. Constant control curves retain only their first/closing keys.
- Added foot rings with forward arrows and knee diamonds. Their helper objects live in `RigShapes`, hidden individually in the viewport and excluded from rendering; the collection remains enabled so Blender can draw the control shapes. These shapes are actively used by the new controls.
- Hid the original bones in the `Deform` bone collection and enabled bone overlays/in-front display. Toggle that collection back on in Armature Data Properties when editing original bones.
- Verified the idle's evaluated mesh changes by less than 0.09 mm when enabling IK and the loop closes exactly. Probed both foot translations, rotations, knee pole response, and action-dependent constraint influence; restored all temporary probes.
- Replayed the creation script successfully in a separate background Blender process on the pre-edit backup.
- Pre-edit backup: `C:\Users\meesles\AppData\Local\Temp\pickleknight-before-foot-ik-20261025-201126.blend`. Original file remains unsaved, in Pose Mode with `Idle` active.

#### Using the controls

- Use **Pose Position**, then **Pose Mode**. Select a foot ring and press **G** to position the leg, or **R** to rotate the foot. Move its knee diamond to change the bend direction.
- Insert location/rotation keys to keep edits during animation playback. Merely moving a keyed control previews an edit until the frame changes.
- `Idle` uses IK. `Run` and `IdleSource` currently use their original FK animation; their IK handles do not drive the legs.

### 2026-10-25 — straighten rest feet

- Reproduction script: `scripts/blender-straighten-feet.py`. Run after sole flattening. The `FeetStraightened` property prevents repeat application.
- Model forward is +X. Measured each rest foot's horizontal head-to-tail direction and rotated around its ankle about world Z: left -17.386 degrees, right +18.551 degrees.
- Applied the same rotation to the rest foot/toe bones and lower boot mesh. Full mesh correction below 45 mm, smoothly fading to no correction at 140 mm to blend into the lower shin. Modified 577 vertices.
- Verified all foot/toe rest-bone yaw angles are effectively zero. Vertex heights, topology, UVs, and weights are unchanged, preserving flattened rest sole contact.
- Checked the existing new idle after the rest edit: loop closure remains exact, but its source-derived stance still turns the feet and its lowest point is approximately 2.76 mm below ground. Idle stance refitting remains pending; animation keys were not edited in this step.
- Inspected the rest feet in the viewport and left Rest Position active.
- Pre-edit backup: `C:\Users\meesles\AppData\Local\Temp\pickleknight-before-straight-feet-20261025-200724.blend`. Original file remains unsaved.

### 2026-10-25 — flatten rest-mesh soles

- Reproduction script: `scripts/blender-flatten-soles.py`. Run in Blender's Text Editor on the normalized mesh before this operation. The `SolesFlattened` mesh property prevents accidental repeat application.
- Worked in Rest Position. Adjusted only mesh vertex Z in the lower foot regions: X between -0.075 and 0.12, left Y > 0.065, right Y < -0.06 (model units/meters).
- Left: flattened vertices below 6 mm to Z=0; blended into unchanged geometry at 35 mm. Right: flattened below 14 mm; blended into unchanged geometry at 42 mm. Used a monotone cubic height remap with zero slope at the flat region and identity slope at the unchanged boundary.
- Modified 102 left-foot and 77 right-foot vertices. Flat contact patches contain 23 and 16 vertices respectively. Largest vertical adjustment was approximately 16.2 mm.
- Verified topology, UVs, weights, and vertex X/Y coordinates are unchanged, with no degenerate triangles. All four heel-to-toe regions now reach Z=0 within 0.001 mm. Inspected close front/side viewport views.
- Rest bones and animation keys were not modified. The existing idle stance has not been recalibrated against the edited soles.
- Pre-edit backup: `C:\Users\meesles\AppData\Local\Temp\pickleknight-before-sole-flatten-20261025-200526.blend`. Original file remains unsaved; left in Rest Position for inspection.

### 2026-10-25 — inspect rest-pose foot contact

- Confirmed the armature is in Rest Position and inspected close side/front orthographic views.
- On vertices weighted primarily to each foot/toe, the left foot's lowest point is near Z=0, but its heel region is approximately 5.5 mm higher. The right foot's lowest point is approximately 7.3 mm above Z=0; its heel region is approximately 14.1 mm above Z=0.
- These are coarse mesh-region measurements, not fitted sole planes. Both soles tilt and the right foot floats in the source rest geometry.
- The new idle's zero foot-bone drift and grounded lowest vertex do not establish flat sole contact. Foot alignment remains unresolved. No geometry, rest bones, or animation changed during this inspection.

### Inspect one action without NLA interference

1. Stop animation playback before inspecting or sampling frames.
2. Select the armature and assign the action to inspect.
3. Disable NLA evaluation while editing the active action: `bpy.data.objects['Armature'].animation_data.use_nla = False`. This preserves all tracks and actions. Re-enable it when intentionally previewing an NLA arrangement.
4. Set the scene playback range to the action's range and inspect the suspect frames again before deleting keys.

### 2026-10-25 — pickleknight.blend

- File: `C:\Users\meesles\Coding\avalon-docs\3d assets\faction - pickle\pickle soldier\pickleknight.blend`.
- Inspected objects, collections, data users, modifiers, bone constraints, images, and animation references. Found no orphaned data blocks.
- `glTF_not_exported` was a hidden collection, not a scene. Its `Icosphere` mesh supplied custom shapes for 41 bones.
- Cleared all 41 custom-shape references, then removed the helper object, its unused mesh data, and the collection.
- Verified zero custom-shape references remain. Character mesh, armature, and both `preset:biped:idle` and `preset:biped:run` actions remain; both actions were referenced by NLA strips during inspection.
- Changes remain unsaved in Blender. Visual review and saving are pending.

### 2026-10-25 — name normalization

- The current Blender state contained `Icosphere` and `glTF_not_exported` again when inspected. Renamed that collection to `RigShapes`; this pass did not remove the helper.
- Renamed the character object and mesh data to `CharacterMesh`, 32 bones and their matching vertex groups, and the actions/NLA tracks/strips to `Idle` and `Run`.
- Verified all 315 animation channels resolve, vertex weights are unchanged, and evaluated mesh positions match exactly at frames 1, 15, 30, and 60 under the current NLA setup.
- Changes remain unsaved in Blender.

### 2026-10-25 — new short Idle

- The NLA isolation change did not resolve the user's editor experience. Built a new action instead of trimming the long source clip.
- Reproduction script: `scripts/blender-create-idle.py`. Run it in Blender's Text Editor on the normalized file before creating `IdleSource`; it intentionally stops if that backup action already exists.
- Preserved the old 369-frame action as `IdleSource` with a fake user. `Run` remains intact. Made the new `Idle` active with NLA evaluation disabled.
- Used source frame 150 as a settled stance, held the lower body fixed, and added small periodic chest, head, and arm motion. Keyed location, quaternion rotation, and scale on all 41 bones to avoid inheriting unkeyed transforms from other actions.
- New loop: 48 frames at the existing 24 fps (2 seconds), plus duplicate closing key at frame 49. Preview range is 1–48. Linear baked keys and Cycles modifiers repeat the motion.
- Raised the root by approximately 9.46 mm in world Z to bring the source pose's lowest mesh point to ground level.
- Verified exact evaluated-mesh loop closure, zero foot-bone drift, and smooth rotation at 193 quarter-frame samples before the constant ground adjustment. Rechecked ground level, loop closure, and foot positions afterward. Visually inspected four viewport poses across the loop.
- Saved a pre-authoring copy at `C:\Users\meesles\AppData\Local\Temp\pickleknight-before-new-idle-20261025-200012.blend`. The original `.blend` remains unsaved with the new action active.

### 2026-10-25 — repeat custom-shape removal

- Cleared all 41 custom-shape references again and removed `Icosphere`, its unused mesh data, and `RigShapes`.
- Verified zero custom shapes remain. Objects are `Armature` and `CharacterMesh`; actions are `Idle` and `Run`.
- Cleanup and naming changes remain unsaved in Blender. Save the `.blend` before closing to retain them.

### 2026-10-25 — isolate Idle and diagnose opening hand twitch

- `Idle` spans frames 1–369. The enabled `Run` NLA strip overlaps frames 1–31 while `Idle` is assigned as the active action.
- Sampled opening hand motion with and without NLA: maximum left-hand rotation change fell from approximately 28.07 degrees/frame to 0.62 degrees/frame with NLA disabled.
- Disabled NLA evaluation on `Armature` to preview/edit `Idle` alone. Preserved both actions, all NLA tracks, and all animation keys; no opening frames were deleted.
- Inspected viewport samples near the beginning, stopped ongoing playback, set the playback range to 1–369, and returned to frame 1.
- Changes remain unsaved in Blender.
