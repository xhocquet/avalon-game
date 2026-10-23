# Vision map viewer

Open a world scene, then **Project → Tools → Avalon: Vision Map**. Run a C# build and enable the Avalon Vision Map plugin in Project Settings if it has not loaded yet.

- Cyan lines show exported sight boundaries. Red lines exceed the adjustable length threshold; long edges are review hints, including legitimate outer map boundaries.
- Shift + left click places a probe on the world Y=0 plane. Cyan fill shows visible cells, using `Visibility.CanSee` at the runtime texture's 128 × 128 sample centers. The yellow cross marks the source.
- Probe range, overlay height, and visibility through geometry are adjustable. The status reports whether the source is inside a blocker or outside the map.
- The viewer reads the scene's `MapLayout*.bytes` and reloads when that file changes. Run `just export-scene-data` after editing map geometry. The timestamp is the exported file's modification time.

The probe uses a zero-radius source, with no team vision union or display blur. Overlay nodes are temporary and excluded from scene saves.
