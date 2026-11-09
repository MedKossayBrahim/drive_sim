# Forest Bike Ride

A mobile bicycle game built in Unity (Unity 6, URP) where the player rides a bike through a forest island, collects coins, jumps ramps, dodges barriers, and reaches the finish-line ball.

The game is designed for Android in landscape orientation with tilt steering.

---

## Features

### Core gameplay

- **Bike physics controller** — WheelCollider-based two-wheel bike with motor torque, braking, reverse, speed limits, and uphill torque assist. Forward = joystick up, reverse = joystick down, steering = phone tilt.
- **Tilt steering** — The phone accelerometer drives left/right steering. Calibration, invert, dead zone, and sensitivity are adjustable at runtime via on-screen buttons. Tilt left steers left, tilt right steers right.
- **Joystick controls** — On-screen joystick controls forward/backward throttle (push up to accelerate, pull down to brake/reverse). A separate BOOST button provides a speed multiplier.
- **PEDAL / auto-pedal** — In tilt mode, holding the left pad acts as full-power PEDAL; releasing it engages automatic pedal assist that increases on uphills so the bike keeps climbing without stalling.
- **Wheelie** — Tilting the phone back lifts the front wheel via a torque-based controller. A hard-limit angle prevents flipping over.
- **Bike stability assist** — Applies roll-correction torque to keep the bike upright at all speeds, with low-speed pitch assist. Tuned to keep the bike from falling over during turns without breaking hill climbing.

### Level content

- **Forest island terrain** — A painted-terrain island with trees, road splat layers, and invisible world-bound walls that keep the bike on the map.
- **Road route** — A defined riding path from the spawn point through to the finish ball, following the painted road on the terrain.
- **10 collectible coins** — Spinning gold coins placed along the road (before the first ramp, between the two ramps, and after the second ramp leading to the finish). Each coin is collected on bike contact with a particle burst effect.
- **2 jump ramps** — Quarter-pipe kicker ramps placed on the road. The bike can ride up, launch, and land. Ramps are aligned with the route direction.
- **4 road barriers** — Small solid obstacles on the road. Hitting one costs exactly 1 heart with a cooldown to prevent multi-hit abuse.
- **Finish-line ball** — A soccer ball placed after the second ramp. Touching it triggers "LEVEL COMPLETE!" and shows the coin count with a REPLAY button.
- **Road sign** — A west-east road sign prop along the route.
- **Decorative stones** — Forest-pack stone meshes scattered along and beside the road.

### Hearts and game over

- **3-heart life system** — The player starts with 3 hearts drawn in the top-right corner of the screen.
- **Ramp-jump penalty** — A failed ramp jump (bad landing, crash, or fall) removes 1 heart. A clean jump and landing costs none.
- **Barrier collision penalty** — Hitting a road barrier removes exactly 1 heart. A 1.5-second cooldown prevents repeated heart loss while the bike remains in contact.
- **Game over** — When all 3 hearts are lost, the bike freezes, controls disable, and a "GAME OVER" screen appears with a REPLAY button.
- **Replay** — Reloads the entire scene, resetting the bike to its spawn point, hearts to 3, coins, ramps, barriers, and the finish ball to their initial states.

### Camera

- **Chase camera** — A third-person camera that follows the bike with smooth position/rotation interpolation, collision avoidance, and look-ahead.

### Haptics

- **Android vibration** — The phone vibrates on impacts with obstacles, trees, rocks, and barriers. Vibration strength scales with impact speed. A cooldown prevents continuous vibration during sustained contact.

### UI (all IMGUI, no Canvas required for gameplay HUD)

- **On-screen joystick** — Throttle control (forward/backward only).
- **BOOST button** — Right-side button for speed boost.
- **TILT / CAL / INV buttons** — Toggle tilt mode, calibrate neutral tilt, and invert steering axis.
- **Coin counter HUD** — Top-right coin count with icon.
- **Heart display** — Three hearts below the coin HUD.
- **Tilt debug overlay** — Optional on-screen diagnostics showing sensor vector, calibrated tilt, and final steering values.
- **GAME OVER screen** — Full-screen overlay with REPLAY button.
- **LEVEL COMPLETE screen** — Full-screen overlay with coin count and REPLAY button.

### Landscape orientation

- The game opens in landscape (LandscapeLeft) and stays landscape throughout. Portrait is disabled at the project level.

---

## Project structure

