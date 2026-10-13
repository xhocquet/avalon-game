# Stylized Fire

Author: Loop_Box / Loop-Box.
Shader: https://godotshaders.com/shader/stylized-botw-fire/
Demo: https://github.com/Loop-Box/Stylized-Fire-For-Godot4.5
Source commit: df057699a5752c9808681b2b25b748d652306dca

The shader page licenses its code under CC0. The repository contains no separate
license for the mesh; the shader page explicitly excludes depicted assets from CC0.

FlameMesh.tres and StylizedFire.gdshader are unchanged copies of the source mesh
and shader. The three scenes preserve the demo's orange, cyan, and green shader
gradients, procedural noise, particle trails, and light colors. Materials and
particle resources are local to each instance. The root origin is at the flame base.
Each effect loops until removed by its caller; there is no automatic lifetime.

Open Scenes/Playgrounds/StylizedFirePreview.tscn and run the current scene (F6).
