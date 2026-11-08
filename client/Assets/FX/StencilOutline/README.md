# Stencil unit outlines

Adapted from [dmlary's MIT-licensed compositor](https://github.com/dmlary/godot-stencil-based-outline-compositor-effect). The original license is in `LICENSE.txt`.

`HoverOutlineRenderer` installs this effect on the active camera. Selected and hovered meshes use cached copies of their original StandardMaterial3D/ORMMaterial3D resources with stencil writes enabled. Clearing both states restores their previous overrides. Original meshes supply animation, skinning and depth testing; there is no extra geometry pass or GPU readback.

Edit `HoverStencilOutline.tres` to tune selection/hover opacity, outline width, glow opacity and `thickness` (the total search/glow radius in pixels). Colors come from `TeamPalette.Default`. Picking is unchanged.

Stencil bits 0-1 encode the palette index (neutral, team one, team two), bit 2 marks selection and bit 3 marks hover. Each active layer copies the matching team silhouettes, runs jump flood, then composites the team colors. Selection renders first; hover renders second and stays bright over selected units. All units in a layer share the pass. Both layers reuse two RG32F buffers (about 31.6 MiB at 1080p). Adding selected units adds no extra fullscreen passes or buffers. Empty layers skip their work.

Unit outlines require Forward+ with MSAA disabled. Unsupported configurations disable unit outlines and report a warning. Custom ShaderMaterials on target meshes are skipped with a warning; auxiliary effects can use the `hover_outline_exclude` group.

The loading screen preloads the effect, SPIR-V shaders and palette, then warms the destination camera before dismissing the overlay. Temporary previews render every faction hero/minion, turret, crystal and container with all team/state combinations. The camera retains the resulting material variants, compositor pipelines and viewport buffers. Headless runs skip GPU preparation.

Run from the repository root with the Godot Mono console executable:

```powershell
Godot --path client --resolution 640x480 --script ../scripts/verify-stencil-outline.gd
Godot --path client --resolution 640x480 --script ../scripts/verify-stencil-layers.gd
Godot --path client --rendering-driver d3d12 --resolution 1000x750 --script ../scripts/verify-stencil-outline.gd -- --game
Godot --path client --resolution 640x480 --script ../scripts/verify-outline-loading.gd -- --gametype=nav-playground
```

The probes check hero, minion and turret scenes, original-material restoration, unchanged interiors, target switching, foreground occlusion, resizing, target cleanup, team colors and selection/hover combinations. Screenshots are saved as `user://stencil-*.png`.
