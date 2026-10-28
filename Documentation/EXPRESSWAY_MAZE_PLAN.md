# Expressway Maze location plan

**Status:** Implemented and verified on 2026-10-26  
**Reference:** User-provided Expressway Maze sketch  
**Target:** Expanded fixed Sunset Basin scene, 900 x 720 m  
**Art direction:** Existing low-poly graybox style with shared materials and simple landmark silhouettes

## Design intent

Use the supplied sketch as the authoritative topology and driving-flow schema while retaining the project's low-poly graybox art direction. The generated route geometry follows the drawing's relative placement: northwest signature loop, northeast stacked interchange, east canal sweep, south-center start/finish, central street route, service-road shortcut, and southwest police escape path.

The player should be able to understand the map through four strong silhouettes:

1. A large elevated drift loop in the northwest.
2. A stacked interchange and spiral connector in the northeast.
3. A continuous street-level combo route through the center and south.
4. An east-side canal expressway that reconnects to the main network.

The existing central grid, stable legacy graph ids, vehicle systems, traffic, police, recovery, camera, scoring, and stunt rules remain compatible. The playable envelope expands from 680 x 540 m to 900 x 720 m so the signature loop, interchange, high-speed routes, and stunt landing zones have distinct silhouettes instead of competing for the same space. This is a map re-layout and expansion pass, not a new gameplay architecture.

The surrounding ground and boundary-fiction envelope should expand to approximately 1,400 x 1,150 m. Downtown remains compact; most of the added area belongs to the elevated expressway, canal, industrial, hillside, and landing-zone corridors.

## Spatial layout

| Area | Elevation | Primary use | Planned content |
|---|---:|---|---|
| Northwest signature loop | 8 m | Player, traffic, police | 220-260 m-wide two-way elevated oval around a low-poly landmark tower; long constant-radius drift sections; two ordinary ramp connections |
| Northeast stacked interchange | 0 / 8 m | Player, traffic, police | Expressway crossing, broad spiral connector, two lower-road crossings, clear support spacing, multiple non-U-turn exits |
| Central service maze | 0 m | Player, traffic, police | Existing grid retained as the readable core; tighter S-curves, alleys, gas-station cut-through, and a compact service-road loop |
| Blind shortcut | 0-4 m | Player, police | Short drop-down from the central elevated edge to a service road; no civilian traffic; guarded entry and open landing/runout |
| South combo route | 0 m | Player, traffic, police | Flowing sequence from the start area through curves, an alley, and the boulevard; supports sustained drift combos without forced stops |
| Southwest escape route | 0-3 m | Player, police | Wide industrial bypass with one drop-down and two exits; allows pursuit continuation without becoming the fastest civilian route |
| East canal expressway | 0 / 8 m | Player, traffic, police | High-speed sweep beside a low-poly canal wall; connects the north interchange to the south boulevard |
| Jump transfer | 8-10 m launch | Player only | Authored 20-30 m transfer from one elevated branch to a parallel branch, with a 50-70 m landing/runout deck, bailout lane, and ground-level recovery space |
| Industrial stunt yard | 0-5 m | Player only | Existing rail-yard and container jumps repositioned as complete run-up -> launch -> gap -> landing -> runout lines |

## Route hierarchy and access

The scene communicates route purpose through geometry plus thin, low-poly route guides matching the schema legend. These guides are road-surface markings rather than navigation UI: magenta for the street-level main/combo route, blue for the elevated route, yellow for shortcuts, and green for police escape routes.

| Route family | Graph access | Geometry rule |
|---|---|---|
| Street-level main route | Player, Traffic, Police | 14-20 m wide, terrain-supported, ordinary intersections, thin magenta guide |
| Elevated expressway | Player, Traffic, Police | 16-18 m wide, continuous ribbon mesh, 8 m nominal deck height, solid supports and guardrails, thin blue guide |
| Signature drift loop | Player, Traffic, Police | Broad constant-radius curves; two independent connections so it never behaves as a dead end |
| Shortcut / alternate | Player, Police | 9-12 m wide; no civilian traffic; always rejoins a public road; thin yellow guide |
| Police escape route | Player, Police | Wider and more forgiving than an alley; connects industrial and hillside sides of the network; thin green guide |
| Stunt launch and gap | Player | 18-25 degree launch; separate trigger; landing and bailout are not AI graph routes |

