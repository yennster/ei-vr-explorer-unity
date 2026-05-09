# macOS setup — Edge Impulse VR Explorer (Unity, Quest 2)

End-to-end setup on a Mac, from a clean machine to an APK running on a Quest 2.
Total time: ~30 minutes, mostly waiting on Unity downloads.

## 1. Unity Hub + Editor

```bash
brew install --cask unity-hub
open -a "Unity Hub"
```

In Unity Hub:

1. **Sign in** (or create) a Unity ID. Personal license is free.
2. **Installs → Install Editor → 6 LTS** (Unity 6 LTS, `6000.0.x` series).
   This project uses **Unity Sentis 2.1.3** for on-device ONNX inference,
   which requires Unity 6 (Sentis dropped 2022.3 LTS support in 1.4+).
3. On the **Add modules** screen, tick:
   - **Android Build Support**
     - **OpenJDK** (sub-tick)
     - **Android SDK & NDK Tools** (sub-tick)
   - (Optional) **Mac Build Support (Mono)** for testing in the Editor without going to Quest.
4. Click **Install**. ~5 GB download.

> Apple Silicon: Unity Hub auto-picks the Apple-silicon (`-arm64`) editor build. No Rosetta needed.

> If you previously opened the project in Unity 2022.3 LTS, the first time
> you reopen it in Unity 6 you'll get a one-time upgrade prompt. Click
> **Continue**; Unity rewrites `ProjectSettings/ProjectVersion.txt` to your
> Unity 6 version automatically.

## 2. Android platform-tools (`adb`)

```bash
brew install --cask android-platform-tools
adb --version   # confirm on PATH
```

> Unity bundles its own `adb`, buried under
> `~/Library/Application Support/Unity/Hub/Editor/<version>/PlaybackEngines/AndroidPlayer/SDK/platform-tools/`.
> Installing it globally is much less annoying.

## 3. Open the project

```bash
cd /Users/jenny/Work/ei-vr-ar-app/unity-app
open -a "Unity Hub"
```

In Unity Hub: **Open → Add project from disk →** select this directory. Pick
2022.3 LTS as the editor. First-time open takes a few minutes — Unity resolves
`Packages/manifest.json`, imports TextMesh Pro essentials, and compiles the
C# scripts.

## 4. Add Meta XR SDK Core

Meta XR SDK isn't on Unity's package registry, so install it through the Asset
Store:

1. In a browser (or in Unity → Window → Asset Store) open
   https://assetstore.unity.com/packages/tools/integration/meta-xr-core-sdk-269169
   → **Add to My Assets** (free).
2. **Window → Package Manager → My Assets** → find **Meta XR Core SDK** →
   **Download**, then **Import**.
3. Accept the project setup tasks Meta proposes (linker XML, Android manifest, etc.).
   The wizard lives at **Edit → Project Settings → Meta XR**.

Then:

- **Edit → Project Settings → XR Plug-in Management → Android tab** → tick **Oculus**.

## On-device inference: Unity Sentis + ONNX

The Live Inference scene runs ML on the headset using **Unity Sentis** (the
official ML inference engine bundled with Unity) against an **ONNX** model
auto-fetched from Edge Impulse:

1. Companion (`/api/model-bundle/<projectId>`) calls `POST /deploy` on EI
   Studio with `deployType: "onnx"` and polls the build job.
2. Headset downloads the artifact to `persistentDataPath/model.onnx`.
3. [LiveInferenceRunner.cs](Assets/Scripts/LiveInferenceRunner.cs) loads it
   with `ModelLoader.Load(stream)`, creates a Sentis `Worker` on the
   GPUCompute backend, and runs inference every 250 ms over a 2 s sliding IMU
   window.
4. After the Collect & Retrain scene retrains the project, the new ONNX is
   downloaded and the Live Inference scene hot-swaps to it via the
   `AppState.ModelChanged` event — no scene reload required.

The `com.unity.sentis` package is in `Packages/manifest.json` and resolves
automatically. Quest 2 supports the GPUCompute backend (Vulkan compute
shaders); fall back to `BackendType.CPU` if you hit any device-specific
issues — change one line in `LiveInferenceRunner.LoadModel()`.

**Note on input shape**: the runner currently flattens the 6-axis IMU window
to a `(1, windowSamples * 6)` tensor. If your impulse uses a DSP block
(spectral analysis, etc.) before the NN, the model expects DSP features as
input, not raw IMU. The simplest fix is to rebuild the EI deployment with
**EON Compiler**, which bakes the DSP block into the exported model so it
takes raw IMU samples directly.

## 5. Player Settings for Quest 2

**Edit → Project Settings → Player → Android tab**:

- **Other Settings → Configuration**
  - **Scripting Backend**: IL2CPP
  - **Target Architectures**: tick **ARM64**, untick ARMv7
  - **Minimum API Level**: 29
  - **Target API Level**: Automatic (or 32+)
- **XR Plug-in Management → Oculus** → tick **Quest 2** in supported devices.

## 6. Quest 2 dev mode + USB

On the headset:

1. **Meta Quest mobile app** on your phone → Devices → your headset →
   **Headset settings → Developer Mode → ON**.
   (Requires registering as an organization at https://dashboard.meta.com — free, instant.)
2. Plug the Quest 2 into your Mac with a USB-C cable. Inside the headset,
   accept the **Allow USB debugging** prompt and tick **Always allow from this computer**.

On the Mac:

```bash
adb devices
# List of devices attached
# 1WMHHA00000000   device
```

Troubleshooting:

| Symptom | Fix |
|---|---|
| `unauthorized` next to device | Accept prompt inside Quest, then `adb kill-server && adb start-server` |
| Empty device list | Try a different cable — many USB-C cables are charge-only |
| Quest doesn't show prompt | Unplug/replug, or `Settings → System → Developer` in headset to verify dev mode is on |

## 7. Build & run

In Unity:

1. **File → Build Settings → Android** → **Switch Platform** if not already.
2. **Add Open Scenes** until the build list contains: `Setup`, `Explorer`,
   `LiveInference`, `Collect` — in that order. Setup must be index 0.
3. Click **Build and Run** with the Quest connected. Unity builds an APK,
   pushes it via adb, and auto-launches it inside the headset.

Sideload a pre-built APK:

```bash
adb install -r build/EIVR.apk
# Auto-launch (replace with your Player → Identification → Package Name):
adb shell am start -n com.yennster.eivr/com.unity3d.player.UnityPlayerActivity
```

## 8. First-run inside the headset

1. Setup scene loads first. The Companion URL field defaults to
   `https://your-companion.vercel.app` — change it (or hard-code in
   [Assets/Scripts/PairingSetup.cs](Assets/Scripts/PairingSetup.cs))
   to your real companion URL, e.g. `https://explorer.jennyspeelman.dev`.
2. On https://explorer.jennyspeelman.dev, paste your EI API key, get a
   6-digit pairing code.
3. Type the code in-headset, hit pair. Setup persists across restarts.

## Common first-time gotchas

- **Meta XR setup wizard flagging issues** → click **Apply All** in the wizard.
- **"BuildFailedException: Player Settings invalid"** → re-check
  Section 5; the most common miss is forgetting IL2CPP / ARM64.
- **LiteRT package fails to resolve** → confirm the OpenUPM scoped registry
  in `Packages/manifest.json` is intact (`package.openupm.com` →
  `com.google.ai.edge.litert`).
- **APK installs but app crashes on launch** → check
  `adb logcat -s Unity` for the actual stack trace.
