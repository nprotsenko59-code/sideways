# SIDEWAYS development notes

## Architecture

- `VehicleController` owns Rigidbody forces and publishes read-only telemetry.
- `VehicleInputSource` converts keyboard and touch-zone input into smoothed normalized steering and handbrake demand.
- `VehicleRecovery` records safe road poses and emits reset/combo-cancel events.
- `RideSessionController` owns the Driving/Ended lifecycle and two-second flip rule.
- `ChaseCameraController` is a custom damped follow camera with look-ahead and obstruction checks.
- Handling, camera, and ride rules are ScriptableObject assets under `Assets/Sideways/Configuration`.
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
- [ ] Physical iPhone profiling and touch QA.

## Manual QA

1. Navigate the central four-way intersection using A/D or arrows.
2. Hold steering through an outer corner, press Space, observe BRAKE = ON followed by nonzero slip and DRIFT = YES, then release and recover.
3. Perform a forward 180-degree U-turn on a cross road.
4. Collide with a building and boundary barrier; confirm the car remains stable and grounded.
5. Press R/Reset after a safe pose is recorded; confirm position and velocities reset.
6. Roll past 100 degrees and recover before two seconds; the ride must continue.
7. Remain flipped for two seconds; the ride must end and DRIVE AGAIN must restart it.
8. Drive continuously for ten minutes and confirm no fall-through, console errors, or unstable camera motion.
9. On iPhone, steer from either half, swipe downward and hold to initiate a drift, then verify release recovery.
10. On iPhone, verify both landscape orientations, safe-area controls, and a 60 FPS profiler capture.

## Known limitations

- Driving values are first-pass graybox defaults and require device-based feel tuning.
- No score provider exists yet, so the results view intentionally hides score fields.
- No reverse or service brake is available; the handbrake only initiates drifts and upright recovery uses the explicit reset action.
- The environment is a driving sandbox, not the LA neighborhood slice.
- Audio, haptics, police, traffic, challenges, and progression are deferred.

## Development log

- 2026-10-18 — Milestones 0–1 foundation and first playable implemented from the approved brief.
- 2026-10-18 — Verification: 13 EditMode tests passed, 3 PlayMode tests passed, and Unity produced a successful iOS IL2CPP/Xcode export under `Builds/iOS`.
- 2026-10-18 — Device validation remains pending: full Xcode is not installed/selected (`xcode-select` points to Command Line Tools), so signing, `xcodebuild`, and iPhone profiling were not run.
- 2026-10-19 — Added handbrake drift initiation using Space or a downward touch swipe; verification passed with 14 EditMode and 4 PlayMode tests.
