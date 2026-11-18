# Minimap

The HUD draws a saved terrain image and live 2D icons. It has no 3D viewport or camera.

- Bounds: `World/NavigationRegion3D/GroundMesh/MinimapGuides`. Its `Ground` reference defines the full image rectangle from the mesh's local X/Z bounds. The editor outline uses the same bounds.
- Image: `client/Assets/UI/Minimap/avalon.png`, assigned to the guides' `Texture`. Regenerate with `just bake-minimap` after changing the ground material. The bake contains only the ground, without props, units, lighting, or fog.
- Coordinates: image top-left is ground-local minimum X/Z; bottom-right is maximum X/Z. `MinimapBounds.WorldToUv` and `UvToWorld` share this mapping and account for the ground transform. Keep replacement artwork inside that exact rectangle; do not add margins.
- Icons: existing `MinimapMarker` resources supply textures, team colors, and sizes. The HUD projects visible markers into the same rectangle; hero portraits retain their circular border. Pooled/hidden entities follow their existing visibility.
- Fog: the overlay reuses `VisionRenderer`'s filtered mask and world bounds. It does not rasterize another mask.
- HUD inspection: `GameUI/DefaultUI/MinimapFrame/Minimap` shows the saved texture in the editor. Runtime creates its icon controls and fog overlay. Other maps without a saved image use a plain background and their own ground bounds.
