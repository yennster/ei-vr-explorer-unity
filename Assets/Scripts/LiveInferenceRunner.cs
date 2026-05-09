using System.Collections.Generic;
using System.IO;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.XR;

namespace EI.VR
{
    public enum InputModality
    {
        Motion,
        Audio,
    }

    /// <summary>
    /// Loads the ONNX model from AppState.ModelPath and runs it against a
    /// sliding window of either IMU or microphone samples in real time using
    /// Unity Sentis. Drives the wrist HUD with the current predicted class
    /// label and confidence.
    ///
    /// Modality is set in the inspector (default: Motion). For Audio, raw
    /// 16 kHz mono samples are fed straight to the model — works when the EI
    /// export was built with EON Compiler so the DSP step (MFCC/MFE) lives
    /// inside the ONNX. Without EON Compiler the input shape will not match.
    ///
    /// Hot-swaps the model when AppState.ModelChanged fires (after retraining).
    /// </summary>
    public class LiveInferenceRunner : MonoBehaviour
    {
        [Header("Output HUD")]
        [SerializeField] private TMPro.TMP_Text wristLabel;
        [SerializeField] private UnityEngine.UI.Image confidenceBar;

        [Header("Modality")]
        [SerializeField] private InputModality modality = InputModality.Motion;

        [Header("Motion settings")]
        [SerializeField] private int motionRateHz = 62;
        [SerializeField] private int motionWindowMs = 2000;
        [SerializeField] private int motionStrideMs = 250;

        [Header("Audio settings")]
        [SerializeField] private int audioRateHz = 16000;
        [SerializeField] private int audioWindowMs = 1000;
        [SerializeField] private int audioStrideMs = 250;

        [Header("Class names (in model output order)")]
        [SerializeField] private string[] classNames = { "class_0", "class_1", "class_2" };

        // ---- shared inference state ----
        private Model _model;
        private Worker _worker;
        private string _currentClass = "loading…";
        private float _currentConfidence;

        // ---- motion ring buffer ----
        private const int MotionAxes = 6;
        private float[] _motionRing;
        private int _motionRingHead;
        private int _motionWindowSamples;
        private int _motionStrideSamples;
        private float _motionAccum;
        private int _motionSinceInfer;

        // ---- audio capture ----
        private AudioClip _micClip;
        private string _micDevice;
        private int _audioWindowSamples;
        private int _audioStrideSamples;
        private int _audioReadHead;
        private int _audioSinceInfer;
        private float[] _audioBuffer;

        private void Start()
        {
            if (modality == InputModality.Motion)
            {
                _motionWindowSamples = motionRateHz * motionWindowMs / 1000;
                _motionStrideSamples = motionRateHz * motionStrideMs / 1000;
                _motionRing = new float[_motionWindowSamples * MotionAxes];
            }
            else
            {
                _audioWindowSamples = audioRateHz * audioWindowMs / 1000;
                _audioStrideSamples = audioRateHz * audioStrideMs / 1000;
                _audioBuffer = new float[_audioWindowSamples];
                StartMic();
            }

            if (AppState.I != null)
            {
                AppState.I.ModelChanged += LoadModel;
                LoadModel();
            }
        }

        private void OnDestroy()
        {
            if (AppState.I != null) AppState.I.ModelChanged -= LoadModel;
            StopMic();
            _worker?.Dispose();
            _model = null;
        }

