---
status: resolved
trigger: "Ok, turning left and right on the car feels wrong: like it doesn't turn beyond certain point"
created: 2026-10-27
updated: 2026-10-27
---

## Symptoms

- Expected: sustained full left/right input should retain useful steering authority and continue rotating the car through a corner.
- Actual: turning response plateaus early and feels capped.
- Errors: none reported.
- Timeline: observed with the current axle/tire handling model.
- Reproduction: drive normally and hold full left or right steering.

## Current Focus

- hypothesis: high-speed steering reduction and always-on countersteer assist combine to cancel too much requested steering during an ordinary corner.
- test: quantify full-input steering at speed and isolate whether countersteer assist opposes same-direction cornering.
- expecting: the 11-degree high-speed floor can be reduced by up to another 5 degrees, leaving roughly 6 degrees of effective steering at maximum speed.
- next_action: resolved; await physical feel feedback.

## Evidence

- timestamp: 2026-10-27
  finding: Default high-speed steering is 11 degrees at 28 m/s and countersteer assist is added unconditionally with a 5-degree cap.
- timestamp: 2026-10-27
  finding: Input smoothing reaches full digital steering in about 0.22 seconds, so input latency is not the limiting factor.
- timestamp: 2026-10-27
  finding: After restoring front steering authority, the feint regression measured only 8.49 degrees of rear slip against an 11-degree rear saturation threshold; the rear axle remained too planted to rotate with the stronger front response.

## Eliminated

- hypothesis: steering input never reaches full scale.
  reason: VehicleInputSource targets exactly -1 or +1 and MoveSteering reaches that target without overshoot.

## Resolution

- root_cause: The 11-degree high-speed steering floor was reduced by unconditional recovery assist during ordinary cornering, while the rear tire curve remained too planted after front steering authority was restored.
- fix: Raised the 38/20-degree steering envelope with reduction above maximum road speed, gated recovery assist behind active-drift deliberate countersteer, and reduced rear peak grip to 8.8 with an 8-degree breakaway target.
- verification: 80/80 EditMode tests and 12/12 PlayMode tests passed, including sustained road-speed steering, handbrake recovery, and no-handbrake feint saturation.
- files_changed: VehicleController.cs, VehicleMath.cs, VehicleHandlingProfile.cs, DefaultVehicleHandling.asset, VehicleMathTests.cs, ProjectAssetTests.cs, VehiclePlayModeTests.cs, Documentation/DEVELOPMENT.md
