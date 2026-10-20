# SIDEWAYS development notes

## Architecture

- `VehicleController` owns Rigidbody forces and publishes read-only telemetry.
- `VehicleInputSource` converts keyboard and touch-zone input into smoothed normalized steering and handbrake demand.
- `VehicleRecovery` records safe road poses and emits reset/combo-cancel events.
- `VehicleCollisionSensor` reports obstacle impacts without owning scoring policy.
- `DriftScoreController` evaluates telemetry through a deterministic scoring model and exposes the optional ride-score provider.
- `RoadGraphAsset` is the stable, validated navigation source for both rendered roads and future police/traffic routing.
- `RoadGraphComponent` exposes the active graph from the scene and draws road-class gizmos when selected.
- `PoliceDirector` owns pursuit state and penalties; `PoliceVisionSensor` is the only source of sightings, while `PoliceVehicleController` follows the shared road graph.
- `RecklessDrivingTracker` turns sustained drifts and severe impacts into a short-lived reportable condition without deciding whether police can see them.
- `ChallengeController` owns the optional challenge lifecycle and consumes stable road-sign ids or drift-point events through a deterministic `ChallengeModel`.
- `TrafficSpawner` owns sparse population and protected spawn selection; each `TrafficVehicle` stays graph-bound and reports at most one near miss per encounter.
- `RideResourceController` owns per-ride coins and fuel, while `FuelModel` keeps consumption and refill rules deterministic.
- `RideSessionController` owns the Driving/Ended lifecycle and two-second flip rule.
- `ChaseCameraController` is a custom damped follow camera with look-ahead and obstruction checks.
- Handling, camera, ride rules, and drift scoring are ScriptableObject assets under `Assets/Sideways/Configuration`.
- The `PlayerCar` prefab keeps physics on its root and replaceable visuals below `Visual`.

## Milestone checklist

- [x] Unity 6000.6.6f1 project, URP, Input System, uGUI and test assemblies.
- [x] iOS-first landscape configuration and Xcode export entry point.
- [x] Directly playable DrivingSandbox scene and replaceable car prefab.
- [x] Automatic acceleration, analog-smoothed steering, lateral grip and drift telemetry.
- [x] Space-key and downward-swipe handbrake drift initiation.
- [x] Custom chase camera, collision correction and configurable presentation.
- [x] Safe-pose reset and combo-cancel event.
- [x] Sustained-flip ride ending and restart overlay.
- [x] Toggleable debug telemetry.
- [x] EditMode and PlayMode tests plus manual QA checklist.
- [x] Telemetry-driven drift scoring with angle/speed scaling and anti-exploit limits.
- [x] Timed combo multiplier, recovery grace, banking, and explicit combo cancellation.
- [x] Collision severity policy and results/best-score integration.
- [x] Runtime score/combo HUD with grace feedback.
- [x] Fixed LA-inspired neighborhood with grid, boulevard, scenic curves, alleys, and multiple loops.
- [x] Connected road graph with alternate routes and future navigation API.
- [x] Ocean, hillside, retaining-wall, and industrial boundary fiction.
- [x] Enforced graybox renderer/collider budgets and asset-import contract.
- [x] One-unit police patrol with vision-cone and building-occlusion detection.
- [x] Road-graph pursuit/interception, last-known-position search, reacquisition, and moving escape.
- [x] Recoverable busted penalty with active-combo cancellation and banked-score preservation.
- [x] Police state HUD and deterministic pursuit-state tests.
- [x] Data-driven challenge lifecycle with start, stop, retry, countdown, success, failure, and typed reward hook.
- [x] Unique road-sign destruction and drift-score challenge definitions.
- [x] Sparse graph-driven traffic with protected spawning and solid player interaction.
- [x] Collision-free, relative-speed near-miss scoring with one event per encounter.
- [x] Challenge and bonus HUD feedback without interrupting free roam.
- [x] Road coin lines, banked coin awards, draining fuel, refill canisters, and resource HUD.
- [ ] Physical iPhone profiling and touch QA.

## Manual QA

