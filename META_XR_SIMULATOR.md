# Meta XR Simulator on macOS

Meta XR Simulator is a desktop application that simulates a Quest at the
OpenXR layer, so you can run the editor's Play mode without a real
headset. **This is the closest macOS equivalent to Windows-only Air
Link** — same iteration loop (hit Play → see the scene), no APK round-trip,
no headset required.

## What it's good for vs. not

| ✅ Works in the simulator | ⚠️ Limited / fake | ❌ Doesn't work meaningfully |
|---|---|---|
| Pairing scene (UI flow, code entry) | Microphone (uses Mac's real mic — fine for audio impulses) | Motion impulse inference (controller IMU is fake — predictions on flat zero-data are garbage) |
| Feature Explorer (EI API calls + 3D rendering) | Touch controller buttons (mapped to keyboard/mouse) | Real on-device performance profiling (use the real Quest for that) |
| Object Detection demo (synthetic scene + Sentis inference is real) | Hand tracking (no real hands; controllers only) | Collect & Retrain (recording IMU samples that are all zeros gives EI useless training data) |

**Bottom line**: use the simulator to verify UI flows, the build runs end
to end, the model loads, the boundaries between scenes work. Use the
real headset for any serious ML / sensor work.

## Prerequisites

- macOS Apple Silicon (M1/M2/M3/M4). The simulator ships an `arm64` build.
- Unity 6 LTS opened on this project (already set up — see
  [SETUP.md](SETUP.md)).
- Project's `Packages/manifest.json` already has:
  - `com.unity.xr.openxr` 1.13.0 or later ✓ (we're on 1.16.1)
  - `com.meta.xr.sdk.core` v66 or later ✓ (we're on 201.0.0)
- The OpenXR + Meta XR setup we did during scene wiring still applies.

## Install (one-time)

The Meta XR Simulator is now a **standalone macOS app**, not a UPM
package — Meta deprecated the UPM package.

1. Download the Apple-silicon installer from:
   <https://developers.meta.com/horizon/downloads/package/meta-xr-simulator-mac-arm/>
2. Open the `.dmg` and drag **Meta XR Simulator** into `/Applications`.
3. The first time you launch it, macOS Gatekeeper may block it
   (right-click the app → **Open** → confirm).

## Activate the simulator inside Unity

Once installed, Unity will detect the simulator's OpenXR runtime when
you tell it to look:

1. In Unity, **Meta → Meta XR Simulator → Activate** (or click the
   simulator icon that appears next to the Play button at the top of
   the editor).
2. Watch the Console for:
   ```
   [Meta XR Simulator is activated]
   ```
3. Hit **Play ▶︎** in Unity. The simulator window opens — a desktop
   view of the Quest's mock-runtime, with the active scene rendered
   into it.

> Note: when activated through Unity, the slider in the standalone
> simulator UI will *not* indicate active status. That's expected — the
> activation is happening at the OpenXR runtime level, the simulator UI
> just doesn't reflect it.

## Daily workflow

```
Edit scripts/scenes
  ↓
Hit Play in Unity
  ↓
Meta XR Simulator window opens, scene runs in mock-Quest viewport
  ↓
Use keyboard/mouse to look around + simulate controller buttons
  ↓
Stop Play, edit, repeat
```

Default keyboard/mouse mappings (visible in the simulator's HUD):

- **Mouse**: head look
- **WASD**: head translate
- **Hold left mouse + drag**: simulate pointing with right controller
- **Spacebar**: trigger button on the active controller
- Buttons / thumbsticks: see the simulator's on-screen overlay

## Returning to the real headset

When you're ready to test on actual hardware:

1. **Meta → Meta XR Simulator → Deactivate** in Unity.
2. Either Build And Run (USB or wireless adb) for an APK on the Quest,
   or — if you're on Windows — re-enable Air Link / Quest Link.

Alternating between simulator and real headset is fine; just toggle
Activate/Deactivate per session.

## Notes specific to this project

- **Pairing scene**: works fully. The pairing code from
  https://explorer.jennyspeelman.dev gets recognized and stored;
  the Setup scene transitions to Explorer normally.
- **Feature Explorer**: the EI API calls happen over Wi-Fi, the 3D
  point cloud renders. Picking a point requires the simulated
  controller raycast — see the SamplePicker note below.
- **`SamplePicker` controller raycast**: the simulator binds the right
  controller to the keyboard's spacebar by default, but the
  `<XRController>{RightHand}/triggerPressed` action might map
  differently in the simulator. If the trigger doesn't fire on
  spacebar, open **Window → Analysis → Input Debugger**, find the
  simulated XR controller, and verify which action path responds.
- **Live Inference (motion)**: the runner will start, the model will
  load, but predictions are based on flat-zero IMU. Don't take any
  on-screen result seriously — verify with the real headset.
- **Live Inference (audio)**: the simulator passes through your Mac's
  real microphone, so audio inference produces real predictions. Useful
  for verifying the MFE/MFCC + Sentis pipeline end-to-end.
- **Object Detection demo**: the synthetic-scene camera renders fine,
  bounding boxes appear over detected primitives/glTF props. This is
  the modality that benefits most from simulator iteration.
- **Collect & Retrain**: skip in the simulator. The recorded "samples"
  are flat zeros and would pollute your EI project's data.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `Meta` menu missing | Project Settings → Meta XR setup wizard hasn't completed; click **Apply All** when prompted |
| `Activate` is greyed out | Project's OpenXR plugin or Meta XR SDK is below the required versions — re-resolve `Packages/manifest.json` |
| Simulator window doesn't open when you hit Play | Check Console for `[Meta XR Simulator is activated]`; if missing, re-run **Meta → Meta XR Simulator → Activate** |
| Black screen in simulator | OpenXR runtime conflict (e.g., another XR runtime is set on macOS) — quit other XR apps and retry |
| Trigger button doesn't fire SamplePicker | Check the keybinding in the simulator's overlay; rebind `<XRController>{RightHand}/triggerPressed` in the SamplePicker inspector if needed |

## Comparison: macOS testing options

| Method | In-editor Play | APK on Quest | macOS support |
|---|---|---|---|
| **Meta XR Simulator** | ✅ (this doc) | — | ✅ |
| **USB Build And Run** | — | ✅ (data cable required) | ✅ |
| **Wireless adb** | — | ✅ (over Wi-Fi) | ✅ |
| **Air Link / Quest Link** | ✅ | — | ❌ Windows only — see [AIRLINK.md](AIRLINK.md) |

The simulator and wireless adb together give macOS users a tight
iteration loop: simulator for fast UI work, wireless adb when you need
the real Quest sensors.