No ordinary ramp may terminate at scenery, a roof edge, or an isolated platform. Each access class permitted onto a ramp must have at least one permitted continuation at both endpoints. Stunt ramps are the only intentional airborne discontinuities, and each must include a complete approach and exit line.

## Height bands

- **Street level:** 0 m.
- **Service decks and drop-down shortcuts:** 3-5 m.
- **Warehouse roofs:** 5-6 m.
- **Main expressway and signature loop:** 8 m nominal.
- **Jump lips:** up to 10 m where needed for the transfer, while keeping the receiving deck broad and readable.

Ordinary AI roads remain at or below 10 degrees. Roof and service-deck approaches remain at or below 12 degrees. Player stunt launches remain inside the existing 18-25 degree envelope.

## Low-poly visual language

- Keep primitive or editor-generated meshes, shared URP materials, and flat color blocks.
- Use concrete columns, guardrails, retaining walls, road markings, container stacks, bus shelters, and sign slabs as the main infrastructure kit.
- Build the signature landmark from a cylinder, stacked rings, and one accent-color crown instead of detailed architecture.
- Use building masses to frame routes and hide map boundaries; avoid dense facade detail.
- Preserve the west ocean. Add a narrow east canal water strip beyond a solid canal wall so it reads as boundary fiction without becoming a new traversable system.
- Use one accent family per district: warm industrial, cool expressway, neutral downtown, and muted green hillside. Route markings stay sparse enough that the road surface remains readable from the top camera.

## Gameplay beats

### Signature drift loop

The northwest loop is the primary landmark and high-speed practice area. Entry and exit ramps connect at different points, allowing a full lap, a partial lap, or continuation toward downtown. Traffic may use it, but the path cost should keep it varied rather than mandatory.

### Stacked interchange

The northeast interchange provides the map's main route-choice moment. The lower street passes beneath without sharing graph nodes. The upper expressway continues north/south, while a broad spiral connector returns to the east canal road. Support placement must preserve lower-road sightlines and camera clearance.

### Jump transfer

The transfer sits between parallel elevated branches. It uses the existing `StuntRamp` and `StuntController` rules. The takeoff is visibly optional, traffic and police cannot select it, and missing the landing falls into a recoverable ground zone rather than outside the map.

### Blind shortcut

The shortcut is a discoverable drop-down from a mid-level connector to the central service road. It is usable by the player and police, but excluded from civilian routes. Barriers and markings should reveal the exit only after committing to the entry.

### Combo route

The main south-to-center route should string together a medium-speed curve, a narrow alley, a wider drift corner, and a short ramp or crest. It uses existing drift and stunt scoring; no new combo system is added.

### Destructible zone

Cluster existing breakable signs and lightweight barrier props along the west-central service area. The first pass reuses current destruction and challenge behavior rather than adding a general destruction simulation.

## Implementation sequence

### 1. Graph blueprint and regression contract

- Preserve all current legacy node and edge ids used by tests and systems.
- Set the graph's driveable size to 900 x 720 m and redistribute existing anchors rather than uniformly scaling every coordinate.
- Add new ids for the signature loop, interchange spiral, east canal route, blind shortcut, escape route, and jump approach/landing anchors.
- Author full 3D centerlines and access flags before generating scene geometry.
- Add tests for access-specific connectivity, ordinary-ramp continuation, travel-time route selection, grade limits, and non-connecting vertical crossings.
- Define a small set of named navigation anchors for spawn, landmark, interchange, canal, industrial, and escape-route destinations.

### 2. Primary road ribbons

- Build the signature loop, expressway spine, spiral connector, canal sweep, and their ground approaches using the existing continuous ribbon mesh path.
- Generate consolidated guardrail and support meshes to avoid collider seams and renderer growth.
- Keep deck widths and corner radii generous enough for the current simcade handling model.
- Verify all ordinary ramps have a physical road surface beyond both ends before adding scenery.

### 3. Street-level route reshaping

