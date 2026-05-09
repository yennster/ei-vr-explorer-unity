using System.Collections.Generic;
using System.IO;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.XR;

namespace EI.VR
{
    /// <summary>
    /// Loads the ONNX model from AppState.ModelPath and runs it against a
    /// sliding IMU window in real time using Unity Sentis. Drives the wrist
    /// HUD with the current predicted class label and confidence.
    ///
    /// Hot-swaps the model when AppState.ModelChanged fires (after retraining).
    /// </summary>
    public class LiveInferenceRunner : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_Text wristLabel;
        [SerializeField] private UnityEngine.UI.Image confidenceBar;
        [SerializeField] private int sampleRateHz = 62;
        [SerializeField] private int windowMs = 2000;
        [SerializeField] private int strideMs = 250;

        // Class names — populated from the impulse's learn-block. Until we
        // wire that fetch, the indices come straight from the model output.
        [SerializeField] private string[] classNames = { "class_0", "class_1", "class_2" };

        private float[] _ringBuffer;
        private int _ringHead;
        private int _windowSamples;
        private int _strideSamples;
        private int _sinceLastInfer;
        private float _accum;
        private const int Axes = 6;

        private Model _model;
        private Worker _worker;

        private string _currentClass = "loading…";
        private float _currentConfidence;

        private void Start()
        {
            _windowSamples = sampleRateHz * windowMs / 1000;
            _strideSamples = sampleRateHz * strideMs / 1000;
            _ringBuffer = new float[_windowSamples * Axes];
            if (AppState.I != null)
            {
                AppState.I.ModelChanged += LoadModel;
                LoadModel();
            }
        }

        private void OnDestroy()
        {
            if (AppState.I != null) AppState.I.ModelChanged -= LoadModel;
            _worker?.Dispose();
            _model = null;
        }

        private void LoadModel()
        {
            if (string.IsNullOrEmpty(AppState.I?.ModelPath) || !File.Exists(AppState.I.ModelPath))
            {
                _currentClass = "no model";
                return;
            }
            try
            {
                using var stream = new FileStream(AppState.I.ModelPath, FileMode.Open, FileAccess.Read);
                _model = ModelLoader.Load(stream);
                _worker?.Dispose();
                _worker = new Worker(_model, BackendType.GPUCompute);
                _currentClass = "ready";
                Debug.Log($"[Sentis] Model loaded from {AppState.I.ModelPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Sentis] LoadModel failed: {e}");
                _currentClass = "load error";
            }
        }

        private void Update()
        {
            float dt = 1f / sampleRateHz;
            _accum += Time.deltaTime;
            while (_accum >= dt)
            {
                PushSample();
                _accum -= dt;
                _sinceLastInfer++;
                if (_sinceLastInfer >= _strideSamples && _worker != null)
                {
                    _sinceLastInfer = 0;
                    RunInference();
                }
            }
            if (wristLabel != null) wristLabel.text = $"{_currentClass}\n{_currentConfidence:P0}";
            if (confidenceBar != null) confidenceBar.fillAmount = _currentConfidence;
        }

        private void PushSample()
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, devices);
            if (devices.Count == 0) return;
            var d = devices[0];
            d.TryGetFeatureValue(CommonUsages.deviceAcceleration, out var a);
            d.TryGetFeatureValue(CommonUsages.deviceAngularVelocity, out var w);
            int idx = _ringHead * Axes;
            _ringBuffer[idx + 0] = a.x; _ringBuffer[idx + 1] = a.y; _ringBuffer[idx + 2] = a.z;
            _ringBuffer[idx + 3] = w.x; _ringBuffer[idx + 4] = w.y; _ringBuffer[idx + 5] = w.z;
            _ringHead = (_ringHead + 1) % _windowSamples;
        }

        /// <summary>
        /// Flatten the ring buffer in chronological order and feed it to the
        /// model. Model expects shape (1, windowSamples * 6) for a typical EI
        /// motion classifier; adjust if your impulse uses a different shape.
        /// </summary>
        private void RunInference()
        {
            int total = _windowSamples * Axes;
            var ordered = new float[total];
            int writeIdx = 0;
            // Read from oldest sample (= ringHead) forward
            for (int i = 0; i < _windowSamples; i++)
            {
                int sampleIdx = (_ringHead + i) % _windowSamples;
                System.Array.Copy(_ringBuffer, sampleIdx * Axes, ordered, writeIdx, Axes);
                writeIdx += Axes;
            }

            using var input = new Tensor<float>(new TensorShape(1, total), ordered);
            _worker.Schedule(input);

            using var output = (_worker.PeekOutput() as Tensor<float>).ReadbackAndClone();
            var probs = output.DownloadToArray();

            int best = 0;
            float bestVal = float.NegativeInfinity;
            for (int i = 0; i < probs.Length; i++)
            {
                if (probs[i] > bestVal) { bestVal = probs[i]; best = i; }
            }
            _currentClass = best < classNames.Length ? classNames[best] : $"class_{best}";
            _currentConfidence = Mathf.Clamp01(bestVal);
        }
    }
}
