# Looping Bubbles

Imported from the supplied "Looping Particles (Dust or Bubbles)" pack. Attribution and license were not included.

`SourceBubble.obj` preserves the full supplied geometry, normals, UVs and faces with normalized object/material names and a reconstructed MTL. The export contains overlapping shells and degenerate triangles. `Bubble.obj` retains the first shell's source positions, normals and UVs, welds duplicate seam vertices, and reconstructs its convex surface as 58 triangles so it is closed and transparent particles do not stack many shells. `BubbleAlpha.png` is unchanged (4096 x 4096); its grayscale red channel supplies opacity, since its alpha channel is opaque.

The source contains no animation or particle settings. `Scenes/FX/Skills/LoopingBubbles.tscn` adds a rising GPU particle loop with growth/fade curves and a wet, translucent rim shader. Tint and emission radius are exposed on the root; particle count, lifetime, speed and scale can be edited on `Particles` and its process material.

`configure(radius, color)` changes the footprint and tint. `play()` restarts emission, `stop()` lets live particles finish, and `stop(true)` hides the effect for pooling. Each instance has independent draw/process materials.

Run `Scenes/Playgrounds/LoopingBubblesPreview.tscn` to see blue and green variants. This scene is reusable independently of Snail Trail.

