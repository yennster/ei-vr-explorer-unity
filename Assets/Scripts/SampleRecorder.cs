using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace EI.VR
{
    public enum RecordingSensor
    {
        LeftControllerIMU,
        RightControllerIMU,
        HeadsetIMU,
        Microphone,
    }

    /// <summary>
    /// Captures a fixed-length window of IMU or microphone samples from the
    /// chosen Quest source. Output is a Recording with sensor metadata and a
    /// values matrix shaped [N_samples][N_axes], ready for IngestionUploader.
    ///
    /// IMU sources sample-by-sample at <c>imuRateHz</c> on the Update loop.
    /// Microphone uses Unity's Microphone API at <c>micRateHz</c> (default
    /// 16 kHz mono — EI's standard for keyword spotting / sound classification),
    /// and the resulting AudioClip is read into a 1-axis values matrix when
    /// the recording window finishes.
    /// </summary>
    public class SampleRecorder : MonoBehaviour
    {
        [Header("IMU")]
        [SerializeField] private int imuRateHz = 62;
        [SerializeField] private int imuLengthMs = 2000;

        [Header("Microphone")]
        [SerializeField] private int micRateHz = 16000;
        [SerializeField] private int micLengthMs = 1000;

        public bool IsRecording { get; private set; }
        public float Progress01 { get; private set; }
        public Recording LatestResult { get; private set; }

        private RecordingSensor _sensor;

        // ---- IMU state ----
        private List<float[]> _imuValues;
        private int _imuSamplesNeeded;
        private float _imuAccum;
        private float _imuIntervalSec;

        // ---- mic state ----
        private string _micDevice;
        private AudioClip _micClip;
        private int _micStartSample;
        private float _micStartTime;
        private float _micEndTime;

        public void Begin(RecordingSensor sensor)
        {
            if (IsRecording) return;
            _sensor = sensor;
            Progress01 = 0f;
            IsRecording = true;
            if (sensor == RecordingSensor.Microphone) BeginMic();
            else BeginImu();
        }

        private void Update()
        {
            if (!IsRecording) return;
            if (_sensor == RecordingSensor.Microphone) TickMic();
            else TickImu();
        }

        // ---- IMU path --------------------------------------------------------

        private void BeginImu()
        {
            _imuIntervalSec = 1f / imuRateHz;
            _imuSamplesNeeded = Mathf.RoundToInt(imuRateHz * (imuLengthMs / 1000f));
            _imuValues = new List<float[]>(_imuSamplesNeeded);
            _imuAccum = 0f;
        }

        private void TickImu()
        {
            _imuAccum += Time.deltaTime;
            while (_imuAccum >= _imuIntervalSec && _imuValues.Count < _imuSamplesNeeded)
            {
                _imuValues.Add(ReadImu(_sensor));
                _imuAccum -= _imuIntervalSec;
            }
            Progress01 = _imuValues.Count / (float)_imuSamplesNeeded;
            if (_imuValues.Count >= _imuSamplesNeeded)
            {
                IsRecording = false;
                LatestResult = new Recording
                {
                    sensors = ImuSensorMeta(),
                    values = _imuValues.ToArray(),
                    intervalMs = 1000f / imuRateHz,
                };
            }
        }

        private static float[] ReadImu(RecordingSensor s)
        {
            switch (s)
            {
                case RecordingSensor.LeftControllerIMU:
                    return ReadDevice(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller);
                case RecordingSensor.RightControllerIMU:
                    return ReadDevice(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller);
                case RecordingSensor.HeadsetIMU:
                    return ReadDevice(InputDeviceCharacteristics.HeadMounted);
                default:
                    return new float[6];
            }
        }

        private static float[] ReadDevice(InputDeviceCharacteristics filter)
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(filter, devices);
            if (devices.Count == 0) return new float[6];
            var d = devices[0];
            d.TryGetFeatureValue(CommonUsages.deviceAcceleration, out var a);
            d.TryGetFeatureValue(CommonUsages.deviceAngularVelocity, out var w);
            return new[] { a.x, a.y, a.z, w.x, w.y, w.z };
        }

        private static CompanionClient.Sensor[] ImuSensorMeta() => new[]
        {
            new CompanionClient.Sensor { name = "accX", units = "m/s2" },
            new CompanionClient.Sensor { name = "accY", units = "m/s2" },
            new CompanionClient.Sensor { name = "accZ", units = "m/s2" },
            new CompanionClient.Sensor { name = "gyrX", units = "rad/s" },
            new CompanionClient.Sensor { name = "gyrY", units = "rad/s" },
            new CompanionClient.Sensor { name = "gyrZ", units = "rad/s" },
        };

        // ---- mic path --------------------------------------------------------

        private void BeginMic()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[SampleRecorder] No microphone available");
                IsRecording = false;
                return;
            }
            _micDevice = Microphone.devices[0];
            // Allocate a clip that's at least our window length plus a small margin.
            int clipLengthSec = Mathf.Max(2, Mathf.CeilToInt(micLengthMs / 1000f) + 1);
            _micClip = Microphone.Start(_micDevice, loop: false, lengthSec: clipLengthSec, frequency: micRateHz);
            _micStartTime = Time.time;
            _micEndTime = _micStartTime + (micLengthMs / 1000f);
            _micStartSample = Microphone.GetPosition(_micDevice);
        }

        private void TickMic()
        {
            float elapsed = Time.time - _micStartTime;
            float total = (_micEndTime - _micStartTime);
            Progress01 = Mathf.Clamp01(elapsed / total);
            if (Time.time < _micEndTime) return;

            // Stop and read.
            int samplesNeeded = Mathf.RoundToInt(micRateHz * (micLengthMs / 1000f));
            Microphone.End(_micDevice);

            var raw = new float[_micClip.samples];
            _micClip.GetData(raw, 0);

            int count = Mathf.Min(samplesNeeded, raw.Length - _micStartSample);
            var values = new float[count][];
            for (int i = 0; i < count; i++) values[i] = new[] { raw[_micStartSample + i] };

            LatestResult = new Recording
            {
                sensors = new[] { new CompanionClient.Sensor { name = "audio", units = "wav" } },
                values = values,
                intervalMs = 1000f / micRateHz,
            };
            IsRecording = false;
            _micDevice = null;
            _micClip = null;
        }

        public class Recording
        {
            public CompanionClient.Sensor[] sensors;
            public float[][] values;
            public float intervalMs;
        }
    }
}
