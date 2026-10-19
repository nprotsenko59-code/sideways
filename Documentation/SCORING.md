# SIDEWAYS Milestone 2 scoring contract

This contract defines the first playable drift-scoring loop. All balance values live in `DefaultScoring.asset` and can be tuned without changing code.

## Scoring eligibility

A physics tick scores only when the car is grounded, the vehicle controller reports a drift, speed is at least 6 m/s, absolute slip is between 8° and 70°, vehicle tilt is at most 45°, and yaw rate is at most 180°/s. These bounds reject stationary steering, airborne motion, backward spins, flips, and high-rate donut exploits.

## Formula

Base score is 100 points per second. Slip quality rises from a 0.4 factor at 8° to full value at the 35° ideal angle, then falls toward 0.4 at 70°. Speed scales the result from 1× at 6 m/s to 1.75× at 25 m/s. The active combo multiplier is applied last.

Scoring delta time is capped at 0.1 seconds per update so an editor stall, application resume, or frame spike cannot award a burst of points.

## Combo lifecycle

- A combo begins as soon as an eligible drift earns points.
- Drift time—not grace time—increases the multiplier by 0.25 every two seconds, up to 3×.
- Leaving a valid drift starts a 1.5-second grace window. Resuming a valid drift continues the same combo.
- Expiring the grace window banks the active combo into the ride total.
- A road reset, sustained flip, or collision at 7 m/s relative speed or higher cancels the unbanked combo.
- Cancellation never removes already banked score.
- Lesser collisions interrupt scoring for 0.4 seconds but preserve the combo while its grace window remains.

## Persistence and results

The ride result records the final banked score through `RideRecordStore`. Last and best scores use local `PlayerPrefs` storage. Restart begins a new score session while retaining those records.

## Deferred extensions

Discrete U-turn bonuses, near misses, traffic interactions, police escape bonuses, challenges, and progression modifiers remain outside this milestone. A U-turn can still earn ordinary drift points when it satisfies the eligibility rules above.
