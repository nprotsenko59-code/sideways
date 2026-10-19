# SIDEWAYS

Unity `6000.6.6f1` graybox prototype for an iOS-first arcade drift game.

## Open and play

1. Open this folder with Unity Hub using Unity `6000.6.6f1`.
2. Open `Assets/Sideways/Scenes/DrivingSandbox.unity`.
3. Press Play.

Controls:

- A/D or left/right arrows: steer.
- Hold the left or right half of the screen on touch devices: steer.
- R or the Reset button: return to the last safe road pose.
- F3: toggle the debug telemetry panel.

The car accelerates automatically. There is no brake or reverse in this prototype. Remaining flipped for two seconds ends the ride; the restart overlay begins a fresh ride.

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

See [Documentation/DEVELOPMENT.md](Documentation/DEVELOPMENT.md) for architecture, QA, and current limitations.
