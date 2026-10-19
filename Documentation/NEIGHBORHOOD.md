# SIDEWAYS Milestone 3 neighborhood contract

## Scope and dimensions

The first fixed neighborhood is **Sunset Basin**, a fictional LA-inspired test district. Its navigable road envelope is approximately 520 × 420 metres inside a larger 900 × 800 metre ground plane. It is intentionally a compact gameplay slice, not a geographically accurate city or an infinite world.

The route mix contains a nine-junction urban grid, a 22-metre-wide southern boulevard, a curved scenic perimeter, three hill connectors, four narrow alley shortcuts, and several overlapping loops. Downtown (`G_C`), the boulevard (`B_SC`), the hillside (`O_N`), and the coastal route (`O_W`) act as navigation anchors. Key areas retain alternate routes if their most direct link is unavailable.

## Boundary fiction

Every public road joins another road and the network has no accidental dead-end nodes. The playable envelope is concealed by environment fiction rather than roads terminating at an exposed map edge:

- West: ocean water, an overlook, and a solid seawall.
- North: a layered hillside ridge behind the scenic road.
- East: a retaining wall and continued terrain beyond it.
- South: rail/industrial walls and warehouse frontage.

These elements are still solid prototype geometry. They communicate why the player cannot continue while preserving a learnable loop inside the map.

## Road graph schema

`LAPrototypeRoadGraph.asset` is the shared source for road surfaces and future navigation. Each node has a stable string id, position, semantic role, and explicit permission for a designed dead end. Each edge has a stable id, endpoint ids, road class, directionality, width, suggested speed, and an ordered world-space centerline that supports curved roads.

The asset validates unique ids, valid endpoints, centerline continuity, dimensions, connectivity, and accidental dead ends. `TryFindRoute` provides a basic graph route for future police and traffic systems, including temporarily blocked edges. Scene rendering does not own or mutate graph data.

## Prototype content budget

The directly playable scene is kept below these Milestone 3 soft caps, enforced by EditMode tests:

- 220 environment renderers.
- 180 environment colliders.
- 90 generated road/marking objects.
- 48 primary building or landmark bodies.
- One shared road graph asset; no runtime road generation.

Roads and buildings use primitive meshes, shared URP materials, and box colliders. There is no sector streaming, occlusion bake, final LOD setup, or device-specific quality tier yet. Those become necessary when the fixed city expands beyond this slice.

## Deferred

The graph has no lane-level traffic metadata, intersection priority, spawn sockets, police patrol weights, or district streaming information. Those additions must extend stable node/edge ids rather than replacing the graph.
