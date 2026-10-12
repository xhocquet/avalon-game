# Flocks Attack

Imported from the supplied Flocks Attack OBJ export. Attribution and license were not included.

The 253 files contain 252 distinct changing mesh frames followed by a flat ground plane.
All 252 flock frames are retained as compressed ArrayMesh resources. The unrelated ground
plane is omitted. Positions are uniformly scaled by 0.003 without recentering each frame,
preserving the movement along local -Z. Normals and triangle topology are preserved.
FrameManifest.json records the original file indices, SHA-256 hashes, and mesh counts.

No textures, UVs, materials, or playback timing were supplied. The scene adds a blue
procedural glow and plays the frames at an assumed 60 fps, then clears the mesh and fades
at 4.2 seconds. The scene remains available for replay or caller-managed removal.