1. Navigate the central four-way intersection using A/D or arrows.
2. Hold steering through an outer corner, press Space, observe BRAKE = ON followed by nonzero slip and DRIFT = YES, then release and recover.
3. Perform a forward 180-degree U-turn on a cross road.
4. Collide with a building and boundary barrier; confirm the car remains stable and grounded.
5. Press R/Reset after a safe pose is recorded; confirm position and velocities reset.
6. Roll past 100 degrees and recover before two seconds; the ride must continue.
7. Remain flipped for two seconds; the ride must end and DRIVE AGAIN must restart it.
8. Sustain a drift and confirm score accumulation, multiplier growth, and combo banking after the recovery grace window.
9. Build an active combo, press Reset, and confirm the active points are discarded while the banked total remains unchanged.
10. Build an active combo and hit a solid obstacle hard enough to cancel it; verify a light contact only pauses scoring briefly.
11. End a ride by remaining flipped; confirm final and best score appear, then restart and verify a fresh zero-score ride.
12. Drive continuously for ten minutes and confirm no fall-through, console errors, or unstable camera motion.
13. On iPhone, steer from either half, swipe downward and hold to initiate a drift, then verify release recovery.
14. On iPhone, verify both landscape orientations, safe-area controls, and a 60 FPS profiler capture.
15. From the centre grid, reach the boulevard by at least two different routes.
16. Drive the scenic perimeter, both diagonal alleys, and all three hill connectors; confirm every route rejoins the network.
17. Drive for ten minutes while deliberately choosing unfamiliar turns; confirm there are no exposed road endings or accidental map-edge dead ends.
18. Drift in clear view of the police car; confirm the HUD changes from SPOTTED to PURSUED.
19. Break line of sight behind a solid building; confirm SEARCHING begins and the unit heads to the last visible position rather than following through the wall.
20. Reappear during search; confirm pursuit resumes immediately.
21. Break sight again and keep moving until the search expires; confirm ESCAPED, followed by a return to PATROL.
22. Stop while hidden during search; confirm the escape countdown pauses.
23. Allow police to remain close while the car is slow; confirm BUSTED resets the car and cancels only the active combo.
24. Press C, destroy one road sign, wait for it to respawn, and destroy it again; confirm it counts only once in the current challenge.
25. Destroy 10 different signs inside 60 seconds; confirm success, reward feedback, and uninterrupted driving.
26. Start the drift-score challenge and earn 5,000 drift points; confirm only drift points advance its progress.
27. Let either challenge expire, then retry it; confirm failure never ends or resets the ride.
28. Press C while a challenge is active; confirm it stops cleanly and remains available to retry.
29. Drive toward newly populated areas; confirm traffic never appears close to or directly in front of the car.
30. Pass a traffic car closely at meaningful relative speed without contact; confirm one near-miss award. Repeat after leaving and re-entering to confirm a new encounter can score.
31. Contact a traffic car while inside its near-miss zone; confirm the collision does not also award a near miss.
32. Follow a coin line and confirm every coin adds 100 banked points and increments the coin counter once.
33. Drive until fuel decreases, collect a green canister, and confirm fuel increases without exceeding 100.
34. Reach zero fuel and confirm propulsion stops while steering and coasting remain active; collect a canister to restore propulsion.

## Known limitations

- Driving values are first-pass graybox defaults and require device-based feel tuning.
- No reverse or service brake is available; the handbrake only initiates drifts and upright recovery uses the explicit reset action.
- Sunset Basin remains primitive graybox geometry and represents one compact LA-inspired slice, not a full city.
- The road graph is junction-level; Milestone 5 traffic uses directional lane offsets, while intersection priority, signals, lane changes, and police patrol weights remain deferred.
- Drift scoring currently has no discrete U-turn, proximity, traffic, or police-escape bonuses; those systems remain deferred.
- Milestone 4 police is deliberately limited to one unit and one heat level; radio dispatch, roadblocks, damage, and escalation are deferred.
- Audio, haptics, pedestrians, challenge discovery, persistent rewards, and progression are deferred.

## Development log

- 2026-10-18 — Milestones 0–1 foundation and first playable implemented from the approved brief.
- 2026-10-18 — Verification: 13 EditMode tests passed, 3 PlayMode tests passed, and Unity produced a successful iOS IL2CPP/Xcode export under `Builds/iOS`.
- 2026-10-18 — Device validation remains pending: full Xcode is not installed/selected (`xcode-select` points to Command Line Tools), so signing, `xcodebuild`, and iPhone profiling were not run.
- 2026-10-19 — Added handbrake drift initiation using Space or a downward touch swipe; verification passed with 14 EditMode and 4 PlayMode tests.
- 2026-10-19 — Milestone 2 added deterministic drift scoring, combo banking/cancellation, collision severity, score HUD, and local final/best ride results.
- 2026-10-19 — Milestone 2 verification: 24 EditMode tests and 4 PlayMode tests passed, and Unity produced a successful unsigned iOS Xcode export.
- 2026-10-19 — Milestone 3 added the Sunset Basin graybox neighborhood and reusable road graph; verification passed with 28 EditMode tests, 4 PlayMode tests, a live editor play check, and a successful unsigned iOS Xcode export.
- 2026-10-19 — Milestone 4 added sight-based police patrol, graph pursuit, last-known-position search, reacquisition, moving escape, a recoverable busted penalty, and police-state HUD feedback.
- 2026-10-19 — Milestone 4 verification: 36 EditMode tests and 4 PlayMode tests passed, the live editor check held 60 FPS with the patrol HUD active, and Unity produced a successful unsigned iOS Xcode export.
- 2026-10-20 — Milestone 5 added optional timed challenges, unique respawning road signs, sparse road-graph traffic, protected spawning, one-event near-miss scoring, and challenge/bonus HUD feedback.
- 2026-10-20 — Milestone 5 verification: 43 EditMode tests and 5 PlayMode tests passed, the live Game view confirmed the combined HUD and road-sign presentation, and Unity produced a successful unsigned iOS Xcode export.
- 2026-10-20 — Added replayable road coins and fuel canisters, deterministic fuel consumption, propulsion cutoff at empty, banked coin awards, and a combined resource HUD.
- 2026-10-20 — Collectible/fuel verification: 47 EditMode tests and 5 PlayMode tests passed; live Play Mode confirmed coin awards, fuel drain, canister refill, 60 FPS, and non-overlapping HUD presentation.
