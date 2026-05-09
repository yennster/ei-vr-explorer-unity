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
the **Unity 6 LTS** editor. First-time open takes a few minutes — Unity
resolves `Packages/manifest.json` (Sentis, XRI, Input System, Newtonsoft, the
Unity built-in modules), imports TextMesh Pro essentials, and compiles the
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
   (Requires registering as a verified developer organization at
   https://developers.meta.com/horizon/manage — free, takes a couple of
   minutes. The legacy URL https://developer.oculus.com/manage/ also works
   and redirects there.)
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

## 7. Build the scenes (one click)

Open the project in Unity 6, wait for the editor to finish compiling, then:

1. **Tools → EI VR Explorer → Build All Scenes**.
2. Confirm the dialog.

The script generates:

- `Assets/Scenes/{Setup,Explorer,LiveInference,Collect,ObjectDetection}.unity`
- `Assets/Prefabs/{Waveform,BoundingBox}.prefab`
- `Assets/Materials/FeaturePoint.mat`

It also:
- Drops the **OVRCameraRig** prefab from Meta XR SDK into every scene
  (resolved via `AssetDatabase.FindAssets("OVRCameraRig t:Prefab")`, so it
  works regardless of where Meta puts the package internally).
- Wires `SamplePicker.rayOrigin` in the Explorer scene to the rig's
  **RightHandAnchor** for controller-based pointing.
- Auto-populates **`DemoSceneSpawner.propPrefabs`** in ObjectDetection
  with the glTF prefabs from `Assets/Models/glTF/`.
- Adds all five scenes to **Build Settings** in the right order
  (Setup at index 0).

If Meta XR SDK isn't installed yet, the script logs a warning and uses a
placeholder `Camera` instead of OVRCameraRig — install
`com.meta.xr.sdk.core`, then re-run **Build All Scenes** to upgrade in
place.

## 8. Build & run

In Unity:

1. **File → Build Settings → Android** → **Switch Platform** if not already.
2. Click **Build and Run** with the Quest connected. Unity builds an APK,
   pushes it via adb, and auto-launches it inside the headset.

Sideload a pre-built APK:

```bash
adb install -r build/EIVR.apk
# Auto-launch (replace with your Player → Identification → Package Name):
adb shell am start -n com.yennster.eivr/com.unity3d.player.UnityPlayerActivity
```

## 9. First-run inside the headset

1. Setup scene loads first. The Companion URL field is pre-filled with
   `https://explorer.jennyspeelman.dev` (change in
   [Assets/Scripts/PairingSetup.cs](Assets/Scripts/PairingSetup.cs)
   if you fork the companion).
2. On https://explorer.jennyspeelman.dev, paste your EI API key, get a
   6-digit pairing code.
3. Type the code in-headset, hit pair. Setup persists across restarts.

## On-device inference: TFLite → ONNX → Sentis

Edge Impulse doesn't expose ONNX as a deploy block for most projects, so the
companion does **server-side TFLite → ONNX conversion** before the headset
sees the model. Full pipeline:

