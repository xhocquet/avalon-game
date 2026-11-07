# Stencil hover experiment

Adapted from [dmlary's MIT-licensed compositor](https://github.com/dmlary/godot-stencil-based-outline-compositor-effect). The original license is in `LICENSE.txt`.

`HoverOutlineRenderer` installs this effect on the active camera. Selected and hovered meshes use cached copies of their original StandardMaterial3D/ORMMaterial3D resources with stencil writes enabled. Clearing both states restores their previous overrides. Original meshes supply animation, skinning and depth testing; there is no extra geometry pass or GPU readback.

Edit `HoverStencilOutline.tres` to tune selection opacity (0.5), hover opacity (1.0), outline width, glow opacity and `thickness` (the total search/glow radius in pixels). Colors come from `TeamPalette.Default`. Picking is unchanged.

Stencil bits 0-1 encode the palette index (neutral, team one, team two), bit 2 marks selection and bit 3 marks hover. Each active layer copies the matching team silhouettes, runs jump flood, then composites the team colors. Selection renders first; hover renders second and stays bright over selected units. All units in a layer share the pass. Both layers reuse the same two buffers, so adding selected units adds no extra fullscreen passes or buffers. Empty layers skip their work.

Changes from upstream:

- Two RG32F nearest-coordinate buffers instead of three RGBA32F buffers: about 31.6 MiB at 1080p.
- Soft outline edge and distance-based glow, composited after transparencies and before postprocessing.
- Compute bounds checks, inter-pass barriers, matching front/back stencil state for Direct3D 12, lazy initialization and explicit cleanup.
- Removed shader hot reload and console logging.

This branch uses stencil for Forward+ without MSAA. Compatibility, Mobile and MSAA configurations use the previous mask renderer. Backend selection occurs when the camera's hover renderer is created. Custom ShaderMaterials on target meshes are currently skipped with a warning.

The fallback supports team-colored hover only; selection keeps the existing ground indicators there.

MSAA is not validated: the mask fallback showed a small outline leak through a foreground wall at 2x MSAA. Default non-MSAA stencil and Compatibility occlusion checks pass.

Set the environment variable `AVALON_HOVER_OUTLINE=mask` before launching to compare against the old renderer with the same Forward+ settings.

Run from the repository root with the Godot Mono console executable:

```powershell
Godot --path client --resolution 640x480 --script ../scripts/verify-stencil-outline.gd
Godot --path client --resolution 640x480 --script ../scripts/verify-stencil-layers.gd
Godot --path client --rendering-method gl_compatibility --resolution 640x480 --script ../scripts/verify-hover-outline.gd
Godot --path client --rendering-driver d3d12 --resolution 1000x750 --script ../scripts/verify-stencil-outline.gd -- --game
```

The stencil probe checks all five hero scenes and a minion, material restoration, unchanged interiors, target switching, foreground occlusion, resize and target cleanup. Screenshots are saved as `user://stencil-*.png`.

The original single-layer implementation added approximately 0.29-0.30 ms GPU time at 1600x900 on an RX 6600 XT, versus 0.17-0.22 ms with the mask. These are historical viewport timing medians, not full-battle or two-layer timings. A live singleplayer hero was also visually checked under Forward+/Direct3D 12; the Vulkan full-world probe failed allocating sky textures before enabling the effect.
