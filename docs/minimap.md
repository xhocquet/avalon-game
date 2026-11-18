# Minimap

The HUD draws a saved terrain image and live 2D icons. It has no 3D viewport or camera.

- Bounds: `World/NavigationRegion3D/GroundMesh/MinimapGuides`. Its `Ground` reference defines the full image rectangle from the mesh's local X/Z bounds. The editor outline uses the same bounds.
- Image: `client/Assets/UI/Minimap/avalon.png`, assigned to the guides' `Texture`. Regenerate with `just bake-minimap` after changing the ground material. The bake contains only the ground, without props, units, lighting, or fog.
- Coordinates: image top-left is ground-local minimum X/Z; bottom-right is maximum X/Z. `MinimapBounds.WorldToUv` and `UvToWorld` share this mapping and account for the ground transform. Keep replacement artwork inside that exact rectangle; do not add margins.
- Icons: existing `MinimapMarker` resources supply textures, team colors, and sizes. The HUD projects visible markers into the same rectangle; hero portraits retain their circular border. Pooled/hidden entities follow their existing visibility.
- Fog: the overlay reuses `VisionRenderer`'s filtered mask and world bounds. It does not rasterize another mask.
- HUD inspection: `GameUI/DefaultUI/MinimapFrame/Minimap` shows the saved texture in the editor. Runtime creates its icon controls and fog overlay. Other maps without a saved image use a plain background and their own ground bounds.

Left-click or left-drag the minimap to center the camera, leaving follow/lock mode. Drags clamp to the image edges and stop on release. Right-click issues the normal selected-unit move order (or the local hero fallback), using nav snapping and the normal destination marker. Minimap input does not start world selection or attack units beneath the HUD.

`Minimap/ViewportFootprint` outlines the main camera's ground coverage above the fog and icons. Its outline connects the four screen-corner rays intersected with the ground plane, so position, zoom, camera height, FOV, and viewport aspect update automatically. Perspective produces a trapezoid rather than an enclosing rectangle. The minimap clips any portion outside the map; views whose corners do not intersect the ground hide the outline.

`Minimap/PlayerPath` draws only the local hero's current route as a 1-pixel red line at 80% opacity, beneath all icons. It reads the predicted frame's `NavAgentComponent` corridor and uses Klotho's `FPNavMeshFunnel.FindCorners`, with a private display buffer and no additional path search. Paths end at the current `PathTarget`, without a destination dot. The overlay refreshes on HUD sync and clears on arrival, stop, path failure, death, or missing hero. It never enumerates minion paths. Color and line width are editable on the authored `PlayerPath` node, which also shows an editor preview.
