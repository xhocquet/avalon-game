# Stencil hover experiment

Adapted from [dmlary's MIT-licensed compositor](https://github.com/dmlary/godot-stencil-based-outline-compositor-effect). The original license is in `LICENSE.txt`.

`HoverOutlineRenderer` installs this effect on the active camera. Hovered meshes use cached copies of their original StandardMaterial3D/ORMMaterial3D resources with stencil writes enabled. Unhover restores their previous overrides. Original meshes supply animation, skinning and depth testing; there is no extra geometry pass or GPU readback.

Edit `HoverStencilOutline.tres` to tune color, outline width, glow opacity and `thickness` (the total search/glow radius in pixels). Effects stop dispatching when hover clears. Picking is unchanged.

Changes from upstream:

- Two RG32F nearest-coordinate buffers instead of three RGBA32F buffers: about 31.6 MiB at 1080p.
- Soft outline edge and distance-based glow, composited after transparencies and before postprocessing.
- Compute bounds checks, inter-pass barriers, matching front/back stencil state for Direct3D 12, lazy initialization and explicit cleanup.
- Removed shader hot reload and console logging.

This branch uses stencil for Forward+ without MSAA. Compatibility, Mobile and MSAA configurations use the previous mask renderer. Backend selection occurs when the camera's hover renderer is created. Custom ShaderMaterials on target meshes are currently skipped with a warning.

MSAA is not validated: the mask fallback showed a small outline leak through a foreground wall at 2x MSAA. Default non-MSAA stencil and Compatibility occlusion checks pass.

Set the environment variable `AVALON_HOVER_OUTLINE=mask` before launching to compare against the old renderer with the same Forward+ settings.

Run from the repository root with the Godot Mono console executable:

```powershell
Godot --path client --resolution 640x480 --script ../scripts/verify-stencil-outline.gd
Godot --path client --rendering-method gl_compatibility --resolution 640x480 --script ../scripts/verify-hover-outline.gd
Godot --path client --rendering-driver d3d12 --resolution 1000x750 --script ../scripts/verify-stencil-outline.gd -- --game
```

The stencil probe checks all five hero scenes and a minion, material restoration, unchanged interiors, target switching, foreground occlusion, resize and target cleanup. Screenshots are saved as `user://stencil-*.png`.

An isolated animated PickleKnight at 1600x900 on an RX 6600 XT added approximately 0.29-0.30 ms GPU time with stencil, versus 0.17-0.22 ms with the mask. These are viewport timing medians, not full-battle timings. A live singleplayer hero was also visually checked under Forward+/Direct3D 12; the Vulkan full-world probe failed allocating sky textures before enabling the effect.
