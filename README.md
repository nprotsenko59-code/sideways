# SIDEWAYS

Unity `6000.6.6f1` graybox prototype for an iOS-first portrait arcade drift game. The directly playable scene is a focused 320 × 720 m northbound drift run: one broad, continuous road combines readable straights with long, rounded S-curves and solid visible boundaries.

## Open and play

1. Open this folder with Unity Hub using Unity `6000.6.6f1`.
2. Open `Assets/Sideways/Scenes/DrivingSandbox.unity`.
3. Press Play.

Controls:

- Hold A/D, the left/right arrows, or either half of the screen: steer and progressively build drift strength. A quick tap makes a light correction; a longer hold produces a stronger slide.
- Space or the BRAKE button: apply the regular service brake.
- R or the Reset button: return to the last safe road pose.
- C or the Challenge button: start, stop, continue to the next challenge, or retry a failed challenge.
- F3: toggle the debug telemetry panel.
- V or the Camera button: switch between the primary overhead view and the alternate chase view.
- Escape or P: pause and open options/accessibility settings.

The car accelerates automatically. Steering uses speed-sensitive front-axle geometry and progressive front/rear tire grip. Holding a direction progressively increases steering-drift assistance: the rear grip unloads and yaw authority grows over 1.1 seconds, so input duration controls drift intensity without a handbrake. Countersteering and a light recovery assist control the angle. The regular brake reduces forward speed without initiating a drift. Rear smoke, skid trails, body movement, visible steering, tire noise, and drift-entry haptics communicate the available grip. There is no reverse. Sustained, controlled slides build a live combo whose multiplier grows with drift time. Brief recovery gaps preserve the combo; the combo banks after the grace window. A road reset, a severe obstacle impact, or ending the ride discards only the unbanked combo and never reduces banked score.

One police unit patrols the road network. Visible reckless driving starts one-star pursuit; continued visible recklessness escalates to three stars, dispatching up to three coordinated units and a high-heat roadblock. To escape, break line of sight, open at least 55 m of distance, and keep moving through the 24-second search; letting units close the gap reverses escape progress. Escapes award 500 banked points per attained heat level. If police catch a slow car, the active combo is cancelled and the car returns to its last safe pose; banked score and the ride continue.

Civilian traffic follows the road graph with an eight-car pooled population, protected spawn spacing, junction yielding, and a tight active radius around the player. Hard player impacts temporarily wreck traffic cars; fast, collision-free passes award one near-miss bonus per encounter. Optional 60-second events include destroying 10 unique road signs and earning 5,000 drift points; completing or failing either event leaves free roam uninterrupted.

Coins are arranged in readable lines along major roads and add 100 banked points each. Fuel drains only while the car is moving; green canisters refill 35 fuel. At zero fuel, automatic propulsion stops but steering, coasting, and road reset remain available. Resetting with an empty tank grants 15 emergency fuel so an isolated car cannot become permanently stranded. Coins and canisters respawn to keep free roam replayable.

Remaining flipped for two seconds ends the ride. The results overlay shows the run score and locally retained best score, and DRIVE AGAIN starts a fresh ride without leaving the sandbox.

The default camera uses a close, oblique overhead composition with forward framing and gently damped heading changes; the original chase view remains available for comparison. The options overlay persists audio, haptics, high-contrast UI, large-HUD and reduced-motion preferences. Lightweight engine/event audio is generated at runtime, while supported iOS devices receive rate-limited haptic feedback for meaningful events.

## Rebuild generated prototype assets

Run `Sideways > Rebuild Prototype` in the Unity editor, or:

```sh
/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath "$PWD" \
  -executeMethod Sideways.Editor.ProjectBootstrap.Configure \
  -logFile Logs/bootstrap.log
```

## Tests

```sh
mkdir -p Logs TestResults
/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD" -runTests -testPlatform editmode \
  -testResults "$PWD/TestResults/editmode.xml" -logFile "$PWD/Logs/editmode.log"

/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD" -runTests -testPlatform playmode \
  -testResults "$PWD/TestResults/playmode.xml" -logFile "$PWD/Logs/playmode.log"
```

## iOS export

The iOS module is required. Export an unsigned development Xcode project with:

```sh
/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath "$PWD" \
  -executeMethod Sideways.Editor.ProjectBootstrap.BuildIos \
  -logFile "$PWD/Logs/ios-build.log"
```

Open `Builds/iOS/Unity-iPhone.xcodeproj`, choose an Apple developer team and device, then build from Xcode. Device performance claims require an iPhone 15-class or newer profiler capture; Editor and Simulator results do not qualify.

For a non-development export, run `Sideways > Build iOS Release` in Unity or invoke `Sideways.Editor.ProjectBootstrap.BuildIosRelease`; it writes to `Builds/iOS-Release` without development or script-debugging flags.

See [Documentation/SCORING.md](Documentation/SCORING.md) for scoring, [Documentation/NEIGHBORHOOD.md](Documentation/NEIGHBORHOOD.md) for the map and road-graph contract, [Documentation/POLICE.md](Documentation/POLICE.md) for pursuit rules, [Documentation/CHALLENGES_TRAFFIC.md](Documentation/CHALLENGES_TRAFFIC.md) for Milestone 5 rules, [Documentation/MOBILE_RELEASE.md](Documentation/MOBILE_RELEASE.md) for Milestone 6 mobile/release requirements, [Documentation/WANTED_TRAFFIC.md](Documentation/WANTED_TRAFFIC.md) for Milestone 7, [Documentation/ASSET_IMPORT.md](Documentation/ASSET_IMPORT.md) for asset handoff rules, and [Documentation/DEVELOPMENT.md](Documentation/DEVELOPMENT.md) for architecture and QA.
