# Milestone 4 police contract

## Player promise

Police pressure is readable and escapable. A unit can react only to reckless driving it actually sees, remembers only the most recent verified sighting, searches that area when visual contact breaks, and can be escaped by continuing to move unseen. The system never reads a hidden player's live position for pursuit decisions.

## Wanted triggers

- A sustained drift at or above the configured trigger speed is reckless for a short memory window.
- A severe obstacle collision is immediately reckless for the same window.
- Reckless behavior starts pursuit only while the police vision sensor has unobstructed visual contact.
- Ordinary driving, hidden drifting, and hidden collisions do not create heat.
- Milestone 4 established the single-unit pursuit lifecycle; Milestone 7 layers three-level heat and reinforcement deployment over that lifecycle.

## Sighting and search

- The sensor uses a configurable horizontal vision cone, range, and an obstacle-layer line cast.
- Buildings and solid boundary geometry block sight. Roads and decorative geometry do not.
- Last-known position updates only from a successful sensor sighting.
- Loss of sight begins a short pursuit memory, then transitions to a 15-second search at the last-known road area.
- Any visual reacquisition during search immediately resumes pursuit; no new reckless act is required.
- The search timer advances only while the player is moving at the configured minimum escape speed. Stationary hiding cannot complete an escape.
- Completing the unseen moving search enters `Escaped` briefly, then returns the unit to `Patrol`.

## Navigation and interception

- The police car follows the shared `RoadGraphAsset`; it does not drive directly through buildings.
- Patrol cycles through authored graph nodes.
- Pursuit routes toward the nearest graph node to a short velocity-based intercept prediction while visual contact exists.
- Without visual contact, pursuit and search route only toward the stored last-known position.
- Route calculations are reused while the target remains associated with the same graph node.

## Busted condition and penalty

- A bust requires continuous visual contact while the police is within 4.5 metres and the player remains at or below 6 m/s for 1.25 seconds.
- A fast pass or momentary proximity is not a bust.
- A bust cancels the active drift combo and invokes the existing safe-road reset.
- Banked score is never deducted, the ride does not end, and the unit returns to patrol.

## Runtime states and events

- State enum: `Patrol`, `Pursuit`, `Search`, `Escaped`.
- Typed events report state transitions, status snapshots, sightings, busts, and escapes.
- The HUD presents `PATROL`, `SPOTTED` / `PURSUED`, `SEARCHING`, `ESCAPED`, and `BUSTED` without owning gameplay decisions.

## Deferred

Radio dispatch presentation, dynamic roadblock placement, specialist police vehicles, arrest cinematics, and persistent police damage remain later work.
