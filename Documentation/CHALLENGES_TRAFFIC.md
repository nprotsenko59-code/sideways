# Milestone 5 challenge and traffic contract

## Optional challenge loop

- Challenges are ScriptableObject definitions containing a stable id, display name, event type, duration, target, and reward.
- `C` or the HUD button starts the selected challenge. Pressing it while active stops the challenge without affecting the ride.
- Failure, cancellation, and success never end free roam, reset the player, or alter police state.
- A failed or stopped challenge remains selected for an immediate retry. Success advances the next start to the following definition.
- Rewards are emitted through a typed `ChallengeReward` event and awarded as banked score. They do not modify or cancel the active drift combo.

## Included challenges

### Destroy 10 road signs

- Target: 10 unique signs in 60 seconds.
- Signs emit their stable authored id when destroyed, disappear for 12 seconds, and respawn for free roam.
- The challenge model stores counted ids for the current attempt, so a respawned sign cannot be counted twice.
- Restarting the challenge clears the attempt-local id set.

### Earn 5,000 drift score

- Target: earn 5,000 telemetry-qualified drift points in 60 seconds.
- Only positive points emitted by the drift scorer count. Near-miss and challenge-reward bonuses do not count toward drift progress.
- Banking or losing a combo does not subtract already-earned challenge progress.

## Ambient traffic

- The default profile caps the scene at four traffic vehicles.
- Cars spawn on road-graph centerlines, choose a travel direction, keep to that direction's lane offset, and continue through connected edges.
- Traffic speed derives from the active edge's speed limit and remains inside profile limits.
- Kinematic vehicle bodies have solid colliders and slow for vehicles, police, the player, and obstacles ahead.
- Cars beyond the despawn radius are repositioned to another eligible road location.

## Spawn safety

- Candidates must be between the configured minimum and maximum distance from the player.
- Candidates inside the forward exclusion cone are rejected, preventing repeated pop-in directly ahead.
- A bounded number of attempts is used; if no safe candidate exists, that traffic car remains inactive until a later reposition opportunity.
- Traffic can be disabled independently on `TrafficSpawner` for debugging.

## Near misses

- Each traffic car owns one proximity trigger and one encounter state.
- An encounter records closest distance, maximum relative speed, and whether a physical collision occurred.
- A score event is emitted only after leaving the trigger, only when relative speed meets the configured threshold, and never after a collision.
- One trigger entry/exit cycle can emit at most one typed `NearMissEvent`.
- Near-miss points are banked immediately and shown through the bonus HUD without affecting the active combo.

## Deferred

Intersection priority, signals, overtaking, lane changes, traffic pooling, traffic damage, pedestrians, richer challenge discovery, persistent rewards, and additional challenge types remain future work.
