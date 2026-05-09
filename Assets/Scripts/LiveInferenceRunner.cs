using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR;

namespace EI.VR
{
    /// <summary>
    /// Loads the TFLite model from AppState.ModelPath and runs it against a
    /// sliding IMU window in real time. Drives the wrist HUD with the current
    /// predicted class label and confidence.
    ///
    /// IMPORTANT: This is a skeleton. The TFLite C# API surface depends on
    /// the package version (`com.google.ai.edge.litert`). Wire the calls to
    /// `Interpreter` from that package once installed in Unity.
    /// </summary>
    public class LiveInferenceRunner : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_Text wristLabel;
        [SerializeField] private UnityEngine.UI.Image confidenceBar;
        [SerializeField] private int sampleRateHz = 62;
        [SerializeField] private int windowMs = 2000;
        [SerializeField] private int strideMs = 250;

        private float[] _ringBuffer;
        private int _ringHead;
        private int _windowSamples;
        private int _strideSamples;
        private int _sinceLastInfer;
        private float _accum;

        // Replace with your TFLite interpreter instance once the LiteRT
        // package is added in Unity.
        // private Interpreter _interpreter;

        private string _currentClass;
        private float _currentConfidence;
        private string[] _classNames = { "?" };

        private void Start()
        {
            _windowSamples = sampleRateHz * windowMs / 1000;
            _strideSamples = sampleRateHz * strideMs / 1000;
            _ringBuffer = new float[_windowSamples * 6]; // 6 axes (acc+gyr)
            if (AppState.I != null)
            {
                AppState.I.ModelChanged += LoadModel;
                LoadModel();
            }
        }

        private void OnDestroy()
        {
            if (AppState.I != null) AppState.I.ModelChanged -= LoadModel;
        }

        private void LoadModel()
        {
            if (string.IsNullOrEmpty(AppState.I?.ModelPath) || !File.Exists(AppState.I.ModelPath)) return;
            // var modelBytes = File.ReadAllBytes(AppState.I.ModelPath);
            // _interpreter?.Dispose();
            // _interpreter = new Interpreter(modelBytes, new InterpreterOptions { threads = 2 });
            Debug.Log("Model reloaded: " + AppState.I.ModelPath);
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
                if (_sinceLastInfer >= _strideSamples)
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
            int idx = _ringHead * 6;
            _ringBuffer[idx + 0] = a.x; _ringBuffer[idx + 1] = a.y; _ringBuffer[idx + 2] = a.z;
            _ringBuffer[idx + 3] = w.x; _ringBuffer[idx + 4] = w.y; _ringBuffer[idx + 5] = w.z;
            _ringHead = (_ringHead + 1) % _windowSamples;
        }

        private void RunInference()
        {
            // TODO: flatten ring buffer in chronological order, run DSP block
            // (the EI WASM bundle exposes this; for TFLite-only export the
            // model itself includes feature extraction if "EON Compiler" was
            // used at build time), then invoke _interpreter.
            //
            // Placeholder: cycle through class names so the HUD has something
            // to display before TFLite is wired up.
            if (_classNames.Length > 1)
            {
                _currentClass = _classNames[(int)(Time.time) % _classNames.Length];
                _currentConfidence = 0.5f + 0.5f * Mathf.PerlinNoise(Time.time, 0);
            }
            else
            {
                _currentClass = "model not loaded";
                _currentConfidence = 0f;
            }
        }
    }
}
