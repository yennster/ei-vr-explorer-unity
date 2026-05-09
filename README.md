# Edge Impulse VR Explorer — Unity (Quest 2)

Quest 2 VR client for Edge Impulse. Three things, all from inside the headset:

1. **3D feature explorer** — every training/testing window of your DSP block
   rendered as a room-scale point cloud, colored by class. Pick three feature
   axes (defaults to top-3 by feature importance). Tap a point to see the raw
   sample's waveform float next to it.
2. **Live on-headset inference** — runs your latest TFLite export against the
   Quest IMU in real time, with a wrist HUD showing class + confidence.
3. **Collect → retrain → redeploy from VR** — record fresh IMU samples on the
   headset, label them, upload to your Edge Impulse project via the Ingestion
   API, fire a retrain, watch the build progress, and the live inference
   scene auto-swaps to the new model.

Pairs with the [web-companion](https://github.com/) Next.js app for the
pairing handshake and as a thin proxy for the long-running EI build/train jobs.

## Open the project

See **[SETUP.md](SETUP.md)** for end-to-end macOS setup (Unity Hub install,
Android build modules, Meta XR SDK, Quest 2 dev mode, adb, build & run).
TL;DR:

1. `brew install --cask unity-hub android-platform-tools`
2. Install **Unity 2022.3 LTS** with Android Build Support (OpenJDK + SDK + NDK).
3. Open this directory in Unity Hub.
4. Let Package Manager resolve `Packages/manifest.json`
   (`com.google.ai.edge.litert` is already listed).
5. Add **Meta XR SDK Core** from the Asset Store.

## Build scenes

The C# scripts in `Assets/Scripts/` map to four scenes you'll wire up in the
Unity Editor:

- **Setup.unity** — `PairingSetup` on a Canvas with code + base-URL inputs.
- **Explorer.unity** — `AxisPicker` + `FeatureCloud` (assign a sphere mesh
  and an instanced material) + `SamplePicker` on the right XR controller.
- **LiveInference.unity** — `LiveInferenceRunner` on an empty GameObject,
  with a TMP wrist label child.
- **Collect.unity** — `SampleRecorder`, `IngestionUploader`, `RetrainController`,
  driven by Canvas buttons for label/category/sensor/record/upload/retrain.

Add all four to **Build Settings → Scenes In Build** in that order.

## Player Settings

- Platform: **Android**, target architecture **ARM64**.
- XR Plug-in Management → **Oculus** ticked.
- Minimum API Level **29** (Android 10).
- Internet access **Required** (for EI API calls).

## Run on Quest 2

```bash
adb devices                     # confirm headset is connected
adb install -r build/EIVR.apk   # after Build And Run, or sideload manually
```

Open the app, paste the 6-digit pairing code from the companion site, and
you're in.

## Status / TODOs

- LiteRT (`com.google.ai.edge.litert`) interpreter wiring in
  `LiveInferenceRunner.RunInference` — currently stubbed.
- DSP block evaluation before TFLite invocation. Easiest path: rebuild the
  EI deployment with **EON Compiler** so the `.tflite` includes the DSP step.
- QR scanner for the Setup scene (e.g. ZXing.Net for Unity).
- Microphone recording path in `SampleRecorder` for audio models.
