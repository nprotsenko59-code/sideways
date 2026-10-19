# SIDEWAYS graybox asset import standard

Use these rules when replacing prototype geometry with production assets.

## Scale and axes

- Unity units are metres: 1 unit = 1 metre.
- Use Y-up and +Z forward. Apply transforms before export.
- Cars place their root pivot at ground centre, aligned with the longitudinal centreline.
- Buildings and props place their root pivot at ground level; modular roads place it on the surface centreline.

## Hierarchy and collisions

- Vehicle collision and Rigidbody components stay on the prefab root; visual meshes remain replaceable children.
- Environment visuals and collision meshes must be separate children when they differ.
- Prefer box, capsule, and compound primitive colliders. Static mesh colliders require a clear reason; moving non-convex mesh colliders are prohibited.
- Keep road, obstacle, vehicle, and visual-only objects on their established Unity layers.
- Do not bind gameplay to renderer names, material slots, or mesh topology.

## Rendering

- Use shared URP materials. Do not instantiate a material per object at runtime.
- Prototype props should normally fit within a 1024 px texture set; reserve 2048 px for large landmarks or vehicles.
- Author at least one lower-detail mesh before a final asset replaces a repeated large graybox object. Exact LOD distances require device profiling.
- Keep transparent materials and real-time lights exceptional on mobile.

## Prefab handoff

- Place imported source assets outside gameplay prefabs, then assemble replaceable prefabs under the relevant SIDEWAYS content folder.
- Preserve stable road-graph ids and scene component references when swapping visuals.
- Validate silhouette, scale, pivot, collision, and camera readability in `DrivingSandbox` before accepting an asset.
- Final texture compression, LFS thresholds, and city-wide asset budgets remain release-preparation decisions.
