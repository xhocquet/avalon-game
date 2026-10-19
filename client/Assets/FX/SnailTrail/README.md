# Snail Trail

`Scenes/FX/Heroes/Snailhead/SnailTrail.tscn` is one stationary slime segment for Snailhead's Snail Trail (skill 505). Its default radius is 1, matching `TrailWidth` in `snailhead.json`. The mesh is a depth-projection volume; place the scene root at ground level over opaque geometry.

The shader adapts [LeLu's decal shader](https://godotshaders.com/shader/decal-shader-y_fading-particle-alpha-scissor-treshold/), based on OlisUnfinishedProjects' Godot 4 decal shader. The source code is CC0. The adaptation keeps depth projection, Y fading and alpha-scissor erosion, adds Compatibility depth reconstruction, and replaces the source's unspecified textures with procedural slime. No source images or videos are included.

Adjust `radius`, `lifetime_seconds`, `variation_seed`, or the instance-local shader material in the inspector. A placed scene stays visible until `play()` is called; `configure_segment(width, duration_seconds, seed)` sets its size and starts it. It hides on expiry and can be replayed or pooled. `use_particle_alpha_scissor` enables the source's `COLOR.a` threshold when using this shader on particles; leave it disabled on this mesh.

Gameplay uses `SnailTrailEffects` through `SkillEffectsManager`. It reconciles predicted `TrailSegment` entities, using their position, width and expiry tick; erosion follows the skill asset's rank-scaled lifetime. The pool preloads `TrailSegmentCount` splatters per hero with this skill, includes active splatters in that budget, and rents overflow on demand. Expired or rolled-back segments return to the pool. Snail Trail replaces its generic trail telegraph; other trail skills keep their fallback. The integration is client-only.

`Scenes/FX/Heroes/Snailhead/VenomousSlobber.tscn` uses the same shader in cone mode with a brief outward reveal, erosion and bubbles. `VenomousSlobberEffects` pools it and listens for confirmed skill 504 casts, using the skill asset's cone range/angle and the cast position/aim. The scene's `duration_seconds` (1.4 seconds) controls cosmetic persistence; damage is instantaneous in the sim. Its aiming telegraph remains available, while this effect replaces the cast telegraph.