```
Assets/
  BikeGame/
    Scripts/          Gameplay scripts (controls, hearts, coins, barriers, camera, vibration, etc.)
    Editor/           Editor tools (scene builder, ramp/coin/barrier placement, APK build, verification)
    Scenes/           ForestBikeRide.unity — the main playable scene
    Ramp/             Ramp FBX model, materials, and textures
    Coins/            Coin model, gold material, burst particle material, coin prefab
    Ball/             Soccer ball mesh and textures for the finish-line ball
    UI/               UI assets
  SimpleBikeControl/  Third-party bike controller (SimpleBike, InputManager, Biker, PlayerController, GameManager, EventManager)
  Settings/           URP render pipeline assets (Mobile and PC profiles)
  Resources/          Runtime-loaded assets (soccer_ball.glb for the finish ball)
  Scenes/             Default Unity sample scene
```

### Key scripts

| Script | Purpose |
|---|---|
| `SimpleBike.cs` | Core bike physics: WheelColliders, motor/brake torque, steering, center of mass, speed limits, uphill assist |
| `BikeStabilityAssist.cs` | Roll and pitch correction to keep the bike upright |
| `BikeWheelie.cs` | Front-wheel lift via torque, driven by phone tilt |
| `MobileBikeControls.cs` | On-screen joystick, BOOST button, tilt mode toggle, throttle routing |
| `TiltSteering.cs` | Accelerometer-based steering with calibration, invert, dead zone, and smoothing |
| `BikeGameBootstrap.cs` | Runtime scene assembly: spawns bike/rider, mounts rider, sets up camera, creates world bounds |
| `BikeChaseCamera.cs` | Third-person chase camera with smoothing and collision |
| `HeartSystem.cs` | 3-heart life counter, ramp-jump penalty, barrier damage, game over, replay, heart UI |
| `BarrierCollision.cs` | Detects bike-vs-barrier collisions and calls HeartSystem.TakeDamage |
| `CoinCollectible.cs` | Spinning coin with trigger collection and particle burst |
| `CoinCounter.cs` | Singleton coin count with HUD label |
| `LevelCompleteTrigger.cs` | Finish-ball trigger, stops the ride, shows LEVEL COMPLETE, handles replay |
| `BikeImpactFeedback.cs` | Collision detection and vibration trigger with cooldown |
| `BikeVibration.cs` | Android vibration wrapper (API-level aware) |
| `BallVisualSetup.cs` | Loads the soccer ball GLB visual at runtime |

### Editor tools

| Script | Purpose |
|---|---|
| `BikeGameSceneBuilder.cs` | Builds the forest ride scene from scratch |
| `RampSetup.cs` | Imports and places the ramp mesh aligned to the route |
| `BikeCoinSceneSetup.cs` | Creates the coin prefab and distributes coins along the road |
| `PlaceBarriers.cs` | Places 4 road barriers along the route using terrain heights |
| `BikeBallSetup.cs` | Sets up the finish-line ball in the scene |
| `CoinHudSetup.cs` | Builds the coin counter HUD canvas |
| `BikeGameApkBuild.cs` | Batch-mode APK build entry point |
| `BikeGameAndroidSetup.cs` | Android post-process gradle configuration |
| `BikeGameVerification.cs` | Automated play-mode steering verification |
| `BikeGameHillDiagnostics.cs` | Hill-climb and steering diagnostics harness |
| `BikeGameDebugTools.cs` | Debug utilities |

---

## How to play

1. Tilt the phone left/right to steer.
2. Push the on-screen joystick up to accelerate, pull down to brake or reverse.
3. Hold the BOOST button for extra speed.
4. Ride up ramps to jump — land cleanly to keep your hearts.
5. Avoid the road barriers — each hit costs 1 heart.
6. Collect coins along the way.
7. Reach the soccer ball at the end to complete the level.
8. Lose all 3 hearts and it's game over — tap REPLAY to restart.

## Controls summary

| Input | Action |
|---|---|
| Joystick up | Accelerate forward |
| Joystick down | Brake / reverse |
| Phone tilt left | Steer left |
| Phone tilt right | Steer right |
| Phone tilt back | Wheelie (lift front wheel) |
| BOOST button | Speed boost |
| PEDAL (hold left pad) | Full-power pedal (tilt mode) |
| CAL button | Recalibrate tilt neutral |
| INV button | Invert steering axis |
| TILT button | Toggle tilt/touch steering mode |
| REPLAY button | Restart the level (after game over or level complete) |

---

## Build

The APK can be built in batch mode:

```
Unity.exe -batchmode -nographics -buildTarget Android \
  -projectPath <project> \
  -executeMethod BikeGame.EditorTools.BikeGameApkBuild.Build \
  -logFile <log> -quit
```

Output: `Builds/Android/ForestBikeRide.apk`

## Requirements

- Unity 6000.6.6f1 (Unity 6)
- Universal Render Pipeline (URP)
- Android build support (target API 33+)
- Input System package (active input handler)
