# SIDEWAYS portrait drift-course contract

## Scope and layout

The directly playable location is a fixed **320 × 720 metre portrait drift run**. It is a focused northbound course rather than a free-roam city: the player starts near the south edge, auto-drives forward, follows one continuous road through broad S-curves, and reaches a marked finish near the north edge.

The route uses seven stable edges:

- `Course_Straight`
- `Course_LeftEntry`
- `Course_LeftArc`
- `Course_Crossover`
- `Course_RightArc`
- `Course_NorthSweep`
- `Course_Finish`

Every playable road is at least 22 metres wide. Curves use dense, rounded centerlines suitable for sustained drifting rather than right-angle intersections. Continuous red low-poly edge barriers are solid, visible, and placed on the dedicated `RoadBoundary` physics layer. Start/finish pylons, roadside buildings, signs, collectibles, and the `DriftRunTower` retain the shared low-poly visual language.

## Road graph

`PortraitDriftCourse.asset` is the active scene graph and uses graph id `portrait-drift-run`. It contains eight nodes from `COURSE_START` to `COURSE_FINISH` and seven bidirectional Boulevard edges. Player, civilian traffic, and police may use every course edge.

The graph remains the shared source for road rendering, safe-pose recovery, traffic, and police navigation. It preserves the existing `RoadAccess`, `RoadFeature`, travel-time routing, validation, and 3D centerline APIs. `LAPrototypeRoadGraph.asset` remains in the project as legacy test data, but it is not assigned to the directly playable scene.

## Camera and orientation

Gameplay supports portrait orientation only. The Canvas reference resolution is 1080 × 1920. The default overhead camera uses an oblique forward-looking composition so the car remains low in frame and at least 35 metres of upcoming road stays visible. The chase view remains available through the camera toggle.

## Prototype content budget

The generated scene remains below the existing soft caps enforced by EditMode tests:

- 320 environment renderers.
- 260 environment colliders.
- 150 generated road/marking objects.
- 48 primary building or landmark bodies.
- One active shared road graph asset; no runtime road generation.

Roads and buildings use primitive meshes, shared URP materials, and combined colliders. Final art, pedestrians, minimap, reverse driving, streaming, and device-specific quality tiers remain out of scope.
