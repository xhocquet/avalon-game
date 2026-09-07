---
name: rig-animate
description: Author or repair looping animations on a rigged 3D character with the rig-cleanup kit - idle, attack, walk/shuffle, tail, or a full cleanup of generator output (Tripo, Meshy, scans) into an export-ready cycle. Use when handed a .blend character and asked to add, fix or judge an animation, and for the symptoms - feet sliding, character hovering or sinking through the floor, a bone that ignores its curves, a seam tearing, a cycle that does not loop. Triggers on: .blend plus animation, idle/attack/walk cycle, foot plant, rig cleanup, contact sheet, "make it stand", "add an idle".
---

# Rig animation

The kit is `rig-cleanup`: config-driven authoring of planted, looping actions on a skinned
armature. It is model-agnostic; everything model-specific lives in a `cleanup_config.py`
beside the .blend.

**This skill is a router.** Its job is to get you to the two or three files that matter for
the task in front of you. The kit's own docs total ~90KB - reading them end to end costs more
than the work does, and most of it will not apply to your model.

## 1. Locate the kit, then survey the model

```sh
KIT=$(ls -d "/c/Users/meesles/Coding"/*/"3d assets/rig-cleanup" 2>/dev/null | head -1)
B="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"
"$B" -b "<model>.blend" --python "$KIT/survey.py"
```

**Run this before reading any kit source, every time.** It takes three seconds and it answers
most of what you would otherwise go looking for: object scales, the bone tree, rotation modes,
per-action channel coverage, where each action actually puts the floor, and - the part worth
the most - the frame in each existing action where each foot lies flattest, printed as
`STANCE_ROT` lines you can paste.

Every model-specific surprise in this kit's history was diagnosed backwards from a bad render,
hours in, and every one was plainly visible in `survey.py`'s output from the start.

## 2. Route

| asked for | run | read first |
|---|---|---|
| idle | `idle.py` | README "Add an idle or an attack" |
| attack | `attack.py` | same, plus the yaw-column note |
| walk, model **has legs** | `steps/step1..step5` | README "Clean a new model"; **BIPEDS.md if 2-legged** |
| walk, model **legless** | `shuffle.py` | README "No legs" |
| tail | `tail.py` | README "Add a tail" - run it **LAST** |
| clean generator output | `steps/step1..step5` | README top to "The crouch budget" |
| judge / debug an existing action | `verify.py` then `sheets.py` | README "Verifying without wasting a cycle" |

**Does this model need steps 1-5 at all?** Usually not. They exist to turn a *baked preset
gait* into a clean cycle. A hand-built rig, a legless character, or anything with no source
walk skips them entirely and the action authors run straight on the source blend. Three of the
four models in this tree skip them.

### Copy the nearest worked config, do not start from the template

`config_template.py` is a blank with ~40 fields that will not apply. Copy the config of the
closest existing model instead and edit down:

| model shape | copy from |
|---|---|
| legless, robed, floating | `faction - all hair wiz/all hair wizard/` |
| biped, generator output, baked walk | `faction - crystal/warrior/` |
| quadruped | `faction - crystal/crystal turtle/` |
| bind pose is a stride; armature not at scale 1 | `faction - all hair wiz/swirley_eye/` |

Only `LEGS`, `BEND`, `ROOT`/`SPINE`/`HEAD` and `FORWARD` must be right before the first run.
Everything else is a dial: run, look at the sheets, adjust, re-run.

## 3. Author, verify, look

```sh
"$B" -b "<model>.blend" --python "$KIT/idle.py"          # writes <ASSET>_Idle, saves in place
"$B" -b "<model>.blend" --python "$KIT/verify.py"
"$B" -b "<model>.blend" --python "$KIT/sheets.py" -- <outdir> 4     # then LOOK at them
```

`verify.py` and `sheets.py` read the *assigned* action and the scene frame range, so assign
the action you want to inspect first (a two-line `--python-expr` before the script).

**Rendering is not optional.** An asset once passed every numeric check - sub-millimetre
plants, exact loop - while the legs were splayed sideways with the belly on the floor.

## 4. Traps that produce silently wrong output

Each of these cost an afternoon. None of them raises an error.

- **An action inherits every channel it does not carry.** Blender leaves them holding whatever
  the previously assigned action last evaluated. A source action that keys location and scale
  on every bone means an authored rotation-only action solves against an offset it cannot
  reproduce. `survey.py` flags uneven coverage; `planted.py` neutralises it.
- **A non-1 armature scale splits the config into two unit systems.** Root-bone *locations*
  (`SWAY`, `SURGE`, `BOB`, `DROP`) are armature-local; `STANCE_CONTACT`, `CLUSTER_BAND` and
  `AIM_CAP` are world. Get it wrong and the legs clamp.
- **Euler rotation modes make every curve this kit writes a no-op** - the action loads, the
  bone does not move. Run `quatify.py` first on a hand-built or generator rig.
- **The bind pose may not be a stance.** A sculpt frozen mid-stride has one foot planted and
  one pointing backwards; plant that and the model stands on a toe tip in mid-air with every
  figure reading zero. `survey.py` prints the fix. Do not derive it - see README, "When the
  bind pose is not a stance", for the two derivations that look principled and are not.
- **`FORWARD` comes off the sculpt, not the rig.** Getting it 90 degrees out is silent: roll
  becomes a fore/aft rock and the breath becomes a wobble, and it still loops cleanly.
- **Steps 1-4 use the ORIGINAL bone names; step 5 renames.** Both name sets appear in a
  config on purpose. Do not "tidy" one to match the other.

When `verify.py`'s `floor` disagrees with the calibration figure, **the floor is the true
one** - a sub-millimetre plant on a character 500mm off the ground means the plant is tracking
something that is not the sole.

## 5. Working efficiently

This work is measured in Blender round trips, and each one re-sends the whole conversation.

- **Batch probes.** One headless script that answers five questions beats five scripts. If the
  `blender` MCP server is available, prefer it - a persistent session turns a 3-second cold
  start into an instant query, which matters most in exactly the debug loops that get long.
- **Grep the kit, don't read it.** `leg_solver.py` and `anim_utils.py` are ~30KB together. You
  almost always want one function.
- **When two measurements disagree, compare them on ONE frame in ONE session before
  theorising.** Solve-then-measure versus play-back-then-measure, same frame, is three lines
  and it isolates in one round what costs ten rounds of plausible theories.
- **Sample the contact sheets** (`-- <dir> 4`) so tiles stay under the 24 grid, and read the
  front view first; the other three only if it looks wrong. A full sheet is ~1.7K tokens.
- **Use `Edit`, never `sed`/inline Python, on a file already in context** - an out-of-band
  write makes the harness re-dump the whole file back at you.
- Back up the .blend before the first authoring run. Every author saves **in place**.
