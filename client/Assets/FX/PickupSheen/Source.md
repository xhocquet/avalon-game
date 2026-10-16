# Pickup Sheen

Shader author: QbieShay.
Source: https://godotshaders.com/shader/spatial-particle-shader/
Documentation: https://blog.sparkles.cafe/p/qbie-vfx-kit/
Retrieved: 2026-10-15. Shader code is MIT licensed on the source page.

`SpatialParticleShader.godot3.txt` preserves the published shader unchanged.
`SpatialParticle.gdshader` ports its matrix names, sampler hints, normal output,
and depth reconstruction to Godot 4. It uses unshaded additive rendering for the
pickup sheen and respects the original flipbook enable switch.
`DiamondSheen.svg` is an original four-point mask authored for this scene;
no textures or artwork were copied from the banner or demo project.

Instance `res://Scenes/FX/PickupSheen.tscn` under an item or a world marker.
The root sits at ground level; the main glint is centered 0.65 units above it.
It faces the active camera, pulses every 2.4 seconds, and loops until removed.
Particles follow the root. No script, gameplay hooks, or automatic deletion.
Scale the root to resize; edit the local shader material for emission, the
particle color ramp for tint, or the particle lifetime for pulse speed.
Set both particle nodes' `emitting` to false to stop new glints and let existing
ones finish; hide or free the root to remove it immediately.

Run `res://Scenes/Playgrounds/PickupSheenPreview.tscn` with F6 for a lit item
preview. Glow uses the viewport environment; the tapered star also works without it.

## Shader license

MIT License

Copyright (c) 2021 QbieShay

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
