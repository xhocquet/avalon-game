---
name: sanitize-godot-model-names
description: Clean messy imported Godot model and scene node names, especially GLB/GLTF assets that show source names like Sketchfab_model, Object_2, or exporter-generated dotted names. Use when the user asks to sanitize, rename, normalize, or clean node names in Godot .tscn/.scn/.glb/.gltf assets, or when wrapper .tscn overrides fail because the Godot outliner still shows names from the imported model.
---

# Sanitize Godot Model Names

## Workflow

1. Inspect the visible scene first:
   - Read the target `.tscn` when present.
   - If it instances a `.glb`/`.gltf`, inspect that source model too.
   - For `.glb`, use `scripts/sanitize_glb_node_names.ps1 -Path <file> -ListOnly` to list current source node names.

2. Prefer editing the real source of the names:
   - If Godot still shows dirty children under an instanced GLB, wrapper node overrides are not enough.
   - Rewrite the GLB JSON node names directly with the script.
   - Keep the `.tscn` wrapper simple unless it needs its own root rename, transform, script, materials, or collision overrides.

3. Choose names that describe hierarchy and role:
   - Common root cleanup: `Sketchfab_model` -> `Model`.
   - Common container cleanup: long exporter names -> `Meshes`, `Armature`, `Skeleton`, `Rig`, or another role-specific name.
   - Common mesh cleanup: `Object_2`, `Object_3` -> `BodyMesh`, `WeaponMesh`, `RockMesh`, etc.

4. Verify after edits:
   - Re-run the script with `-ListOnly` and confirm the names.
   - Read/diff the wrapper `.tscn` if changed.
   - Tell the user Godot may need asset reimport or project refocus to refresh the outliner.

## GLB Script

Use the bundled script for binary `.glb` files:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Users\meesles\.codex\skills\sanitize-godot-model-names\scripts\sanitize_glb_node_names.ps1 -Path .\client\Assets\Models\Minion.glb -ListOnly
```

Rewrite by passing `old=new` pairs:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Users\meesles\.codex\skills\sanitize-godot-model-names\scripts\sanitize_glb_node_names.ps1 -Path .\client\Assets\Models\Minion.glb -Rename "Sketchfab_model=Model","Object_2=BodyMesh"
```

The script only rewrites the GLB JSON chunk and copies the remaining binary payload unchanged. It updates the GLB total length and JSON chunk length if the renamed JSON changes size.
