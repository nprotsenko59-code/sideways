# SIDEWAYS development notes

## Architecture

- `VehicleController` owns Rigidbody forces and publishes read-only telemetry.
- `VehicleInputSource` converts keyboard and touch-zone input into smoothed normalized steering and handbrake demand.
- `VehicleRecovery` records safe road poses and emits reset/combo-cancel events.
- `VehicleCollisionSensor` reports obstacle impacts without owning scoring policy.
- `DriftScoreController` evaluates telemetry through a deterministic scoring model and exposes the optional ride-score provider.
- `RoadGraphAsset` is the stable, validated navigation source for both rendered roads and future police/traffic routing.
- `RoadGraphComponent` exposes the active graph from the scene and draws road-class gizmos when selected.
- `PoliceDirector` owns pursuit, wanted heat, reinforcement deployment, escape rewards, and penalties; each `PoliceVisionSensor` is a source of verified sightings, while coordinated `PoliceVehicleController` units follow the shared road graph.
- `RecklessDrivingTracker` turns sustained drifts and severe impacts into a short-lived reportable condition without deciding whether police can see them.
- `ChallengeController` owns the optional challenge lifecycle and consumes stable road-sign ids or drift-point events through a deterministic `ChallengeModel`.
- `TrafficSpawner` owns the local traffic population and protected spawn selection; each `TrafficVehicle` stays graph-bound and reports at most one near miss per encounter.
- `RideResourceController` owns per-ride coins and fuel, while `FuelModel` keeps consumption and refill rules deterministic.
- `PlayerPreferencesController` owns the pause-safe options overlay and applies persistent accessibility preferences.
- `GameFeedbackController` owns synthesized engine/event audio and rate-limited iOS haptics without affecting gameplay rules.
- `RideSessionController` owns the Driving/Ended lifecycle and two-second flip rule.
- `ChaseCameraController` owns switchable chase and overhead views, with damped look-ahead and chase-view obstruction checks.
- Handling, camera, ride rules, and drift scoring are ScriptableObject assets under `Assets/Sideways/Configuration`.
- The `PlayerCar` prefab keeps physics on its root and replaceable visuals below `Visual`.

## Milestone checklist

- [x] Unity 6000.6.6f1 project, URP, Input System, uGUI and test assemblies.
- [x] iOS-first landscape configuration and Xcode export entry point.
- [x] Directly playable DrivingSandbox scene and replaceable car prefab.
- [x] Automatic acceleration, analog-smoothed steering, lateral grip and drift telemetry.
- [x] Space-key and downward-swipe handbrake drift initiation.
- [x] Switchable chase/overhead camera, chase-view collision correction, and configurable presentation.
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
- [x] Graph-driven local traffic with protected spawning and solid player interaction.
- [x] Collision-free, relative-speed near-miss scoring with one event per encounter.
- [x] Challenge and bonus HUD feedback without interrupting free roam.
- [x] Road coin lines, banked coin awards, draining fuel, refill canisters, and resource HUD.
- [x] Persistent audio, haptics, high-contrast, large-HUD, and reduced-motion preferences.
- [x] Allocation-conscious HUD rendering plus toggleable FPS/GC telemetry.
- [x] Explicit iOS 15 baseline and separate development/release Xcode exports.
- [x] Three-level wanted heat with coordinated reinforcements, roadblock deployment, and escape rewards.
- [x] Pooled damageable traffic with junction yielding.
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
19. Break line of sight behind a solid building; confirm SEARCHING begins and the unit predicts the player's route, then sweeps nearby road junctions rather than stopping at the last visible position.
20. Reappear during search; confirm pursuit resumes immediately.
21. Break sight again, open at least 55 m of distance, and keep moving until the search expires; confirm ESCAPED, followed by a return to PATROL. Let the unit close the gap during another attempt and confirm escape progress reverses.
22. Stop while hidden during search; confirm the escape countdown pauses.
23. Allow police to remain close while the car is slow; confirm BUSTED resets the car and cancels only the active combo.
24. Press C, destroy one road sign, wait for it to respawn, and destroy it again; confirm it counts only once in the current challenge.
25. Destroy 10 different signs inside 60 seconds; confirm success, reward feedback, and uninterrupted driving.
26. Start the drift-score challenge and earn 5,000 drift points; confirm only drift points advance its progress.
27. Let either challenge expire, then retry it; confirm failure never ends or resets the ride.
28. Press C while a challenge is active; confirm it stops cleanly and remains available to retry.
29. Drive through several districts; confirm multiple traffic cars cycle into the local area without appearing directly in front of the player or overlapping one another.
30. Pass a traffic car closely at meaningful relative speed without contact; confirm one near-miss award. Repeat after leaving and re-entering to confirm a new encounter can score.
31. Contact a traffic car while inside its near-miss zone; confirm the collision does not also award a near miss.
32. Follow a coin line and confirm every coin adds 100 banked points and increments the coin counter once.
33. Drive until fuel decreases, collect a green canister, and confirm fuel increases without exceeding 100.
34. Reach zero fuel and confirm propulsion stops while steering and coasting remain active; collect a canister to restore propulsion.
35. Open OPTIONS with Escape/P or the touch button; confirm the ride pauses and every preference persists after a restart.
36. Toggle high contrast and large HUD; confirm all settings text remains readable in both landscape orientations.
37. Toggle reduced motion; confirm the camera uses less look-ahead and settles faster without changing vehicle handling.
38. Toggle audio and haptics independently; confirm each suppresses only its own feedback channel.
39. On a development device build, toggle F3 and confirm 60 FPS with `GC 0 B` during steady straight-line driving.
40. Confirm the ride starts in TOP view. Press V and use the Camera button to switch between TOP and CHASE; confirm both transitions are smooth, the car remains readable, the overhead heading follows turns without snapping, and touch steering still works.
41. Trigger pursuit, then continue drifting in police view; confirm heat rises from one to three stars and the active unit count grows from one to three.
42. At three stars, confirm the visible roadblock deploys across the southern central road while leaving a narrow escape gap; escape or get busted and confirm it retracts.
43. Escape at each heat level and confirm the banked reward is 500 points per star; confirm an active combo is not modified.
44. Observe two traffic cars reaching the same junction; confirm one yields until the other clears it.
45. Hit a traffic car at high relative speed; confirm it is temporarily removed and later returns from the existing pool.

