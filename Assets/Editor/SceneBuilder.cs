#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EI.VR.EditorTools
{
    /// <summary>
    /// One-click scene builder for the Edge Impulse VR Explorer.
    ///
    /// Tools → EI VR Explorer → Build All Scenes  generates five scenes
    /// (Setup, Explorer, LiveInference, Collect, ObjectDetection) under
    /// Assets/Scenes/ with the C# components wired to sensible defaults,
    /// and adds them to Build Settings in the right order.
    ///
    /// What this DOESN'T do (still on you, post-generation):
    ///   • Drop in OVRCameraRig from Meta XR SDK in place of the plain Camera.
    ///   • Set up XR Interaction Toolkit ray interactor on a controller (the
    ///     SamplePicker in Explorer scene needs a controller transform with a
    ///     ray pointing forward).
    ///   • Visual polish (button colors, font sizes, layout tweaks).
    /// </summary>
    public static class SceneBuilder
    {
        private const string ScenesRoot = "Assets/Scenes";
        private const string AppStateName = "AppState";

        [MenuItem("Tools/EI VR Explorer/Build All Scenes")]
        public static void BuildAll()
        {
            EnsureFolder(ScenesRoot);
            var paths = new List<string>
            {
                BuildSetupScene(),
                BuildExplorerScene(),
                BuildLiveInferenceScene(),
                BuildCollectScene(),
                BuildObjectDetectionScene(),
            };
            ApplyToBuildSettings(paths);
            Debug.Log($"[SceneBuilder] Built {paths.Count} scenes; added to Build Settings.");
            EditorUtility.DisplayDialog(
                "Scene Builder",
                $"Built {paths.Count} scenes under {ScenesRoot}/ and added them to Build Settings.\n\n" +
                "Next steps:\n" +
                "• Replace the placeholder Camera in each scene with OVRCameraRig from Meta XR SDK.\n" +
                "• Wire up XR Ray Interactor on the right controller in Explorer + ObjectDetection.\n" +
                "• File → Build And Run to deploy to the Quest.",
                "OK");
        }

        // ---- Scene 1: Setup --------------------------------------------------

        public static string BuildSetupScene()
        {
            var scene = NewEmptyScene("Setup");

            EnsureAppState();
            CreateCamera();
            CreateLight();

            var canvas = CreateCanvas("Canvas", worldSpace: false);
            var panel = CreatePanel(canvas.transform, "Panel");

            CreateText(panel.transform, "Title", "Edge Impulse VR Explorer", 28,
                anchorY: 0.85f, height: 80);
            CreateText(panel.transform, "Subtitle",
                "Type the 6-digit pairing code from explorer.jennyspeelman.dev",
                14, anchorY: 0.72f, height: 60);

            var codeInput = CreateInputField(panel.transform, "CodeInput", "code", 24,
                anchorY: 0.55f, height: 60);
            var urlInput = CreateInputField(panel.transform, "CompanionUrlInput",
                "https://explorer.jennyspeelman.dev", 14,
                anchorY: 0.40f, height: 50);

            var statusText = CreateText(panel.transform, "Status", "", 14,
                anchorY: 0.27f, height: 50);
            statusText.color = new Color(0.5f, 0.5f, 0.5f);

            var btn = CreateButton(panel.transform, "PairButton", "Pair", 18,
                anchorY: 0.13f, height: 60);

            var go = new GameObject("PairingSetup");
            go.transform.SetParent(canvas.transform.parent, false);
            var pair = go.AddComponent<PairingSetup>();

            var so = new SerializedObject(pair);
            so.FindProperty("codeInput").objectReferenceValue = codeInput;
            so.FindProperty("companionUrlInput").objectReferenceValue = urlInput;
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.FindProperty("nextScene").stringValue = "Explorer";
            so.ApplyModifiedProperties();

            btn.onClick.AddListener(() => { /* placeholder; runtime hook below */ });
            // Use UnityEvent.AddPersistentListener via UnityEventTools for serialized link.
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                btn.onClick, pair.OnPairButton);

            return SaveScene(scene, "Setup");
        }

        // ---- Scene 2: Explorer ----------------------------------------------

        public static string BuildExplorerScene()
        {
            var scene = NewEmptyScene("Explorer");
            EnsureAppState();
            var cam = CreateCamera();
            cam.transform.position = new Vector3(0, 1.6f, -2.5f);
            CreateLight();

            // FeatureCloud GameObject.
            var cloudGo = new GameObject("FeatureCloud");
            var cloud = cloudGo.AddComponent<FeatureCloud>();
            var cloudSo = new SerializedObject(cloud);
            cloudSo.FindProperty("pointMesh").objectReferenceValue =
                BuiltinMesh(PrimitiveType.Sphere);
            cloudSo.FindProperty("pointMaterial").objectReferenceValue =
                CreateInstancedMaterial();
            cloudSo.ApplyModifiedProperties();

            // Status text on a world-space canvas.
            var canvas = CreateCanvas("StatusCanvas", worldSpace: true);
            canvas.transform.position = new Vector3(0, 2.5f, 0);
            canvas.transform.localScale = Vector3.one * 0.005f;
            var status = CreateText(canvas.transform, "Status",
                "Loading feature cloud…", 36,
                anchorY: 0.5f, height: 200);

            // AxisPicker.
            var pickerGo = new GameObject("AxisPicker");
            var picker = pickerGo.AddComponent<AxisPicker>();
            var pickerSo = new SerializedObject(picker);
            pickerSo.FindProperty("cloud").objectReferenceValue = cloud;
            pickerSo.FindProperty("statusText").objectReferenceValue = status;
            pickerSo.ApplyModifiedProperties();

            // SamplePicker (raycast hookup left as-is; needs XR controller).
            var spGo = new GameObject("SamplePicker");
            var sp = spGo.AddComponent<SamplePicker>();
            var spSo = new SerializedObject(sp);
            spSo.FindProperty("cloud").objectReferenceValue = cloud;
            spSo.FindProperty("rayOrigin").objectReferenceValue = cam.transform;
            spSo.FindProperty("waveformPrefab").objectReferenceValue = BuildWaveformPrefab();
            spSo.ApplyModifiedProperties();

            return SaveScene(scene, "Explorer");
        }

        // ---- Scene 3: LiveInference ----------------------------------------

        public static string BuildLiveInferenceScene()
        {
            var scene = NewEmptyScene("LiveInference");
            EnsureAppState();
            var cam = CreateCamera();
            CreateLight();

            // Wrist HUD canvas.
            var canvas = CreateCanvas("WristHUD", worldSpace: true);
            canvas.transform.position = new Vector3(0, 1.4f, 0.5f);
            canvas.transform.localScale = Vector3.one * 0.003f;

            var label = CreateText(canvas.transform, "Label", "—\n0%", 64,
                anchorY: 0.6f, height: 220);
            var bar = CreateFilledImage(canvas.transform, "ConfidenceBar",
                new Color(0.23f, 0.28f, 0.76f), anchorY: 0.3f, height: 30);

            var go = new GameObject("LiveInferenceRunner");
            var runner = go.AddComponent<LiveInferenceRunner>();
            var so = new SerializedObject(runner);
            so.FindProperty("wristLabel").objectReferenceValue = label;
            so.FindProperty("confidenceBar").objectReferenceValue = bar;
            so.ApplyModifiedProperties();

            return SaveScene(scene, "LiveInference");
        }

        // ---- Scene 4: Collect ----------------------------------------------

        public static string BuildCollectScene()
        {
            var scene = NewEmptyScene("Collect");
            EnsureAppState();
            CreateCamera();
            CreateLight();

            var canvas = CreateCanvas("Canvas", worldSpace: false);
            var panel = CreatePanel(canvas.transform, "Panel");

            CreateText(panel.transform, "Title", "Collect & Retrain", 24,
                anchorY: 0.92f, height: 60);

            var labelInput = CreateInputField(panel.transform, "LabelInput",
                "label (e.g. wave)", 18, anchorY: 0.78f, height: 50);

            var progressText = CreateText(panel.transform, "ProgressText",
                "Idle", 16, anchorY: 0.65f, height: 40);

            var recordBtn = CreateButton(panel.transform, "RecordButton",
                "Record", 18, anchorY: 0.55f, height: 50);

            var uploadBtn = CreateButton(panel.transform, "UploadButton",
                "Upload", 18, anchorY: 0.45f, height: 50);

            var retrainBtn = CreateButton(panel.transform, "RetrainButton",
                "Retrain & Redeploy", 18, anchorY: 0.32f, height: 50);

            var statusText = CreateText(panel.transform, "RetrainStatus",
                "", 14, anchorY: 0.20f, height: 40);

            var logText = CreateText(panel.transform, "RetrainLog",
                "", 10, anchorY: 0.08f, height: 100);
            logText.alignment = TextAlignmentOptions.TopLeft;

            // SampleRecorder.
            var recGo = new GameObject("SampleRecorder");
            var rec = recGo.AddComponent<SampleRecorder>();

            // IngestionUploader.
            var upGo = new GameObject("IngestionUploader");
            var up = upGo.AddComponent<IngestionUploader>();
            var upSo = new SerializedObject(up);
            upSo.FindProperty("recorder").objectReferenceValue = rec;
            upSo.FindProperty("statusText").objectReferenceValue = progressText;
            upSo.ApplyModifiedProperties();

            // RetrainController.
            var rcGo = new GameObject("RetrainController");
            var rc = rcGo.AddComponent<RetrainController>();
            var rcSo = new SerializedObject(rc);
            rcSo.FindProperty("statusText").objectReferenceValue = statusText;
            rcSo.FindProperty("logText").objectReferenceValue = logText;
            rcSo.ApplyModifiedProperties();

            recordBtn.onClick.AddListener(() => rec.Begin(RecordingSensor.RightControllerIMU));
            uploadBtn.onClick.AddListener(() =>
            {
                up.Label = labelInput.text;
                up.Upload();
            });
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                retrainBtn.onClick, rc.OnRetrainButton);

            return SaveScene(scene, "Collect");
        }

        // ---- Scene 5: ObjectDetection --------------------------------------

        public static string BuildObjectDetectionScene()
        {
            var scene = NewEmptyScene("ObjectDetection");
            EnsureAppState();
            var userCam = CreateCamera();
            userCam.transform.position = new Vector3(0, 1.6f, -1f);
            CreateLight();

            // The "demo scene" — table + spawner.
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "DemoTable";
            table.transform.position = new Vector3(0, 0.5f, 1.5f);
            table.transform.localScale = new Vector3(1.5f, 0.05f, 0.8f);

            var spawnerGo = new GameObject("DemoSceneSpawner");
            spawnerGo.transform.position = new Vector3(0, 0.6f, 1.5f);
            var spawner = spawnerGo.AddComponent<DemoSceneSpawner>();
            var spawnSo = new SerializedObject(spawner);
            spawnSo.FindProperty("spawnRoot").objectReferenceValue = spawnerGo.transform;
            spawnSo.ApplyModifiedProperties();

            // Demo Camera that the model sees (separate from the user camera).
            var demoCamGo = new GameObject("DemoCamera");
            demoCamGo.transform.position = new Vector3(0, 1.0f, 0.5f);
            demoCamGo.transform.LookAt(new Vector3(0, 0.6f, 1.5f));
            var demoCam = demoCamGo.AddComponent<Camera>();
            demoCam.clearFlags = CameraClearFlags.SolidColor;
            demoCam.backgroundColor = new Color(0.94f, 0.94f, 0.94f);

            // Viewport quad in the user's view, showing the demo camera feed.
            var viewport = CreateCanvas("Viewport", worldSpace: true);
            viewport.transform.position = new Vector3(0, 1.5f, 0.5f);
            viewport.transform.localScale = Vector3.one * 0.0025f;
            var rt = viewport.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400, 400);

            var feed = CreateRawImage(viewport.transform, "Feed");
            var boxes = CreateBoxesLayer(viewport.transform, "Boxes");

            var overlayGo = new GameObject("BoundingBoxOverlay");
            overlayGo.transform.SetParent(viewport.transform, false);
            var overlay = overlayGo.AddComponent<BoundingBoxOverlay>();
            var oSo = new SerializedObject(overlay);
            oSo.FindProperty("overlayRoot").objectReferenceValue = boxes;
            oSo.FindProperty("boxPrefab").objectReferenceValue = BuildBoxPrefab();
            oSo.ApplyModifiedProperties();

            var runnerGo = new GameObject("ObjectDetectionRunner");
            var runner = runnerGo.AddComponent<ObjectDetectionRunner>();
            var rSo = new SerializedObject(runner);
            rSo.FindProperty("sourceCamera").objectReferenceValue = demoCam;
            rSo.FindProperty("feedDisplay").objectReferenceValue = feed;
            rSo.FindProperty("overlay").objectReferenceValue = overlay;
            rSo.ApplyModifiedProperties();

            return SaveScene(scene, "ObjectDetection");
        }

        // ---- helpers --------------------------------------------------------

        private static Scene NewEmptyScene(string name)
        {
            var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            s.name = name;
            return s;
        }

        private static string SaveScene(Scene scene, string name)
        {
            var path = $"{ScenesRoot}/{name}.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(path), Path.GetFileName(path));
        }

        private static void EnsureAppState()
        {
            if (Object.FindFirstObjectByType<AppState>() != null) return;
            var go = new GameObject(AppStateName);
            go.AddComponent<AppState>();
        }

        private static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0, 1.6f, -1f);
            var cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            return cam;
        }

        private static Light CreateLight()
        {
            var go = new GameObject("Directional Light");
            go.transform.rotation = Quaternion.Euler(50, -30, 0);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            return l;
        }

        private static Canvas CreateCanvas(string name, bool worldSpace)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = worldSpace ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static GameObject CreatePanel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.2f, 0.1f);
            rt.anchorMax = new Vector2(0.8f, 0.9f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
            return go;
        }

        private static TMP_Text CreateText(Transform parent, string name, string text,
            float fontSize, float anchorY = 0.5f, float height = 60)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.05f, anchorY);
            rt.anchorMax = new Vector2(0.95f, anchorY);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0, height);
            rt.anchoredPosition = Vector2.zero;
            return t;
        }

        private static TMP_InputField CreateInputField(Transform parent, string name,
            string placeholder, float fontSize, float anchorY, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            var input = go.AddComponent<TMP_InputField>();

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.1f, anchorY);
            rt.anchorMax = new Vector2(0.9f, anchorY);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0, height);
            rt.anchoredPosition = Vector2.zero;

            // Text component.
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = new Vector2(0, 0); textRt.anchorMax = new Vector2(1, 1);
            textRt.offsetMin = new Vector2(10, 5); textRt.offsetMax = new Vector2(-10, -5);
            var textComp = textGo.AddComponent<TextMeshProUGUI>();
            textComp.fontSize = fontSize;
            textComp.color = Color.white;
            input.textComponent = textComp;

            // Placeholder.
            var phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(go.transform, false);
            var phRt = (RectTransform)phGo.transform;
            phRt.anchorMin = new Vector2(0, 0); phRt.anchorMax = new Vector2(1, 1);
            phRt.offsetMin = new Vector2(10, 5); phRt.offsetMax = new Vector2(-10, -5);
            var phComp = phGo.AddComponent<TextMeshProUGUI>();
            phComp.fontSize = fontSize;
            phComp.text = placeholder;
            phComp.color = new Color(0.5f, 0.5f, 0.5f);
            input.placeholder = phComp;

            return input;
        }

        private static Button CreateButton(Transform parent, string name, string label,
            float fontSize, float anchorY, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.23f, 0.28f, 0.76f);
            var btn = go.AddComponent<Button>();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.2f, anchorY);
            rt.anchorMax = new Vector2(0.8f, anchorY);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0, height);
            rt.anchoredPosition = Vector2.zero;
            CreateText(go.transform, "Label", label, fontSize, 0.5f, height);
            return btn;
        }

        private static Image CreateFilledImage(Transform parent, string name, Color color,
            float anchorY, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillAmount = 0.5f;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.1f, anchorY);
            rt.anchorMax = new Vector2(0.9f, anchorY);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0, height);
            rt.anchoredPosition = Vector2.zero;
            return img;
        }

        private static RawImage CreateRawImage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<RawImage>();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return img;
        }

        private static RectTransform CreateBoxesLayer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static Mesh BuiltinMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            return mesh;
        }

        private static Material CreateInstancedMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.enableInstancing = true;
            EnsureFolder("Assets/Materials");
            var path = "Assets/Materials/FeaturePoint.mat";
            AssetDatabase.CreateAsset(mat, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static GameObject BuildWaveformPrefab()
        {
            EnsureFolder("Assets/Prefabs");
            var path = "Assets/Prefabs/Waveform.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = new GameObject("Waveform");
            go.AddComponent<LineRenderer>();
            go.AddComponent<WaveformRenderer>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject BuildBoxPrefab()
        {
            EnsureFolder("Assets/Prefabs");
            var path = "Assets/Prefabs/BoundingBox.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = new GameObject("BoundingBox", typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            img.color = new Color(0.96f, 0.27f, 0.31f, 0.25f); // semi-transparent EI red
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(50, 50);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = new Vector2(0, 1);
            labelRt.anchorMax = new Vector2(1, 1);
            labelRt.pivot = new Vector2(0, 0);
            labelRt.sizeDelta = new Vector2(0, 16);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 12;
            label.color = Color.white;
            label.text = "?";

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void ApplyToBuildSettings(List<string> scenePaths)
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var p in scenePaths)
                scenes.Add(new EditorBuildSettingsScene(p, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