        // ---- model lifecycle --------------------------------------------------

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
                Debug.Log($"[Sentis] Model loaded ({modality}) from {AppState.I.ModelPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Sentis] LoadModel failed: {e}");
                _currentClass = "load error";
            }
        }

        // ---- update tick ------------------------------------------------------

        private void Update()
        {
            if (_worker == null) { UpdateHud(); return; }

            if (modality == InputModality.Motion) TickMotion();
            else TickAudio();

            UpdateHud();
        }

        private void UpdateHud()
        {
            if (wristLabel != null) wristLabel.text = $"{_currentClass}\n{_currentConfidence:P0}";
            if (confidenceBar != null) confidenceBar.fillAmount = _currentConfidence;
        }

        // ---- motion path ------------------------------------------------------

        private void TickMotion()
        {
            float dt = 1f / motionRateHz;
            _motionAccum += Time.deltaTime;
            while (_motionAccum >= dt)
            {
                PushMotionSample();
                _motionAccum -= dt;
                _motionSinceInfer++;
                if (_motionSinceInfer >= _motionStrideSamples)
                {
                    _motionSinceInfer = 0;
                    RunMotionInference();
                }
            }
        }

        private void PushMotionSample()
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, devices);
            if (devices.Count == 0) return;
            var d = devices[0];
            d.TryGetFeatureValue(CommonUsages.deviceAcceleration, out var a);
            d.TryGetFeatureValue(CommonUsages.deviceAngularVelocity, out var w);
            int idx = _motionRingHead * MotionAxes;
            _motionRing[idx + 0] = a.x; _motionRing[idx + 1] = a.y; _motionRing[idx + 2] = a.z;
            _motionRing[idx + 3] = w.x; _motionRing[idx + 4] = w.y; _motionRing[idx + 5] = w.z;
            _motionRingHead = (_motionRingHead + 1) % _motionWindowSamples;
        }

        private void RunMotionInference()
        {
            int total = _motionWindowSamples * MotionAxes;
            var ordered = new float[total];
            int writeIdx = 0;
            for (int i = 0; i < _motionWindowSamples; i++)
            {
                int sampleIdx = (_motionRingHead + i) % _motionWindowSamples;
                System.Array.Copy(_motionRing, sampleIdx * MotionAxes, ordered, writeIdx, MotionAxes);
                writeIdx += MotionAxes;
            }
            using var input = new Tensor<float>(new TensorShape(1, total), ordered);
            Invoke(input);
        }

        // ---- audio path -------------------------------------------------------

        private void StartMic()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[Sentis] No microphone found");
                _currentClass = "no mic";
                return;
            }
            _micDevice = Microphone.devices[0];
            // ~10s loop buffer is plenty; we only ever read the last `audioWindowSamples`.
            _micClip = Microphone.Start(_micDevice, loop: true, lengthSec: 10, frequency: audioRateHz);
            Debug.Log($"[Sentis] Mic started: {_micDevice} @ {audioRateHz} Hz");
        }

        private void StopMic()
        {
            if (_micDevice != null && Microphone.IsRecording(_micDevice))
                Microphone.End(_micDevice);
            _micDevice = null;
            _micClip = null;
        }

        private void TickAudio()
        {
            if (_micClip == null) return;
            int writeHead = Microphone.GetPosition(_micDevice);
            // Number of new samples since last tick (handle wrap).
            int clipSamples = _micClip.samples;
            int newSamples = (writeHead - _audioReadHead + clipSamples) % clipSamples;
            if (newSamples == 0) return;
            _audioReadHead = writeHead;
            _audioSinceInfer += newSamples;
            if (_audioSinceInfer < _audioStrideSamples) return;
            _audioSinceInfer = 0;

            // Read the most recent window.
            ReadLastWindow(_audioBuffer, writeHead, clipSamples);
            using var input = new Tensor<float>(new TensorShape(1, _audioWindowSamples), _audioBuffer);
            Invoke(input);
        }

        private void ReadLastWindow(float[] dst, int writeHead, int clipSamples)
        {
            int start = (writeHead - _audioWindowSamples + clipSamples) % clipSamples;
            if (start + _audioWindowSamples <= clipSamples)
            {
                _micClip.GetData(dst, start);
            }
            else
            {
                int firstChunk = clipSamples - start;
                var tmp = new float[clipSamples];
                _micClip.GetData(tmp, 0);
                System.Array.Copy(tmp, start, dst, 0, firstChunk);
                System.Array.Copy(tmp, 0, dst, firstChunk, _audioWindowSamples - firstChunk);
            }
        }

        // ---- shared invoke ---------------------------------------------------

        private void Invoke(Tensor<float> input)
        {
            _worker.Schedule(input);
            using var output = (_worker.PeekOutput() as Tensor<float>).ReadbackAndClone();
            var probs = output.DownloadToArray();
            int best = 0;
            float bestVal = float.NegativeInfinity;
            for (int i = 0; i < probs.Length; i++)
                if (probs[i] > bestVal) { bestVal = probs[i]; best = i; }
            _currentClass = best < classNames.Length ? classNames[best] : $"class_{best}";
            _currentConfidence = Mathf.Clamp01(bestVal);
        }
    }
}