1. Companion **`/api/build-deployment/<projectId>`** (clicked from the page,
   or auto-called) discovers a TFLite-bearing target — looks in priority
   order for `arduino`, `android-cpp`, `wasm-browser-simd`, `wasm`, `zip` —
   and triggers an EI build with `engine: tflite` (NOT `tflite-eon` —
   EON output is a custom binary that isn't standard TFLite).
2. Companion **`/api/model-bundle/<projectId>`** (called by the headset):
   - Downloads the EI deploy zip via the Studio API.
   - Unzips and parses `tflite-trained.{h,cpp}` — the TFLite model is
     embedded as a C byte array. [tflite-extract.ts](../web-companion/src/lib/tflite-extract.ts)
     pulls those bytes out.
   - POSTs the raw TFLite bytes to **`/api/convert`** (a Python serverless
     function on the same Vercel project) which runs `tflite2onnx`.
   - Streams the resulting ONNX bytes back to the headset.
3. Headset writes them to `persistentDataPath/model.onnx`.
4. [LiveInferenceRunner.cs](Assets/Scripts/LiveInferenceRunner.cs) loads it
   with `ModelLoader.Load(stream)`, creates a Sentis `Worker` on the
   GPUCompute backend, and runs inference every 250 ms over a sliding window.
5. After the Collect & Retrain scene retrains the project, the headset
   re-fetches the model bundle (fresh conversion runs server-side again) and
   the Live Inference scene hot-swaps to it via `AppState.ModelChanged`.

The `com.unity.sentis` 2.1.3 package is in `Packages/manifest.json` and
resolves automatically. Quest 2 supports the GPUCompute backend (Vulkan
compute shaders); fall back to `BackendType.CPU` if you hit any device-specific
issues — change one line in `LiveInferenceRunner.LoadModel()`.

### What kind of EI projects this works for

| Impulse | Status | DSP handling |
|---|---|---|
| **Object detection (FOMO)** | ✓ clean | No DSP — RT readback feeds the model directly |
| **Image classification** | ✓ clean | Same as FOMO |
| **Motion + Spectral Analysis** | ✓ via C# DSP | [SpectralAnalysisExtractor.cs](Assets/Scripts/SpectralAnalysisExtractor.cs) reimplements EI's block |
| **Audio + MFE** | ✓ via C# DSP | [MFEExtractor.cs](Assets/Scripts/MFEExtractor.cs) reimplements EI's block |
| **Audio + MFCC** | ✓ via C# DSP | [MFCCExtractor.cs](Assets/Scripts/MFCCExtractor.cs) wraps MFE + DCT-II |
| **Anything with EON Compiler ON** | ✗ doesn't convert | Rebuild with EON off |

### DSP parameter matching (motion + audio)

The C# extractors are pragmatic reimplementations of EI's blocks — they
follow the same recipe but aren't bit-exact. To get reasonable results on
a model trained against EI's reference DSP, **match the inspector params
to your impulse**.

> **Auto-config from `metadata.json`** — if you got the model via the
> [Unity Sentis deployment block](https://github.com/yennster/ei-unity-sentis-block)
> (Enterprise), the bundle's `metadata.json` carries the impulse's DSP
> block parameters and class names. Call
> `BundleMetadataLoader.Apply(bundle, runner)` at startup and the inspector
> configs auto-populate — no manual copying. See
> [Assets/Scripts/BundleMetadataLoader.cs](Assets/Scripts/BundleMetadataLoader.cs)
> for the API.

**Motion (Spectral Analysis):**

In Studio → your impulse → **Spectral Analysis** block, note these values
and set the matching fields on `LiveInferenceRunner.spectralConfig`:

| EI Studio field | Inspector field |
|---|---|
| Number of peaks | `peakCount` |
| Filter cut-off (low) | `lowFrequencyHz` |
| Filter cut-off (high) | `highFrequencyHz` (0 = Nyquist) |
| Power edges | `powerEdges` (Hz, in ascending order) |
| Window size / sample rate | `motionWindowMs`, `motionRateHz` (top-level fields) |

The runner emits per-axis features in this order:
`[RMS, skew, kurtosis, peak1_freq, peak1_mag, ..., peakN_mag, band1_power, band2_power, ...]`
then concatenates all 6 axes. Total feature length is what your trained NN
expects as input.

**Audio (MFE):**

In Studio → your impulse → **Audio (MFE)** block, copy:

| EI Studio field | Inspector field (`mfeConfig`) |
|---|---|
| Frame length | `frameLengthSec` (e.g. 0.02) |
| Frame stride | `frameStrideSec` (e.g. 0.01) |
| Filter number | `numFilters` |
| FFT length | `fftSize` |
| Low frequency | `lowFrequencyHz` |
| High frequency | `highFrequencyHz` (0 = Nyquist) |
| Pre-emphasis coefficient | `preEmphasis` (default 0.97) |

The Audio modality reshapes the result to `(1, numFrames, numFilters, 1)`
to match EI's NN input layout.

**Audio (MFCC):** same fields as MFE on `mfccConfig`, plus `numCepstral`
(typically 13) — the number of cepstral coefficients to keep after the
DCT. EI's MFCC block produces `(1, numFrames, numCepstral, 1)`. The
extractor uses orthonormal DCT-II (matches scipy/librosa default).

### Sanity-check the feature length

If Sentis throws `Tensor shape mismatch on input 0` when the model runs,
your `Spectral` / `MFE` config doesn't match what the impulse expected.
The error message includes both shapes — work backwards from there to
adjust `peakCount`, `powerEdges`, `numFilters`, etc.

### Two modalities, one app

The runner has an `InputModality` enum exposed in the inspector:

| Modality | Sampling | Window | Tensor shape fed to Sentis |
|---|---|---|---|
| **Motion** | Right-controller IMU @ 62 Hz, 6 axes (acc + gyr) | 2 s, 250 ms stride | `(1, windowSamples * 6)` |
| **Audio** | Quest mic @ 16 kHz mono via `Microphone.Start` | 1 s, 250 ms stride | `(1, audioWindowSamples)` |

Pick the right modality on the `LiveInferenceRunner` GameObject in the
LiveInference scene before building. The Collect scene's `SampleRecorder`
mirrors this — choose the matching `RecordingSensor`.

### EON Compiler is essentially required

Both modalities feed Sentis **raw samples**. If your impulse uses a DSP
block (motion: spectral analysis; audio: MFCC/MFE/spectrogram), the exported
ONNX expects DSP features, not raw samples — and Sentis will throw a shape
mismatch on the first inference. Fix:

1. In Edge Impulse Studio → **Deployment**.
2. Toggle **EON Compiler: ON** before clicking Build.
3. EON bakes the DSP step into the ONNX so it accepts raw IMU/audio
   directly. Same model, different export.

You only need to do this once per project — the companion's auto-build will
keep using the EON-compiled output thereafter.

### Audio: extra Quest setup

For audio impulses (keyword spotting, sound classification, etc.):

1. **Set modality to Audio** in the inspector (LiveInferenceRunner +
   SampleRecorder).
2. **Android microphone permission** — Quest 2 needs `RECORD_AUDIO`. Unity
   adds it automatically once it sees a `Microphone` API call in compiled
   scripts. Verify after a build:
   ```bash
   adb shell dumpsys package com.yennster.eivr | grep RECORD_AUDIO
   # expect: android.permission.RECORD_AUDIO  granted=true
   ```
   On first launch the headset prompts for mic permission. If you accidentally
   deny, re-grant from **Settings → Apps → \<your app\> → Permissions**.
3. **Sample rate** — 16 kHz mono is EI's default for audio. Override via
   `audioRateHz` / `micRateHz` if your project uses something else.

## Object detection on Quest 2 (synthetic camera demo)

Quest 2 doesn't expose its passthrough cameras to apps (Meta gates that to
Quest 3 / 3S via the Camera Access API), so the object-detection mode is a
**synthetic-scene demo** — every part is real Edge Impulse + Sentis ML, but
the camera is a virtual Unity Camera instead of the real headset cameras.

### Scene assembly (one-time, in the Unity Editor)

Create a new scene `ObjectDetection.unity` with:

1. A **Demo Camera** GameObject (just a `Camera`) pointed at a virtual
   "tabletop" — a quad with some props on top.
2. The `DemoSceneSpawner` component on a parent GameObject. Either:
   - Leave `propPrefabs` empty → it'll spawn random colored Unity primitives
     (cube/sphere/capsule/cylinder) so the demo runs out of the box, or
   - Drag in your own prefabs (USDZ-derived — see below) for nicer visuals.
3. A **world-space Canvas** placed in the user's view. Inside it:
   - A `RawImage` covering the canvas. This shows the Demo Camera's feed.
   - A child `RectTransform` (the "boxes layer") that exactly overlays the
     RawImage rect. Add the `BoundingBoxOverlay` component, point its
     `overlayRoot` at this rect, and assign a small `boxPrefab` (an empty
     GameObject with an Image set to a transparent fill + thin colored
     outline + a TMP\_Text child for the label).
4. The `ObjectDetectionRunner` component on any GameObject. Wire:
   - `sourceCamera` → the Demo Camera
   - `feedDisplay` → the RawImage
   - `overlay` → the BoundingBoxOverlay
   - `inputSize` → match your impulse (FOMO defaults to **96**, sometimes 160)
5. Add the new scene to **Build Settings → Scenes In Build**.

The Live Inference scene's `LiveInferenceRunner` continues to handle Motion
and Audio — the object-detection runner is intentionally separate so the
image preprocessing and box overlay code stay isolated from the IMU/audio
sliding-window code.

### 3D models for the object-detection demo

8 free Khronos glTF sample models (Duck, BoomBox, DamagedHelmet, Avocado,
Lantern, WaterBottle, AntiqueCamera, Corset) ship in `Assets/Models/glTF/`.
All CC0 or CC-BY 4.0; `com.unity.cloud.gltfast` imports them automatically.

If they're missing (e.g. fresh clone with the .glb files gitignored), pull
them with:

```bash
cd unity-app
./tools/fetch_glb_demos.sh
```

The `Build All Scenes` menu picks them up automatically and wires them
into `DemoSceneSpawner.propPrefabs`. If the array ends up empty, the
spawner falls back to spawning random colored primitives so the scene
still works.

### Edge Impulse FOMO export caveats

The Image modality assumes the EI ONNX is a **FOMO** model exported with
EON Compiler:

- **Build with EON Compiler ON** (same Build button on the companion as the
  motion/audio path).
- FOMO outputs a per-cell heatmap shaped `(1, H, W, numClasses + 1)` (the
  +1 channel is "background"). [FomoOutputParser.cs](Assets/Scripts/FomoOutputParser.cs)
  thresholds + flood-fills to turn the heatmap into bounding boxes.
- For non-FOMO architectures (YOLOv5 / YOLOv8 from EI), you'd need a
  different output parser — not included here yet.

## Common first-time gotchas

- **Build fails: "Could not create asset from .../ovr_icon_break_bb_connection.png:
  File could not be read"** → Meta XR SDK ships a WebP file with a `.png`
  extension. Run `./tools/fix_meta_xr_webp_icons.sh` from the `unity-app`
  directory; it converts the WebP files in `Library/PackageCache/com.meta.xr.sdk.core*`
  to real PNG in place. Re-run this any time you reset `Library/`.
- **Build fails: "Microphone Usage Description is empty"** → handled
  automatically by `Assets/Editor/ProjectSettingsBootstrapper.cs` on
  Editor load. If you somehow still see it, set the field manually under
  Edit → Project Settings → Player → search "microphone".
- **TMP example scripts fail to compile (VertexZoom.cs / TMP_TextSelector_B.cs)**
  → Unity 6 mesh API changed. We deleted the bundled
  `Assets/TextMesh Pro/Examples & Extras/` folder; if it returns after a
  TMP re-import, delete it again.



- **Meta XR setup wizard flagging issues** → click **Apply All** in the wizard.
- **"BuildFailedException: Player Settings invalid"** → re-check
  Section 5; the most common miss is forgetting IL2CPP / ARM64.
- **Sentis throws "input shape mismatch" on first inference** → almost
  always EON Compiler off. Re-deploy from EI with EON on.
- **`com.unity.sentis` fails to resolve** → make sure you opened the project
  in Unity 6 LTS, not 2022.3. Sentis 2.x requires Unity 6.
- **APK installs but app crashes on launch** → check
  `adb logcat -s Unity` for the actual stack trace.
