# SIDEWAYS

Unity `6000.6.6f1` graybox prototype for an iOS-first arcade drift game. The directly playable Sunset Basin scene is a fixed LA-inspired neighborhood with an urban grid, boulevard, curved scenic roads, and alley shortcuts.

## Open and play

1. Open this folder with Unity Hub using Unity `6000.6.6f1`.
2. Open `Assets/Sideways/Scenes/DrivingSandbox.unity`.
3. Press Play.

Controls:

- A/D or left/right arrows: steer.
- Hold the left or right half of the screen on touch devices: steer.
- Hold Space while steering: engage the handbrake and initiate a drift.
- On touch devices, swipe downward in either steering half and keep holding; releasing ends the handbrake after a short drift-entry pulse.
- R or the Reset button: return to the last safe road pose.
- C or the Challenge button: start, stop, continue to the next challenge, or retry a failed challenge.
- F3: toggle the debug telemetry panel.
- V or the Camera button: switch between the primary overhead view and the alternate chase view.
- Escape or P: pause and open options/accessibility settings.

The car accelerates automatically. The handbrake is for drift initiation rather than stopping, and there is no reverse. Sustained, controlled slides build a live combo whose multiplier grows with drift time. Brief recovery gaps preserve the combo; the combo banks after the grace window. A road reset, a severe obstacle impact, or ending the ride discards only the unbanked combo and never reduces banked score.

One police unit patrols the road network. If it sees a sustained drift or severe crash, it pursues using the shared road graph and predicts travel after losing sight. To escape, break line of sight, open at least 55 m of distance, and keep moving through the 24-second search; letting the unit close the gap reverses escape progress. If it catches a slow car, the active combo is cancelled and the car returns to its last safe pose; banked score and the ride continue.

Civilian traffic follows the road graph with an eight-car local population, protected spawn spacing, and a tighter active radius around the player. Fast, collision-free passes award one near-miss bonus per encounter. Optional 60-second events include destroying 10 unique road signs and earning 5,000 drift points; completing or failing either event leaves free roam uninterrupted.

Coins are arranged in readable lines along major roads and add 100 banked points each. Fuel drains only while the car is moving; green canisters refill 35 fuel. At zero fuel, automatic propulsion stops but steering, coasting, and road reset remain available. Coins and canisters respawn to keep free roam replayable.

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

See [Documentation/SCORING.md](Documentation/SCORING.md) for scoring, [Documentation/NEIGHBORHOOD.md](Documentation/NEIGHBORHOOD.md) for the map and road-graph contract, [Documentation/POLICE.md](Documentation/POLICE.md) for pursuit rules, [Documentation/CHALLENGES_TRAFFIC.md](Documentation/CHALLENGES_TRAFFIC.md) for Milestone 5 rules, [Documentation/MOBILE_RELEASE.md](Documentation/MOBILE_RELEASE.md) for Milestone 6 mobile/release requirements, [Documentation/ASSET_IMPORT.md](Documentation/ASSET_IMPORT.md) for asset handoff rules, and [Documentation/DEVELOPMENT.md](Documentation/DEVELOPMENT.md) for architecture and QA.