- Retain the central grid as the orientation anchor.
- Re-route selected eastern and southern links into the combo route and lower service maze.
- Add the blind shortcut and southwest police escape route without creating a faster civilian path through restricted edges.
- Place the start area at the south-center approach with at least three immediate route choices.

### 4. Stunts and recovery spaces

- Add the elevated jump transfer as a complete run-up, launch, gap, landing, runout, and bailout assembly.
- Reposition the two existing industrial stunt lines so they enter and leave real roads cleanly.
- Keep launch ramps player-only and preserve the existing qualification, airtime, clean-landing, score, and combo-grace rules.
- Ensure missed jumps land inside solid, recoverable geometry and never wedge the car between supports.

### 5. Infrastructure and landmarks

- Add the low-poly landmark tower, canal wall, stacked-interchange supports, retaining walls, compact building masses, container yard, shelters, and destructible clusters.
- Use combined meshes for repeated visual-only props where practical.
- Place supports after the drive lines are validated; no support may block a road, bailout lane, landing zone, or top-camera tracking corridor.

### 6. AI, camera, and recovery validation

- Confirm traffic uses the signature loop, upper expressway, spiral, and canal road while excluding shortcuts, rooftops, escape-only spurs, and stunts.
- Confirm police can pursue across the main elevated network plus the blind shortcut and southwest escape route.
- Validate waypoint height and vehicle slope alignment through all vertical transitions.
- Validate safe poses on the signature loop, spiral, warehouse roof, and jump landing deck.
- Test both camera modes above, below, and beside overlapping roads.

### 7. Scene generation and release checks

- Regenerate `DrivingSandbox.unity` from `ProjectBootstrap` rather than hand-editing generated scene objects.
- Update neighborhood and development documentation.
- Run EditMode and PlayMode suites, perform a live driving pass, and produce the iOS export.
- Treat the current soft caps of 320 environment renderers, 260 environment colliders, and 150 generated road/marking objects as the first optimization target. After the primary road pass, record actual counts and consolidate repeated geometry before proposing any higher limits; cap increases are not automatic consequences of the larger footprint.

## Primary files

- `Assets/Sideways/Editor/ProjectBootstrap.cs` — graph coordinates, generated geometry, infrastructure, landmarks, and stunt assemblies.
- `Assets/Sideways/Configuration/LAPrototypeRoadGraph.asset` — regenerated fixed graph asset.
- `Assets/Sideways/Scenes/DrivingSandbox.unity` — regenerated fixed scene.
- `Assets/Sideways/Tests/EditMode/RoadGraphTests.cs` — graph, access, grade, continuity, and crossing tests.
- `Assets/Sideways/Tests/EditMode/ProjectAssetTests.cs` — scene content and budget assertions.
- `Assets/Sideways/Tests/PlayMode/VehiclePlayModeTests.cs` — elevated traversal, jump, camera, AI height, and recovery flows.
- `Documentation/NEIGHBORHOOD.md` and `Documentation/DEVELOPMENT.md` — final map contract and QA checklist.

## Acceptance criteria

- The map is recognizable from the top camera by the signature loop, stacked interchange, central service maze, and east canal sweep.
- The graph reports a 900 x 720 m driveable envelope inside an approximately 1,400 x 1,150 m ground and boundary-fiction plane.
- Every ordinary road and ramp reconnects; only authored stunt gaps interrupt the drive surface.
- The player can complete at least three distinct loops without reversing: street combo, signature expressway, and canal/interchange.
- Civilian traffic has a connected route across the elevated network but never selects shortcuts, rooftops, escape-only spurs, or stunt launches.
- Police can pursue between street and elevated layers and through the two escape-route alternates without flattening waypoint height.
- The jump transfer has an unobstructed landing, a bailout, and a recoverable miss zone.
- Legacy graph ids and existing drift, pursuit, traffic, collectible, challenge, recovery, camera, and stunt tests remain valid.
- The fixed scene remains inside the existing mobile graybox budgets and exports successfully for iOS.

## Explicitly out of scope

- Copying the sketch's neon/Japanese visual treatment.
- A minimap, route arrows, or new navigation UI.
- Streaming or procedural city generation.
- New trick rotations or a larger stunt-combo system.
- Lane-changing traffic, traffic lights, pedestrians, or final production art.
