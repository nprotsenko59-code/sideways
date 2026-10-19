# SIDEWAYS development notes

## Architecture

- `VehicleController` owns Rigidbody forces and publishes read-only telemetry.
- `VehicleInputSource` converts keyboard and touch-zone input into smoothed normalized steering and handbrake demand.
- `VehicleRecovery` records safe road poses and emits reset/combo-cancel events.
- `VehicleCollisionSensor` reports obstacle impacts without owning scoring policy.
- `DriftScoreController` evaluates telemetry through a deterministic scoring model and exposes the optional ride-score provider.
- `RoadGraphAsset` is the stable, validated navigation source for both rendered roads and future police/traffic routing.
- `RoadGraphComponent` exposes the active graph from the scene and draws road-class gizmos when selected.
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

## Known limitations

- Driving values are first-pass graybox defaults and require device-based feel tuning.
- No reverse or service brake is available; the handbrake only initiates drifts and upright recovery uses the explicit reset action.
- Sunset Basin remains primitive graybox geometry and represents one compact LA-inspired slice, not a full city.
- The road graph is junction-level only; lane direction, traffic priority, and police patrol weights are deferred.
- Drift scoring currently has no discrete U-turn, proximity, traffic, or police bonuses; those systems remain deferred.
- Audio, haptics, police, traffic, challenges, and progression are deferred.

## Development log

- 2026-10-18 — Milestones 0–1 foundation and first playable implemented from the approved brief.
- 2026-10-18 — Verification: 13 EditMode tests passed, 3 PlayMode tests passed, and Unity produced a successful iOS IL2CPP/Xcode export under `Builds/iOS`.
- 2026-10-18 — Device validation remains pending: full Xcode is not installed/selected (`xcode-select` points to Command Line Tools), so signing, `xcodebuild`, and iPhone profiling were not run.
- 2026-10-19 — Added handbrake drift initiation using Space or a downward touch swipe; verification passed with 14 EditMode and 4 PlayMode tests.
- 2026-10-19 — Milestone 2 added deterministic drift scoring, combo banking/cancellation, collision severity, score HUD, and local final/best ride results.
- 2026-10-19 — Milestone 2 verification: 24 EditMode tests and 4 PlayMode tests passed, and Unity produced a successful unsigned iOS Xcode export.
- 2026-10-19 — Milestone 3 added the Sunset Basin graybox neighborhood and reusable road graph; verification passed with 28 EditMode tests, 4 PlayMode tests, a live editor play check, and a successful unsigned iOS Xcode export.
