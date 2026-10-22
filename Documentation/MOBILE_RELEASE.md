# Milestone 6 mobile polish and release contract

## Supported baseline

- Unity `6000.6.6f1`, URP and IL2CPP.
- iOS 15.0 or newer.
- iPhone is the primary device family.
- Landscape left and landscape right are supported; portrait is disabled.
- The performance target is 60 FPS on iPhone 15-class hardware or newer.

## Player-facing polish

- The OPTIONS button or Escape/P opens a pause-safe settings overlay.
- Audio, haptics, high contrast, large HUD and reduced camera motion are persistent local preferences.
- Reduced motion shortens camera look-ahead and increases follow damping.
- Audio uses a lightweight synthesized engine loop and event cues, requiring no imported placeholder audio assets.
- Haptics fire only on iOS for meaningful pickups, impacts, rewards and ride-ending events, with rate limiting.

## Performance contract

- The debug HUD starts hidden and can be toggled with F3.
- The debug HUD reports smoothed FPS and `GC Allocated In Frame` bytes for device captures.
- Frequently updated HUDs cache rendered values so ordinary straight-line driving does not rebuild text every frame.
- Device acceptance requires a Development build attached to the Unity Profiler for a continuous ten-minute drive.
- The accepted capture must hold 60 FPS without recurring steady-state managed allocations. One-time scene-load, audio-generation and UI-opening allocations are allowed.

## Build variants

- `Sideways > Build iOS Development` exports `Builds/iOS` with script debugging for profiling.
- `Sideways > Build iOS Release` exports `Builds/iOS-Release` without development flags.
- Both exports are unsigned. Developer-team selection, provisioning and App Store metadata remain Xcode responsibilities.

## Physical-device gate

The following cannot be accepted from the Editor or Simulator:

1. Safe-area layout in both landscape orientations.
2. Touch steering, downward-swipe handbrake and settings interaction.
3. Audio routing and haptic feel on hardware.
4. Ten-minute thermal/performance capture at 60 FPS.
5. Zero recurring managed allocations during steady-state driving.

Full Xcode must be installed and selected with `xcode-select` before device discovery, signing or profiler attachment can run.