## Known limitations

- Driving values are first-pass graybox defaults and require device-based feel tuning.
- No reverse or service brake is available; the handbrake only initiates drifts and upright recovery uses the explicit reset action.
- Sunset Basin remains primitive graybox geometry and represents one compact LA-inspired slice, not a full city.
- The road graph is junction-level; traffic now has exclusive junction yielding, while signals, directional priority, and lane changes remain deferred.
- Drift scoring currently has no discrete U-turn, proximity, traffic, or police-escape bonuses; those systems remain deferred.
- Police escalation currently caps at three identical units and one fixed authored roadblock; dynamic placement, radio dispatch, and specialist units are deferred.
- Imported production audio, pedestrians, challenge discovery, persistent rewards, and progression are deferred.

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
- 2026-10-22 — Milestone 6 added persistent accessibility/options controls, synthesized audio, iOS haptics, reduced-motion camera behavior, allocation-conscious HUD updates, GC telemetry, an explicit iOS 15 floor, and separate development/release export paths.
- 2026-10-22 — Milestone 6 verification: 49 EditMode tests and 5 PlayMode tests passed, the options overlay was exercised in live Play Mode, and Unity produced a successful unsigned iOS Xcode export.
- 2026-10-22 — Physical device acceptance remains pending because full Xcode and device tooling are not installed; `xcode-select` currently points to Command Line Tools.
- 2026-10-22 — Strengthened police pursuit with predictive interception, road-junction search sweeps, distance-gated escape progress, wider detection, faster replanning, and more capable pursuit handling; verification passed with 52 EditMode and 5 PlayMode tests.
- 2026-10-22 — Promoted the overhead view to the default and retuned it to a closer, oblique arcade composition with forward framing and damped heading changes, while retaining chase view as an alternate.
- 2026-10-22 — Increased the local traffic pool from four to eight cars, tightened its spawn/despawn ring for the overhead camera, and added minimum spawn spacing to prevent vehicle stacking.
- 2026-10-22 — Milestone 7 added three-level wanted heat, coordinated police reinforcements, a high-heat roadblock, heat-scaled escape rewards, junction-yielding traffic, and temporary traffic wrecks.
- 2026-10-22 — Milestone 7 verification: 59 EditMode tests and 5 PlayMode tests passed, the rebuilt scene ran cleanly in Play Mode, and Unity produced a successful unsigned iOS Xcode export.
