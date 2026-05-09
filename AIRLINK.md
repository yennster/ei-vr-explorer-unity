# Air Link / Quest Link development (Windows only)

If you're on **Windows**, you can use Meta's Air Link (wireless) or Quest
Link (wired) to run Unity's **Play mode directly inside the Quest** —
skipping the APK build step entirely during iteration. macOS doesn't
support either; Mac users have to use the APK build path documented in
[SETUP.md](SETUP.md).

## What Air Link / Quest Link actually does

- **Streams the editor's Play mode** to the headset via a Meta-branded
  PCVR pipeline. You hit Play in Unity, the rendering happens on the
  Windows GPU, the frames are streamed to the Quest's displays.
- **Does NOT install APKs**. For that you still use USB or wireless adb
  (covered below).

Use Air Link / Quest Link for fast iteration on scene logic. Use APK
deploy when you want the build to actually run **on** the Quest's
Snapdragon (e.g. testing real on-device performance, distributing to
testers, demoing without your PC nearby).

## Hardware + software prerequisites

| Requirement | Why |
|---|---|
| Windows 10 or 11 | Meta Quest Link app is Windows-only |
| Discrete GPU (NVIDIA RTX 2060 / RX 5700 or better) | PCVR streaming is GPU-bound |
| Wi-Fi 6 or 6E router, 5 GHz band, **Mac/PC on Ethernet** | Latency-sensitive; 2.4 GHz is too slow |
| Quest plugged into the same network as the PC | Air Link is local-network only |
| Meta Quest Link app installed | https://www.meta.com/quest/setup/ |
| Same Meta account on PC + headset | Required for pairing |

If you don't have Wi-Fi 6, use the **wired Quest Link cable** (USB-C 3.x)
instead of Air Link — same in-editor Play mode benefit, no Wi-Fi
bottleneck.

## One-time setup

### On the Windows PC

1. Install **Meta Quest Link** from
   <https://www.meta.com/quest/setup/>.
2. Run it, sign in with the same Meta account that's on your Quest.
3. Open the app's **Settings → Beta** → enable **Air Link** if you want
   wireless. (Quest Link cable mode works without this toggle.)

### On the Quest

1. **Settings → Quick Settings → Quest Link** (or "Air Link") →
   pair with your PC. Approve the pairing prompt that pops up on the PC.
2. Once paired, the headset shows your PC in the Quest Link panel.
3. Tap **Connect** to enter Link mode — you'll see the Meta Quest Link
   home environment streamed from your PC.

## Daily workflow with Unity

1. Put the Quest on, hit **Connect** in the Quest Link panel.
2. On your PC, open the Unity project. Make sure **XR Plug-in Management
   → OpenXR** with **Oculus Touch** interaction profile is enabled in
   Project Settings. (The SceneBuilder auto-runs this; you don't have to
   re-do it.)
3. In Unity, hit **Play ▶︎**. Within ~1 second the editor's Play mode
   appears inside the headset.
4. Iterate: edit scripts/scenes, hit Play again, verify in-headset.

For changes that need real on-Quest behavior (microphone permission
prompts, sensor sampling at native rates, performance profiling), build
the APK and deploy via either path below.

## APK deployment over Wi-Fi (separate from Air Link)

Air Link doesn't push APKs. Use **wireless adb** for that — same as on
macOS, same Unity bundled `adb`.

### Wireless Debugging pairing (Quest software v44+)

1. **In headset**: Settings → Developer → **Wireless Debugging** → on.
2. Tap **"Pair device with pairing code"** — shows IP, port, and 6-digit
   code.
3. **In Windows terminal (PowerShell or cmd)**:
   ```powershell
   $ADB = "C:\Program Files\Unity\Hub\Editor\6000.0.x\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
   & $ADB pair 192.168.1.42:<pair-port>
   # paste the 6-digit code when prompted
   & $ADB connect 192.168.1.42:<adb-port>
   & $ADB devices
   # expect: 192.168.1.42:<port>   device
   ```
4. In Unity → **File → Build Settings → Run Device** dropdown, pick the
   wireless device. Build And Run pushes over Wi-Fi.

### Legacy (USB-once) wireless adb

If your Quest software is older than v44, you need a one-time USB pair:

```powershell
$ADB = "C:\Program Files\Unity\Hub\Editor\6000.0.x\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"

# Plug Quest in via USB once
& $ADB devices                              # confirm it's listed

# Get IP
& $ADB shell ip route | findstr "src"

# Switch to TCP mode + connect over Wi-Fi
& $ADB tcpip 5555
& $ADB connect 192.168.1.42:5555
& $ADB devices                              # now shows the wireless device

# Unplug USB; build wirelessly from Unity going forward
```

## Comparison: Mac vs Windows development workflow

| Capability | macOS | Windows |
|---|---|---|
| Build APK + Build And Run via USB | ✅ | ✅ |
| Build APK + push wirelessly via adb | ✅ | ✅ |
| In-editor Play streamed to headset (Air Link / Quest Link) | ❌ not supported by Meta | ✅ |
| Quest Link cable (wired in-editor Play) | ❌ | ✅ |

For most iteration you want Air Link or the Link cable. macOS users have
to do the APK round-trip.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Air Link option missing in Quest Quick Settings | Enable it under Settings → System → Quest Link → Air Link |
| Air Link laggy / stuttering | Plug PC into router via Ethernet; switch to 5 GHz/Wi-Fi 6; reduce render scale in the Meta Quest Link PC app |
| Play mode in Unity but headset still shows Quest Link home | Quest Link app on PC must be in foreground, "Connect" must be active in headset, then hit Play |
| `adb devices` empty after `tcpip 5555` | Network firewall blocking port 5555. On Windows: allow `adb.exe` through Windows Defender Firewall |
| Wireless connection drops mid-build | Wi-Fi roaming or sleep — pin the Quest to one access point; disable Wi-Fi power save on the PC |

## What's NOT in this guide

- Setting up the Quest itself (dev mode, account) — see
  [SETUP.md §5](SETUP.md) (the cross-platform Quest dev mode + USB
  section). It uses adb commands but the dev mode setup is the same on
  Windows.
- Meta Quest Link cable purchase decisions — any USB-C 3.x cable rated
  for 5 Gbps + data works; you don't need Meta's official one.
- Unity Editor + Meta XR SDK install on Windows — basically identical to
  the macOS instructions in [SETUP.md](SETUP.md), except `brew` becomes
  `winget` or downloads from the Unity Hub installer.
